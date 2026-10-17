using System; using System.IO; using System.Linq; using System.Security.Cryptography; using OpenFusion.Unity25;
// uso: TypedTests <archivo> <Texture2D|Mesh|AnimationClip> [max]  -> imprime resumen estable para comparar con Python
var f = SerializedFile.Parse(File.ReadAllBytes(args[0])); int max = args.Length > 2 ? int.Parse(args[2]) : 8, n = 0, fail = 0;
foreach (var o in f.Objects.OrderBy(x => x.PathId))
{
  var t = f.TypeOf(o); if (t == null || t.Type != args[1]) continue;
  try {
    var v = f.ReadObject(o, out _);
    if (args[1] == "Texture2D") { var tx = Texture25.From(v); var rgba = tx.ToRGBA32(); if (Environment.GetEnvironmentVariable("DUMP") is string dd) File.WriteAllBytes(Path.Combine(dd, o.PathId + ".rgba"), rgba);
    Console.WriteLine($"{o.PathId}|{tx.Name}|{tx.Width}x{tx.Height}|f{tx.Format}|{Convert.ToHexString(MD5.HashData(rgba))}"); }
    else if (args[1] == "Mesh") { var m = Mesh25.From(v); Console.WriteLine($"{o.PathId}|{m.Name}|v{m.Positions.Length/3}|tri{m.SubmeshTriangles.Sum(s => s.Length/3)}|skin{(m.BoneIdx?.Length/4 ?? 0)}|bind{m.BindPose.Length}"); }
    else { var c = Clip25.From(v); Console.WriteLine($"{o.PathId}|{c.Name}|pos{c.Position.Count}|rot{c.Rotation.Count}|scale{c.Scale.Count}|keys{c.Position.Sum(p => p.Keys.Length)}/{c.Rotation.Sum(r => r.Times.Length)}"); }
  } catch (Exception e) { fail++; Console.WriteLine($"{o.PathId}|ERROR {e.GetType().Name}: {e.Message}"); }
  if (++n >= max) break;
}
Console.WriteLine($"# {n} objetos, {fail} errores"); return fail;
