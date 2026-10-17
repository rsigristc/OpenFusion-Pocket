using System; using System.IO; using System.Linq; using System.Collections.Generic; using OpenFusion.Unity25;
int skins=0, skinBad=0, nullBones=0, extMesh=0, anims=0, clipsOk=0, clipsExt=0, curvesTot=0, curvesHit=0, animBad=0, idxBad=0, bindMis=0;
var sample = new List<string>(); var miss = new List<string>();
foreach (var path in args) {
  var raw = File.ReadAllBytes(path); var files = new Dictionary<string,byte[]>(); try { files = UnityWebBundle.Parse(raw).Files; } catch { files["x"] = raw; }
  foreach (var kv in files) {
    SerializedFile f; try { f = SerializedFile.Parse(kv.Value); } catch { continue; }
    if (!f.Objects.Any(o => f.TypeOf(o)?.Type == "SkinnedMeshRenderer" || f.TypeOf(o)?.Type == "Animation")) continue;
    var g = SceneGraph25.Build(f); var byId = f.Objects.ToDictionary(o => o.PathId);
    foreach (var n in g.ByTransform.Values) {
      if (n.Skin != null) {
        skins++; nullBones += n.Skin.Bones.Count(b => b == 0);
        if (n.Skin.Mesh.FileId != 0 || !byId.ContainsKey(n.Skin.Mesh.PathId)) { extMesh++; continue; }
        var m = Mesh25.From(f.ReadObject(byId[n.Skin.Mesh.PathId], out _));
        bool bad = false;
        if (m.BindPose.Length != n.Skin.Bones.Length) { bindMis++; bad = true; }
        if (m.BoneIdx != null && m.BoneIdx.Length > 0 && m.BoneIdx.Max() >= n.Skin.Bones.Length) { idxBad++; bad = true; }
        if (n.Skin.Bones.Any(b => b != 0 && !g.ByTransform.ContainsKey(b))) bad = true;
        if (bad) { skinBad++; if (sample.Count < 5) sample.Add($"{n.Name} bones={n.Skin.Bones.Length} bind={m.BindPose.Length} maxIdx={(m.BoneIdx?.Length>0?m.BoneIdx.Max():-1)}"); }
      }
      if (n.Anim != null) {
        anims++; bool bad = false;
        foreach (var cp in n.Anim.Clips.Where(c => c.PathId != 0).Distinct()) {
          if (cp.FileId != 0 || !byId.ContainsKey(cp.PathId)) { clipsExt++; continue; }
          var c = Clip25.From(f.ReadObject(byId[cp.PathId], out _)); clipsOk++;
          var paths = c.Position.Select(x => x.Path).Concat(c.Scale.Select(x => x.Path)).Concat(c.Rotation.Select(x => x.Path));
          foreach (var pth in paths) { curvesTot++; if (SceneGraph25.FindPath(n, pth) != null) curvesHit++; else if (miss.Count < 12) miss.Add(n.Name + " | " + c.Name + " | " + pth); }
        }
        if (bad) animBad++;
      }
    }
  }
}
Console.WriteLine($"skins={skins} (mallaExterna={extMesh}) malos={skinBad} [bindPose!=huesos={bindMis}, indiceFuera={idxBad}] huesosNulos={nullBones}");
Console.WriteLine($"animations={anims} clips={clipsOk} clipsExternos={clipsExt} curvas={curvesTot} rutasResueltas={curvesHit} ({(curvesTot>0?100.0*curvesHit/curvesTot:0):F2}%)");
foreach (var s in sample) Console.WriteLine("  " + s);
foreach (var m in miss) Console.WriteLine("  miss: " + m);
