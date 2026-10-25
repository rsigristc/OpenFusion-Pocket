// Cielo del cliente 2.5: cnPlayerCamera usa un Skybox 6 caras (TutorialAssets/Skybox.mat fuera de la zona libre, Skybox_FreeZone.mat dentro del Futuro y el tutorial)
// con _Tint = color de niebla del dong (RenderFX/Skybox: textura * tint * 2). Aquí: cubo de 6 caras que sigue a la cámara, dibujado al fondo sin profundidad.
// Texturas: Resources/OpenFusion/Sky/{n|f}_{front,back,left,right,up,down}. Convención Unity: Front +Z, Back -Z, Left +X, Right -X. SIN COMPILAR.
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class LegacySky : MonoBehaviour
    {
        static readonly string[] Faces = { "front", "back", "left", "right", "up", "down" };
        // zona libre del original: FutureZoneRect (5632,512,2048,2048) y TutorialZoneRect (512,512,512,512), en metros (x, z)
        static readonly Rect FutureZone = new Rect(5632f, 512f, 2048f, 2048f), TutorialZone = new Rect(512f, 512f, 512f, 512f);

        Transform cam; Transform star; float starScale = -1f, starTarget = 0.5f; Mesh mesh; Vector3[] verts; Material[] mats; bool free = true; bool built; Color tint = new Color(0.5f, 0.5f, 0.5f, 1f);

        public static LegacySky Create(Camera cam)
        {
            var go = new GameObject("LegacySky"); var s = go.AddComponent<LegacySky>(); s.cam = cam.transform; s.Build(); return s;
        }

        public static bool IsFreeZone(Vector3 p) { var q = new Vector2(p.x, p.z); return FutureZone.Contains(q) || TutorialZone.Contains(q); }

        bool seasonal; Texture2D seasonalSky, seasonalStar, normalStar;
        public void Apply(Vector3 camPos, Color fog, ModelFactory models = null)
        {
            if (!built) return;
            bool f = IsFreeZone(camPos);
            if (f != free) { free = f; LoadTextures(); }
            starTarget = free ? 1f : 0.5f;   // FusionStar.LateUpdate: la escala del plano tiende a 1 en zona libre y a 0.5 en zona de pago
            tint = new Color(fog.r, fog.g, fog.b, 1f);
            for (int i = 0; i < mats.Length; i++) mats[i].SetColor("_Tint", tint);
            bool halloween = SeasonalEvents.Instance != null && SeasonalEvents.Instance.Halloween;
            if (halloween)
            {
                if (seasonalSky == null) seasonalSky = models?.Texture("tutorialassets/sky_night_back.psd");
                if (seasonalStar == null) seasonalStar = models?.Texture("tutorialassets/fusionstar-1.dds");
                if (seasonalSky != null) foreach (var material in mats) material.mainTexture = seasonalSky;
                if (seasonalStar != null && star != null) star.GetComponent<Renderer>().sharedMaterial.mainTexture = seasonalStar;
                seasonal = true;
            }
            else if (seasonal)
            {
                LoadTextures();
                if (star != null) star.GetComponent<Renderer>().sharedMaterial.mainTexture = normalStar;
                seasonal = false; seasonalSky = seasonalStar = null;
            }
        }

        void Build()
        {
            var sh = Shader.Find("OpenFusion/SkyFace");
            if (sh == null) { Debug.LogWarning("[sky] falta el shader OpenFusion/SkyFace"); return; }
            mats = new Material[6];
            for (int i = 0; i < 6; i++) mats[i] = new Material(sh) { hideFlags = HideFlags.HideAndDontSave, name = "Sky_" + Faces[i] };
            const float h = 5f;
            // vértices por cara (esquinas BL, BR, TR, TL vistas desde dentro)
            Vector3[][] q =
            {
                new[] { new Vector3(-h,-h, h), new Vector3( h,-h, h), new Vector3( h, h, h), new Vector3(-h, h, h) },   // front +Z (u +x)
                new[] { new Vector3( h,-h,-h), new Vector3(-h,-h,-h), new Vector3(-h, h,-h), new Vector3( h, h,-h) },   // back -Z (u -x)
                new[] { new Vector3( h,-h, h), new Vector3( h,-h,-h), new Vector3( h, h,-h), new Vector3( h, h, h) },   // left +X (u -z)
                new[] { new Vector3(-h,-h,-h), new Vector3(-h,-h, h), new Vector3(-h, h, h), new Vector3(-h, h,-h) },   // right -X (u +z)
                new[] { new Vector3(-h, h,-h), new Vector3( h, h,-h), new Vector3( h, h, h), new Vector3(-h, h, h) },   // up +Y (u +x, v +z)
                new[] { new Vector3(-h,-h, h), new Vector3( h,-h, h), new Vector3( h,-h,-h), new Vector3(-h,-h,-h) },   // down -Y
            };
            verts = new Vector3[24]; var uvs = new Vector2[24]; var m = new Mesh { name = "SkyCube", subMeshCount = 6 }; mesh = m;
            var uv4 = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            for (int f = 0; f < 6; f++) for (int k = 0; k < 4; k++) { verts[f * 4 + k] = q[f][k]; uvs[f * 4 + k] = uv4[k]; }
            m.vertices = verts; m.uv = uvs;
            for (int f = 0; f < 6; f++) m.SetTriangles(new[] { f * 4, f * 4 + 1, f * 4 + 2, f * 4, f * 4 + 2, f * 4 + 3 }, f);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            gameObject.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            built = true; LoadTextures(); BuildStar();
        }

        void LoadTextures()
        {
            SetUpFace();
            string pre = free ? "OpenFusion/Sky/f_" : "OpenFusion/Sky/n_";
            for (int i = 0; i < 6; i++)
            {
                var t = Resources.Load<Texture2D>(pre + Faces[i]);
                if (t != null) { t.wrapMode = TextureWrapMode.Clamp; mats[i].SetTexture("_MainTex", t); }
                else Debug.LogWarning("[sky] falta la textura " + pre + Faces[i]);
            }
        }

        // la cara superior viene girada de forma distinta en cada juego de texturas (medido por continuidad de bordes con las caras laterales):
        // normal: borde superior de la imagen = cara Front (+Z), izquierdo = Left (+X); zona libre: borde derecho = Front, superior = Left (+X).
        void SetUpFace()
        {
            if (mesh == null) return; const float h = 5f;
            // esquinas de la imagen BL, BR, TR, TL -> posición
            Vector3 bl, br, tr, tl;
            if (!free) { tl = new Vector3(h, h, h); tr = new Vector3(-h, h, h); br = new Vector3(-h, h, -h); bl = new Vector3(h, h, -h); }
            else { tr = new Vector3(h, h, h); tl = new Vector3(h, h, -h); br = new Vector3(-h, h, h); bl = new Vector3(-h, h, -h); }
            verts[16] = bl; verts[17] = br; verts[18] = tr; verts[19] = tl; mesh.vertices = verts;
        }

        // FusionStar del cliente original (hijo de la cámara, rotación fija en mundo): un plano de 10x10 en (0,8,12), textura fusionStar.mat, mezcla alfa.
        // El plano de Unity girado (0,.7071,-.7071,0) mira a -Z: u crece hacia -X y v hacia -Y (si se ve invertido, cambiar StarFlipU/StarFlipV).
        public static bool StarFlipU, StarFlipV;
        void BuildStar()
        {
            var sh = Shader.Find("OpenFusion/SkyStar"); var tex = Resources.Load<Texture2D>("OpenFusion/Sky/fusionstar");
            normalStar = tex;
            if (sh == null || tex == null) { Debug.LogWarning("[sky] falta el shader SkyStar o la textura fusionstar"); return; }
            tex.wrapMode = TextureWrapMode.Clamp;
            var go = new GameObject("FusionStar"); go.transform.SetParent(transform, false); go.transform.localPosition = new Vector3(0f, 8f, 12f); star = go.transform;
            var m = new Mesh { name = "FusionStarQuad" }; const float h = 5f;
            Vector2[] uv = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var v = new Vector3[4]; for (int i = 0; i < 4; i++) { float u = StarFlipU ? 1f - uv[i].x : uv[i].x, w = StarFlipV ? 1f - uv[i].y : uv[i].y; v[i] = new Vector3(h - 2f * h * u, h - 2f * h * w, 0f); }
            m.vertices = v; m.uv = uv; m.triangles = new[] { 0, 1, 2, 0, 2, 3 }; m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave, name = "FusionStar", mainTexture = tex };
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            starScale = -1f;
        }

        void LateUpdate()
        {
            if (cam != null) transform.position = cam.position;
            if (star != null) { starScale = starScale < 0f ? starTarget : starScale * (1f - Time.deltaTime) + starTarget * Time.deltaTime; star.localScale = Vector3.one * starScale; }
        }
        void OnDestroy() { if (mats != null) foreach (var m in mats) if (m != null) Destroy(m); }
    }
}
