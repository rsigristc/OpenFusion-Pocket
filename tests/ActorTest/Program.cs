using System; using System.IO; using System.Linq; using System.Collections.Generic; using OpenFusion.Unity25;
int fails = 0; void Check(bool ok, string m) { Console.WriteLine((ok ? "OK    " : "FALLO ") + m); if (!ok) fails++; }
// 1) mallas con huesos reales de los bundles locales
var skinned = new List<Mesh25>();
foreach (var path in args) {
  var raw = File.ReadAllBytes(path); var files = new Dictionary<string, byte[]>(); try { files = UnityWebBundle.Parse(raw).Files; } catch { files["x"] = raw; }
  foreach (var kv in files) { SerializedFile f; try { f = SerializedFile.Parse(kv.Value); } catch { continue; }
    foreach (var o in f.Objects) { if (f.TypeOf(o)?.Type != "Mesh") continue; try { var m = Mesh25.From(f.ReadObject(o, out _)); if (m.BoneIdx != null && m.BoneIdx.Length > 0 && m.BindPose.Length > 0 && m.SubmeshTriangles.Length > 0 && m.Positions.Length > 0) skinned.Add(m); } catch { } } } }
Console.WriteLine($"mallas con huesos reales: {skinned.Count}");
Check(skinned.Count >= 100, "hay >=100 mallas skinned reales");
// transformaciones de hueso rigidas pseudo-aleatorias (determinista)
var rnd = new Random(7);
float[] RandRigid() { double a = rnd.NextDouble() * 6.28, b = rnd.NextDouble() * 6.28, c = rnd.NextDouble() * 6.28; double ca = Math.Cos(a), sa = Math.Sin(a), cb = Math.Cos(b), sb = Math.Sin(b), cc = Math.Cos(c), sc = Math.Sin(c);
  float[] r = { (float)(cb*cc), (float)(-cb*sc), (float)sb, (float)(rnd.NextDouble()*4-2), (float)(sa*sb*cc+ca*sc), (float)(-sa*sb*sc+ca*cc), (float)(-sa*cb), (float)(rnd.NextDouble()*4-2), (float)(-ca*sb*cc+sa*sc), (float)(ca*sb*sc+sa*cc), (float)(ca*cb), (float)(rnd.NextDouble()*4-2), 0, 0, 0, 1 }; return r; }
float[] Mul(float[] a, float[] b) { var r = new float[16]; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { float s = 0; for (int k = 0; k < 4; k++) s += a[i*4+k]*b[k*4+j]; r[i*4+j] = s; } return r; }
// Skinning de Unity: v' = sum_i w_i * (bone_i.localToWorld * bindpose_i) * v
float[] Skin(Mesh25 m, float[][] boneMats, int vert) { var v = new[] { m.Positions[3*vert], m.Positions[3*vert+1], m.Positions[3*vert+2], 1f }; var res = new float[3];
  for (int j = 0; j < 4; j++) { float w = m.BoneW[4*vert+j]; if (w == 0) continue; var M = Mul(boneMats[m.BoneIdx[4*vert+j]], m.BindPose[m.BoneIdx[4*vert+j]]); for (int r = 0; r < 3; r++) res[r] += w * (M[r*4]*v[0] + M[r*4+1]*v[1] + M[r*4+2]*v[2] + M[r*4+3]); } return res; }
