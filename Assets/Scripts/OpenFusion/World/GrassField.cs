// Hierba del terreno con mallas propias (ver Shaders/OpenFusionGrass.shader). Se construye a partir de los mapas de detalle del TerrainData 2.5:
// por celda (1 m) y prototipo hay `c` briznas en posiciones aleatorias (determinísticas) dentro de la celda; tamaño aleatorio entre min/max de ancho y alto;
// color = lerp(seco, sano, ruido Perlin con frecuencia = noiseSpread). La malla se divide en bloques de 64x64 celdas que se activan según la distancia
// de visibilidad (GraphicsFx: detailCull {0,40,80,120,160} m). Con Custom = true el sistema de detalles del Terrain se desactiva.
// SIN COMPILAR EN UNITY.
using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class GrassInfo
    {
        public sealed class Proto { public Texture2D Tex; public float MinW, MaxW, MinH, MaxH, Noise; public Color Healthy, Dry; }
        public List<Proto> Protos = new List<Proto>(); public int[][,] Maps; public int Res;
    }

    public sealed class GrassField : MonoBehaviour
    {
        public static bool Custom = true, Enabled = true;
        public static float Distance = 80f;
        const int ChunkCells = 64, MaxPerCell = 8;
        sealed class Chunk { public GameObject Go; public Bounds B; public Mesh Mesh; }
        readonly List<Chunk> chunks = new List<Chunk>(); static Shader shader; readonly List<Material> mats = new List<Material>(); float next;

        public void Init(GameObject terrainGo, TerrainData td, GrassInfo info)
        {
            if (shader == null) shader = Shader.Find("OpenFusion/Grass"); if (shader == null) { Debug.LogWarning("[grass] shader OpenFusion/Grass no encontrado: se usa el sistema de detalles del Terrain"); Custom = false; return; }
            var size = td.size; int res = info.Res; float cell = size.x / res; float cellZ = size.z / res; int nChunks = Mathf.CeilToInt(res / (float)ChunkCells);
            int cap = Application.isMobilePlatform && SystemInfo.systemMemorySize < 4000 ? 2 : MaxPerCell;
            foreach (var p in info.Protos) { var m = new Material(shader) { name = "OF_Grass_" + (p.Tex != null ? p.Tex.name : "?") }; m.mainTexture = p.Tex; mats.Add(m); }
            long blades = 0;
            for (int cz = 0; cz < nChunks; cz++) for (int cx = 0; cx < nChunks; cx++)
            {
                var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var corners = new List<Vector2>(); var cols = new List<Color>(); var subTris = new List<List<int>>(); var used = new List<int>();
                for (int m = 0; m < info.Maps.Length; m++)
                {
                    var map = info.Maps[m]; if (map == null) continue; List<int> tris = null; var p = info.Protos[m];
                    for (int z = cz * ChunkCells; z < Mathf.Min(res, (cz + 1) * ChunkCells); z++)
                        for (int x = cx * ChunkCells; x < Mathf.Min(res, (cx + 1) * ChunkCells); x++)
                        {
                            int c = map[z, x]; if (c <= 0) continue;
                            c = Mathf.Min(c, cap);
                            if (tris == null) { tris = new List<int>(); subTris.Add(tris); used.Add(m); }
                            var rnd = new System.Random((x * 73856093) ^ (z * 19349663) ^ (m * 83492791));
                            for (int k = 0; k < c; k++)
                            {
                                float lx = (x + (float)rnd.NextDouble()) * cell, lz = (z + (float)rnd.NextDouble()) * cellZ;
                                float ly = td.GetInterpolatedHeight(lx / size.x, lz / size.z);
                                float w = Mathf.Lerp(p.MinW, p.MaxW, (float)rnd.NextDouble()), h = Mathf.Lerp(p.MinH, p.MaxH, (float)rnd.NextDouble());
                                float n = Mathf.PerlinNoise(lx * Mathf.Max(0.02f, p.Noise) + 11.3f, lz * Mathf.Max(0.02f, p.Noise) + 5.7f);
                                var col = Color.Lerp(p.Dry, p.Healthy, n); col.a = 1f; int b = verts.Count; var pos = new Vector3(lx, ly, lz);
                                verts.Add(pos); verts.Add(pos); verts.Add(pos); verts.Add(pos);
                                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                                corners.Add(new Vector2(-w * 0.5f, 0)); corners.Add(new Vector2(w * 0.5f, 0)); corners.Add(new Vector2(w * 0.5f, h)); corners.Add(new Vector2(-w * 0.5f, h));
                                cols.Add(col); cols.Add(col); cols.Add(col); cols.Add(col);
                                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); blades++;
                            }
                        }
                }
                if (verts.Count == 0) continue;
                var mesh = new Mesh { name = "grass_" + cx + "_" + cz }; if (verts.Count > 60000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetUVs(1, corners); mesh.SetColors(cols); mesh.subMeshCount = subTris.Count;
                for (int s = 0; s < subTris.Count; s++) mesh.SetTriangles(subTris[s], s);
                var bb = new Bounds(verts[0], Vector3.zero); foreach (var v in verts) bb.Encapsulate(v); bb.Expand(new Vector3(3f, 3f, 3f)); mesh.bounds = bb;
                var go = new GameObject("grass_" + cx + "_" + cz); go.transform.SetParent(terrainGo.transform, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); var ms = new Material[used.Count]; for (int s = 0; s < used.Count; s++) ms[s] = mats[used[s]]; mr.sharedMaterials = ms;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mesh.UploadMeshData(true);
                go.SetActive(false); chunks.Add(new Chunk { Go = go, B = bb, Mesh = mesh });
            }
            Debug.Log("[grass] " + terrainGo.name + ": hierba propia, " + blades + " briznas en " + chunks.Count + " bloques");
        }

        void Update()
        {
            if (Time.time < next) return; next = Time.time + 0.4f; var cam = Camera.main; if (cam == null) return; var cp = cam.transform.position;
            float lim = Distance + 8f;
            foreach (var c in chunks)
            {
                if (c.Go == null) continue; bool want = Enabled && Distance > 1f;
                if (want) { var wb = new Bounds(transform.TransformPoint(c.B.center), c.B.size); want = wb.SqrDistance(cp) < lim * lim; }
                if (c.Go.activeSelf != want) c.Go.SetActive(want);
            }
        }

        void OnDestroy()
        {
            foreach (var c in chunks) if (c.Mesh != null) Destroy(c.Mesh);
            foreach (var m in mats) if (m != null) Destroy(m);
        }
    }
}
