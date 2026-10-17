// BitmapFontLoader: reconstruye en tiempo de ejecucion las 24 fuentes de mapa de bits del cliente original (Unity Web Player 2.5, "Font" con m_CharacterRects)
// a partir de Resources/OpenFusion/UI/Data/Fonts/<json>.json (TextAsset) + atlas Resources/OpenFusion/UI/Textures/<m_Texture>.png.
//
// Dos caminos:
//  1) BitmapFont.Draw(...)  -> dibuja el texto con GUI.DrawTextureWithTexCoords usando EXACTAMENTE la maquetacion original (ancho, kerning por pares,
//     m_ConvertCase, interlineado m_LineSpacing, ajuste de linea por palabras). Es el camino determinista; NpcUI lo usa. Validado en Python (preview_font.py).
//  2) BitmapFontLoader.BuildUnityFont(...) -> UnityEngine.Font con font.characterInfo + font.material para usar en GUIStyle.font / UGUI Text.
//     Limitaciones conocidas (ver REPORT.md): CharacterInfo.advance es int (se pierde el kerning de pares) y la linea base la decide Unity.
//
// Datos del JSON (Unity serializa Font asi):
//   m_CharacterRects[i] = { index (codepoint), uv{x,y,width,height} (origen ABAJO-izquierda, normalizado),
//                           vert{x,y,width,height} (px; x = desplazamiento, y = distancia NEGATIVA desde el borde superior de la linea hasta el borde superior del glifo,
//                           height NEGATIVA = alto del glifo), width (avance en px) }
//   m_LineSpacing (alto de linea en px), m_Kerning (multiplicador del avance, siempre 1), m_KerningValues = [[[a,b],delta],...] (px, normalmente negativos),
//   m_ConvertCase (0 nada, 1 MAYUSCULAS, 2 minusculas), m_Texture ("tex:Textures/font_Texture__257.png").
// Orientacion: las copias del proyecto ya estan volteadas (PNG derecho) => el origen V es el mismo que en el original: NO hay que recalcular V.
// Si cargaras el PNG crudo (boca abajo) pasa atlasUpsideDown=true: se samplea con V' = 1 - V (y alto negativo para que el glifo salga derecho).
// SIN COMPILAR EN UNITY (compilado contra stubs en C# 9 con dotnet para comprobar sintaxis).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class BitmapFont
    {
        public struct Glyph
        {
            public float U, V, UW, VH;     // uv (origen abajo-izquierda) tal cual el JSON
            public float X, Y, W, H;       // vert: X offset, Y = distancia hacia ABAJO desde el borde superior de la linea hasta el borde superior del glifo (positivo), W/H tamano del quad
            public float Advance;
            public bool HasQuad;
        }

        public string Name;
        public Texture2D Atlas;
        public float LineHeight;           // m_LineSpacing
        public float KerningScale = 1f;    // m_Kerning
        public int ConvertCase;            // 0/1/2
        public bool AtlasUpsideDown;
        public float Baseline;             // px desde el borde superior de la linea hasta la linea base (= fondo de la 'H')
        public readonly Dictionary<int, Glyph> Glyphs = new Dictionary<int, Glyph>();
        readonly Dictionary<long, float> kern = new Dictionary<long, float>();
        Font unityFont;

        // ---------- construccion ----------
        public static BitmapFont FromJson(string json, Texture2D atlas, bool atlasUpsideDown)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>; if (root == null) return null;
            var f = new BitmapFont { Atlas = atlas, AtlasUpsideDown = atlasUpsideDown };
            f.Name = Str(root, "m_Name"); f.LineHeight = (float)Num(root, "m_LineSpacing", 12); f.KerningScale = (float)Num(root, "m_Kerning", 1); f.ConvertCase = (int)Num(root, "m_ConvertCase", 0);
            var rects = root.ContainsKey("m_CharacterRects") ? root["m_CharacterRects"] as List<object> : null;
            if (rects != null)
                foreach (var o in rects)
                {
                    var d = o as Dictionary<string, object>; if (d == null) continue; var uv = d["uv"] as Dictionary<string, object>; var vt = d["vert"] as Dictionary<string, object>;
                    var g = new Glyph
                    {
                        U = (float)Num(uv, "x", 0), V = (float)Num(uv, "y", 0), UW = (float)Num(uv, "width", 0), VH = (float)Num(uv, "height", 0),
                        X = (float)Num(vt, "x", 0), Y = -(float)Num(vt, "y", 0), W = (float)Num(vt, "width", 0), H = -(float)Num(vt, "height", 0), Advance = (float)Num(d, "width", 0)
                    };
                    g.HasQuad = g.UW > 0f && g.VH > 0f; f.Glyphs[(int)Num(d, "index", 0)] = g;
                }
            var kv = root.ContainsKey("m_KerningValues") ? root["m_KerningValues"] as List<object> : null;
            if (kv != null)
                foreach (var o in kv) { var pr = o as List<object>; if (pr == null || pr.Count < 2) continue; var ab = pr[0] as List<object>; if (ab == null || ab.Count < 2) continue; f.kern[Key((int)(double)ab[0], (int)(double)ab[1])] = (float)(double)pr[1]; }
            Glyph h; f.Baseline = f.Glyphs.TryGetValue('H', out h) ? h.Y + h.H : f.LineHeight * 0.8f;   // fondo de una mayuscula plana
            return f;
        }
        static long Key(int a, int b) { return ((long)a << 20) | (uint)b; }
        static string Str(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v is string ? (string)v : ""; }
        static double Num(Dictionary<string, object> d, string k, double def) { object v; return d != null && d.TryGetValue(k, out v) && v is double ? (double)v : def; }

        // ---------- metricas (identicas a preview_font.py) ----------
        public string Convert(string s) { if (string.IsNullOrEmpty(s)) return ""; s = s.Replace("\r", ""); return ConvertCase == 1 ? s.ToUpperInvariant() : ConvertCase == 2 ? s.ToLowerInvariant() : s; }
        float Adv(char c) { Glyph g; if (!Glyphs.TryGetValue(c, out g) && !Glyphs.TryGetValue(' ', out g)) return LineHeight * 0.35f; return g.Advance * KerningScale; }
        float Kern(char a, char b) { float v; return kern.TryGetValue(Key(a, b), out v) ? v * KerningScale : 0f; }
        public float Width(string s) { float w = 0f; for (int i = 0; i < s.Length; i++) { w += Adv(s[i]); if (i + 1 < s.Length) w += Kern(s[i], s[i + 1]); } return w; }
        float WidthTrim(string s) { return Width(s.TrimEnd(' ')); }

        /// <summary>Lineas resultantes (ya con m_ConvertCase). maxWidth &lt;= 0 => sin ajuste (solo saltos '\n').</summary>
        public List<string> Lines(string text, float maxWidth)
        {
            var res = new List<string>(); foreach (var para in Convert(text).Split('\n'))
            {
                if (maxWidth <= 0f) { res.Add(para); continue; }
                string line = ""; foreach (var word in para.Split(' ')) { string cand = line.Length == 0 ? word : line + " " + word; if (line.Length == 0 || Width(cand) <= maxWidth) line = cand; else { res.Add(line); line = word; } }
                res.Add(line);
            }
            return res;
        }
        /// <summary>Ancho maximo sin ajuste (equivale a GUIStyle.CalcMinMaxWidth.max sin padding).</summary>
        public float MaxLineWidth(string text) { float m = 0; foreach (var l in Lines(text, 0f)) m = Mathf.Max(m, Width(l)); return m; }
        /// <summary>Alto del texto ajustado a wrapWidth (sin padding).</summary>
        public float Height(string text, float wrapWidth) { return Lines(text, wrapWidth).Count * LineHeight; }

        // ---------- dibujo ----------
        /// <summary>Dibuja texto. rect/pad en "espacio de diseno"; pivot/scale transforman a pantalla (p' = pivot + (p - pivot) * scale), igual que FFGUIUtility.ScaleAroundPivot.
        /// anchor como GUIStyle.alignment. Sin recorte (clip) de glifos fuera del rect.</summary>
        public void Draw(Rect rect, string text, TextAnchor anchor, bool wrap, Color color, RectOffset pad = null, Vector2 pivot = default(Vector2), float scale = 1f)
        {
            if (Atlas == null || string.IsNullOrEmpty(text) || Event.current == null || Event.current.type != EventType.Repaint) return;
            text = Loc.T(text);
            float pl = pad != null ? pad.left : 0, pr = pad != null ? pad.right : 0, pt = pad != null ? pad.top : 0, pb = pad != null ? pad.bottom : 0;
            float cx = rect.x + pl, cy = rect.y + pt, cw = rect.width - pl - pr, ch = rect.height - pt - pb;
            var lines = Lines(text, wrap ? cw : 0f); float bh = lines.Count * LineHeight;
            int a = (int)anchor, v = a / 3, hz = a % 3;     // TextAnchor: 0..2 upper, 3..5 middle, 6..8 lower; 0 left,1 center,2 right
            float top = v == 0 ? cy : v == 1 ? cy + (ch - bh) * 0.5f : cy + ch - bh;
            var old = GUI.color; GUI.color = new Color(color.r, color.g, color.b, color.a * old.a);
            for (int i = 0; i < lines.Count; i++)
            {
                string l = lines[i]; float lw = WidthTrim(l); float pen = hz == 0 ? cx : hz == 1 ? cx + (cw - lw) * 0.5f : cx + cw - lw; float ltop = top + i * LineHeight;
                for (int j = 0; j < l.Length; j++)
                {
                    char c = l[j]; Glyph g;
                    if (Glyphs.TryGetValue(c, out g) && g.HasQuad)
                    {
                        float gx = Mathf.Floor(pen + g.X + 0.5f), gy = Mathf.Floor(ltop + g.Y + 0.5f);   // mismo redondeo que el preview
                        var dst = new Rect(pivot.x + (gx - pivot.x) * scale, pivot.y + (gy - pivot.y) * scale, g.W * scale, g.H * scale);
                        GUI.DrawTextureWithTexCoords(dst, Atlas, AtlasUpsideDown ? new Rect(g.U, 1f - g.V, g.UW, -g.VH) : new Rect(g.U, g.V, g.UW, g.VH), true);
                    }
                    pen += Adv(c); if (j + 1 < l.Length) pen += Kern(c, l[j + 1]);
                }
            }
            GUI.color = old;
        }

        // ---------- UnityEngine.Font ----------
        /// <summary>Font con characterInfo. Quirks: advance es int (sin kerning por pares); usa GUIStyle.fontSize = 0 (o igual a CharacterInfo.size); la linea base/interlineado
        /// de una Font creada con new Font() los decide Unity (puede requerir +-1-2 px); el shader "GUI/Text Shader" debe estar en Always Included Shaders (URP lo elimina).</summary>
        public Font ToUnityFont()
        {
            if (unityFont != null) return unityFont;
            var font = new Font(); font.name = Name;
            var sh = Shader.Find("GUI/Text Shader"); if (sh == null) sh = Shader.Find("UI/Default"); if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh); mat.mainTexture = Atlas; mat.color = Color.white; font.material = mat;
            var list = new List<CharacterInfo>(Glyphs.Count); int size = Mathf.RoundToInt(LineHeight);
            foreach (var kvp in Glyphs)
            {
                var g = kvp.Value; var ci = new CharacterInfo(); ci.index = kvp.Key; ci.advance = Mathf.RoundToInt(g.Advance * KerningScale);
                // relativo a la linea base (Y arriba): maxY = baseline - Y, minY = maxY - H
                float maxY = Baseline - g.Y, minY = maxY - g.H;
                ci.minX = Mathf.RoundToInt(g.X); ci.maxX = Mathf.RoundToInt(g.X + g.W); ci.minY = Mathf.RoundToInt(minY); ci.maxY = Mathf.RoundToInt(maxY);
                ci.glyphWidth = Mathf.RoundToInt(g.W); ci.glyphHeight = Mathf.RoundToInt(g.H); ci.bearing = Mathf.RoundToInt(g.X); ci.size = size; ci.style = FontStyle.Normal;
                float v0 = g.V, v1 = g.V + g.VH; if (AtlasUpsideDown) { v0 = 1f - g.V; v1 = 1f - g.V - g.VH; }   // v0 = borde inferior del glifo mostrado
                ci.uvBottomLeft = new Vector2(g.U, v0); ci.uvBottomRight = new Vector2(g.U + g.UW, v0); ci.uvTopRight = new Vector2(g.U + g.UW, v1); ci.uvTopLeft = new Vector2(g.U, v1);
                list.Add(ci);
            }
            font.characterInfo = list.ToArray(); unityFont = font; return font;
        }
    }

    public static class BitmapFontLoader
    {
        public const string DataPath = "OpenFusion/UI/Data/Fonts/", TexPath = "OpenFusion/UI/Textures/";
        static readonly Dictionary<string, BitmapFont> cache = new Dictionary<string, BitmapFont>();

        /// <summary>jsonName sin extension, p.ej. "JEFFE___14__903". Devuelve null (y avisa una vez) si falta el JSON o el atlas.</summary>
        public static BitmapFont Load(string jsonName, bool atlasUpsideDown = false)
        {
            BitmapFont f; string ck = jsonName + (atlasUpsideDown ? "|flip" : ""); if (cache.TryGetValue(ck, out f)) return f;
            f = null;
            try
            {
                var ta = Resources.Load<TextAsset>(DataPath + jsonName);
                if (ta == null) Debug.LogWarning("[bitmapfont] falta TextAsset " + DataPath + jsonName);
                else
                {
                    var root = MiniJson.Parse(ta.text) as Dictionary<string, object>; string texName = TexName(root != null && root.ContainsKey("m_Texture") ? root["m_Texture"] as string : null);
                    var atlas = string.IsNullOrEmpty(texName) ? null : Resources.Load<Texture2D>(TexPath + texName);
                    if (atlas == null) Debug.LogWarning("[bitmapfont] falta atlas " + TexPath + texName + " (" + jsonName + ")");
                    else { atlas.filterMode = FilterMode.Bilinear; atlas.wrapMode = TextureWrapMode.Clamp; f = BitmapFont.FromJson(ta.text, atlas, atlasUpsideDown); }
                }
            }
            catch (Exception e) { Debug.LogWarning("[bitmapfont] " + jsonName + ": " + e.Message); }
            cache[ck] = f; return f;
        }
        public static Font LoadUnityFont(string jsonName, bool atlasUpsideDown = false) { var f = Load(jsonName, atlasUpsideDown); return f != null ? f.ToUnityFont() : null; }
        public static Font BuildUnityFont(BitmapFont f) { return f != null ? f.ToUnityFont() : null; }

        // "tex:Textures/font_Texture__257.png" -> "font_Texture__257"
        static string TexName(string m)
        {
            if (string.IsNullOrEmpty(m)) return null; int i = m.LastIndexOf('/'); if (i >= 0) m = m.Substring(i + 1); else if (m.StartsWith("tex:")) m = m.Substring(4);
            if (m.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) m = m.Substring(0, m.Length - 4); return m;
        }
    }

    // JSON minimo (objetos -> Dictionary<string,object>, arrays -> List<object>, numeros -> double). JsonUtility no sirve: m_KerningValues son arrays anidados.
    static class MiniJson
    {
        public static object Parse(string s) { int i = 0; try { return Val(s, ref i); } catch (Exception) { return null; } }
        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        static object Val(string s, ref int i)
        {
            Ws(s, ref i); char c = s[i];
            if (c == '{') { var d = new Dictionary<string, object>(); i++; Ws(s, ref i); if (s[i] == '}') { i++; return d; } while (true) { Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i); i++; d[k] = Val(s, ref i); Ws(s, ref i); if (s[i] == ',') { i++; continue; } i++; return d; } }
            if (c == '[') { var l = new List<object>(); i++; Ws(s, ref i); if (s[i] == ']') { i++; return l; } while (true) { l.Add(Val(s, ref i)); Ws(s, ref i); if (s[i] == ',') { i++; continue; } i++; return l; } }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }
        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder(); i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\') { i++; char e = s[i]; if (e == 'u') { sb.Append((char)System.Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; } else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e); }
                else sb.Append(s[i]); i++;
            }
            i++; return sb.ToString();
        }
    }
}
