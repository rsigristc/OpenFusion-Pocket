// Banner de mensajes del nanocom (cnGUINanocom.SendMessageBox / RenderNanoMessage): caja deslizante junto al minimapa con cola (MsgboxList).
// Medidas del original: NanoMessageRect 372x122, NanoComNpcBox (51,3,321,119), NanoComUserBox (101,3,271,119), titulo (130,2,200,30), texto (120,25,180,70), icono NPC (60,15,64,64).
// Animacion: fNewMessagetime 1 -> 0 a 2/s; x = nRect.x - 372 + sin^2(t^2*pi/2)*372 + 65, ancho (1-sin^2)*372. nRect.x = Screen.width - 187.
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class NanoMessages : MonoBehaviour
    {
        public static NanoMessages Instance;
        public enum Kind { User = 0, Npc = 9, Nano = 10, Etc = 8 }
        sealed class Msg { public string Title, Text, Icon; public Kind Kind; public float Timer; }
        readonly List<Msg> list = new List<Msg>();
        float newT = 1f;
        public bool Visible = true;
        public string LabelFontJson = "ChaletBook-Regular_Small__1018", TitleFontJson = "JEFFE___14__903";
        BitmapFont chalet, title; bool fontsTried;
        readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();

        public static NanoMessages Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            Instance = host.GetComponent<NanoMessages>(); if (Instance == null) Instance = host.AddComponent<NanoMessages>();
            return Instance;
        }
        /// <summary>SendMessageBox: encola; si la cola estaba vacia el mensaje entra deslizando.</summary>
        public static void Post(string titleStr, string text, Kind kind, float seconds, string icon = null)
        {
            if (Instance == null) return;
            var m = new Msg { Title = titleStr ?? "", Text = text ?? "", Kind = kind, Timer = seconds > 0f ? seconds : 5f, Icon = icon };
            if (Instance.list.Count == 0) { Instance.newT = 1f; Sfx.Ui("Comm_SlideIn"); }
            Instance.list.Add(m);
        }
        public static void Announce(string text, float seconds) { Post("Dexter", text, Kind.Npc, Mathf.Clamp(seconds, 4f, 20f), "dexter_icon"); }


        // ---- Mensaje de guia al entrar (cnMissionManager l.1990-2012): npc guia segun iGuide (mentor), texto LoginNomail/LoginMail de GuideTable ----
        static readonly int[] GuideNpc = { 707, 728, 731, 732, 1171 };
        XdtTableScript tables; PacketDispatcher disp; int guideMentor = -1; float guideAt = -1f;
        public void InitGuide(PacketDispatcher d, XdtTableScript t)
        {
            tables = t; if (disp != null) disp.SelfEnter -= OnSelfEnter; disp = d; if (d != null) d.SelfEnter += OnSelfEnter;
        }
        void OnSelfEnter(EnterSucc e) { guideMentor = e.Quest != null ? e.Quest.Mentor : 0; guideAt = 4f; }   // hilo de red: solo marcar
        void TickGuide()
        {
            if (guideMentor < 0) return; if (guideAt > 0f) { guideAt -= Time.deltaTime; return; }
            int g = guideMentor; guideMentor = -1;
            try
            {
                if (tables == null || tables.m_pGuideTable == null || tables.m_pNpcTable == null) return;
                if (g < 1 || g > GuideNpc.Length) { Debug.Log("[guia] mentor=" + g + " sin guia"); return; }
                var gt = tables.m_pGuideTable; var ge = g < gt.m_pGuideData.Length ? gt.m_pGuideData[g] : null; if (ge == null) return;
                int sid = ge.m_iLoginNomail; string txt = null;
                if (gt.m_pGuideStringData != null) { if (sid >= 0 && sid < gt.m_pGuideStringData.Length && gt.m_pGuideStringData[sid] != null) txt = gt.m_pGuideStringData[sid].m_pszString; if (string.IsNullOrEmpty(txt)) foreach (var x in gt.m_pGuideStringData) if (x != null && x.m_iNum == sid) { txt = x.m_pszString; break; } }
                if (string.IsNullOrEmpty(txt)) { Debug.Log("[guia] sin texto para mentor=" + g + " id=" + sid); return; }
                int npc = GuideNpc[g - 1]; NpcTableElement row = null; foreach (var e in tables.m_pNpcTable.m_pNpcData) if (e != null && e.m_iNpcNumber == npc) { row = e; break; }
                string name = "Dexter", icon = "dexter_icon";
                if (row != null)
                {
                    var sd = tables.m_pNpcTable.m_pNpcStringData; if (sd != null && row.m_iNpcName >= 0 && row.m_iNpcName < sd.Length && sd[row.m_iNpcName] != null) name = sd[row.m_iNpcName].m_strName ?? name;
                    var idt = tables.m_pNpcTable.m_pNpcIconData; if (idt != null && row.m_iIcon1 >= 0 && row.m_iIcon1 < idt.Length && idt[row.m_iIcon1] != null)
                    { var ic = idt[row.m_iIcon1]; string pre = ic.m_iIconType == 4 ? "npcicon_" : ic.m_iIconType == 8 ? "mobicon_" : ic.m_iIconType == 10 ? "hnpcicon_" : null; if (pre != null) icon = pre + ic.m_iIconNumber.ToString("00"); }
                }
                Debug.Log("[guia] mentor=" + g + " npc=" + npc + " " + name + " icono=" + icon);
                Post(name, txt, Kind.Npc, 8f, icon); ChatHud.AddEventString(4, name + ": " + txt);
            }
            catch (System.Exception ex) { Debug.LogWarning("[guia] " + ex.Message); }
        }

        Texture2D T(string n)
        {
            if (string.IsNullOrEmpty(n)) return null; Texture2D t; if (tex.TryGetValue(n, out t)) return t;
            t = Resources.Load<Texture2D>("OpenFusion/UI/Textures/" + n); if (t == null) t = Resources.Load<Texture2D>("OpenFusion/Icons/" + n); if (t == null) t = Resources.Load<Texture2D>("OpenFusion/Portraits/" + n);
            if (t != null) { t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp; } else Debug.LogWarning("[nanomsg] falta textura " + n);
            tex[n] = t; return t;
        }

        void Update()
        {
            TickGuide();
            if (list.Count == 0) return;
            newT -= Time.deltaTime * 2f; if (newT < 0f) newT = 0f;
            var m = list[0]; m.Timer -= Time.deltaTime;
            if (m.Timer < 0f) { list.RemoveAt(0); newT = 1f; Sfx.Ui(list.Count == 0 ? "Comm_SlideOut" : "Comm_SlideIn"); }
        }

        void OnGUI()
        {
            if (GameMenu.HudHidden) return;
            if (!Visible || list.Count == 0 || Event.current == null || Event.current.type != EventType.Repaint || Screen.height <= 0) return;
            if (!fontsTried) { fontsTried = true; chalet = BitmapFontLoader.Load(LabelFontJson); title = BitmapFontLoader.Load(TitleFontJson); }
            float W = Screen.width, Sc = GameOptions.UiScale;
            float t2 = newT * newT, s = Mathf.Sin(t2 * Mathf.PI * 0.5f); s *= s;
            const float mw = 372f, mh = 122f;
            // diseno anclado a la derecha: x_pantalla = W + (xd - W) * Sc, con xd = W - 187 - 372 + s*372 + 65 en unidades de diseno desplazadas desde W
            float xd = W - 187f - mw + s * mw + 65f, wd = (1f - s) * mw;
            var g = new Rect(W + (xd - W) * Sc, 0f, wd * Sc, mh * Sc);
            var m = list[0];
            var old = GUI.color; int od = GUI.depth; GUI.depth = 7; GUI.color = Color.white;
            GUI.BeginGroup(g);
            try
            {
                Rect npcBox = new Rect(51f, 3f, 321f, 119f), userBox = new Rect(101f, 3f, 271f, 119f), full = new Rect(0f, 0f, 372f, 122f);
                Rect titleR = new Rect(130f, 2f, 200f, 30f), textR = new Rect(120f, 25f, 180f, 70f), npcIcon = new Rect(60f, 15f, 64f, 64f), nanoIcon = new Rect(55f, 10f, 64f, 64f);
                switch (m.Kind)
                {
                    case Kind.Npc: Box(npcBox, "nanocom_message_npc", Sc); Icon(npcIcon, m.Icon, Sc); break;
                    case Kind.Nano: Box(full, "nanocom_message_nano", Sc); Icon(nanoIcon, m.Icon, Sc); break;
                    case Kind.Etc: Box(npcBox, "nanocom_message_etc", Sc); Icon(npcIcon, m.Icon, Sc); break;
                    default: Box(userBox, "nanocom_message", Sc); break;
                }
                Txt(title, titleR, m.Title, Sc, new Color(0f, 1f, 1f, 1f), TextAnchor.MiddleLeft, null);   // estilo "BIGFont14": cian, MiddleLeft, sin padding
                Txt(chalet, textR, m.Text, Sc, new Color(1f, 0.996f, 1f, 1f), TextAnchor.UpperLeft, new RectOffset(10, 6, 4, 6));   // estilo "messagetext": padding 10/6/4/6
            }
            finally { GUI.EndGroup(); GUI.color = old; GUI.depth = od; }
        }
        void Box(Rect r, string n, float Sc) { var t = T(n); if (t != null) GUI.DrawTexture(new Rect(r.x * Sc, r.y * Sc, r.width * Sc, r.height * Sc), t, ScaleMode.StretchToFill, true); }
        void Icon(Rect r, string n, float Sc) { var t = T(n); if (t != null) GUI.DrawTexture(new Rect(r.x * Sc, r.y * Sc, r.width * Sc, r.height * Sc), t, ScaleMode.StretchToFill, true); }
        void Txt(BitmapFont f, Rect r, string text, float Sc, Color c, TextAnchor an, RectOffset pad)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (f != null) { f.Draw(r, text, an, true, c, pad, Vector2.zero, Sc); return; }
            var st = new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = an, fontSize = Mathf.RoundToInt(12 * Sc) }; st.normal.textColor = c;
            GUI.Label(new Rect(r.x * Sc, r.y * Sc, r.width * Sc, r.height * Sc), text, st);
        }
    }
}
