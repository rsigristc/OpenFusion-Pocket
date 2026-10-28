// Ventana WORLD MAP (WorldMapMode del original, GUISkin FusionFallMapSkin 1373). Ventana 1036x654 centrada sobre TutorialAssets/panelback.png.
//  - Mapa: Future / Darklands / Tutorial (zoom Type3 = zona, Type4 = vista local) y resto del mundo (4 niveles de zoom, payzone_00N). Textura = fuente UV WorldMapRect (suavizada 0,2/frame hacia TargetWorldMapRect).
//  - Iconos de NPC (MiniMapNpc = Data/clientnpc.json) filtrados por tipo (botones de filtro 553,45 + SHOW FILTERS), simbolos de mision (15 '!' / 16 '?'), nombre al pasar el raton.
//  - Jugador (map_icon_00 girado), waypoint de mision (icono 2/3/28/29), waypoints de usuario (clic derecho o pulsacion larga; hasta 6, claves PlayerPrefs "UserWaypoints"/"UserWaypointN{X,Z,H}").
//  - Arrastrar para desplazar, rueda / pellizco / barra de zoom, flechas (RepeatButton) para desplazar, MY VIEW / WORLD VIEW. Efectos de lineas (line_main / line_main00 / line_main01).
//  - Al abrir pide REQ_PRESENT_NPC_TYPES; solo se dibujan los tipos que el servidor dice presentes (si nunca responde, se dibujan todos).
//  - Arcos de transporte al pasar el raton por un icono Monkey/Scamper/Woosh (destinos de TransportationTable; gris = no registrado) y anillo giratorio sobre la XCom (resurreccion) mas cercana.
// Mapa de instancia (WorldMapMode.bInsFlag): dentro de una instancia sin EP (bInsMap && iEp_ID <= 0) se muestra el mensaje 174 (OK -> Exit) y solo se dibuja TutorialAssets/no_map sobre el panel.
// SIN COMPILAR EN UNITY.
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed partial class WorldMapWindow : MonoBehaviour
    {
        public static WorldMapWindow Instance;
        /// <summary>Carga una textura de los bundles del cliente por ruta (p. ej. "TutorialAssets/Mapframe.png").</summary>
        public Func<string, Texture> TexLoader;
        const int SK = Skin.Map;

        public static WorldMapWindow Ensure(GameObject host) { var w = host.GetComponent<WorldMapWindow>(); if (w == null) w = host.AddComponent<WorldMapWindow>(); return w; }

        enum Zoom { Type1, Type2, Type3, Type4 }

        // ---- geometria del original ----
        static readonly Rect WindowRect = new Rect(0, 0, 1036, 654), CloseR = new Rect(995, 13, 26, 26), HelpR = new Rect(995, 608, 30, 30), FilterR = new Rect(553, 45, 30, 30);
        static readonly Rect ClickRect = new Rect(70, 60, 870, 540), FrameRect = new Rect(17, 10, 960, 630), PicRect0 = new Rect(20, 14, 950, 624);
        static readonly Rect UpR = new Rect(478, 10, 40, 37), DnR = new Rect(478, 600, 40, 37), LtR = new Rect(17, 307, 37, 40), RtR = new Rect(938, 307, 37, 40);
        static readonly Rect LocalR = new Rect(25, 18, 110, 18), WorldR = new Rect(25, 38, 110, 18), ShowFiltersR = new Rect(857, 18, 110, 18);
        static readonly Rect ZoomInR = new Rect(25, 60, 26, 26), ZoomBarR = new Rect(35, 80, 8, 60), ZoomOutR = new Rect(25, 120, 26, 26);
        static readonly Rect[] ZoomBtn = { new Rect(28, 110, 20, 7), new Rect(28, 100, 20, 7), new Rect(28, 90, 20, 7) };
        static readonly Rect FutureZone = new Rect(5632, 512, 2048, 2048), DarkZone = new Rect(1024, 5120, 2560, 2048), TutorialZone = new Rect(0, 0, 1536, 1536);
        static readonly Vector2 ScreenSize = new Vector2(950f, 624f);
        const float World = 8192f, ScreenRatio = 0.6568421f;
        static readonly int[] MissionIcons = { 15, 16 }, ShopIcons = { 4, 5, 6, 7, 8, 9, 10, 31, 33 }, IndividualIcons = { 13, 12, 32, 22, 21, 34, 24, 11, 30, 25 };
        static readonly float[] Hues = { 235f, 290f, 60f, 100f, 30f, 180f };

        // ---- estado ----
        bool open, showFilters = true, bDragging, longFired; int zone = 3; Zoom zoom = Zoom.Type1;
        bool insFlag; Texture noMap;   // bInsFlag / NoiseTex
        readonly float[] zx = { 1f, 0.5f, 0.25f, 0.1159668f }, zy = { 1f, 0.5f, 0.25f, 0.1159668f };
        Rect worldRect = new Rect(0, 0, 1, 1), targetRect = new Rect(0, 0, 1, 1);
        string localName = "Player Location", nameTip = ""; Vector2 lastMouse, downPos; float downTime, sweep1, sweep2, pinchPrev;
        readonly HashSet<int> iconOn = new HashSet<int>(); bool filtersLoaded;
        readonly HashSet<int> present = new HashSet<int>(); volatile bool presentKnown; readonly object presentLock = new object();
        readonly System.Collections.Concurrent.ConcurrentQueue<Action> q = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        Texture back, frame; readonly Texture[] payZone = new Texture[4], freeZone = new Texture[2], darkZone = new Texture[2]; bool backTried;
        Texture iconBg, iconArrow, iconShine, lineMap, line0, line1, blackTex; Texture xcomBg, iconArrowDown;
        sealed class Uw { public float X, Z, Hue; public Rect Hit; }
        static readonly List<Uw> userWaypoints = new List<Uw>(); static bool uwLoaded;
        NanocomHud nh; WorldEntities ents; PacketDispatcher disp; Texture2D white;

        public void Init(PacketDispatcher d, NanocomHud n, WorldEntities e)
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            disp = d; nh = n; ents = e; if (disp != null) disp.Unhandled += OnPacket;
        }
        void Awake()
        {
            Instance = this; GameMenu.Implemented.Add(GameMenu.Win.Map);
            for (int i = 0; i < 4; i++) zy[i] *= ScreenRatio;
            LoadUserWaypoints();
        }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; if (Instance == this) Instance = null; }

        void OnPacket(uint type, byte[] b)
        {   // sP_FE2CL_REP_PRESENT_NPC_TYPES { int bClear; int iCnt } + iCnt x int
            if (type == PacketIds.P_FE2CL_REP_PC_REGIST_TRANSPORTATION_LOCATION_SUCC && b != null && b.Length >= 28)
            {   // sP_FE2CL_REP_PC_REGIST_TRANSPORTATION_LOCATION_SUCC { eTT, iLocationID, iWarpLocationFlag, long[2] aWyvernLocationFlag }
                var bb = b; q.Enqueue(() => { var qd = ents != null ? ents.QuestData : null; if (qd == null) return; qd.Warp = BitConverter.ToInt32(bb, 8); qd.Wyvern[0] = BitConverter.ToInt64(bb, 12); qd.Wyvern[1] = BitConverter.ToInt64(bb, 20); });
                return;
            }
            if (type != PacketIds.P_FE2CL_REP_PRESENT_NPC_TYPES || b == null || b.Length < 8) return;
            var body = b; q.Enqueue(() =>
            {
                int clear = BitConverter.ToInt32(body, 0), cnt = BitConverter.ToInt32(body, 4);
                if (clear != 0) present.Clear();
                for (int i = 0; i < cnt; i++) { int o = 8 + 4 * i; if (o + 4 > body.Length) break; present.Add(BitConverter.ToInt32(body, o)); }
                presentKnown = true;
            });
        }

        // ---- carga de textura con variantes de extension (el original pide .psd) ----
        Texture Load(string name)
        {
            if (TexLoader == null) return null;
            foreach (var ext in new[] { ".psd", ".png", ".dds", ".tga", "" })
            {
                try { var t = TexLoader("TutorialAssets/" + name + ext); if (t != null) return t; } catch (Exception) { }
            }
            Debug.LogWarning("[map] no se encontro la textura TutorialAssets/" + name); return null;
        }
        Texture PayZone(int i) { if (payZone[i] == null) payZone[i] = Load("payzone_00" + (i + 1)); return payZone[i]; }
        Texture FreeZoneTex(int i) { if (freeZone[i] == null) freeZone[i] = Load("freezone_00" + (i + 1)); return freeZone[i]; }
        Texture DarkZoneTex(int i) { if (darkZone[i] == null) darkZone[i] = Load("darkland_00" + (i + 1)); return darkZone[i]; }

        // ---- filtros (WorldMapMode.LoadFilters/SaveFilters: dos int con los 64 bits) ----
        void LoadFilters()
        {
            filtersLoaded = true; long def = 0; foreach (int i in MissionIcons) def |= 1L << i; foreach (int i in ShopIcons) def |= 1L << i; foreach (int i in IndividualIcons) def |= 1L << i;
            long lo = PlayerPrefs.GetInt("MapFiltersLow", (int)def) & 0xFFFFFFFFL, hi = PlayerPrefs.GetInt("MapFiltersHigh", (int)(def >> 32)) & 0xFFFFFFFFL; long f = lo | (hi << 32);
            iconOn.Clear(); for (int i = 0; i < 64; i++) if ((f & (1L << i)) != 0) iconOn.Add(i);
        }
        void SaveFilters() { long f = 0; foreach (int i in iconOn) f |= 1L << i; PlayerPrefs.SetInt("MapFiltersLow", (int)f); PlayerPrefs.SetInt("MapFiltersHigh", (int)(f >> 32)); }
        void SetIcons(IEnumerable<int> icons, bool on) { foreach (int i in icons) { if (on) iconOn.Add(i); else iconOn.Remove(i); } }

        // ---- waypoints de usuario (UserWaypoints) ----
        static void LoadUserWaypoints()
        {
            if (uwLoaded) return; uwLoaded = true; int n = Mathf.Min(PlayerPrefs.GetInt("UserWaypoints", 0), Hues.Length);
            for (int i = 0; i < n; i++) userWaypoints.Add(new Uw { X = PlayerPrefs.GetFloat("UserWaypoint" + i + "X"), Z = PlayerPrefs.GetFloat("UserWaypoint" + i + "Z"), Hue = PlayerPrefs.GetFloat("UserWaypoint" + i + "H") });
        }
        static void SaveUserWaypoints()
        {
            PlayerPrefs.SetInt("UserWaypoints", userWaypoints.Count);
            for (int i = 0; i < userWaypoints.Count; i++) { var w = userWaypoints[i]; PlayerPrefs.SetFloat("UserWaypoint" + i + "X", w.X); PlayerPrefs.SetFloat("UserWaypoint" + i + "Z", w.Z); PlayerPrefs.SetFloat("UserWaypoint" + i + "H", w.Hue); }
        }
        static float NextHue() { foreach (float h in Hues) { bool used = false; foreach (var w in userWaypoints) if (Mathf.Approximately(w.Hue, h)) { used = true; break; } if (!used) return h; } return 0f; }
        static int HueNumber(float hue) { for (int i = 0; i < Hues.Length; i++) if (Mathf.Approximately(Hues[i], hue)) return i + 1; return -1; }

        // ---- apertura / cierre ----
        void Update()
        {
            while (q.TryDequeue(out var a)) a();
            bool now = GameMenu.Current == GameMenu.Win.Map;
            if (now && !open) OnOpen(); else if (!now && open) OnClosed();
            open = now;
        }
        Transform Me { get { return ents != null && ents.Self != null ? ents.Self.transform : null; } }
        void OnOpen(bool notify = true)
        {
            if (dualInitialized) { bDragging = false; insFlag = RaceSystem.InsMap && RaceSystem.EpId <= 0; if (insFlag && noMap == null) noMap = Load("no_map"); return; }
            if (!filtersLoaded) LoadFilters();
            if (!backTried) { backTried = true; back = Load("panelback"); frame = Load("Mapframe"); }
            if (back == null && TexLoader != null) { try { back = TexLoader("TutorialAssets/panelback.png"); } catch (Exception) { } }
            localName = nh != null ? nh.CurrentDongName : "unknown"; nameTip = ""; bDragging = false; longFired = false; helpOpen = false;
            var me = Me; if (me != null) Debug.Log("[map] jugador pos=" + me.position + " yaw=" + me.eulerAngles.y + " waypoint=" + (nh != null && nh.ViewWayPoint ? nh.WayPoint.ToString() : "none") + " dong=" + localName); zone = 3; zoom = Zoom.Type1; zx[0] = 1f; zy[0] = 1f;
            if (me != null)
            {
                var pt = new Vector2(me.position.x, me.position.z); var c = new Vector2(pt.x / World, pt.y / World);
                if (FutureZone.Contains(pt)) { zone = 0; zoom = Zoom.Type3; }
                else if (DarkZone.Contains(pt)) { zone = 1; zoom = Zoom.Type3; }
                else if (TutorialZone.Contains(pt)) { zone = 2; zoom = Zoom.Type4; }
                SetZoomRect(c);
            }
            else SetZoomRect(new Vector2(0.5f, 0.5f));
            worldRect = targetRect;
            if (NpcUI.Sender != null) { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PRESENT_NPC_TYPES, 8); NpcUI.Sender(p); }   // uiLastSyncTime = 0
            insFlag = false;
            if (RaceSystem.InsMap && RaceSystem.EpId <= 0)
            {   // WorldMapMode: SendSystemMessageBox(174, "Exit") + NoiseTex (no_map)
                if (noMap == null) noMap = Load("no_map");
                insFlag = true; if (notify) SysMessageBox.Show(174, () => { if (GameMenu.Current == GameMenu.Win.Map) GameMenu.CloseWindow(); }, null);
            }
        }
        void OnClosed() { insFlag = false; SaveFilters(); SaveUserWaypoints(); PlayerPrefs.Save(); if (!dualInitialized) { for (int i = 0; i < 4; i++) payZone[i] = null; for (int i = 0; i < 2; i++) { freeZone[i] = null; darkZone[i] = null; } } bDragging = false; }

        // ---- zoom ----
        Rect ZoneRect() { return zone == 0 ? FutureZone : zone == 1 ? DarkZone : zone == 2 ? TutorialZone : new Rect(0, 0, World, World); }
        void SetZoomRect(Vector2 c)
        {
            float w = zx[(int)zoom], h = zy[(int)zoom]; targetRect = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h); Clamp();
        }
        void Clamp()
        {   // limites del mundo y de la zona (SetZoomRect / ClampWorldMap)
            float w = zx[(int)zoom], h = zy[(int)zoom]; var r = targetRect; r.width = w; r.height = h;
            var z = ZoneRect(); float x0 = z.x / World, y0 = z.y / World, x1 = z.xMax / World, y1 = z.yMax / World;
            if (r.x < x0) r.x = x0; if (r.y < y0) r.y = y0; if (r.x + w > x1) r.x = x1 - w; if (r.y + h > y1) r.y = y1 - h;
            if (r.x < 0f) r.x = 0f; if (r.y < 0f) r.y = 0f; targetRect = r;
        }
        Vector2 CenterPos() { return new Vector2(targetRect.x + targetRect.width * 0.5f, targetRect.y + targetRect.height * 0.5f); }
        bool CanZoomIn => zoom < Zoom.Type4;
        bool CanZoomOut => (int)zoom > (zone == 3 ? 0 : 2);
        void ZoomIn(Vector2 c)
        {
            if (!CanZoomIn) return;
            Sfx.Ui("height_up"); zoom = (Zoom)((int)zoom + 1); SetZoomRect(c);
        }
        void ZoomOut(Vector2 c)
        {
            if (!CanZoomOut) return;
            Sfx.Ui("height_down"); zoom = (Zoom)((int)zoom - 1); SetZoomRect(c);
        }

        // ---- geometria del mapa ----
        Rect PicRect()
        {   // Type1: letterbox del mundo completo (la imagen 8192x8192 se ajusta; 1/3 del sobrante arriba/izquierda)
            var r = PicRect0; if (zone != 3 || zoom != Zoom.Type1) return r; var t = PayZone(0); if (t == null) return r;
            if ((float)t.width / PicRect0.width > (float)t.height / PicRect0.height)
            { float n = PicRect0.height - t.height * PicRect0.width / t.width, n2 = n / 3f, n3 = n - n2; r.y += n2; r.height -= n3; }
            else { float n = PicRect0.width - t.width * PicRect0.height / t.height, n2 = n / 3f, n3 = n - n2; r.x += n2; r.width -= n3; }
            return r;
        }
        bool MouseToWorld(Rect pic, Vector2 m, out Vector2 w)
        {
            w = Vector2.zero; if (!pic.Contains(m)) return false;
            float u = (m.x - pic.x) / pic.width, v = 1f - (m.y - pic.y) / pic.height;
            w = new Vector2(targetRect.x + u * targetRect.width, targetRect.y + v * targetRect.height); return true;
        }
        Rect IconRect(Rect pic, Vector2 n, float size)
        {   // posicion normalizada del mundo -> rect en pantalla (Y invertida)
            float px = (n.x - worldRect.x) / worldRect.width, py = (n.y - worldRect.y) / worldRect.height;
            return new Rect(pic.x + px * pic.width - size * 0.5f, pic.y + pic.height - py * pic.height - size * 0.5f, size, size);
        }
        int ZoneOf(Vector2 p) { if (FutureZone.Contains(p)) return 0; if (DarkZone.Contains(p)) return 1; if (TutorialZone.Contains(p)) return 2; return 3; }
        bool DongAt(Vector2 p, out string dong, out string zoneName)
        {
            dong = zoneName = "unknown"; if (nh == null || !nh.DongAt(p, out dong, out zoneName)) return false;
            if (zone == 3 && (zoneName.Contains("The Future") || zoneName.Contains("The Darklands"))) return false;
            return true;
        }

        // ============================================================ dibujo ============================================================
        /// rotacion en el espacio local actual (GUIUtility.RotateAroundPivot aplica el pivote antes de la matriz de escala/centrado y descoloca los iconos)
        static Vector2 grp;
        static void Rot(float a, Vector2 pv) { pv += grp; GUI.matrix = GUI.matrix * Matrix4x4.TRS(pv, Quaternion.Euler(0f, 0f, a), Vector3.one) * Matrix4x4.TRS(-pv, Quaternion.identity, Vector3.one); }
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        void OnGUI()
        {
            if (!open) return; var e = Event.current; if (e == null || Screen.height <= 0) return;
            if (white == null) { white = Texture2D.whiteTexture; iconBg = UiKit.Tex("CCLeftIn3BG"); iconArrow = UiKit.Tex("menuChatArrow"); xcomBg = UiKit.Tex("map_icon_25_bgactive.tga"); iconArrowDown = RotCW(iconArrow); iconShine = UiKit.Tex("neonshinebg"); lineMap = UiKit.Tex("line_main"); line0 = UiKit.Tex("line_main00"); line1 = UiKit.Tex("line_main01"); blackTex = UiKit.Tex("back"); }
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f * 1.0f);
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1500; GUI.color = Color.white;
            try
            {
                GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
                if (e.type == EventType.Repaint)
                {
                    GUI.color = new Color(0f, 0f, 0f, back != null ? 1f : 0.75f); GUI.DrawTexture(new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc), white); GUI.color = Color.white;
                    if (back != null) GUI.DrawTexture(new Rect(-960f, -720f, 1920f, 1440f), back, ScaleMode.ScaleToFit);
                }
                var wr = new Rect(-WindowRect.width * 0.5f, -WindowRect.height * 0.5f, WindowRect.width, WindowRect.height);
                if (insFlag)
                {   // bInsFlag: solo el panel de fondo y la textura no_map (sin controles)
                    GUI.BeginGroup(wr); if (noMap != null && e.type == EventType.Repaint) GUI.DrawTexture(FrameRect, noMap, ScaleMode.ScaleToFit); GUI.EndGroup();
                    if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
                    return;
                }
                GUI.BeginGroup(wr); grp = wr.position;
                DrawWindow(e);
                GUI.EndGroup();
                if (nameTip.Length > 0 && e.type == EventType.Repaint)
                {   // etiqueta flotante con el nombre (estilo "window") junto al raton, en coordenadas de ventana
                    var m = e.mousePosition - new Vector2(wr.x, wr.y); var r = new Rect(wr.x + m.x + 4f, wr.y + m.y - 10f, 200f, 20f);
                    var st = Skin.Get(SK, "window"); if (st != null) Skin.Box(r, st, nameTip);
                }
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();   // ventana modal
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        bool Hold(Rect r, string style)
        {   // GUI.RepeatButton: verdadero mientras se mantiene pulsado
            var st = Skin.Get(SK, style); var e = Event.current; int id = GUIUtility.GetControlID(FocusType.Passive, r); bool hov = r.Contains(e.mousePosition), res = false;
            if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
            if (e.type == EventType.Repaint && st != null)
            {
                bool act = GUIUtility.hotControl == id && hov; var t = act ? (st.A != null ? st.A : st.N) : hov && GUIUtility.hotControl == 0 ? (st.H != null ? st.H : st.N) : st.N; Skin.Bg(r, t, st); res = act;
            }
            return res;
        }
        bool Btn(Rect r, string text, string style, bool en = true) { return Skin.Button(r, text, Skin.Get(SK, style), en); }

        void DrawWindow(Event e)
        {
            var me = Me; var box = Skin.Get(SK, "box"); if (box != null && e.type == EventType.Repaint) Skin.Bg(new Rect(0, 0, WindowRect.width, WindowRect.height), box.N, box);
            if (Btn(CloseR, "", "Closebutton")) { SaveFilters(); SaveUserWaypoints(); GameMenu.CloseWindow(); return; }
            if (Btn(HelpR, "", "help")) { if (GameMenu.Implemented.Contains(GameMenu.Win.Guide)) { GameMenu.CloseWindow(); GameMenu.OpenWindow(GameMenu.Win.Guide); return; } helpOpen = !helpOpen; }
            Rect pic = PicRect(); var wpHits = new List<KeyValuePair<Uw, Rect>>();
            if (e.type == EventType.Repaint) DrawMap(pic, me, wpHits);
            if (frame != null && e.type == EventType.Repaint) GUI.DrawTexture(FrameRect, frame);
            DrawFilters(e);
            // flechas de desplazamiento (RepeatButton): 1024 unidades de mundo por segundo
            float dt = Time.unscaledDeltaTime, step = 1024f / World * dt;
            if (Hold(DnR, "dn_arrow") && e.type == EventType.Repaint) { targetRect.y -= zy[(int)zoom] * step; Clamp(); }
            if (Hold(UpR, "up_arrow") && e.type == EventType.Repaint) { targetRect.y += zy[(int)zoom] * step; Clamp(); }
            if (Hold(LtR, "left_arrow") && e.type == EventType.Repaint) { targetRect.x -= zx[(int)zoom] * step; Clamp(); }
            if (Hold(RtR, "right_arrow") && e.type == EventType.Repaint) { targetRect.x += zx[(int)zoom] * step; Clamp(); }
            {
                Skin.Box(ZoomBarR, Skin.Get(SK, "zoom_bar"));
                if (Btn(ZoomOutR, "", "zoom_out", CanZoomOut)) ZoomOut(CenterPos());
                for (int k = zone == 3 ? 0 : 2; k < 3; k++)
                {
                    if (k == (int)zoom) Skin.Box(ZoomBtn[k], Skin.Get(SK, "zoom_select"));
                    else if (Btn(ZoomBtn[k], "", "zoom_but")) { var c = CenterPos(); zoom = (Zoom)k; SetZoomRect(c); }
                }
                if (Btn(ZoomInR, "", "zoom_in", CanZoomIn)) ZoomIn(CenterPos());
            }
            if (zoom == Zoom.Type4)
            {
                if (Btn(WorldR, "WORLD VIEW", "view_but"))
                {
                    zoom = zone == 3 ? Zoom.Type1 : Zoom.Type3; var c = me != null ? new Vector2(me.position.x / World, me.position.z / World) : new Vector2(0.5f, 0.5f); SetZoomRect(c);
                }
                Skin.Box(LocalR, Skin.Get(SK, "view_off"), "MY VIEW");
            }
            else
            {
                if (Btn(LocalR, "MY VIEW", "view_but")) { zoom = Zoom.Type4; var c = me != null ? new Vector2(me.position.x / World, me.position.z / World) : new Vector2(0.5f, 0.5f); SetZoomRect(c); }
                Skin.Box(WorldR, Skin.Get(SK, "view_off"), "WORLD VIEW");
            }
            Skin.Label(new Rect(55, 616, 110, 20), "CURRENT LOCATION:", Skin.Get(SK, "sfont")); Skin.Label(new Rect(170, 616, 270, 20), localName, Skin.Get(SK, "gray"));
            if (helpOpen) DrawHelp(e); else HandleMap(e, pic, wpHits);
        }
        bool helpOpen;
        static readonly string[] HelpLines =
        {
            "WORLD MAP", "",
            "Drag the map with the mouse (or one finger) to move it. Use the arrows to scroll.",
            "Use the mouse wheel, pinch, or the + / - buttons to zoom.",
            "MY VIEW centers the map on you. WORLD VIEW shows the whole world.",
            "SHOW FILTERS toggles which icons (missions, shops, vendors, nanos...) are displayed.",
            "Right-click (or press and hold) on the map to place a Custom Waypoint. Do the same on a waypoint to remove it. Up to 6.",
            "The red mission marker shows where to go; + means the target is above you, - below you.",
            "When a marker is off the map, an arrow on the edge points towards it.", "",
            "Click anywhere or press ? to close this help."
        };
        void DrawHelp(Event e)
        {
            var r = new Rect(250, 150, 540, 320);
            if (e.type == EventType.Repaint) { var oc = GUI.color; GUI.color = new Color(0, 0, 0, 0.88f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = oc; }
            var st = Skin.Get(SK, "sfont"); var ti = Skin.Get(SK, "gray");
            for (int i = 0; i < HelpLines.Length; i++) Skin.Label(new Rect(r.x + 15, r.y + 12 + i * 27f, r.width - 30, 26), HelpLines[i], i == 0 ? ti : st);
            if (e.type == EventType.MouseDown && e.button == 0) { helpOpen = false; e.Use(); }
        }

        // ---- mapa (Repaint) ----
        void DrawMap(Rect pic, Transform me, List<KeyValuePair<Uw, Rect>> wpHits)
        {
            worldRect.x = targetRect.x * 0.2f + worldRect.x * 0.8f; worldRect.y = targetRect.y * 0.2f + worldRect.y * 0.8f;
            worldRect.width = targetRect.width * 0.2f + worldRect.width * 0.8f; worldRect.height = targetRect.height * 0.2f + worldRect.height * 0.8f;
            Texture mapTex;
            if (zone == 0 || zone == 2) mapTex = FreeZoneTex(Mathf.Clamp((int)zoom - 2, 0, 1));
            else if (zone == 1) mapTex = DarkZoneTex(Mathf.Clamp((int)zoom - 2, 0, 1));
            else
            {
                if (zoom == Zoom.Type1 && blackTex != null) GUI.DrawTexture(PicRect0, blackTex);
                mapTex = PayZone((int)zoom);
            }
            if (mapTex != null) GUI.DrawTextureWithTexCoords(pic, mapTex, worldRect, false);
            nameTip = "";
            var mouse = Event.current.mousePosition; bool hovered = false; Texture hoverTex = null; Rect hoverRect = default(Rect); bool hoverGray = false;
            Rect view = default(Rect); bool haveView = me != null;
            if (haveView) view = new Rect(me.position.x / World - ScreenSize.x * 0.5f / World, me.position.z / World - ScreenSize.y * 0.5f / World, ScreenSize.x / World, ScreenSize.y / World);
            if (nh != null)
            {
                var all = nh.AllClientNpcs(); int xcomNpc = NearestXcomNpc(all, me);
                for (int i = 0; i < all.Count; i++)
                {
                    var pr = all[i]; var pos = pr.Value; var n = new Vector2(pos.x / World, pos.z / World);
                    if (!targetRect.Contains(n) || (zoom == Zoom.Type4 && !view.Contains(n))) continue;
                    var wp = new Vector2(pos.x, pos.z); if (ZoneOf(wp) != zone && zone != 3) continue; if (zone == 3 && ZoneOf(wp) != 3) continue;
                    var row = nh.NpcRowOf(pr.Key); if (row == null) continue;
                    if (presentKnown && !present.Contains(pr.Key)) continue;
                    int icon = row.m_iMapIcon; int sym = nh.MissionSymbolOf(pr.Key); if (sym == 2) icon = 16; else if (sym == 1) icon = 15;
                    if (icon <= 0 || (!iconOn.Contains(icon) && zoom != Zoom.Type4)) continue;
                    string dn, zn; if (!DongAt(wp, out dn, out zn)) continue;
                    var tex = nh.MapIconTexture(icon); if (tex == null) continue;
                    var r = IconRect(pic, n, 16f);
                    int tm = icon == 21 ? 1 : icon == 22 ? 2 : icon == 34 ? 3 : 0; bool reg = tm == 0 || TransportRegistered(pr.Key, tm);
                    if (icon == 25 && i == xcomNpc) DrawXcomRing(r);
                    bool hov = r.Contains(mouse); Color outline = Color.clear;
                    if (hov)
                    {
                        hovered = true; hoverTex = tex; hoverRect = r; hoverGray = !reg; nameTip = nh.NpcNameOf(pr.Key);
                        if (tm != 0)
                        {
                            Color c2 = tm == 1 ? new Color(0.878f, 0.769f, 1f) : tm == 2 ? new Color(0.682f, 1f, 0.718f) : new Color(1f, 0.859f, 0.765f); c2.a = Mathf.Sin(Time.time * 6f) * 0.5f + 0.75f;
                            outline = reg ? c2 : new Color(1f, 1f, 1f, 0.2f); DrawTransportArcs(pic, r, pr.Key, tm, c2, reg);
                        }
                    }
                    var ocl = GUI.color; if (!reg) GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                    if (outline.a > 0f) DrawOutline(r, tex, outline);
                    GUI.DrawTexture(r, tex); GUI.color = ocl;
                }
            }
            if (hovered && hoverTex != null) { var ohc = GUI.color; if (hoverGray) GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f); GUI.DrawTexture(new Rect(hoverRect.x - 2f, hoverRect.y - 2f, hoverRect.width + 4f, hoverRect.height + 4f), hoverTex); GUI.color = ohc; }
            if (!hovered && zoom != Zoom.Type4 && (mouse.x < FilterR.xMin || mouse.y > FilterR.yMax))
            {
                Vector2 w; if (MouseToWorld(pic, mouse, out w)) { string dn, zn; nameTip = DongAt(new Vector2(w.x * World, w.y * World), out dn, out zn) ? dn : "unknown"; }
            }
            // waypoint de mision
            if (nh != null && nh.ViewWayPoint && nh.WayPointOption) DrawWaypoint(pic, me, nh.WayPoint, true, Color.white);
            // waypoints de usuario
            foreach (var uw in userWaypoints)
            {
                var r = DrawWaypoint(pic, me, new Vector3(uw.X, me != null ? me.position.y : 0f, uw.Z), false, Color.HSVToRGB(Mathf.Repeat(uw.Hue, 360f) / 360f, 0.55f, 1f));
                wpHits.Add(new KeyValuePair<Uw, Rect>(uw, r)); if (r.Contains(mouse)) nameTip = "Custom Waypoint #" + HueNumber(uw.Hue);
            }
            // jugador
            if (me != null)
            {
                var n = new Vector2(me.position.x / World, me.position.z / World);
                if (targetRect.Contains(n))
                {
                    var r = IconRect(pic, n, 16f); var t = nh != null ? nh.MapIconTexture(0) : null;
                    if (t != null) { float yaw = me.eulerAngles.y + (nh != null ? nh.PlayerYawOffset : 0f); var m = GUI.matrix; Rot(yaw, new Vector2(r.x + 8f, r.y + 8f)); GUI.DrawTexture(r, t); GUI.matrix = m; }
                }
            }
            // efectos de lineas
            if (lineMap != null) GUI.DrawTexture(pic, lineMap);
            if (line0 != null)
            {
                var sr = pic; sr.height = line0.height; sweep1 += Time.unscaledDeltaTime * 0.15f; if (sweep1 > 1f + sr.height / pic.height) sweep1 = 0f;
                float y = sweep1 * pic.height - sr.height; var src = new Rect(0, 0, 1, 1);
                if (y < 0f) { src.y -= y / sr.height; src.height += y / sr.height; sr.height += y; y = 0f; }
                if (y > pic.height - sr.height) { float rem = pic.height - y; src.height = rem / sr.height; sr.height = rem; }
                sr.y += y; if (sr.height > 0f) GUI.DrawTextureWithTexCoords(sr, line0, src, true);
            }
            if (line1 != null)
            {
                var sr = pic; sr.height = line1.height; sweep2 += Time.unscaledDeltaTime * 0.5f; if (sweep2 > 1f) sweep2 = 0f;
                sr.y += Mathf.Min(pic.height - sr.height, sweep2 * pic.height); GUI.DrawTexture(sr, line1);
            }
        }

        // ---- transporte (TransportCache) y XCom ----
        TransportationTableScript TT { get { return nh != null && nh.Tables != null ? nh.Tables.m_pTransportationTable : null; } }
        static TransportLocationElement[] Locs(TransportationTableScript t, int tm) { return tm == 1 ? t.m_pBroomstickLocation : t.m_pTransportationWarpLocation; }   // 1 Monkey, 2 Scamper, 3 Woosh
        TransportLocationElement LocOfNpc(int npcType, int tm)
        {
            var t = TT; if (t == null) return null; var l = Locs(t, tm); if (l == null) return null;
            for (int i = 0; i < l.Length; i++) if (l[i] != null && l[i].m_iNPCID == npcType && l[i].m_iLocationID != 0) return l[i];
            return null;
        }
        bool LocRegistered(int tm, int id)
        {
            var qd = ents != null ? ents.QuestData : null; if (qd == null) return false;
            if (tm == 1) { if (id > 63) return (qd.Wyvern[1] & (1L << (id - 64))) != 0; return id >= 1 && (qd.Wyvern[0] & (1L << (id - 1))) != 0; }
            return id >= 1 && (qd.Warp & (1 << (id - 1))) != 0;
        }
        bool TransportRegistered(int npcType, int tm) { var l = LocOfNpc(npcType, tm); return l != null && LocRegistered(tm, l.m_iLocationID); }
        void DrawTransportArcs(Rect pic, Rect from, int npcType, int tm, Color active, bool fromRegistered)
        {   // WorldMapMode: un arco por destino de TransportationTable que sale de la ubicacion de este NPC
            if (Event.current.type != EventType.Repaint) return;
            var t = TT; var loc = LocOfNpc(npcType, tm); if (t == null || loc == null || t.m_pTransportationData == null) return;
            var locs = Locs(t, tm); var done = new HashSet<int>(); Color dim = new Color(1f, 1f, 1f, 0.2f);
            foreach (var row in t.m_pTransportationData)
            {
                if (row == null || (tm == 1 && row.m_iMoveType != 2) || row.m_iStartLocation != loc.m_iLocationID) continue;
                int end = row.m_iEndLocation; if (end <= 0 || end >= locs.Length) continue; var dst = locs[end]; if (dst == null || dst.m_iLocationID == 0 || !done.Add(dst.m_iLocationID)) continue;
                bool reg = LocRegistered(tm, dst.m_iLocationID); Vector2 wp = new Vector2(dst.m_iXpos * 0.01f, dst.m_iYpos * 0.01f), nn; string dn, zn;
                if (!DongAt(wp, out dn, out zn)) nn = new Vector2(targetRect.xMin, targetRect.yMax);
                else if (zone != ZoneOf(wp)) nn = new Vector2(targetRect.xMax, targetRect.yMin);
                else nn = wp / World;
                var to = IconRect(pic, nn, 16f); bool act = fromRegistered && reg; float dist = Vector2.Distance(from.center, to.center);
                DrawArc(from.center, to.center, dist * 0.2f, act ? active : dim, 2f + (float)(int)zoom, (int)(dist / 5f), !act, pic);
            }
        }
        void DrawArc(Vector2 a, Vector2 b, float bow, Color col, float width, int segs, bool dashed, Rect clip)
        {   // FFGUIUtility.DrawArc: curva de Bezier cuadratica a trozos (con trazos si dashed)
            if (segs < 1) segs = 1; Vector2 mid = (a + b) * 0.5f, dir = b - a; if (dir.sqrMagnitude < 1e-6f) return; dir.Normalize(); Vector2 ctl = mid + new Vector2(-dir.y, dir.x) * bow, p0 = a;
            for (int i = 1; i <= segs; i++)
            {
                float u = (float)i / segs, v = 1f - u; Vector2 p1 = v * v * a + 2f * v * u * ctl + u * u * b;
                if (!dashed || i % 2 == 0) Line(p0, p1, col, width, clip); p0 = p1;
            }
        }
        void Line(Vector2 a, Vector2 b, Color col, float width, Rect clip)
        {   // FFGUIUtility.DrawLine (recortada al rectangulo del mapa)
            if (!Clip(ref a, ref b, clip)) return; float len = Vector2.Distance(a, b); if (len <= 1e-4f || white == null) return;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * 57.29578f; var m = GUI.matrix; var oc = GUI.color; GUI.color = col; Rot(ang, a); GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, len, width), white); GUI.matrix = m; GUI.color = oc;
        }
        static bool Clip(ref Vector2 a, ref Vector2 b, Rect r)
        {   // Liang-Barsky
            float dx = b.x - a.x, dy = b.y - a.y, t0 = 0f, t1 = 1f; float[] p = { -dx, dx, -dy, dy }, qv = { a.x - r.xMin, r.xMax - a.x, a.y - r.yMin, r.yMax - a.y };
            for (int i = 0; i < 4; i++)
            {
                if (p[i] == 0f) { if (qv[i] < 0f) return false; continue; }
                float t = qv[i] / p[i];
                if (p[i] < 0f) { if (t > t1) return false; if (t > t0) t0 = t; } else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            Vector2 na = new Vector2(a.x + t0 * dx, a.y + t0 * dy), nb = new Vector2(a.x + t1 * dx, a.y + t1 * dy); a = na; b = nb; return true;
        }
        void DrawOutline(Rect r, Texture t, Color c)
        {   // FFGUIUtility.GetOutlinedTexture (1 px)
            var oc = GUI.color; GUI.color = c; foreach (var d in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1) }) GUI.DrawTexture(new Rect(r.x + d.x, r.y + d.y, r.width, r.height), t); GUI.color = oc;
        }
        readonly Dictionary<int, int> xcomNpcCache = new Dictionary<int, int>();
        int NearestXcomNpc(List<KeyValuePair<int, Vector3>> all, Transform me)
        {   // CnGuiChat.FindNearestValidXComForPC + GetNpcIndexForXcom (fuera de instancias)
            var xt = nh != null && nh.Tables != null ? nh.Tables.m_pXComTable : null; if (xt == null || xt.m_pXComData == null || me == null || all == null) return -1;
            XComElement best = null; float bd = float.MaxValue;
            for (int i = 1; i < xt.m_pXComData.Length; i++)
            {
                var x = xt.m_pXComData[i]; if (x == null || x.m_iXpos == 0 || x.m_iZone != 0) continue;
                float d = (PlayerAvatarFactory.ToClient(x.m_iXpos, x.m_iYpos, x.m_iZpos) - me.position).magnitude; if (d < bd) { bd = d; best = x; }
            }
            if (best == null) return -1; int idx; if (xcomNpcCache.TryGetValue(best.m_iXcomNumber, out idx)) return idx;
            var c = PlayerAvatarFactory.ToClient(best.m_iXpos, best.m_iYpos, best.m_iZpos); float bn = float.MaxValue; idx = -1;
            for (int k = 0; k < all.Count; k++) { float dd = new Vector2(all[k].Value.x - c.x, all[k].Value.z - c.z).magnitude; if (dd < bn) { bn = dd; idx = k; } }
            xcomNpcCache[best.m_iXcomNumber] = idx; return idx;
        }
        void DrawXcomRing(Rect r)
        {   // anillo giratorio (map_icon_25_bgactive, 28x28) sobre la XCom mas cercana
            if (xcomBg == null) return; var r4 = new Rect(r.x - 6f, r.y - 6f, 28f, 28f); float a = Time.time * 360f % 360f; var m = GUI.matrix;
            Rot(a, new Vector2(r4.x + 13.6f, r4.y + 14f)); GUI.DrawTexture(r4, xcomBg); GUI.matrix = m;
        }
        /// Copia girada 90 grados en sentido horario (equivale a RotateAroundPivot(90) del original sobre una flecha de 8x11).
        static Texture RotCW(Texture src)
        {
            if (src == null) return null;
            try
            {
                var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32); var prev = RenderTexture.active; Graphics.Blit(src, rt); RenderTexture.active = rt;
                var t = new Texture2D(src.width, src.height, TextureFormat.ARGB32, false); t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); t.Apply(); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                int w = t.width, h = t.height; var sp = t.GetPixels(); var dp = new Color[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) dp[(w - 1 - x) * h + y] = sp[y * w + x];   // (x,y) -> (y, w-1-x) en una imagen de h x w
                var o = new Texture2D(h, w, TextureFormat.ARGB32, false); o.SetPixels(dp); o.Apply(); o.filterMode = FilterMode.Bilinear; return o;
            }
            catch (Exception) { return null; }
        }

        /// DrawWaypoint: icono base (2), flecha (3) girada hacia el punto si cae fuera de la vista, (28/29) si esta >5 m por encima/debajo del jugador.
        Rect DrawWaypoint(Rect pic, Transform me, Vector3 worldPos, bool checkZone, Color tint)
        {
            var n = new Vector2(worldPos.x / World, worldPos.z / World);
            if (checkZone && ZoneOf(new Vector2(worldPos.x, worldPos.z)) != zone) return default(Rect);
            bool clamped = false;
            if (n.x < worldRect.xMin) { n.x = worldRect.xMin; clamped = true; } if (n.y < worldRect.yMin) { n.y = worldRect.yMin; clamped = true; }
            if (n.x > worldRect.xMax) { n.x = worldRect.xMax; clamped = true; } if (n.y > worldRect.yMax) { n.y = worldRect.yMax; clamped = true; }
            var r = IconRect(pic, n, 16f); var oc = GUI.color; GUI.color = tint;
            Texture baseT = nh != null ? nh.MapIconTexture(2) : null, arrowT = nh != null ? nh.MapIconTexture(3) : null, upT = nh != null ? nh.MapIconTexture(28) : null, downT = nh != null ? nh.MapIconTexture(29) : null;
            if (clamped)
            {
                float ax = (r.center.x - pic.center.x) / pic.width, ay = (r.center.y - pic.center.y) / pic.height, ang = Mathf.Atan2(ay, ax) * 180f / 3.14f + 90f;
                var m = GUI.matrix; Rot(ang, new Vector2(r.x + 8f, r.y + 8f)); if (arrowT != null) GUI.DrawTexture(r, arrowT); GUI.matrix = m;
            }
            else if (me != null && worldPos.y > me.position.y + 5f && upT != null) GUI.DrawTexture(r, upT);
            else if (me != null && worldPos.y < me.position.y - 5f && downT != null) GUI.DrawTexture(r, downT);
            else if (baseT != null) GUI.DrawTexture(r, baseT);
            GUI.color = oc; return r;
        }

        // ---- filtros ----
        void DrawFilters(Event e)
        {
            if (showFilters && zoom != Zoom.Type4)
            {
                int order = 0; Filter(15, MissionIcons, order++, e); Filter(10, ShopIcons, order++, e);
                for (int j = 0; j < IndividualIcons.Length; j++) Filter(IndividualIcons[j], new[] { IndividualIcons[j] }, order++, e);
            }
            if (Btn(ShowFiltersR, "SHOW FILTERS", showFilters ? "view_off" : "view_but")) showFilters = !showFilters;
        }
        void Filter(int rep, int[] set, int order, Event e)
        {
            bool on = iconOn.Contains(rep); var r = FilterR; r.x += (r.width + 2f) * order; bool hov = r.Contains(e.mousePosition); bool hi = on || hov;
            if (e.type == EventType.MouseDown && e.button == 0 && hov) { Sfx.Button(); SetIcons(set, !on); e.Use(); }
            if (e.type != EventType.Repaint) return;
            if (iconBg != null) GUI.DrawTexture(r, iconBg);
            if (iconArrowDown != null) GUI.DrawTexture(new Rect(r.center.x - 1f - 5.5f, r.y - 7f + 5.5f - 4f, 11f, 8f), iconArrowDown);   // el original gira iconArrow 90 grados sobre su centro: aqui ya viene girada
            var oc = GUI.color; GUI.color = new Color(1f, 1f, 1f, hov ? 1f : on ? 0.333f : 0.1f); if (iconShine != null) GUI.DrawTexture(r, iconShine, ScaleMode.StretchToFill, true); GUI.color = oc;
            var tex = nh != null ? nh.MapIconTexture(rep) : null; if (tex == null) return;
            var c = new Rect(r.center.x - tex.width * 0.5f, r.center.y - tex.height * 0.5f, tex.width, tex.height);
            if (hi) { GUI.color = new Color(0.19f, 0.98f, 0.98f, 1f); foreach (var d in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1) }) GUI.DrawTexture(new Rect(c.x + d.x, c.y + d.y, c.width, c.height), tex); GUI.color = oc; }
            GUI.DrawTexture(c, tex);
        }

        // ---- interaccion sobre el mapa: arrastre, rueda, pellizco, clic derecho / pulsacion larga ----
        void HandleMap(Event e, Rect pic, List<KeyValuePair<Uw, Rect>> wpHits)
        {
            if (e.type == EventType.Used) return;
            Vector2 m = e.mousePosition;
            if (e.type == EventType.ScrollWheel && ClickRect.Contains(m)) { Vector2 w; if (MouseToWorld(pic, m, out w)) { if (e.delta.y < 0f) ZoomIn(w); else ZoomOut(w); } e.Use(); return; }
            if (e.type == EventType.MouseDown && e.button == 0 && ClickRect.Contains(m)) { bDragging = zoom != Zoom.Type1; lastMouse = m; downPos = m; downTime = Time.unscaledTime; longFired = false; }
            else if (e.type == EventType.MouseDrag && e.button == 0 && lastMouse != Vector2.zero)
            {
                if (bDragging && ClickRect.Contains(m))
                {
                    var d = m - lastMouse;
                    if (d.sqrMagnitude > 0f) { targetRect.x -= d.x / PicRect0.width * zx[(int)zoom]; targetRect.y += d.y / PicRect0.height * zy[(int)zoom]; Clamp(); }
                }
                if ((m - downPos).magnitude > 8f) longFired = true;
                lastMouse = m;
            }
            else if (e.type == EventType.MouseUp && e.button == 0) { bDragging = false; lastMouse = Vector2.zero; }
            if (e.type == EventType.MouseDown && e.button == 1 && pic.Contains(m)) { AddOrRemoveWaypoint(pic, m, wpHits); e.Use(); }
            // pulsacion larga (tactil): sin mover el dedo durante 0,6 s = clic derecho
            if (e.type == EventType.Repaint && Input.GetMouseButton(0) && !longFired && lastMouse != Vector2.zero && Time.unscaledTime - downTime > 0.6f && ClickRect.Contains(downPos))
            { longFired = true; AddOrRemoveWaypoint(pic, downPos, wpHits); }
            // pellizco con dos dedos: zoom en el centro del gesto
            if (e.type == EventType.Repaint && Input.touchCount == 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1); float dist = Vector2.Distance(a.position, b.position);
                if (a.phase == TouchPhase.Began || b.phase == TouchPhase.Began) pinchPrev = dist;
                else if (Mathf.Abs(dist - pinchPrev) > 60f) { var c = CenterPos(); if (dist > pinchPrev) ZoomIn(c); else ZoomOut(c); pinchPrev = dist; }
                longFired = true; bDragging = false;
            }
        }
        void AddOrRemoveWaypoint(Rect pic, Vector2 m, List<KeyValuePair<Uw, Rect>> wpHits)
        {
            foreach (var kv in wpHits) if (kv.Value.Contains(m)) { userWaypoints.Remove(kv.Key); Sfx.Ui("scroll_down"); SaveUserWaypoints(); return; }
            Vector2 w; if (!MouseToWorld(pic, m, out w)) return;
            if (userWaypoints.Count >= Hues.Length) { Sfx.Ui("action_failure01"); return; }
            userWaypoints.Add(new Uw { X = w.x * World, Z = w.y * World, Hue = NextHue() }); Sfx.Ui("select_color"); SaveUserWaypoints();
        }
    }
}
