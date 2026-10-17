// Lógica de mundo del cliente original (DongLoader / MapAttributeTable / CachingManifest) sin dependencias de Unity.
// Mundo = rejilla 16x16 de "dongs" de 512 m; cada dong es una escena Map_XX_YY cargada aditivamente.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public enum ZoneType { Tutorial, Free, Paid }

    public struct DongColors { public float[] Fog, Sky, Light; public float FogDepth; public bool Set; }

    public static class DongGrid
    {
        public const float Size = 512f, LoadDistance = 280f, UnloadDistance = 340f;
        public const int Count = 16;
        public static string Prefix(string prefix, int x, int y) => prefix + (x < 10 ? "0" : "") + x + "_" + (y < 10 ? "0" : "") + y;
        public static string MapName(int x, int y) => Prefix("Map_", x, y);
        public static void Cell(float wx, float wz, out int x, out int y) { x = (int)Math.Floor(wx / Size); y = (int)Math.Floor(wz / Size); }
        public static float[] Origin(int x, int y) => new[] { x * Size, 0f, y * Size };
        public static float[] Center(int x, int y) => new[] { x * Size + 256f, 0f, y * Size + 256f };
        /// distancia^2 desde una posición al rectángulo del dong
        public static float SqrDistance(float px, float pz, int x, int y)
        {
            float x0 = x * Size, x1 = (x + 1) * Size, z0 = y * Size, z1 = (y + 1) * Size, dx = 0, dz = 0;
            if (px < x0) dx = Math.Abs(x0 - px); else if (px > x1) dx = Math.Abs(x1 - px);
            if (pz < z0) dz = Math.Abs(z0 - pz); else if (pz > z1) dz = Math.Abs(z1 - pz);
            return dx * dx + dz * dz;
        }

        /// Decisión de streaming por posición del jugador, igual que DongLoader.UpdatePosition:
        /// primero descarga un dong cargado a más de 340 m; si no, carga el más cercano (<280 m) de los 3x3 alrededor.
        public static (bool load, int x, int y, bool unload, int ux, int uy) Step(float px, float pz, Func<int, int, bool> loaded, Func<int, int, bool> loadable)
        {
            for (int i = 0; i < Count; i++) for (int j = 0; j < Count; j++)
                if (loaded(j, i) && SqrDistance(px, pz, j, i) > UnloadDistance * UnloadDistance) return (false, 0, 0, true, j, i);
            int cx, cy; Cell(px, pz, out cx, out cy); float best = LoadDistance * LoadDistance; int bx = -1, by = -1;
            for (int k = Math.Max(cy - 1, 0); k < Math.Min(cy + 2, Count); k++) for (int l = Math.Max(cx - 1, 0); l < Math.Min(cx + 2, Count); l++)
                if (!loaded(l, k) && loadable(l, k)) { float d = SqrDistance(px, pz, l, k); if (d < best) { best = d; bx = l; by = k; } }
            return bx >= 0 ? (true, bx, by, false, 0, 0) : (false, 0, 0, false, 0, 0);
        }

        /// Mezcla bilineal de niebla/cielo/luz entre los 4 dongs más cercanos (DongLoader.GetPositionColor).
        public static void PositionColor(float px, float pz, Func<int, int, DongColors> get, out float[] fog, out float[] sky, out float[] light, out float fogDepth, out float[] rim)
        {
            float u = px / Size - 0.5f, v = pz / Size - 0.5f; int x0 = (int)Math.Floor(u), x1 = x0 + 1, y0 = (int)Math.Floor(v), y1 = y0 + 1;
            float fx = Clamp01((u - x0 - 0.25f) * 2f), fz = Clamp01((v - y0 - 0.25f) * 2f);
            var fg = new float[4]; var sk = new float[4]; var lt = new float[4]; float fd = 0, sum = 0;
            void Add(int cx, int cy, float w) { if (cx < 0 || cy < 0 || cx >= Count || cy >= Count) return; var c = get(cx, cy); if (!c.Set) return;
                for (int i = 0; i < 4; i++) { fg[i] += w * c.Fog[i]; sk[i] += w * c.Sky[i]; lt[i] += w * c.Light[i]; } fd += w * c.FogDepth; sum += w; }
            Add(x0, y0, (1 - fx) * (1 - fz)); Add(x1, y0, fx * (1 - fz)); Add(x0, y1, (1 - fx) * fz); Add(x1, y1, fx * fz);
            if (sum > 0) { float k = 1f / sum; for (int i = 0; i < 4; i++) { fg[i] *= k; sk[i] *= k; lt[i] *= k; } fd *= k; }
            else { fd = 0; sk = new float[] { 1, 1, 1, 1 }; fg = new float[] { 0, 0, 0, 1 }; lt = new float[] { 1, 1, 1, 1 }; }
            fog = fg; sky = sk; light = lt; fogDepth = fd;
            rim = new[] { fog[0] * 0.35f, fog[1] * 0.35f, fog[2] * 0.35f, 1f };   // Shader global _RimColor
        }
        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        /// MapAttributeTable.GetAttributes: attributes[ix + iz*heightmapWidth], celdas de size/(w-1).
        public static byte Attribute(byte[] attributes, int heightmapWidth, int heightmapHeight, float sizeX, float sizeZ, float originX, float originZ, float wx, float wz, out bool outOfBounds)
        {
            float cx = sizeX / (heightmapWidth - 1), cz = sizeZ / (heightmapHeight - 1);
            int ix = (int)Math.Floor((wx - originX) / cx), iz = (int)Math.Floor((wz - originZ) / cz);
            outOfBounds = ix < 0 || iz < 0 || ix > heightmapWidth - 1 || iz > heightmapHeight - 1;
            return outOfBounds ? (byte)0 : attributes[ix + iz * heightmapWidth];
        }
    }

    /// CachingManifest (ScriptableObject en sharedassets0.assets de main.unity3d): lista de bundles por zona con versión.
    public sealed class CachingManifest25
    {
        public sealed class Entry { public int Version; public string FileName; }
        public Dictionary<string, List<Entry>> Lists = new Dictionary<string, List<Entry>>();
        public int[] PaidCoords;
        // FreeZoneCoordinates está codificado en el cliente (no serializado)
        public static readonly int[] FreeCoords = { 10,0,10,1,10,2,11,0,11,1,11,2,11,3,11,4,12,0,12,1,12,2,12,3,12,4,13,0,13,1,13,2,13,3,13,4,14,0,14,1,14,2,14,3,15,0,15,1,15,2,15,3,14,14,14,12,15,12 };
        public static CachingManifest25 From(Dictionary<string, object> v)
        {
            var m = new CachingManifest25();
            foreach (var kv in v) if (kv.Key.StartsWith("m_") && kv.Value is object[] arr && kv.Key != "m_PaidZoneCoordinates" && arr.Length > 0 && arr[0] is Dictionary<string, object>)
                m.Lists[kv.Key] = arr.Cast<Dictionary<string, object>>().Select(d => new Entry { Version = D.I(d["version"]), FileName = (string)d["fileName"] }).ToList();
            m.PaidCoords = ((object[])v["m_PaidZoneCoordinates"]).Select(D.I).ToArray();
            return m;
        }
        public static CachingManifest25 FromMainUnity3d(byte[] mainUnity3d)
        {
            var b = UnityWebBundle.Parse(mainUnity3d); var f = SerializedFile.Parse(b.Files["sharedassets0.assets"]);
            foreach (var o in f.Objects) { if (f.TypeOf(o)?.Type != "MonoBehaviour") continue; var v = f.ReadObject(o, out _); if (v.ContainsKey("m_PaidZoneCoordinates")) return From(v); }
            throw new InvalidOperationException("CachingManifest no encontrado");
        }
        sealed class SavedManifest
        {
            public int schema; public string hash;
            public Dictionary<string, List<Entry>> lists; public int[] paid;
        }
        public static CachingManifest25 LoadCached(byte[] main, string path, string hash)
        {
            if (!string.IsNullOrEmpty(hash))
            {
                try
                {
                    var saved = System.IO.File.Exists(path) ? Newtonsoft.Json.JsonConvert.DeserializeObject<SavedManifest>(System.IO.File.ReadAllText(path)) : null;
                    if (saved != null && saved.schema == 1 && saved.hash == hash && saved.lists != null && saved.lists.Count > 0 && saved.paid != null)
                        return new CachingManifest25 { Lists = saved.lists, PaidCoords = saved.paid };
                }
                catch (Exception) { }
            }
            var parsed = FromMainUnity3d(main);
            if (!string.IsNullOrEmpty(hash))
                try
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(new SavedManifest { schema = 1, hash = hash, lists = parsed.Lists, paid = parsed.PaidCoords }));
                }
                catch (System.IO.IOException) { }
            return parsed;
        }
        public bool IsTutorial(int x, int y) => x <= 2 && y <= 2;
        public bool IsFree(int x, int y) { for (int i = 0; i < FreeCoords.Length; i += 2) if (FreeCoords[i] == x && FreeCoords[i + 1] == y) return true; return false; }
        public bool IsPaid(int x, int y) { for (int i = 0; i < PaidCoords.Length; i += 2) if (PaidCoords[i] == x && PaidCoords[i + 1] == y) return true; return false; }
        public bool Has(int x, int y, ZoneType t) => t == ZoneType.Tutorial ? IsTutorial(x, y) : t == ZoneType.Free ? IsFree(x, y) : IsPaid(x, y);
        public int Version(string fileName) { foreach (var l in Lists.Values) foreach (var e in l) if (e.FileName == fileName) return e.Version; return 0; }
        public IEnumerable<Entry> All => Lists.Values.SelectMany(l => l);
        /// bundles necesarios para una zona (compartidos de la zona + sus dongs), como los descarga AssetLoader
        public IEnumerable<Entry> ForZone(ZoneType t)
        {
            foreach (var n in new[] { "m_CharacterCreation", "m_CharacterSelection", "m_Tutorial" }) if (Lists.TryGetValue(n, out var l)) foreach (var e in l) yield return e;
            if (t >= ZoneType.Free) foreach (var n in new[] { "m_FreeZone", "m_FreeZoneComplete" }) if (Lists.TryGetValue(n, out var l)) foreach (var e in l) yield return e;
            if (t == ZoneType.Paid) foreach (var n in new[] { "m_PaidZone", "m_PaidZoneComplete" }) if (Lists.TryGetValue(n, out var l)) foreach (var e in l) yield return e;
        }
    }

    /// EnviroSoundScript (ScriptableObject): lista de (posición, archivo) de sonido ambiental por dong.
    public sealed class EvSound25
    {
        public float[] Position; public string FileName;
        public static List<EvSound25> From(Dictionary<string, object> v)
            => ((object[])v["EvSoundArray"]).Cast<Dictionary<string, object>>().Select(e => new EvSound25 { Position = D.V3(e["Position"]), FileName = (string)e["FileName"] }).ToList();
    }
}
