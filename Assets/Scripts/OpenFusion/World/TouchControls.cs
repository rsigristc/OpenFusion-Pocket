// Controles táctiles (Android): joystick flotante a la izquierda, botón de salto, botón de chat, arrastre con un dedo = girar cámara, pellizco = zoom,
// toque corto = seleccionar/hablar con NPC. Se activa solo en móvil (o en el Editor con ForceInEditor / tecla F10 para probar con el ratón no hace falta: usa Device Simulator).
// Each finger owns its action until release, independently of the simulated mouse.
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    [DefaultExecutionOrder(-200)]
    public sealed class TouchControls : MonoBehaviour
    {
        public static TouchControls Instance { get; private set; }
        public bool ForceInEditor = false;
        /// <summary>Hay pantalla táctil real que gobernar.</summary>
        public static bool Active { get { return Instance != null && Instance.on; } }
        /// <summary>Joystick: x = derecha/izquierda, y = adelante/atrás (−1..1, ya con zona muerta). Cero si no se toca o hay diálogo/chat.</summary>
        public static Vector2 Move { get; private set; }
        /// <summary>Giro de cámara de este cuadro, en GRADOS (x derecha, y arriba).</summary>
        public static Vector2 Look { get; private set; }
        /// <summary>Cambio de separación entre dos dedos este cuadro, en píxeles (+ = separar).</summary>
        public static float Pinch { get; private set; }
        static readonly HashSet<ControlMap.K> held = new HashSet<ControlMap.K>(), down = new HashSet<ControlMap.K>(), up = new HashSet<ControlMap.K>();
        public static bool Held(ControlMap.K k) { return Active && held.Contains(k); }
        public static bool Down(ControlMap.K k) { return Active && down.Contains(k); }
        public static bool Up(ControlMap.K k) { return Active && up.Contains(k); }
        static bool jumpFlag; static bool tapFlag; static Vector2 tapPos;
        public static bool ConsumeJump() { bool j = jumpFlag; jumpFlag = false; return j; }
        /// <summary>Toque corto de este cuadro (coordenadas de pantalla estilo Input.mousePosition, origen abajo-izquierda).</summary>
        public static bool TryTap(out Vector2 pos) { pos = tapPos; bool t = tapFlag; tapFlag = false; return t; }
        /// <summary>Equivale a Input.GetAxisRaw("Horizontal"/"Vertical") pero con el joystick (−1, 0, 1).</summary>
        public static float AxisRaw(string name)
        {
            if (Active && Move.sqrMagnitude > 0f) { float v = name == "Horizontal" ? Move.x : Move.y; return Mathf.Abs(v) < 0.4f ? 0f : Mathf.Sign(v); }
            float cv = name == "Horizontal" ? ControlMap.Strafe : ControlMap.Forward; return Mathf.Abs(cv) < 0.4f ? 0f : Mathf.Sign(cv);
        }

        enum Role { None, Joy, Look, LookStick, Action, Chat }
        static readonly Color[] ControllerColors = { new Color(1,.3f,.3f), new Color(.3f,1,.4f), new Color(.3f,.6f,1), Color.yellow };
        static readonly string[] ControllerLabels = { "B\nATK", "A\nJUMP", "X\nWEAPON", "Y\nVEHICLE" };
        sealed class Tch { public Role Role; public ControlMap.K Action; public Vector2 Start, Pos, Prev; public float T0; public float Moved; }
        readonly Dictionary<int, Tch> tch = new Dictionary<int, Tch>();
        bool on; int joyId = -1; Vector2 joyCenter, joyThumb; Texture2D disc, ring;
        float dpScale;   // píxeles por dp (160 dpi = 1)
        public float TapMaxSeconds = 0.35f, TapMaxDp = 12f, LookDegPerDp = 0.3f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this; on = false;
            if (Application.isMobilePlatform)
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep; Input.multiTouchEnabled = true; Input.simulateMouseWithTouches = true;
                QualitySettings.vSyncCount = 0;
            }
        }
        void ResetTouches()
        {
            tch.Clear(); joyId = -1; Move = Look = Vector2.zero; Pinch = 0f;
            held.Clear(); down.Clear(); up.Clear(); jumpFlag = tapFlag = false;
        }
        // Surface resizing invalidates held fingers and the cached pixel/dp geometry.
        public void CancelForSurfaceChange() { ResetTouches(); dpScale = 0f; }
        void OnApplicationFocus(bool focus) { if (!focus) ResetTouches(); }
        void OnApplicationPause(bool paused) { if (paused) ResetTouches(); }
        void OnDisable() { ResetTouches(); }
        void OnDestroy() { ResetTouches(); if (Instance == this) { Instance = null; Move = Vector2.zero; Look = Vector2.zero; Pinch = 0f; } if (disc != null) Destroy(disc); if (ring != null) Destroy(ring); }

        static float ViewWidth => Screen.width > 0 ? Screen.width : 1280f;
        static float ViewHeight => Screen.height > 0 ? Screen.height : 720f;
        static Rect SafeArea { get { var r = Screen.safeArea; return r.width > 0 && r.height > 0 ? r : new Rect(0, 0, ViewWidth, ViewHeight); } }
        public static Rect LayoutArea { get { var r=SafeArea;return new Rect(r.x,ViewHeight-r.yMax,r.width,r.height); } }
        public Rect LayoutRect(int index) => index==TouchLayout.Joystick ? JoyRect : index==TouchLayout.CameraStick ? CameraStickRect : ActionRect(TouchLayout.ActionIndex(index));
        float Dp { get { if (dpScale <= 0f) { float dpi = Screen.dpi > 1f ? Screen.dpi : 160f; dpScale = Mathf.Min(dpi / 160f, ViewHeight / 540f, ViewWidth / 960f); } return dpScale; } }
        Vector2 ToGui(Vector2 p) { return new Vector2(p.x, ViewHeight - p.y); }   // Input (abajo-izq) -> GUI (arriba-izq)
        Rect ActionRect(int index)
        {
            // Fixed cells keep the user's muscle memory when another button is hidden.
            var safe = SafeArea; float d = Mathf.Min(Dp * TouchLayout.Scale, (safe.height - 185f * Dp) / 325f), size = 58f * d, gap = 7f * d;
            float right = safe.xMax - 12f * Dp, bottom = ViewHeight - safe.yMin - 125f * Dp;
            var fallback = new Rect(right - (index % 3 + 1) * (size + gap), bottom - (index / 3 + 1) * (size + gap), size, size);
            return TouchLayout.ActionRect(TouchLayout.GeometryIndex(index), new Rect(safe.x, ViewHeight - safe.yMax, safe.width, safe.height), fallback);
        }
        Rect ChatRect { get { return new Rect(SafeArea.xMax - 62f * Dp, 210f * Dp, 52f * Dp, 42f * Dp); } }
        int HitAction(Vector2 g)
        {
            for (int i = 0; i < TouchLayout.Actions.Length; i++) if (ActionVisible(i) && ActionRect(i).Contains(g)) return i;
            return -1;
        }
        public static bool ActionVisible(int index)
        {
            var k = TouchLayout.Actions[index];
            return TouchLayout.Enabled(index) && !(DualScreenController.ShowSecondaryNanos && (k == ControlMap.K.Nano1 || k == ControlMap.K.Nano2 || k == ControlMap.K.Nano3));
        }
        Rect JoyRect { get { var safe=SafeArea;var area=new Rect(safe.x,ViewHeight-safe.yMax,safe.width,safe.height);return TouchLayout.ActionRect(TouchLayout.Joystick,area,new Rect(safe.x+58*Dp,ViewHeight-safe.yMin-168*Dp,110*Dp,110*Dp)); } }
        bool InJoyZone(Vector2 g) { return TouchLayout.FixedJoystick ? JoyRect.Contains(g) : g.x < ViewWidth * 0.38f && g.y > ViewHeight * 0.35f; }
        Rect CameraStickRect { get { var safe=SafeArea;var area=new Rect(safe.x,ViewHeight-safe.yMax,safe.width,safe.height);return TouchLayout.ActionRect(TouchLayout.CameraStick,area,new Rect(area.x+area.width*.65f-55*Dp,area.y+area.height*.8f-55*Dp,110*Dp,110*Dp)); } }
        bool Blocked { get { return ChatHud.Typing || NpcUI.Active || WelcomePopup.Active || GameMenu.Blocking || TouchProfileEditor.Active || GamepadPointer.Active; } }

        public static bool CanShow(bool connected, bool player, bool launcher, bool loading, bool selection)
            => connected && player && !launcher && !loading && !selection;
        void Update()
        {
            var client = GameClient.FindSelf;
            bool worldReady = CanShow(client != null && client.WorldConnected,
                client != null && client.Bootstrap != null && client.Bootstrap.Player != null,
                client != null && client.GetComponent<LauncherScreen>()?.Visible == true,
                LoadingScreen.I != null && LoadingScreen.I.Visible && !LoadingScreen.I.Mini, client != null && client.GetComponent<CharSelectScreen>()?.Visible == true);
            bool active = (ForceInEditor || Application.isMobilePlatform && worldReady) && !(TouchLayout.HideWithPad && ControlMap.PadActive);
            ProcessTouches(Input.touches, active, Blocked);
        }
        internal void ProcessTouches(Touch[] touches, bool enabled, bool blocked)
        {
            Look = Vector2.zero; Pinch = 0f; Move = Vector2.zero;
            if (on != enabled) { on = enabled; ResetTouches(); }
            down.Clear(); up.Clear(); tapFlag = false;
            if (!on) return;
            if (blocked) { ResetTouches(); return; }
            var previous = new HashSet<ControlMap.K>(held); held.Clear();
            var seen = new HashSet<int>();
            for (int i = 0; i < touches.Length; i++)
            {
                var t = touches[i]; seen.Add(t.fingerId); Vector2 g = ToGui(t.position);
                if (t.phase == TouchPhase.Began)
                {
                    var c = new Tch { Start = t.position, Pos = t.position, Prev = t.position, T0 = Time.unscaledTime };
                    int action = HitAction(g);
                    if (action >= 0) { c.Role = Role.Action; c.Action = TouchLayout.Actions[action]; }
                    else if (ChatRect.Contains(g)) c.Role = Role.Chat;
                    else if (InJoyZone(g) && joyId < 0 && !blocked) { c.Role = Role.Joy; joyId = t.fingerId; joyCenter = TouchLayout.FixedJoystick ? new Vector2(JoyRect.center.x, ViewHeight-JoyRect.center.y) : t.position; joyThumb = t.position; }
                    else if (TouchLayout.CameraStickEnabled && CameraStickRect.Contains(g)) c.Role = Role.LookStick;
                    else c.Role = Role.Look;
                    tch[t.fingerId] = c;
                    if (c.Role == Role.Chat && ChatHud.Instance != null) { if (ChatHud.Typing) ChatHud.Instance.Close(); else ChatHud.Instance.Open(); }
                    continue;
                }
                Tch s; if (!tch.TryGetValue(t.fingerId, out s)) continue;
                s.Prev = s.Pos; s.Pos = t.position; s.Moved += (s.Pos - s.Prev).magnitude;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    if (s.Role == Role.Look && t.phase == TouchPhase.Ended && Time.unscaledTime - s.T0 <= TapMaxSeconds && s.Moved <= TapMaxDp * Dp) { tapFlag = true; tapPos = t.position; }
                    if (s.Role == Role.Joy) joyId = -1;
                    tch.Remove(t.fingerId); continue;
                }
                if (s.Role == Role.Joy) joyThumb = t.position;
            }
            // dedos que desaparecieron sin fase Ended
            List<int> gone = null; foreach (var k in tch.Keys) if (!seen.Contains(k)) (gone ?? (gone = new List<int>())).Add(k);
            if (gone != null) foreach (var k in gone) { if (k == joyId) joyId = -1; tch.Remove(k); }

            foreach (var s in tch.Values) if (s.Role == Role.Action) held.Add(s.Action);
            foreach (var k in held) if (!previous.Contains(k)) down.Add(k);
            foreach (var k in previous) if (!held.Contains(k)) up.Add(k);

            if (joyId >= 0 && !blocked)
            {
                float R = JoyRect.width * .5f; var d = joyThumb - joyCenter; if (d.magnitude > R) { if (!TouchLayout.FixedJoystick) joyCenter = joyThumb - d.normalized * R; d = (joyThumb - joyCenter).normalized * R; }   // flotante: arrastra la base
                var v = d / R; if (v.magnitude < 0.15f) v = Vector2.zero; Move = Vector2.ClampMagnitude(v, 1f);
            }
            else if (joyId >= 0) { joyId = -1; }

            // cámara: un dedo = girar; dos dedos de cámara = pellizco (zoom)
            var looks = new List<Tch>(); foreach (var s in tch.Values) if (s.Role == Role.Look) looks.Add(s);
            if (looks.Count == 1) { var d = looks[0].Pos - looks[0].Prev; if (looks[0].Moved > TapMaxDp * Dp) Look = d / Dp * LookDegPerDp; }
            else if (looks.Count >= 2)
            {
                float now = (looks[0].Pos - looks[1].Pos).magnitude, before = (looks[0].Prev - looks[1].Prev).magnitude; Pinch = (now - before) / Dp;
            }
            foreach(var stick in tch.Values) if(stick.Role==Role.LookStick) {
                var center=CameraStickRect.center;center.y=ViewHeight-center.y;
                var axis=Vector2.ClampMagnitude((stick.Pos-center)/Mathf.Max(1,CameraStickRect.width*.5f),1);
                if(axis.magnitude>.15f) Look+=axis*140f*Mathf.Min(Time.unscaledDeltaTime,.05f)*(ControlMap.CameraSensitivity/5f);
                break;
            }
            // un toque con un dedo de "joystick" o "cámara" mientras se escribe en el chat no mueve nada
            if (blocked) { Move = Vector2.zero; }
        }

        void MakeTex()
        {
            if (disc != null) return;
            const int N = 128; disc = new Texture2D(N, N, TextureFormat.RGBA32, false); ring = new Texture2D(N, N, TextureFormat.RGBA32, false);
            disc.hideFlags = ring.hideFlags = HideFlags.HideAndDontSave; disc.wrapMode = ring.wrapMode = TextureWrapMode.Clamp;
            var cd = new Color32[N * N]; var cr = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f; float r = Mathf.Sqrt(dx * dx + dy * dy); float aa = Mathf.Clamp01((1f - r) * N * 0.5f);
                cd[y * N + x] = new Color(1f, 1f, 1f, aa); float ring_ = Mathf.Clamp01(1f - Mathf.Abs(r - 0.93f) * N * 0.25f); cr[y * N + x] = new Color(1f, 1f, 1f, ring_ * aa);
            }
            disc.SetPixels32(cd); disc.Apply(false, true); ring.SetPixels32(cr); ring.Apply(false, true);
        }

        void OnGUI()
        {
            if (!on || Blocked || Event.current.type != EventType.Repaint) return;
            MakeTex(); GUI.depth = -1000; var old = GUI.color; float joyAlpha = TouchLayout.Opacity(TouchLayout.Joystick) / .6f;
            if (!Blocked)
            {
                if (joyId >= 0)
                {
                    float R = JoyRect.width * .5f; var c = ToGui(joyCenter); var th = ToGui(joyThumb); if ((th - c).magnitude > R) th = c + (th - c).normalized * R;
                    GUI.color = new Color(1f, 1f, 1f, 0.18f * joyAlpha); GUI.DrawTexture(new Rect(c.x - R, c.y - R, 2 * R, 2 * R), disc);
                    GUI.color = new Color(1f, 1f, 1f, 0.55f * joyAlpha); GUI.DrawTexture(new Rect(c.x - R, c.y - R, 2 * R, 2 * R), ring);
                    float r2 = R * .47f; GUI.color = new Color(1f, 1f, 1f, 0.5f * joyAlpha); GUI.DrawTexture(new Rect(th.x - r2, th.y - r2, 2 * r2, 2 * r2), disc);
                }
                else
                {   // pista de dónde pulsar
                    float R = JoyRect.width * .5f; float cx = JoyRect.center.x, cy = JoyRect.center.y;
                    GUI.color = new Color(1f, 1f, 1f, 0.10f * joyAlpha); GUI.DrawTexture(new Rect(cx - R, cy - R, 2 * R, 2 * R), disc);
                    GUI.color = new Color(1f, 1f, 1f, 0.28f * joyAlpha); GUI.DrawTexture(new Rect(cx - R, cy - R, 2 * R, 2 * R), ring);
                }
                var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(11f * Dp * TouchLayout.Scale), fontStyle = FontStyle.Bold, wordWrap = true };
                for (int i = 0; i < TouchLayout.Actions.Length; i++)
                {
                    if (!ActionVisible(i)) continue;
                    var r = ActionRect(i); bool pressed = held.Contains(TouchLayout.Actions[i]);
                    GUI.color = new Color(1f, 1f, 1f, (pressed ? .8f : .35f) * TouchLayout.Opacity(TouchLayout.GeometryIndex(i))); GUI.DrawTexture(r, disc);
                    GUI.color = new Color(1f, 1f, 1f, TouchLayout.Opacity(TouchLayout.GeometryIndex(i))); GUI.DrawTexture(r, ring);
                    if(TouchLayout.ControllerStyle && i<4) {
                        var color=ControllerColors[i];color.a=TouchLayout.Opacity(TouchLayout.GeometryIndex(i));GUI.color=color;GUI.DrawTexture(r,ring);
                    }
                    GUI.color = new Color(1,1,1,TouchLayout.Opacity(TouchLayout.GeometryIndex(i))); GUI.Label(r, TouchLayout.ControllerStyle && i<4 ? ControllerLabels[i] : TouchLayout.Labels[i], st);
                }
                if(TouchLayout.CameraStickEnabled) {
                    var r=CameraStickRect;GUI.color=new Color(1,1,1,TouchLayout.Opacity(TouchLayout.CameraStick));GUI.DrawTexture(r,ring);
                    var thumb=r.center;
                    foreach(var stick in tch.Values)if(stick.Role==Role.LookStick){thumb=r.center+Vector2.ClampMagnitude(ToGui(stick.Pos)-r.center,r.width*.5f);break;}
                    GUI.DrawTexture(new Rect(thumb.x-r.width*.2f,thumb.y-r.height*.2f,r.width*.4f,r.height*.4f),disc);
                    GUI.color=new Color(1,1,1,TouchLayout.Opacity(TouchLayout.CameraStick));GUI.Label(r,"LOOK",st);
                }
            }
            var ch = ChatRect; GUI.color = new Color(1f, 1f, 1f, 0.22f); GUI.DrawTexture(ch, disc); GUI.color = new Color(1f, 1f, 1f, 0.6f); GUI.DrawTexture(ch, ring);
            GUI.color = Color.white; var st2 = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(13f * Dp), fontStyle = FontStyle.Bold }; st2.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(ch, "CHAT", st2);
            GUI.color = old;
        }
    }
}
