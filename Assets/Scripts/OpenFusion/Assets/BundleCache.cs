// Descarga y caché en disco de los bundles de un servidor/versión; implementa IBundleSource para DongLoader. Sin dependencias de Unity.
// SIN VERIFICAR contra un servidor real: (1) cómo se corresponden los nombres del manifiesto de versión con los del CachingManifest,
// (2) el algoritmo del campo hash (aquí se deduce por la longitud del hex), (3) que lo descargado sea un UnityWeb/LZMA.
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using OpenFusion.Api;
using OpenFusion.Unity25;
using OpenFusion.World;

namespace OpenFusion.Assets
{
    /// Bundles de la cadena de versiones (la más nueva primero): cada nombre se resuelve a la versión más nueva que lo define.
    public sealed class BundleCatalog
    {
        public sealed class Item { public BundleInfo Info; public VersionManifest Owner; public string DirectUrl; public readonly List<string> CacheVersions = new List<string>(); public string Url => DirectUrl ?? Owner.asset_url.TrimEnd('/') + "/" + Info.name; }
        readonly Dictionary<string, Item> byName = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Item> byStem = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        public int Count => byName.Count;
        public IEnumerable<Item> All => byName.Values;

        public static BundleCatalog FromChain(IEnumerable<VersionManifest> newestFirst)
        {
            var c = new BundleCatalog();
            foreach (var v in newestFirst) if (v.bundles != null) foreach (var kv in v.bundles)
            {
                var b = kv.Value; b.name = kv.Key;
                if (c.byName.TryGetValue(b.name, out var existing))
                {
                    // Full patch manifests also repeat unchanged bundles. Reuse the
                    // ancestor's cache only when its verified content is identical.
                    // Keep the download URL from the newest manifest that defines it.
                    var current = existing.Info.compressed_info; var ancestor = b.compressed_info;
                    if (current != null && ancestor != null && !string.IsNullOrEmpty(current.hash) &&
                        current.size == ancestor.size && string.Equals(current.hash, ancestor.hash, StringComparison.OrdinalIgnoreCase))
                    { existing.Owner = v; existing.Info = b; existing.CacheVersions.Add(v.uuid); }
                    continue;
                }
                var it = new Item { Info = b, Owner = v, DirectUrl = v.asset_url.TrimEnd('/') + "/" + b.name }; it.CacheVersions.Add(v.uuid); c.byName[b.name] = it; var stem = Stem(b.name); if (!c.byStem.ContainsKey(stem)) c.byStem[stem] = it;
            }
            return c;
        }
        static string Stem(string n) { var i = n.LastIndexOf('.'); n = i > 0 ? n.Substring(0, i) : n; return n.Replace("_", "").ToLowerInvariant(); }

        /// fileName del CachingManifest ("Map_01_00.unity3d", "Tutorial.resourceFile"). Primero igualdad sin mayúsculas, luego por nombre sin extensión ni "_".
        public Item Find(string fileName) => byName.TryGetValue(fileName, out var it) ? it : byStem.TryGetValue(Stem(fileName), out it) ? it : null;
    }

    public interface IBundleDownloader { Task EnsureAsync(IEnumerable<string> fileNames, IProgress<DownloadProgress> progress = null, int parallel = 3, CancellationToken ct = default); }

    public struct DownloadProgress { public int FilesDone, FilesTotal; public long BytesDone, BytesTotal; }

    public sealed class BundleCache : IBundleSource, IBundleDownloader
    {
        readonly HttpClient http; readonly string dir; readonly BundleCatalog catalog;
        readonly DecodedBundleCache decoded;
        public Action<string> LoadLog;
        readonly ConcurrentDictionary<string, SemaphoreSlim> fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        readonly ConcurrentDictionary<string, (long length, long ticks)> verified = new ConcurrentDictionary<string, (long, long)>();
        readonly SemaphoreSlim downloadSlots = new SemaphoreSlim(3, 3);

        /// dir: carpeta de este servidor+versión (p. ej. <persistentDataPath>/bundles/<serverId>/<uuid>); los archivos se guardan por uuid de versión dueña.
        public BundleCache(string dir, BundleCatalog catalog, HttpMessageHandler handler = null)
        {
            this.dir = dir; this.catalog = catalog; http = handler == null ? new HttpClient() : new HttpClient(handler, false);
            decoded = new DecodedBundleCache(Path.Combine(dir, "decoded-v1"));
            MigrateLegacyCache();
        }

