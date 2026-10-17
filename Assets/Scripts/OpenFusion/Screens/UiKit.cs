// Utilidades comunes de las pantallas (launcher, carga, selección y creación). IMGUI con escala = Screen.height / 681 como el cliente original.
// Convención: cada pantalla llama a UiKit.Begin*(…) en OnGUI, dibuja en coordenadas de REFERENCIA (altura 681) relativas a la esquina/centro elegido y llama a UiKit.End().
// SIN COMPILAR EN UNITY.
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.World;

namespace OpenFusion.Screens
{
    public static class UiKit
    {
        public const float RefH = 681f;
        public const string TexPath = "OpenFusion/UI/Textures/";
        public static float S { get { return Mathf.Max(0.1f, Screen.height / RefH); } }
        /// <summary>Tamaño de la pantalla en unidades de referencia.</summary>
        public static Vector2 RefSize { get { return new Vector2(Screen.width / S, Screen.height / S); } }
        public static Texture2D White { get { return Texture2D.whiteTexture; } }

        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, BitmapFont> fontCache = new Dictionary<string, BitmapFont>();
        /// <summary>Textura de Resources/OpenFusion/UI/Textures/&lt;name&gt; (sin extensión). null si no existe (se avisa una vez).</summary>
        public static Texture2D Tex(string name) { return Load(TexPath + name, name); }
        /// <summary>Textura por ruta de Resources completa (sin extensión).</summary>
        public static Texture2D TexPath_(string resPath) { return Load(resPath, resPath); }
        static Texture2D Load(string path, string key)
        {
            Texture2D t; if (texCache.TryGetValue(key, out t)) return t;
            t = Resources.Load<Texture2D>(path); if (t == null) Debug.LogWarning("[ui] falta la textura " + path); else { t.wrapMode = TextureWrapMode.Clamp; }
            texCache[key] = t; return t;
        }
        /// <summary>Fuente de mapa de bits (nombre del JSON en Resources/OpenFusion/UI/Data/Fonts, p. ej. "JEFFE___14__903").</summary>
        public static BitmapFont Font(string jsonName)
        {
            BitmapFont f; if (fontCache.TryGetValue(jsonName, out f)) return f;
            try { f = BitmapFontLoader.Load(jsonName); } catch (System.Exception e) { Debug.LogWarning("[ui] fuente " + jsonName + ": " + e.Message); f = null; }
            fontCache[jsonName] = f; return f;
        }

