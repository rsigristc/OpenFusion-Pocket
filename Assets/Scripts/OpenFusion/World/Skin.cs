// GUISkin del original (Resources/OpenFusion/UI/Data/bindings.json): estilos con fondo normal/hover/activo, borde de 9 porciones, relleno, fuente de mapa de bits y color de texto.
// Uso: var st = Skin.Get(Skin.Menu, "RedButton"); if (Skin.Button(rect, "TEXTO", st)) ...   Las coordenadas son las de GUI (el llamador aplica la matriz/escala).
// SIN COMPILAR EN UNITY.
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    public sealed class SkinStyle
    {
        public string Name; public Texture2D N, H, A, ON; public int bL, bR, bT, bB, pL, pR, pT, pB; public BitmapFont Font; public TextAnchor Align = TextAnchor.MiddleCenter;
        public Color cN = Color.white, cH = Color.white, cA = Color.white; public bool Wrap; public float FixedW, FixedH; public Vector2 Offset;
        public RectOffset Padding { get { return new RectOffset(pL, pR, pT, pB); } }
        public bool HasBorder { get { return bL + bR + bT + bB > 0; } }
    }

    public static class Skin
    {
        // path_id de los GUISkin (bindings.json)
        public const int Nano = 1379, Option = 1365, Inven = 1366, Menu = 1380, Chat = 1368, Journal = 1370, JournalL = 1388, Map = 1373;
        static List<object> root; static readonly Dictionary<string, SkinStyle> cache = new Dictionary<string, SkinStyle>();
        static readonly Dictionary<string, BitmapFont> fonts = new Dictionary<string, BitmapFont>();
        static readonly Dictionary<string, string> fontFile = new Dictionary<string, string> {
            { "JEFFE___06", "JEFFE___06__970" }, { "JEFFE___08", "JEFFE___08__948" }, { "JEFFE___12", "JEFFE___12__953" }, { "JEFFE___13", "JEFFE___13__934" }, { "JEFFE___14", "JEFFE___14__903" },
            { "JEFFE___16", "JEFFE___16__1012" }, { "JEFFE___24", "JEFFE___24__1101" }, { "JEFFE___40", "JEFFE___40__905" }, { "JEFFE___72", "JEFFE___72__1129" },
            { "ChaletBook-Regular", "ChaletBook-Regular__1115" }, { "ChaletBook-Regular Small", "ChaletBook-Regular_Small__1018" }, { "ChaletBook-Regular Small 1", "ChaletBook-Regular_Small_1__1119" },
            { "Comic Sans MS Bold", "Comic_Sans_MS_Bold__1139" } };

        public static BitmapFont Font(string name)
        {
            if (string.IsNullOrEmpty(name)) return null; if (name.StartsWith("Font:")) name = name.Substring(5);
            BitmapFont f; if (fonts.TryGetValue(name, out f)) return f; string file; f = UiKit.Font(fontFile.TryGetValue(name, out file) ? file : name); fonts[name] = f; return f;
        }

        static void Load()
        {
            if (root != null) return; root = new List<object>();
            var ta = Resources.Load<TextAsset>("OpenFusion/UI/Data/bindings"); if (ta == null) { Debug.LogWarning("[skin] falta bindings.json"); return; }
            var p = MiniJson.Parse(ta.text) as List<object>; if (p != null) root = p;
        }
        static Dictionary<string, object> D(object o) { return o as Dictionary<string, object>; }
        static int I(object o) { return o is double ? (int)(double)o : 0; }
        static float F(object o) { return o is double ? (float)(double)o : 0f; }
        static Texture2D Tx(object st)
        {
            var d = D(st); if (d == null) return null; var s = d.ContainsKey("m_Background") ? d["m_Background"] as string : null; if (string.IsNullOrEmpty(s)) return null;
            int i = s.LastIndexOf('/'); if (i >= 0) s = s.Substring(i + 1); if (s.EndsWith(".png")) s = s.Substring(0, s.Length - 4); return UiKit.Tex(s);
        }
        static Color Col(object st, Color def)
        {
            var d = D(st); if (d == null || !d.ContainsKey("m_TextColor")) return def; var c = D(d["m_TextColor"]); if (c == null) return def; return new Color(F(c["r"]), F(c["g"]), F(c["b"]), F(c["a"]));
        }

        /// Estilo por nombre dentro de un GUISkin (customStyles) o uno de los integrados: "button", "label", "box", "toggle", "textField", "window", "horizontalSlider", "horizontalSliderThumb", "scrollView", ...
        public static SkinStyle Get(int skinId, string name)
        {
            string key = skinId + "/" + name; SkinStyle r; if (cache.TryGetValue(key, out r)) return r;
            Load(); Dictionary<string, object> found = null; string skinFont = null;
            foreach (var e in root)
            {
                var d = D(e); if (d == null || I(d["path_id"]) != skinId) continue; var f = D(d["fields"]); if (f == null) continue;
                var cs = f.ContainsKey("customStyles") ? f["customStyles"] as List<object> : null;
                if (cs != null) foreach (var s in cs) { var sd = D(s); if (sd != null && string.Equals(sd["m_Name"] as string, name, System.StringComparison.OrdinalIgnoreCase)) { found = sd; break; } }
                if (found == null && f.ContainsKey("m_" + name)) found = D(f["m_" + name]);
                if (found == null && name.Length > 0) { string lf = char.ToLowerInvariant(name[0]) + name.Substring(1); if (f.ContainsKey("m_" + lf)) found = D(f["m_" + lf]); }
                skinFont = f.ContainsKey("m_Font") ? f["m_Font"] as string : null;
                break;
            }
            if (found == null) { Debug.LogWarning("[skin] estilo no encontrado " + key); cache[key] = null; return null; }
            r = new SkinStyle { Name = name };
            r.N = Tx(found["m_Normal"]); r.H = Tx(found["m_Hover"]); r.A = Tx(found["m_Active"]); r.ON = found.ContainsKey("m_OnNormal") ? Tx(found["m_OnNormal"]) : null;
            r.cN = Col(found["m_Normal"], Color.white); r.cH = Col(found["m_Hover"], r.cN); r.cA = Col(found["m_Active"], r.cN);
            var b = D(found["m_Border"]); if (b != null) { r.bL = I(b["m_Left"]); r.bR = I(b["m_Right"]); r.bT = I(b["m_Top"]); r.bB = I(b["m_Bottom"]); }
            var pd = D(found["m_Padding"]); if (pd != null) { r.pL = I(pd["m_Left"]); r.pR = I(pd["m_Right"]); r.pT = I(pd["m_Top"]); r.pB = I(pd["m_Bottom"]); }
            { string fn = found.ContainsKey("m_Font") ? found["m_Font"] as string : null; if (string.IsNullOrEmpty(fn) || fn == "None" || fn == "Font:None") fn = skinFont; r.Font = Font(fn); }
             r.Align = (TextAnchor)I(found.ContainsKey("m_Alignment") ? found["m_Alignment"] : 4.0);
            r.Wrap = found.ContainsKey("m_WordWrap") && I(found["m_WordWrap"]) != 0; r.FixedW = F(found.ContainsKey("m_FixedWidth") ? found["m_FixedWidth"] : 0.0); r.FixedH = F(found.ContainsKey("m_FixedHeight") ? found["m_FixedHeight"] : 0.0);
            var co = found.ContainsKey("m_ContentOffset") ? D(found["m_ContentOffset"]) : null; if (co != null) r.Offset = new Vector2(F(co["x"]), F(co["y"]));
            cache[key] = r; return r;
        }

        // ---- dibujo ----
        public static void Bg(Rect r, Texture2D t, SkinStyle s)
        {
            if (t == null || Event.current.type != EventType.Repaint) return;
            if (s != null && s.HasBorder) UiKit.Slice(r, t, s.bL, s.bR, s.bT, s.bB); else GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
        }
        /// GUI.Box / GUI.Label con estilo (fondo normal + texto).
        public static void Box(Rect r, SkinStyle s, string text = null)
        {
            if (s == null) return; Bg(r, s.N, s);
            if (!string.IsNullOrEmpty(text) && s.Font != null && Event.current.type == EventType.Repaint) s.Font.Draw(r, text, s.Align, s.Wrap, s.cN, s.Padding);
        }
        public static void Label(Rect r, string text, SkinStyle s, Color? color = null)
        {
            if (s == null || string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            if (s.Font != null) s.Font.Draw(r, text, s.Align, s.Wrap, (color ?? s.cN), s.Padding);
        }
        /// GUI.Button con estilo; deshabilitado se dibuja con alfa 0,5 (GUI.enabled = false). Devuelve true al soltar dentro.
        public static bool Button(Rect r, string text, SkinStyle s, bool enabled = true, bool silent = false)
        {
            if (s == null) return false; int id = GUIUtility.GetControlID(FocusType.Passive, r); var e = Event.current; bool hov = r.Contains(e.mousePosition), res = false;
            if (enabled)
            {
                if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; if (hov) { res = true; if (!silent) Sfx.Button(); } e.Use(); }
            }
            if (e.type == EventType.Repaint)
            {
                var old = GUI.color; if (!enabled) GUI.color = new Color(old.r, old.g, old.b, old.a * 0.5f);
                bool act = enabled && hov && GUIUtility.hotControl == id; bool over = enabled && hov && GUIUtility.hotControl == 0;
                var t = act ? (s.A != null ? s.A : s.N) : over ? (s.H != null ? s.H : s.N) : s.N; Bg(r, t, s);
                if (!string.IsNullOrEmpty(text) && s.Font != null) { var c = act ? s.cA : over ? s.cH : s.cN; var rr = new Rect(r.x + s.Offset.x, r.y + s.Offset.y, r.width, r.height); s.Font.Draw(rr, text, s.Align, s.Wrap, c, s.Padding); }
                GUI.color = old;
            }
            return res;
        }
    }
}