// 2) grupos de 5 piezas: la fusion con tablas de indice -> huesos del "actor" debe dar exactamente el mismo vertice que cada pieza por separado
int groups = 0, vertsChecked = 0, bad = 0; double maxErr = 0; var actorMats = Enumerable.Range(0, 70).Select(_ => RandRigid()).ToArray();
for (int g = 0; g + 5 <= skinned.Count && g < 60; g += 5) {
  var sel = skinned.Skip(g).Take(5).ToList(); var parts = new List<WearPart>(); var perm = new Random(g);
  foreach (var m in sel) { var idx = Enumerable.Range(0, m.BindPose.Length).Select(_ => perm.Next(0, actorMats.Length)).ToArray(); parts.Add(new WearPart { Mesh = m, Index = new ActorWearIndex25 { M = idx, F = idx.Select(i => (i + 1) % actorMats.Length).ToArray() } }); }
  foreach (bool female in new[] { false, true }) {
    var mg = ActorMerge.Merge(parts, female); groups++;
    var boneMats = mg.ActorBoneIndex.Select(i => actorMats[i]).ToArray();   // matriz de cada hueso de la malla fundida = la del hueso del actor al que apunta
    int v0 = 0;
    for (int pi = 0; pi < parts.Count; pi++) {
      var s = parts[pi].Mesh; int n = s.Positions.Length / 3; var idx = parts[pi].Index.For(female); var ownMats = Enumerable.Range(0, s.BindPose.Length).Select(i => actorMats[idx[i]]).ToArray();
      for (int v = 0; v < n; v += Math.Max(1, n / 25)) { var a = Skin(s, ownMats, v); var b = Skin(mg.Mesh, boneMats, v0 + v); double e = Math.Sqrt(Enumerable.Range(0, 3).Sum(k => (a[k]-b[k])*(a[k]-b[k]))); maxErr = Math.Max(maxErr, e); vertsChecked++; if (e > 1e-3) bad++; }
      // los triangulos del submesh pi son los del submesh 0 de la pieza desplazados
      var t0 = s.SubmeshTriangles[0]; var t1 = mg.Mesh.SubmeshTriangles[pi]; if (t0.Length != t1.Length || t0.Select((x, i) => x + v0 != t1[i]).Any(x => x)) bad++;
      v0 += n;
    }
    if (mg.Mesh.BoneIdx.Any(i => i < 0 || i >= mg.Mesh.BindPose.Length) || mg.Mesh.SubmeshTriangles.SelectMany(t => t).Any(i => i < 0 || i >= mg.Mesh.Positions.Length / 3)) bad++;
  } }
Check(groups > 0 && bad == 0, $"fusion de {groups} grupos de 5 piezas (M y F): {vertsChecked} vertices, error maximo {maxErr:g3}, fallos {bad}");
// 3) errores esperados
bool threw = false; try { ActorMerge.Merge(new[] { new WearPart { Mesh = skinned[0], Index = new ActorWearIndex25 { M = new int[0], F = new int[0] } } }, false); } catch (ArgumentException) { threw = true; } Check(threw, "tabla de indices corta -> error claro");
// 4) lectores de campos (nombres del cliente) y reglas de material
var dict = new Dictionary<string, object> { ["actorBones"] = new object[] { new Dictionary<string, object> { ["m_FileID"] = 0, ["m_PathID"] = 5L } }, ["clothes"] = new object[0], ["female"] = 1, ["height"] = 0.5f, ["shape"] = 1.0f };
Check(ActorCombiner25.IsMatch(dict) && ActorCombiner25.From(dict).Female && ActorCombiner25.From(dict).ActorBones[0].PathId == 5, "ActorCombiner25 lee actorBones/female");
var wi = new Dictionary<string, object> { ["transformIndicesM"] = new object[] { 3, 4 }, ["transformIndicesF"] = new object[] { 5, 6 } };
Check(ActorWearIndex25.IsMatch(wi) && ActorWearIndex25.From(wi).For(true)[1] == 6 && ActorWearIndex25.From(wi).For(false)[0] == 3, "ActorWearIndex25 M/F");
Check(ActorMerge.RoleOf("hair_main") == ActorMerge.Role.Primary && ActorMerge.IsHair("hair_main") && ActorMerge.RoleOf("body_sub") == ActorMerge.Role.Sub && ActorMerge.RoleOf("x_override") == ActorMerge.Role.Override, "reglas de material sub/override/hair");
Check(ActorMerge.AttachPath(ActorMerge.Attach.Hat).EndsWith("Bip01 Head/Bip01 helmet01") && ActorMerge.AttachPath(ActorMerge.Attach.Back).EndsWith("Bip01 Spine1/Bip01 back01"), "rutas de acople");
return fails;
