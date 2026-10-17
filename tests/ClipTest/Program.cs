using System; using System.IO; using System.Linq; using OpenFusion.Unity25;
int clips = 0, fail = 0, curves = 0, flips = 0; double maxErr = 0;
foreach (var path in args)
{
  var bytes = File.ReadAllBytes(path);
  byte[] data = bytes;
  try { var b = UnityWebBundle.Parse(bytes); foreach (var kv in b.Files) { if (kv.Key.EndsWith(".resS") || kv.Key.EndsWith(".resource")) continue; Run(kv.Value); } }
  catch { Run(bytes); }
}
void Run(byte[] d)
{
  SerializedFile f; try { f = SerializedFile.Parse(d); } catch { return; }
  foreach (var o in f.Objects)
  {
    var t = f.TypeOf(o); if (t == null || t.Type != "AnimationClip") continue;
    try
    {
      var c = Clip25.From(f.ReadObject(o, out _)); clips++;
      // hemisfera: tras Fix no debe quedar ningún dot<0 entre quats consecutivos
      foreach (var rc in c.Rotation) { var q = ClipCurves.FixHemisphere(rc.Quats); for (int i = 1; i < q.Length; i++) { float dot = 0; for (int k = 0; k < 4; k++) dot += q[i-1][k]*q[i][k]; if (dot < -1e-6f) flips++; } }
      var cs = ClipCurves.Build(c); curves += cs.Count;
      // la curva evaluada en las claves debe devolver el valor de la clave
      foreach (var s in cs) for (int i = 0; i < s.T.Length; i++) maxErr = Math.Max(maxErr, Math.Abs(ClipCurves.Eval(s, s.T[i]) - s.V[i]));
      if (cs.Any(s => s.T.Length != s.V.Length || s.In.Any(float.IsNaN) || s.Out.Any(float.IsNaN))) { fail++; Console.WriteLine("NaN/len en " + c.Name); }
    }
    catch (Exception e) { fail++; Console.WriteLine("ERROR " + e.Message); }
  }
}
Console.WriteLine($"clips={clips} curvas={curves} hemisferaMal={flips} errMax={maxErr:g3} fallos={fail}");
return fail + flips > 0 || clips == 0 ? 1 : 0;
