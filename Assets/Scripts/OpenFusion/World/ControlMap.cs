// Equivalente de ConfigurableInput (Assembly-CSharp-first_pass): 40 acciones con tres asignaciones cada una (ajuste 1 = teclado, ajuste 2 = alternativa, ajuste 3 = mando),
// persistidas con las MISMAS claves de PlayerPrefs del original ("<Key>Mapping", "<Key>AxisMapping", "<Key>AltMapping", "<Key>AltAxisMapping", "<Key>PadMapping", "<Key>PadAxisMapping", "InputVer"=2, "PadType",
// "Camera Sensitivity", "CameraYflip"). Los ejes analogicos usan los nombres del InputManager del original ("Joy X", "Joy Y", "Joy 3".."Joy 10"), que se anaden a ProjectSettings/InputManager.asset.
// Perfiles de mando: 0 XBox 360, 1 RumblePad 2, 2 PS2 (tablas de Windows del original) y 3 = "Android / Handheld" (agregado: botones genericos de Android, A=0 B=1 X=2 Y=3 L1=4 R1=5 Select=6 Start=7 L3=8 R3=9, stick derecho = Joy 3/Joy 4).
// Keyboard, controller and independent touch action state.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    public static class ControlMap
    {
        public enum K
        {
            Up, Down, Left, Right, Esc, Jump, Fire1, Fire2, Nano1, Nano2, Nano3, WpnChange, WpnCharge, NanoCharge, Inventory, NanoBook, Journal, QEmailBox, Contacts, Help,
            AutoRun, Menu, WorldMap, Option, LeftTurn, RightTurn, FreeCamera, SendChat, ZoomIn, ZoomOut, CameraUp, CameraDown, CameraLeft, CameraRight, CombatMode, CameraShoulderToggle,
            PerformanceMetrics, VehicleToggle, NanocomClock, None
        }
        public sealed class Bind { public KeyCode key; public int axis = -1; }

        public static readonly string[] PadName = { "XBox 360", "RumblePad 2", "PS2", "Android / Handheld" };
        static readonly string[] Axes = { "Mouse X", "Mouse Y", "Mouse ScrollWheel", "Joy X", "Joy Y", "Joy 3", "Joy 4", "Joy 5", "Joy 6", "Joy 7", "Joy 8", "Joy 9", "Joy 10" };
        const int N = 40; static readonly int Count = Enum.GetValues(typeof(K)).Length;
        static readonly Bind[] map = Mk(), alt = Mk(), pad = Mk();
        static Bind[] Mk() { var a = new Bind[N]; for (int i = 0; i < N; i++) a[i] = new Bind(); return a; }
        public static float CameraSensitivity = 5f; public static bool FlipCameraY; public static int JoyType; public static bool SameKey, Capturing;
        public static int CancelCaptureFrame = -1;
        static bool loaded, androidMappings;

        // ---------------------------------------------------------------- tablas por defecto (ConfigurableInput..cctor)
        static readonly KeyCode[][] DefKey =
        {
            new[] { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.BackQuote, KeyCode.Space, KeyCode.Z, KeyCode.X, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Tab, KeyCode.R, KeyCode.C, KeyCode.I, KeyCode.N, KeyCode.J, KeyCode.P, KeyCode.None, KeyCode.H,
                    KeyCode.Home, KeyCode.Return, KeyCode.M, KeyCode.Quote, KeyCode.Q, KeyCode.E, KeyCode.LeftControl, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.F1, KeyCode.F, KeyCode.F5, KeyCode.V, KeyCode.Period, KeyCode.None },
            new[] { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None,
                    KeyCode.None, KeyCode.KeypadEnter, KeyCode.None, KeyCode.None, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.RightControl, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None }
        };
        static readonly int[][] DefAxis =
        {
            new[] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 2, 2, 1, 1, 0, 0, -1, -1, -1, -1, -1, -1 },
            new[] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }
        };
        // mando: [perfil][accion] -> boton (0 = KeyCode.None) y eje (-1 = ninguno)
        static KeyCode PadDefKey(int type, int k)
        {
            switch (type)
            {
                case 0: switch (k) { case 5: return KeyCode.JoystickButton0; case 8: return KeyCode.JoystickButton2; case 9: return KeyCode.JoystickButton3; case 10: return KeyCode.JoystickButton1; case 11: return KeyCode.JoystickButton5; case 13: return KeyCode.JoystickButton4; case 21: return KeyCode.JoystickButton7; } break;
                case 1:
                case 2: switch (k) { case 5: return KeyCode.JoystickButton1; case 6: return KeyCode.JoystickButton7; case 7: return KeyCode.JoystickButton6; case 8: return KeyCode.JoystickButton0; case 9: return KeyCode.JoystickButton3; case 10: return KeyCode.JoystickButton2; case 11: return KeyCode.JoystickButton5; case 13: return KeyCode.JoystickButton4; case 21: return KeyCode.JoystickButton9; case 36: return KeyCode.JoystickButton7; } break;
                default:
                    switch (k)
                    {
                        case 5: return KeyCode.JoystickButton0; case 6: return KeyCode.JoystickButton5; case 7: return KeyCode.JoystickButton4; case 8: return KeyCode.JoystickButton2; case 9: return KeyCode.JoystickButton3; case 10: return KeyCode.JoystickButton1;
                        case 4: return KeyCode.JoystickButton7; case 11: return KeyCode.JoystickButton9; case 22: return KeyCode.JoystickButton6; case 35: return KeyCode.JoystickButton8;
                    }
                    break;
            }
            return KeyCode.None;
        }
        static int PadDefAxis(int type, int k)
        {
            if (k == 0 || k == 1) return 4; if (k == 2 || k == 3) return 3;   // stick izquierdo (Joy Y / Joy X)
            switch (type)
            {
                case 0: if (k == 6 || k == 7) return 5; if (k == 28 || k == 29) return 9; if (k == 30 || k == 31) return 7; if (k == 32 || k == 33) return 6; if (k == 36) return 5; if (k == 37) return 8; break;
                case 1: if (k == 28 || k == 29) return 8; if (k == 30 || k == 31) return 6; if (k == 32 || k == 33) return 5; if (k == 37) return 7; break;
                case 2: if (k == 28 || k == 29) return 8; if (k == 30 || k == 31) return 5; if (k == 32 || k == 33) return 6; if (k == 37) return 7; break;
                default: if (k == 30 || k == 31) return 6; if (k == 32 || k == 33) return 5; break;   // stick derecho en Android: Joy 3 (horizontal) / Joy 4 (vertical)
            }
            return -1;
        }
        // pares de eje del original (axisKeyMappings)
        static readonly K[][] Pairs = { new[] { K.Right, K.Left }, new[] { K.RightTurn, K.LeftTurn }, new[] { K.Down, K.Up }, new[] { K.CameraRight, K.CameraLeft }, new[] { K.CameraUp, K.CameraDown }, new[] { K.ZoomIn, K.ZoomOut },
            new[] { K.Inventory, K.Journal }, new[] { K.QEmailBox, K.Contacts }, new[] { K.Fire1, K.Fire1 }, new[] { K.Fire2, K.Fire2 }, new[] { K.VehicleToggle, K.None } };

        public static int DefaultJoyType() { return Application.platform == RuntimePlatform.Android ? 3 : 0; }

        // ---------------------------------------------------------------- carga / guardado
        public static void EnsureLoaded() { if (!loaded) Load(); }
        public static void Load() => LoadForPlatform(Application.platform == RuntimePlatform.Android);
        static void LoadForPlatform(bool android)
        {
            androidMappings = android;
            loaded = true; CameraSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("Camera Sensitivity", 5f), 1f, 10f); FlipCameraY = PlayerPrefs.GetInt("CameraYflip", 0) != 0;
            bool ok = PlayerPrefs.GetInt("InputVer", 0) == 2; JoyType = Mathf.Clamp(PlayerPrefs.GetInt("PadType", android ? 3 : 0), 0, PadName.Length - 1);
            for (int i = 0; i < N; i++)
            {
                string n = ((K)i).ToString();
                map[i].key = (KeyCode)PlayerPrefs.GetInt(n + "Mapping", (int)DefKey[0][i]); map[i].axis = PlayerPrefs.GetInt(n + "AxisMapping", DefAxis[0][i]);
                alt[i].key = (KeyCode)PlayerPrefs.GetInt(n + "AltMapping", (int)DefKey[1][i]); alt[i].axis = PlayerPrefs.GetInt(n + "AltAxisMapping", DefAxis[1][i]);
                pad[i].key = ok ? (KeyCode)PlayerPrefs.GetInt(n + "PadMapping", (int)PadDefKey(JoyType, i)) : PadDefKey(JoyType, i); pad[i].axis = ok ? PlayerPrefs.GetInt(n + "PadAxisMapping", PadDefAxis(JoyType, i)) : PadDefAxis(JoyType, i);
            }
            ResetTransientState();
            Debug.Log("[input] loaded profile=" + PadName[JoyType] + " attack=" + (int)pad[(int)K.Fire1].key + " axis=" + pad[(int)K.Fire1].axis);
        }
        static void SavePadPreferences()
        {
            PlayerPrefs.SetInt("InputVer", 2); PlayerPrefs.SetInt("PadType", JoyType);
            for (int i = 0; i < N; i++)
            {
                string n = ((K)i).ToString();
                PlayerPrefs.SetInt(n + "PadMapping", (int)pad[i].key); PlayerPrefs.SetInt(n + "PadAxisMapping", pad[i].axis);
            }
        }
        static void ResetTransientState()
        {
            Array.Clear(axCur, 0, N); Array.Clear(axPrev, 0, N); tickFrame = -1;
            auto = false; Capturing = false; badAxis.Clear(); CancelRemap();
        }
        public static void Save()
        {
            if (!loaded) return;
            SavePadPreferences();
            PlayerPrefs.SetFloat("Camera Sensitivity", CameraSensitivity); PlayerPrefs.SetInt("CameraYflip", FlipCameraY ? 1 : 0);
            for (int i = 0; i < N; i++)
            {
                string n = ((K)i).ToString();
                PlayerPrefs.SetInt(n + "Mapping", (int)map[i].key); PlayerPrefs.SetInt(n + "AxisMapping", map[i].axis); PlayerPrefs.SetInt(n + "AltMapping", (int)alt[i].key); PlayerPrefs.SetInt(n + "AltAxisMapping", alt[i].axis);
            }
            TouchLayout.Save(); PlayerPrefs.Save();
        }
        /// ResetInputControl: sensibilidad 5 y eje Y de camara sin invertir.
        public static void ResetInputControl() { CameraSensitivity = 5f; FlipCameraY = false; }
        /// ResetPadMap: restaura las asignaciones de mando del perfil elegido.
        public static void ResetPadMap() { EnsureLoaded(); for (int i = 0; i < N; i++) { pad[i].key = PadDefKey(JoyType, i); pad[i].axis = PadDefAxis(JoyType, i); } ResetTransientState(); }
        /// ResetMappings: todo a los valores por defecto (perfil de mando por defecto de la plataforma).
        public static void ResetMappings()
        {
            EnsureLoaded(); JoyType = androidMappings ? 3 : 0;
            for (int i = 0; i < N; i++) { map[i].key = DefKey[0][i]; map[i].axis = DefAxis[0][i]; alt[i].key = DefKey[1][i]; alt[i].axis = DefAxis[1][i]; pad[i].key = PadDefKey(JoyType, i); pad[i].axis = PadDefAxis(JoyType, i); }
            ResetTransientState();
        }

        // copia para CANCEL (GameOptions.Push/Pop)
        static int[] bk; static float bkSens; static bool bkFlip, bkAndroid; static int bkJoy;
        public static void Push()
        {
            EnsureLoaded(); bk = new int[N * 6]; for (int i = 0; i < N; i++) { bk[i * 6] = (int)map[i].key; bk[i * 6 + 1] = map[i].axis; bk[i * 6 + 2] = (int)alt[i].key; bk[i * 6 + 3] = alt[i].axis; bk[i * 6 + 4] = (int)pad[i].key; bk[i * 6 + 5] = pad[i].axis; }
            TouchLayout.Push(); bkSens = CameraSensitivity; bkFlip = FlipCameraY; bkJoy = JoyType; bkAndroid = androidMappings;
        }
        public static void Pop()
        {
            if (bk == null) return; for (int i = 0; i < N; i++) { map[i].key = (KeyCode)bk[i * 6]; map[i].axis = bk[i * 6 + 1]; alt[i].key = (KeyCode)bk[i * 6 + 2]; alt[i].axis = bk[i * 6 + 3]; pad[i].key = (KeyCode)bk[i * 6 + 4]; pad[i].axis = bk[i * 6 + 5]; }
            TouchLayout.Pop(); CameraSensitivity = bkSens; FlipCameraY = bkFlip; JoyType = bkJoy; androidMappings = bkAndroid; ResetTransientState();
        }

        // ---------------------------------------------------------------- lectura de ejes
        static readonly HashSet<int> badAxis = new HashSet<int>();
        static float Raw(int a)
        {
            if (a < 3 || a >= Axes.Length || badAxis.Contains(a)) return 0f;   // los ejes del raton los gestiona la camara del port
            try { return Input.GetAxisRaw(Axes[a]); } catch (Exception) { badAxis.Add(a); Debug.LogWarning("[input] falta el eje '" + Axes[a] + "' en el InputManager (ProjectSettings/InputManager.asset)"); return 0f; }
        }
        static bool MouseAllowed(KeyCode k) { return Input.touchCount == 0 || (int)k < (int)KeyCode.Mouse0 || (int)k > (int)KeyCode.Mouse6; }
        static bool Any(KeyCode k) { return k != KeyCode.None && MouseAllowed(k) && Input.GetKey(k); }
        // ConfigurableInput.GetAxis(up, down) para una asignacion
        static float Ax(Bind up, Bind down, bool keys = true)
        {
            if (up.axis != -1) return Raw(up.axis); if (down.axis != -1) return Raw(down.axis);
            if (!keys) return 0f; bool u = Any(up.key), d = Any(down.key); if (u && d) return 0f; return u ? 1f : d ? -1f : 0f;
        }
        static float Sum(K up, K down, bool withPad = true, float padSign = 1f)
        {
            int u = (int)up, d = (int)down; float v = Ax(map[u], map[d]) + Ax(alt[u], alt[d]);
            if (withPad) v += padSign * Ax(pad[u], pad[d]); return v;
        }
        static bool Typing { get { return ChatHud.Typing; } }

        /// Avance (+1) / retroceso (-1): teclas Up/Down y stick izquierdo. Incluye "autorun".
        public static float Forward
        {
            get
            {
                Tick(); float v = -Sum(K.Down, K.Up);   // el original usa Down como "up" del eje Vertical (adelante = negativo)
                if (Mathf.Abs(v) > 0.01f) auto = false; else if (auto) v = 1f; return Mathf.Clamp(v, -1f, 1f);
            }
        }
        /// Paso lateral (+1 derecha).
        public static float Strafe { get { Tick(); return Mathf.Clamp(Sum(K.Right, K.Left), -1f, 1f); } }
        /// Giro (+1 derecha): teclas LeftTurn/RightTurn.
        public static float Turn { get { Tick(); return Mathf.Clamp(Sum(K.RightTurn, K.LeftTurn), -1f, 1f); } }
        /// Camara (+1 derecha / arriba). Ejes de camara del teclado y del stick (el raton lo lee GameClient).
        public static float CameraX { get { Tick(); float v = Sum(K.CameraRight, K.CameraLeft); return Mathf.Clamp(FlipCameraY ? v : v, -1f, 1f); } }
        public static float CameraY { get { Tick(); float v = Sum(K.CameraUp, K.CameraDown, true, -1f); if (FlipCameraY) v = -v; return Mathf.Clamp(v, -1f, 1f); } }
        /// Zoom (+1 acercar): teclas ZoomIn/ZoomOut (GetAxisZoom no incluye el mando).
        public static float Zoom { get { Tick(); return Mathf.Clamp(Sum(K.ZoomIn, K.ZoomOut, false), -1f, 1f); } }

        // ---------------------------------------------------------------- botones
        static readonly bool[] axCur = new bool[N], axPrev = new bool[N]; static int tickFrame = -1; static bool auto;
        static bool AxisKey(int i) { return i == (int)K.Fire1 || i == (int)K.Fire2 || i == (int)K.Jump || i == (int)K.WpnChange || i == (int)K.NanoCharge || i == (int)K.WpnCharge || i == (int)K.CameraShoulderToggle || i == (int)K.VehicleToggle; }
        static void Tick()
        {
            if (!loaded) Load(); if (tickFrame == Time.frameCount) return; tickFrame = Time.frameCount;
            for (int i = 0; i < N; i++) { if (!AxisKey(i)) continue; axPrev[i] = axCur[i]; axCur[i] = (map[i].axis >= 3 && Mathf.Abs(Raw(map[i].axis)) > 0.5f) || (alt[i].axis >= 3 && Mathf.Abs(Raw(alt[i].axis)) > 0.5f) || (pad[i].axis >= 3 && Mathf.Abs(Raw(pad[i].axis)) > 0.5f); }
            if (!Typing && Down(K.AutoRun)) auto = !auto;
        }
        public static bool Held(K k, bool mouse = true)
        {
            Tick(); int i = (int)k; if (i >= N || Capturing) return false;
            if (TouchControls.Held(k)) return true;
            return Any(map[i].key) && (mouse || map[i].key != KeyCode.Mouse0) || Any(alt[i].key) && (mouse || alt[i].key != KeyCode.Mouse0) || Any(pad[i].key) || (AxisKey(i) && axCur[i]);
        }
        public static bool IsPointerKey(KeyCode key) => (int)key >= (int)KeyCode.Mouse0 && (int)key <= (int)KeyCode.Mouse6;
        // Keep controller/keyboard/touch actions independent of the mouse hover
        // state. Android retains a synthetic mouse position after a UI tap.
        public static bool HeldNonPointer(K k)
        {
            Tick(); int i = (int)k; if (i >= N || Capturing) return false;
            return TouchControls.Held(k) || !IsPointerKey(map[i].key) && Any(map[i].key) ||
                !IsPointerKey(alt[i].key) && Any(alt[i].key) || !IsPointerKey(pad[i].key) && Any(pad[i].key) || AxisKey(i) && axCur[i];
        }
        public static bool HeldPointer(K k)
        {
            EnsureLoaded(); int i = (int)k; if (i >= N || Capturing) return false;
            return IsPointerKey(map[i].key) && Any(map[i].key) || IsPointerKey(alt[i].key) && Any(alt[i].key) || IsPointerKey(pad[i].key) && Any(pad[i].key);
        }
        public static bool Down(K k, bool mouse = true)
        {
            if (!loaded) Load(); int i = (int)k; if (i >= N || Capturing) return false; if (tickFrame != Time.frameCount) Tick();
            if (TouchControls.Down(k)) return true;
            return D(map[i].key, mouse) || D(alt[i].key, mouse) || D(pad[i].key, true) || (AxisKey(i) && axCur[i] && !axPrev[i]);
        }
        public static bool Up(K k, bool mouse = true)
        {
            if (!loaded) Load(); int i = (int)k; if (i >= N || Capturing) return false; if (tickFrame != Time.frameCount) Tick();
            if (TouchControls.Up(k)) return true;
            return U(map[i].key, mouse) || U(alt[i].key, mouse) || U(pad[i].key, true) || (AxisKey(i) && !axCur[i] && axPrev[i]);
        }
        static bool D(KeyCode c, bool mouse) { return c != KeyCode.None && (mouse || c != KeyCode.Mouse0) && MouseAllowed(c) && Input.GetKeyDown(c); }
        static bool U(KeyCode c, bool mouse) { return c != KeyCode.None && (mouse || c != KeyCode.Mouse0) && MouseAllowed(c) && Input.GetKeyUp(c); }
        /// Hay un mando o stick en uso este frame (para ocultar el joystick tactil, etc.).
        static float nextPadPoll; static bool padConnected;
        public static bool PadActive
        {
            get
            {
                if (Time.unscaledTime >= nextPadPoll)
                {
                    nextPadPoll = Time.unscaledTime + .25f;
                    try { padConnected = HasGamepad(Input.GetJoystickNames()); } catch (Exception) { padConnected = false; }
                }
                return padConnected;
            }
        }

        public static bool HasGamepad(string[] names)
        {
            if (names != null) foreach (var name in names) if (!string.IsNullOrWhiteSpace(name)) return true;
            return false;
        }

        // ---------------------------------------------------------------- textos y reasignacion (cnOptionMode)
        static string Pretty(KeyCode k) { return k == KeyCode.None ? "" : k.ToString(); }
        public static string Name(K k, int slot)
        {
            int i = (int)k; var b = slot == 0 ? map[i] : slot == 1 ? alt[i] : pad[i];
            return b.axis == -1 ? Pretty(b.key) : (b.axis >= 0 && b.axis < Axes.Length ? Axes[b.axis] : "");
        }
        static Bind[] Slot(int slot) { return slot == 0 ? map : slot == 1 ? alt : pad; }
        public static KeyCode PendingKey { get; private set; }
        public static int PendingAxis { get; private set; } = -1;
        public static string ConflictText { get; private set; }
        static readonly float[] captureAxisOrigin = new float[13]; static bool captureStarted;
        public static void BeginRemap()
        {
            CancelRemap(); for (int a = 3; a < captureAxisOrigin.Length; a++) captureAxisOrigin[a] = Raw(a);
            captureStarted = true;
        }
        public static void CancelRemap() { captureStarted = false; SameKey = false; PendingKey = KeyCode.None; PendingAxis = -1; ConflictText = ""; }
        static K AxisPartner(K k)
        {
            foreach (var p in Pairs) if (p[0] != p[1]) { if (p[0] == k) return p[1]; if (p[1] == k) return p[0]; }
            return K.None;
        }
        public static void Clear(K k, int slot)
        {
            EnsureLoaded(); var b = Slot(slot)[(int)k]; b.key = KeyCode.None; b.axis = -1;
        }
        // No mutation on conflict: replacement only happens after explicit confirmation.
        public static bool Assign(K k, int slot, KeyCode key, int axis = -1, bool replace = false)
        {
            EnsureLoaded(); var conflicts = new List<Bind>(); var names = new List<string>();
            K partner = axis >= 0 ? AxisPartner(k) : K.None;
            for (int c = 0; c < 3; c++) for (int i = 0; i < N; i++)
            {
                if (c == slot && (i == (int)k || i == (int)partner)) continue;
                var b = Slot(c)[i];
                if (axis >= 0 ? b.axis == axis : key != KeyCode.None && b.key == key)
                { conflicts.Add(b); names.Add(((K)i) + " (Setting " + (c + 1) + ")"); }
            }
            if (conflicts.Count > 0 && !replace)
            {
                PendingKey = key; PendingAxis = axis; ConflictText = string.Join(", ", names); SameKey = true; return false;
            }
            foreach (var b in conflicts) { if (axis >= 0) b.axis = -1; else b.key = KeyCode.None; }
            var target = Slot(slot)[(int)k]; target.key = key; target.axis = axis;
            if (partner != K.None) { var pair = Slot(slot)[(int)partner]; pair.axis = axis; pair.key = KeyCode.None; }
            CancelRemap(); return true;
        }
        public static bool ReplacePending(K k, int slot) { return Assign(k, slot, PendingKey, PendingAxis, true); }
        static KeyCode[] allCodes;
        public static bool Remap(K k, int slot)
        {
            EnsureLoaded(); if (!captureStarted) { BeginRemap(); return false; } bool joy = slot == 2;
            if (allCodes == null) allCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));
            foreach (var c in allCodes)
            {
                // Mouse clicks are handled by SettingsWindow after cell hit testing.
                if (c == KeyCode.None || c == KeyCode.Escape || ((int)c >= (int)KeyCode.Mouse0 && (int)c <= (int)KeyCode.Mouse6) ||
                    (joy != ((int)c >= 330)) || !Input.GetKeyDown(c)) continue;
                return Assign(k, slot, c);
            }
            int best = -1; float bv = .65f;
            for (int a = 0; a < Axes.Length; a++)
            {
                if (joy != (a >= 3)) continue;
                float v = a < 3 ? (a == 2 ? Mathf.Abs(Input.GetAxisRaw("Mouse ScrollWheel")) * 8f : 0f) : Mathf.Abs(Raw(a) - captureAxisOrigin[a]);
                if (v > bv) { bv = v; best = a; }
            }
            return best >= 0 && Assign(k, slot, KeyCode.None, best);
        }
    }
}