        void MigrateLegacyCache()
        {
            if (!Directory.Exists(dir)) return;
            // Formato anterior: servidor/versionElegida/versionPropietaria/archivo.
            // Mover archivos verificados reutiliza la descarga sin conservar dos copias.
            var oldRoots = Directory.GetDirectories(dir);
            foreach (var it in catalog.All)
            {
                string destination = PathFor(it);
                if (File.Exists(destination)) continue;
                var aliases = it.CacheVersions.Count > 0 ? it.CacheVersions : new List<string> { it.Owner.uuid };
                var candidates = aliases.Select(uuid => Path.Combine(dir, uuid, Sanitize(it.Info.name)))
                    .Concat(oldRoots.SelectMany(oldRoot => aliases.Select(uuid => Path.Combine(oldRoot, uuid, Sanitize(it.Info.name)))));
                foreach (var old in candidates)
                {
                    if (!Valid(old, it, true)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Move(old, destination);
                    break;
                }
            }
        }

        public string PathFor(BundleCatalog.Item it) => Path.Combine(dir, it.Owner.uuid, Sanitize(it.Info.name));
        static string Sanitize(string n) { foreach (var ch in Path.GetInvalidFileNameChars()) n = n.Replace(ch, '_'); return n; }

        public bool IsCached(string fileName) { var it = catalog.Find(fileName); return it != null && Cached(it); }
        public long SizeOf(string fileName) => catalog.Find(fileName)?.Info.compressed_info?.size ?? 0;

        bool Cached(BundleCatalog.Item it)
        {
            string path = PathFor(it); var file = new FileInfo(path);
            if (!file.Exists) return false;
            var stamp = (file.Length, file.LastWriteTimeUtc.Ticks);
            if (verified.TryGetValue(path, out var old) && old == stamp) return true;
            if (!Valid(path, it, true)) return false;
            verified[path] = stamp;
            return true;
        }

        static bool Valid(string path, BundleCatalog.Item it, bool checkHash)
        {
            if (!File.Exists(path)) return false;
            var size = it.Info.compressed_info?.size ?? 0; if (size > 0 && new FileInfo(path).Length != size) return false;
            return !checkHash || HashMatches(path, it.Info.compressed_info?.hash);
        }

        /// Deduce el algoritmo por la longitud del hex (32 MD5, 40 SHA-1, 64 SHA-256, 128 SHA-512). Longitud desconocida/vacío -> no se puede comprobar (true).
        public static bool HashMatches(string path, string hex)
        {
            if (string.IsNullOrEmpty(hex)) return true;
            HashAlgorithm a; switch (hex.Length) { case 32: a = MD5.Create(); break; case 40: a = SHA1.Create(); break; case 64: a = SHA256.Create(); break; case 128: a = SHA512.Create(); break; default: return true; }
            using (a) using (var fs = File.OpenRead(path)) return string.Equals(BitConverter.ToString(a.ComputeHash(fs)).Replace("-", ""), hex, StringComparison.OrdinalIgnoreCase);
        }

        /// Archivos que faltan (no están en caché o están corruptos) de la lista pedida, con su tamaño. Los desconocidos para el servidor van en `unknown`.
        public List<string> Missing(IEnumerable<string> fileNames, out List<string> unknown)
        {
            var miss = new List<string>(); unknown = new List<string>();
            foreach (var f in fileNames.Distinct(StringComparer.OrdinalIgnoreCase)) { var it = catalog.Find(f); if (it == null) unknown.Add(f); else if (!Cached(it)) miss.Add(f); }
            return miss;
        }

        /// Descarga (en paralelo limitado) lo que falte; escribe a .part, comprueba tamaño y hash y renombra. Lanza si algo no coincide.
        public async Task EnsureAsync(IEnumerable<string> fileNames, IProgress<DownloadProgress> progress = null, int parallel = 3, CancellationToken ct = default)
        {
            var names = fileNames.ToArray();
            var scan = await Task.Run(() => { var m = Missing(names, out var u); return (missing: m, unknown: u); }, ct);
            var missing = scan.missing; var unknown = scan.unknown;
            if (unknown.Count > 0) throw new IOException("Bundles ausentes del manifiesto: " + string.Join(", ", unknown));
            var todo = missing.Select(f => catalog.Find(f)).ToList();
            var p = new DownloadProgress { FilesTotal = todo.Count, BytesTotal = todo.Sum(i => i.Info.compressed_info?.size ?? 0) }; var gate = new object();
            using (var sem = new SemaphoreSlim(Math.Max(1, parallel)))
            {
                await Task.WhenAll(todo.Select(async it =>
                {
                    await sem.WaitAsync(ct);
                    try
                    {
                        await DownloadOne(it, ct);
                        lock (gate) { p.FilesDone++; p.BytesDone += it.Info.compressed_info?.size ?? 0; progress?.Report(p); }
                    }
                    finally { sem.Release(); }
                }));
            }
        }

        async Task DownloadOne(BundleCatalog.Item it, CancellationToken ct)
        {
            var path = PathFor(it); var sem = fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct);
            try
            {
                // Otra precarga puede haber descargado el mismo archivo mientras esperabamos.
                if (await Task.Run(() => Cached(it), ct)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(path)); var tmp = path + ".part";
                await downloadSlots.WaitAsync(ct);
                try
                {
                    using (var r = await http.GetAsync(it.Url, HttpCompletionOption.ResponseHeadersRead, ct))
                    {
                        if (!r.IsSuccessStatusCode) throw new HttpRequestException("HTTP " + (int)r.StatusCode + " (" + r.ReasonPhrase + ") descargando " + it.Info.name + " -> " + it.Url);
                        using (var stream = await r.Content.ReadAsStreamAsync())
                        using (var fs = File.Create(tmp)) await stream.CopyToAsync(fs, 81920, ct);
                    }
                    if (!await Task.Run(() => Valid(tmp, it, true), ct)) throw new IOException("tamaño o hash distinto: " + it.Info.name);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                }
                finally { try { if (File.Exists(tmp)) File.Delete(tmp); } finally { downloadSlots.Release(); } }
            }
            finally { sem.Release(); }
        }

