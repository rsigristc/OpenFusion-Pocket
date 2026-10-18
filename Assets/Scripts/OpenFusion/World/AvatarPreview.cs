// AvatarPreview: vista 3D del personaje propio para My Stuff (cmOwnAvatar de Panel_UserClothes). SIN COMPILAR NI PROBAR EN UNITY.
// Construye un avatar aparte con PlayerAvatarFactory (capa 30, a 4000 m bajo el mundo para que la camara principal no lo vea) y lo dibuja con una camara propia a un RenderTexture con fondo transparente.
// Se reconstruye cuando cambia el equipo visible; Yaw lo giran las flechas de la ventana (el original rota 1 grado por fotograma).
using System;
using UnityEngine;
using UnityEngine.Rendering;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class AvatarPreview : MonoBehaviour
    {
        public const int Layer = 30;
        public PlayerAvatarFactory Factory;
        // PlayerAvatarFactory.Create(..., own:false) gira el modelo 180 grados dentro del contenedor: con Yaw 0 la camara (en +Z) ve la espalda; de frente = 180.
        public const float FrontYaw = 180f;
        public float Yaw = FrontYaw;       // grados
        /// <summary>Vehiculo montado a mostrar (id de la tabla de vehiculos, 0 = ninguno) y tablas para resolverlo. Quien dibuja lo actualiza cada fotograma.</summary>
        public int VehicleItem; public XdtTableScript Tables;
        public float Fov = 28f, Fill = 1.12f;
        public bool LegacyCam;               // camara fija del original (cnSimpleCharRenderCamera): solo la instancia de My Stuff
        public bool Head;                  // encuadre de cara (retratos del intercambio)
        int idx;                           // instancia (separa los avatares en el mundo de previsualizacion)
        static int extras;
        /// <summary>Instancia adicional (no singleton) para retratos: Head = solo la cabeza.</summary>
        public static AvatarPreview CreateExtra(GameObject host, PlayerAvatarFactory f, bool head)
        {
            var c = new GameObject("AvatarFace" + (extras + 1)); c.transform.SetParent(host.transform, false); var p = c.AddComponent<AvatarPreview>(); p.Factory = f; p.Head = head; p.idx = ++extras; p.Fov = 24f; return p;
        }
        static AvatarPreview inst;
        public static AvatarPreview Ensure(GameObject host) { if (inst != null) return inst; inst = host.GetComponent<AvatarPreview>(); if (inst == null) inst = host.AddComponent<AvatarPreview>(); inst.LegacyCam = true; return inst; }

        GameObject go; RenderTexture rt; Camera cam; string sig = ""; float last, retry; bool framed; Vector3 center; float dist; int w, h;
        Animation avAn; string avClip; GameObject vehGo; float vehRetry; ModelCatalog vehCat; XdtTableScript vehCatFor;

        static string Sig(PcAppearance a)
        {
            var s = new System.Text.StringBuilder(); s.Append(a.Style.Gender).Append('|'); foreach (var it in a.Equip) s.Append(it.Id).Append(',').Append(it.Opt).Append(';');
            var st = a.Style; s.Append(st.FaceStyle).Append(st.HairStyle).Append(st.HairColor).Append(st.SkinColor).Append(st.EyeColor).Append(st.Height).Append(st.Body);
            return s.ToString();
        }

        /// <summary>Textura del avatar (null mientras no se haya podido construir). Llamar cada fotograma mientras se muestra.</summary>
        public Texture Get(PcAppearance a, int width, int height)
        {
            if (a == null || Factory == null) return null; last = Time.unscaledTime;
            string s = Sig(a) + "|v" + VehicleItem;
            if (go == null || s != sig) { if (Time.unscaledTime < retry) return go != null && rt != null ? rt : null; Build(a, s, width, height); }
            return go != null && rt != null ? rt : null;
        }

        void Build(PcAppearance a, string s, int width, int height)
        {
            retry = Time.unscaledTime + 1.5f;
            try
            {
                var made = Factory.Create(a, transform, false); if (made == null) return;   // aun sin bundles: se reintenta
                if (go != null) Destroy(go); go = made; sig = s; framed = false; vehGo = null; vehRetry = 0f; avAn = null; avClip = null;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var sh in go.GetComponentsInChildren<BlobShadow>(true)) sh.enabled = false;
                go.transform.position = new Vector3(7000f + idx * 40f, -4000f, 7000f); go.transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
                foreach (var an in go.GetComponentsInChildren<Animation>(true)) { an.cullingType = AnimationCullingType.AlwaysAnimate; }
                if (rt == null || w != width || h != height) { if (rt != null) { rt.Release(); Destroy(rt); } w = width; h = height; rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "AvatarRT", antiAliasing = 1 }; rt.Create(); }
            }
            catch (Exception ex) { Debug.LogWarning("[avatar3d] " + ex.Message); }
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++) { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; }
            return null;
        }

        void UpdateVehicle()
        {
            if (go == null || VehicleItem <= 0 || vehGo != null || Time.unscaledTime < vehRetry) return;
            vehRetry = Time.unscaledTime + 1.5f; TryAttachVehicle();
        }

        /// <summary>InventoryManagerScript.LoadVehicle (vista previa con iVehicle > 0): modelo wear/&lt;MMesh&gt;.nif en el hueso "vehicle" (rot. 90 en X), textura en los materiales main/sub, animacion board_stand1 / scooter_stand1 y arma oculta.</summary>
        void TryAttachVehicle()
        {
            try
            {
                var vr = VehicleRide.Instance; if (vr == null || vr.GetModels == null || Tables == null) return;
                var mf = vr.GetModels(); if (mf == null) return;
                if (vehCat == null || vehCatFor != Tables) { vehCat = ModelCatalog.From(Tables); vehCatFor = Tables; }
                var vt = Tables.m_pVehicleItemTable; var d = vt != null ? vt.m_pItemData : null; if (d == null) return;
                ItemElement row = null;
                if (VehicleItem < d.Length && d[VehicleItem] != null && d[VehicleItem].m_iItemNumber == VehicleItem) row = d[VehicleItem];
                else foreach (var el in d) if (el != null && el.m_iItemNumber == VehicleItem) { row = el; break; }
                if (row == null || row.m_iEquipType <= 0) return;
                var mr = vehCat.Item(EquipSlot.Vehicle, row.m_iItemNumber); if (mr == null || mr.Model == null) return;
                var rf = mf.Locate(mr.Model); if (!rf.Ok) return;
                var vgo = mf.Builder.BuildPrefabAt(rf, null); if (vgo == null) return;
                string clip = row.m_iEquipType == 1 ? "board_stand1" : "scooter_stand1";
                foreach (var an in go.GetComponentsInChildren<Animation>(true)) { if (an == null || an.GetClip(clip) == null) continue; an.cullingType = AnimationCullingType.AlwaysAnimate; an[clip].wrapMode = WrapMode.Loop; an.Play(clip); avAn = an; avClip = clip; break; }
                foreach (var tg in go.GetComponentsInChildren<ActorAttachTag>(true)) if (tg.Slot == 3) foreach (var wr in tg.GetComponentsInChildren<Renderer>(true)) wr.enabled = false;
                WorldEntities.ActivateAll(vgo); vgo.SetActive(true);
                var bone = FindDeep(go.transform, "vehicle");
                vgo.transform.SetParent(bone != null ? bone : go.transform, false);
                vgo.transform.localPosition = Vector3.zero; vgo.transform.localRotation = bone != null ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity; vgo.transform.localScale = Vector3.one;
                if (bone != null) vgo.AddComponent<VehicleWorldScale>();
                foreach (var tr in vgo.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = Layer;
                Texture main = null; try { if (mr.Tex1 != null) main = mf.Texture(mr.Tex1); } catch (Exception) { }
                var av = vr.GetAvatars != null ? vr.GetAvatars() : null; var asm = av != null ? av.Assembler : null;
                foreach (var rd in vgo.GetComponentsInChildren<Renderer>(true))
                {
                    if (rd is ParticleSystemRenderer) continue; rd.enabled = true;
                    foreach (var m in rd.materials)
                    {
                        bool isMain = m.name.Contains("main"), isSub = m.name.Contains("sub");
                        if ((isMain || isSub) && main != null) { if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", main); else m.mainTexture = main; }
                        if (m.HasProperty("_SpecMap") && asm != null) { if (isMain && asm.MainSpec != null) m.SetTexture("_SpecMap", asm.MainSpec); else if (isSub && asm.SubSpec != null) m.SetTexture("_SpecMap", asm.SubSpec); }
                    }
                }
                foreach (var a2 in vgo.GetComponentsInChildren<Animation>(true)) { a2.cullingType = AnimationCullingType.AlwaysAnimate; foreach (AnimationState st in a2) st.wrapMode = WrapMode.Loop; a2.Play(); }
                vehGo = vgo; framed = false;
            }
            catch (Exception ex) { Debug.LogWarning("[avatar3d] vehiculo: " + ex.Message); }
        }

        /// <summary>El clip de vehiculo (board_/scooter_stand1) en bucle: si termino (WrapMode.Once) el avatar quedaba congelado mientras el vehiculo seguia animado.</summary>
        void KeepAvatarAnim()
        {
            if (avAn == null || string.IsNullOrEmpty(avClip)) return; var st = avAn[avClip]; if (st == null) return;
            if (st.wrapMode != WrapMode.Loop) st.wrapMode = WrapMode.Loop;
            if (!avAn.IsPlaying(avClip)) avAn.Play(avClip);
        }

        void EnsureCamera()
        {
            if (cam != null) return;
            var c = new GameObject("AvatarPreviewCamera"); c.transform.SetParent(transform, false); cam = c.AddComponent<Camera>(); cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f); cam.cullingMask = 1 << Layer; cam.fieldOfView = Fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 80f; cam.allowHDR = false; cam.allowMSAA = false;
        }

        void Frame()
        {
            Bounds b = new Bounds(); bool has = false;
            foreach (var rd in go.GetComponentsInChildren<Renderer>(false)) { if (!rd.enabled || rd is ParticleSystemRenderer) continue; if (!has) { b = rd.bounds; has = true; } else b.Encapsulate(rd.bounds); }
            if (!has || b.size.sqrMagnitude < 1e-6f) { center = go.transform.position + Vector3.up; dist = 5f; framed = true; return; }
            if (Head)
            {   // cara: ~13 % superior del cuerpo
                float hh = b.size.y * 0.13f, tf = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad); center = new Vector3(b.center.x, b.max.y - hh * 0.9f, b.center.z); dist = (hh / tf + b.size.z * 0.5f); framed = true; return;
            }
            center = b.center; float t = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad); float half = Mathf.Max(b.size.y * 0.5f, b.size.x * 0.5f * ((float)h / w));
            dist = (half / t + b.size.z * 0.5f) * Fill; framed = true;
        }

        bool TryOnVisible() => InventoryWindow.Instance != null && InventoryWindow.Instance.TryOnOpen;
        float nextDualFrame;
        void LateUpdate()
        {
            if (go == null || rt == null || Time.unscaledTime - last > 0.5f) return;
            if (idx == 0 && DualScreenController.Instance != null && DualScreenController.Instance.Active && DualScreenController.Instance.SecondaryTab == "Equipment" && !TryOnVisible())
            { if (Time.unscaledTime < nextDualFrame) return; nextDualFrame = Time.unscaledTime + DualEquipmentPreview.FrameInterval; }
            UpdateVehicle(); KeepAvatarAnim(); EnsureCamera(); go.transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
            if (!framed) Frame();
            if (Head || !LegacyCam) { cam.transform.position = center + new Vector3(0f, 0.15f, 0f) + Vector3.forward * dist; cam.transform.LookAt(center); }
            else
            {   // cnSimpleCharRenderCamera (legacy): Distance 2.65, fHeight 1, vAngle (-10,0,0), fov 45; la camara mira a los pies del avatar y luego sube fHeight (mismo encuadre con o sin vehiculo)
                Vector3 feet = go.transform.position; Vector3 p = feet + Quaternion.Euler(-10f, 0f, 0f) * Vector3.forward * 2.65f;
                cam.fieldOfView = 45f; cam.transform.position = p; cam.transform.LookAt(feet); cam.transform.position = p + new Vector3(0f, 1f, 0f);
            }
            try { PreviewRender.Render(cam, rt); }
            catch (Exception ex) { Debug.LogWarning("[avatar3d] render: " + ex.Message); }

        }

        void OnDestroy() { if (rt != null) { rt.Release(); Destroy(rt); } if (go != null) Destroy(go); if (inst == this) inst = null; }
    }
}
