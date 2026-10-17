using System; using System.IO; using System.Linq; using System.Collections.Generic; using System.Threading; using System.Threading.Tasks;
using OpenFusion.Assets; using OpenFusion.Unity25; using OpenFusion.World;
int fails = 0; void Check(bool ok, string m) { Console.WriteLine((ok ? "OK    " : "FALLO ") + m); if (!ok) fails++; }
var man = CachingManifest25.FromMainUnity3d(File.ReadAllBytes(args[1])); var local = new FfCacheBundleSource(args[0]);
// 1. fases
int Shared(string l) => man.Lists[l].Select(e => e.FileName).Distinct().Count(n => !Phases.IsDongFile(n));
var st = new BundleStreamer(man, new Rec());
Check(st.FilesUpTo(Phase.Tutorial).Count == Shared("m_CharacterCreation") + Shared("m_CharacterSelection") + Shared("m_Tutorial"), $"hasta Tutorial: {st.FilesUpTo(Phase.Tutorial).Count} compartidos");
Check(Phases.TutorialMaps(man).Count() == 9, "9 mapas del tutorial dentro de m_Tutorial");
Check(st.FilesUpTo(Phase.PaidZone).Count == st.FilesUpTo(Phase.Tutorial).Count + Shared("m_FreeZone") + Shared("m_PaidZone"), $"hasta PaidZone: {st.FilesUpTo(Phase.PaidZone).Count}");
Check(st.FilesUpTo(Phase.PaidZone).All(n => man.Version(n) != 0) && st.FilesUpTo(Phase.PaidZone).Distinct().Count() == st.FilesUpTo(Phase.PaidZone).Count, "todos en el manifiesto, sin duplicados");
// 2. dongs
Check(st.DongFiles(0, 0).SequenceEqual(new[] { "Map_00_00.unity3d" }), "dong del tutorial: sólo Map_");
Check(st.DongFiles(13, 3).SequenceEqual(new[] { "Map_13_03.unity3d", "DongResources_13_03.resourceFile" }), "dong libre 13,3: Map_ + DongResources_");
Check(st.DongFiles(8, 5).Count == 2, "dong de pago 8,5");
Check(st.DongFiles(7, 15).Count == 0 || !man.IsPaid(7, 15), "dong fuera de zona -> nada (si no es de pago)");
var rec = new Rec(); var st2 = new BundleStreamer(man, rec); await st2.PrefetchAroundAsync(13, 3);
Check(rec.Calls.Count == 1 && rec.Calls[0].Take(2).SequenceEqual(new[] { "Map_13_03.unity3d", "DongResources_13_03.resourceFile" }), "prefetch 3x3: empieza por el propio dong");
Check(rec.Calls[0].Distinct().Count() == rec.Calls[0].Count && rec.Calls[0].All(n => man.Version(n) != 0), $"prefetch sin duplicados, {rec.Calls[0].Count} archivos válidos");
// 3. gestor sobre la caché del launcher
var mgr = new BundleManager(local, man); var store = new AssetStore(); int n = 0;
foreach (var name in man.All.Select(e => e.FileName).Distinct()) { var f = mgr.Load(name); if (f == null) continue; n++; foreach (var kv in f) { try { store.Add(kv.Key, kv.Value); } catch { } } }
Check(n == 11 && mgr.LoadedFiles.Count == 11, $"carga {n} bundles locales");
Check(mgr.TryLocate("mob/npc_arturo.kfm", out var e) && mgr.BundleFor("mob/npc_arturo.kfm") == "Retro_shared_part2.resourceFile", "mob/npc_arturo.kfm -> Retro_shared_part2");
var r = store.Resolve(e.File, e.Target); Check(r.Ok && store.TypeOf(r) == "GameObject", "la entrada resuelve en el AssetStore a un GameObject");
Check(mgr.TryLocate("texture/b10_hexstaff.dds", out var t) && store.TypeOf(store.Resolve(t.File, t.Target)) == "Texture2D", "textura -> Texture2D");
Check(mgr.TryLocate("Icons/nanoicon_01.png", out var ic) && ic.Target.FileId != 0, "icono de nano apunta a otro bundle (FileId>0) y se resuelve por Externals");
Check(!mgr.TryLocate("wear/hat_nunca_visto.nif", out _), "ruta inexistente -> false");
mgr.Release("Retro_shared_part2.resourceFile"); Check(!mgr.TryLocate("mob/npc_arturo.kfm", out _) && !mgr.IsLoaded("Retro_shared_part2.resourceFile"), "Release quita el índice");
mgr.Load("Retro_shared_part2.resourceFile"); Check(mgr.TryLocate("mob/npc_arturo.kfm", out _), "recarga restaura la ruta");
// 4. hilos: DongLoader llama Load en Task.Run mientras la UI consulta rutas
var errs = 0; var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(() => { try { for (int k = 0; k < 20; k++) { if (i % 2 == 0) { mgr.Release("Tutorial.resourceFile"); mgr.Load("Tutorial.resourceFile"); } else mgr.TryLocate("mob/npc_arturo.kfm", out _); } } catch { Interlocked.Increment(ref errs); } })).ToArray(); await Task.WhenAll(tasks);
Check(errs == 0, "Load/Release/TryLocate concurrentes sin excepciones");
return fails;
class Rec : IBundleDownloader { public List<List<string>> Calls = new List<List<string>>();
  public Task EnsureAsync(IEnumerable<string> f, IProgress<DownloadProgress> p = null, int par = 3, CancellationToken ct = default) { Calls.Add(f.ToList()); return Task.CompletedTask; } }