        // ---- espacio de dibujo ----
        static readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        static void Push(Matrix4x4 m) { stack.Push(GUI.matrix); GUI.matrix = m; }
        /// <summary>Origen (0,0) = centro de la pantalla; x derecha, y abajo. Un rect centrado de 370x275 es Rect(-185,-137.5,370,275).</summary>
        public static void BeginCenter() { Push(Matrix4x4.TRS(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f), Quaternion.identity, new Vector3(S, S, 1f))); }
        /// <summary>Origen = esquina superior izquierda.</summary>
        public static void BeginTopLeft() { Push(Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(S, S, 1f))); }
        /// <summary>Origen = esquina superior derecha (x negativas hacia la izquierda: el panel de la mini barra de carga es Rect(-160,361,150,30)).</summary>
        public static void BeginTopRight() { Push(Matrix4x4.TRS(new Vector3(Screen.width, 0f, 0f), Quaternion.identity, new Vector3(S, S, 1f))); }
        /// <summary>Origen = esquina inferior izquierda (y negativas hacia arriba).</summary>
        public static void BeginBottomLeft() { Push(Matrix4x4.TRS(new Vector3(0f, Screen.height, 0f), Quaternion.identity, new Vector3(S, S, 1f))); }
        public static void BeginBottomRight() { Push(Matrix4x4.TRS(new Vector3(Screen.width, Screen.height, 0f), Quaternion.identity, new Vector3(S, S, 1f))); }
        /// <summary>Sin escala (píxeles de pantalla).</summary>
        public static void BeginPixels() { Push(Matrix4x4.identity); }
        public static void End() { if (stack.Count > 0) GUI.matrix = stack.Pop(); }

        // ---- dibujo ----
        public static void Img(Rect r, Texture t, ScaleMode mode = ScaleMode.StretchToFill) { if (t != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(r, t, mode, true); }
        public static void Img(Rect r, Texture t, Color tint, ScaleMode mode = ScaleMode.StretchToFill) { if (t == null || Event.current.type != EventType.Repaint) return; var o = GUI.color; GUI.color = tint * o; GUI.DrawTexture(r, t, mode, true); GUI.color = o; }
        public static void Fill(Rect r, Color c) { if (Event.current.type != EventType.Repaint) return; var o = GUI.color; GUI.color = c * o; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o; }
        public static void Outline(Rect r, Color c, float w = 1f) { Fill(new Rect(r.x, r.y, r.width, w), c); Fill(new Rect(r.x, r.yMax - w, r.width, w), c); Fill(new Rect(r.x, r.y, w, r.height), c); Fill(new Rect(r.xMax - w, r.y, w, r.height), c); }
        /// <summary>Rellena cubriendo todo el rect recortando lo que sobra (ScaleAndCrop del original).</summary>
        public static void Cover(Rect r, Texture t) { if (t == null || Event.current.type != EventType.Repaint) return; GUI.DrawTexture(r, t, ScaleMode.ScaleAndCrop, true); }
        /// <summary>Nine-slice con bordes L,R,T,B en píxeles de la textura (GUIStyle.border del original).</summary>
        public static void Slice(Rect r, Texture2D t, int L, int R, int T, int B)
        {
            if (t == null || Event.current.type != EventType.Repaint) return;
            float w = t.width, h = t.height; float xs0 = 0, xs1 = L, xs2 = w - R, xs3 = w; float ys0 = 0, ys1 = B, ys2 = h - T, ys3 = h;   // UV con origen abajo-izquierda: B = borde inferior, T = superior
            float[] dx = { r.x, r.x + L, r.xMax - R, r.xMax }; float[] dy = { r.y, r.y + T, r.yMax - B, r.yMax };
            float[] ux = { xs0 / w, xs1 / w, xs2 / w, xs3 / w }; float[] uy = { ys3 / h, ys2 / h, ys1 / h, ys0 / h };   // fila superior de pantalla = parte alta de la textura
            for (int j = 0; j < 3; j++) for (int i = 0; i < 3; i++)
            {
                var dr = Rect.MinMaxRect(dx[i], dy[j], dx[i + 1], dy[j + 1]); if (dr.width <= 0f || dr.height <= 0f) continue;
                var uv = Rect.MinMaxRect(ux[i], uy[j + 1], ux[i + 1], uy[j]); GUI.DrawTextureWithTexCoords(dr, t, uv, true);
            }
        }
        public static bool Hover(Rect r) { return r.Contains(Event.current.mousePosition); }

        /// <summary>Texto con fuente de mapa de bits (sin envoltura salvo wrap=true). Si la fuente no carga, usa la fuente de GUI.</summary>
        public static void Label(BitmapFont f, Rect r, string text, TextAnchor a, Color c, bool wrap = false)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            if (f != null) { f.Draw(r, text, a, wrap, c); return; }
            var st = FallbackStyle; st.alignment = a; st.wordWrap = wrap; st.normal.textColor = c; st.hover.textColor = c; st.active.textColor = c; st.focused.textColor = c; st.onNormal.textColor = c; st.onHover.textColor = c; st.onActive.textColor = c; st.onFocused.textColor = c; GUI.Label(r, text, st);
        }
        static GUIStyle fallback; static GUIStyle FallbackStyle { get { if (fallback == null) { fallback = new GUIStyle(GUI.skin.label) { fontSize = 14 }; } return fallback; } }

        /// <summary>Botón con textura normal/hover/pulsada; el texto va centrado. Devuelve true al soltar el botón del ratón / el dedo dentro del rect.</summary>
        public static bool Button(Rect r, Texture2D normal, Texture2D over, string label = null, BitmapFont f = null, Color? textColor = null, bool enabled = true)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, r); var e = Event.current; bool hov = r.Contains(e.mousePosition), res = false;
            if (enabled)
            {
                if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; if (hov) { res = true; Sfx.Button(); } e.Use(); }
            }
            if (e.type == EventType.Repaint)
            {
                var t = enabled && (hov || GUIUtility.hotControl == id) && over != null ? over : normal; if (t != null) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
                if (!string.IsNullOrEmpty(label)) Label(f, r, label, TextAnchor.MiddleCenter, (textColor ?? Color.white) * (enabled ? Color.white : new Color(1, 1, 1, 0.5f)));
            }
            return res;
        }
        /// <summary>Botón que se mantiene mientras está pulsado (RepeatButton: rotar el personaje, etc.).</summary>
        public static bool Held(Rect r, Texture2D normal, Texture2D over)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, r); var e = Event.current; bool hov = r.Contains(e.mousePosition);
            if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
            bool held = GUIUtility.hotControl == id;
            if (e.type == EventType.Repaint) { var t = (hov || held) && over != null ? over : normal; if (t != null) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); }
            return held;
        }

        /// <summary>Campo de texto (nombre del control para el foco). El tamaño de fuente va en unidades de referencia.</summary>
        public static string Field(Rect r, string value, int maxLen, string controlName, bool password = false, int fontSize = 15)
        {
            var st = new GUIStyle(GUI.skin.textField) { fontSize = fontSize, alignment = TextAnchor.MiddleLeft }; st.normal.textColor = Color.white; st.focused.textColor = Color.white; st.hover.textColor = Color.white;
            GUI.SetNextControlName(controlName);
            return password ? GUI.PasswordField(r, value ?? "", '*', maxLen, st) : GUI.TextField(r, value ?? "", maxLen, st);
        }
        public static bool EnterPressed() { var e = Event.current; return e.type == EventType.KeyUp && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter); }

        // ---- render de personajes 3D a RenderTexture (misma técnica que PlayerHud/NanoModelRt: SubmitRenderRequest bajo URP) ----
        public static void RenderCam(Camera cam, RenderTexture rt)
        {
            if (cam == null || rt == null) return;
            cam.targetTexture = rt;
            try { var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest(); req.destination = rt; if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render(); }
            catch (System.Exception ex) { Debug.LogWarning("[ui] render: " + ex.Message); }
            cam.targetTexture = null;
        }
        public static RenderTexture NewRt(int w, int h) { var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 }; rt.Create(); return rt; }
    }
}
