using System; using System.IO; using System.Collections.Generic; using System.Linq; using OpenFusion.Unity25;
var f = SerializedFile.Parse(File.ReadAllBytes(args[0])); int n = 0;
foreach (var o in f.Objects.OrderBy(x => x.PathId))
{
  var t = f.TypeOf(o); if (t == null || t.Type != args[1]) continue;
  var v = f.ReadObject(o, out _);
  string info = args[1] switch {
    "Texture2D" => $"{v["m_Name"]}|{v["m_Width"]}|{v["m_Height"]}|{v["m_TextureFormat"]}|{((byte[])v["image data"]).Length}",
    "Mesh" => $"{v["m_Name"]}|{((object[])v["m_Vertices"]).Length}|{((byte[])v["m_IndexBuffer"]).Length}",
    "AnimationClip" => $"{v["m_Name"]}|{((object[])v["m_PositionCurves"]).Length}|{((object[])v["m_RotationCurves"]).Length}",
    _ => v["m_Name"].ToString() };
  Console.WriteLine($"{o.PathId}|{info}"); if (++n >= 6) break;
}
