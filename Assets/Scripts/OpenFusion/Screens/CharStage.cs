// Escenario 3D de las pantallas de selección/creación: avatares lejos del mapa (capa 28) y cámaras que se renderizan a RenderTexture (como el original:
// cnSimpleCharRenderCamera fov 45, pos = avatar + Euler(vAngle)*forward*Distance, LookAt(avatar), pos.y += fHeight). SIN COMPILAR EN UNITY.
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.World;

namespace OpenFusion.Screens
{
    /// <summary>Vista de un avatar en una RenderTexture. Modifica Distance/Height/Angle libremente; el escenario la renderiza cada cuadro mientras Active.</summary>
    public sealed class CharView
    {
        public GameObject Avatar; public RenderTexture Rt; public bool Active = true;
        public float Distance = 2f, Height = 0.75f, Fov = 45f; public Vector3 Angle = new Vector3(-10f, 0f, 0f);
        public Transform LookAt;   // null = la raíz del avatar (el original apunta a la raíz y sube la cámara fHeight); p. ej. "Bip01 Head" para las miniaturas
        public Texture Texture { get { return Rt; } }
    }

    public sealed class CharStage : MonoBehaviour
    {
        public const int Layer = 28;
        public static readonly Vector3 Origin = new Vector3(7000f, -4000f, 7000f);
        public Color RimColor = new Color(0x0B / 255f, 0x44 / 255f, 0x82 / 255f);
        PlayerAvatarFactory factory; Camera cam; Light sun; readonly List<CharView> views = new List<CharView>(); readonly List<GameObject> avatars = new List<GameObject>();

        public static CharStage Create(PlayerAvatarFactory f)
        {
            var go = new GameObject("CharStage"); go.transform.position = Origin; var st = go.AddComponent<CharStage>(); st.factory = f;
            var cg = new GameObject("StageCamera"); cg.transform.SetParent(go.transform, false); st.cam = cg.AddComponent<Camera>(); st.cam.enabled = false;
            st.cam.clearFlags = CameraClearFlags.SolidColor; st.cam.backgroundColor = new Color(0, 0, 0, 0); st.cam.cullingMask = 1 << Layer; st.cam.nearClipPlane = 0.05f; st.cam.farClipPlane = 60f; st.cam.allowHDR = false; st.cam.allowMSAA = false;
            var lg = new GameObject("StageSun"); lg.transform.SetParent(go.transform, false); lg.transform.rotation = Quaternion.Euler(35f, 160f, 0f); st.sun = lg.AddComponent<Light>(); st.sun.type = LightType.Directional; st.sun.intensity = 1.0f; st.sun.color = new Color(1f, 0.98f, 0.95f); st.sun.shadows = LightShadows.None;
            return st;
        }

        /// <summary>Crea el avatar de la apariencia dada en la posición local (x, y, z) del escenario. Devuelve null si falta género o piezas (se registra el aviso).</summary>
        public GameObject Spawn(PcAppearance a, Vector3 local)
        {
            if (factory == null || a == null) return null;
            GameObject go = null; try { go = factory.Create(a, transform, false); } catch (System.Exception e) { Debug.LogWarning("[stage] avatar: " + e.Message); }
            if (go == null) return null;
            foreach (var bs in go.GetComponentsInChildren<BlobShadow>(true)) Destroy(bs);
            go.transform.position = Origin + local; go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // mira hacia la cámara (+z -> cámara en +z... el original usa direction (0,0,1))
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            Shader.SetGlobalColor("_RimColor", RimColor);
            avatars.Add(go); return go;
        }
        public void Remove(GameObject go) { if (go == null) return; avatars.Remove(go); for (int i = views.Count - 1; i >= 0; i--) if (views[i].Avatar == go) { Release(views[i]); views.RemoveAt(i); } Destroy(go); }
        public void Clear() { foreach (var v in views) Release(v); views.Clear(); foreach (var g in avatars) if (g != null) Destroy(g); avatars.Clear(); }
        static void Release(CharView v) { if (v.Rt != null) { v.Rt.Release(); Object.Destroy(v.Rt); v.Rt = null; } }

        /// <summary>Vista del avatar en una RenderTexture de w x h píxeles.</summary>
        public CharView View(GameObject avatar, int w, int h, float distance, float height, Transform lookAt = null)
        {
            var v = new CharView { Avatar = avatar, Distance = distance, Height = height, LookAt = lookAt, Rt = UiKit.NewRt(w, h) }; views.Add(v); return v;
        }
        public void RemoveView(CharView v) { if (v == null) return; views.Remove(v); Release(v); }
        /// <summary>Transform de la cabeza ("Bip01 Head") para las miniaturas.</summary>
        public static Transform Head(GameObject avatar) { if (avatar == null) return null; foreach (var t in avatar.GetComponentsInChildren<Transform>(true)) if (t.name == "Bip01 Head") return t; return null; }

        void LateUpdate()
        {
            foreach (var v in views)
            {
                if (!v.Active || v.Avatar == null || v.Rt == null) continue;
                var p = v.LookAt != null ? v.LookAt.position : v.Avatar.transform.position;
                cam.fieldOfView = v.Fov; cam.transform.position = p + Quaternion.Euler(v.Angle) * Vector3.forward * v.Distance; cam.transform.LookAt(p);
                { var pos = cam.transform.position; pos.y += v.Height; cam.transform.position = pos; }   // cnSimpleCharRenderCamera: tras LookAt sube la cámara fHeight (también con "Bip01 Head")
                UiKit.RenderCam(cam, v.Rt);
            }
        }
        void OnDestroy() { foreach (var v in views) Release(v); views.Clear(); }
    }
}
