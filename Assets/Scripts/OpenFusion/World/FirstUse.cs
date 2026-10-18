// Hints de "primer uso" (cnFirstUseSysManager + cnGuiFirstUse del cliente original).
//  Check(cond): si hints activos y la condición (1..72) no está marcada => marca el flag (local + REQ_PC_FIRST_USE_FLAG_SET 318767238), añade el icono (máx. 10) y,
//  si no hay Computress activa, la muestra con su burbuja y sonido "FU Sound/<m_strComment1>.wav". Flags iniciales: sPCLoadData2CL.iFirstUseFlag1/2 (ENTER_SUCC).
//  Iconos: bajo-derecha (Screen.width-45*(n-k), Screen.height-190, 38x38; 2ª fila a -235 si n>5). Computress: (0,H-435,244,380); burbuja x=10,y=H-405-alto; cerrar (163,H-400,23,23).
//  Click en un icono: abre la Game Guide en el tema/subtema de la tabla descargada.
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class FirstUse : MonoBehaviour
    {
        public static FirstUse Instance; public static bool MouseOver { get; private set; }
        public static long Flag1, Flag2;
        sealed class Icon { public int Id; public float Until; public int Main, Sub; }
        sealed class Comp { public int Id; public float Stay; public string Name, Desc, Sound; }
        readonly List<Icon> icons = new List<Icon>(); readonly Queue<Comp> compQ = new Queue<Comp>();
        Comp cur; bool computress; Vector2 bubble; XdtTableScript tables; PacketDispatcher disp; PlayerHud hud; WorldEntities ents;
        Texture2D texComp, texIcon, texIconSel, texBub, texTail, texClose, texCloseOver; BitmapFont font; bool loaded; bool hadHp; AudioSource snd;

        public static FirstUse Ensure(GameObject host) { var f = host.GetComponent<FirstUse>(); if (f == null) f = host.AddComponent<FirstUse>(); Instance = f; return f; }
        public void Init(PacketDispatcher d, XdtTableScript t, PlayerHud h, WorldEntities e)
        {
            if (disp != null) disp.SelfEnter -= OnSelfEnter;
            disp = d; tables = t; hud = h; ents = e; if (disp != null) disp.SelfEnter += OnSelfEnter;
        }
        void OnDestroy() { if (disp != null) disp.SelfEnter -= OnSelfEnter; StopSound(); if (Instance == this) Instance = null; MouseOver = false; }
        void OnSelfEnter(EnterSucc e) { Flag1 = e.Quest?.FirstUse1 ?? 0; Flag2 = e.Quest?.FirstUse2 ?? 0; icons.Clear(); compQ.Clear(); computress = false; cur = null; StopSound(); hadHp = false; }

        public static bool Has(int c) { if (c < 1 || c > 72) return false; if (c <= 64) return (Flag1 & (1L << (c - 1))) != 0; return (Flag2 & (1L << (c - 65))) != 0; }

        /// <summary>cnFirstUseSysManager.CheckCondition</summary>
        public static void Check(int cond)
        {
            var f = Instance; if (f == null || !GameOptions.Cur.GameHint || cond <= 0 || cond >= 73 || Has(cond) || NpcUI.Sender == null) return;
            var rows = f.tables?.m_pFirstUseTable?.m_pFirstUseData;
            if (rows == null || cond >= rows.Length || rows[cond] == null) return; // Do not consume an unavailable hint.
            if (cond <= 64) Flag1 |= 1L << (cond - 1); else Flag2 |= 1L << (cond - 65);
            if (NpcUI.Sender != null) NpcUI.Sender(ClientPackets.FirstUseFlagSet(cond));
            f.AddIcon(cond);
        }

        static bool Deferred(int id) { return id == 2 || id == 62 || id == 63 || id == 11 || id == 15; }
        bool Busy() { return NpcUI.Active || GameMenu.Blocking; }   // el original difiere ciertas pistas mientras el modo de juego != 5

        void AddIcon(int id)
        {
            var ft = tables != null ? tables.m_pFirstUseTable : null; if (ft == null || ft.m_pFirstUseData == null || id >= ft.m_pFirstUseData.Length || ft.m_pFirstUseData[id] == null) return;
            var d = ft.m_pFirstUseData[id]; StringTableElement st = ft.m_pFirstUseString != null && d.m_iString >= 0 && d.m_iString < ft.m_pFirstUseString.Length ? ft.m_pFirstUseString[d.m_iString] : null;
            if (icons.Count == 10) icons.RemoveAt(0);
            icons.Add(new Icon { Id = id, Until = Time.time + d.m_iIconDuration, Main = d.m_iHelpMain, Sub = d.m_iHelpSub });
            var c = new Comp { Id = id, Stay = d.m_iCompDuration, Name = st != null ? st.m_strName : "", Desc = st != null ? st.m_strComment : "", Sound = st != null ? st.m_strComment1 : "" };
            if (computress || (Busy() && Deferred(id))) { compQ.Enqueue(c); return; }
            Show(c);
        }

        void Show(Comp c)
        {
            cur = c; computress = true; c.Stay = Time.time + c.Stay; Sized = false; PlaySound(c.Sound);
        }
        bool Sized;
        void PlaySound(string name)
        {
            StopSound(); if (string.IsNullOrWhiteSpace(name) || Sfx.Instance == null) return;
            var expected = cur;
            Sfx.Instance.Play2D("fu sound/" + name.ToLowerInvariant() + ".wav", Sfx.VoiceVolume, false, a =>
            { if (cur == expected && computress) snd = a; else if (a != null) Destroy(a.gameObject); });
        }
        void StopSound() { if (snd != null) { snd.Stop(); Destroy(snd.gameObject); } snd = null; }

        void NextComp()
        {
            computress = false; StopSound();
            if (compQ.Count == 0) return;
            var c = compQ.Dequeue();
            if (Busy() && Deferred(c.Id)) { compQ.Enqueue(c); return; }
            Show(c);
        }

        void Update()
        {
            for (int i = 0; i < icons.Count && icons[i].Until <= Time.time;) icons.RemoveAt(i);   // UpdateFirstUse: sólo caducan los del frente
            if (computress && cur != null && cur.Stay < Time.time) NextComp();
            else if (!computress && compQ.Count > 0) NextComp();
            // muerte del jugador => Resurrect (29)
            if (hud != null && hud.Data != null && ents != null && ents.Self != null) { if (hud.Data.Hp > 0) hadHp = true; else if (hadHp) { hadHp = false; Check(29); } }
        }

        void Load()
        {
            if (loaded) return; loaded = true; const string U = "OpenFusion/UI/Textures/";
            texComp = Resources.Load<Texture2D>(U + "FirstUseComputress"); texIcon = Resources.Load<Texture2D>(U + "FirstUseIcon"); texIconSel = Resources.Load<Texture2D>(U + "FirstUseIconSelected");
            texBub = Resources.Load<Texture2D>(U + "bbubble"); texTail = Resources.Load<Texture2D>(U + "bb_009"); texClose = Resources.Load<Texture2D>(U + "close_normal"); texCloseOver = Resources.Load<Texture2D>(U + "close_over");
            font = BitmapFontLoader.Load("JEFFE___14__903");
        }

        void Press(int idx)
        {
            if (idx < 0 || idx >= icons.Count) return; var ic = icons[idx];
            int oldBig = GuideWindow.NextBig, oldSmall = GuideWindow.NextSmall;
            GuideWindow.NextBig = ic.Main; GuideWindow.NextSmall = ic.Sub;
            if (!GameMenu.OpenWindow(GameMenu.Win.Guide)) { GuideWindow.NextBig = oldBig; GuideWindow.NextSmall = oldSmall; return; }
            icons.RemoveAt(idx); computress = false; cur = null; compQ.Clear(); StopSound();
        }

        void OnGUI()
        {
            var ev = Event.current; if (ev == null) return;
            if (!GameOptions.Cur.GameHint && icons.Count == 0 && !computress) { MouseOver = false; return; }
            if (ents == null || ents.Self == null || GameMenu.HudHidden) { MouseOver = false; return; }
            if (!computress && icons.Count == 0) { MouseOver = false; return; }
            Load(); float sc = GameOptions.UiScale; float W = Screen.width / sc, H = Screen.height / sc;
            var om = GUI.matrix; int od = GUI.depth; GUI.depth = 5; GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f)); bool over = false; var mp = ev.mousePosition;
            try
            {
                if (computress && cur != null)
                {
                    if (font != null)
                    {
                        if (!Sized) { float w = Mathf.Max(100f, Mathf.Min(300f, font.MaxLineWidth(cur.Desc) + 20f)); bubble = new Vector2(w, font.Height(cur.Desc, w - 20f) + 20f); Sized = true; }
                        var rb = new Rect(10f, H - 405f - bubble.y, bubble.x, bubble.y);
                        if (ev.type == EventType.Repaint)
                        {
                            Slice(texBub, rb, 6f);
                            font.Draw(rb, cur.Desc, TextAnchor.MiddleCenter, true, Color.black, new RectOffset(10, 10, 10, 10));
                        }
                        if (rb.Contains(mp)) over = true;
                    }
                    var rc = new Rect(163f, H - 400f, 23f, 23f); bool hv = rc.Contains(mp);
                    if (ev.type == EventType.Repaint) { var t = hv && texCloseOver != null ? texCloseOver : texClose; if (t != null) GUI.DrawTexture(rc, t); }
                    if (GUI.Button(rc, GUIContent.none, GUIStyle.none)) { OnClose(); }
                    var rp = new Rect(0f, H - 435f, 244f, 380f);
                    if (ev.type == EventType.Repaint && texComp != null) GUI.DrawTexture(rp, texComp, ScaleMode.StretchToFill, true);
                    if (rp.Contains(mp)) over = true;
                }
                int n = icons.Count;
                for (int k = 0; k < n; k++)
                {
                    float x, y; if (n > 5) { if (k < 5) { x = W - 45f * (5 - k); y = H - 190f; } else { x = W - 45f * (n - k); y = H - 235f; } } else { x = W - 45f * (n - k); y = H - 190f; }
                    var r = new Rect(x, y, 38f, 38f); bool hv = r.Contains(mp); if (hv) over = true;
                    if (ev.type == EventType.Repaint) { var t = hv && texIconSel != null ? texIconSel : texIcon; if (t != null) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); }
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { Press(k); break; }
                }
            }
            finally { GUI.matrix = om; GUI.depth = od; }
            if (ev.type == EventType.Repaint || ev.type == EventType.Layout) MouseOver = over;
        }

        void OnClose()
        {   // OnComputressExitPressed
            NextComp();
        }

        static void Slice(Texture2D t, Rect r, float b)
        {
            if (t == null) return; float W = t.width, H = t.height;
            float[] sx = { 0, b, W - b, W }, dx = { r.x, r.x + b, r.xMax - b, r.xMax }, sy = { 0, b, H - b, H }, dy = { r.y, r.y + b, r.yMax - b, r.yMax };
            for (int j = 0; j < 3; j++) for (int i = 0; i < 3; i++)
            {   // coordenadas de textura con origen abajo-izquierda
                var dst = new Rect(dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]);
                var src = new Rect(sx[i] / W, 1f - sy[j + 1] / H, (sx[i + 1] - sx[i]) / W, (sy[j + 1] - sy[j]) / H);
                if (dst.width > 0f && dst.height > 0f) GUI.DrawTextureWithTexCoords(dst, t, src, true);
            }
        }
    }
}
