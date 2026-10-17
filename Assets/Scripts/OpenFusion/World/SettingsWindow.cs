// Ventana SETTINGS (cnOptionMode del original, GUISkin FusionFallOptionSkin 1365). Ventana 1020x638 centrada, fondo TutorialAssets/panelback.png, 4 pestanas.
// Portado: GRAPHICS & SOUND (completa, con las coordenadas del original) y GAME UI (elementos flotantes). Pendientes: colores de chat y escala de UI (GAME UI), SOCIAL y CONTROLS.
// Extra del port movil (no existe en el original): boton ADVANCED GRAPHICS (resolucion interna, antialiasing, glow, saturacion, FPS).
// SIN COMPILAR EN UNITY.
using System;
using UnityEngine;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    public sealed class SettingsWindow : MonoBehaviour
    {
        public static SettingsWindow Instance;
        public Func<string, Texture> BackLoader;
        const int SK = Skin.Option;

        public static SettingsWindow Ensure(GameObject host)
        {
            var s = host.GetComponent<SettingsWindow>(); if (s == null) s = host.AddComponent<SettingsWindow>(); return s;
        }

        // ---- geometria del original ----
        static readonly Rect ExitRect = new Rect(987, 0, 30, 30), FrameRect = new Rect(10, 50, 980, 540), ApplyRect = new Rect(500, 600, 180, 25), SaveRect = new Rect(695, 600, 180, 25), AdvRect = new Rect(300, 600, 180, 25);
        static readonly Rect[] NameRect = { new Rect(8, 19, 275, 44), new Rect(330, 21, 188, 32), new Rect(560, 21, 189, 33), new Rect(798, 19, 196, 45) };
        static readonly Rect[] SelRect = { new Rect(8, 17, 271, 49), new Rect(329, 19, 192, 44), new Rect(557, 20, 197, 44), new Rect(799, 3, 194, 61) };
        static readonly string[] TabStyle = { "graphics_tab", "gameUI_tab", "social_tab", "controls_tab" };
        static readonly string[] TabText = { "GRAPHICS & SOUND", "GAME UI", "SOCIAL", "CONTROLS" }; static readonly float[] TabDx = { 14f, 0f, 0f, 0f };   // el original lleva 3 espacios delante de GRAPHICS & SOUND (la fuente de mapa de bits los ignora)
        static readonly Rect LeftSky = new Rect(10, 20, 450, 45), RightSky = new Rect(490, 20, 450, 45);
        static readonly Rect[] Box =
        {
            new Rect(0,0,0,0), new Rect(20,160,239,36), new Rect(20,181,430,319), new Rect(10,50,240,460), new Rect(248,47,26,27), new Rect(220,64,240,446), new Rect(0,0,170,350),
            new Rect(500,150,209,36), new Rect(500,171,435,329), new Rect(490,50,240,460), new Rect(730,47,26,27), new Rect(700,64,240,446), new Rect(20,28,100,50), new Rect(500,28,100,50),
            new Rect(275,30,180,25), new Rect(755,30,180,25), new Rect(9,65,150,50), new Rect(0,100,150,50), new Rect(30,200,150,50), new Rect(30,235,150,50), new Rect(30,280,150,50),
            new Rect(30,310,150,50), new Rect(30,340,150,50), new Rect(30,380,150,50), new Rect(30,410,150,50), new Rect(30,440,150,50), new Rect(30,470,150,50), new Rect(510,80,150,50),
            new Rect(520,200,150,50), new Rect(520,275,150,50), new Rect(520,350,150,50), new Rect(520,425,150,50), new Rect(40,170,200,50), new Rect(520,160,200,50)
        };
        static readonly Rect[] LPro =
        {
            new Rect(200,185,40,40), new Rect(230,202,170,16), new Rect(230,197,170,16), new Rect(405,186,40,40), new Rect(200,225,40,40), new Rect(230,242,170,16), new Rect(230,237,170,16), new Rect(405,226,40,40)
        };
        static readonly Rect[] LRadio =
        {
            new Rect(200,277,22,21), new Rect(260,277,22,21), new Rect(200,310,22,21), new Rect(260,310,22,21), new Rect(200,345,22,21), new Rect(260,345,22,21),
            new Rect(200,377,22,21), new Rect(260,377,22,21), new Rect(200,408,22,21), new Rect(260,408,22,21)
        };
        static readonly Rect[] LPDown = { new Rect(200,82,223,21), new Rect(200,115,223,21), new Rect(200,437,223,21), new Rect(200,468,223,21) };
        static readonly Rect[] RPro =
        {
            new Rect(695,105,40,40), new Rect(723,122,170,16), new Rect(723,117,170,16), new Rect(900,105,40,40), new Rect(723,225,170,16), new Rect(695,210,40,40), new Rect(723,220,170,16),
            new Rect(900,210,40,40), new Rect(723,315,170,16), new Rect(695,300,40,40), new Rect(723,310,170,16), new Rect(900,300,40,40), new Rect(723,395,170,16), new Rect(695,380,40,40),
            new Rect(723,390,170,16), new Rect(900,380,40,40), new Rect(723,465,170,16), new Rect(695,450,40,40), new Rect(723,460,170,16), new Rect(900,450,40,40)
        };
        static readonly Rect[] RRadio =
        {
            new Rect(700,90,22,21), new Rect(760,90,22,21), new Rect(700,195,22,21), new Rect(760,195,22,21), new Rect(700,285,22,21), new Rect(760,285,22,21),
            new Rect(700,365,22,21), new Rect(760,365,22,21), new Rect(700,435,22,21), new Rect(760,435,22,21)
        };
        static readonly Rect[] DisplayBox =
        {
            new Rect(10,20,935,45), new Rect(10,50,650,230), new Rect(660,47,26,27), new Rect(560,64,385,216), new Rect(70,60,200,200), new Rect(70,60,239,36), new Rect(70,81,370,179), new Rect(690,30,250,25), new Rect(100,70,150,50)
        };
        static readonly string[] DisplayNames =
        {
            "MY NAME", "OTHER PLAYER NAMES", "GROUP MEMBER NAMES", "NPC NAMES", "MONSTER NAMES", "DAMAGE NUMBERS", "BALLOON CHAT", "ANIMATED NANOCOM", "NANOCOM CLOCK", "COMPUTRESS HINTS",
            "NPC MESSAGES IN CHAT WINDOW", "COMBAT IN CHAT WINDOW", "TIMESTAMP IN CHAT LOG"
        };

        // ---- estado ----
        bool open, handled, bApply = true, advanced, touchOptions, remapConflict; int tab, popup = -1; Texture back; bool backTried;
        static Texture2D tSky, tProBar, tThumb, tChk1, tChk2; static bool texTried;

        void Awake() { Instance = this; GameMenu.Implemented.Add(GameMenu.Win.Settings); GraphicsFx.Ensure(gameObject); GameOptions.Applied += OnApplied; }
        void OnDestroy() { CancelCapture(); GameOptions.Applied -= OnApplied; if (Instance == this) Instance = null; }
        void OnApplicationFocus(bool focus) { if (!focus) CancelCapture(); }
        void OnApplied() { var nh = FindObjectOfType<NanocomHud>(); if (nh != null) nh.ClockEnabled = GameOptions.Cur.NanocomClock; var ch = FindObjectOfType<ChatHud>(); if (ch != null) { ch.TimeStamp = GameOptions.Cur.ChatTimeStamp; ch.EventMessage1 = GameOptions.Cur.EventMsg1; ch.EventMessage2 = GameOptions.Cur.EventMsg2; } }

        void Update()
        {
            bool now = GameMenu.Current == GameMenu.Win.Settings;
            if (now && !open) OnOpen(); else if (!now && open) OnClosed();
            open = now;
            if (open && cfgSlot != -1 && !remapConflict && pressSlot < 0)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Time.unscaledTime > cfgDeadline) { ControlMap.CancelCaptureFrame = Time.frameCount; CancelCapture(); }
                else if (ControlMap.Remap(cfgKey, cfgSlot)) { CancelCapture(); Changed(); }
                else if (ControlMap.SameKey) remapConflict = true;
            }
            ControlMap.Capturing = open && (cfgSlot != -1 || remapConflict);
        }
        void CancelCapture()
        {
            cfgSlot = -1; remapConflict = false; pressSlot = -1; ControlMap.CancelRemap(); ControlMap.Capturing = false;
        }

        void OnOpen()
        {
            CancelCapture(); touchOptions = false; GameOptions.EnsureLoaded(); ControlMap.EnsureLoaded(); GameOptions.Push(); bApply = true; tab = 0; popup = -1; advanced = false; handled = false; cfgSlot = -1; keyScroll = 0f;
            if (!backTried) { backTried = true; try { if (BackLoader != null) back = BackLoader("TutorialAssets/panelback.png"); } catch (Exception) { } }
        }
        void OnClosed()
        {
            CancelCapture(); touchOptions = false;   // cerrada desde fuera (ESC): lo no aplicado se descarta, como el boton de cerrar
            if (!handled && !bApply) { GameOptions.Pop(); GameOptions.Apply(); }
        }
        void DoApply() { CancelCapture(); bApply = true; GameOptions.Save(); GameOptions.Apply(); GameOptions.Push(); }
        void DoSave() { DoApply(); handled = true; GameMenu.CloseWindow(); }
        void DoCancel()
        {
            if (bApply) { DoSave(); return; }
            handled = true; GameOptions.Pop(); GameOptions.Apply(); GameMenu.CloseWindow();
        }

        // ============================================================ dibujo ============================================================
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }

        void OnGUI()
        {
            if (!open) return; var e = Event.current; if (e == null || Screen.height <= 0) return;
            if (!texTried) { texTried = true; tSky = UiKit.Tex("equipbar"); tProBar = UiKit.Tex("setting_progressbar_img"); tThumb = UiKit.Tex("setting_progressbar"); tChk1 = UiKit.Tex("set_check"); tChk2 = UiKit.Tex("set_check2"); }
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(H / PlayerHud.UiRefH, W / 1020f);
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1500; GUI.color = Color.white;
            try
            {
                GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
                if (e.type == EventType.Repaint)
                {
                    GUI.color = new Color(0f, 0f, 0f, back != null ? 1f : 0.75f); GUI.DrawTexture(new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc), Texture2D.whiteTexture); GUI.color = Color.white;
                    if (back != null) GUI.DrawTexture(new Rect(-960f, -720f, 1920f, 1440f), back, ScaleMode.ScaleToFit);
                }
                GUI.BeginGroup(new Rect(-510f, -319f, 1020f, 638f));
                DrawWindow(e);
                GUI.EndGroup();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();   // la ventana es modal
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        bool En { get { return popup < 0 && !advanced && !touchOptions && !remapConflict; } }

        void DrawWindow(Event e)
        {
            bool en = En;
            for (int i = 0; i < 4; i++)
            {
                if (i != tab && Skin.Button(NameRect[i], "", Skin.Get(SK, TabStyle[i]), en)) { tab = i; popup = -1; CancelCapture(); }
                if (i != tab) TabLabel(i, NameRect[i], TabStyle[i]);
            }
            Skin.Box(FrameRect, Skin.Get(SK, "Box"));
            GUI.BeginGroup(new Rect(FrameRect.x + 10f, FrameRect.y + 10f, FrameRect.width - 20f, FrameRect.height - 20f));
            if (tab == 0) DrawGraphics(e); else if (tab == 1) DrawGameUi(e); else if (tab == 2) DrawSocial(e); else DrawControls(e);
            GUI.EndGroup();
            Skin.Box(SelRect[tab], Skin.Get(SK, TabStyle[tab] + "_sel")); TabLabel(tab, SelRect[tab], TabStyle[tab] + "_sel");
            if (Skin.Button(ExitRect, "", Skin.Get(Skin.Inven, "closebut"), en)) DoCancel();
            var key = Skin.Get(SK, "keybutton");
            if (tab == 0 && Skin.Button(AdvRect, "ADVANCED GRAPHICS", key, en)) advanced = true;
            if (Skin.Button(ApplyRect, "APPLY CHANGE", key, en && !bApply)) DoApply();
            if (Skin.Button(SaveRect, "SAVE AND EXIT", key, en)) DoSave();
            if (advanced) DrawAdvanced(e);
            if (touchOptions) DrawTouchOptions();
            if (remapConflict) DrawRemapConflict();
            // A click on a binding cell has already been consumed. Other mouse buttons
            // can now be captured without stealing cancel / clear / confirmation clicks.
            if (!remapConflict && cfgSlot >= 0 && cfgSlot != 2 && Input.touchCount == 0 && e.type == EventType.MouseDown)
            {
                if (ControlMap.Assign(cfgKey, cfgSlot, (KeyCode)((int)KeyCode.Mouse0 + e.button))) { CancelCapture(); Changed(); }
                else if (ControlMap.SameKey) remapConflict = true;
                e.Use();
            }
        }

        void TabLabel(int i, Rect r, string style)
        {
            var st = Skin.Get(SK, style); if (st == null || st.Font == null || Event.current.type != EventType.Repaint) return;
            st.Font.Draw(new Rect(r.x + TabDx[i], r.y, r.width, r.height), TabText[i], st.Align, false, st.cN, st.Padding);
        }

        // ---- pestana SOCIAL (cnOptionMode.OnSocial) ----
        static readonly Rect[] SocialBox =
        {
            new Rect(10,220,450,45), new Rect(10,250,240,260), new Rect(250,247,26,27), new Rect(100,264,360,246), new Rect(80,280,300,60), new Rect(80,357,300,60), new Rect(80,434,300,60),
            new Rect(90,300,150,40), new Rect(-10,377,250,40), new Rect(90,454,150,40), new Rect(20,228,200,50), new Rect(275,230,180,25)
        };
        static readonly Rect[] SocialRadio = { new Rect(260,300,22,21), new Rect(320,300,22,21), new Rect(260,377,22,21), new Rect(320,377,22,21), new Rect(260,454,22,21), new Rect(320,454,22,21) };
        static readonly Rect[] BlockBox = { new Rect(490,20,450,45), new Rect(490,50,450,460), new Rect(500,28,400,50), new Rect(510,40,368,50), new Rect(565,100,300,300), new Rect(570,415,290,25) };
        static readonly Rect[] LanguageBox = { new Rect(10,20,450,45), new Rect(10,50,450,150), new Rect(20,28,400,50), new Rect(30,140,368,50), new Rect(45,67,380,70), new Rect(28,92,200,35), new Rect(238,92,175,21) };
        void DrawSocial(Event e)
        {
            var o = GameOptions.Cur; bool en = En; var btn = Skin.Get(SK, "button");
            Sky(SocialBox[0]); Dark(SocialBox[3], "darkbox2"); Dark(SocialBox[1], "darkbox2"); Dark(SocialBox[2], "darkbox2_inter"); Dark(SocialBox[4], "darkbox"); Dark(SocialBox[5], "darkbox"); Dark(SocialBox[6], "darkbox");
            Lbl(SocialBox[7], "GROUP REQUESTS:", "smallfont"); Lbl(SocialBox[8], "BUDDY REQUESTS:", "smallfont"); Lbl(SocialBox[9], "TRADE REQUESTS:", "smallfont");
            Tog(ref o.AllowGroup, Radio(SocialRadio[0], SocialRadio[1], o.AllowGroup, en, "Yes", "No"), false, o);
            Tog(ref o.AllowBuddy, Radio(SocialRadio[2], SocialRadio[3], o.AllowBuddy, en, "Yes", "No"), false, o);
            Tog(ref o.AllowTrade, Radio(SocialRadio[4], SocialRadio[5], o.AllowTrade, en, "Yes", "No"), false, o);
            Lbl(SocialBox[10], "ALLOW REQUESTS", "titlefont");
            if (Skin.Button(SocialBox[11], "DEFAULT REQUESTS", btn, en)) { GameOptions.DefaultSocial(o); Changed(); }
            // idioma: el port solo trae el texto en ingles
            Sky(LanguageBox[0]); Dark(LanguageBox[1], "darkbox2"); Lbl(LanguageBox[2], "GAME LANGUAGE", "titlefont");
            Lbl(LanguageBox[3], "Game text translations are provided by community members and may not be 100% accurate. Note that not all elements are translated and you may see a mix of your language and English.", "whitelabel");
            Dark(LanguageBox[4], "darkbox"); Lbl(LanguageBox[5], "SELECTED LANGUAGE:", "smallfont"); if (Skin.Button(LanguageBox[6], LangName(LangSel), Skin.Get(SK, "pulldown"), en)) popup = 11;
            // jugadores bloqueados: sin lista de amigos en el cliente todavia
            Sky(BlockBox[0]); Dark(BlockBox[1], "darkbox2"); Lbl(BlockBox[2], "BLOCKED PLAYERS", "titlefont");
            Lbl(BlockBox[3], "Select a name below to remove it from your Blocked Players list. Reminder: Unblocked players can send you requests and chat messages.", "whitelabel");
            Dark(BlockBox[4], "textfield"); Skin.Button(BlockBox[5], "UNIGNORE", btn, false);
            if (popup == 11) DrawLangPopup(e); else if (popup >= 0) popup = -1;
        }

        // ---- idioma (cnOptionMode.availableLanguages: EN, PT, ES, IT, TR, Candy Pirate) ----
        static readonly int[] LangIds = { 0, 1, 2, 4, 8, 5 };
        static readonly string[] LangNames = { "English", "Portuguese (Brazil)", "Spanish (Latin America)", "Italian", "Turkish", "Candy Pirate" };
        static int LangSel { get { return Loc.Lang; } }
        static string LangName(int id) { for (int i = 0; i < LangIds.Length; i++) if (LangIds[i] == id) return LangNames[i]; return "Unknown"; }
        void DrawLangPopup(Event e)
        {
            var box = Skin.Get(SK, "downbox"); var item = Skin.Get(SK, "itemsel"); var pd = Skin.Get(SK, "pulldown"); var d = LanguageBox[6]; int n = LangIds.Length; var b = new Rect(d.x - 65f, d.y, 240f, n * 22f + 40f);
            if (e.type == EventType.MouseDown && !b.Contains(e.mousePosition)) { popup = -1; e.Use(); return; }
            if (e.type == EventType.Repaint && box != null) Skin.Bg(b, box.N, box);
            int pick = -1; for (int i = 0; i < n; i++) if (Skin.Button(new Rect(d.x - 65f, d.y + 22f * (i + 1) + 4f, 240f, 22f), LangNames[i], item)) pick = i;
            Skin.Box(d, pd, LangName(LangSel));
            if (pick < 0) return; Loc.SetLang(LangIds[pick]); popup = -1;
        }

        // ---- pestana CONTROLS (cnOptionMode.OnKeyMapping / OnKeyMapping2) ----
        static readonly Rect[] InputBox = { new Rect(30,75,200,50), new Rect(450,90,200,50), new Rect(250,73,22,21), new Rect(310,73,22,21), new Rect(680,90,170,16), new Rect(655,70,50,50), new Rect(680,85,170,16), new Rect(855,70,50,50) };
        static readonly Rect[] InputBox2 = { new Rect(30,100,200,50), new Rect(250,103,22,21), new Rect(310,103,22,21) };
        static readonly Rect PadTitleR = new Rect(20,198,210,21), PadBoxR = new Rect(250,200,223,21), KeyWarnR = new Rect(30,210,800,50), KeyListR = new Rect(25,230,900,260);
        static readonly Rect[] KeyTab = { new Rect(10,12,169,36), new Rect(10,340,169,36), new Rect(10,575,139,36), new Rect(10,865,139,36) };
        static readonly Rect[] KeyBody = { new Rect(10,35,900,280), new Rect(10,363,900,180), new Rect(10,595,870,250), new Rect(10,885,870,320) };
        static readonly float[] KeyTitleY = { 20f, 349f, 584f, 874f };
        static readonly string[] GroupTitle = { "MOVEMENT", "INTERFACE", "CAMERA", "COMBAT" };
        static readonly string[][] RowName =
        {
            new[] { "Forward", "Backward", "Turn Right", "Turn Left", "Sidestep Right", "Sidestep Left", "Jump", "Autorun" },
            new[] { "Nano Com", "My Stuff", "Journal", "E-Mail", "Map" },
            new[] { "Camera Up", "Camera Down", "Camera Left", "Camera Right", "Zoom In", "Zoom Out", "Toggle Aim" },
            new[] { "Attack/Use Target", "Switch Weapon", "Nano Power", "Nano Boost", "Weapon Boost", "Summon Nano #1", "Summon Nano #2", "Summon Nano #3", "Vehicle" }
        };
        static readonly ControlMap.K[][] RowKey =
        {
            new[] { ControlMap.K.Up, ControlMap.K.Down, ControlMap.K.RightTurn, ControlMap.K.LeftTurn, ControlMap.K.Right, ControlMap.K.Left, ControlMap.K.Jump, ControlMap.K.AutoRun },
            new[] { ControlMap.K.Menu, ControlMap.K.Inventory, ControlMap.K.Journal, ControlMap.K.QEmailBox, ControlMap.K.WorldMap },
            new[] { ControlMap.K.CameraUp, ControlMap.K.CameraDown, ControlMap.K.CameraLeft, ControlMap.K.CameraRight, ControlMap.K.ZoomIn, ControlMap.K.ZoomOut, ControlMap.K.CameraShoulderToggle },
            new[] { ControlMap.K.Fire1, ControlMap.K.WpnChange, ControlMap.K.Fire2, ControlMap.K.NanoCharge, ControlMap.K.WpnCharge, ControlMap.K.Nano1, ControlMap.K.Nano2, ControlMap.K.Nano3, ControlMap.K.VehicleToggle }
        };
        const float KeyContentH = 1215f, RowPitch = 30f; static readonly float[] ColX = { 180f, 400f, 620f };
        int cfgSlot = -1; ControlMap.K cfgKey; string cfgName = ""; float cfgDeadline, keyScroll; int pressSlot = -1; ControlMap.K pressKey; float pressAt; Vector2 pressPos;
        void DrawControls(Event e)
        {
            var o = GameOptions.Cur; bool en = En; var btn = Skin.Get(SK, "button");
            // ---- INPUT CONTROLS ----
            Sky(DisplayBox[0]); Dark(new Rect(560, 64, 385, 76), "darkbox2"); Dark(new Rect(10, 50, 650, 90), "darkbox2"); Dark(DisplayBox[2], "darkbox2_inter");
            var t = Box[12]; t.width = 200f; Lbl(t, "INPUT CONTROLS", "titlefont");
            if (Skin.Button(DisplayBox[7], "DEFAULT INPUT CONTROLS", btn, en)) { ControlMap.ResetInputControl(); o.VariableJump = true; Changed(); }
            Lbl(InputBox[0], "INVERT CAMERA Y-AXIS:", "smallfont"); Lbl(InputBox[1], "CAMERA SENSITIVITY:", "smallfont"); Lbl(InputBox2[0], "ENABLE VARIABLE JUMP:", "smallfont");
            int r = Radio(InputBox[2], InputBox[3], ControlMap.FlipCameraY, en, "Yes", "No"); if (r >= 0 && (r == 1) != ControlMap.FlipCameraY) { ControlMap.FlipCameraY = r == 1; Changed(); }
            Tog(ref o.VariableJump, Radio(InputBox2[1], InputBox2[2], o.VariableJump, en, "Yes", "No"), false, o);
            Bar(InputBox[4]); Lbl(InputBox[5], "Low"); Lbl(InputBox[7], "High");
            float cs = Slider(InputBox[6], ControlMap.CameraSensitivity, 1f, 10f, en, 31); if (Mathf.Abs(cs - ControlMap.CameraSensitivity) > 1e-4f) ControlMap.CameraSensitivity = cs;
            if (Skin.Button(new Rect(20, 135, 300, 24), "TOUCH CONTROLS...", btn, en)) { CancelCapture(); touchOptions = true; }
            Lbl(new Rect(330, 135, 600, 24), "Clear: right-click / hold touch 2s. Click again to cancel.", "smallfont");
            // ---- KEY MAPPING ----
            Sky(new Rect(10, 160, 935, 45)); Dark(new Rect(460, 204, 485, 306), "darkbox2"); Dark(new Rect(10, 190, 650, 320), "darkbox2"); Dark(new Rect(660, 187, 26, 27), "darkbox2_inter");
            Lbl(new Rect(20, 168, 200, 50), "KEY MAPPING", "titlefont");
            if (Skin.Button(new Rect(690, 170, 250, 25), "DEFAULT KEY MAPPING", btn, en)) { CancelCapture(); ControlMap.ResetMappings(); Changed(); }
            Dark(KeyListR, "textfield");
            DrawKeyList(e, en);
            Lbl(PadTitleR, "Selected Gamepad :", "bigfont14");
            if (Skin.Button(PadBoxR, ControlMap.PadName[ControlMap.JoyType], Skin.Get(SK, "pulldown"), en)) popup = 10;
            if (cfgSlot != -1)
            {
                string msg = "Awaiting Input: " + cfgName + " (click the cell again to cancel)";
                Skin.Label(KeyWarnR, msg, Skin.Get(SK, "warnlabel"), Color.yellow);
            }
            if (popup == 10) DrawPadPopup(e);
            else if (popup >= 0) popup = -1;
        }
        void DrawPadPopup(Event e)
        {
            var box = Skin.Get(SK, "downbox"); var item = Skin.Get(SK, "itemsel"); var pd = Skin.Get(SK, "pulldown"); int n = ControlMap.PadName.Length; var b = new Rect(PadBoxR.x, PadBoxR.y, 200f, 30f + n * 20f);
            if (e.type == EventType.MouseDown && !b.Contains(e.mousePosition) && !PadBoxR.Contains(e.mousePosition)) { popup = -1; e.Use(); return; }
            if (e.type == EventType.Repaint && box != null) Skin.Bg(b, box.N, box);
            int pick = -1; for (int i = 0; i < n; i++) if (Skin.Button(new Rect(PadBoxR.x, PadBoxR.y + 30f + i * 20f, 200f, 20f), ControlMap.PadName[i], item)) pick = i;
            Skin.Box(PadBoxR, pd, ControlMap.PadName[ControlMap.JoyType]);
            if (pick < 0) return; CancelCapture(); ControlMap.JoyType = pick; ControlMap.ResetPadMap(); popup = -1; Changed();
        }
        void KeyBtn(Rect r, string text, bool sel, bool en, ControlMap.K key, int slot, string name)
        {
            var e = Event.current; var st = Skin.Get(SK, "keybutton"); if (st == null) return;
            if (e.type == EventType.Repaint)
            {
                var tex = sel ? (st.ON != null ? st.ON : st.N) : st.N; Skin.Bg(r, tex, st);
                if (st.Font != null) st.Font.Draw(r, sel ? "Awaiting Input" : text, st.Align, false, sel ? st.cA : st.cN, st.Padding);
            }
            if (!en) return;
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
            {
                if (e.button == 1) { CancelCapture(); ControlMap.Clear(key, slot); Changed(); }
                else if (e.button == 0) { pressSlot = slot; pressKey = key; pressAt = Time.unscaledTime; pressPos = e.mousePosition; }
                e.Use();
            }
            if (pressSlot != slot || pressKey != key) return;
            if ((e.mousePosition - pressPos).sqrMagnitude > 144f) { pressSlot = -1; return; }
            if (Input.touchCount > 0 && Time.unscaledTime - pressAt >= 2f)
            {
                CancelCapture(); ControlMap.Clear(key, slot); Changed(); return;
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                pressSlot = -1;
                if (r.Contains(e.mousePosition))
                {
                    if (sel) CancelCapture();
                    else { CancelCapture(); cfgSlot = slot; cfgKey = key; cfgName = name; cfgDeadline = Time.unscaledTime + 8f; ControlMap.BeginRemap(); ControlMap.Capturing = true; }
                }
                e.Use();
            }
        }
        void DrawRemapConflict()
        {
            UiKit.Fill(new Rect(0, 0, 1020, 638), new Color(0, 0, 0, .9f));
            var text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(110, 180, 800, 130), "This control is already assigned to: " + ControlMap.ConflictText + "\nReplace will clear those bindings and assign it to " + cfgName + ".", text);
            var btn = Skin.Get(SK, "keybutton");
            if (Skin.Button(new Rect(270, 345, 200, 38), "REPLACE", btn)) { ControlMap.ReplacePending(cfgKey, cfgSlot); CancelCapture(); Changed(); }
            if (Skin.Button(new Rect(540, 345, 200, 38), "CANCEL", btn)) CancelCapture();
        }
        void DrawTouchOptions()
        {
            UiKit.Fill(new Rect(0, 0, 1020, 638), new Color(0, 0, 0, .96f));
            var label = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            var toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 17 };
            GUI.Label(new Rect(90, 35, 840, 45), "TOUCH CONTROLS", label);
            bool hide = GUI.Toggle(new Rect(95, 100, 800, 36), TouchLayout.HideWithPad, "Hide when a gamepad is connected (restore on disconnect)", toggle);
            if (hide != TouchLayout.HideWithPad) { TouchLayout.HideWithPad = hide; Changed(); }
            GUI.Label(new Rect(95, 140, 400, 35), "Button size", label);
            float size = GUI.HorizontalSlider(new Rect(510, 155, 360, 30), TouchLayout.Scale, .65f, 1.3f);
            if (Mathf.Abs(size - TouchLayout.Scale) > .001f) { TouchLayout.Scale = size; Changed(); }
            for (int i = 0; i < TouchLayout.Actions.Length; i++)
            {
                bool value = GUI.Toggle(new Rect(110 + i % 3 * 295, 215 + i / 3 * 52, 280, 40), TouchLayout.Enabled(i), TouchLayout.Labels[i], toggle);
                if (value != TouchLayout.Enabled(i)) { TouchLayout.Enable(i, value); Changed(); }
            }
            var btn = Skin.Get(SK, "keybutton");
            if (Skin.Button(new Rect(260, 535, 220, 38), "RESET", btn)) { TouchLayout.Reset(); Changed(); }
            if (Skin.Button(new Rect(540, 535, 220, 38), "DONE", btn)) touchOptions = false;
            GUI.Label(new Rect(95, 585, 830, 35), "Use APPLY CHANGE or SAVE AND EXIT to save.", label);
        }
        void DrawKeyList(Event e, bool en)
        {
            var view = new Rect(KeyListR.x + 5f, KeyListR.y + 5f, KeyListR.width - 10f, KeyListR.height - 10f); float max = Mathf.Max(0f, KeyContentH - view.height);
            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition) && en) { keyScroll = Mathf.Clamp(keyScroll + e.delta.y * 20f, 0f, max); e.Use(); }
            // barra de desplazamiento (arrastrable)
            var track = new Rect(view.xMax - 12f, view.y, 12f, view.height); float th = Mathf.Max(24f, view.height * view.height / KeyContentH); float ty = track.y + (max > 0f ? keyScroll / max : 0f) * (track.height - th); var thumb = new Rect(track.x + 2f, ty, 8f, th);
            int sid = GUIUtility.GetControlID(7777, FocusType.Passive);
            if (en && e.type == EventType.MouseDown && e.button == 0 && track.Contains(e.mousePosition)) { GUIUtility.hotControl = sid; e.Use(); }
            if (GUIUtility.hotControl == sid && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)) { float f = Mathf.Clamp01((e.mousePosition.y - track.y - th * 0.5f) / Mathf.Max(1f, track.height - th)); keyScroll = f * max; e.Use(); }
            else if (GUIUtility.hotControl == sid && e.type == EventType.MouseUp) { GUIUtility.hotControl = 0; e.Use(); }
            GUI.BeginGroup(new Rect(view.x, view.y, view.width - 14f, view.height));
            float sy = -keyScroll; var tint = new Color(0.2f, 0.6f, 0.8f);
            for (int g = 0; g < 4; g++)
            {
                var tab = KeyTab[g]; tab.y += sy; var body = KeyBody[g]; body.y += sy;
                if (tab.yMax < 0f && body.yMax < 0f || tab.y > view.height && body.y > view.height) continue;
                Dark(tab, "darkbox_inter"); Dark(body, "darkboxside");
                var tr = new Rect(20f, KeyTitleY[g] + sy, 200f, 50f); Skin.Label(tr, GroupTitle[g], Skin.Get(SK, "titlefont"), tint);
                float y0 = KeyTab[g].y + sy;
                for (int c = 0; c < 3; c++) Lbl(new Rect(ColX[c], y0 + 28f, 180f, 20f), "Setting " + (c + 1), "label");
                for (int i = 0; i < RowName[g].Length; i++)
                {
                    float ry = y0 + 55f + i * RowPitch; if (ry + 24f < 0f || ry > view.height) continue; var k = RowKey[g][i]; string nm = RowName[g][i];
                    Lbl(new Rect(20f, ry, 150f, 24f), nm, "smallfont");
                    for (int c = 0; c < 3; c++)
                    {
                        int slot = c; var key = k; string name = nm; bool sel = cfgSlot == slot && cfgKey == key;
                        KeyBtn(new Rect(ColX[c], ry, 180f, 24f), ControlMap.Name(k, c), sel, en, key, slot, name);
                    }
                }
            }
            GUI.EndGroup();
            if (e.type == EventType.MouseUp && e.button == 0) pressSlot = -1;
            if (max > 0f && e.type == EventType.Repaint) { UiKit.Fill(track, new Color(0f, 0f, 0f, 0.35f)); UiKit.Fill(thumb, new Color(0.2f, 0.6f, 0.8f, 0.9f)); }
        }

        // ---- controles comunes ----
        void Changed() { bApply = false; }
        void Tog(ref bool f, int n, bool custom, OptionData o) { if (n < 0) return; bool v = n > 0; if (f != v) { f = v; if (custom) o.Detail = DetailLevel.Custom; Changed(); } }

        /// RadioButton del original: dos casillas On/Off; la elegida muestra set_check2 y etiqueta blanca. Devuelve 1/0 si se pulso, -1 si no.
        int Radio(Rect r1, Rect r2, bool flag, bool en, string s1 = "On", string s2 = "Off")
        {
            var e = Event.current; int res = -1; var lab = Skin.Get(SK, "label"); var wl = Skin.Get(SK, "whitelabel");
            if (en && e.type == EventType.MouseDown && e.button == 0)
            {
                if (r1.Contains(e.mousePosition)) res = 1; else if (r2.Contains(e.mousePosition)) res = 0;
                if (res >= 0) { e.Use(); Sfx.Button(); }
            }
            if (e.type == EventType.Repaint)
            {
                var p1 = new Rect(r1.x + 30f, r1.y, r1.width, r1.height); var p2 = new Rect(r2.x + 30f, r2.y, r2.width, r2.height);
                Skin.Label(p1, s1, flag ? wl : lab); Skin.Label(p2, s2, flag ? lab : wl);
                if (tChk1 != null && tChk2 != null) { GUI.DrawTexture(r1, flag ? tChk2 : tChk1); GUI.DrawTexture(r2, flag ? tChk1 : tChk2); }
            }
            return res;
        }

        /// GUI.HorizontalSlider con la barra setting_progressbar_img; el tirador (13x27) cuelga del y del rect del slider.
        float Slider(Rect r, float v, float min, float max, bool en, int id)
        {
            var e = Event.current; const float tw = 13f; int cid = GUIUtility.GetControlID(id + 7000, FocusType.Passive);
            var hit = new Rect(r.x - 4f, r.y - 4f, r.width + 8f, 27f);
            if (en && e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition)) { GUIUtility.hotControl = cid; e.Use(); }
            if (GUIUtility.hotControl == cid && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                float t = Mathf.Clamp01((e.mousePosition.x - r.x - tw * 0.5f) / (r.width - tw)); float nv = Mathf.Lerp(min, max, t);
                if (Mathf.Abs(nv - v) > 1e-4f) { v = nv; Changed(); }
                e.Use();
            }
            else if (GUIUtility.hotControl == cid && e.type == EventType.MouseUp) { GUIUtility.hotControl = 0; e.Use(); }
            if (e.type == EventType.Repaint && tThumb != null) { float t = Mathf.InverseLerp(min, max, v); GUI.DrawTexture(new Rect(r.x + t * (r.width - tw), r.y, tw, 27f), tThumb); }
            return v;
        }
        void Bar(Rect r) { if (tProBar != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(r, tProBar); }
        void Lbl(Rect r, string t, string style = "label") { Skin.Label(r, t, Skin.Get(SK, style)); }
        void Dark(Rect r, string style) { var s = Skin.Get(SK, style); if (s != null && Event.current.type == EventType.Repaint) Skin.Bg(r, s.N, s); }
        void Sky(Rect r) { if (tSky != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(r, tSky); }
        string P(string name) { return name; }

        // ---- pestana GRAPHICS & SOUND ----
        static readonly string[] LevelItems = { "BEST QUALITY", "GOOD QUALITY", "BALANCED", "GOOD PERFORMANCE", "BEST PERFORMANCE" };
        static readonly float[] ResScales = { 1f, 0.85f, 0.7f, 0.6f, 0.5f };
        string ResText(float s) { return Mathf.RoundToInt(Screen.width * s) + "X" + Mathf.RoundToInt(Screen.height * s); }

        void DrawGraphics(Event e)
        {
            var o = GameOptions.Cur; bool en = En; var pd = Skin.Get(SK, "pulldown");
            Sky(LeftSky); Dark(Box[5], "darkbox2"); Dark(Box[3], "darkbox2"); Dark(Box[4], "darkbox2_inter"); Dark(Box[1], "darkbox_inter"); Dark(Box[2], "darkboxside");
            Lbl(Box[12], "GRAPHICS", "titlefont"); Lbl(Box[16], "SCREEN MODE:", "bigfont14"); Lbl(Box[17], "DETAIL LEVEL:", "bigfont14");
            Lbl(Box[18], "VISIBILITY:", "smallfont"); Lbl(Box[19], "PARTICLE EFFECT DENSITY:", "smallfont");
            Lbl(LPro[0], "Near"); Bar(LPro[1]);
            float vis = Slider(LPro[2], o.Visibility, 0f, 1f, en, 1); if (Mathf.Abs(vis - o.Visibility) > 1e-4f) { o.Visibility = vis; o.Detail = DetailLevel.Custom; }
            Lbl(LPro[3], "Far"); Lbl(LPro[4], "Low"); Bar(LPro[5]);
            int pe = Mathf.RoundToInt(Slider(LPro[6], o.ParticleEffect, 0f, 3f, en, 2)); if (pe != o.ParticleEffect) { o.ParticleEffect = pe; o.Detail = DetailLevel.Custom; }
            Lbl(LPro[7], "High");
            Lbl(Box[20], "TOON OUTLINE:", "smallfont"); Lbl(Box[21], "GLOW:", "smallfont"); Lbl(Box[22], "ANISOTROPIC FILTERING:", "smallfont"); Lbl(Box[23], "SOFT VEGETATION:", "smallfont"); Lbl(Box[24], "OBJECT FADING:", "smallfont");
            Tog(ref o.Toon, Radio(LRadio[0], LRadio[1], o.Toon, en), true, o); Tog(ref o.Glow, Radio(LRadio[2], LRadio[3], o.Glow, en), true, o);
            Tog(ref o.Aniso, Radio(LRadio[4], LRadio[5], o.Aniso, en), true, o); Tog(ref o.SoftVegetation, Radio(LRadio[6], LRadio[7], o.SoftVegetation, en), true, o);
            Tog(ref o.Fade, Radio(LRadio[8], LRadio[9], o.Fade, en), true, o);
            Lbl(Box[25], "SHADOWS:", "smallfont"); Lbl(Box[26], "TEXTURE QUALITY:", "smallfont"); Lbl(Box[32], "CUSTOM DETAIL LEVEL", "smallcyan");
            if (Skin.Button(Box[14], "DEFAULT GRAPHICS", Skin.Get(SK, "button"), en)) { GameOptions.DefaultGraphics(o); Changed(); }
            if (Skin.Button(LPDown[0], ResText(o.RenderScale), pd, en)) popup = 0;
            if (Skin.Button(LPDown[1], o.Detail.ToString(), pd, en)) popup = 1;
            if (Skin.Button(LPDown[2], o.Shadow.ToString(), pd, en)) popup = 2;
            if (Skin.Button(LPDown[3], o.Texture.ToString(), pd, en)) popup = 3;
            // ---- sonido ----
            Sky(RightSky); Dark(Box[11], "darkbox2"); Dark(Box[9], "darkbox2"); Dark(Box[10], "darkbox2_inter"); Dark(Box[7], "darkbox_inter"); Dark(Box[8], "darkboxside");
            Lbl(Box[13], "SOUND", "titlefont"); Lbl(Box[27], "MASTER VOLUME:", "bigfont14"); Lbl(Box[28], "SOUND EFFECT VOLUME:", "bigfont14"); Lbl(Box[29], "VOICE VOLUME:", "bigfont14");
            Lbl(Box[30], "AMBIENT VOLUME:", "bigfont14"); Lbl(Box[31], "MUSIC VOLUME:", "bigfont14"); Lbl(Box[33], "CUSTOM VOLUME", "smallcyan");
            if (Skin.Button(Box[15], "DEFAULT SOUND", Skin.Get(SK, "button"), en)) { GameOptions.DefaultSound(o); Changed(); }
            Tog(ref o.MasterOn, Radio(RRadio[0], RRadio[1], o.MasterOn, en), false, o); Tog(ref o.SfxOn, Radio(RRadio[2], RRadio[3], o.SfxOn, en), false, o);
            Tog(ref o.VoiceOn, Radio(RRadio[4], RRadio[5], o.VoiceOn, en), false, o); Tog(ref o.AmbientOn, Radio(RRadio[6], RRadio[7], o.AmbientOn, en), false, o);
            Tog(ref o.MusicOn, Radio(RRadio[8], RRadio[9], o.MusicOn, en), false, o);
            for (int k = 0; k < 5; k++)
            {   // cada volumen: etiqueta Low, barra, slider, etiqueta High (los indices del original no siguen un orden estricto)
                int[] lo = { 0, 5, 9, 13, 17 }, hi = { 3, 7, 11, 15, 19 }, bar = { 1, 4, 8, 12, 16 }, sl = { 2, 6, 10, 14, 18 };
                Lbl(RPro[lo[k]], "Low"); Bar(RPro[bar[k]]); Lbl(RPro[hi[k]], "High");
                float cur = k == 0 ? o.Master : k == 1 ? o.Sfx : k == 2 ? o.Voice : k == 3 ? o.Ambient : o.Music;
                float nv = Slider(RPro[sl[k]], cur, 0f, 1f, en, 10 + k);
                if (nv != cur) { if (k == 0) o.Master = nv; else if (k == 1) o.Sfx = nv; else if (k == 2) o.Voice = nv; else if (k == 3) o.Ambient = nv; else o.Music = nv; }
            }
            if (popup >= 0) DrawPopup(e, o);
        }

        void DrawPopup(Event e, OptionData o)
        {
            var box = Skin.Get(SK, "downbox"); var item = Skin.Get(SK, "itemsel"); var pd = Skin.Get(SK, "pulldown"); Rect b, d = LPDown[popup]; string cur; int n;
            if (popup == 0) { n = ResScales.Length; b = new Rect(d.x, d.y, 200f, 30f + n * 20f + 0f); cur = ResText(o.RenderScale); }
            else if (popup == 1) { b = new Rect(d.x, d.y, 200f, 150f); cur = o.Detail.ToString(); }
            else if (popup == 2) { b = new Rect(d.x, d.y, 200f, 80f); cur = o.Shadow.ToString(); }
            else { b = new Rect(d.x, d.y - 78f, 200f, 100f); cur = o.Texture.ToString(); }
            if (e.type == EventType.MouseDown && !b.Contains(e.mousePosition) && !d.Contains(e.mousePosition)) { popup = -1; e.Use(); return; }
            if (e.type == EventType.Repaint && box != null) Skin.Bg(b, box.N, box);
            int pick = -1;
            if (popup == 0) { for (int i = 0; i < ResScales.Length; i++) if (Skin.Button(new Rect(d.x, d.y + 20f + i * 20f, 200f, 20f), ResText(ResScales[i]), item)) pick = i; }
            else if (popup == 1) { for (int i = 0; i < 5; i++) if (Skin.Button(new Rect(d.x, d.y + 30f + i * 20f, 200f, 20f), LevelItems[i], item)) pick = i; }
            else if (popup == 2)
            {
                if (Skin.Button(new Rect(d.x, d.y + 30f, 200f, 20f), "All Characters", item)) pick = 1;
                if (Skin.Button(new Rect(d.x, d.y + 50f, 200f, 20f), "Own Player Only", item)) pick = 0;
            }
            else
            {
                if (Skin.Button(new Rect(d.x, d.y - 70f, 200f, 20f), "High", item)) pick = 0;
                if (Skin.Button(new Rect(d.x, d.y - 50f, 200f, 20f), "Medium", item)) pick = 1;
                if (Skin.Button(new Rect(d.x, d.y - 30f, 200f, 20f), "Low", item)) pick = 2;
            }
            Skin.Box(d, pd, cur);
            if (pick < 0) return;
            if (popup == 0) o.RenderScale = ResScales[pick];
            else if (popup == 1) GameOptions.SetDetailLevel(o, (DetailLevel)pick);
            else if (popup == 2) { var v = (ShadowDetail)pick; if (v != o.Shadow) o.Detail = DetailLevel.Custom; o.Shadow = v; }
            else { var v = (TextureQuality)pick; if (v != o.Texture) o.Detail = DetailLevel.Custom; o.Texture = v; }
            popup = -1; Changed();
        }

        // ---- pestana GAME UI (elementos flotantes) ----
        void DrawGameUi(Event e)
        {
            var o = GameOptions.Cur; bool en = En;
            Sky(DisplayBox[0]); Dark(DisplayBox[3], "darkbox2"); Dark(DisplayBox[1], "darkbox2"); Dark(DisplayBox[2], "darkbox2_inter"); Dark(DisplayBox[5], "darkbox_inter"); Dark(DisplayBox[6], "darkboxside");
            var t = Box[12]; t.width = 200f; Lbl(t, "DISPLAY ELEMENTS", "titlefont");
            var r0 = new Rect(310, 95, 22, 21); var r1 = new Rect(370, 95, 22, 21);
            Rect[] ra = new Rect[26], rb = new Rect[26]; Rect[] nm = new Rect[13];
            for (int k = 0; k < 6; k++) { ra[k] = new Rect(r0.x, r0.y + (r0.height + 5f) * k, 22, 21); rb[k] = new Rect(r1.x, r1.y + (r1.height + 5f) * k, 22, 21); nm[k] = new Rect(0, 100 + 25 * k, 300, 5); }
            for (int k = 0; k < 6; k++) { ra[6 + k] = new Rect(r0.x + 450f, r0.y + (r0.height + 5f) * k - 10f, 22, 21); rb[6 + k] = new Rect(r1.x + 450f, r1.y + (r1.height + 5f) * k - 10f, 22, 21); nm[6 + k] = new Rect(450, 100 + 25 * k - 10, 300, 5); }
            ra[12] = new Rect(ra[11].x, ra[11].y + 26f, 22, 21); rb[12] = new Rect(rb[11].x, rb[11].y + 26f, 22, 21); nm[12] = new Rect(450, nm[11].y + 25f, 300, 5);
            Tog(ref o.MyName, Radio(ra[0], rb[0], o.MyName, en), false, o); Tog(ref o.OtherName, Radio(ra[1], rb[1], o.OtherName, en), false, o); Tog(ref o.GroupName, Radio(ra[2], rb[2], o.GroupName, en), false, o);
            Tog(ref o.NpcName, Radio(ra[3], rb[3], o.NpcName, en), false, o); Tog(ref o.MonsterName, Radio(ra[4], rb[4], o.MonsterName, en), false, o); Tog(ref o.FloatNumber, Radio(ra[5], rb[5], o.FloatNumber, en), false, o);
            Tog(ref o.BalloonChat, Radio(ra[6], rb[6], o.BalloonChat, en), false, o); Tog(ref o.AnimatedNanos, Radio(ra[7], rb[7], o.AnimatedNanos, en), false, o); Tog(ref o.NanocomClock, Radio(ra[8], rb[8], o.NanocomClock, en), false, o);
            Tog(ref o.GameHint, Radio(ra[9], rb[9], o.GameHint, en), false, o); Tog(ref o.EventMsg1, Radio(ra[10], rb[10], o.EventMsg1, en), false, o); Tog(ref o.EventMsg2, Radio(ra[11], rb[11], o.EventMsg2, en), false, o);
            Tog(ref o.ChatTimeStamp, Radio(ra[12], rb[12], o.ChatTimeStamp, en), false, o);
            for (int k = 0; k < 13; k++) Lbl(nm[k], DisplayNames[k] + ":", "smallfont");
            if (Skin.Button(DisplayBox[7], "DEFAULT DISPLAY ELEMENTS", Skin.Get(SK, "button"), en)) { GameOptions.DefaultDisplay(o); Changed(); }
            Lbl(DisplayBox[8], "FLOATING DISPLAY", "smallcyan");
            DrawChatColors(e, o, en);
            if (popup >= 0) popup = -1;
        }

        // ---- CHAT TEXT COLORS + UI SCALING (cnOptionMode.OnDisplay, 2a mitad) ----
        static readonly Rect[] ChatBox =
        {
            new Rect(10,300,690,45), new Rect(10,330,405,180), new Rect(414,327,26,27), new Rect(315,344,385,166), new Rect(20,308,200,50), new Rect(445,310,250,25),
            new Rect(35,360,194,120), new Rect(255,360,194,120), new Rect(475,360,194,120), new Rect(45,365,150,50), new Rect(265,365,150,50), new Rect(485,365,150,50)
        };
        static readonly Rect ColorBoxR = new Rect(45, 390, 20, 20);
        static Texture2D tCBox, tCSel; static bool cTried;
        void DrawChatColors(Event e, OptionData o, bool en)
        {
            if (!cTried) { cTried = true; tCBox = UiKit.Tex("color_box"); tCSel = UiKit.Tex("color_box_selected"); }
            Sky(ChatBox[0]); Dark(ChatBox[3], "darkbox2"); Dark(ChatBox[1], "darkbox2"); Dark(ChatBox[2], "darkbox2_inter");
            var tt = ChatBox[4]; Lbl(tt, "CHAT TEXT COLORS", "titlefont");
            if (Skin.Button(ChatBox[5], "DEFAULT TEXT COLORS", Skin.Get(SK, "button"), en)) { GameOptions.DefaultChatColors(o); Changed(); }
            for (int c = 0; c < 3; c++) Dark(ChatBox[6 + c], "darkbox_inter");
            Lbl(ChatBox[9], "GENERAL CHAT", "smallcyan"); Lbl(ChatBox[10], "GROUP CHAT", "smallcyan"); Lbl(ChatBox[11], "BUDDY CHAT", "smallcyan");
            // columnas del original: 0 = general (iGeneral), 1 = grupo (iGroup), 2 = buddy (iBuddy); 3 filas x 6 colores
            int[] sel = { o.ChatGeneral, o.ChatGroup, o.ChatBuddy }; var wl = Skin.Get(SK, "whitelabel"); var oc = GUI.color;
            for (int col = 0; col < 3; col++)
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 6; j++)
                    {
                        var r = ColorBoxR; r.x += 220f * col + (r.width + 10f) * j; r.y += (r.height + 10f) * i; int idx = i * 6 + j;
                        if (e.type == EventType.Repaint && tCBox != null) { GUI.color = Color.white; GUI.DrawTexture(r, tCBox); }
                        var hit = new Rect(r.x + 3f, r.y - 3f, r.width, r.height);
                        if (e.type == EventType.Repaint) { GUI.color = Color.white; Skin.Label(hit, "Aa", wl, GameOptions.Palette[idx]); }
                        if (en && e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition))
                        {
                            if (col == 0) o.ChatGeneral = idx; else if (col == 1) o.ChatGroup = idx; else o.ChatBuddy = idx; Sfx.Button(); Changed(); e.Use();
                        }
                    }
            GUI.color = oc;
            if (e.type == EventType.Repaint && tCSel != null)
                for (int col = 0; col < 3; col++)
                {
                    int k = sel[col]; var r = ColorBoxR; r.x += 220f * col + (r.width + 10f) * (k % 6); r.y += (r.height + 10f) * (k / 6);
                    GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, 24f, 24f), tCSel);
                }
            // UI SCALING
            var hdr = ChatBox[0]; hdr.x = ChatBox[0].x + ChatBox[0].width + 20f; hdr.width = 225f; Sky(hdr);
            var title = hdr; title.x += 10f; title.y += 8f; Lbl(title, "UI SCALING", "titlefont");
            var content = hdr; content.y = hdr.y + hdr.height - 15f; content.height = 180f; Dark(content, "darkbox2");
            var t1 = content; t1.x += 10f; t1.y += 10f; t1.width -= 20f; t1.height = 45f; Lbl(t1, "Adjust the size of the user interface.\nLowest setting = no scaling\nHighest setting = scale to fit", "whitelabel");
            var t2 = content; t2.x += 10f; t2.y = t1.y + t1.height + 10f; t2.width -= 20f; t2.height = 20f; Lbl(t2, "UI SCALE FACTOR", "smallfont");
            var bar = new Rect(content.x + 10f, t2.y + t2.height + 10f, content.width - 20f, 20f); Bar(bar);
            var sl = bar; sl.y -= 4f; float nv = Slider(sl, o.UiFactor, 0f, 1f, en, 30); if (Mathf.Abs(nv - o.UiFactor) > 1e-4f) { o.UiFactor = nv; Changed(); }
        }

        // ---- panel ADVANCED GRAPHICS (extra del port movil) ----
        static readonly string[] AaNames = { "OFF", "FXAA", "MSAA 2X", "MSAA 4X" }; static readonly string[] FpsNames = { "30", "60", "MAX" };
        void DrawAdvanced(Event e)
        {
            var o = GameOptions.Cur; var panel = new Rect(300f, 125f, 420f, 380f);
            if (e.type == EventType.MouseDown && !panel.Contains(e.mousePosition)) e.Use();
            if (e.type == EventType.Repaint)
            {
                UiKit.Fill(new Rect(0, 0, 1020, 638), new Color(0, 0, 0, 0.55f)); Dark(panel, "darkbox2"); Dark(new Rect(panel.x, panel.y, 420f, 36f), "darkbox_inter");
            }
            Lbl(new Rect(panel.x + 20f, panel.y + 6f, 380f, 30f), "ADVANCED GRAPHICS", "titlefont");
            float x = panel.x + 24f, y = panel.y + 52f;
            // resolucion interna
            Lbl(new Rect(x, y, 300f, 18f), "RENDER RESOLUTION", "smallcyan"); Bar(new Rect(x, y + 22f, 170f, 16f));
            float sc = Slider(new Rect(x, y + 17f, 170f, 16f), o.RenderScale, 0.5f, 1f, true, 20); if (Mathf.Abs(sc - o.RenderScale) > 1e-4f) { o.RenderScale = Mathf.Round(sc * 20f) / 20f; o.Detail = DetailLevel.Custom; }
            Lbl(new Rect(x + 190f, y + 16f, 200f, 24f), Mathf.RoundToInt(o.RenderScale * 100f) + "%  (" + ResText(o.RenderScale) + ")", "whitelabel");
            // glow
            y += 56f; Lbl(new Rect(x, y, 300f, 18f), "GLOW STRENGTH", "smallcyan"); Bar(new Rect(x, y + 22f, 170f, 16f));
            float gs = Slider(new Rect(x, y + 17f, 170f, 16f), o.GlowStrength, 0f, 1f, true, 21); if (Mathf.Abs(gs - o.GlowStrength) > 1e-4f) { o.GlowStrength = gs; o.Detail = DetailLevel.Custom; }
            Lbl(new Rect(x + 190f, y + 16f, 200f, 24f), Mathf.RoundToInt(o.GlowStrength * 100f) + "%", "whitelabel");
            // saturacion
            y += 56f; Lbl(new Rect(x, y, 300f, 18f), "COLOR SATURATION", "smallcyan"); Bar(new Rect(x, y + 22f, 170f, 16f));
            float sa = Slider(new Rect(x, y + 17f, 170f, 16f), o.Saturation, 0f, 1f, true, 22); if (Mathf.Abs(sa - o.Saturation) > 1e-4f) { o.Saturation = sa; o.Detail = DetailLevel.Custom; }
            Lbl(new Rect(x + 190f, y + 16f, 200f, 24f), (o.Saturation - 0.5f >= 0 ? "+" : "") + Mathf.RoundToInt((o.Saturation - 0.5f) * 200f) + "%", "whitelabel");
            // antialiasing
            y += 56f; Lbl(new Rect(x, y, 300f, 18f), "ANTI-ALIASING", "smallcyan");
            for (int i = 0; i < 4; i++) { int r = Choice(new Rect(x + i * 94f, y + 20f, 22f, 21f), AaNames[i], o.AA == i); if (r == 1 && o.AA != i) { o.AA = i; o.Detail = DetailLevel.Custom; Changed(); } }
            // fps
            y += 60f; Lbl(new Rect(x, y, 300f, 18f), "FRAME RATE", "smallcyan");
            for (int i = 0; i < 3; i++) { int r = Choice(new Rect(x + i * 94f, y + 20f, 22f, 21f), FpsNames[i], o.Fps == i); if (r == 1 && o.Fps != i) { o.Fps = i; Changed(); } }
            if (Skin.Button(new Rect(panel.x + 110f, panel.yMax - 40f, 200f, 25f), "CLOSE", Skin.Get(SK, "keybutton"))) advanced = false;
        }
        int Choice(Rect r, string text, bool on)
        {
            var e = Event.current; int res = -1; var lab = Skin.Get(SK, "label"); var wl = Skin.Get(SK, "whitelabel");
            var hit = new Rect(r.x, r.y, 90f, r.height);
            if (e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition)) { res = 1; e.Use(); Sfx.Button(); }
            if (e.type == EventType.Repaint) { Skin.Label(new Rect(r.x + 28f, r.y, 60f, r.height), text, on ? wl : lab); if (tChk1 != null && tChk2 != null) GUI.DrawTexture(r, on ? tChk2 : tChk1); }
            return res;
        }
    }
}
