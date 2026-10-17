// Modo "Animated Nanos" del original (InventoryManagerScript.cmNano + cnSimpleCharRenderCamera): el modelo 3D del nano se dibuja con una cámara
// (fov 45, Distance 2.3, fHeight 0.8, vAngle (-10,0,0)) sobre el rectángulo del slot. Aquí: un clon del nano por id lejos del mapa y un RenderTexture por nano.
// SIN COMPILAR EN UNITY.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFusion.World
{
    public sealed class NanoModelRt : MonoBehaviour
    {
        static NanoModelRt inst; void Awake() { inst = this; }
        /// DrawNanoCamera: desplazamiento vertical del punto de mira (nano "hundido" con poca estamina).
        public static void SetSink(int nanoId, float y) { if (inst != null) inst.sink[nanoId] = y; }
        readonly Dictionary<int, float> sink = new Dictionary<int, float>();
        public const int NanoLayer = 29;
        public int Size = 128; public float Fov = 45f, Distance = 2.3f, Height = 0.8f; public Vector3 Angle = new Vector3(-10f, 0f, 0f);
        ModelFactory models; ModelCatalog catalog;
        sealed class Entry { public GameObject Go; public Transform Pivot; public RenderTexture Rt; public Animation Anim; public BillboardNode[] Bills; public bool Failed; public float Last, LastTry; public bool Framed; public Vector3 Center; public float Dist; }
        readonly Dictionary<int, Entry> map = new Dictionary<int, Entry>();
        Camera cam; int slotCounter;

        public void Init(ModelFactory m, ModelCatalog c) { models = m; catalog = c; }

        /// Provider para NanoWheelHud.NanoModelProvider: devuelve la textura del nano (null mientras no se haya podido cargar el modelo -> se usa el icono).
        public Texture Get(int nanoId)
        {
            if (models == null || catalog == null || nanoId < 0) return null;
            if (!map.TryGetValue(nanoId, out var e)) { e = new Entry(); map[nanoId] = e; TryBuild(nanoId, e); }
            else if (e.Go == null && !e.Failed && Time.time - e.LastTry > 1f) TryBuild(nanoId, e);   // el bundle del nano puede llegar tarde
            e.Last = Time.time;
            return e.Go != null && e.Rt != null ? e.Rt : null;
        }

        /// <summary>Modelo 3D de un NPC por numero de tabla (estacion de nanos: cmNanoMachine, 500x300). Clave interna negativa.</summary>
        const int PB = 1000000;
        static int NpcNum(int id) { return id <= -PB ? -id - PB - 1 : -id - 1; }
        /// <summary>Retrato de la cabeza de un NPC (cnTrans: pCamera, 90x90). Clave interna menor que -PB.</summary>
        public Texture GetNpcPortrait(int npcNumber)
        {
            if (models == null || catalog == null || npcNumber <= 0) return null; int id = -(npcNumber + 1 + PB);
            if (!map.TryGetValue(id, out var e)) { e = new Entry(); map[id] = e; TryBuild(id, e); }
            else if (e.Go == null && !e.Failed && Time.time - e.LastTry > 1f) TryBuild(id, e);
            e.Last = Time.time;
            return e.Go != null && e.Rt != null ? e.Rt : null;
        }

        public Texture GetNpc(int npcNumber)
        {
            if (models == null || catalog == null || npcNumber <= 0) return null; int id = -(npcNumber + 1);
            if (!map.TryGetValue(id, out var e)) { e = new Entry(); map[id] = e; TryBuild(id, e); }
            else if (e.Go == null && !e.Failed && Time.time - e.LastTry > 1f) TryBuild(id, e);
            e.Last = Time.time;
            return e.Go != null && e.Rt != null ? e.Rt : null;
        }

        void TryBuild(int id, Entry e)
        {
            e.Last = Time.time; e.LastTry = Time.time;
            try
            {
                var m = id < 0 ? catalog.Npc(NpcNum(id)) : catalog.Nano(id); if (m == null || m.Model == null) { e.Failed = true; if (id < 0) Debug.LogWarning("[nanomodel] npc " + NpcNum(id) + " sin modelo en catalogo (" + (id <= -PB ? "retrato" : "estacion") + ")"); return; }
                var go = models.Instantiate(m, null); if (go == null) { if (id < 0) Debug.Log("[nanomodel] npc " + NpcNum(id) + ": bundle aun no disponible, se reintenta"); return; }   // aún sin bundle: se reintenta
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) { t.gameObject.SetActive(true); t.gameObject.layer = NanoLayer; }
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                var main = m.Tex1 != null ? models.Texture(m.Tex1) : null; var sub = m.Tex2 != null ? models.Texture(m.Tex2) : null;
                foreach (var rd in go.GetComponentsInChildren<Renderer>(true))
                {
                    rd.enabled = true;
                    foreach (var mat in rd.materials) { if (mat.name.Contains("sub") && sub != null) mat.SetTexture("_MainTex", sub); else if (mat.name.Contains("main") && main != null) mat.SetTexture("_MainTex", main); }
                }
                go.transform.SetParent(transform, false); int slot = slotCounter++;
                go.transform.position = new Vector3(6000f + slot * 20f, -4000f, 6000f); go.transform.rotation = Quaternion.identity;
                var an = go.GetComponent<Animation>() ?? go.GetComponentInChildren<Animation>(true);
                if (an != null)
                {
                    an.enabled = true; an.cullingType = AnimationCullingType.AlwaysAnimate; string pick = null;
                    foreach (var n in new[] { "stand1", "stand", "idle", "wait", "run" }) if (an[n] != null) { pick = n; break; }
                    if (pick == null) foreach (AnimationState st in an) { pick = st.name; break; }
                    if (pick != null) { an[pick].wrapMode = WrapMode.Loop; an.Play(pick); }
                }
                e.Go = go; e.Pivot = go.transform; e.Anim = an; e.Bills = go.GetComponentsInChildren<BillboardNode>(true); if (id < 0) Debug.Log("[nanomodel] npc " + NpcNum(id) + (id <= -PB ? " retrato" : " estacion") + " cargado, escala " + go.transform.localScale.x.ToString("F2"));
                e.Rt = new RenderTexture(id <= -PB ? 128 : id < 0 ? 500 : Size, id <= -PB ? 128 : id < 0 ? 300 : Size, 16, RenderTextureFormat.ARGB32) { name = (id < 0 ? "NpcRT" : "NanoRT") + id, antiAliasing = 1 }; e.Rt.Create();
            }
            catch (System.Exception ex) { e.Failed = true; Debug.LogWarning("[nanomodel] nano " + id + ": " + ex.Message); }
        }

        /// Encuadre automático: el modelo llena el cuadro (el original usa distancia fija 2.3, pero sus nanos están escalados para ese valor; aquí se calcula desde los bounds).
        public float FillFactor = 1.08f;
        void Frame(Entry e)
        {
            Bounds b = new Bounds(); bool has = false;
            foreach (var rd in e.Go.GetComponentsInChildren<Renderer>(false)) { if (!rd.enabled) continue; if (!has) { b = rd.bounds; has = true; } else b.Encapsulate(rd.bounds); }
            if (!has || b.size.sqrMagnitude < 1e-6f) { e.Center = new Vector3(0f, Height, 0f); e.Dist = Distance; e.Framed = true; return; }
            float t = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float asp = e.Rt != null && e.Rt.height > 0 ? (float)e.Rt.width / e.Rt.height : 1f; float half = Mathf.Max(b.size.y, b.size.x / asp) * 0.5f;
            e.Center = b.center - e.Pivot.position; e.Dist = (half / t + b.size.z * 0.5f) * FillFactor; e.Framed = true;
            Debug.Log("[nanomodel] nano encuadrado: tamaño " + b.size + " dist " + e.Dist.ToString("F2"));
        }

        /// Estacion de nanos: el original muestra la parte alta (planeta/holograma) de la estacion; se encuadran los renderers cuyo centro esta en el 60 % superior del modelo.
        public bool StationLegacyCam = true;
        /// NanoMachineCamera de main.unity3d (cnSimpleCharRenderCamera de Panel_UserClothes.cmNanoMachine): vAngle (23,-110,0), Distance 2.5, fHeight 3.6, fov 45, near 0.3, far 5, culling layer 10, clear DepthOnly.
        /// Mira a un punto 3.6 m sobre el pivote del NPC (esfera/holograma de la estacion) desde 2.5 m. Se usa far 8 para poder colocar el fondo (ver StationBackdrop) a 7.5 m.
        public Vector3 StationAngle = new Vector3(23f, -110f, 0f); public float StationDistance = 2.5f, StationHeight = 3.6f, StationNear = 0.3f, StationFar = 8f, StationBackdropDist = 7.5f;
        /// El original dibuja la camara con clear DepthOnly directamente sobre la interfaz (lo aditivo se suma al fondo). Aqui el RenderTexture se rellena con el fondo del panel (colthback2) mediante un quad
        /// a StationBackdropDist de la camara (shader OpenFusion/RtBackdrop): sin eso, los planos aditivos negros (halo/anillos) dejaban alfa 1 con color negro = cuadrado negro dentado.
        /// Los fija InventoryWindow.MyStuff antes de pedir el modelo: textura del fondo, rectangulo que ocupa en coordenadas del panel (ScaleToFit) y region visible del RT (0,0,500,300 del grupo desplazado 8,7).
        public static Texture StationBackdrop; public static Rect StationBackdropFit, StationBackdropView;
        /// Retrato: false = camara legacy exacta (cnSimpleCharRenderCamera); true = encuadre automatico de cabeza.
        public bool PortraitAuto = false;
        /// Estacion: false = modelo completo centrado (aspecto 500x300, sin recortar); true = solo el 40 % superior (holograma).
        public bool StationUpperOnly = false;
        void FrameStation(Entry e)
        {
            var rds = e.Go.GetComponentsInChildren<Renderer>(false); Bounds all = new Bounds(); bool has = false;
            foreach (var rd in rds) { if (!rd.enabled) continue; if (!has) { all = rd.bounds; has = true; } else all.Encapsulate(rd.bounds); }
            if (!has || all.size.sqrMagnitude < 1e-6f) { e.Dist = 0f; return; }
            float thr = all.min.y + all.size.y * 0.6f; Bounds up = new Bounds(); bool hu = false;
            foreach (var rd in rds) { if (!rd.enabled || rd.bounds.center.y < thr) continue; if (!hu) { up = rd.bounds; hu = true; } else up.Encapsulate(rd.bounds); }
            if (!hu || !StationUpperOnly) up = all;
            float t = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad); float asp = e.Rt != null && e.Rt.height > 0 ? (float)e.Rt.width / e.Rt.height : 1.667f;
            float half = Mathf.Max(up.size.y, up.size.x / asp) * 0.5f; e.Center = up.center - e.Pivot.position; e.Dist = (half / t + up.size.z * 0.5f) * FillFactor;
            Debug.Log("[nanomodel] estacion encuadrada: todo " + all.size + " holograma " + up.size + " centro " + e.Center + " dist " + e.Dist.ToString("F2"));
        }

        void EnsureCamera()
        {
            if (cam != null) return;
            var go = new GameObject("NanoRenderCamera"); go.transform.SetParent(transform, false); cam = go.AddComponent<Camera>(); cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f); cam.cullingMask = 1 << NanoLayer; cam.fieldOfView = Fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
            cam.allowHDR = false; cam.allowMSAA = false;
            var sh = Shader.Find("OpenFusion/RtBackdrop");
            if (sh != null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); var col = q.GetComponent<Collider>(); if (col != null) Destroy(col);
                q.name = "NanoBackdrop"; q.layer = NanoLayer; q.transform.SetParent(cam.transform, false);
                var qr = q.GetComponent<Renderer>(); qr.shadowCastingMode = ShadowCastingMode.Off; qr.receiveShadows = false; backMat = new Material(sh) { name = "NanoBackdropMat" }; qr.sharedMaterial = backMat;
                backQuad = q; q.SetActive(false);
            }
        }
        GameObject backQuad; Material backMat;

        /// Coloca el quad de fondo para que cubra todo el frustum y le asigna la region (u,v) del fondo del panel que corresponde al RT 500x300 (ScaleToFit del original).
        void PlaceBackdrop(bool on, float aspect)
        {
            if (backQuad == null) return;
            var tex = StationBackdrop; var fit = StationBackdropFit; var view = StationBackdropView;
            if (!on || tex == null || fit.width < 1f || fit.height < 1f || view.width < 1f || view.height < 1f) { if (backQuad.activeSelf) backQuad.SetActive(false); return; }
            float d = StationBackdropDist, h = 2f * d * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * 1.02f;
            backQuad.transform.localPosition = new Vector3(0f, 0f, d); backQuad.transform.localRotation = Quaternion.identity; backQuad.transform.localScale = new Vector3(h * aspect, h, 1f);
            // u = (x - fit.x) / fit.w ; v (textura, origen abajo) = 1 - (y - fit.y) / fit.h ; x,y en coordenadas del panel (arriba-izquierda)
            float su = view.width / fit.width, sv = view.height / fit.height, ou = (view.x - fit.x) / fit.width, ov = 1f - (view.yMax - fit.y) / fit.height;
            backMat.mainTexture = tex; backMat.mainTextureScale = new Vector2(su, sv); backMat.mainTextureOffset = new Vector2(ou, ov);
            if (!backQuad.activeSelf) backQuad.SetActive(true);
        }

        void LateUpdate()
        {
            Entry any = null; foreach (var e in map.Values) if (e.Go != null && Time.time - e.Last < 0.5f) { any = e; break; }
            if (any == null) return;
            EnsureCamera();
            foreach (var kv0 in map)
            {
                var e = kv0.Value; int key = kv0.Key;
                if (e.Go == null || e.Rt == null || Time.time - e.Last > 0.5f) continue;   // sólo los nanos que la rueda pidió este cuadro
                cam.nearClipPlane = 0.1f; bool useBackdrop = false;
                if (key <= -PB)
                {   // retrato cnTrans: cnSimpleCharRenderCamera (Distance 2.3, fHeight 0.8, vAngle (-10,0,0)), fov 45, farClip 5, mira al origen del NPC
                    if (!e.Framed)
                    {
                        e.Framed = true; Bounds ab = new Bounds(); bool hb = false;
                        foreach (var rd in e.Go.GetComponentsInChildren<Renderer>(false)) { if (!rd.enabled) continue; if (!hb) { ab = rd.bounds; hb = true; } else ab.Encapsulate(rd.bounds); }
                        Debug.Log("[nanomodel] retrato npc " + NpcNum(key) + ": bounds " + (hb ? ab.size.ToString("F2") : "vacio") + " base y " + (hb ? (ab.min.y - e.Pivot.position.y).ToString("F2") : "?") + (PortraitAuto ? " (auto)" : " (camara legacy)"));
                        if (PortraitAuto && hb)
                        {
                            float hh = ab.size.y; float cy = ab.max.y - hh * 0.12f;
                            e.Center = new Vector3(ab.center.x, cy, ab.center.z) - e.Pivot.position; e.Dist = (hh * 0.15f) / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) + ab.size.z * 0.3f;
                        }
                    }
                    var pv0 = e.Pivot.position;
                    if (PortraitAuto && e.Dist > 0f) { var ph = pv0 + e.Center; cam.transform.position = ph + Vector3.forward * e.Dist; cam.transform.LookAt(ph); }
                    else { cam.transform.position = pv0 + Quaternion.Euler(Angle) * Vector3.forward * 2.3f; cam.transform.LookAt(pv0); cam.transform.position += new Vector3(0f, 0.8f, 0f); }
                    cam.farClipPlane = 5f;
                }
                else if (key < 0)
                {   // cmNanoMachine (cnSimpleCharRenderCamera de NanoMachineCamera): Angle (23,-110,0), Distance 2.5, fHeight 3.6, near 0.3, far 5 sobre el pivote del NPC. Con StationLegacyCam=false se encuadra el modelo (FrameStation)
                    if (!e.Framed) { e.Framed = true; if (!StationLegacyCam) FrameStation(e); }
                    if (StationLegacyCam || e.Dist <= 0f)
                    {   // Update del original: pos = pivote + Euler(vAngle)*forward*Distance; LookAt(pivote); pos.y += fHeight (la direccion de mirada no se corrige tras subir la camara)
                        var pv = e.Pivot.position; cam.transform.position = pv + Quaternion.Euler(StationAngle) * Vector3.forward * StationDistance; cam.transform.LookAt(pv); cam.transform.position += new Vector3(0f, StationHeight, 0f);
                        cam.nearClipPlane = StationNear; cam.farClipPlane = StationFar; useBackdrop = true;
                    }
                    else { var pc = e.Pivot.position + e.Center; cam.transform.position = pc + Quaternion.Euler(Angle) * Vector3.forward * e.Dist; cam.transform.LookAt(pc); cam.farClipPlane = 50f; }
                }
                else
                {
                if (!e.Framed) Frame(e);
                float sk = 0f; sink.TryGetValue(key, out sk);
                var p = e.Pivot.position + e.Center + new Vector3(0f, sk, 0f); cam.transform.position = p + Quaternion.Euler(Angle) * Vector3.forward * e.Dist; cam.transform.LookAt(p);
                }
                if (key >= 0) cam.farClipPlane = 50f;
                cam.fieldOfView = Fov; cam.targetTexture = e.Rt;
                PlaceBackdrop(useBackdrop, (float)e.Rt.width / e.Rt.height);
                if (e.Bills != null) foreach (var b in e.Bills)
                {   // BillboardNode (desactivado en el clon): los planos *_BillboardCamera (halos de la estacion) miran a la camara del RT; Mode 0 = Camera, 1 = Up (solo guinada)
                    if (b == null) continue;
                    if (b.Mode == 0) b.transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
                    else if (b.Mode == 1) { var f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude > 1e-12f) b.transform.rotation = Quaternion.LookRotation(f) * Quaternion.Euler(0f, 180f, 0f); }
                }
                var req = new RenderPipeline.StandardRequest(); req.destination = e.Rt;
                try { if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render(); }
                catch (System.Exception ex) { Debug.LogWarning("[nanomodel] render: " + ex.Message); }
                cam.targetTexture = null;
            }
        }

        void OnDestroy() { foreach (var e in map.Values) { if (e.Rt != null) { e.Rt.Release(); Destroy(e.Rt); } if (e.Go != null) Destroy(e.Go); } }
    }
}
