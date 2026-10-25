// Creación de nombre ("name wheel") del cliente FusionFall original (CnCharNameCreationMode / CnGuiNameCreation), IMGUI sobre UiKit.
// Ventana 672x365 con pestañas "GENERATE A NAME" (rueda de 3 columnas) y "CREATE YOUR OWN" (texto libre). Flujo: CHECK_CHAR_NAME -> SAVE_CHAR_NAME.
// Táctil: columnas arrastrables con el dedo, botones de 36+ unidades de área táctil (el dibujo conserva el tamaño original). SIN COMPILAR EN UNITY.
// Textos de mensajes de sistema por id (187, 189, 192, 198-202, 228, 229): textos exactos de la tabla SystemMessage (xdtdatas).
using System;
using System.Threading.Tasks;
using UnityEngine;
using OpenFusion.World;
using OpenFusion.Net;
using OpenFusion.Tables;
using OpenFusion.Tables.Gen;

namespace OpenFusion.Screens
{
    /// <summary>Utilidades compartidas por NameCreateScreen y CharCreateScreen (botones táctiles, fondo, cuadro de mensaje).</summary>
    internal static class CcUi
    {
        public static readonly Color Cyan = new Color(0.8f, 1f, 1f);          // texto de los estilos Transparent/BodyText
        public static readonly Color Cyan2 = new Color(0f, 1f, 1f);           // (0,255,255) del original: cian puro
        public static readonly Color Yellow = new Color(1f, 1f, 0f);          // NameDisplay
        public static readonly Color Grey = new Color(0.9f, 0.9f, 0.9f);      // TabButton
        public static BitmapFont F12 { get { return UiKit.Font("JEFFE___12__953"); } }
        public static BitmapFont F14 { get { return UiKit.Font("JEFFE___14__903"); } }
        public static BitmapFont F16 { get { return UiKit.Font("JEFFE___16__1012"); } }
        public static BitmapFont ChaletSmall { get { return UiKit.Font("ChaletBook-Regular_Small__1018"); } }