        /// IBundleSource: devuelve los archivos internos del bundle ya descargado (null si no está en caché). Acepta UnityWeb/LZMA o un SerializedFile suelto.
        public Dictionary<string, byte[]> Load(string fileName)
        {
            var it = catalog.Find(fileName); if (it == null) return null; var path = PathFor(it); if (!Cached(it)) return null;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Dictionary<string, byte[]> Decode()
            {
                var raw = File.ReadAllBytes(path);
                try { return new Dictionary<string, byte[]>(UnityWebBundle.Parse(raw).Files); }
                catch (NotSupportedException) { return new Dictionary<string, byte[]> { [fileName] = raw }; }
            }
            var stamp = new FileInfo(path);
            string identity = fileName + ":" + (it.Info.compressed_info?.hash ?? "") + ":" + stamp.Length + ":" + stamp.LastWriteTimeUtc.Ticks;
            bool transient = fileName.StartsWith("Map_", StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("DongResources_", StringComparison.OrdinalIgnoreCase);
            var files = decoded.Load(identity, Decode, out bool hit, transient);
            LoadLog?.Invoke("[load] " + fileName + " " + (hit ? "fast cache" : "LZMA/cache preparation") + " " + watch.ElapsedMilliseconds + "ms");
            return files;
        }

        /// main.unity3d de la versión (contiene el CachingManifest). Se guarda en <dir>/<uuid>/main.unity3d y se verifica tamaño y hash.
        public async Task<byte[]> EnsureMainFileAsync(VersionManifest v, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(v.main_file_url)) throw new InvalidOperationException("la versión no tiene main_file_url");
            var it = new BundleCatalog.Item { Owner = v, DirectUrl = v.main_file_url, Info = new BundleInfo { name = "main.unity3d", compressed_info = v.main_file_info } };
            await DownloadOne(it, ct);
            return await Task.Run(() => File.ReadAllBytes(PathFor(it)), ct);
        }
        public bool IsMainCached(VersionManifest v) => Cached(new BundleCatalog.Item { Owner = v, Info = new BundleInfo { name = "main.unity3d", compressed_info = v.main_file_info } });

        public void Delete(string fileName) { var it = catalog.Find(fileName); if (it != null) { var p = PathFor(it); if (File.Exists(p)) File.Delete(p); } }
    }
}
