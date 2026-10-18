// PlayerTarget: objetivo "otro jugador" al acercarse (cnAvatarAttack.Update/UpdateTargetUI/TalkUser + UserContainer.GetConeList + CnGuiMonster_info.DoWindowUser + PrintName.SetCombatIcon(2)). SIN COMPILAR NI PROBAR EN UNITY.
//   Seleccion: jugador remoto mas cercano dentro del cono de vision del propio avatar (iViewAnge 90 grados respecto a la camara aplanada; distancia = max(radio 0,3, alto 1,6 / 2) + 15 / 2 m; y de la diferencia x 0,5).
//              No se selecciona en combate (GameCondition eC_Combat, con PvP apagado) ni con ventanas abiertas. Si hay un monstruo mas cercano (distancia - 5 m <= distancia al jugador) manda el monstruo (NpcUI.MobTarget).
//   Marco:     ventana character_info (255x66) arriba al centro (pivote TopCenter): nombre (70,0,190,20), nivel "00" (90,23,60,20), barra HP (58,22,186,4) con retraso rojo, retrato (5,-3,64,64) por camara, freechat (3,10,18,14), disco (1,49,69,23).
//   Interaccion: mientras hay jugador objetivo (y no monstruo) el icono target_icon2 parpadea sobre su cabeza (PrintName.CombatIcon[2], alfa 0,5 + (sen(5t) + 1) * 0,25) y la tecla / clic / boton de ataque NO ataca: abre el menu de jugador (TalkUser -> NpcIconMode.DoPcWindow = TradeOverlay.OpenMenu).
// Pendiente: nano del jugador (nombre, barra, icono; requiere NANO_ACTIVE de otros), HP / nivel actualizados de otros jugadores (solo se conocen los de PC_NEW), icono freechat real (bit 64 de iSpecialState), tecla de modo combate (bAllowPlayerInteraction), PvP.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using OpenFusion.Net;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    public sealed class PlayerTarget : MonoBehaviour
    {
        public static PlayerTarget Instance;
        public static PlayerTarget Ensure(GameObject host) { var v = host.GetComponent<PlayerTarget>(); if (v == null) v = host.AddComponent<PlayerTarget>(); Instance = v; return v; }

        /// <summary>Id del jugador remoto seleccionado y mostrado en el marco (0 = ninguno). Con valor != 0 el ataque abre el menu de jugador.</summary>
        public static int TalkId { get; private set; }

        public int PortraitLayer = 26;
        public float PortraitFov = 60f, PortraitDistance = 0.5f, PortraitHeight = 0.2f, PortraitYaw = -20f;
        public string NeckBoneName = "Bip01 Neck", HeadBoneName = "Bip01 Head";
        const float ViewAngleDeg = 90f, ViewDistance = 15f * 0.5f, UserRadius = 0.3f, UserHeight = 1.6f, MobPenalty = 5f;
        static readonly Rect WindowRect = new Rect(0f, 5f, 255f, 66f), NameRect = new Rect(70f, 0f, 190f, 20f), LevelRect = new Rect(90f, 23f, 60f, 20f), HpRect = new Rect(58f, 22f, 186f, 4f),
            IconRect = new Rect(5f, -3f, 64f, 64f), FreeChatRect = new Rect(3f, 10f, 18f, 14f), DiskRect = new Rect(1f, 49f, 69f, 23f);
        static readonly Color LabelCol = new Color(0.9f, 0.9f, 0.9f, 1f);

        WorldEntities ents; PacketDispatcher disp; GameClient gc; PlayerHud hud;
        readonly ConcurrentDictionary<int, int> levels = new ConcurrentDictionary<int, int>(), hps = new ConcurrentDictionary<int, int>();
        int target;                       // seleccion de este frame (0 = ninguna)
        float shownHp = -1f; int shownId;
        BitmapFont font; bool fontTried;

        public void Init(WorldEntities e, PacketDispatcher d, GameClient g)
        {
            if (disp != null) { disp.PcEnter -= OnPcEnter; disp.PcExit -= OnPcExit; }
            ents = e; disp = d; gc = g; hud = GetComponent<PlayerHud>();
            if (disp != null) { disp.PcEnter += OnPcEnter; disp.PcExit += OnPcExit; }
        }
        void OnDestroy()
        {
            if (disp != null) { disp.PcEnter -= OnPcEnter; disp.PcExit -= OnPcExit; }
            if (clone != null) Destroy(clone.gameObject); if (rt != null) { rt.Release(); Destroy(rt); } if (cam != null) Destroy(cam.gameObject);
            if (Instance == this) Instance = null; TalkId = 0;
        }
        void OnPcEnter(PcAppearance a) { if (a == null) return; levels[a.Id] = a.Level; hps[a.Id] = a.Hp; }
        void OnPcExit(int id) { int v; levels.TryRemove(id, out v); hps.TryRemove(id, out v); }

        static bool Blocked()
        {
            return ChatHud.Typing || NpcUI.Active || WelcomePopup.Active || GameMenu.Blocking || TradeOverlay.Active;
        }

        /// <summary>Lo llama CombatSystem cada frame con "atacar pulsado". Devuelve true si hay jugador objetivo (el ataque no se ejecuta); en el flanco de pulsacion abre el menu (TalkUser).</summary>
        public static bool Intercept(bool want)
        {
            bool edge = want && !prevWantS; prevWantS = want;
            if (TalkId == 0 || Instance == null) return NpcUI.TalkIntercept(edge);   // sin jugador objetivo: atacar con NPC objetivo = hablar (TalkNpc)
            if (edge && !Blocked() && !VehicleRide.SelfRiding) Instance.Talk();
            return true;
        }
        static bool prevWantS;

        void Talk()
        {
            if (TradeWindow.Instance == null || TradeWindow.Active || TradeOverlay.MenuActive) return;
            TradeOverlay.OpenMenu(TalkId, TradeWindow.Instance.FullName(TalkId));
        }

        // ---------------- seleccion (UserContainer.GetConeList) ----------------
        void Update()
        {
            target = 0; TalkId = 0;
            if (ents == null || ents.Self == null || Camera.main == null || TradeWindow.Instance == null) return;
            if (GameMenu.Current != GameMenu.Win.None || NpcUI.Active) return;
            if (gc != null && Time.time < gc.CombatUntil) return;   // en combate no se selecciona a otros jugadores (bFlag && !bEnableAttackPC)
            var self = ents.Self.transform; Vector3 pos = self.position + Vector3.up * (UserHeight * 0.5f);
            var fw = Camera.main.transform.forward; fw.y = 0f; if (fw.sqrMagnitude < 1e-4f) fw = self.forward; fw.Normalize();
            int best = 0; float bd = float.MaxValue;
            foreach (var kv in ents.OtherPlayers)
            {
                var go = kv.Value; if (go == null || !go.activeInHierarchy) continue; if (RemoteCombatAnim.IsDead(kv.Key)) continue;
                var p2 = go.transform.position;
                if (p2.y < pos.y && pos.y < p2.y + UserHeight) p2.y = pos.y; else if (p2.y > pos.y) { } else p2.y = p2.y + UserHeight;
                var d = p2 - pos; d.y *= 0.5f; float dist = d.magnitude; if (dist < 1e-4f) { best = kv.Key; bd = 0f; break; }
                float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d / dist, fw), -1f, 1f)) * Mathf.Rad2Deg;
                if (ang > ViewAngleDeg) continue;
                float lim = Mathf.Max(UserRadius, UserHeight * 0.5f) + ViewDistance; if (d.sqrMagnitude >= lim * lim) continue;
                if (dist < bd) { bd = dist; best = kv.Key; }
            }
            if (best == 0) return;
            var fn = NpcUI.TalkNpc;
            if (fn != null && NpcUI.TalkNpcDist - MobPenalty <= bd) return;   // UpdateTargetUI: gana el NPC (Npc_Penalty 5 sin PvP): solo se ve el marco del NPC
            var mob = NpcUI.MobTarget;
            if (mob != null && mob != fn && mob.Hp > 0 && mob.SortDist - MobPenalty <= bd) return;   // UpdateTargetUI: gana el monstruo mas cercano
            target = best; TalkId = best;
        }

        // ---------------- retrato (cnCharRenderCamera "OtherUser_RTCamera") ----------------
        Camera cam; RenderTexture rt; int rtW, rtH; Transform neck, clone; int cloneId; static readonly Vector3 CloneAt = new Vector3(500f, -4000f, 0f);
        static Transform FindDeep(Transform r, string n) { if (r.name == n) return r; for (int i = 0; i < r.childCount; i++) { var f = FindDeep(r.GetChild(i), n); if (f != null) return f; } return null; }
        void EnsureCamera()
        {
            if (cam != null) return;
            var go = new GameObject("PlayerTarget_PortraitCamera"); go.hideFlags = HideFlags.HideAndDontSave; DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>(); cam.enabled = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 8f; cam.allowHDR = false; cam.allowMSAA = false; cam.useOcclusionCulling = false;
        }
        void EnsureRt(float scale)
        {
            int w = Mathf.Max(32, Mathf.CeilToInt(IconRect.width * scale * 2f)), h = Mathf.Max(32, Mathf.CeilToInt(IconRect.height * scale * 2f));
            if (rt != null && rtW == w && rtH == h) return;
            if (rt != null) { rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.name = "PlayerTarget_Portrait"; rt.antiAliasing = 1; rt.filterMode = FilterMode.Bilinear; rt.wrapMode = TextureWrapMode.Clamp; rt.Create(); rtW = w; rtH = h; portraitValid = false;
        }
        bool portraitValid;
        void EnsureClone(GameObject src)
        {
            if (clone != null && cloneId == target) return;
            if (clone != null) Destroy(clone.gameObject);
            clone = null; neck = null; portraitValid = false; cloneId = target; if (src == null) return;
            var go = Instantiate(src); go.name = "PortraitCloneOther"; go.SetActive(true);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) { if (t == null) continue; t.gameObject.layer = PortraitLayer; if (t.name == "BlobShadow" || t.name == "shadow") t.gameObject.SetActive(false); }
            go.transform.SetParent(null); go.transform.SetPositionAndRotation(CloneAt, Quaternion.identity);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = !(r is ParticleSystemRenderer);
            var an = go.GetComponentInChildren<Animation>(true);
            if (an != null) { an.enabled = true; an.cullingType = AnimationCullingType.AlwaysAnimate; var st = an["stand1"]; if (st != null) { st.wrapMode = WrapMode.Loop; an.Play("stand1"); } }
            clone = go.transform;
        }
        void LateUpdate()
        {
            if (target == 0 || GameMenu.HudHidden)
            {
                if (clone != null) { Destroy(clone.gameObject); clone = null; cloneId = 0; portraitValid = false; }
                return;
            }
            try
            {
                GameObject src; var t = ents.TransformOfPlayer(target); src = t != null ? t.gameObject : null;
                EnsureCamera(); EnsureRt(GameOptions.UiScale); EnsureClone(src); if (clone == null) return;
                if (neck == null || !neck.IsChildOf(clone)) { neck = FindDeep(clone, NeckBoneName); if (neck == null) neck = FindDeep(clone, HeadBoneName); }
                Vector3 tp; if (neck != null) tp = neck.position; else tp = clone.position + Vector3.up * 1.4f;
                Quaternion rot = clone.rotation;
                Vector3 pos = tp + rot * Quaternion.Euler(0f, PortraitYaw, 0f) * Vector3.forward * PortraitDistance;
                cam.transform.position = pos; cam.transform.LookAt(tp); pos = cam.transform.position; pos.y += PortraitHeight; cam.transform.position = pos;
                cam.fieldOfView = PortraitFov; cam.aspect = IconRect.width / IconRect.height; cam.cullingMask = 1 << PortraitLayer;
                PreviewRender.Render(cam, rt);
                portraitValid = true;
            }
            catch (Exception e) { Debug.LogWarning("[objetivo] retrato: " + e.Message); portraitValid = false; }

        }

        // ---------------- dibujo ----------------
        float headH = 1.6f, headAt = -10f; int headId;
        void OnGUI()
        {
            var e = Event.current; if (e == null || e.type != EventType.Repaint || target == 0 || ents == null) return;
            if (GameMenu.HudHidden || NpcUI.Active) return;
            var tr = ents.TransformOfPlayer(target); if (tr == null) return;
            if (!fontTried) { fontTried = true; font = BitmapFontLoader.Load("ChaletBook-Regular_Small__1018"); }
            float s = GameOptions.UiScale, W = Screen.width; var oc = GUI.color; int od = GUI.depth; GUI.depth = 11;
            try
            {
                // ventana (pivote TopCenter): grupo (x centrada, y 5) del tamano de WindowUserRect
                float gx = W * 0.5f - WindowRect.width * s * 0.5f, gy = WindowRect.y * s;
                GUI.BeginGroup(new Rect(gx, gy, WindowRect.width * s, WindowRect.height * s));
                var back = UiKit.Tex("character_info"); if (back != null) GUI.DrawTexture(Sc(WindowRect, s), back, ScaleMode.StretchToFill, true);
                string nm = TradeWindow.Instance != null ? TradeWindow.Instance.FullName(target) : "";
                int lv; levels.TryGetValue(target, out lv); int hp; hps.TryGetValue(target, out hp);
                Txt(NameRect, nm, s); Txt(LevelRect, Mathf.Max(1, lv).ToString("00"), s);
                int max = hud != null && hud.MaxHpProvider != null ? hud.MaxHpProvider(Mathf.Max(1, lv)) : 0; float v = max > 0 ? Mathf.Clamp01(hp / (float)max) : 1f;
                if (shownId != target || shownHp < 0f) { shownId = target; shownHp = v; } else if (shownHp > v) shownHp = Mathf.MoveTowards(shownHp, v, Time.deltaTime * 0.6f); else shownHp = v;
                var bar = UiKit.Tex("HP_BAR");
                if (bar != null)
                {
                    var r = HpRect; r.width *= shownHp; GUI.color = Color.red; GUI.DrawTexture(Sc(r, s), bar, ScaleMode.StretchToFill, true);
                    r.width = HpRect.width * v; GUI.color = Color.white; GUI.DrawTexture(Sc(r, s), bar, ScaleMode.StretchToFill, true);
                }
                GUI.color = Color.white;
                if (portraitValid && rt != null) GUI.DrawTexture(Sc(IconRect, s), rt, ScaleMode.StretchToFill, true);
                var fc = UiKit.Tex("freechat_icon"); if (fc != null) GUI.DrawTexture(Sc(FreeChatRect, s), fc, ScaleMode.StretchToFill, true);
                var disk = UiKit.Tex("DiskFront"); if (disk != null) GUI.DrawTexture(Sc(DiskRect, s), disk, ScaleMode.StretchToFill, true);
                GUI.EndGroup();
                // icono de objetivo sobre la cabeza (PrintName.CombatIcon[2], altura 0,8)
                var cm = Camera.main; var ic = UiKit.Tex("target_icon2");
                if (cm != null && ic != null && TalkId == target)
                {
                    if (headId != target || Time.unscaledTime - headAt > 1f) { headId = target; headAt = Time.unscaledTime; headH = BoundsHeight(tr.gameObject); }
                    var sp = cm.WorldToScreenPoint(tr.position + Vector3.up * (headH * 0.8f));
                    if (sp.z > 0.1f)
                    {
                        float a = 0.5f + (Mathf.Sin(Time.time * 5f) + 1f) * 0.25f; GUI.color = new Color(a, a, a, a); GUI.depth = 900;
                        float w = ic.width * s, h = ic.height * s; GUI.DrawTexture(new Rect(sp.x - w * 0.5f, Screen.height - sp.y - h * 0.5f, w, h), ic, ScaleMode.StretchToFill, true);
                    }
                }
            }
            finally { GUI.color = oc; GUI.depth = od; }
        }
        static Rect Sc(Rect r, float s) { return new Rect(r.x * s, r.y * s, r.width * s, r.height * s); }
        void Txt(Rect r, string text, float s)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (font != null) { font.Draw(r, text, TextAnchor.MiddleLeft, true, LabelCol, null, Vector2.zero, s); return; }
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.Max(8, Mathf.RoundToInt(12 * s)), clipping = TextClipping.Clip }; st.normal.textColor = LabelCol; GUI.Label(Sc(r, s), text, st);
        }
        static float BoundsHeight(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(); Bounds b = default(Bounds); bool has = false;
            foreach (var r in rs) { if (r is ParticleSystemRenderer || !r.enabled || !r.gameObject.activeInHierarchy || r.gameObject.name == "blobshadow") continue; if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            return has ? Mathf.Clamp(b.max.y - go.transform.position.y, 0.8f, 8f) : 1.6f;
        }
    }
}
