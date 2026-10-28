// Terreno (TerrainData) y grafo de escena (GameObject/Transform/MeshFilter/Renderer) de Unity 2.5.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFusion.Unity25
{
    using Dict = Dictionary<string, object>;

    public struct PPtr25 { public int FileId; public long PathId; public bool IsNull => PathId == 0;
        public static PPtr25 From(object o) { var d = (Dict)o; return new PPtr25 { FileId = D.I(d["m_FileID"]), PathId = System.Convert.ToInt64(d["m_PathID"]) }; } }

    public sealed class DetailProto25 { public PPtr25 Texture; public float MinW, MaxW, MinH, MaxH, Noise, Bend; public float[] Healthy = {1,1,1,1}, Dry = {1,1,1,1}; public int RenderMode; }

    public sealed class SplatLayer25 { public PPtr25 Texture; public float TileX, TileY; }

    /// TerrainData: mapa de alturas 16 bit, capas de splat, mapas alfa, lightmap y detalle.
    public sealed class Terrain25
    {
        public string Name; public int Width, Height; public float ScaleX, ScaleY, ScaleZ;
        public ushort[] Heights;                         // Width*Height, columna-major (x*Height+z), 0..32766 -> 0..ScaleY
        public List<SplatLayer25> Splats = new List<SplatLayer25>();
        public List<PPtr25> AlphaTextures = new List<PPtr25>();   // RGBA: 4 capas por textura
        public int AlphamapResolution, BaseMapResolution;
        public PPtr25 Lightmap; public int DetailPrototypes, TreePrototypes, TreeInstances, Shifts;
        public List<DetailProto25> DetailProtos = new List<DetailProto25>();
        public int DetailPatchCount, DetailPatchSamples;
        public byte[][] DetailLayerIndices, DetailCounts;   // por parche (índice a*PatchCount+b): índice de prototipo por ranura y recuentos (ranura*S*S + muestra)
        public const float HeightDiv = 32766f;
        public float HeightMeters(int x, int z) => Heights[x * Height + z] / HeightDiv * ScaleY;
        static byte[] Bytes(object o) { if (o is byte[] b) return b; var a = (object[])o; var r = new byte[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = (byte)D.I(a[i]); return r; }
        static float[] Col(object o) { var d = D.O(o); return new[] { D.F(d["r"]), D.F(d["g"]), D.F(d["b"]), D.F(d["a"]) }; }
        static void ParseDetail(Terrain25 t, Dict dd)
        {
            t.DetailPatchCount = D.I(dd["m_PatchCount"]); t.DetailPatchSamples = D.I(dd["m_PatchSamples"]);
            foreach (var po in D.A(dd["m_DetailPrototypes"]))
            {
                var p = D.O(po);
                t.DetailProtos.Add(new DetailProto25 { Texture = PPtr25.From(p["prototypeTexture"]), MinW = D.F(p["minWidth"]), MaxW = D.F(p["maxWidth"]), MinH = D.F(p["minHeight"]), MaxH = D.F(p["maxHeight"]),
                    Noise = D.F(p["noiseSpread"]), Bend = D.F(p["bendFactor"]), Healthy = Col(p["healthyColor"]), Dry = Col(p["dryColor"]), RenderMode = D.I(p["renderMode"]) });
            }
            var pa = D.A(dd["m_Patches"]); t.DetailLayerIndices = new byte[pa.Length][]; t.DetailCounts = new byte[pa.Length][];
            for (int i = 0; i < pa.Length; i++) { var p = D.O(pa[i]); t.DetailLayerIndices[i] = Bytes(p["layerIndices"]); t.DetailCounts[i] = Bytes(p["numberOfObjects"]); }
        }
        public static Terrain25 From(Dict v)
        {
            var hm = D.O(v["m_Heightmap"]); var sd = D.O(v["m_SplatDatabase"]); var dd = D.O(v["m_DetailDatabase"]);
            var sc = D.O(hm["m_Scale"]);
            var t = new Terrain25 { Name = (string)v["m_Name"], Width = D.I(hm["m_Width"]), Height = D.I(hm["m_Height"]),
                ScaleX = D.F(sc["x"]), ScaleY = D.F(sc["y"]), ScaleZ = D.F(sc["z"]),
                AlphamapResolution = D.I(sd["m_AlphamapResolution"]), BaseMapResolution = D.I(sd["m_BaseMapResolution"]),
                Lightmap = PPtr25.From(v["m_Lightmap"]),
                DetailPrototypes = D.A(dd["m_DetailPrototypes"]).Length, TreePrototypes = D.A(dd["m_TreePrototypes"]).Length,
                TreeInstances = D.A(dd["m_TreeInstances"]).Length, Shifts = hm.ContainsKey("m_Shifts") ? D.A(hm["m_Shifts"]).Length : 0 };
            var hs = D.A(hm["m_Heights"]); t.Heights = new ushort[hs.Length];
            for (int i = 0; i < hs.Length; i++) t.Heights[i] = (ushort)D.I(hs[i]);
            foreach (var s in D.A(sd["m_Splats"])) { var d = D.O(s); var ts = d["tileSize"];
                float tx = 0, ty = 0; if (ts is Dict td) { tx = D.F(td["x"]); ty = D.F(td["y"]); }
                t.Splats.Add(new SplatLayer25 { Texture = PPtr25.From(d["texture"]), TileX = tx, TileY = ty }); }
            try { ParseDetail(t, dd); } catch (Exception) { }
            foreach (var a in D.A(sd["m_AlphaTextures"])) t.AlphaTextures.Add(PPtr25.From(a));
            return t;
        }
    }

    public sealed class SkinInfo25 { public PPtr25 Mesh; public long[] Bones; public bool UpdateWhenOffscreen; }
    public sealed class AnimInfo25 { public PPtr25 Default; public List<PPtr25> Clips = new List<PPtr25>(); public int WrapMode; public bool PlayAutomatically; }

    public sealed class SceneNode25
    {
        public long GameObjectId, TransformId; public string Name; public int Layer; public bool Active;
        public float[] Pos, Rot, Scale; public SceneNode25 Parent; public List<SceneNode25> Children = new List<SceneNode25>();
        public List<string> ComponentTypes = new List<string>(); public List<KeyValuePair<string, long>> Components = new List<KeyValuePair<string, long>>();
        public PPtr25 Mesh; public List<PPtr25> Materials = new List<PPtr25>(); public PPtr25 ColliderMesh;
        public bool ColliderEnabled = true, ColliderTrigger, ColliderConvex;
        public long ParentTransformId;
        public SkinInfo25 Skin; public AnimInfo25 Anim;
        /// matriz 4x4 local->mundo, fila-major (columna vector). Unity: T*R*S, mano izquierda.
        public float[] LocalMatrix()
        {
            float x = Rot[0], y = Rot[1], z = Rot[2], w = Rot[3];
            float[] r = { 1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w), 2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w), 2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y) };
            return new[] { r[0]*Scale[0], r[1]*Scale[1], r[2]*Scale[2], Pos[0],
                           r[3]*Scale[0], r[4]*Scale[1], r[5]*Scale[2], Pos[1],
                           r[6]*Scale[0], r[7]*Scale[1], r[8]*Scale[2], Pos[2], 0, 0, 0, 1 };
        }
        public static float[] Mul(float[] a, float[] b) { var r = new float[16]; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { float s = 0; for (int k = 0; k < 4; k++) s += a[i*4+k]*b[k*4+j]; r[i*4+j] = s; } return r; }
        public float[] WorldMatrix() => Parent == null ? LocalMatrix() : Mul(Parent.WorldMatrix(), LocalMatrix());
    }

    /// Jerarquía de una escena o bundle: nodos con sus componentes principales.
    public sealed class SceneGraph25
    {
        public Dictionary<long, SceneNode25> ByTransform = new Dictionary<long, SceneNode25>();
        public List<SceneNode25> Roots = new List<SceneNode25>();
        Dictionary<long, SceneNode25> byComponent;
        /// Nodo que posee el componente con ese pathId (p. ej. un SkinnedMeshRenderer referenciado desde ActorWearSkinTable.skins).
        public SceneNode25 OwnerOf(long componentId)
        {
            if (byComponent == null) { byComponent = new Dictionary<long, SceneNode25>(); foreach (var n in ByTransform.Values) foreach (var c in n.Components) byComponent[c.Value] = n; }
            return byComponent.TryGetValue(componentId, out var o) ? o : null;
        }
        /// Resuelve una ruta de animación ("Bip01/Bip01 Pelvis/...") desde `root`; "" = el propio root.
        public static SceneNode25 FindPath(SceneNode25 root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root; var cur = root;
            foreach (var part in path.Split('/')) { cur = cur.Children.FirstOrDefault(c => c.Name == part); if (cur == null) return null; }
            return cur;
        }
        public static SceneGraph25 Build(SerializedFile f)
        {
            var g = new SceneGraph25(); var byId = f.Objects.ToDictionary(o => o.PathId);
            var gos = new Dictionary<long, Dict>(); var nodes = new Dictionary<long, SceneNode25>();   // por GameObject id
            foreach (var o in f.Objects) if (f.TypeOf(o)?.Type == "GameObject") gos[o.PathId] = f.ReadObject(o, out _);
            foreach (var o in f.Objects) if (f.TypeOf(o)?.Type == "Transform")
            {
                var t = f.ReadObject(o, out _); long goId = PPtr25.From(t["m_GameObject"]).PathId;
                var n = new SceneNode25 { TransformId = o.PathId, GameObjectId = goId, Pos = D.V3(t["m_LocalPosition"]), Rot = D.V4(t["m_LocalRotation"]), Scale = D.V3(t["m_LocalScale"]),
                    ParentTransformId = PPtr25.From(t["m_Father"]).PathId };
                if (gos.TryGetValue(goId, out var go)) { n.Name = (string)go["m_Name"]; n.Layer = D.I(go["m_Layer"]); n.Active = D.I(go["m_IsActive"]) != 0;
                    foreach (var c in D.A(go["m_Component"])) { var cp = PPtr25.From(D.O(c)["second"]); if (cp.FileId == 0 && byId.TryGetValue(cp.PathId, out var co)) {
                        var ty = f.TypeOf(co)?.Type; n.ComponentTypes.Add(ty); n.Components.Add(new KeyValuePair<string, long>(ty, cp.PathId));
                        if (ty == "MeshFilter") n.Mesh = PPtr25.From(f.ReadObject(co, out _)["m_Mesh"]);
                        else if (ty == "MeshCollider") {
                            var collider = f.ReadObject(co, out _); n.ColliderMesh = PPtr25.From(collider["m_Mesh"]);
                            n.ColliderEnabled = !collider.ContainsKey("m_Enabled") || D.I(collider["m_Enabled"]) != 0;
                            n.ColliderTrigger = collider.ContainsKey("m_IsTrigger") && D.I(collider["m_IsTrigger"]) != 0;
                            n.ColliderConvex = collider.ContainsKey("m_Convex") && D.I(collider["m_Convex"]) != 0;
                        }
                        else if (ty == "Renderer" || ty == "MeshRenderer" || ty == "SkinnedMeshRenderer")
                        {
                            var rv = f.ReadObject(co, out _);
                            foreach (var m in D.A(rv["m_Materials"])) n.Materials.Add(PPtr25.From(m));
                            if (ty == "SkinnedMeshRenderer")
                                n.Skin = new SkinInfo25 { Mesh = PPtr25.From(rv["m_Mesh"]), Bones = D.A(rv["m_Bones"]).Select(b => PPtr25.From(b).PathId).ToArray(),
                                    UpdateWhenOffscreen = rv.ContainsKey("m_UpdateWhenOffscreen") && D.I(rv["m_UpdateWhenOffscreen"]) != 0 };
                        }
                        else if (ty == "Animation")
                        {
                            var av = f.ReadObject(co, out _); var a = new AnimInfo25 { Default = PPtr25.From(av["m_Animation"]), WrapMode = D.I(av["m_WrapMode"]), PlayAutomatically = D.I(av["m_PlayAutomatically"]) != 0 };
                            foreach (var c2 in D.A(av["m_Animations"])) a.Clips.Add(PPtr25.From(c2));
                            n.Anim = a;
                        } } } }
                nodes[goId] = n; g.ByTransform[o.PathId] = n;
            }
            foreach (var n in g.ByTransform.Values) { if (n.ParentTransformId != 0 && g.ByTransform.TryGetValue(n.ParentTransformId, out var p)) { n.Parent = p; p.Children.Add(n); } else g.Roots.Add(n); }
            return g;
        }
    }
}
