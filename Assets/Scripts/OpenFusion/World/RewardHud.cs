// Recompensas (cnDisplayMapName.RewardItem / RewardFM / ShinyIcon / DoRewardGUI): iconos que rebotan abajo (caja, boosts, pociones, huevo de Coco),
// contadores rodantes de Fusion Matter / Taros y las lineas de chat "You receive..." / "You earned..." / "You collected...".
// Animacion original: y = |sin(pi/2 + pi*(2-f)^3)| * f^2 * 170, con f = tiempo/2 (rebote hacia su sitio); luego 2 s de fundido.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class RewardHud : MonoBehaviour
    {
        public static RewardHud Instance;
        sealed class Disp { public string Title; public int Count; public Texture Icon; public float T = IconTime; }
        const float IconTime = 2f;
        readonly List<Disp> crate = new List<Disp>(), boost = new List<Disp>(), potion = new List<Disp>(), shiny = new List<Disp>();
        int curFm, curTaros, tgtFm, tgtTaros; float lastUp, fmTimer, tarosTimer;
        XdtTableScript tables; PlayerHud hud; PacketDispatcher disp;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
        public Func<string, Texture> IconProvider;
        BitmapFont font; bool fontTried;

        public static RewardHud Ensure(GameObject host) { if (Instance != null) return Instance; Instance = host.GetComponent<RewardHud>(); if (Instance == null) Instance = host.AddComponent<RewardHud>(); return Instance; }

        public void Init(PlayerHud h, PacketDispatcher d, XdtTableScript t)
        {
            tables = t;
            if (hud != null) hud.RewardApplied -= OnReward; hud = h; if (hud != null) hud.RewardApplied += OnReward;
            if (disp != null) disp.Unhandled -= OnPacket; disp = d; if (d != null) d.Unhandled += OnPacket;
        }
        void OnDestroy() { if (hud != null) hud.RewardApplied -= OnReward; if (disp != null) disp.Unhandled -= OnPacket; if (Instance == this) Instance = null; }
        void OnPacket(uint type, byte[] b)
        {
            if (type == PacketIds.P_FE2CL_REP_SHINY_PICKUP_SUCC && b != null && b.Length >= 8) { int sk = BitConverter.ToInt16(b, 0), cs = BitConverter.ToInt32(b, 4); q.Enqueue(() => ShinyPickup(sk, cs)); }
        }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }

        Texture2D T(string n)
        {
            Texture2D t; if (tex.TryGetValue(n, out t)) return t;
            t = Resources.Load<Texture2D>("OpenFusion/UI/Textures/" + n); if (t != null) { t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp; } else Debug.LogWarning("[reward] falta textura " + n);
            tex[n] = t; return t;
        }
        static string IconName(IconElement e)
        {
            if (e == null) return null; string n = e.m_iIconNumber.ToString("00");
            switch (e.m_iIconType) { case 0: return "wpnicon_" + n; case 1: return "nanoicon_" + n; case 2: return "skillicon_" + n; case 3: return "cosicon_" + n; case 4: return "npcicon_" + n; case 5: return "nanoready_" + n; case 6: return "questitemicon_" + n; case 7: return "generalitemicon_" + n; case 8: return "mobicon_" + n; case 9: return "fusionicon_" + n; case 10: return "hnpcicon_" + n; case 11: return "transport_" + n; case 12: return "vehicle_" + n; }
            return null;
        }
        Texture Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (IconProvider != null) return IconProvider(name);
            return Resources.Load<Texture2D>("OpenFusion/Icons/" + name);
        }

        // ---- REP_REWARD_ITEM (RewardFM + RewardItem): prev* = valores antes de aplicar el paquete ----
        void OnReward(int pTaros, int pFm, int pNano, int pWpn, byte[] b)
        {
            try
            {
                if (b == null || b.Length < 36) return;
                int taros = I(b, 0), fm = I(b, 4), bn = I(b, 8), bw = I(b, 12), cnt = (sbyte)b[16];
                for (int i = 0; i < cnt; i++)
                {
                    int o = 36 + 20 * i; if (o + 20 > b.Length) break;
                    int type = BitConverter.ToInt16(b, o), id = BitConverter.ToInt16(b, o + 2), eil = I(b, o + 12);
                    if (eil == 1 && type == 9) ChestReward(id);
                }
                if (bn > pNano) FirstUse.Check(19); if (bw > pWpn) FirstUse.Check(20); if (bn > pNano) potion.Add(new Disp { Count = bn - pNano, Title = "+" + (bn - pNano) + " POTIONS" });
                if (bw > pWpn) boost.Add(new Disp { Count = bw - pWpn, Title = "+" + (bw - pWpn) + " BOOSTS" });
                if (curFm > pFm) curFm = pFm; if (curTaros > pTaros) curTaros = pTaros;
                if (curFm == tgtFm) curFm = pFm; if (curTaros == tgtTaros) curTaros = pTaros;
                tgtFm = fm; tgtTaros = taros;
                if (taros > pTaros) { FirstUse.Check(26); ChatHud.AddEventString(5, "You earned " + (taros - pTaros) + " Taros for a total " + taros + " taros."); tarosTimer = 2f; }
                if (fm > pFm) { ChatHud.AddEventString(5, "You collected " + (fm - pFm) + " Fusion Matter for a total " + fm + " Fusion Matter."); fmTimer = 2f; }
            }
            catch (Exception ex) { Debug.LogWarning("[reward] " + ex.Message); }
        }
        void ChestReward(int id)
        {
            int chk = 0; var ct = tables != null ? tables.m_pChestItemTable : null;
            if (ct != null && ct.m_pItemData != null) foreach (var e in ct.m_pItemData) if (e != null && e.m_iItemNumber == id) { chk = e.m_iChestCheck; break; }
            if (chk == 0) { FirstUse.Check(8); crate.Add(new Disp { Count = 1, Title = "+1 C.R.A.T.E" }); ChatHud.AddEventString(5, "You receive 1 C.R.A.T.E"); }
            else if (chk == 1) ChatHud.AddEventString(5, "You receive 1 E.G.G");
        }

        // ---- REP_SHINY_PICKUP_SUCC (ShinyIcon): huevo de Coco ----
        void ShinyPickup(int skillId, int cstb)
        {
            var st = tables != null ? tables.m_pSkillTable : null; if (st == null) return;
            string name = st.m_pSkillStringData != null && skillId >= 0 && skillId < st.m_pSkillStringData.Length && st.m_pSkillStringData[skillId] != null ? st.m_pSkillStringData[skillId].m_strName : "";
            ChatHud.AddEventString(5, "You receive <" + name + "> from Coco's Egg"); FirstUse.Check(25);
            int ik = -1;
            if (cstb == 0) { if (st.m_pSkillData != null && skillId >= 0 && skillId < st.m_pSkillData.Length && st.m_pSkillData[skillId] != null) ik = st.m_pSkillData[skillId].m_iIcon; }
            else if (st.m_pSkillBuffData != null && cstb > 0 && cstb < st.m_pSkillBuffData.Length && st.m_pSkillBuffData[cstb] != null) ik = st.m_pSkillBuffData[cstb].m_iBuffIcon;
            Texture ic = null; if (st.m_pSkillIconData != null && ik >= 0 && ik < st.m_pSkillIconData.Length) ic = Icon(IconName(st.m_pSkillIconData[ik]));
            { string inm = st.m_pSkillIconData != null && ik >= 0 && ik < st.m_pSkillIconData.Length ? IconName(st.m_pSkillIconData[ik]) : null; Debug.Log("[huevo] skill=" + skillId + " cstb=" + cstb + " icono=" + ik + " -> " + inm + (ic != null ? " (ok)" : " (NO ENCONTRADO)")); }
            shiny.Add(new Disp { Title = name, Icon = ic });
        }

        void Tick(List<Disp> l, bool removeAtZero) { if (l.Count == 0) return; l[0].T -= Time.deltaTime; if (removeAtZero ? l[0].T <= 0f : l[0].T < -IconTime) l.RemoveAt(0); }
        void Update()
        {
            Action a; while (q.TryDequeue(out a)) a();
            Tick(crate, false); Tick(boost, false); Tick(potion, false); Tick(shiny, false);
            bool ch = false;
            if (Time.time - lastUp > 0.1f)
            {
                if (curFm != tgtFm) { int n = Mathf.Clamp(Mathf.Abs(tgtFm - curFm) / 5, 1, 10); curFm += curFm < tgtFm ? n : -n; ch = true; } else if (fmTimer > 0f) fmTimer -= Time.deltaTime;
                if (curTaros != tgtTaros) { int n = Mathf.Clamp(Mathf.Abs(tgtTaros - curTaros) / 5, 1, 10); curTaros += curTaros < tgtTaros ? n : -n; ch = true; } else if (tarosTimer > 0f) tarosTimer -= Time.deltaTime;
            }
            if (ch) lastUp = Time.time;
        }

        // ---- dibujo: diseno en unidades de pantalla escaladas alrededor del centro-abajo (ScreenPivot.Bottom) ----
        float S, Cx, H;
        Rect R(float x, float y, float w, float h) { return new Rect(Cx + (x - Cx) * S, H + (y - H) * S, w * S, h * S); }
        void Txt(Rect r, string s, TextAnchor an, Color c)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (font != null) { font.Draw(new Rect(r.x, r.y, r.width / S, r.height / S), s, an, false, c, null, new Vector2(r.x, r.y), S); return; }   // la fuente escala los glifos alrededor del pivote: el rect va en unidades de diseno
            var st = new GUIStyle(GUI.skin.label) { alignment = an, fontSize = Mathf.RoundToInt(13 * S), fontStyle = FontStyle.Bold }; st.normal.textColor = c; GUI.Label(r, s, st);
        }
        void Shadowed(float x, float y, float w, float h, string s, float alpha)
        {
            Txt(R(x + 1f, y + 1f, w, h), s, TextAnchor.UpperCenter, new Color(0f, 0f, 0f, alpha)); Txt(R(x, y, w, h), s, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, alpha));
        }
        // devuelve true si dibujo algo
        void Bounce(List<Disp> l, Texture back, float x, int kind)
        {
            if (l.Count == 0 || back == null) return; var d = l[0];
            float w = back.width, h = back.height, bh = kind == 3 ? h : 170f, f = d.T / IconTime, k = 2f - f, y, alpha = 1f;
            if (d.T > 0f) y = Mathf.Abs(Mathf.Sin(Mathf.PI * 0.5f + Mathf.PI * k * k * k)) * f * f * bh; else { y = 0f; alpha = Mathf.Min(1f, (1f + d.T / IconTime) * 2f); }
            y += H - bh; var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(R(x, y, w, h), back, ScaleMode.StretchToFill, true);
            if (kind == 3 && d.Icon != null) GUI.DrawTexture(R(x + 69.5f, y + 70f, 22f, 22f), d.Icon, ScaleMode.StretchToFill, true);
            if (kind == 3) Shadowed(x, y + h - 25f, w, 25f, d.Title, alpha); else Shadowed(x, y + 110f, w, 30f, d.Title, alpha);
            GUI.color = old;
        }
        void Counter(string tn, float x, float w, float h, int value, float timer, bool changing, int dx)
        {
            if (!changing && !(timer > 0f)) return; var t = T(tn); if (t == null) return;
            float a = timer < 1f && !changing ? timer : 1f; var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            float y = H - h; GUI.DrawTexture(R(x, y, w, h), t, ScaleMode.StretchToFill, true);
            int v = value, div = 100000000;
            for (int i = 0; i < 9; i++) { int dg = v / div; Txt(R(x + dx + 12 * i, y + 31f, 10f, 20f), dg.ToString(), TextAnchor.UpperCenter, Color.white); v -= dg * div; div /= 10; }
            GUI.color = old;
        }
        void OnGUI()
        {
            if (GameMenu.HudHidden) return;
            if (Event.current == null || Event.current.type != EventType.Repaint || Screen.height <= 0) return;
            if (crate.Count + boost.Count + potion.Count + shiny.Count == 0 && curFm == tgtFm && curTaros == tgtTaros && fmTimer <= 0f && tarosTimer <= 0f) return;
            if (!fontTried) { fontTried = true; font = BitmapFontLoader.Load("JEFFE___14__903"); }
            S = GameOptions.UiScale; Cx = Screen.width * 0.5f; H = Screen.height; int od = GUI.depth; GUI.depth = 12;
            // Retrobution: pociones y boosts suben junto a sus contadores (abajo a la derecha); caja y huevo al centro como en el cliente 2.5
            float W = Screen.width; Func<float, float> RightX = o => Cx + (W - o * S - Cx) / S;   // o = distancia al borde derecho en unidades de diseno
            Bounce(crate, T("crate"), Cx - 150f - 75f, 0);
            Bounce(potion, T("potion__548"), RightX(448f + 60f + 130f), 1);
            Bounce(boost, T("boost"), RightX(448f + 60f), 2);
            { var sb = T("shiny_back"); if (sb != null) Bounce(shiny, sb, Cx - sb.width * 0.5f, 3); }
            Counter("fm_count", Cx - 172f, 172f, 63f, curFm, fmTimer, curFm != tgtFm, 6);
            Counter("taros_count", Cx, 170f, 64f, curTaros, tarosTimer, curTaros != tgtTaros, 6);
            GUI.depth = od;
        }
    }
}
