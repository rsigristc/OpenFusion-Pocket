using System; using System.IO; using System.Linq; using System.Net; using System.Net.Http; using System.Security.Cryptography; using System.Threading; using System.Threading.Tasks; using System.Collections.Generic;
using OpenFusion.Api; using OpenFusion.Assets; using OpenFusion.Unity25; using OpenFusion.World;
// Servidor simulado: sirve main.unity3d (un UnityWeb/LZMA real) bajo varios nombres. Comprueba descarga, caché, hash, tamaño, versión hija/padre y Load.
var real = File.ReadAllBytes(args[0]); string sha = Convert.ToHexString(SHA256.HashData(real)).ToLowerInvariant();
int fails = 0; void Check(bool ok, string m) { Console.WriteLine((ok ? "OK    " : "FALLO ") + m); if (!ok) fails++; }
var h = new Fake(real);
BundleInfo B(string n, long size = -1, string hash = null) => new BundleInfo { name = n, compressed_info = new HashInfo { size = size < 0 ? real.Length : size, hash = hash ?? sha } };
var parent = new VersionManifest { uuid = "p-uuid", asset_url = "https://cdn.test/p/", bundles = new List<BundleInfo> { B("Tutorial.resourceFile"), B("Map_01_00.unity3d"), B("old_only.resourceFile") } };
var child = new VersionManifest { uuid = "c-uuid", parent_uuid = "p-uuid", asset_url = "https://cdn.test/c/", bundles = new List<BundleInfo> { B("Map_01_00.unity3d"), B("bad_size.resourceFile", 5), B("bad_hash.resourceFile", -1, new string('0', 64)) } };
var cat = BundleCatalog.FromChain(new[] { child, parent });
Check(cat.Count == 5, $"catalogo fusiona la cadena: {cat.Count} bundles (esperado 5: Map_01_00 esta en las dos versiones)");
Check(cat.Find("map_01_00.unity3d").Owner.uuid == "c-uuid", "la version hija gana sobre la padre");
Check(cat.Find("Tutorial.resourceFile").Owner.uuid == "p-uuid", "un bundle sin cambios se sirve de la version padre");
Check(cat.Find("MAP_01_00.UNITY3D") != null && cat.Find("Map01_00.unity3d") != null, "busqueda sin mayusculas y sin '_'");
Check(cat.Find("nope.resourceFile") == null, "desconocido -> null");
var tmp = Path.Combine(Path.GetTempPath(), "bc_" + Guid.NewGuid().ToString("N")); var cache = new BundleCache(tmp, cat, h);
var miss = cache.Missing(new[] { "Tutorial.resourceFile", "Map_01_00.unity3d", "nope.resourceFile" }, out var unk);
Check(miss.Count == 2 && unk.Count == 1, $"Missing: faltan {miss.Count}, desconocidos {unk.Count}");
Check(cache.Load("Tutorial.resourceFile") == null, "Load sin descargar -> null");
DownloadProgress last = default; var prog = new SyncProgress(p => last = p);
await cache.EnsureAsync(new[] { "Tutorial.resourceFile", "Map_01_00.unity3d" }, prog);
Check(h.Count == 2 && last.FilesDone == 2 && last.BytesDone == 2L * real.Length, $"descarga: {h.Count} peticiones, progreso {last.FilesDone}/{last.FilesTotal}, {last.BytesDone} B");
Check(h.Urls.Contains("https://cdn.test/c/Map_01_00.unity3d") && h.Urls.Contains("https://cdn.test/p/Tutorial.resourceFile"), "URL = asset_url de la version dueña");
var files = cache.Load("Tutorial.resourceFile"); var direct = UnityWebBundle.Parse(real).Files;
Check(files != null && files.Count == direct.Count && files.All(kv => kv.Value.AsSpan().SequenceEqual(direct[kv.Key])), $"Load devuelve los mismos archivos internos ({files?.Count})");
await cache.EnsureAsync(new[] { "Tutorial.resourceFile", "Map_01_00.unity3d" }); Check(h.Count == 2, "segunda llamada no vuelve a descargar");
Check(cache.IsCached("Map_01_00.unity3d") && !cache.IsCached("old_only.resourceFile"), "IsCached");
bool threw = false; try { await cache.EnsureAsync(new[] { "bad_size.resourceFile" }); } catch (IOException) { threw = true; }
Check(threw && !cache.IsCached("bad_size.resourceFile") && !Directory.GetFiles(tmp, "*.part", SearchOption.AllDirectories).Any(), "tamaño distinto -> error y sin .part");
threw = false; try { await cache.EnsureAsync(new[] { "bad_hash.resourceFile" }); } catch (IOException) { threw = true; }
Check(threw && !cache.IsCached("bad_hash.resourceFile"), "hash distinto -> error");
// corrupcion posterior: se trunca el archivo -> deja de contar como en cache y se vuelve a bajar
var p1 = cache.PathFor(cat.Find("Tutorial.resourceFile")); File.WriteAllBytes(p1, new byte[10]); Check(!cache.IsCached("Tutorial.resourceFile") && cache.Load("Tutorial.resourceFile") == null, "archivo truncado -> no válido");
int before = h.Count; await cache.EnsureAsync(new[] { "Tutorial.resourceFile" }); Check(h.Count == before + 1 && cache.IsCached("Tutorial.resourceFile"), "se re-descarga el corrupto");
// main.unity3d de la versión
var mv = new VersionManifest { uuid = "m-uuid", main_file_url = "https://cdn.test/m/main.unity3d", main_file_info = new HashInfo { size = real.Length, hash = sha } };
var mainBytes = await cache.EnsureMainFileAsync(mv); int hm = h.Count; var again = await cache.EnsureMainFileAsync(mv);
Check(mainBytes.AsSpan().SequenceEqual(real) && h.Count == hm, "EnsureMainFileAsync descarga una vez y reutiliza");
threw = false; try { await cache.EnsureMainFileAsync(new VersionManifest { uuid = "m2", main_file_url = "https://cdn.test/m2/main.unity3d", main_file_info = new HashInfo { size = 5, hash = sha } }); } catch (IOException) { threw = true; }
Check(threw, "main.unity3d con tamaño distinto -> error");
// plan de zona real con el manifiesto de main.unity3d
if (args.Length > 1) {
  var man = CachingManifest25.FromMainUnity3d(File.ReadAllBytes(args[1])); var names = man.ForZone(ZoneType.Tutorial).Select(e => e.FileName).Distinct().ToList();
  var all = man.All.Select(e => e.FileName).Distinct().Count();
  Check(names.Count == 20, $"plan zona Tutorial: {names.Count} archivos (de {all} del manifiesto)");
  var fake = BundleCatalog.FromChain(new[] { new VersionManifest { uuid = "x", asset_url = "https://cdn.test/x/", bundles = man.All.Select(e => e.FileName).Distinct().Select(n => B(n.ToLowerInvariant())).ToList() } });
  var c2 = new BundleCache(Path.Combine(tmp, "z"), fake, h); var m2 = c2.Missing(man.ForZone(ZoneType.Paid).Select(e => e.FileName), out var u2);
  Check(u2.Count == 0 && m2.Count == all, $"los {all} nombres del manifiesto se resuelven contra un catalogo en minusculas (Missing={m2.Count}, desconocidos={u2.Count})");
}
Directory.Delete(tmp, true); return fails;
class SyncProgress : IProgress<DownloadProgress> { Action<DownloadProgress> a; public SyncProgress(Action<DownloadProgress> a) { this.a = a; } public void Report(DownloadProgress p) => a(p); }
class Fake : HttpMessageHandler { byte[] data; public int Count; public List<string> Urls = new List<string>(); public Fake(byte[] d) { data = d; }
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { lock (this) { Count++; Urls.Add(r.RequestUri.ToString()); }
    var n = r.RequestUri.ToString(); if (n.Contains("bad_size")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[7]) });
    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) }); } }
