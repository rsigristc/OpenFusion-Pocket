using System; using System.IO; using System.Linq; using System.Collections.Generic; using OpenFusion.Tables; using OpenFusion.Tables.Gen; using OpenFusion.Unity25; using OpenFusion.World;
// uso: ModelTest <xdtdatas.bin> <bundle>...
var raw = File.ReadAllBytes(args[0]); var r = new PackedReader(raw); r.Header(); var x = XdtTableScript.Read(r);
var cat = ModelCatalog.From(x);
Console.WriteLine($"catalogo: npc={cat.Npcs} nano={cat.Nanos} shiny={cat.Shinies} vehiculo={cat.Vehicles} equipo={cat.Equips}");
var idx = new AssetIndex(); var sfiles = new Dictionary<string, SerializedFile>(); var types = new Dictionary<string,string>(); int fail = 0;
foreach (var path in args.Skip(1)) {
  var bytes = File.ReadAllBytes(path); var files = new Dictionary<string,byte[]>(); try { files = UnityWebBundle.Parse(bytes).Files; } catch { files["x"] = bytes; }
  foreach (var kv in files) { SerializedFile f; try { f = SerializedFile.Parse(kv.Value); } catch { continue; } idx.Add(Path.GetFileName(path), f); sfiles[Path.GetFileName(path)] = f; }
}
Console.WriteLine($"indice local: {idx.Count} rutas");
// por categoria: con modelo / encontrado en el indice local / texturas encontradas
foreach (var g in cat.All().GroupBy(m => m.Kind)) {
  int n = g.Count(), withModel = g.Count(m => m.Model != null), found = g.Count(m => m.Model != null && idx.Has(m.Model));
  int tex = g.Count(m => m.Tex1 != null), texFound = g.Count(m => m.Tex1 != null && idx.Has(m.Tex1));
  Console.WriteLine($"{g.Key,-8} total={n,5} conModelo={withModel,5} modeloEnCacheLocal={found,4} | conTextura={tex,5} texturaEnCacheLocal={texFound,4}");
}
// formato de rutas: ¿todos los modelos hallados apuntan a un GameObject?; ejemplos de rutas faltantes
var found2 = cat.All().Where(m => m.Model != null && idx.Has(m.Model)).Take(5).Select(m => m.ToString()); Console.WriteLine("hallados (ej): " + string.Join(" | ", found2));
var miss = cat.All().Where(m => m.Model != null && !idx.Has(m.Model)).GroupBy(m => m.Model.Substring(0, m.Model.IndexOf('/'))).Select(g => g.Key + ":" + g.Count());
Console.WriteLine("modelos fuera del cache local por directorio: " + string.Join(", ", miss));
// el índice local de mob/ y wear/ y nano/: cuántas rutas NO las referencia ninguna tabla (¿hay otro camino de carga?)
var referenced = new HashSet<string>(cat.All().SelectMany(m => new[] { m.Model, m.ModelFemale }).Where(s => s != null));
foreach (var d in new[] { "mob/", "wear/", "nano/" }) { var all = idx.Keys.Where(k => k.StartsWith(d)).ToList(); var un = all.Where(k => !referenced.Contains(k)).ToList(); Console.WriteLine($"{d} en indice local={all.Count} sin referencia en tablas={un.Count} ej: {string.Join(", ", un.Take(6))}"); }
// Actor/m.kfm, w.kfm
Console.WriteLine($"actor/m.kfm local={idx.Has("actor/m.kfm")} actor/w.kfm local={idx.Has("actor/w.kfm")}");

// cada modelo hallado debe apuntar a un GameObject existente (mismo bundle, o el bundle externo si FileId>0 ya esta cargado) y colgar un subarbol con malla/huesos/animacion
var graphs = new Dictionary<string, SceneGraph25>(); int uniq = 0, ok = 0, ext = 0, notGo = 0, noNode = 0, skinned = 0, animated = 0, noRender = 0;
foreach (var path in cat.All().Where(m => m.Model != null).Select(m => m.Model).Concat(new[]{"actor/m.kfm"}).Distinct()) {
  if (!idx.TryGet(path, out var e)) continue; uniq++;
  var f = sfiles[e.File]; var t = e.Target;
  if (t.FileId != 0) { ext++; continue; }   // apunta a otro bundle (p. ej. el tutorial): se resuelve con AssetStore.Resolve, comprobado en WorldTest
  var o = f.Objects.FirstOrDefault(x => x.PathId == t.PathId);
  if (o == null || f.TypeOf(o)?.Type != "GameObject") { notGo++; continue; }
  if (!graphs.TryGetValue(e.File, out var g)) graphs[e.File] = g = SceneGraph25.Build(f);
  var root = g.ByTransform.Values.FirstOrDefault(n => n.GameObjectId == t.PathId); if (root == null) { noNode++; continue; }
  var all = new List<SceneNode25>(); void W(SceneNode25 n) { all.Add(n); foreach (var c in n.Children) W(c); } W(root);
  ok++; if (all.Any(n => n.Skin != null)) skinned++; if (all.Any(n => n.Anim != null)) animated++; if (!all.Any(n => n.Skin != null || n.Mesh.PathId != 0)) noRender++;
}
Console.WriteLine($"modelos unicos hallados={uniq}: GameObject+subarbol OK={ok} (con malla skinned={skinned}, con Animation={animated}, sin ninguna malla={noRender}), destino en otro bundle={ext}, no GameObject={notGo}, sin nodo={noNode}");
fail += notGo + noNode;

// --- NPC humanoides (all_hnpc) -> ActorLook
var hr = new PackedReader(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(args[0]), "all_hnpc.bin"))); hr.Header(); var hn = HNpcTableScript.Read(hr);
int rows = hn.TableElement.Length, withPieces = 0, females = 0, wrongSet = 0, allWear = 0, hasAny = 0; var wearAll = new HashSet<string>();
foreach (var h in hn.TableElement) { var l = ActorLook.FromHNpc(h); if (l.Female) females++; if (l.Cloth.Any(c => c != null)) withPieces++;
  if (l.Height < 0 || l.Height > 1 || l.Shape < 0 || l.Shape > 1) wrongSet++;
  foreach (var w in l.Cloth.Concat(l.Attach).Where(x => x != null)) wearAll.Add(w);
  var req = l.RequiredPaths().ToList(); if (req.Any(r => idx.Has(r))) hasAny++; }
int wearLocal = wearAll.Count(w => idx.Has(w));
Console.WriteLine($"all_hnpc: filas={rows} mujeres={females} con alguna pieza={withPieces} altura/forma fuera de 0..1={wrongSet}; piezas wear/ distintas={wearAll.Count} (en cache local={wearLocal}); filas con algún asset local={hasAny}");
if (wrongSet > 0) fail++;
return fail;
