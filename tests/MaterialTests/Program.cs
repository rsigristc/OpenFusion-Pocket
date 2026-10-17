using System; using System.IO; using System.Linq; using System.Text.RegularExpressions; using System.Collections.Generic; using OpenFusion.Unity25; using OpenFusion.Materials;
// uso: MaterialTests <directorio ffcache/<uuid>>  -> mapea todos los materiales de todos los archivos serializados v6 que encuentre
var files = Directory.GetDirectories(args[0]).SelectMany(d => Directory.GetFiles(d)).Where(f => !Path.GetFileName(f).StartsWith("__")).OrderBy(f => f).ToList();
var sfs = new Dictionary<string, SerializedFile>(); var shaderName = new Dictionary<(string, long), string>();
foreach (var f in files)
{
  var b = File.ReadAllBytes(f); SerializedFile sf; try { sf = SerializedFile.Parse(b); } catch { continue; }
  var key = Path.GetFileName(f).ToLowerInvariant(); sfs[key] = sf;
  foreach (var o in sf.Objects.Where(o => o.ClassId == 48)) { var raw = System.Text.Encoding.UTF8.GetString(b, (int)o.Offset, (int)o.Size); var m = Regex.Match(raw, "Shader\\s+\"([^\"]+)\""); if (m.Success) shaderName[(key, o.PathId)] = m.Groups[1].Value; }
}
int vfxBad = 0, total = 0, unres = 0, nocat = 0, notarget = 0, fail = 0; var byKind = new Dictionary<string, int>(); var missing = new Dictionary<string, int>();
foreach (var (key, sf) in sfs)
  foreach (var o in sf.Objects.Where(o => o.ClassId == 21))
  {
    total++;
    try {
      var v = sf.ReadObject(o, out _); var sp = (Dictionary<string, object>)v["m_Shader"]; int fid = System.Convert.ToInt32(sp["m_FileID"]); long pid = System.Convert.ToInt64(sp["m_PathID"]);
      string fkey = fid == 0 ? key : (fid - 1 < sf.Externals.Count ? Path.GetFileName(sf.Externals[fid - 1]).ToLowerInvariant() : null);
      string name = fkey != null && shaderName.TryGetValue((fkey, pid), out var n) ? n : null;
      if (name == null) { unres++; continue; }
      var spec = MaterialMapper.Map(v, name);
      if (spec.Shader == null) { nocat++; missing[name] = missing.GetValueOrDefault(name) + 1; continue; }
      var k = spec.Shader.Kind.ToString(); byKind[k] = byKind.GetValueOrDefault(k) + 1;
      if (ShaderTargets.Target(spec.Shader.Kind, name) == null) notarget++; if (spec.Shader.Kind == ShaderKind.Vfx && ShaderTargets.VfxMode(name) < 0 && !name.Contains("Hologram")) vfxBad++;
    } catch (Exception e) { fail++; if (fail < 4) Console.WriteLine("ERROR " + e.Message); }
  }
Console.WriteLine($"materiales={total} errores={fail} shader-sin-resolver={unres} fuera-de-catálogo={nocat}");
foreach (var kv in byKind.OrderByDescending(x => x.Value)) Console.WriteLine($"  {kv.Key,-14} {kv.Value}");
Console.WriteLine($"VFX sin modo: {vfxBad}");
Console.WriteLine($"con shader moderno asignado: {byKind.Where(k => k.Key is "LegacyLit" or "LegacyGlow" or "Particle" or "Other" or "Toon").Sum(k => k.Value) - 0} ; sin portar: {notarget}");
foreach (var kv in missing) Console.WriteLine("  falta en catálogo: " + kv.Key + " x" + kv.Value);
return fail;