        static Texture2D grad;
        /// <summary>Fondo degradado oscuro de pantalla completa (charcreationbg no existe en el proyecto).</summary>
        public static void Background()
        {
            if (Event.current.type != EventType.Repaint) return;
            var bg = UiKit.Tex("CharCreationBG");   // charcreationbg.png del original (se muestra cubriendo la pantalla)
            if (bg != null) { GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture, ScaleMode.StretchToFill, false); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bg, ScaleMode.ScaleAndCrop, false); return; }
            if (grad == null)
            {
                grad = new Texture2D(1, 64, TextureFormat.RGBA32, false); grad.wrapMode = TextureWrapMode.Clamp; grad.filterMode = FilterMode.Bilinear;
                var top = new Color(0.03f, 0.10f, 0.22f); var bot = new Color(0f, 0.02f, 0.07f);
                for (int y = 0; y < 64; y++) grad.SetPixel(0, y, Color.Lerp(bot, top, y / 63f));
                grad.Apply();
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), grad, ScaleMode.StretchToFill, false);
        }

        /// <summary>Amplía un rect (centrado) hasta un mínimo para el área táctil; el dibujo no cambia.</summary>
        public static Rect Pad(Rect r, float minW, float minH)
        {
            float w = Mathf.Max(r.width, minW), h = Mathf.Max(r.height, minH);
            return new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
        }

        static void Face(Rect draw, Texture2D t, int sl)
        {
            if (t != null) { if (sl > 0) UiKit.Slice(draw, t, sl, sl, sl, sl); else GUI.DrawTexture(draw, t, ScaleMode.StretchToFill, true); }
            else { UiKit.Fill(draw, new Color(0.05f, 0.25f, 0.55f, 0.9f)); UiKit.Outline(draw, new Color(0f, 0.9f, 1f, 0.9f)); }
        }

        /// <summary>Botón con área táctil propia (hit) distinta de la del dibujo (draw). sl = borde nine-slice en píxeles (0 = estirar).</summary>
        public static bool Btn(Rect draw, Rect hit, Texture2D n, Texture2D o, string label, BitmapFont f, Color col, bool enabled, int sl = 0)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, hit); var e = Event.current; bool hov = hit.Contains(e.mousePosition), res = false;
            if (enabled)
            {
                if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; if (hov) res = true; e.Use(); }
            }
            if (e.type == EventType.Repaint)
            {
                bool hot = enabled && (GUIUtility.hotControl == id || (hov && GUIUtility.hotControl == 0 && !Application.isMobilePlatform));
                var o0 = GUI.color; if (!enabled) GUI.color = new Color(1, 1, 1, 0.5f * o0.a);
                Face(draw, hot && o != null ? o : n, sl);
                GUI.color = o0;
                if (!string.IsNullOrEmpty(label)) UiKit.Label(f, draw, label, TextAnchor.MiddleCenter, col * (enabled ? Color.white : new Color(1, 1, 1, 0.5f)));
            }
            return res;
        }

        /// <summary>Zona táctil invisible (sin dibujo).</summary>
        public static bool Tap(Rect hit, bool enabled)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, hit); var e = Event.current; bool hov = hit.Contains(e.mousePosition), res = false;
            if (!enabled) return false;
            if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; if (hov) res = true; e.Use(); }
            return res;
        }

        /// <summary>RepeatButton: devuelve true mientras el dedo/ratón la mantiene pulsada.</summary>
        public static bool Held(Rect draw, Rect hit, Texture2D n, Texture2D o, bool enabled, int sl = 0)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, hit); var e = Event.current; bool hov = hit.Contains(e.mousePosition);
            if (enabled)
            {
                if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
            }
            bool held = enabled && GUIUtility.hotControl == id;
            if (e.type == EventType.Repaint) Face(draw, held && o != null ? o : n, sl);
            return held;
        }

        public static Texture2D Tex(string n) { return UiKit.Tex(n); }
    }

    /// <summary>Cuadro de mensaje modal (sustituye a cnSystemMessageManager): OK, o OK/CANCEL.</summary>
    internal sealed class CcMsg
    {
        public bool Visible; string text, ok = "OK", cancel; Action onOk, onCancel;
        public void Show(string t, Action onOk = null) { text = t; ok = "OK"; cancel = null; this.onOk = onOk; onCancel = null; Visible = true; }
        public void Confirm(string t, Action onOk, Action onCancel = null) { text = t; ok = "OK"; cancel = "CANCEL"; this.onOk = onOk; this.onCancel = onCancel; Visible = true; }

        /// <summary>Se dibuja el último, en coordenadas centradas (UiKit.BeginCenter).</summary>
        public void Draw()
        {
            if (!Visible) return;
            var e = Event.current;
            UiKit.Fill(new Rect(-4000, -4000, 8000, 8000), new Color(0f, 0f, 0f, 0.6f));
            var box = new Rect(-190f, -85f, 380f, 170f);
            var bg = CcUi.Tex("CCNameBG"); if (bg != null) UiKit.Slice(box, bg, 5, 5, 0, 0); else { UiKit.Fill(box, new Color(0.02f, 0.1f, 0.25f, 0.97f)); }
            UiKit.Outline(box, new Color(0f, 0.9f, 1f, 0.9f));
            UiKit.Label(CcUi.ChaletSmall, new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 90f), text, TextAnchor.MiddleCenter, CcUi.Cyan, true);
            var bn = CcUi.Tex("QuitButNor"); var bo = CcUi.Tex("QuitButUp");
            float by = box.yMax - 50f, bw = 120f, bh = 36f; bool press = false, cancelPress = false;
            if (cancel == null) press = CcUi.Btn(new Rect(box.center.x - bw * 0.5f, by, bw, bh), new Rect(box.center.x - bw * 0.5f, by, bw, bh), bn, bo, ok, CcUi.F14, CcUi.Cyan, true, 5);
            else
            {
                press = CcUi.Btn(new Rect(box.center.x - bw - 10f, by, bw, bh), new Rect(box.center.x - bw - 10f, by, bw, bh), bn, bo, ok, CcUi.F14, CcUi.Cyan, true, 5);
                cancelPress = CcUi.Btn(new Rect(box.center.x + 10f, by, bw, bh), new Rect(box.center.x + 10f, by, bw, bh), bn, bo, cancel, CcUi.F14, CcUi.Cyan, true, 5);
            }
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp) e.Use();   // el resto de la pantalla no recibe nada
            if (press) { Visible = false; var a = onOk; onOk = null; if (a != null) a(); }
            else if (cancelPress) { Visible = false; var a = onCancel; onCancel = null; if (a != null) a(); }
        }
    }

    public sealed class NameCreateScreen : MonoBehaviour
    {
        const float ItemH = 30f, ListW = 146f;
        static readonly float[] ColX = { 120f, 264f, 408f };

        LoginSession login; int slot; TaskCompletionSource<NameSaved> tcs; bool active, busy;
        readonly string[][] lists = new string[3][];            // 0 nombre, 1 segundo, 2 apellido (elemento 0 vacío; se usan 1..Length-1)
        bool wheelOk = true, generate = true; string own = "";
        readonly int[] cur = { 1, 1, 1 };                        // iCurFirst/Middle/LastNameIndex
        readonly float[] off = new float[3];                     // desplazamiento en píxeles de referencia (+ = contenido hacia abajo)
        readonly int[] holdDir = new int[3], settle = new int[3]; readonly float[] heldSince = new float[3];
        readonly bool[] dragging = new bool[3], returning = new bool[3]; readonly float[] lastY = new float[3], dragTotal = new float[3];
        string curFirst = "", curLast = "", completeName = "";
        readonly CcMsg msg = new CcMsg();

        /// <summary>Muestra la ventana y devuelve el NameSaved cuando el servidor reserva el nombre, o null si se pulsa EXIT.</summary>
        public Task<NameSaved> Run(LoginSession login, int slot, GameTables tables)
        {
            if (tcs != null && !tcs.Task.IsCompleted) return tcs.Task;
            this.login = login; this.slot = slot; tcs = new TaskCompletionSource<NameSaved>(); busy = false; msg.Visible = false;
            var nt = tables != null && tables.Xdt != null ? tables.Xdt.m_pNameTable : null;
            lists[0] = ToArr(nt != null ? nt.m_pFirstName : null); lists[1] = ToArr(nt != null ? nt.m_pMiddleName : null); lists[2] = ToArr(nt != null ? nt.m_pLastName : null);
            wheelOk = lists[0].Length >= 4 && lists[1].Length >= 4 && lists[2].Length >= 4;   // la rueda necesita al menos algunas entradas
            generate = wheelOk; own = "";
            for (int i = 0; i < 3; i++) { cur[i] = 1; off[i] = 0f; holdDir[i] = 0; settle[i] = 0; dragging[i] = false; returning[i] = false; }
            Recompose(); active = true; enabled = true; return tcs.Task;
        }

        static string[] ToArr(NameStringElement[] a)
        {
            if (a == null) return new string[0];
            var r = new string[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = a[i] != null && a[i].m_pstrNameString != null ? a[i].m_pstrNameString : "";
            return r;
        }

        void Finish(NameSaved r) { active = false; enabled = false; if (tcs != null) tcs.TrySetResult(r); }
        void OnDestroy() { if (tcs != null) tcs.TrySetResult(null); }

        // ---------- rueda ----------
        int Wrap(int c, int i) { int n = lists[c].Length - 1; return ((i - 1) % n + n) % n + 1; }   // anillo sobre los elementos 1..Length-1

        /// <summary>SetCompleteName del original.</summary>
        void Recompose()
        {
            if (!wheelOk) { curFirst = ""; curLast = ""; completeName = ""; return; }
            curFirst = lists[0][cur[0]];
            string mid = lists[1][cur[1]]; if (mid == " ") mid = "";
            string last = lists[2][cur[2]]; if (last == " ") last = "";
            string last2;
            if (mid.Contains(" ") || mid == "") last2 = last.Length > 0 ? char.ToUpper(last[0]) + last.Substring(1) : "";
            else last2 = last.Length > 0 ? char.ToLower(last[0]) + last.Substring(1) : "";
            curLast = mid + last2; completeName = curFirst + " " + curLast;
        }

        /// <summary>MakeRandomName: índices aleatorios (nombre 1..L-2, segundo/apellido 2..L-2). En el original no consulta al servidor (RandomNameSucc local).</summary>
        void MakeRandom()
        {
            if (!wheelOk) return;
            int a = lists[0].Length, b = lists[1].Length, c = lists[2].Length;
            cur[0] = UnityEngine.Random.Range(1, a - 1); cur[1] = UnityEngine.Random.Range(2, b - 1); cur[2] = UnityEngine.Random.Range(2, c - 1);
            for (int i = 0; i < 3; i++) { off[i] = 0f; settle[i] = 0; returning[i] = false; }
            Recompose();
        }

        void Update()
        {
            if (!active || !wheelOk) return;
            float dt = Time.unscaledDeltaTime;
            for (int c = 0; c < 3; c++) Step(c, dt);
        }

        void Step(int c, float dt)
        {
            if (dragging[c]) return;
            if (returning[c]) { off[c] = Mathf.MoveTowards(off[c], 0f, Mathf.Max(1f, dt * 300f)); if (Mathf.Abs(off[c]) < 0.01f) { off[c] = 0f; returning[c] = false; } return; }
            int dir = holdDir[c];
            if (dir != 0 && settle[c] == 0) heldSince[c] = Time.realtimeSinceStartup;
            if (dir != 0) settle[c] = dir;
            if (settle[c] == 0) return;
            // velocidad del original: dt * clamp(tiempo pulsado, 1, 3) * (1.5 * 100) píxeles por cuadro
            float amt = dt * Mathf.Clamp(Time.realtimeSinceStartup - heldSince[c], 1f, 3f) * 150f;
            off[c] += settle[c] * amt;
            if (settle[c] > 0 && off[c] >= ItemH) { cur[c] = Wrap(c, cur[c] - 1); off[c] = 0f; Recompose(); if (holdDir[c] == 0) settle[c] = 0; }
            else if (settle[c] < 0 && off[c] <= -ItemH) { cur[c] = Wrap(c, cur[c] + 1); off[c] = 0f; Recompose(); if (holdDir[c] == 0) settle[c] = 0; }
        }

        // ---------- dibujo ----------
        void OnGUI()
        {
            if (!active) return;
            GUI.depth = -10;
            CcUi.Background();
            UiKit.BeginCenter();
            try { DrawWindow(); msg.Draw(); } finally { UiKit.End(); }
        }

        void DrawWindow()
        {
            float ox = -336f, oy = -182.5f; var e = Event.current;
            bool en = !busy && !msg.Visible;
            Func<float, float, float, float, Rect> W = (x, y, w, h) => new Rect(ox + x, oy + y, w, h);
            var tex = UiKit.Tex("CCNameBG");
            if (tex != null) UiKit.Slice(W(0, 0, 672, 365), tex, 5, 5, 0, 0); else UiKit.Fill(W(0, 0, 672, 365), new Color(0.02f, 0.1f, 0.25f, 0.97f));
            UiKit.Label(CcUi.F14, W(5, 5, 250, 20), "CHARACTER CREATION", TextAnchor.MiddleLeft, CcUi.Cyan);

            // pestañas: el botón es el de la pestaña inactiva; la activa es el panel con su etiqueta (TabText)
            var tb = UiKit.Tex("CCNameTabButtonNormal"); var tbo = UiKit.Tex("CCNameTabButtonOver");
            if (generate)
            {
                var r = W(354, 31, 231, 26);
                if (CcUi.Btn(r, CcUi.Pad(r, 231, 36), tb, tbo, "CREATE YOUR OWN", CcUi.F14, CcUi.Grey, en, 0)) generate = false;
            }
            else if (wheelOk)
            {
                var r = W(90, 33, 231, 26);
                if (CcUi.Btn(r, CcUi.Pad(r, 231, 36), tb, tbo, "GENERATE A NAME", CcUi.F14, CcUi.Grey, en, 0)) generate = true;
            }
            UiKit.Label(CcUi.F14, W(220, 30, 231, 26), "-OR-", TextAnchor.MiddleCenter, CcUi.Cyan2);

            if (generate) DrawGenerateTab(W, en); else DrawOwnTab(W, en);

            string foot = generate ? "Names must have at least two parts.\nSingle-word names will not be accepted."
                                   : "Names must have at least two parts.\nCustom first names cannot exceed 8 characters.\nCustom last names cannot exceed 16 characters.";
            UiKit.Label(CcUi.ChaletSmall, generate ? W(200, 316, 270, 40) : W(100, 316, 420, 40), foot, TextAnchor.MiddleCenter, CcUi.Cyan, true);

            var qn = UiKit.Tex("QuitButNor"); var qo = UiKit.Tex("QuitButUp");
            var rx = W(9, 323, 68, 29);
            if (CcUi.Btn(rx, CcUi.Pad(rx, 68, 36), qn, qo, "EXIT", CcUi.F14, CcUi.Cyan, en, 5)) AskExit();
            var rc = W(525, 322, 135, 29);
            if (CcUi.Btn(rc, CcUi.Pad(rc, 135, 36), UiKit.Tex("CSEnterButton"), UiKit.Tex("CSEnterGameOver"), "CONTINUE", CcUi.F14, Color.white, en, 0)) OnContinue();

            if (busy) UiKit.Label(CcUi.F14, W(0, 290, 672, 22), "CHECKING NAME...", TextAnchor.MiddleCenter, CcUi.Yellow);
            if (en && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { AskExit(); e.Use(); }   // botón Atrás de Android
        }

        void DrawGenerateTab(Func<float, float, float, float, Rect> W, bool en)
        {
            var area = UiKit.Tex("CCNameTab1"); var ar = W(0, 32, 671, 285);
            if (area != null) UiKit.Slice(ar, area, 5, 5, 21, 10); else UiKit.Fill(ar, new Color(0f, 0.08f, 0.2f, 0.9f));
            UiKit.Label(CcUi.F14, W(90, 31, 231, 26), "GENERATE A NAME", TextAnchor.MiddleCenter, CcUi.Grey);
            var plate = UiKit.Tex("CCNameDisplay"); var nr = W(76, 65, 524, 30);
            if (plate != null) UiKit.Slice(nr, plate, 5, 5, 5, 5);   // fondo del NameDisplay (TEXTO ASUMIDO que el estilo lo lleva)
            UiKit.Label(CcUi.F16, nr, completeName, TextAnchor.MiddleCenter, CcUi.Yellow);
            UiKit.Img(W(119, 124, 435, 156), UiKit.Tex("CCScrollBG"));
            for (int c = 0; c < 3; c++) DrawColumn(c, W, en);
            UiKit.Img(W(121, 127, 432, 150), UiKit.Tex("CCNameShade"));
            var rr = W(567, 192, 92, 20);
            if (CcUi.Btn(rr, CcUi.Pad(rr, 92, 36), UiKit.Tex("QuitButNor"), UiKit.Tex("QuitButUp"), "RANDOM", CcUi.F14, CcUi.Grey, en, 5)) MakeRandom();
        }

        void DrawColumn(int c, Func<float, float, float, float, Rect> W, bool en)
        {
            var e = Event.current;
            var up = W(ColX[c], 112, 147, 16); var dn = W(ColX[c], 112 + 164, 147, 16);
            var list = W(ColX[c], 112 + 16, ListW, 5 * ItemH);
            // botones (RepeatButton): arriba mueve el contenido hacia abajo (+), abajo hacia arriba (-); área táctil de 36
            bool hu = CcUi.Held(up, new Rect(up.x, up.y - 10f, up.width, 36f), UiKit.Tex("CCScrollUpNormal"), UiKit.Tex("CCScrollUpOver"), en);
            bool hd = CcUi.Held(dn, new Rect(dn.x, dn.yMax - 26f, dn.width, 36f), UiKit.Tex("CCScrollDownUp"), UiKit.Tex("CCScrollDownOver"), en);
            holdDir[c] = hu ? 1 : (hd ? -1 : 0);

            // arrastre táctil sobre la lista
            int id = GUIUtility.GetControlID(FocusType.Passive); var mp = e.mousePosition;
            if (en)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && list.Contains(mp) && GUIUtility.hotControl == 0)
                { GUIUtility.hotControl = id; dragging[c] = true; lastY[c] = mp.y; dragTotal[c] = 0f; settle[c] = 0; returning[c] = false; e.Use(); }
                else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id)
                {
                    float d = mp.y - lastY[c]; lastY[c] = mp.y; dragTotal[c] += Mathf.Abs(d); off[c] += d;
                    while (off[c] >= ItemH) { cur[c] = Wrap(c, cur[c] - 1); off[c] -= ItemH; }
                    while (off[c] <= -ItemH) { cur[c] = Wrap(c, cur[c] + 1); off[c] += ItemH; }
                    Recompose(); e.Use();
                }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0; dragging[c] = false;
                    if (dragTotal[c] < 6f)
                    {   // toque sin arrastre: salta a la fila tocada (-2..2 respecto a la central)
                        int k = Mathf.FloorToInt((mp.y - list.y - 2f * ItemH) / ItemH);
                        if (k != 0 && k >= -2 && k <= 2) { cur[c] = Wrap(c, cur[c] + k); off[c] = 0f; Recompose(); }
                        else returning[c] = off[c] != 0f;
                    }
                    else if (Mathf.Abs(off[c]) >= ItemH * 0.5f) { settle[c] = off[c] > 0f ? 1 : -1; heldSince[c] = Time.realtimeSinceStartup; }
                    else returning[c] = off[c] != 0f;
                    e.Use();
                }
            }

            // lista con recorte: 7 filas (buffers incluidos); la fila central (y = 60) es la elegida
            if (e.type == EventType.Repaint)
            {
                GUI.BeginGroup(list);
                for (int k = -3; k <= 3; k++)
                {
                    string t = lists[c][Wrap(c, cur[c] + k)]; if (t == " ") t = "<BLANK>";
                    UiKit.Label(CcUi.F16, new Rect(0f, 2f * ItemH + k * ItemH + off[c], ListW, ItemH), t, TextAnchor.MiddleCenter, CcUi.Cyan);
                }
                GUI.EndGroup();
            }
        }

        void DrawOwnTab(Func<float, float, float, float, Rect> W, bool en)
        {
            var area = UiKit.Tex("CCNameTab2"); var ar = W(0, 30, 671, 286);
            if (area != null) UiKit.Slice(ar, area, 5, 5, 21, 10); else UiKit.Fill(ar, new Color(0f, 0.08f, 0.2f, 0.9f));
            UiKit.Label(CcUi.F14, W(354, 31, 231, 26), "CREATE YOUR OWN", TextAnchor.MiddleCenter, CcUi.Grey);
            UiKit.Label(CcUi.F14, W(168, 103, 341, 25), "WANT TO CREATE YOUR OWN NAME?", TextAnchor.MiddleCenter, CcUi.Cyan2);
            UiKit.Label(CcUi.ChaletSmall, W(142, 139, 396, 58), "Type in the name you wish to use and \n click 'CONTINUE'. Your custom name\n will be submitted for review.", TextAnchor.MiddleCenter, CcUi.Cyan, true);
            bool prev = GUI.enabled; GUI.enabled = prev && en;
            own = UiKit.Field(W(146, 217, 383, 28), own, 26, "own", false, 16);
            GUI.enabled = prev;
            if (en && UiKit.EnterPressed()) OnContinue();
        }

        // ---------- lógica ----------
        void AskExit() { msg.Confirm("Would you like to exit Character Creation?", () => Finish(null)); }   // msg 198 (texto de SystemMessage)

        /// <summary>Botón CONTINUE: valida igual que el original y lanza CHECK -> SAVE.</summary>
        void OnContinue()
        {
            if (busy || msg.Visible) return;
            Debug.Log("[character creation] stage=validate_name mode=" + (generate ? "wheel" : "custom"));
            if (generate)
            {
                if (completeName == "") { msg.Show("You must indicate a name to continue."); return; }                 // msg 199
                if (curLast == "") { msg.Show("SORRY! \nNames must have at least two parts. Single-word names will not be accepted. "); return; }       // msg 200
                Submit(curFirst, curLast, cur[0], cur[1], cur[2], true);
            }
            else
            {
                string[] parts = (own ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) { msg.Show("You must indicate a name to continue."); return; }                                              // msg 199
                if (parts.Length == 1) { msg.Show("You must indicate a first name and a last name to continue."); return; }   // msg 202
                if (parts[0].Length > 8) { msg.Show("Custom first names cannot exceed eight characters."); return; }
                string first = parts[0], last = string.Join(" ", parts, 1, parts.Length - 1);
                if (last.Length > 16) { msg.Show("Custom last names cannot exceed sixteen characters."); return; }
                if (IsWheelName(first, last)) { msg.Show("That name cannot be used. \nPlease try another character name."); return; }   // msg 192
                Submit(first, last, 0, 0, 0, false);
            }
        }

        /// <summary>CheckNameList del original: un nombre libre que coincide con una combinación de la rueda se rechaza (versión corregida: el original compara con Substring(len-1)).</summary>
        bool IsWheelName(string first, string last)
        {
            if (!wheelOk) return false;
            string f = first.ToLowerInvariant(), l = last.ToLowerInvariant();
            for (int i = 1; i < lists[0].Length; i++)
            {
                if (lists[0][i].ToLowerInvariant() != f) continue;
                for (int j = 1; j < lists[1].Length; j++)
                {
                    string m = lists[1][j].Trim().ToLowerInvariant();
                    if (m.Length > l.Length || !l.StartsWith(m, StringComparison.Ordinal)) continue;
                    string rest = l.Substring(m.Length);
                    for (int k = 1; k < lists[2].Length; k++) if (lists[2][k].Trim().ToLowerInvariant() == rest) return true;
                }
            }
            return false;
        }

        async void Submit(string first, string last, int fn, int mn, int ln, bool wheel)
        {
            busy = true;
            try
            {
                string[] ok = await login.CheckNameAsync(fn, ln, mn, first, last);                       // REQ_CHECK_CHAR_NAME{iFNCode,iLNCode,iMNCode,...}
                string f2 = ok != null && ok.Length > 0 && !string.IsNullOrEmpty(ok[0]) ? ok[0] : first;
                string l2 = ok != null && ok.Length > 1 && !string.IsNullOrEmpty(ok[1]) ? ok[1] : last;
                NameSaved ns = await login.SaveNameAsync(slot, 1, fn, ln, mn, f2, l2);                    // REQ_SAVE_CHAR_NAME{..., iGender = 1, iSlotNum}
                busy = false;
                if (!wheel)
                    msg.Show("THANKS! \nThe name you submitted is now being reviewed. While we are reviewing this name, you have been assigned a temporary name: Player " + ns.Uid, () => Finish(ns));   // msg 189
                else Finish(ns);
            }
            catch (LoginException le) { busy = false; Debug.LogWarning("[character creation] stage=" + le.Stage + " code=" + le.Code); msg.Show(ErrorText(le)); }
            catch (Exception ex) { busy = false; Debug.LogWarning("[character creation] stage=name_error type=" + ex.GetType().Name); msg.Show("Name check error.\n" + ex.Message); }
        }

        static string ErrorText(LoginException le)
        {
            if (le.Stage == "save_slot") return "The server could not reserve this character slot. Return to character selection and try again. (" + le.Code + ")";
            if (le.Stage == "save_name")
            {
                if (le.Code == 1) return "Sorry!\nCustom names are not yet available.";
                if (le.Code == 2) return "That name is taken. \nPlease try another character name.";   // msg 187
                return "Name check error. (" + le.Code + ")";
            }
            switch (le.Code)
            {
                case 1: return "That name is taken. \nPlease try another character name.";              // msg 187
                case 4: return "ERROR! \nYour custom name contains one or more unsupported characters. Please make corrections and submit a new name.";                // msg 228
                case 5: return "The name you previously submitted was not acceptable. Please choose another name.";                // msg 229
                default: return "Name check error. (" + le.Code + ")";
            }
        }
    }
}
