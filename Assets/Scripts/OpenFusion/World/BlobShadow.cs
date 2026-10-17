// Sombra "blob" circular bajo personajes, NPCs y mobs (el cliente original proyecta una sombra suave bajo cada actor).
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class BlobShadow : MonoBehaviour
    {
        static Material mat; static Texture2D tex; float size = 1.2f;
        // Opcion SHADOWS: "Own Player Only" (solo la sombra del jugador) / "All Characters". OwnRoot lo fija GameMenu al aparecer el propio personaje.
        public static bool AllShadows = true; public static Transform OwnRoot; static readonly System.Collections.Generic.List<BlobShadow> all = new System.Collections.Generic.List<BlobShadow>(); GameObject quad;
        bool IsOwn() { return OwnRoot != null && (transform == OwnRoot || transform.IsChildOf(OwnRoot) || OwnRoot.IsChildOf(transform)); }
        void OnDestroy() { all.Remove(this); }
        public static void Refresh() { for (int i = all.Count - 1; i >= 0; i--) { var b = all[i]; if (b == null) { all.RemoveAt(i); continue; } b.ApplyVisible(); } }
        float visAt = -10f; bool hasVis = true;
        bool HasVisual()
        {   // NPCs marcador/invisibles (sin modelo visible) no proyectan sombra
            if (Time.time - visAt < 1.5f) return hasVis; visAt = Time.time + Random.value * 0.5f; hasVis = false;
            foreach (var r in GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer || r.gameObject == quad || !r.enabled || !r.gameObject.activeInHierarchy) continue; hasVis = true; break; }
            return hasVis;
        }
        void ApplyVisible() { if (quad == null) return; bool want = (AllShadows || IsOwn()) && HasVisual(); if (quad.activeSelf != want) quad.SetActive(want); }
        public static void Attach(GameObject holder, float size) { if (holder == null || holder.GetComponent<BlobShadow>() != null) return; holder.AddComponent<BlobShadow>().size = size; }
        void Start()
        {
            if (tex == null)
            {
                const int N = 64; tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float dx = (x + .5f) / N * 2f - 1f, dy = (y + .5f) / N * 2f - 1f; float d = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.4f) * 0.6f; tex.SetPixel(x, y, new Color(0f, 0f, 0f, a)); }
                tex.Apply();
            }
            if (mat == null) { mat = new Material(Shader.Find("Sprites/Default")); mat.mainTexture = tex; mat.renderQueue = 2460; }
            const int G = 8; verts = new Vector3[(G + 1) * (G + 1)]; var uvs = new Vector2[verts.Length]; var tris = new int[G * G * 6];
            for (int j = 0; j <= G; j++) for (int i = 0; i <= G; i++) { int k = j * (G + 1) + i; verts[k] = new Vector3((i / (float)G - 0.5f) * size, 0.06f, (j / (float)G - 0.5f) * size); uvs[k] = new Vector2(i / (float)G, j / (float)G); }
            int n = 0; for (int j = 0; j < G; j++) for (int i = 0; i < G; i++) { int a = j * (G + 1) + i, b = a + 1, c = a + G + 1, d = c + 1; tris[n++] = a; tris[n++] = c; tris[n++] = b; tris[n++] = b; tris[n++] = c; tris[n++] = d; }
            mesh = new Mesh { name = "blobshadow", vertices = verts, uv = uvs, triangles = tris }; mesh.MarkDynamic();
            var q = new GameObject("blobshadow"); quad = q; all.Add(this); q.transform.SetParent(transform, false); q.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = q.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            nextUpdate = Time.time + Random.value * 0.3f; Conform(); ApplyVisible();
        }
        Mesh mesh; Vector3[] verts; float nextUpdate; Vector3 lastPos = new Vector3(1e9f, 0, 0);
        // La sombra se adapta al relieve (antes era un quad plano que el terreno cortaba en pendientes).
        void Update() { if (mesh == null || Time.time < nextUpdate) return; nextUpdate = Time.time + 0.25f; ApplyVisible(); if (quad != null && !quad.activeSelf) return; if ((transform.position - lastPos).sqrMagnitude < 0.01f) return; Conform(); }
        void Conform()
        {
            lastPos = transform.position; var terrains = Terrain.activeTerrains; var inv = transform.worldToLocalMatrix; float baseY = transform.position.y;
            for (int k = 0; k < verts.Length; k++)
            {
                var lp = verts[k]; var wp = transform.TransformPoint(new Vector3(lp.x, 0f, lp.z)); float y = baseY;
                for (int t = 0; t < terrains.Length; t++)
                {
                    var tr = terrains[t]; if (tr == null || tr.terrainData == null) continue; var o = tr.transform.position; var sz = tr.terrainData.size;
                    if (wp.x < o.x || wp.z < o.z || wp.x > o.x + sz.x || wp.z > o.z + sz.z) continue; y = tr.SampleHeight(wp) + o.y; break;
                }
                // máx. 1.5 m de diferencia respecto a los pies: evita deformarse sobre acantilados
                y = Mathf.Clamp(y, baseY - 1.5f, baseY + 1.5f); verts[k] = new Vector3(lp.x, inv.MultiplyPoint3x4(new Vector3(wp.x, y + 0.06f, wp.z)).y, lp.z);
            }
            mesh.vertices = verts; mesh.RecalculateBounds();
        }
    }
}
