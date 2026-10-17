using System; using System.IO; using System.Linq; using System.Collections.Generic; using OpenFusion.Unity25;
// uso: ContainerTests <archivo serializado>...  -> verifica consumo exacto por objeto
int bad = 0;
foreach (var path in args)
{
  var f = SerializedFile.Parse(File.ReadAllBytes(path));
  var byClass = new Dictionary<int, (int ok, int off, int fail)>(); var sizes = new Dictionary<int,int>();
  foreach (var o in f.Objects)
  {
    var (ok, off, fail) = byClass.GetValueOrDefault(o.TypeId);
    if (f.TypeOf(o) == null) { byClass[o.TypeId] = (ok, off, fail + 1); continue; }
    try { f.ReadObject(o, out int used); if (Math.Abs((long)used - o.Size) < 4) ok++; else { off++; if (off <= 2) Console.WriteLine($"      ejemplo: clase {o.TypeId} path={o.PathId} usado={used} tam={o.Size}"); } }
    catch (Exception) { fail++; }
    byClass[o.TypeId] = (ok, off, fail);
  }
  Console.WriteLine($"{Path.GetFileName(path)}: v{f.Version} {(f.BigEndian?"BE":"LE")} tipos={f.Types.Count} objetos={f.Objects.Count}");
  foreach (var kv in byClass.OrderBy(k => k.Key)) { Console.WriteLine($"   clase {kv.Key,3} {f.Types.GetValueOrDefault(kv.Key)?.Type,-22} ok={kv.Value.ok} descuadre={kv.Value.off} error={kv.Value.fail}"); bad += kv.Value.off + kv.Value.fail; }
}
Console.WriteLine(bad == 0 ? "TODO OK" : "problemas: " + bad); return bad;
