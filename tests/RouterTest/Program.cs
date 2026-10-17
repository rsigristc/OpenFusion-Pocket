using System; using System.IO; using System.Linq; using System.Collections.Generic; using OpenFusion.Unity25; using OpenFusion.World;
// uso: RouterTest <ffcache|guid dir> <main.unity3d>
var man = CachingManifest25.FromMainUnity3d(File.ReadAllBytes(args[1])); var src = new FfCacheBundleSource(args[0]);
var idx = new Dictionary<string, AssetIndex>(StringComparer.OrdinalIgnoreCase); var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var e in man.All.GroupBy(e => e.FileName).Select(g => g.First())) {
  var files = src.Load(e.FileName); if (files == null) continue; loaded.Add(e.FileName);
  var ai = new AssetIndex(); foreach (var kv in files) { try { ai.Add(e.FileName, SerializedFile.Parse(kv.Value)); } catch { } } idx[e.FileName] = ai;
}
Console.WriteLine($"bundles locales cargados={loaded.Count}: {string.Join(", ", loaded.OrderBy(x => x))}");
var c = new Contents { Idx = idx, Loaded = loaded }; var router = new ResourceRouter(c);
router.PaidZoneFiles = man.Lists["m_PaidZone"].Select(e => e.FileName).ToList(); router.SetPosition(8 * 512 + 10, 5 * 512 + 10);   // dong 08_05 (el local)
// el cliente pide Icons/ y Help/ con mayúscula; ok = el bundle elegido contiene la ruta (hay rutas duplicadas entre bundles)
int nd = 0, ndOk = 0; int total = 0, agree = 0, nullRoute = 0; var bad = new List<string>(); var cat = new SortedDictionary<string, (int n, int ok)>();
foreach (var (bundle, ai) in idx) foreach (var key in ai.Keys) {
  total++; string inp = key.StartsWith("icons/") ? "Icons" + key.Substring(5) : key.StartsWith("help/") ? "Help" + key.Substring(4) : key; var r = router.Locate(inp); string d = key.Contains('/') ? key.Substring(0, key.IndexOf('/')) : "(raiz)";
  cat.TryGetValue(d, out var t); bool ok = r != null && c.Contains(r, key); cat[d] = (t.n + 1, t.ok + (ok ? 1 : 0));
  if (!key.Contains(".dds")) { nd++; if (ok) ndOk++; }
  if (ok) agree++; else { if (r == null) nullRoute++; if (bad.Count < 14) bad.Add($"{key}: real={bundle} ruta={r ?? "null"}"); }
}
Console.WriteLine($"rutas totales={total} routerAlcanzaLaRuta={agree} ({100.0 * agree / total:F2}%) sinRuta={nullRoute}");
Console.WriteLine($"sin rutas con '.dds' (texturas/materiales que el cliente resuelve por PPtr, no por nombre): {nd} rutas, alcanzadas={ndOk}");
foreach (var kv in cat.OrderByDescending(k => k.Value.n).Take(14)) Console.WriteLine($"  {kv.Key,-24} n={kv.Value.n,5} ok={kv.Value.ok,5}");
foreach (var b in bad) Console.WriteLine("  MAL " + b);
// rutas conocidas
foreach (var p in new[] { "mob/npc_arturo.kfm", "wear/hat_chefhat.nif", "nano/nano_holonano.kfm", "texture/b10_hexstaff.dds", "bgm/00 main theme.ogg", "Icons/nanoicon_01.png", "xdtdatas.asset", "Actor/m.kfm", "map/x" })
  Console.WriteLine($"  {p,-28} -> {router.Locate(p) ?? "null"}");
class Contents : IBundleContents { public Dictionary<string, AssetIndex> Idx; public HashSet<string> Loaded;
  public bool IsLoaded(string b) => Loaded.Contains(b); public bool Contains(string b, string p) => Idx.TryGetValue(b, out var i) && i.Has(p); }
