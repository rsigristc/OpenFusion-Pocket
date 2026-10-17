// GuideWindow: port de cnGuiHelp + cnHelpMode (ventana "GAME GUIDE", Win.Guide).
// Datos: XdtTableScript.m_pHelpTable (HelpTableScript). Skin FusionFallHelpSkin (1385). Ventana 1036x654, fondo panelback.png + HelpSmallBG.png.
// Botones: lista de temas (13,70,158x321 con scroll), subtemas (3 columnas de 5), contenido con scroll (199,200,810x380), PREVIOUS/NEXT SECTION, X.
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class GuideWindow : MonoBehaviour
    {
        public Func<string, Texture> TexLoader;
        const int SK = 1385;
        public static GuideWindow Ensure(GameObject host) { var w = host.GetComponent<GuideWindow>(); if (w == null) w = host.AddComponent<GuideWindow>(); return w; }
        /// Pagina a mostrar al abrir (cnHelpMode.InitMode): tema grande / subtema. Se restablece a 1/0 tras abrir.
        public static int NextBig = 1, NextSmall = 0;

        static readonly Rect WindowRect = new Rect(0, 0, 1036, 654), BigBut = new Rect(13, 80, 135, 16), SmallBut = new Rect(200, 78, 256, 16), Content = new Rect(200, 203, 790, 22);

        struct Elem { public int Type; public string Text, Style; public int H; public Texture Shot; }

        XdtTableScript tables; HelpTableScript ht;
        bool open, texTried; Texture back, smallBack;
        string[] bigTitles = new string[0], smallTitles = new string[0];
        int big = 1, small = 0, smallMax, bigMax; float scrollBig, scrollContent; int bigHeight, contentHeight;
        readonly List<Elem> elems = new List<Elem>();
        readonly Dictionary<string, Texture> shots = new Dictionary<string, Texture>();

        public void Init(XdtTableScript t) { tables = t; ht = t != null ? t.m_pHelpTable : null; }
        void Awake() { GameMenu.Implemented.Add(GameMenu.Win.Guide); }

        void Update()
        {
            bool now = GameMenu.Current == GameMenu.Win.Guide;
            if (now && !open) OnOpen(); else if (!now && open) { back = smallBack = null; texTried = false; shots.Clear(); }
            open = now;
        }

        Texture Load(string path)
        {
            if (TexLoader == null) return null;
            foreach (var ext in new[] { "", ".jpg", ".png", ".psd", ".dds", ".tga" }) { try { var t = TexLoader(path + ext); if (t != null) return t; } catch (Exception) { } }
            return null;
        }

        void OnOpen()
        {
            if (!texTried) { texTried = true; back = Load("TutorialAssets/panelback"); smallBack = Load("CharacterCreationAssets/HelpSmallBG"); }
            if (ht == null && tables != null) ht = tables.m_pHelpTable; if (ht == null || ht.m_pHelpData == null) return;
            bigBut = ht.m_pHelpData.Length; bigMax = bigBut; bigHeight = bigBut * 16 + 100;
            bigTitles = new string[bigBut];
            for (int i = 1; i < bigBut; i++) { int si = ht.m_pHelpData[i].m_iTitleStartString; bigTitles[i] = "  " + (Str(ht.m_pHelpString, si) ?? ""); }
            int b = Mathf.Clamp(NextBig, 1, Mathf.Max(1, bigBut - 1)); int s = Mathf.Max(0, NextSmall); NextBig = 1; NextSmall = 0;
            BigSelected(b); SmallSelected(s);
        }
        int bigBut;

        static string Str(StringTableElement[] a, int i) { return a != null && i >= 0 && i < a.Length && a[i] != null ? a[i].m_strName : null; }
        static string Cmt(StringTableElement[] a, int i) { return a != null && i >= 0 && i < a.Length && a[i] != null ? a[i].m_strComment : null; }

        void BigSelected(int b)
        {   // cnHelpMode.BigTitleSelected
            var el = ht.m_pHelpData[b]; int n = el.m_iSubEndString - el.m_iTitleStartString + 1; smallTitles = new string[Mathf.Max(0, n)]; smallMax = n; big = b; scrollBig = 0f;
            for (int i = 0; i < n; i++) smallTitles[i] = "  " + (Cmt(ht.m_pHelpString, el.m_iTitleStartString + i) ?? "");
        }
        void SmallSelected(int s)
        {   // cnHelpMode.SmallTitleSelected
            small = s; var el = ht.m_pHelpData[big]; int pi = el.m_iTitleStartString + s; if (ht.m_pHelpPageData == null || pi < 0 || pi >= ht.m_pHelpPageData.Length) { elems.Clear(); contentHeight = 0; return; }
            var pg = ht.m_pHelpPageData[pi]; int n = pg.m_iEndContent - pg.m_iStartContent + 1; contentHeight = 0; scrollContent = 0f; elems.Clear();
            for (int i = 0; i < n; i++)
            {
                var d = ht.m_pHelpPageDescData[pg.m_iStartContent + i]; var st = ht.m_pHelpPageString[d.m_iString]; var e = new Elem { Type = d.m_iType };
                if (d.m_iType == 3) { e.Text = "  " + st.m_strName; e.Style = "Title"; contentHeight += 22; }
                else if (d.m_iType == 4) { e.Text = "  " + st.m_strName; e.Style = "Title2"; contentHeight += 22; }
                else if (d.m_iType == 1)
                {
                    e.Text = st.m_strName; e.Style = d.m_iSize == 4 ? "HelpTextMiddleSmall" : "HelpTextLeftSmall"; var sty = Skin.Get(SK, e.Style);
                    e.H = sty != null && sty.Font != null ? Mathf.CeilToInt(sty.Font.Height(Loc.T(e.Text), 795f - sty.pL - sty.pR) + sty.pT + sty.pB) : 20; contentHeight += e.H;
                }
                else if (d.m_iType == 2) { e.Text = st.m_strComment; e.Shot = Shot(e.Text); contentHeight += 262; }
                contentHeight += 25; elems.Add(e);
            }
        }
        Texture Shot(string name)
        {
            if (string.IsNullOrEmpty(name)) return null; Texture t; if (shots.TryGetValue(name, out t)) return t;
            t = Load("Help/" + name); if (t == null) { string n2 = name; int dot = n2.LastIndexOf('.'); if (dot > 0) t = Load("Help/" + n2.Substring(0, dot)); } shots[name] = t; return t;
        }
        void Prev()
        {
            small--;
            if (small < 0)
            {
                big--; if (big == 0) { big = bigMax - 1; var e = ht.m_pHelpData[big]; smallMax = e.m_iSubEndString - e.m_iTitleStartString + 1; }
                small = smallMax - 1; BigSelected(big);
            }
            SmallSelected(small);
        }
        void Next()
        {
            small++;
            if (small == smallMax) { big++; if (big == bigMax) big = 1; small = 0; BigSelected(big); }
            SmallSelected(small);
        }

        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        static void Txt(Rect r, string t, SkinStyle st, Color? c = null, TextAnchor? an = null)
        {
            if (st == null || st.Font == null || string.IsNullOrEmpty(t) || Event.current.type != EventType.Repaint) return;
            st.Font.Draw(r, t, an ?? st.Align, st.Wrap, c ?? st.cN, st.Padding);
        }

        void OnGUI()
        {
            if (!open) return; var e = Event.current; if (e == null || Screen.height <= 0) return;
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f);
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1500; GUI.color = Color.white;
            try
            {
                GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
                if (e.type == EventType.Repaint)
                {
                    GUI.color = new Color(0f, 0f, 0f, back != null ? 1f : 0.75f); GUI.DrawTexture(new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc), Texture2D.whiteTexture); GUI.color = Color.white;
                    if (back != null) GUI.DrawTexture(new Rect(-960f, -720f, 1920f, 1440f), back, ScaleMode.ScaleToFit);
                }
                var wr = new Rect(-WindowRect.width * 0.5f, -WindowRect.height * 0.5f, WindowRect.width, WindowRect.height);
                GUI.BeginGroup(wr); DrawPage(e); GUI.EndGroup();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        void DrawPage(Event e)
        {
            if (smallBack != null && e.type == EventType.Repaint) GUI.DrawTexture(WindowRect, smallBack);
            var lab = Skin.Get(SK, "label"); var hb = Skin.Get(SK, "HelpButton"); var btn = Skin.Get(SK, "button");
            Txt(new Rect(18, 15, 150, 20), Loc.T("GAME GUIDE"), lab); Txt(new Rect(8, 52, 90, 10), Loc.T("CHOOSE A TOPIC") + ":", Skin.Get(SK, "SmallJeffFont"));
            // lista de temas (scroll)
            var vb = new Rect(13, 70, 158, 321); float maxB = Mathf.Max(0f, bigHeight - vb.height);
            if (e.type == EventType.ScrollWheel && vb.Contains(e.mousePosition)) { scrollBig = Mathf.Clamp(scrollBig + e.delta.y * 20f, 0f, maxB); e.Use(); }
            scrollBig = Mathf.Clamp(scrollBig, 0f, maxB);
            GUI.BeginGroup(vb);
            for (int i = 1; i < bigBut; i++)
            {
                var r = new Rect(BigBut.x - vb.x, BigBut.y - vb.y + (i - 1) * (BigBut.height + 5f) - scrollBig, BigBut.width, BigBut.height);
                if (Skin.Button(r, bigTitles[i], hb) && big != i) { BigSelected(i); SmallSelected(0); }
            }
            GUI.EndGroup();
            Txt(new Rect(195, 48, 250, 20), big >= 1 && big < bigTitles.Length ? bigTitles[big] : "", Skin.Get(SK, "BigChaletBookFont"));
            for (int j = 0; j < smallTitles.Length; j++)
            {
                float x = SmallBut.x + (j < 5 ? 0f : j < 10 ? SmallBut.width + 10f : SmallBut.width * 2f + 20f); float y = SmallBut.y + (SmallBut.height + 5f) * (j % 5);
                if (Skin.Button(new Rect(x, y, SmallBut.width, SmallBut.height), smallTitles[j], hb)) SmallSelected(j);
            }
            // contenido (scroll)
            var view = new Rect(199, 200, 810, 380); float maxC = Mathf.Max(0f, contentHeight - view.height);
            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) { scrollContent = Mathf.Clamp(scrollContent + e.delta.y * 20f, 0f, maxC); e.Use(); }
            scrollContent = Mathf.Clamp(scrollContent, 0f, maxC);
            GUI.BeginGroup(view);
            var p = Content; p.x -= view.x; p.y -= view.y + scrollContent;
            foreach (var el in elems)
            {
                if (el.Type == 3) { p = new Rect(Content.x - view.x, p.y, Content.width, Content.height); Txt(p, el.Text, Skin.Get(SK, el.Style)); p.y += p.height + 25f; }
                else if (el.Type == 4) { p.x = Content.x - view.x; p.width = Content.width; p.height = 20f; Txt(p, el.Text, Skin.Get(SK, el.Style)); p.y += p.height + 25f; }
                else if (el.Type == 1) { p.x = Content.x - view.x; p.width = Content.width; p.height = el.H; Txt(p, el.Text, Skin.Get(SK, el.Style)); p.y += p.height + 25f; }
                else if (el.Type == 2) { p.x = 420f - view.x; p.width = 350f; p.height = 262f; if (e.type == EventType.Repaint && el.Shot != null) GUI.DrawTexture(p, el.Shot, ScaleMode.StretchToFill); p.y += p.height + 25f; }
            }
            GUI.EndGroup();
            if (maxC > 0f)
            {   // barra de desplazamiento
                var track = new Rect(view.xMax - 12f, view.y, 12f, view.height); float thumbH = Mathf.Max(20f, track.height * view.height / contentHeight), ty = track.y + (track.height - thumbH) * (scrollContent / maxC);
                int id = GUIUtility.GetControlID(FocusType.Passive, track);
                if (e.type == EventType.Repaint) { var oc = GUI.color; GUI.color = new Color(0f, 0.2f, 0.4f, 0.5f); GUI.DrawTexture(track, Texture2D.whiteTexture); GUI.color = new Color(0.5f, 0.85f, 1f, 0.9f); GUI.DrawTexture(new Rect(track.x + 2f, ty, track.width - 4f, thumbH), Texture2D.whiteTexture); GUI.color = oc; }
                if (e.type == EventType.MouseDown && e.button == 0 && track.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
                if (GUIUtility.hotControl == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)) { scrollContent = Mathf.Clamp01((e.mousePosition.y - track.y - thumbH * 0.5f) / Mathf.Max(1f, track.height - thumbH)) * maxC; e.Use(); }
                else if (GUIUtility.hotControl == id && e.type == EventType.MouseUp) { GUIUtility.hotControl = 0; e.Use(); }
            }
            if (Skin.Button(new Rect(980, 12, 30, 30), "", Skin.Get(SK, "ExitButton"))) { GameMenu.CloseWindow(); return; }
            if (Skin.Button(new Rect(192, 607, 187, 27), "< " + Loc.T("PREVIOUS SECTION"), btn)) Prev();
            if (Skin.Button(new Rect(815, 607, 187, 27), Loc.T("NEXT SECTION") + " >", btn)) Next();
        }
    }
}
