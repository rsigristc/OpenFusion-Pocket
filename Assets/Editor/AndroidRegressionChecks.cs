using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using OpenFusion.Api;
using OpenFusion.Assets;
using OpenFusion.Unity25;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class AndroidRegressionChecks
    {
        sealed class CountingSource : IBundleSource
        {
            public int Reads;
            public Dictionary<string, byte[]> Load(string name) { Reads++; return new Dictionary<string, byte[]> { [name] = new byte[] { 1, 2, 3 } }; }
        }
        sealed class FakeCdn : HttpMessageHandler
        {
            public int Requests, Active, MaxActive; public byte[] Data; public bool WrongHash; public string MissingPrefix;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Interlocked.Increment(ref Requests);
                int active = Interlocked.Increment(ref Active);
                lock (this) MaxActive = Math.Max(MaxActive, active);
                try
                {
                    if (MissingPrefix != null && request.RequestUri.AbsoluteUri.StartsWith(MissingPrefix)) return new HttpResponseMessage(HttpStatusCode.NotFound);
                    await Task.Delay(50, ct);
                    var bytes = (byte[])Data.Clone(); if (WrongHash) bytes[0] ^= 1;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
                }
                finally { Interlocked.Decrement(ref Active); }
            }
        }
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

        public static async Task Run()
        {
            string root = Path.GetFullPath(Path.Combine("Temp", "android-regression-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            var results = new List<string>();
            try
            {
                var fastRoot = Path.Combine(root, "fast");
                var fast = new DecodedBundleCache(fastRoot, 2048);
                int decodes = 0;
                Dictionary<string, byte[]> Decode() { decodes++; return new Dictionary<string, byte[]> { ["file"] = new byte[] { 8, 9, 10 } }; }
                var first = fast.Load("hash-v1", Decode, out bool hit);
                Require(!hit && decodes == 1, "First bundle read must decode original");
                var second = fast.Load("hash-v1", Decode, out hit);
                Require(hit && decodes == 1 && second["file"].SequenceEqual(first["file"]), "Fast cache must bypass decoder and preserve bytes");
                File.WriteAllBytes(Directory.GetFiles(fastRoot, "*.ofc").Single(), new byte[] { 1, 2, 3 });
                fast.Load("hash-v1", Decode, out hit);
                Require(!hit && decodes == 2, "Corrupt derived cache must rebuild from verified original");
                fast.Load("hash-v2", Decode, out hit);
                Require(!hit && decodes == 3, "Changed content identity must never reuse stale data");
                for (int i = 0; i < 30; i++) fast.Load("version-" + i, Decode, out _);
                Require(Directory.GetFiles(fastRoot, "*.ofc").Sum(f => new FileInfo(f).Length) <= 2048 && !Directory.GetFiles(fastRoot, "*.part").Any(), "Derived cache must stay bounded and commit atomically");
                results.Add("PASS: fast bundle cache preserves bytes, skips LZMA on reuse, repairs corruption, invalidates changed versions, and respects disk bound.");
                var protectedRoot = Path.Combine(root, "protected-fast"); var protectedCache = new DecodedBundleCache(protectedRoot, 2048);
                var noise = new byte[800]; new System.Random(37).NextBytes(noise);
                Dictionary<string, byte[]> Shared() => new Dictionary<string, byte[]> { ["shared"] = noise };
                protectedCache.Load("shared", Shared, out _);
                for (int i = 0; i < 12; i++) protectedCache.Load("map-" + i, Shared, out _, true);
                protectedCache.Load("shared", () => throw new Exception("Map traversal evicted shared startup data"), out hit);
                Require(hit && Directory.GetFiles(protectedRoot, "*.ofc").Sum(f => new FileInfo(f).Length) <= 2048, "Map traversal must preserve shared cache and disk bound");
                // The reported issue appeared only on repeated full startup sequences.
                var sequence = new DecodedBundleCache(Path.Combine(root, "sequence"), 65536);
                for (int pass = 0; pass < 3; pass++) for (int i = 0; i < 25; i++)
                {
                    sequence.Load("bundle-" + i, Shared, out hit);
                    Require(hit == (pass > 0), "Repeated full startup must reuse every shared bundle when working set fits");
                }
                results.Add("PASS: three full startup sequences reuse all 25 shared entries; visiting 12 maps preserves shared entries and disk bound.");
                var counting = new CountingSource(); var manager = new BundleManager(counting, new CachingManifest25());
                var resident = manager.Load("resident"); var repeated = manager.Load("resident");
                Require(counting.Reads == 1 && ReferenceEquals(resident["resident"], repeated["resident"]), "Loaded table data must reuse resident arrays");
                manager.Release("resident"); manager.Load("resident");
                Require(counting.Reads == 2, "Releasing a bundle must drop resident lookup");
                results.Add("PASS: repeated bundle requests reuse live byte arrays; release removes reuse references.");
                var bytes = new byte[] { 1, 2, 3, 4, 5, 6 };
                var info = new BundleInfo { compressed_info = new HashInfo { size = bytes.Length, hash = Hash(bytes) } };
                var version = new VersionManifest { uuid = "owner-v1", asset_url = "https://fake.invalid", bundles = new Dictionary<string, BundleInfo> { ["test.resourceFile"] = info } };
                var catalog = BundleCatalog.FromChain(new[] { version });
                using (var cdn = new FakeCdn { Data = bytes })
                {
                    var cache = new BundleCache(Path.Combine(root, "downloads"), catalog, cdn);
                    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => cache.EnsureAsync(new[] { "test.resourceFile" })));
                    Require(cdn.Requests == 1 && cache.IsCached("test.resourceFile"), "Las precargas deben compartir una descarga y verificar su hash.");
                    results.Add("PASS: 8 solicitudes simultaneas -> 1 descarga verificada.");
                    string path = cache.PathFor(catalog.Find("test.resourceFile"));
                    File.WriteAllBytes(path, new byte[bytes.Length]); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(1));
                    await cache.EnsureAsync(new[] { "test.resourceFile" });
                    Require(cdn.Requests == 2 && File.ReadAllBytes(path).SequenceEqual(bytes), "La cache corrupta del mismo tamano debe repararse.");
                    results.Add("PASS: reparacion de cache corrupta del mismo tamano.");
                    bool rejected = false;
                    try { await cache.EnsureAsync(new[] { "missing.resourceFile" }); } catch (IOException) { rejected = true; }
                    Require(rejected, "Un bundle desconocido no debe ignorarse.");
                    results.Add("PASS: bundle desconocido rechazado.");
                }
                using (var cdn = new FakeCdn { Data = bytes, WrongHash = true })
                {
                    var cache = new BundleCache(Path.Combine(root, "bad"), catalog, cdn);
                    bool rejected = false;
                    try { await cache.EnsureAsync(new[] { "test.resourceFile" }); } catch (IOException) { rejected = true; }
                    Require(rejected && !Directory.GetFiles(Path.Combine(root, "bad"), "*.part", SearchOption.AllDirectories).Any(), "Un hash incorrecto debe fallar y retirar el parcial.");
                    results.Add("PASS: hash incorrecto rechazado y parcial eliminado.");
                }
                var many = new VersionManifest { uuid = "many", asset_url = "https://fake.invalid", bundles = Enumerable.Range(0, 12).ToDictionary(i => "item" + i, i => new BundleInfo { compressed_info = info.compressed_info }) };
                using (var cdn = new FakeCdn { Data = bytes })
                {
                    var cache = new BundleCache(Path.Combine(root, "parallel"), BundleCatalog.FromChain(new[] { many }), cdn);
                    await Task.WhenAll(many.bundles.Keys.Select(name => cache.EnsureAsync(new[] { name })));
                    Require(cdn.Requests == 12 && cdn.MaxActive <= 3, "La concurrencia global no debe superar 3 descargas.");
                    results.Add("PASS: 12 precargas independientes, maximo 3 descargas activas.");
                }
                string legacyRoot = Path.Combine(root, "legacy"), old = Path.Combine(legacyRoot, "selected-v2", version.uuid, "test.resourceFile");
                Directory.CreateDirectory(Path.GetDirectoryName(old)); File.WriteAllBytes(old, bytes);
                using (var cdn = new FakeCdn { Data = bytes })
                {
                    var cache = new BundleCache(legacyRoot, catalog, cdn);
                    await cache.EnsureAsync(new[] { "test.resourceFile" });
                    Require(cdn.Requests == 0 && !File.Exists(old) && cache.IsCached("test.resourceFile"), "Migrar debe reutilizar el bundle sin duplicarlo ni descargarlo.");
                    results.Add("PASS: migracion de cache sin descarga ni duplicacion.");
                }
                var patch = new VersionManifest { uuid = "patch-new", asset_url = "https://fake.invalid/current", bundles = new Dictionary<string, BundleInfo> { ["test.resourceFile"] = info } };
                var ancestor = new VersionManifest { uuid = "patch-old", asset_url = "https://fake.invalid/removed", bundles = new Dictionary<string, BundleInfo> { ["test.resourceFile"] = info } };
                var patchedCatalog = BundleCatalog.FromChain(new[] { patch, ancestor });
                Require(patchedCatalog.Find("test.resourceFile").Owner.uuid == ancestor.uuid && patchedCatalog.Find("test.resourceFile").Url == patch.asset_url + "/test.resourceFile", "La URL actual y el propietario de cache deben ser independientes.");
                using (var cdn = new FakeCdn { Data = bytes, MissingPrefix = ancestor.asset_url })
                {
                    var cache = new BundleCache(Path.Combine(root, "removed-ancestor"), patchedCatalog, cdn);
                    await cache.EnsureAsync(new[] { "test.resourceFile" });
                    Require(cache.IsCached("test.resourceFile") && cdn.Requests == 1, "Un CDN antiguo retirado no debe romper una descarga sin cache.");
                    results.Add("PASS: CDN antiguo devuelve 404; descarga usa URL actual y mantiene cache compartida.");
                }
                string aliasRoot = Path.Combine(root, "alias"), aliasFile = Path.Combine(aliasRoot, patch.uuid, "test.resourceFile");
                Directory.CreateDirectory(Path.GetDirectoryName(aliasFile)); File.WriteAllBytes(aliasFile, bytes);
                using (var cdn = new FakeCdn { Data = bytes })
                {
                    var cache = new BundleCache(aliasRoot, patchedCatalog, cdn);
                    await cache.EnsureAsync(new[] { "test.resourceFile" });
                    Require(cdn.Requests == 0 && cache.IsCached("test.resourceFile") && !File.Exists(aliasFile), "Cache de una version equivalente debe migrarse sin duplicacion ni descarga.");
                    results.Add("PASS: cache equivalente de version nueva migrada sin redescarga.");
                }
                // Prueba real de lectura del formato antiguo, sin login ni descargar el juego completo.
                var api = new ServerApi("https://api.ffretrobution.net");
                var server = await api.GetInfo(); var chain = await api.GetChain(server.GameVersions.First(), Path.Combine(root, "metadata"));
                using (var offline = new FakeCdn { Data = bytes })
                {
                    var cachedApi = new ServerApi("https://api.ffretrobution.net", offline);
                    var warmChain = await cachedApi.GetChain(server.GameVersions.First(), Path.Combine(root, "metadata"));
                    Require(offline.Requests == 0 && warmChain[0].uuid == chain[0].uuid && warmChain.Count == chain.Count, "La cadena ya guardada no debe repetir peticiones de red.");
                    results.Add("PASS: metadata de version reutilizada sin llamadas HTTP ni recorrer padres retirados.");
                }
                var liveCatalog = BundleCatalog.FromChain(chain);
                if (chain.Count > 1)
                {
                    var unchanged = chain[0].bundles.FirstOrDefault(kv => chain[1].bundles.TryGetValue(kv.Key, out var old) &&
                        old.compressed_info.hash == kv.Value.compressed_info.hash && old.compressed_info.size == kv.Value.compressed_info.size);
                    Require(unchanged.Key != null && liveCatalog.Find(unchanged.Key).Owner.uuid != chain[0].uuid, "Un patch no debe duplicar bundles identicos de su padre.");
                    var changed = chain[0].bundles.FirstOrDefault(kv => chain[1].bundles.TryGetValue(kv.Key, out var old) && old.compressed_info.hash != kv.Value.compressed_info.hash);
                    Require(changed.Key != null && liveCatalog.Find(changed.Key).Owner.uuid == chain[0].uuid, "El bundle modificado debe usar los datos del patch.");
                    results.Add("PASS: patch reutiliza bundles identicos del padre y conserva la version nueva de los modificados.");
                }
                var live = await Task.Run(() => new BundleCache(Path.Combine(root, "live"), liveCatalog));
                var main = await live.EnsureMainFileAsync(chain[0]);
                var manifest = await Task.Run(() => CachingManifest25.FromMainUnity3d(main));
                string parsedPath = Path.Combine(root, "parsed-manifest.json");
                CachingManifest25.LoadCached(main, parsedPath, chain[0].main_file_info.hash);
                var warmManifest = CachingManifest25.LoadCached(Array.Empty<byte>(), parsedPath, chain[0].main_file_info.hash);
                Require(warmManifest.Lists.Count == manifest.Lists.Count && warmManifest.PaidCoords.SequenceEqual(manifest.PaidCoords), "El manifiesto procesado debe reutilizarse sin descomprimir main.unity3d.");
                results.Add("PASS: main manifest procesado se reutiliza sin repetir LZMA/lectura de objetos.");
                Require(manifest.Lists.Count > 0, "main.unity3d debe contener las fases de descarga.");
                var missing = Phases.Order.SelectMany(p => Phases.Files(manifest, p)).Where(f => liveCatalog.Find(f) == null).Distinct().ToArray();
                Require(missing.Length == 0, "Bundles de fase ausentes del catalogo: " + string.Join(", ", missing));
                results.Add("PASS: CDN Retrobution, main.unity3d " + main.Length + " bytes, " + liveCatalog.Count + " bundles, fases resueltas.");
                results.Add("VERSION: " + chain[0].name + " / " + chain[0].uuid + "; API " + server.ApiVersion);
                await live.EnsureAsync(new[] { "Map_07_08.unity3d" });
                Require(live.IsCached("Map_07_08.unity3d"), "El mapa de la zona reportada debe descargarse desde la version actual.");
                results.Add("PASS: Map_07_08.unity3d descargado y hash verificado usando " + liveCatalog.Find("Map_07_08.unity3d").Url);
                GameCacheInfo.Record(Path.Combine(root, "live"), "test-server", chain[0], liveCatalog);
                var installed = GameCacheInfo.Scan(root, new Dictionary<string, string> { [chain[0].uuid] = chain[0].name });
                var liveRow = installed.FirstOrDefault(r => r.uuid == chain[0].uuid);
                Require(liveRow != null && liveRow.name == chain[0].name && liveRow.downloadedBytes >= main.Length && liveRow.expectedBytes > liveRow.downloadedBytes, "Game Builds debe identificar la version y medir los archivos descargados.");
                results.Add("PASS: Game Builds detecta Retrobution con bytes reales del cache parcial.");
                var tableBundle = liveCatalog.All.First(i => i.Info.name.StartsWith("TableData", StringComparison.OrdinalIgnoreCase));
                await live.EnsureAsync(new[] { tableBundle.Info.name });
                var tables = await Task.Run(() => OpenFusion.Tables.TableLoader.Load(live.Load(tableBundle.Info.name)));
                Require(tables.Xdt.m_pVehicleItemTable.m_pItemData.Length > 0, "La tabla nueva de vehiculos debe poder leerse.");
                var vt = tables.Xdt.m_pVehicleItemTable;
                var petropian = vt.m_pItemData.FirstOrDefault(v => v != null && v.m_iItemName >= 0 && v.m_iItemName < vt.m_pItemStringData.Length &&
                    vt.m_pItemStringData[v.m_iItemName] != null && vt.m_pItemStringData[v.m_iItemName].m_strName.IndexOf("Petropian", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    vt.m_pItemStringData[v.m_iItemName].m_strName.IndexOf("DX", StringComparison.OrdinalIgnoreCase) >= 0);
                Require(petropian != null && petropian.m_iUp_runSpeed == 1500, "DX Petropian debe resolver a velocidad 1500 en las tablas actuales.");
                results.Add("PASS: tablas actuales decodificadas; DX Petropian item " + petropian.m_iItemNumber + ", velocidad " + petropian.m_iUp_runSpeed + ".");

            }
            finally
            {
                Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/android-regression-results.txt", results);
                Directory.Delete(root, true);
            }
            Debug.Log("OpenFusion: regresiones Android OK\n" + string.Join("\n", results));
        }
    }
}
