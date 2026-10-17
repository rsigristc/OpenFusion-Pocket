using System; using System.IO; using System.Linq; using System.Text.Json; using OpenFusion.Convert;
var doc = JsonDocument.Parse(File.ReadAllText(args.Length > 0 ? args[0] : "golden.json")).RootElement; int bad = 0;
PackedBits PB(JsonElement e, bool f) => e.ValueKind == JsonValueKind.Undefined ? null : new PackedBits {
  Data = e.GetProperty("data").EnumerateArray().Select(x => (byte)x.GetInt32()).ToArray(), NumItems = e.GetProperty("n").GetInt32(), BitSize = e.GetProperty("bits").GetInt32(),
  Range = f ? (float)e.GetProperty("range").GetDouble() : 0, Start = f ? (float)e.GetProperty("start").GetDouble() : 0 };
JsonElement G(JsonElement e, string k) => e.TryGetProperty(k, out var v) ? v : default;
double Max(float[] a, JsonElement exp, int per) { double m = 0; int i = 0; foreach (var t in exp.EnumerateArray()) { if (per == 1) { m = Math.Max(m, Math.Abs(a[i] - t.GetDouble())); i++; } else foreach (var c in t.EnumerateArray()) { m = Math.Max(m, Math.Abs(a[i] - c.GetDouble())); i++; } } return m; }
int mi = 0;
foreach (var m in doc.GetProperty("meshes").EnumerateArray()) {
  var inp = m.GetProperty("in"); var o = m.GetProperty("out");
  var cm = new CompressedMeshData { Vertices = PB(G(inp,"m_Vertices"), true), Normals = PB(G(inp,"m_Normals"), true), NormalSigns = PB(G(inp,"m_NormalSigns"), false), UV = PB(G(inp,"m_UV"), true), Triangles = PB(G(inp,"m_Triangles"), false), Weights = PB(G(inp,"m_Weights"), false), BoneIndices = PB(G(inp,"m_BoneIndices"), false) };
  var r = MeshDecoder.Decompress(cm);
  double dp = Max(r.Positions, o.GetProperty("positions"), 3), dn = r.Normals != null ? Max(r.Normals, o.GetProperty("normals"), 3) : 0, du = r.UVs != null ? Max(r.UVs, o.GetProperty("uvs"), 2) : 0;
  bool ok = r.Positions.Length / 3 == o.GetProperty("npos").GetInt32() && r.Indices.Length == o.GetProperty("nidx").GetInt32()
     && r.Indices.Take(60).SequenceEqual(o.GetProperty("indices").EnumerateArray().Select(x => (uint)x.GetInt32()).Take(60)) && dp < 1e-5 && dn < 1e-3 && du < 1e-5;
  string sk = "sin skin";
  if (r.BoneIdx != null) { int ns = r.BoneIdx.Length / 4; double ds = 0; int k = 0; foreach (var s in o.GetProperty("skin").EnumerateArray()) { for (int j = 0; j < 4; j++) { if (s[0][j].GetInt32() != r.BoneIdx[k * 4 + j]) ds = 1; ds = Math.Max(ds, Math.Abs(s[1][j].GetDouble() - r.BoneW[k * 4 + j])); } k++; } ok &= ds < 1e-5 && ns == o.GetProperty("nskin").GetInt32() && r.BoneIndicesConsumed == r.BoneIndicesTotal; sk = $"skin {ns} v, huesos consumidos {r.BoneIndicesConsumed}/{r.BoneIndicesTotal}"; }
  if (!ok) bad++; Console.WriteLine($"{(ok?"OK ":"FALLO")} malla {mi++}: {r.Positions.Length/3} v, {r.Indices.Length/3} tri, dPos={dp:G2} dN={dn:G2} dUV={du:G2}, {sk}");
}
int ci = 0;
foreach (var c in doc.GetProperty("curves").EnumerateArray()) {
  var v = c.GetProperty("values"); PackedVectors.DecodeCurve(PB(c.GetProperty("times"), false), v.GetProperty("data").EnumerateArray().Select(x => (byte)x.GetInt32()).ToArray(), v.GetProperty("n").GetInt32(), PB(c.GetProperty("slopes"), true), out var t, out var q, out var s);
  var o = c.GetProperty("out"); double dt = Max(t, o.GetProperty("t"), 1), dq = Max(q.SelectMany(x => x).ToArray(), o.GetProperty("q"), 4), ds = Max(s, o.GetProperty("s"), 1);
  double nq = q.Max(x => Math.Abs(Math.Sqrt(x.Sum(y => y * y)) - 1));
  bool ok = dt < 1e-4 && dq < 1e-5 && ds < 1e-4 && nq < 1e-3; if (!ok) bad++;
  Console.WriteLine($"{(ok?"OK ":"FALLO")} curva {ci++}: {t.Length} keys, dt={dt:G2} dq={dq:G2} ds={ds:G2} max||q|-1|={nq:G2}");
}
return bad;
