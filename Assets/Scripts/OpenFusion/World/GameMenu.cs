// Menu del juego (ESC) = cnGUINanocom.RenderMenu/SetViewMenu + panel izquierdo de MenuChatScript (LEAVE GROUP / WARP AWAY / HOP ON VEHICLE) + dialogo cnQuit.
//  - ESC (en Android, el boton Atras) abre/cierra el menu; si hay una ventana abierta, la cierra primero. No actua con el chat abierto, un dialogo de NPC o el popup de novedades.
//  - Menu derecho: fondo menu_box (178x294) deslizando desde el borde derecho (fSlideMenu 1 -> 0 a 2/s, curva sin^2), 7 botones 156x26 cada 29 px, X de cierre; GUISkin 1380.
//  - Panel izquierdo: GUISkin 1368 (EmoteBox + RedButton/BlueButton), grupo 172x238 a Screen.height-270, desliza de x=-180 a 0.
//  - Zonas de clic del HUD: minimapa -> mapa, texto de mision -> diario, ventana de estado del personaje -> inventario.
//  - Las ventanas (My Stuff, Journal, E-mail, Map, Settings, Guide) son componentes aparte que se dibujan cuando GameMenu.Current == Win.X y se registran con GameMenu.Implemented.Add(Win.X);
//    las que aun no existen no se abren (se registra en el log). Quit (EXIT GAME) esta implementado aqui.
// SIN COMPILAR EN UNITY.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class GameMenu : MonoBehaviour
    {
        public enum Win { None, MyStuff, Journal, EMail, Map, Settings, Guide, Quit, Bank, Vendor, Trade }

        public static bool Open { get; private set; }              // bViewMenu
        public static float Slide { get; private set; }            // fSlideMenu (1 -> 0)
        public static Win Current { get; private set; }
        public static readonly HashSet<Win> Implemented = new HashSet<Win> { Win.Quit };
        public static bool MouseOverHud { get; private set; }
        /// <summary>Menu visible/deslizando o ventana abierta: el avatar no recibe entrada (cnMainGame.ShowNanoMenu -> SetAvatarInputEnable(false)).</summary>
        /// <summary>Con Settings abierto se oculta todo el HUD (el original muestra la ventana a pantalla completa).</summary>
        public static bool HudHidden { get { return InventoryWindow.NanoAcquisitionActive || WelcomePopup.Active || Current == Win.Settings || Current == Win.Map || Current == Win.Journal || Current == Win.Guide || Current == Win.EMail || Current == Win.MyStuff || Current == Win.Bank || Current == Win.Vendor || Current == Win.Trade || TransportWindow.Active || GuideChangerWindow.Active || EnchantWindow.Active || CombineWindow.Active || RulesWindow.Active || BarberWindow.Shown; } }
        static bool NonLifeBlocking => SysMessageBox.Active || Open || Slide > 0f || BarberWindow.Active || GuideChangerWindow.Active || TransportWindow.Active || RaceSystem.Active || RulesWindow.Active || EnchantWindow.Active || CombineWindow.Active;
        static bool OtherBlocking => PlayerLife.Active || NonLifeBlocking;
        public static bool Blocking => InventoryWindow.NanoAcquisitionActive || TutorialTraining.OverlayBlocking || OtherBlocking || Current != Win.None;
        public static bool NanoStationOnly => !OtherBlocking && Current == Win.MyStuff && InventoryWindow.Machine;
        public static bool EquipmentInfoOnly => !OtherBlocking && Current == Win.MyStuff && InventoryWindow.Instance != null && InventoryWindow.Instance.DualItemPreview;
        public static bool DeathOnly => PlayerLife.Active && !NonLifeBlocking && Current == Win.None;
        public static bool NewMail;                                // cnGUINanocom.bNewMail (aun no hay correo en el cliente)

        public static void SetOpen(bool v, bool sound = true)
        {
            if(v && TutorialTraining.Active && (TutorialTraining.OverlayBlocking || TutorialTraining.Instance.Current<TutorialTraining.Lesson.Journal)) return;
            if (Open == v) return; Open = v; Slide = 1f;
            if (sound) Sfx.Ui(v ? "Open_Screen" : "Close_Screen");
        }
        public static bool OpenWindow(Win w)
        {
            if (InventoryWindow.NanoAcquisitionActive) return false;
            if(!TutorialTraining.WindowAllowed(w)) return false;
            if (w == Win.None) return false;
            if (!Implemented.Contains(w)) { Debug.Log("[menu] la ventana " + w + " aun no esta portada"); return false; }
            if (Open) SetOpen(false, false);
            Current = w; Sfx.Ui("Open_Screen"); return true;
        }
        public static void CloseWindow() { if (Current == Win.None) return; Current = Win.None; Sfx.Ui("Close_Screen"); }

        WorldEntities ents; PacketDispatcher disp; XdtTableScript tables; PcAppearance selfApp;
        public static volatile bool Riding; volatile bool inGroup, riding; volatile int regenTeleX, regenTeleY, regenTeleZ; volatile bool regenPending;
        bool busyPrev; float warpT, warpCool; bool warping; Win pressedHud = Win.None;
        Texture2D tBox; bool texTried;

        public void Init(WorldEntities e, PacketDispatcher d, XdtTableScript t)
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            ents = e; disp = d; tables = t;
            if (disp != null) disp.Unhandled += OnPacket;
            if (ents != null) ents.SelfSpawned += (go, es) => { selfApp = es.Self; if (go != null) { BlobShadow.OwnRoot = go.transform; BlobShadow.Refresh(); } };
        }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; Open = false; Slide = 0f; Current = Win.None; MouseOverHud = false; }
        void OnDisable() { MouseOverHud = false; }

        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        void OnPacket(uint type, byte[] b)   // hilo de red
        {
            if (type == PacketIds.P_FE2CL_PC_GROUP_JOIN_SUCC || type == PacketIds.P_FE2CL_PC_GROUP_JOIN) inGroup = true;
            else if (type == PacketIds.P_FE2CL_PC_GROUP_LEAVE_SUCC) inGroup = false;
            else if (type == PacketIds.P_FE2CL_PC_VEHICLE_ON_SUCC) { riding = true; Riding = true; }
            else if (type == PacketIds.P_FE2CL_PC_VEHICLE_OFF_SUCC) { riding = false; Riding = false; }
            else if (type == PacketIds.P_FE2CL_REP_PC_REGEN_SUCC && b != null && b.Length >= 48) {
                regenTeleX = I(b, 8); regenTeleY = I(b, 12); regenTeleZ = I(b, 16);
                // OpenFusion's revivePlayer sets bMoveLocation=0 even for a spawn relocation.
                // Coordinates are authoritative for normal AND in-place/Phoenix revival.
                Debug.Log("[life] REGEN_SUCC hp=" + I(b, 0) + " target=" + regenTeleX + "," + regenTeleY + "," + regenTeleZ + " moveFlag=" + b[40]);
                // HUD and this component process queues in different Updates. Stage the relocation
                // at receipt, before positive HP can resume locomotion at the old death position.
                GameClient.FindSelf?.QueueServerRelocation(regenTeleX, regenTeleY, regenTeleZ);
                regenPending = true;
            }
        }

        // ============================================================ entrada / estado ============================================================
        void Update()
        {
            if (InventoryWindow.NanoAcquisitionActive) return;
            float dt = Time.unscaledDeltaTime;
            if (Slide > 0f) Slide = Mathf.Max(0f, Slide - dt * 2f);
            if (warpCool > 0f) warpCool -= dt;
            if (regenPending) { regenPending = false; warping = false; if (ents != null) ents.RaiseTeleport(regenTeleX, regenTeleY, regenTeleZ); }
            if (warping) { warpT -= dt; if (warpT <= 0f) { warping = false; SendWarp(); } }
            if ((Input.GetKeyDown(KeyCode.Escape) || ControlMap.Down(ControlMap.K.Esc, false, true)) && !TouchProfileEditor.Active && !busyPrev && !ControlMap.Capturing && ControlMap.CancelCaptureFrame != Time.frameCount)
            {
                if (TutorialTraining.ClosePrompt()) return;
                if (Current != Win.None) CloseWindow();
                else if (Open) SetOpen(false);
                else SetOpen(true);
            }
            if (!busyPrev && !ChatHud.Typing && !NpcUI.Active && !WelcomePopup.Active && !EmailWindow.Typing && !InventoryWindow.Busy && !InventoryWindow.Typing && !ControlMap.Capturing)
            {   // teclas de ventana (ConfigurableInput: Inventory/Journal/QEmailBox/WorldMap/Help/Option)
                Hotkey(ControlMap.K.Inventory, Win.MyStuff); Hotkey(ControlMap.K.Journal, Win.Journal); Hotkey(ControlMap.K.QEmailBox, Win.EMail);
                Hotkey(ControlMap.K.WorldMap, Win.Map); Hotkey(ControlMap.K.Help, Win.Guide); Hotkey(ControlMap.K.Option, Win.Settings);
            }
        }
        void Hotkey(ControlMap.K k, Win w) { if (!ControlMap.Down(k, false, true)) return; if (Current == w) CloseWindow(); else if (Current == Win.None) OpenWindow(w); }
        // estado "ocupado" del frame anterior: el chat/NPC/popup consumen ESC en su propio Update, que puede ejecutarse antes que este
        void LateUpdate() { busyPrev = BarberWindow.Active || GuideChangerWindow.Active || TransportWindow.Active || RaceSystem.Active || RulesWindow.Active || EnchantWindow.Active || CombineWindow.Active || ChatHud.Typing || NpcUI.Active || WelcomePopup.Active || JournalWindow.PopupActive || EmailWindow.Typing || InventoryWindow.Busy || InventoryWindow.Typing || ModalWindow.Overlayed || SocialMenu.Active; }

        void Net(byte[] p) { if (NpcUI.Sender != null && p != null) NpcUI.Sender(p); }
        void SendWarp()
        {   // CnGuiChat.SendWarpAway: REQ_PC_REGEN { iRegenType = 5, eIL = 0, iIndex = XCom mas cercana }
            warpCool = 5f;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_REGEN, 12); ClientPackets.PutInt(p, 4, 5); ClientPackets.PutInt(p, 8, 0); ClientPackets.PutInt(p, 12, NearestXCom()); Net(p);
        }
        public int NearestXCom()
        {   // CnGuiChat.FindNearestValidXComForPC (fuera de instancias: m_iZone == 0)
            var xt = tables != null ? tables.m_pXComTable : null; if (xt == null || xt.m_pXComData == null || ents == null || ents.Self == null) return 0;
            Vector3 me = ents.Self.transform.position; int best = 0; float bd = float.MaxValue;
            for (int i = 1; i < xt.m_pXComData.Length; i++)
            {
                var x = xt.m_pXComData[i]; if (x == null || x.m_iXpos == 0) continue;
                if (RaceSystem.InsMap) { if (x.m_iZone == 0 || x.m_iZone != RaceSystem.InsMapNum) continue; } else if (x.m_iZone != 0) continue;   // CnGuiChat.FindNearestValidXComForPC
                float d = (PlayerAvatarFactory.ToClient(x.m_iXpos, x.m_iYpos, x.m_iZpos) - me).magnitude; if (d < bd) { bd = d; best = x.m_iXcomNumber; }
            }
            return best;
        }
        void SendExit()
        {   // cnMainGame.ReceiveToQuit/ToCharacter: REQ_PC_EXIT { iID }
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_EXIT, 4); ClientPackets.PutInt(p, 4, ents != null ? ents.SelfId : 0); Net(p);
        }

        // ============================================================ dibujo ============================================================
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }

        void OnGUI()
        {
            var e = Event.current; if (e == null || Screen.height <= 0) return;
            if (!texTried) { texTried = true; tBox = UiKit.Tex("menu_box"); }
            float W = Screen.width, H = Screen.height, Sc = GameOptions.UiScale; int od = GUI.depth; var om = GUI.matrix; var oc = GUI.color; GUI.depth = 6; GUI.color = Color.white;
            try
            {
                bool over = false;
                if (Current == Win.None && !Open && Slide <= 0f) HudZones(e, ref over);
                if (Open || Slide > 0f) { DrawMenu(e, W, H, Sc, ref over); DrawLeft(e, W, H, Sc, ref over); }
                if (warping) DrawWarp(W, H, Sc);
                if (Current == Win.Quit) { GUI.depth = 5; DrawQuit(e, W, H, Sc); over = true; }
                MouseOverHud = over;
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        // ---- clic en el HUD: minimapa -> mapa, texto de mision -> diario, estado del personaje -> inventario ----
        void HudZones(Event e, ref bool over)
        {
            Vector2 m = e.mousePosition; Win hit = Win.None;
            if (NanocomHud.MinimapScreen.Contains(m)) hit = Win.Map;
            else if (NanocomHud.MissionScreen.Contains(m)) hit = Win.Journal;
            else if (PlayerHud.StatusScreen.Contains(m)) hit = Win.MyStuff;
            if (hit != Win.None) over = true;
            if (e.type == EventType.MouseDown && e.button == 0) { pressedHud = hit; }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (pressedHud != Win.None && pressedHud == hit && !ChatHud.Typing && !NpcUI.Active && !WelcomePopup.Active) { Sfx.Button(); OpenWindow(hit); }
                pressedHud = Win.None;
            }
        }

        // ---- menu derecho (RenderMenu), pivote TopRight; coordenadas de diseno relativas al borde derecho ----
        static readonly string[] Names = { "MY STUFF", "JOURNAL", "E-MAIL", "MAP", "SETTINGS", "GAME GUIDE", "EXIT GAME" };
        static readonly Win[] Targets = { Win.MyStuff, Win.Journal, Win.EMail, Win.Map, Win.Settings, Win.Guide, Win.Quit };
        const float GroupW = 187f, GroupY = -4f, GroupH = 183f + 294f;
        void DrawMenu(Event e, float W, float H, float Sc, ref bool over)
        {
            float f2 = Slide * Slide; float n3 = Open ? Mathf.Sin(f2 * Mathf.PI * 0.5f) : Mathf.Sin((1f - f2) * Mathf.PI * 0.5f); n3 *= n3;
            GUI.matrix = Mat(new Vector2(W, 0f), Sc);
            var grp = new Rect(-GroupW + n3 * GroupW, GroupY, GroupW * (1f - n3), GroupH);
            bool ready = Slide <= 0f && Open;
            var btn = Skin.Get(Skin.Menu, "button"); var red = Skin.Get(Skin.Menu, "RedButton"); var close = Skin.Get(Skin.Menu, "closebutton");
            GUI.BeginGroup(grp);
            // fondo (MenuRect (0,-7,178,294) + (WindowRect.width-178, WindowRect.height-60))
            if (tBox != null) UiKit.Img(new Rect(9f, 116f, 178f, 294f), tBox);
            for (int i = 0; i < Names.Length; i++)
            {
                var r = new Rect(19f + 1f, 166f + 21f + (26f + 3f) * i, 156f, 26f);
                if (Skin.Button(r, Names[i], i == Names.Length - 1 ? red : btn, ready && TutorialTraining.WindowAllowed(Targets[i]), true)) { Sfx.Button(); OpenWindow(Targets[i]); }
            }
            if (Skin.Button(new Rect(162f, 158f, 23f, 23f), "", close, ready, true)) { Sfx.Button(); SetOpen(false); }
            GUI.EndGroup();
            // zona del menu para bloquear clics hacia el mundo
            if (new Rect(-GroupW + n3 * GroupW + 9f, GroupY + 116f, 178f, 294f).Contains(e.mousePosition)) over = true;
        }

        // ---- panel izquierdo (MenuChatScript.OnOwnGUI, modo "bShow"), pivote BottomLeft ----
        void DrawLeft(Event e, float W, float H, float Sc, ref bool over)
        {
            float f2 = Slide * Slide; float n3 = Open ? Mathf.Sin(f2 * Mathf.PI * 0.5f) : Mathf.Sin((1f - f2) * Mathf.PI * 0.5f); n3 *= n3;
            float gx = Mathf.Lerp(-180f, 0f, 1f - n3);
            GUI.matrix = Mat(new Vector2(0f, H), Sc);
            var box = Skin.Get(Skin.Chat, "EmoteBox"); var red = Skin.Get(Skin.Chat, "RedButton"); var blue = Skin.Get(Skin.Chat, "BlueButton");
            float gy = -270f;   // EmoteMenuPos.y = Screen.height - 270 (relativo al pivote inferior)
            GUI.BeginGroup(new Rect(gx, gy, 172f, 238f));
            Skin.Box(new Rect(0f, 1f, 170f, 127f), box);
            bool ready = Slide <= 0f && Open && !TutorialTraining.Active;
            bool cool = warpCool > 0f || warping;
            if (Skin.Button(new Rect(10f, 22f, 155f, 25f), "LEAVE GROUP", red, ready && inGroup, true)) { Sfx.Button(); Net(ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_GROUP_LEAVE, 1)); inGroup = false; }
            if (Skin.Button(new Rect(10f, 52f, 155f, 25f), "WARP AWAY", red, ready && !cool, true)) { Sfx.Button(); warping = true; warpT = RaceSystem.InsMap ? 20f : 10f; }
            bool hasVeh = selfApp != null && selfApp.Equip != null && selfApp.Equip.Length > 8 && selfApp.Equip[8].Id > 0;
            if (Skin.Button(new Rect(10f, 82f, 155f, 25f), riding ? "HOP OFF VEHICLE" : "HOP ON VEHICLE", blue, ready && hasVeh && !RaceSystem.InsMap, true))
            {
                Sfx.Button(); if (!VehicleRide.Toggle(true)) Net(ClientPackets.New(riding ? PacketIds.P_CL2FE_REQ_PC_VEHICLE_OFF : PacketIds.P_CL2FE_REQ_PC_VEHICLE_ON, 1)); SetOpen(false);   // CnGuiChat.OnVehicle
            }
            GUI.EndGroup();
            if (new Rect(gx, gy + 1f, 170f, 127f).Contains(e.mousePosition)) over = true;
        }

        // ---- cuenta atras de WARP (CnGuiChat: GUI.Label a pantalla completa con "BigFont70") ----
        void DrawWarp(float W, float H, float Sc)
        {
            if (Event.current.type != EventType.Repaint) return;
            var st = Skin.Get(Skin.Chat, "BigFont70"); if (st == null || st.Font == null) return;
            GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
            st.Font.Draw(new Rect(-W * 0.5f, -H * 0.5f, W, H), "WARP\n" + Mathf.Max(0f, warpT).ToString("0"), TextAnchor.MiddleCenter, true, Color.white, null);
        }

        // ---- dialogo de salida (cnQuit, GUISkin 1394), pivote centro ----
        const int QuitSkin = 1394;
        void DrawQuit(Event e, float W, float H, float Sc)
        {
            GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
            var back = Skin.Get(QuitSkin, "back"); var dlg = Skin.Get(QuitSkin, "dlg"); var btn = Skin.Get(QuitSkin, "button"); var cancel = Skin.Get(QuitSkin, "CancelButton");
            var full = new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc);
            if (back != null && e.type == EventType.Repaint) Skin.Bg(full, back.N, back);
            var d = new Rect(-103f, -94f, 206f, 188f);
            if (dlg != null && e.type == EventType.Repaint) Skin.Bg(d, dlg.N, dlg);
            if (e.type == EventType.MouseDown && !d.Contains(e.mousePosition)) e.Use();   // el fondo traga los clics
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(d.x + 13f, d.y + 16f + (45f + 8f) * i, 175f, 45f);
                string t = i == 0 ? "CHANGE CHARACTER" : i == 1 ? "QUIT GAME" : "CANCEL";
                if (Skin.Button(r, t, i == 2 ? cancel : btn))
                {
                    if (i == 2) CloseWindow();
                    else if (i == 0) { SendExit(); CloseWindow(); SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }   // aproximacion: el flujo de seleccion de personaje vuelve a empezar
                    else { SendExit(); Quit(); }
                }
            }
        }
        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
