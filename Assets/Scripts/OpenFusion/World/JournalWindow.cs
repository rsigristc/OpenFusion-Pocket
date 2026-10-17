// JournalWindow: port de cnMissionJournal (Mission Journal) para las pestanas Active / Completed (los modos de oferta y recompensa los resuelve NpcUI).
// Skins: izquierda = FusionFallMissionSkin (1388, Skin.JournalL), derecha = FusionFallMissionSkinR (1370, Skin.Journal). Ventana 1036x654 centrada (panelback detras).
// Datos: NanocomHud (lista de misiones activas, mision actual, progreso, completadas) + tablas de mision / NPC / objetos. Acciones: DELETE MISSION (REQ_PC_TASK_STOP) y MAKE CURRENT MISSION.
// Simplificaciones: con el popup de objetos de recompensa de informacion de objeto (DoWindowPopUP), el nano de la mision se muestra con su icono (sin camara 3D), sin animacion de entrada. SIN COMPILAR NI PROBAR EN UNITY.
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class JournalWindow : MonoBehaviour
    {
        public Func<string, Texture> TexLoader;
        const int SKL = Skin.JournalL, SKR = Skin.Journal;
        public static JournalWindow Ensure(GameObject host) { var w = host.GetComponent<JournalWindow>(); if (w == null) w = host.AddComponent<JournalWindow>(); return w; }

        // ---- geometria del original ----
        static readonly Rect WindowRect = new Rect(0, 0, 1036, 654), LeftBox = new Rect(16, 30, 565, 612), RightBox = new Rect(597, 11, 382, 633);
        static readonly Rect ActiveDlg = new Rect(9, 28, 565, 612), ActiveBar = new Rect(12, 3, 549, 74);
        static readonly Rect MissionName = new Rect(36, 12, 317, 47), MissionLevel = new Rect(36, 58, 317, 20), NpcBox = new Rect(14, 92, 68, 68), NpcName = new Rect(90, 90, 300, 20), NpcPos = new Rect(90, 108, 300, 20);
        static readonly Rect MissionDet = new Rect(90, 145, 300, 20), MissionSub = new Rect(50, 164, 475, 100), Rewards = new Rect(13, 288, 300, 20), RewardsWin = new Rect(12, 315, 577, 245);
        static readonly Rect DelBut = new Rect(15, 574, 190, 25), MakeCurBut = new Rect(312, 574, 243, 25), MyNote = new Rect(26, 170, 500, 18), NoteSub = new Rect(26, 190, 500, 100);
        static readonly Rect EndReward = new Rect(13, 315, 300, 20), EndRewardWin = new Rect(12, 340, 577, 245);

        static readonly Rect OfferDlg = new Rect(8, 11, 566, 636), OfferBar = new Rect(13, 20, 549, 74);   // cnMissionJournal.rectofferdlg / rectofferbarrer (modos Allow / Reward)

        enum Cat { Nano, Guide, World }

        sealed class Entry { public MissionElement Me; public string Name = "", Desc = ""; }

        PacketDispatcher disp; NanocomHud nh; WorldEntities ents; XdtTableScript tables; MissionTableScript mt; NpcTableScript npcT; MessageTableScript msgT;
        bool open, texTried; int tab;                                  // tab 0 = Active, 1 = Completed
        Texture back; readonly Texture[] missionIcon = new Texture[7], banner = new Texture[7];
        Texture skillBack, taros, fm, currentMissionTex, pointTex, closeTex;
        readonly List<Entry> nanoL = new List<Entry>(), guideL = new List<Entry>(), worldL = new List<Entry>();
        readonly Dictionary<int, Entry> entryCache = new Dictionary<int, Entry>();
        MissionElement cur, finalMission; RewardElement reward;
        string title = "", difficulty = "", description = "", taskDescription = "", npcName = "", npcLoc = " ";
        Texture npcTex, nanoTex; string nanoName = "", nanoAttrib = "", nanoDesc = "";
        readonly Texture[] skillTex = new Texture[3]; readonly string[] skillName = new string[3], skillType = new string[3], skillDesc = new string[3];
        readonly Texture[] rwIcon = new Texture[4], rwIcon2 = new Texture[4]; readonly string[] rwName = new string[4], rwName2 = new string[4];
        float subScroll, rewardScroll, listScroll, listContentH = 600f; bool expNano = true, expGuide = true, expWorld = true;
        bool confirmDelete; int completedCount = -1, activeSig = -1; float cooldown;
        // ---- modos de NPC: 1 = oferta (eJM_Allow), 2 = recompensa (eJM_Reward) ----
        public static JournalWindow Instance;
        int mode, pendingMode, pendingNpc, offerNpc; MissionElement pendingMe; float busy; string notice;
        readonly bool[] sel1 = new bool[4], sel2 = new bool[4];
        readonly System.Collections.Concurrent.ConcurrentQueue<Action> evq = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>(); readonly Dictionary<string, float> iconRetry = new Dictionary<string, float>();
        // ---- popup de objeto de recompensa (cnMissionJournal.DoWindowPopUP / DoInfo / SetPopupItem) ----
        public static bool PopupActive;
        sealed class ItemDisp
        {
            public Texture Icon; public int Type, Id, Opt, Level, Point, Group, Defense;
            public string Name = "", Desc = "", TypeName = "", Rate = "", Rarity = "", Trade = "";
            public int TimeLimit; public bool Shop;
        }
        bool popOpen; readonly ItemDisp rewardItem = new ItemDisp(), equipItem = new ItemDisp(), backupItem = new ItemDisp();
        Texture popBack, popSmall, equipTex; PcAppearance selfApp; Texture2D placeholder;
        static readonly Rect ItemPopRect = new Rect(0, 0, 310, 448), ItemPopRectSmall = new Rect(0, 0, 310, 250), ExitBut = new Rect(278, 14, 32, 32), PTitle = new Rect(10, 0, 100, 30);
        static readonly Rect PIcon = new Rect(17, 29, 64, 64), PName = new Rect(90, 29, 190, 64), PDesc = new Rect(10, 60, 290, 100), PInfo = new Rect(10, 150, 290, 197);
        static readonly Rect PStatus = new Rect(10, 252, 180, 20), POne = new Rect(26, 233, 80, 20), PGroup = new Rect(115, 233, 80, 20), PDef = new Rect(204, 233, 80, 20);

        public void Init(PacketDispatcher d, NanocomHud n, WorldEntities e, XdtTableScript t)
        {
            if (disp != null) disp.Unhandled -= OnPacket; disp = d; nh = n; ents = e; tables = t; if (disp != null) disp.Unhandled += OnPacket; if (e != null) e.SelfSpawned += (go, es) => { selfApp = es.Self; };
            mt = t != null ? t.m_pMissionTable : null; npcT = t != null ? t.m_pNpcTable : null; msgT = t != null ? t.m_pMessageTable : null;
        }
        void Awake() { Instance = this; GameMenu.Implemented.Add(GameMenu.Win.Journal); }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; if (Instance == this) Instance = null; }

        // ---- API del servicio de misiones de NPC (NpcIconMode -> cnMissionJournal.InitMode) ----
        /// <summary>m = 1 oferta (ACCEPT / DECLINE), m = 2 recompensa (COMPLETE / CANCEL). npcInstId = iID del NPC que habla.</summary>
        public void OpenMode(int m, MissionElement me, int npcInstId)
        {
            if (me == null || (m != 1 && m != 2)) return; pendingMode = m; pendingMe = me; pendingNpc = npcInstId;
            if (open) { ApplyPending(); return; }
            if (!GameMenu.OpenWindow(GameMenu.Win.Journal)) pendingMode = 0;
        }
        void ApplyPending()
        {
            if (pendingMode == 0 || pendingMe == null) return;
            mode = pendingMode; offerNpc = pendingNpc; var me = pendingMe; pendingMode = 0; pendingMe = null;
            tab = 0; for (int i = 0; i < 4; i++) sel1[i] = sel2[i] = false; busy = 0f; notice = null; confirmDelete = false; popOpen = false;
            SetCurrent(me);
        }
        void EndMode() { GameMenu.CloseWindow(); }
        void OnPacket(uint type, byte[] b)
        {   // TASK_START_SUCC/FAIL y TASK_END_SUCC/FAIL: cierran la ventana de oferta/recompensa o liberan el boton
            if (b == null || b.Length < 4) return; int task = BitConverter.ToInt32(b, 0); int err = b.Length >= 8 ? BitConverter.ToInt32(b, 4) : 0;
            if (type == PacketIds.P_FE2CL_REP_PC_TASK_START_SUCC) evq.Enqueue(() => { if (mode == 1 && cur != null && cur.m_iHTaskID == task) EndMode(); });
            else if (type == PacketIds.P_FE2CL_REP_PC_TASK_END_SUCC) evq.Enqueue(() => { if (mode == 2 && cur != null && cur.m_iHTaskID == task) EndMode(); });
            else if (type == PacketIds.P_FE2CL_REP_PC_TASK_START_FAIL) evq.Enqueue(() => { if (mode == 1 && cur != null && cur.m_iHTaskID == task) { busy = 0f; Debug.LogWarning("[journal] TASK_START_FAIL " + err); } });
            else if (type == PacketIds.P_FE2CL_REP_PC_TASK_END_FAIL) evq.Enqueue(() => { if (mode == 2 && cur != null && cur.m_iHTaskID == task) { busy = 0f; if (err == 13) notice = Msg(12); Debug.LogWarning("[journal] TASK_END_FAIL " + err); } });
        }
        void Accept()
        {   // ACCEPT MISSION: CheckMissionSlot -> mensaje 156; si no, Send(true) + REQ_PC_TASK_START
            if (nh == null || cur == null) return;
            if (!nh.HasMissionSlot(cur.m_iHMissionType)) { notice = Msg(156); return; }
            busy = 4f; Sfx.Ui("Mission_Accepted"); nh.RequestTaskStart(cur.m_iHTaskID, offerNpc);
        }
        static int Count(bool[] a) { int n = 0; for (int i = 0; i < a.Length; i++) if (a[i]) n++; return n; }
        void Complete()
        {   // COMPLETE MISSION: CheckComplete (mensaje 90 si la seleccion no es valida) + REQ_PC_TASK_END
            if (nh == null || cur == null || reward == null) return;
            int c1 = Count(sel1), c2 = Count(sel2);
            bool ok = (reward.m_iBox1Choice <= 0 || (c1 > 0 && c1 <= reward.m_iBox1Choice)) && (reward.m_iBox2Choice <= 0 || (c2 > 0 && c2 <= reward.m_iBox2Choice));
            if (!ok) { notice = Msg(90); return; }
            int b1 = 0, b2 = 0; for (int i = 0; i < 4; i++) { if (sel1[i]) b1 |= 1 << i; if (sel2[i]) b2 |= 1 << i; }
            busy = 4f; nh.RequestTaskEnd(cur.m_iHTaskID, offerNpc, b1, b2);
        }

        // ============================================================ apertura / actualizacion ============================================================
        void Update()
        {
            bool now = GameMenu.Current == GameMenu.Win.Journal;
            if (now && !open) OnOpen(); else if (!now && open) OnClosed();
            open = now; if (!open) return;
            if (cooldown > 0f) cooldown -= Time.unscaledDeltaTime;
            if (busy > 0f) busy -= Time.unscaledDeltaTime;
            Action act; while (evq.TryDequeue(out act)) { try { act(); } catch (Exception ex) { Debug.LogWarning("[journal] " + ex.Message); } }
            if (popOpen && Input.GetKeyDown(KeyCode.Escape)) popOpen = false; PopupActive = popOpen;
            RefreshLists();
        }
        void OnOpen()
        {
            if (!texTried) { texTried = true; LoadTextures(); }
            confirmDelete = false; popOpen = false; extPopup = false; PopupActive = false; completedCount = -1; activeSig = -1; entryCache.Clear(); subScroll = rewardScroll = listScroll = 0f;
            tab = 0; mode = 0; RefreshLists(); SetActive(true); ApplyPending();
        }
        void OnClosed() { if (mode != 0) { int id = offerNpc; mode = 0; busy = 0f; notice = null; NpcUI.ReturnTo(id); } confirmDelete = false; popOpen = false; PopupActive = false; back = null; texTried = false; entryCache.Clear(); }

        void LoadTextures()
        {
            string[] mi = { "nreddicon", "nrdexicon", "nrmojoicon", "nrbenicon", "nrcomicon", "nrnanoicon", "nrworldicon" };
            string[] bn = { "edd_banner", "dexter_banner", "mojo_banner", "ben_banner", "compu_banner", "nano_banner", "world_banner" };
            for (int i = 0; i < 7; i++) { missionIcon[i] = UiKit.Tex(mi[i]); banner[i] = UiKit.Tex(bn[i]); }
            skillBack = UiKit.Tex("buff_icon_back"); taros = UiKit.Tex("tarosicon"); fm = UiKit.Tex("fmicon"); currentMissionTex = UiKit.Tex("current_mission"); pointTex = UiKit.Tex("point"); popBack = UiKit.Tex("unequipdlg"); popSmall = UiKit.Tex("reward_userdlg"); equipTex = UiKit.Tex("equipiteminfo");
            if (TexLoader != null) { foreach (var ext in new[] { ".psd", ".png", ".dds", ".tga", "" }) { try { back = TexLoader("TutorialAssets/panelback" + ext); } catch (Exception) { back = null; } if (back != null) break; } }
        }

        Entry EntryOf(MissionElement me)
        {
            Entry e; if (entryCache.TryGetValue(me.m_iHTaskID, out e) && e.Me == me) return e;
            e = new Entry { Me = me, Name = Str(me.m_iHMissionName) };
            var j = JournalOf(me); if (j != null) e.Desc = Str(j.m_iMissionSummary);
            entryCache[me.m_iHTaskID] = e; return e;
        }
        string Str(int idx) { var sd = mt != null ? mt.m_pMissionStringData : null; return sd != null && idx >= 0 && idx < sd.Length && sd[idx] != null ? sd[idx].m_pstrNameString ?? "" : ""; }
        JournalElement JournalOf(MissionElement me) { var jd = mt != null ? mt.m_pJournalData : null; int i = me.m_iSTJournalIDAdd; return jd != null && i > 0 && i < jd.Length ? jd[i] : null; }
        string Msg(int i) { var m = msgT != null ? msgT.m_pMessageData : null; return m != null && i >= 0 && i < m.Length && m[i] != null ? m[i].m_szString ?? "" : ""; }

        void RefreshLists()
        {
            if (nh == null || mt == null) return;
            if (tab == 0)
            {
                var act = nh.JournalActive(); int sig = act.Count * 1000003 + nh.JournalSelected;
                for (int i = 0; i < act.Count; i++) sig = sig * 31 + act[i].m_iHTaskID;
                if (sig != activeSig) { activeSig = sig; Sort(act); if (mode != 0) { } else if (cur != null && !Contains(act, cur.m_iHMissionID)) SetActive(true); else if (cur != null) Reselect(); }
            }
            else
            {
                var comp = nh.JournalCompleted(); if (comp.Count != completedCount) { completedCount = comp.Count; Sort(comp); }
            }
        }
        static bool Contains(List<MissionElement> l, int missionId) { foreach (var m in l) if (m.m_iHMissionID == missionId) return true; return false; }
        void Reselect()
        {   // la tarea actual de una mision activa puede cambiar (siguiente tarea): se vuelve a leer
            foreach (var m in nh.JournalActive()) if (m.m_iHMissionID == cur.m_iHMissionID && m.m_iHTaskID != cur.m_iHTaskID) { SetCurrent(m); break; }
        }
        void Sort(List<MissionElement> l)
        {
            nanoL.Clear(); guideL.Clear(); worldL.Clear();
            if (tab == 1) l.Sort((a, b) => { int c = a.m_iCTRReqLvMin.CompareTo(b.m_iCTRReqLvMin); return c != 0 ? c : string.CompareOrdinal(Str(a.m_iHMissionName), Str(b.m_iHMissionName)); });
            foreach (var me in l)
            {
                var e = EntryOf(me);
                switch (me.m_iHMissionType) { case 1: guideL.Add(e); break; case 2: nanoL.Add(e); break; case 3: worldL.Add(e); break; }
            }
        }

        /// <summary>SetActive(bForce): lista segun la pestana y mision por defecto.</summary>
        void SetActive(bool force)
        {
            if (nh == null || mt == null) return;
            if (tab == 0)
            {
                var act = nh.JournalActive(); activeSig = -1; Sort(act); MissionElement pick = null;
                int sel = nh.JournalSelected; foreach (var m in act) if (m.m_iHMissionID == sel) pick = m;
                if (pick == null && act.Count > 0) pick = act[act.Count - 1];
                SetCurrent(pick);
            }
            else
            {
                var comp = nh.JournalCompleted(); completedCount = comp.Count; Sort(comp);
                SetCurrent(comp.Count > 0 ? (nanoL.Count > 0 ? nanoL[0].Me : guideL.Count > 0 ? guideL[0].Me : worldL.Count > 0 ? worldL[0].Me : null) : null);
            }
            expNano = expGuide = expWorld = true; subScroll = rewardScroll = 0f;
        }

        void SetCurrent(MissionElement me)
        {
            cur = me; finalMission = null; reward = null; description = taskDescription = ""; npcTex = null; npcName = ""; nanoTex = null; npcLoc = " "; title = difficulty = ""; subScroll = rewardScroll = 0f;
            for (int i = 0; i < 4; i++) { rwIcon[i] = rwIcon2[i] = null; rwName[i] = rwName2[i] = "\n"; }
            if (me == null) return;
            finalMission = nh.JournalFinalTask(me.m_iHMissionID) ?? me;
            var rd = mt.m_pRewardData; int ri = finalMission.m_iSUReward; reward = rd != null && ri >= 0 && ri < rd.Length ? rd[ri] : null;
            switch (me.m_iHMissionType) { case 1: title = Msg(6); break; case 2: title = Msg(7); break; case 3: title = Msg(8); break; }
            difficulty = Loc.T("Level") + " " + me.m_iCTRReqLvMin;
            switch (me.m_iHDifficultyType) { case 0: difficulty += " " + Msg(13); break; case 1: difficulty += " " + Msg(14); break; case 2: difficulty += " " + Msg(15); break; }
            title = title + " : " + Str(me.m_iHMissionName);
            if (me.m_iHJournalNPCID > 0)
            {
                var nd = npcT != null ? npcT.m_pNpcData : null; var row = nd != null && me.m_iHJournalNPCID < nd.Length ? nd[me.m_iHJournalNPCID] : null;
                if (row != null)
                {
                    var ic = npcT.m_pNpcIconData; if (ic != null && row.m_iIcon1 >= 0 && row.m_iIcon1 < ic.Length) npcTex = IconTex(ic[row.m_iIcon1]);
                    npcName = nh.NpcNameForJournal(me.m_iHJournalNPCID);
                }
            }
            var j = JournalOf(me);
            if (j != null)
            {
                if (tab == 1) { description = Str(j.m_iDetaileMissionCompleteSummary); taskDescription = Str(j.m_iMissionCompleteSummary); }
                else if (mode == 2) { description = Str(j.m_iDetaileMissionCompleteSummary); taskDescription = Str(j.m_iDetailedTaskDesc); }
                else if (mode == 1) { description = Str(j.m_iDetaileMissionDesc); taskDescription = Str(j.m_iDetaileMissionDesc); }
                else { description = Str(j.m_iDetaileMissionDesc); taskDescription = Str(j.m_iDetailedTaskDesc); }
            }
            var first = nh.JournalFirstTask(me.m_iHMissionID) ?? me; int npcId = first.m_iHNPCID > 0 ? first.m_iHNPCID : me.m_iHJournalNPCID;
            npcLoc = nh.JournalNpcLocation(npcId);
            if (reward != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    rwIcon[i] = RewardIcon(At(reward.m_iMissionRewarItemType, i), At(reward.m_iMissionRewardItemID, i), out rwName[i]);
                    rwIcon2[i] = RewardIcon(At(reward.m_iMissionRewardItemType2, i), At(reward.m_iMissionRewardItemID2, i), out rwName2[i]);
                }
            }
            if (me.m_iHMissionType == 2) LoadNano(me.m_iSTNanoID);
        }
        static int At(int[] a, int i) { return a != null && i >= 0 && i < a.Length ? a[i] : 0; }

        void LoadNano(int id)
        {
            var nt = tables != null ? tables.m_pNanoTable : null; var st = tables != null ? tables.m_pSkillTable : null; if (nt == null || nt.m_pNanoData == null || id <= 0 || id >= nt.m_pNanoData.Length) return;
            var n = nt.m_pNanoData[id]; if (n == null) return; var sd = nt.m_pNanoStringData;
            if (sd != null && n.m_iNanoName >= 0 && n.m_iNanoName < sd.Length && sd[n.m_iNanoName] != null) { nanoName = sd[n.m_iNanoName].m_strName; nanoAttrib = sd[n.m_iNanoName].m_strComment1; nanoDesc = sd[n.m_iNanoName].m_strComment; }
            if (nt.m_pNanoIconData != null && n.m_iIcon1 >= 0 && n.m_iIcon1 < nt.m_pNanoIconData.Length) nanoTex = IconTex(nt.m_pNanoIconData[n.m_iIcon1]);
            for (int k = 0; k < 3; k++)
            {
                skillTex[k] = null; skillName[k] = skillType[k] = skillDesc[k] = "";
                int ti = n.m_iTune != null && k < n.m_iTune.Length ? n.m_iTune[k] : 0; var td = nt.m_pNanoTuneData; if (td == null || ti <= 0 || ti >= td.Length || td[ti] == null) continue;
                var tune = td[ti]; var sk = st != null && st.m_pSkillData != null && tune.m_iSkillID >= 0 && tune.m_iSkillID < st.m_pSkillData.Length ? st.m_pSkillData[tune.m_iSkillID] : null;
                if (sk != null && st.m_pSkillIconData != null && sk.m_iIcon >= 0 && sk.m_iIcon < st.m_pSkillIconData.Length) skillTex[k] = IconTex(st.m_pSkillIconData[sk.m_iIcon]);
                var ts = nt.m_pNanoTuneStringData; if (ts != null && tune.m_iTuneName >= 0 && tune.m_iTuneName < ts.Length && ts[tune.m_iTuneName] != null) { skillName[k] = ts[tune.m_iTuneName].m_strName; skillType[k] = ts[tune.m_iTuneName].m_strComment1; skillDesc[k] = ts[tune.m_iTuneName].m_strComment; }
            }
        }

        // ---- iconos / objetos de recompensa (GetItemIcon) ----
        Texture2D IconTex(IconElement e)
        {
            string n = NanoWheelHud.IconName(e); if (string.IsNullOrEmpty(n)) return null; Texture2D t;
            if (icons.TryGetValue(n, out t)) return t; float nr; if (iconRetry.TryGetValue(n, out nr) && Time.unscaledTime < nr) return null; t = Resources.Load<Texture2D>("OpenFusion/Icons/" + n); if (t == null) t = Resources.Load<Texture2D>("OpenFusion/Portraits/" + n); if (t == null && TexLoader != null) { try { t = TexLoader("Icons/" + n + ".png") as Texture2D; } catch (Exception) { t = null; } if (t == null) { try { t = TexLoader(n) as Texture2D; } catch (Exception) { t = null; } } } if (t != null) icons[n] = t; else iconRetry[n] = Time.unscaledTime + 3f; return t;   // un icono que aun no esta en el bundle se reintenta cada 3 s en vez de quedar vacio para siempre
        }
        public string ItemName(int type, int id) { string n; ItemInfo(type, id, out n); if (string.IsNullOrEmpty(n)) return ""; int i = n.IndexOf('\n'); return i >= 0 ? n.Substring(0, i) : n; }
        public Texture2D IconTexture(IconElement e) { return IconTex(e); }
        public string ItemComment(int type, int id)
        {
            if (tables == null || id <= 0) return "";
            try
            {
                if (type == 9) { var t = tables.m_pChestItemTable; var it = t.m_pItemData[id]; return t.m_pItemStringData[it.m_iItemName].m_strComment ?? ""; }
                if (type > 6 && type != 10) { var t = tables.m_pGeneralItemTable; var it = t.m_pItemData[id]; return t.m_pItemStringData[it.m_iItemName].m_strComment ?? ""; }
            }
            catch (Exception) { }
            return "";
        }
        public Texture ItemTexture(int type, int id) { string n; return RewardIcon(type, id, out n); }
        /// <summary>Objeto combinado (opt>>16 = objeto de aspecto, solo tipos 0..3): devuelve el id del aspecto o el propio id.</summary>
        int LookId(int type, int id, int opt)
        {
            int look = (opt >> 16) & 0xFFFF; if (look <= 0 || type < 0 || type > 3 || tables == null) return id;
            EquipmentItemTableScript tb = type == 0 ? tables.m_pWeaponItemTable : type == 1 ? tables.m_pShirtsItemTable : type == 2 ? tables.m_pPantsItemTable : tables.m_pShoesItemTable;
            if (tb == null || tb.m_pItemData == null || look >= tb.m_pItemData.Length || tb.m_pItemData[look] == null) return id; return look;
        }
        public Texture ItemTexture(int type, int id, int opt) { string n; return RewardIcon(type, LookId(type, id, opt), out n); }
        public string ItemName(int type, int id, int opt) { return ItemName(type, LookId(type, id, opt)); }

        // ---- popup de objeto usado por otras ventanas (inventario / E-Mail): misma ficha que la recompensa, con otro titulo ----
        string popTitle = "Reward"; bool extPopup; public Rect ExtRect;
        public bool ExternalOpen { get { return popOpen && extPopup; } }
        public void CloseExternal() { if (extPopup) { popOpen = false; extPopup = false; } }
        /// <summary>Muestra la ficha del objeto (tipo/id/opt) y, si es equipo, la del objeto equipado y del arma de reserva.</summary>
        public void ShowItemExternal(int type, int id, int opt, string title, int timeLimit = 0)
        {
            if (!texTried) { texTried = true; LoadTextures(); }
            if (selfApp == null && ents != null && ents.Self != null) { }
            try
            {
                extPopup = true; popTitle = title; popOpen = true; equipItem.Icon = null; backupItem.Icon = null; SetData(rewardItem, type, id, opt); rewardItem.TimeLimit = timeLimit; rewardItem.Shop = title == "For Sale";
                if (rewardItem.Icon == null) { popOpen = false; extPopup = false; return; }
                if ((type > 6 && type != 10) || selfApp == null || selfApp.Equip == null) return;
                int sl = type == 10 ? 8 : type;
                if (sl >= 0 && sl < selfApp.Equip.Length && selfApp.Equip[sl].Id > 0 && !(selfApp.Equip[sl].Id == id && selfApp.Equip[sl].Opt == opt)) { SetData(equipItem, type, selfApp.Equip[sl].Id, selfApp.Equip[sl].Opt); equipItem.TimeLimit = selfApp.Equip[sl].TimeLimit; }   // el objeto equipado no se compara consigo mismo
                if (type == 0 && selfApp.Equip.Length > 7 && selfApp.Equip[7].Id > 0 && selfApp.Equip[7].Id != id) SetData(backupItem, type, selfApp.Equip[7].Id, 1);
            }
            catch (Exception ex) { Debug.LogWarning("[inv] ficha: " + ex.Message); popOpen = false; extPopup = false; }
        }
        /// <summary>Dibuja la ficha dentro del grupo de ventana de 1036x654 de quien la llama.</summary>
        public void DrawExternal(Event e) { if (popOpen && extPopup) DrawPopup(e); if (!popOpen) extPopup = false; }
        Texture RewardIcon(int type, int id, out string name)
        {   // si falta el grafico del icono se usa un marcador para que el objeto siga visible
            var t = ItemInfo(type, id, out name); if (t == null && type != 8 && id > 0 && name != null && name.Length > 1) { if (placeholder == null) { placeholder = new Texture2D(2, 2); for (int a = 0; a < 4; a++) placeholder.SetPixel(a % 2, a / 2, new Color(0.35f, 0.45f, 0.55f, 1f)); placeholder.Apply(); } t = placeholder; }
            return t;
        }
        Texture ItemInfo(int type, int id, out string name)
        {
            name = "\n"; if (id <= 0 || tables == null) return null;
            if (type <= 6 || type == 10)
            {
                EquipmentItemTableScript t = null;
                switch (type) { case 0: t = tables.m_pWeaponItemTable; break; case 1: t = tables.m_pShirtsItemTable; break; case 2: t = tables.m_pPantsItemTable; break; case 3: t = tables.m_pShoesItemTable; break; case 4: t = tables.m_pHatItemTable; break; case 5: t = tables.m_pGlassItemTable; break; case 6: t = tables.m_pBackItemTable; break; case 10: t = tables.m_pVehicleItemTable; break; }
                if (t == null || t.m_pItemData == null || id >= t.m_pItemData.Length || t.m_pItemData[id] == null) return null; var it = t.m_pItemData[id];
                var sd = t.m_pItemStringData; if (sd != null && it.m_iItemName >= 0 && it.m_iItemName < sd.Length && sd[it.m_iItemName] != null) name = sd[it.m_iItemName].m_strName + "\n" + Loc.T("Level") + " " + it.m_iMinReqLev;
                var ic = t.m_pItemIconData; return ic != null && it.m_iIcon >= 0 && it.m_iIcon < ic.Length ? IconTex(ic[it.m_iIcon]) : null;
            }
            switch (type)
            {
                case 8:
                {   // objeto de mision: el original devuelve el nombre y ningun icono
                    var t = tables.m_pQuestItemTable; if (t != null && t.m_pItemData != null && id < t.m_pItemData.Length && t.m_pItemData[id] != null && t.m_pItemStringData != null) { int ni = t.m_pItemData[id].m_iItemName; if (ni >= 0 && ni < t.m_pItemStringData.Length && t.m_pItemStringData[ni] != null) name = t.m_pItemStringData[ni].m_strName + "\n"; }
                    return null;
                }
                case 9:
                {
                    var t = tables.m_pChestItemTable; if (t == null || t.m_pItemData == null || id >= t.m_pItemData.Length || t.m_pItemData[id] == null) return null; var it = t.m_pItemData[id];
                    if (t.m_pItemStringData != null && it.m_iItemName >= 0 && it.m_iItemName < t.m_pItemStringData.Length && t.m_pItemStringData[it.m_iItemName] != null) name = t.m_pItemStringData[it.m_iItemName].m_strName + "\n";
                    return t.m_pItemIconData != null && it.m_iIcon >= 0 && it.m_iIcon < t.m_pItemIconData.Length ? IconTex(t.m_pItemIconData[it.m_iIcon]) : null;
                }
                case 19:
                {
                    var t = tables.m_pNanoTable; if (t == null || t.m_pNanoData == null || id >= t.m_pNanoData.Length || t.m_pNanoData[id] == null) return null; var it = t.m_pNanoData[id];
                    if (t.m_pNanoStringData != null && it.m_iNanoName >= 0 && it.m_iNanoName < t.m_pNanoStringData.Length && t.m_pNanoStringData[it.m_iNanoName] != null) name = t.m_pNanoStringData[it.m_iNanoName].m_strName + "\n";
                    return t.m_pNanoIconData != null && it.m_iIcon1 >= 0 && it.m_iIcon1 < t.m_pNanoIconData.Length ? IconTex(t.m_pNanoIconData[it.m_iIcon1]) : null;
                }
                case 30:
                {
                    var t = npcT; if (t == null || t.m_pNpcData == null || id >= t.m_pNpcData.Length || t.m_pNpcData[id] == null) return null; var it = t.m_pNpcData[id];
                    return t.m_pNpcIconData != null && it.m_iIcon1 >= 0 && it.m_iIcon1 < t.m_pNpcIconData.Length ? IconTex(t.m_pNpcIconData[it.m_iIcon1]) : null;
                }
                default:
                {
                    var t = tables.m_pGeneralItemTable; if (t == null || t.m_pItemData == null || id >= t.m_pItemData.Length || t.m_pItemData[id] == null) return null; var it = t.m_pItemData[id];
                    if (t.m_pItemStringData != null && it.m_iItemName >= 0 && it.m_iItemName < t.m_pItemStringData.Length && t.m_pItemStringData[it.m_iItemName] != null) name = t.m_pItemStringData[it.m_iItemName].m_strName + "\n";
                    return t.m_pItemIconData != null && it.m_iIcon >= 0 && it.m_iIcon < t.m_pItemIconData.Length ? IconTex(t.m_pItemIconData[it.m_iIcon]) : null;
                }
            }
        }

        // ============================================================ helpers de dibujo ============================================================
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        static Color Mul(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }
        static readonly Color Yellow = Color.yellow, Pale = new Color(0.8f, 1f, 1f), Sub = new Color(0f, 0.4f, 0.6f), Dark = new Color(0f, 0.19f, 0.4f);

        /// <summary>GUI.Label con estilo y GUI.color = tint.</summary>
        void Txt(Rect r, string t, SkinStyle st, Color tint, TextAnchor? an = null, bool? wrap = null)
        {
            if (st == null || st.Font == null || string.IsNullOrEmpty(t) || Event.current.type != EventType.Repaint) return;
            st.Font.Draw(r, t, an ?? st.Align, wrap ?? st.Wrap, Mul(st.cN, tint), st.Padding);
        }
        bool Toggle(Rect r, bool v, SkinStyle st)
        {
            var e = Event.current; bool hov = r.Contains(e.mousePosition);
            if (e.type == EventType.MouseDown && e.button == 0 && hov) { Sfx.Button(); e.Use(); return !v; }
            if (e.type == EventType.Repaint && st != null) { var t = v ? (st.ON ?? st.N) : (hov && st.H != null ? st.H : st.N); if (t != null) GUI.DrawTexture(r, t); }
            return v;
        }
        void DrawIconText(Rect r, Texture icon, string text, SkinStyle st, Color tint)
        {
            float x = r.x; if (icon != null && Event.current.type == EventType.Repaint) { float h = Mathf.Min(icon.height, r.height); GUI.DrawTexture(new Rect(x, r.y + (r.height - h) * 0.5f, icon.width * h / icon.height, h), icon); }
            float off = icon != null ? icon.width + 4f : 0f; Txt(new Rect(r.x + off, r.y, r.width - off, r.height), text, st, tint, TextAnchor.MiddleLeft);
        }

        /// <summary>Area con scroll (GUILayout.BeginScrollView): rueda del raton + barra arrastrable. Devuelve el nuevo desplazamiento; el contenido se dibuja con draw(offsetY) dentro de un grupo recortado.</summary>
        float ScrollArea(Rect view, float scroll, float contentH, Action<float> draw, bool allowWheel = true)
        {
            var e = Event.current; float max = Mathf.Max(0f, contentH - view.height); bool bar = max > 0f;
            if (allowWheel && e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) { scroll = Mathf.Clamp(scroll + e.delta.y * 20f, 0f, max); e.Use(); }
            scroll = Mathf.Clamp(scroll, 0f, max);
            GUI.BeginGroup(view); draw(scroll); GUI.EndGroup();
            if (bar)
            {
                var track = new Rect(view.xMax - 16f, view.y, 16f, view.height); var tb = Skin.Get(SKR, "verticalScrollbar"); var th = Skin.Get(SKR, "verticalScrollbarThumb");
                if (e.type == EventType.Repaint) { if (tb != null && tb.N != null) GUI.DrawTexture(track, tb.N); }
                float frac = view.height / contentH, thumbH = Mathf.Max(16f, track.height * frac), ty = track.y + (track.height - thumbH) * (max > 0f ? scroll / max : 0f); var thumb = new Rect(track.x, ty, 16f, thumbH);
                if (e.type == EventType.Repaint && th != null && th.N != null) GUI.DrawTexture(thumb, th.N);
                int id = GUIUtility.GetControlID(FocusType.Passive, track);
                if (e.type == EventType.MouseDown && e.button == 0 && track.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
                if (GUIUtility.hotControl == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
                { float t = Mathf.Clamp01((e.mousePosition.y - track.y - thumbH * 0.5f) / Mathf.Max(1f, track.height - thumbH)); scroll = t * max; e.Use(); }
                else if (GUIUtility.hotControl == id && e.type == EventType.MouseUp) { GUIUtility.hotControl = 0; e.Use(); }
            }
            return scroll;
        }

        // ============================================================ ventana ============================================================
        void OnGUI()
        {
            if (!open) return; var e = Event.current; if (e == null || Screen.height <= 0) return;
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f);
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
                GUI.BeginGroup(wr);
                bool en = !confirmDelete && !popOpen && notice == null;
                DrawWindow(e, en);
                if (confirmDelete) DrawConfirm(e);
                if (notice != null) DrawNotice(e);
                if (popOpen) DrawPopup(e);
                GUI.EndGroup();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();   // ventana modal
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        void DrawWindow(Event e, bool en)
        {
            var box = Skin.Get(SKL, "box"); Skin.Box(WindowRect, box, "Mission Journal");
            DrawLeft(e, en); DrawRight(e, en);
        }

        // ---- panel izquierdo (DoWindowLeft) ----
        void DrawLeft(Event e, bool en)
        {
            if (cur == null) { Skin.Box(LeftBox, Skin.Get(SKL, "EmptyBox"), "No mission selected"); return; }
            bool off = mode != 0; Rect grp = off ? OfferDlg : ActiveDlg; float oy = off ? 10f : 0f;   // offerskipvec = (0, 10)
            var dlg = Skin.Get(SKL, off ? "offerdlg" : tab == 1 ? "enddlg" : "activedlg"); if (dlg != null && e.type == EventType.Repaint) Skin.Bg(grp, dlg.N, dlg);
            GUI.BeginGroup(grp);
            if (off) Skin.Box(new Rect(16f, 2f, 567f, 630f), Skin.Get(SKL, "OfferBack"), Loc.T(mode == 1 ? "MISSION OFFER" : "MISSION COMPLETION"));
            Rect Sk(Rect r) { return new Rect(r.x, r.y + oy, r.width, r.height); }
            // banner
            int bi = cur.m_iHMissionType == 1 ? Mathf.Clamp(cur.m_iCSTReqGuide - 1, 0, 4) : cur.m_iHMissionType == 2 ? 5 : 6;
            if (banner[bi] != null && e.type == EventType.Repaint) GUI.DrawTexture(off ? OfferBar : ActiveBar, banner[bi]);
            var big16 = Skin.Get(SKL, "bigfont16"); var big14 = Skin.Get(SKL, "bigfont14"); var ta = Skin.Get(SKL, "TextArea"); var lab = Skin.Get(SKL, "label");
            Txt(Sk(MissionName), title, big16, Yellow);
            Txt(Sk(MissionLevel), difficulty, ta, Pale);
            if (tab == 1)
            {
                Txt(MyNote, Loc.T("MY NOTES") + ":", ta, Sub, TextAnchor.UpperLeft);
                DrawScrollText(NoteSub, taskDescription + "\n", lab, Dark, ref subScroll);
            }
            else if (mode == 1)
            {
                Txt(Sk(MissionDet), Loc.T("MISSION OFFER") + ":", ta, Sub, TextAnchor.UpperLeft);
                DrawScrollText(Sk(MissionSub), taskDescription + "\n", lab, Dark, ref subScroll);
            }
            else if (mode == 2)
            {
                Txt(Sk(MissionDet), Loc.T("MISSION SUMMARY") + ":", ta, Sub, TextAnchor.UpperLeft);
                DrawScrollText(Sk(MissionSub), description + "\n", lab, Dark, ref subScroll);
            }
            else
            {
                Txt(MissionDet, Loc.T("MISSION DETAILS") + ":", ta, Sub, TextAnchor.UpperLeft);
                string text; int sel = nh != null ? nh.JournalSelected : 0;
                if (sel != 0 && sel == cur.m_iHMissionID) text = nh.JournalProgress(cur.m_iHMissionID) + "\n\n" + taskDescription + "\n"; else text = taskDescription + "\n";
                DrawScrollText(MissionSub, text, lab, Dark, ref subScroll);
            }
            // NPC
            var ni = Skin.Get(SKL, "npcicon"); var nbx = Sk(NpcBox);
            if (ni != null && e.type == EventType.Repaint) { Skin.Bg(nbx, ni.N, ni); if (npcTex != null) GUI.DrawTexture(new Rect(nbx.x + 4f, nbx.y + 5f, nbx.width - 8f, nbx.height - 8f), npcTex); }
            Txt(Sk(NpcName), npcName, ta, Yellow); Txt(Sk(NpcPos), npcLoc, big14, Pale, TextAnchor.UpperLeft);
            if (cur.m_iHMissionType == 2) DrawNano(e);
            else DrawRewards(e);
            // botones
            if (mode == 1)
            {
                if (Skin.Button(new Rect(16f, 578f, 190f, 25f), Loc.T("DECLINE"), Skin.Get(SKL, "RedButton"), en)) { Sfx.Ui("Mission_Decline"); EndMode(); }
                if (Skin.Button(new Rect(339f, 551f, 218f, 85f), Loc.T("ACCEPT MISSION"), Skin.Get(SKL, "acceptbut"), en && busy <= 0f)) Accept();
            }
            else if (mode == 2)
            {
                if (Skin.Button(new Rect(16f, 578f, 190f, 25f), Loc.T("CANCEL"), Skin.Get(SKL, "CancelButton"), en)) { Sfx.Button(); EndMode(); }
                if (Skin.Button(new Rect(319f, 551f, 233f, 70f), Loc.T("COMPLETE MISSION"), Skin.Get(SKL, "acceptbut"), en && busy <= 0f)) Complete();
            }
            else if (tab == 0)
            {
                if (Skin.Button(DelBut, Loc.T("DELETE MISSION"), Skin.Get(SKL, "RedButton"), en && cooldown <= 0f)) { Sfx.Ui("Abandon_Mission"); confirmDelete = true; }
                int sel = nh != null ? nh.JournalSelected : 0;
                if (sel != cur.m_iHMissionID && Skin.Button(MakeCurBut, Loc.T("MAKE CURRENT MISSION"), Skin.Get(SKL, "button"), en)) { if (nh != null) nh.JournalSelect(cur.m_iHMissionID); }
            }
            GUI.EndGroup();
        }

        void DrawScrollText(Rect view, string text, SkinStyle st, Color tint, ref float scroll)
        {
            if (st == null || st.Font == null) return; float w = view.width; float h = st.Font.Height(text, w - 16f); bool bar = h > view.height; float tw = bar ? w - 16f : w; if (bar) h = st.Font.Height(text, tw);
            float sc = scroll; var font = st.Font; var col = Mul(st.cN, tint); var pad = st.Padding;
            scroll = ScrollArea(view, scroll, h, off => { if (Event.current.type == EventType.Repaint) font.Draw(new Rect(0f, -off, tw, h + 4f), text, TextAnchor.UpperLeft, true, col, pad); }, true);
            // ScrollArea dibuja el grupo en 'view' (no incluye el texto fuera del recorte)
        }

        void DrawNano(Event e)
        {
            var big14 = Skin.Get(SKL, "bigfont14"); var big8 = Skin.Get(SKL, "bigfont8"); var lab = Skin.Get(SKL, "label"); var nb = Skin.Get(SKL, "NanoBox");
            float baseY = tab == 0 ? 290f : 317f;
            Txt(new Rect(15f, baseY, 200f, 30f), Loc.T("NANO") + ":", big14, Dark, TextAnchor.UpperLeft);
            GUI.BeginGroup(new Rect(5f, baseY + 17f, 555f, 245f));
            if (nanoTex != null && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(25f, 10f, 128f, 128f), nanoTex, ScaleMode.ScaleToFit);
            Txt(new Rect(10f, 150f, 160f, 20f), nanoName, big14, Color.white, TextAnchor.UpperLeft);
            Txt(new Rect(10f, 165f, 160f, 20f), nanoAttrib, big8, Color.white, TextAnchor.UpperLeft);
            Txt(new Rect(10f, 175f, 160f, 50f), nanoDesc, big8, Color.white, TextAnchor.UpperLeft);
            for (int k = 0; k < 3; k++)
            {
                if (nb != null && e.type == EventType.Repaint) Skin.Bg(new Rect(206f, k * 80 + 5, 330f, 75f), nb.N, nb);
                if (e.type == EventType.Repaint) { if (skillBack != null) GUI.DrawTexture(new Rect(210f, k * 80 + 10, 28f, 28f), skillBack); if (skillTex[k] != null) GUI.DrawTexture(new Rect(208f, k * 80 + 8, 32f, 32f), skillTex[k]); }
                Txt(new Rect(248f, k * 80 + 13, 230f, 20f), skillName[k], big14, Color.white, TextAnchor.UpperLeft);
                Txt(new Rect(248f, k * 80 + 27, 230f, 15f), skillType[k], big8, Color.white, TextAnchor.UpperLeft);
                Txt(new Rect(216f, k * 80 + 45, 310f, 25f), skillDesc[k], lab, Color.white, TextAnchor.UpperLeft);
            }
            GUI.EndGroup();
        }

        void DrawRewards(Event e)
        {
            if (reward == null) return; var big14 = Skin.Get(SKL, "bigfont14"); var tf = Skin.Get(SKL, "TextField"); var lab = Skin.Get(SKL, "label"); var ta = Skin.Get(SKL, "TextArea"); var slot = Skin.Get(Skin.Inven, "slotbox");
            Rect title = mode != 0 ? new Rect(Rewards.x, Rewards.y + 10f, Rewards.width, Rewards.height) : tab == 0 ? Rewards : EndReward, win = mode != 0 ? new Rect(RewardsWin.x, RewardsWin.y + 10f, 577f, 225f) : tab == 0 ? RewardsWin : EndRewardWin;
            Txt(title, Loc.T("Reward") + ":", big14, Dark, TextAnchor.UpperLeft);
            float h = 0f; if (rwIcon[0] != null) h += 110f; if (rwIcon[2] != null) h += 80f; if (reward.m_iCash > 0 && reward.m_iFusionMatter > 0) h += 80f; if (rwIcon2[0] != null) h += 110f; if (rwIcon2[2] != null) h += 80f; h = Mathf.Max(h, 80f);
            rewardScroll = ScrollArea(win, rewardScroll, h, off => DrawRewardContent(off, tf, slot, big14, ta, lab));
        }
        void DrawRewardContent(float off, SkinStyle tf, SkinStyle slot, SkinStyle big14, SkinStyle ta, SkinStyle lab)
        {
            var e = Event.current; bool rep = e.type == EventType.Repaint; float y = -off;
            if (rwIcon[0] != null)
            {
                Txt(new Rect(10f, y + 5f, 500f, 20f), Loc.T(reward.m_iBox1Choice <= 0 ? "You will receive the following items" : "You can choose one of the following items") + ":", lab, Sub, TextAnchor.UpperLeft);
                y += 30f;
                for (int l = 0; l < 4; l++)
                {
                    if (rwIcon[l] == null) continue; int cx = l % 2, cy = l / 2; var box = new Rect(cx * 260 + 10, cy * 80 + y, 250f, 76f); var ic = new Rect(cx * 260 + 20, cy * 80 + y + 6f, 64f, 64f);
                    if (reward.m_iBox1Choice > 0) SelToggle(new Rect(cx * 260 + 223, cy * 80 + y + 48, 22f, 22f), sel1, l, reward.m_iBox1Choice);
                    if (Skin.Button(box, "", tf, !popOpen)) SetPopupItem(0, l); if (rep) { Skin.Bg(ic, slot != null ? slot.N : null, slot); GUI.DrawTexture(ic, rwIcon[l]); }
                    string[] nm = rwName[l].Split('\n');
                    Txt(new Rect(cx * 260 + 90, cy * 80 + y + 6f, 170f, 40f), nm[0], big14, Color.white, TextAnchor.UpperLeft);
                    Txt(new Rect(cx * 260 + 90, cy * 80 + y + 50f, 170f, 40f), nm.Length > 1 ? nm[1] : "", ta, Yellow, TextAnchor.UpperLeft);
                }
                if (rwIcon[0] != null) y += 80f; if (rwIcon[2] != null) y += 80f;
            }
            if (reward.m_iCash > 0 || reward.m_iFusionMatter > 0)
            {
                int c = 0;
                if (reward.m_iFusionMatter > 0) { DrawMoneyBox(c, y, fm, reward.m_iFusionMatter.ToString(), "FUSION MATTER", tf, slot, big14, ta); c++; }
                if (reward.m_iCash > 0) DrawMoneyBox(c, y, taros, reward.m_iCash.ToString(), "TAROS", tf, slot, big14, ta);
                y += 80f;
            }
            if (rwIcon2[0] != null)
            {
                Txt(new Rect(5f, 5f + y, 500f, 20f), Loc.T(reward.m_iBox2Choice <= 0 ? "You will receive the following items" : "You can choose one the following items") + ":", lab, Sub, TextAnchor.UpperLeft);
                y += 30f;
                for (int m = 0; m < 4; m++)
                {
                    if (rwIcon2[m] == null) continue; int cx = m % 2, cy = m / 2; var box = new Rect(cx * 260 + 10, cy * 80 + y, 250f, 76f); var ic = new Rect(cx * 260 + 20, cy * 80 + y + 6f, 64f, 64f);
                    if (reward.m_iBox2Choice > 0) SelToggle(new Rect(cx * 260 + 223, cy * 80 + y + 48, 22f, 22f), sel2, m, reward.m_iBox2Choice);
                    if (Skin.Button(box, "", tf, !popOpen)) SetPopupItem(1, m); if (rep) { Skin.Bg(ic, slot != null ? slot.N : null, slot); GUI.DrawTexture(ic, rwIcon2[m]); }
                    string[] nm = rwName2[m].Split('\n');
                    Txt(new Rect(cx * 260 + 90, cy * 80 + y + 6f, 170f, 40f), nm[0], big14, Color.white, TextAnchor.UpperLeft);
                    Txt(new Rect(cx * 260 + 90, cy * 80 + y + 50f, 170f, 40f), nm.Length > 1 ? nm[1] : "", ta, Color.white, TextAnchor.UpperLeft);
                }
            }
        }
        void SelToggle(Rect r, bool[] sel, int idx, int max)
        {   // GUI.Toggle de recompensa: con un limite de 1 elegir otra sustituye a la anterior; si no, no deja pasar del limite (en el original se valida al completar)
            var tg = Skin.Get(SKL, "toggle") ?? Skin.Get(SKR, "iconToggle"); bool v = Toggle(r, sel[idx], tg); if (v == sel[idx]) return;
            if (v && mode == 2) { if (max == 1) for (int k = 0; k < sel.Length; k++) sel[k] = false; else if (Count(sel) >= max) return; }
            sel[idx] = v;
        }
        void DrawMoneyBox(int c, float y, Texture icon, string value, string label, SkinStyle tf, SkinStyle slot, SkinStyle big14, SkinStyle ta)
        {
            var box = new Rect(c * 260 + 10, y, 250f, 76f); var ic = new Rect(c * 260 + 20, y + 6f, 64f, 64f);
            if (Event.current.type == EventType.Repaint) { Skin.Bg(box, tf != null ? tf.N : null, tf); Skin.Bg(ic, slot != null ? slot.N : null, slot); if (icon != null) GUI.DrawTexture(ic, icon); }
            Txt(new Rect(c * 260 + 90, y + 6f, 170f, 40f), value, big14, Color.white, TextAnchor.UpperLeft);
            Txt(new Rect(c * 260 + 90, y + 16f, 170f, 40f), Loc.T(label), ta, Color.white, TextAnchor.UpperLeft);
        }

        // ============================================================ popup de objeto ============================================================
        SkinStyle PS(string n) { return Skin.Get(Skin.Menu, n) ?? Skin.Get(SKL, n); }
        void SetPopupItem(int box, int slot)
        {
            if (reward == null) return; int id, type;
            if (box == 0) { id = At(reward.m_iMissionRewardItemID, slot); type = At(reward.m_iMissionRewarItemType, slot); }
            else { id = At(reward.m_iMissionRewardItemID2, slot); type = At(reward.m_iMissionRewardItemType2, slot); }
            extPopup = false; popTitle = "Reward"; popOpen = true; equipItem.Icon = null; backupItem.Icon = null; SetData(rewardItem, type, id, 1);
            if (type > 6 && type != 10) return;
            if (selfApp == null || selfApp.Equip == null) return;
            int sl = type == 10 ? 8 : type;
            if (sl >= 0 && sl < selfApp.Equip.Length && selfApp.Equip[sl].Id > 0) SetData(equipItem, type, selfApp.Equip[sl].Id, selfApp.Equip[sl].Opt);
            if (type == 0 && selfApp.Equip.Length > 7 && selfApp.Equip[7].Id > 0) SetData(backupItem, type, selfApp.Equip[7].Id, 1);
        }
        static string Rate(ItemElement it)
        {
            switch (it.m_iEquipType)
            {
                case 0: case 1: case 6: return Loc.T("Short");
                case 3: case 4: case 5: case 8: case 9: case 10: return Loc.T("Medium");
                case 2: case 7: case 11: return Loc.T("Long");
            }
            return it.m_iDelayTime > 0 ? (1f / (it.m_iDelayTime * 0.1f)).ToString("0.00") : "";
        }
        void SetData(ItemDisp d, int type, int id, int opt)
        {   // cnItemDisplayInfo.SetData
            string nm; int look = LookId(type, id, opt); d.Icon = RewardIcon(type, look, out nm); if (d.Icon == null || tables == null) { d.Icon = null; return; }
            d.Type = type; d.Id = id; d.Opt = opt; d.TimeLimit = 0; d.Shop = false; d.Level = 0; d.Name = ""; d.Desc = ""; d.TypeName = ""; d.Rate = ""; d.Rarity = ""; d.Trade = "";
            if (type > 6 && type != 10)
            {
                if (type == 9)
                {
                    var t = tables.m_pChestItemTable; var it = t.m_pItemData[id]; var sd = t.m_pItemStringData[it.m_iItemName];
                    d.Name = sd.m_strName; d.Desc = sd.m_strComment; d.Trade = Loc.T(it.m_iTradeAble != 0 ? "Tradable" : "Not tradable");
                }
                else
                {
                    var t = tables.m_pGeneralItemTable; var it = t.m_pItemData[id]; var sd = t.m_pItemStringData[it.m_iItemName];
                    d.Name = sd.m_strName; d.Desc = sd.m_strComment; d.Trade = Loc.T(it.m_iTradeAble != 0 ? "Tradable" : "Not tradable");
                }
                return;
            }
            EquipmentItemTableScript tb = null; string tn = "";
            switch (type)
            {
                case 0: tb = tables.m_pWeaponItemTable; tn = "Weapon"; break; case 1: tb = tables.m_pShirtsItemTable; tn = "Body"; break;
                case 2: tb = tables.m_pPantsItemTable; tn = "Legs"; break; case 3: tb = tables.m_pShoesItemTable; tn = "Shoes"; break;
                case 4: tb = tables.m_pHatItemTable; tn = "Hat"; break; case 5: tb = tables.m_pGlassItemTable; tn = "Glasses"; break;
                case 6: tb = tables.m_pBackItemTable; tn = "Backpack"; break; case 10: tb = tables.m_pVehicleItemTable; tn = "Vehicle"; break;
            }
            if (tb == null) return; var e = tb.m_pItemData[id]; var s2 = tb.m_pItemStringData[e.m_iItemName];
            d.Level = e.m_iMinReqLev; d.Point = e.m_iPointRat; d.Group = e.m_iGroupRat; d.Defense = e.m_iDefenseRat; d.Name = s2.m_strName; d.Desc = s2.m_strComment;
            d.TypeName = Loc.T(tn); d.Rate = type == 0 ? Rate(e) : type == 10 ? e.m_iUp_runSpeed + " Class" : Loc.T("N/A"); d.Trade = Loc.T(e.m_iTradeAble != 0 ? "Tradable" : "Not tradable");
            if (type == 0) { string[] wn = { null, "Melee", "Pistol", "Shattergun", "Rifle", "Rocket", "Thrown" }; if (e.m_iTargetMode >= 1 && e.m_iTargetMode <= 6) d.TypeName = Loc.T(wn[e.m_iTargetMode]); }
            if (look != id)
            {   // objeto combinado: nombre, descripcion y aspecto del objeto de aspecto; no intercambiable; rareza especial
                var ls = tb.m_pItemStringData[tb.m_pItemData[look].m_iItemName]; d.Name = ls.m_strName; d.Desc = ls.m_strComment; d.Trade = Loc.T("Not tradable"); d.Rarity = Loc.T("Special"); return;
            }
            switch (e.m_iRarity) { case 1: d.Rarity = Loc.T("Common"); break; case 2: d.Rarity = Loc.T("Uncommon"); break; case 3: d.Rarity = Loc.T("Rare"); break; case 4: d.Rarity = Loc.T("Ultra rare"); break; case 5: d.Rarity = Loc.T("Amazing!"); break; }
        }
        void DrawPopup(Event e)
        {   // DoWindowPopUP
            bool eq = rewardItem.Type <= 6 || rewardItem.Type == 10; var baseR = eq ? ItemPopRect : ItemPopRectSmall; var w = baseR;
            int n = (rewardItem.Icon != null ? 1 : 0) + (equipItem.Icon != null ? 1 : 0) + (backupItem.Icon != null ? 1 : 0); if (n < 1) n = 1;
            w.y = (WindowRect.height - baseR.height) / 2f; w.x = (WindowRect.width - (baseR.width * n + 15f * (n - 1))) / 2f;
            ExtRect = extPopup && eq ? new Rect(w.x, w.y, w.width, w.height - 54f) : w;   // en el original los botones de accion (equipar, vender, comprar...) van dentro de la ventana, a y+394
            DrawInfo(rewardItem, equipItem, w, Loc.T(popTitle));
            if (equipItem.Icon != null) { w.x += w.width + 15f; DrawInfo(equipItem, null, w, Loc.T("Equipped")); }
            if (backupItem.Icon != null) { w.x += w.width + 15f; DrawInfo(backupItem, null, w, Loc.T("Backup")); }
        }
        static Color Cmp(int other, int mine) { if (other <= -1000) return Color.white; return other > mine ? Color.red : other == mine ? Color.white : Color.green; }
        static string TzName(DateTime local)
        {
            try { var off = TimeZoneInfo.Local.GetUtcOffset(local); return "UTC" + (off < TimeSpan.Zero ? "-" : "+") + Math.Abs(off.Hours).ToString("00") + ":" + Math.Abs(off.Minutes).ToString("00"); }
            catch (Exception) { return "UTC"; }
        }
        void DrawInfoExt(ItemDisp it, ItemDisp cmp, Rect w, string title)
        {   // UnequipPopup / EquipPopup.DrawPopup con el skin del inventario: ventana 310x448, titulo fuera del grupo interno (0,13,310,435)
            var e = Event.current; bool veh = it.Type == 10, eq = it.Type <= 6 || veh; var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (e.type == EventType.Repaint)
            {
                if (eq) { if (popBack != null) GUI.DrawTexture(w, popBack); }
                else if (popSmall != null) GUI.DrawTexture(w, popSmall);
            }
            var lab = Skin.Get(Skin.Inven, "label") ?? PS("label"); var left = Skin.Get(Skin.Inven, "leftLabel") ?? lab; var slot = Skin.Get(Skin.Inven, "slotbox");
            var rl = Skin.Get(Skin.Inven, "rightLabel") ?? PS("RightLabel"); var cl = Skin.Get(Skin.Inven, "centerLabel") ?? PS("CenterLabel"); var ybig = Skin.Get(Skin.Inven, "ybiglabel") ?? lab;
            var cyan = new Color(0.78f, 1f, 1f);
            GUI.BeginGroup(w);
            Txt(new Rect(10f, -2f, 100f, 20f), title, lab, Color.black, TextAnchor.UpperLeft);
            if (eq && !veh && equipTex != null && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(2f, 170f, 305f, 84f), equipTex);
            GUI.BeginGroup(new Rect(0f, 13f, 310f, 435f));
            if (Skin.Button(new Rect(277f, 0f, 32f, 32f), "", PS("CloseButton"), true)) popOpen = false;
            if (e.type == EventType.Repaint) { Skin.Bg(new Rect(16f, 16f, 64f, 64f), slot != null ? slot.N : null, slot); GUI.DrawTexture(new Rect(16f, 16f, 64f, 64f), it.Icon); }
            string nm = it.Name;
            if (!veh && !it.Shop && it.TimeLimit > 0) { try { nm += "\n" + new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(it.TimeLimit).ToLocalTime().ToString(inv); } catch (Exception) { } }
            Txt(new Rect(82f, 16f, 170f, 40f), nm, lab, Color.white, TextAnchor.UpperLeft, true);
            if (eq) Txt(new Rect(82f, 50f, 170f, 20f), Loc.T("Level") + " " + it.Level, lab, Color.yellow, TextAnchor.UpperLeft);
            Txt(new Rect(12f, 95f, 280f, 40f), it.Desc, left, Color.white, TextAnchor.UpperLeft, true);
            if (eq && !veh)
            {
                bool c = cmp != null && cmp.Icon != null;
                Txt(new Rect(28f, 219f, 80f, 20f), it.Point.ToString(), lab, Cmp(c ? cmp.Point : -1000, it.Point), TextAnchor.MiddleCenter);
                Txt(new Rect(115f, 219f, 80f, 20f), it.Group.ToString(), lab, Cmp(c ? cmp.Group : -1000, it.Group), TextAnchor.MiddleCenter);
                Txt(new Rect(207f, 219f, 80f, 20f), it.Defense.ToString(), lab, Cmp(c ? cmp.Defense : -1000, it.Defense), TextAnchor.MiddleCenter);
            }
            else if (veh && it.TimeLimit > 0)
            {   // EquipPopup / UnequipPopup: RENTAL PERIOD + caducidad (en la tienda: duracion "desde la compra")
                Txt(new Rect(10f, 138f, 200f, 20f), Loc.T("RENTAL PERIOD"), lab, Color.white, TextAnchor.UpperLeft);
                try
                {
                    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); var at = epoch.AddSeconds(it.TimeLimit);
                    if (it.Shop)
                    {
                        var span = at - epoch; string t2 = "";
                        if (span.Days > 0) t2 += span.Days + " " + Loc.T("DAYS") + " ";
                        if (span.Hours > 0) t2 += span.Hours + " " + Loc.T("HOURS") + " ";
                        if (span.Minutes > 0) t2 += span.Minutes + " " + Loc.T("MINUTES") + " ";
                        if (span.Seconds > 0) t2 += span.Seconds + " " + Loc.T("SECONDS");
                        t2 += "\n" + Loc.T("From time of purchase");
                        Txt(new Rect(5f, 150f, 300f, 100f), t2, ybig, Color.white, TextAnchor.MiddleCenter);
                    }
                    else
                    {
                        var dt = at.ToLocalTime();
                        Txt(new Rect(5f, 140f, 300f, 100f), "Expires " + dt.ToString("MMMM d, yyyy", inv), ybig, Color.white, TextAnchor.MiddleCenter);
                        Txt(new Rect(5f, 160f, 300f, 100f), " At " + dt.ToString("hh:mm tt", inv) + " " + TzName(dt), ybig, Color.white, TextAnchor.MiddleCenter);
                    }
                }
                catch (Exception) { }
            }
            if (eq)
            {
                Txt(new Rect(10f, 238f, 200f, 20f), Loc.T("INFO"), lab, Color.white, TextAnchor.UpperLeft);
                string[] k = { "Type", veh ? "Speed" : "Range", "Rarity", "Trade Availability" }, v = { it.TypeName, it.Rate, it.Rarity, it.Trade };
                for (int i = 0; i < 4; i++)
                {
                    Txt(new Rect(8f, 256f + i * 20f, 130f, 20f), Loc.T(k[i]), rl, cyan);
                    Color vc = Color.white; if (i == 1 && !veh && v[i] == Loc.T("N/A")) vc = Color.blue; else if (i == 3) vc = v[i] == Loc.T("Not tradable") ? Color.red : Color.green;
                    Txt(new Rect(150f, 260f + i * 20f, 130f, 20f), v[i], cl, vc);
                }
            }
            GUI.EndGroup(); GUI.EndGroup();
        }
        void DrawInfo(ItemDisp it, ItemDisp cmp, Rect w, string title)
        {   // DoInfo
            if (it.Icon == null) return; if (extPopup) { DrawInfoExt(it, cmp, w, title); return; }
            var e = Event.current; bool eq = it.Type <= 6 || it.Type == 10;
            if (e.type == EventType.Repaint)
            {
                if (eq) { if (popBack != null) GUI.DrawTexture(w, popBack); if (it.Type != 10 && equipTex != null) GUI.DrawTexture(new Rect(w.x + 2f, w.y + 170f, 305f, 84f), equipTex); }
                else if (popSmall != null) GUI.DrawTexture(w, popSmall);
            }
            var b14 = PS("bigfont14"); var lab = PS("label"); var slot = Skin.Get(Skin.Inven, "slotbox"); var rl = PS("RightLabel"); var cl = PS("CenterLabel");
            GUI.BeginGroup(w);
            if (Skin.Button(ExitBut, "", PS("CloseButton"), true)) popOpen = false;
            Txt(PTitle, title, b14, Color.black, TextAnchor.UpperLeft);
            if (e.type == EventType.Repaint) { Skin.Bg(PIcon, slot != null ? slot.N : null, slot); GUI.DrawTexture(PIcon, it.Icon); }
            Txt(new Rect(PName.x, PName.y, PName.width, 40f), it.Name, b14, Color.white, TextAnchor.UpperLeft, true);
            if (eq) Txt(new Rect(PName.x, PName.y + (it.Name != null && it.Name.Length > 22 ? 36f : 18f), PName.width, 20f), Loc.T("level") + " " + it.Level, b14, Color.yellow, TextAnchor.UpperLeft);
            Txt(PDesc, it.Desc, lab, Color.white, TextAnchor.UpperLeft, true);
            if (eq)
            {
                bool c = cmp != null && cmp.Icon != null;
                Txt(POne, it.Point.ToString(), b14, Cmp(c ? cmp.Point : -1000, it.Point), TextAnchor.MiddleCenter);
                Txt(PGroup, it.Group.ToString(), b14, Cmp(c ? cmp.Group : -1000, it.Group), TextAnchor.MiddleCenter);
                Txt(PDef, it.Defense.ToString(), b14, Cmp(c ? cmp.Defense : -1000, it.Defense), TextAnchor.MiddleCenter);
                Txt(PStatus, Loc.T("Info"), b14, Color.white, TextAnchor.UpperLeft);
                Txt(new Rect(PInfo.x, PInfo.y, PInfo.width, 20f), Loc.T("STATS"), b14, Color.white, TextAnchor.UpperLeft);
                string[] k = { "Type", it.Type == 10 ? "Speed" : "Range", "Rarity", "Trade availability" }, v = { it.TypeName, it.Rate, it.Rarity, it.Trade };
                for (int i = 0; i < 4; i++)
                {
                    float y = 270f + i * 20f;   // las filas empiezan en la primera celda de la textura (y=270)
                    Txt(new Rect(PInfo.x, y, 130f, 20f), Loc.T(k[i]), rl, Color.white, TextAnchor.MiddleRight);
                    Color vc = Color.white; if (i == 1 && it.Type != 10 && v[i] == Loc.T("N/A")) vc = Color.blue; else if (i == 3) vc = v[i] == Loc.T("Not tradable") ? Color.red : Color.green;   // EquipPopup/UnequipPopup: solo Range N/A azul, Rarity blanco, Trade rojo/verde
                    Txt(new Rect(PInfo.x + 130f, y, 160f, 20f), v[i], cl, vc, TextAnchor.MiddleCenter);
                }
            }
            GUI.EndGroup();
        }

        // ---- panel derecho (DoWindowRight) ----
        void DrawRight(Event e, bool en)
        {
            if (Skin.Button(new Rect(WindowRect.width - 40f, RightBox.y, 32f, 32f), "", Skin.Get(SKR, "CloseButton"), en)) { GameMenu.CloseWindow(); return; }
            if (Skin.Button(new Rect(WindowRect.width - 50f, 608f, 30f, 30f), "", Skin.Get(SKR, "button"), en) && GameMenu.Implemented.Contains(GameMenu.Win.Guide)) { GameMenu.CloseWindow(); GameMenu.OpenWindow(GameMenu.Win.Guide); return; }
            if (mode != 0) Skin.Box(RightBox, Skin.Get(SKR, "AllowTab"), Loc.T("Active Mission List"));
            else if (tab == 0)
            {
                if (Skin.Button(new Rect(680f, 11f, 100f, 30f), Loc.T("Completed"), Skin.Get(SKR, "Tab2"), en)) { Sfx.Ui("Tab_Click01"); tab = 1; SetActive(true); }
                Skin.Box(RightBox, Skin.Get(SKR, "Active_Tab"), Loc.T("Active"));
            }
            else
            {
                if (Skin.Button(new Rect(597f, 11f, 100f, 30f), Loc.T("Active"), Skin.Get(SKR, "Tab1"), en)) { Sfx.Ui("Tab_Click01"); tab = 0; SetActive(true); }
                Skin.Box(RightBox, Skin.Get(SKR, "Compl_Tab"), Loc.T("Completed"));
            }
            GUI.BeginGroup(RightBox);
            var view = new Rect(11f, 40f, 368f, 558f);
            listScroll = ScrollArea(view, listScroll, listContentH, off => { listContentH = tab == 0 ? DrawActiveList(off, en && mode == 0) : DrawCompletedList(off, en); });
            Skin.Box(new Rect(5f, 32f, 350f, 571f), Skin.Get(SKR, "window"));
            GUI.EndGroup();
        }

        int GuideIcon { get { int g = nh != null ? nh.JournalGuide : 0; return g <= 0 ? 4 : Mathf.Clamp(g - 1, 0, 6); } }

        float DrawActiveList(float off, bool en)
        {
            var b16 = Skin.Get(SKR, "bigfont16"); float y = 5f - off;
            DrawIconText(new Rect(5f, y, 300f, 24f), missionIcon[5], " " + Loc.T("NANO MISSION"), b16, Color.white); Txt(new Rect(290f, y, 100f, 20f), nanoL.Count + "/1", b16, Color.white, TextAnchor.UpperLeft); y += 20f;
            y = RenderActive(nanoL, y, 1, en);
            DrawIconText(new Rect(5f, y, 300f, 24f), missionIcon[GuideIcon], " " + Loc.T("GUIDE MISSION"), b16, Color.white); Txt(new Rect(290f, y, 100f, 20f), guideL.Count + "/1", b16, Color.white, TextAnchor.UpperLeft); y += 20f;
            y = RenderActive(guideL, y, 1, en);
            DrawIconText(new Rect(5f, y, 300f, 24f), missionIcon[6], " " + Loc.T("WORLD MISSIONS"), b16, Color.white); Txt(new Rect(290f, y, 100f, 20f), worldL.Count + "/4", b16, Color.white, TextAnchor.UpperLeft); y += 20f;
            y = RenderActive(worldL, y, 4, en);
            return y + off + 10f;
        }

        float RenderActive(List<Entry> list, float y, int max, bool en)
        {
            var mb = Skin.Get(SKR, "MissionBack"); var tf = Skin.Get(SKR, "TextField"); var ta = Skin.Get(SKR, "TextArea"); var b14 = Skin.Get(SKR, "bigfont14"); var lab = Skin.Get(SKR, "label"); var itg = Skin.Get(SKR, "iconToggle"); var ni = Skin.Get(SKR, "npcicon");
            int sel = nh != null ? nh.JournalSelected : 0; var e = Event.current;
            foreach (var en2 in list)
            {
                bool flag = sel == en2.Me.m_iHMissionID; if (flag) { if (currentMissionTex != null && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(5f, y, 126f, 19f), currentMissionTex); y += 10f; }
                string obj = Str(en2.Me.m_iHCurrentObjective); bool isCur = cur != null && en2.Me.m_iHTaskID == cur.m_iHTaskID;
                if (isCur)
                {
                    var pos = new Rect(311f, y + 32f, 22f, 22f); bool t = Toggle(pos, flag, itg);
                    if (Skin.Button(new Rect(6f, y, 330f, 86f), "", mb, en)) SetCurrent(en2.Me);
                    if (ta != null && ta.N != null && e.type == EventType.Repaint) Skin.Bg(new Rect(3f, y - 3f, 336f, 92f), ta.N, ta);
                    if (e.type == EventType.Repaint && ni != null) { var r = new Rect(10f, y + 10f, 68f, 68f); Skin.Bg(r, ni.N, ni); if (npcTex != null) GUI.DrawTexture(new Rect(r.x + 4f, r.y + 5f, r.width - 8f, r.height - 8f), npcTex); }
                    Txt(new Rect(85f, y + 5f, 200f, 35f), en2.Name, Skin.Get(SKR, "box"), Color.white);
                    Txt(new Rect(85f, y + 35f, 200f, 50f), obj, lab, Color.white, TextAnchor.UpperLeft, true);
                    if (t != flag && t && !flag && nh != null) nh.JournalSelect(en2.Me.m_iHMissionID);
                    if (pointTex != null && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(-23f, 30f + y, 30f, 22f), pointTex);
                    y += 101f;
                }
                else
                {
                    var pos = new Rect(311f, y + 20f, 22f, 22f); bool t = Toggle(pos, flag, itg);
                    if (Skin.Button(new Rect(6f, y, 330f, 56f), "", mb, en)) SetCurrent(en2.Me);
                    if (flag && tf != null && tf.N != null && e.type == EventType.Repaint) Skin.Bg(new Rect(3f, y - 3f, 336f, 62f), tf.N, tf);
                    Txt(new Rect(15f, y + 5f, 280f, 25f), en2.Name, b14, Color.white, TextAnchor.UpperLeft);
                    Txt(new Rect(15f, y + 25f, 280f, 30f), obj, lab, Color.white, TextAnchor.UpperLeft, true);
                    if (t && !flag && nh != null) nh.JournalSelect(en2.Me.m_iHMissionID);
                    y += 71f;
                }
            }
            for (int i = list.Count; i < max; i++)
            {
                var oc = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.5f); if (mb != null && mb.N != null && e.type == EventType.Repaint) Skin.Bg(new Rect(6f, y, 330f, 27f), mb.N, mb); GUI.color = oc; y += 42f;
            }
            return y;
        }

        float DrawCompleted(List<Entry> list, float y, bool en)
        {
            var mb = Skin.Get(SKR, "MissionBack"); var ta = Skin.Get(SKR, "TextArea"); var b14 = Skin.Get(SKR, "bigfont14"); var lab = Skin.Get(SKR, "label"); var ni = Skin.Get(SKR, "npcicon"); var e = Event.current;
            foreach (var en2 in list)
            {
                if (cur != null && en2.Me.m_iHTaskID == cur.m_iHTaskID)
                {
                    if (Skin.Button(new Rect(6f, y, 330f, 86f), "", mb, en)) SetCurrent(en2.Me);
                    if (ta != null && ta.N != null && e.type == EventType.Repaint) Skin.Bg(new Rect(3f, y - 3f, 336f, 92f), ta.N, ta);
                    if (e.type == EventType.Repaint && ni != null) { var r = new Rect(10f, y + 10f, 68f, 68f); Skin.Bg(r, ni.N, ni); if (npcTex != null) GUI.DrawTexture(new Rect(r.x + 4f, r.y + 5f, r.width - 8f, r.height - 8f), npcTex); }
                    Txt(new Rect(85f, y + 5f, 200f, 30f), " " + en2.Name, b14, Color.white, TextAnchor.UpperLeft);
                    Txt(new Rect(85f, y + 35f, 200f, 50f), en2.Desc, lab, Color.white, TextAnchor.UpperLeft, true);
                    if (pointTex != null && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(-23f, 30f + y, 30f, 22f), pointTex);
                    y += 101f; continue;
                }
                if (Skin.Button(new Rect(6f, y, 330f, 56f), "", mb, en)) SetCurrent(en2.Me);
                Txt(new Rect(15f, y + 5f, 280f, 30f), " " + en2.Name, b14, Color.white, TextAnchor.UpperLeft);
                Txt(new Rect(15f, y + 25f, 280f, 30f), en2.Desc, lab, Color.white, TextAnchor.UpperLeft, true);
                y += 71f;
            }
            return y;
        }

        float DrawCompletedList(float off, bool en)
        {
            var b16 = Skin.Get(SKR, "bigfont16"); var itg = Skin.Get(SKR, "iconToggle"); float y = 5f - off;
            expNano = Toggle(new Rect(5f, y, 22f, 22f), expNano, itg); DrawIconText(new Rect(30f, y, 250f, 24f), missionIcon[5], " " + Loc.T("NANO MISSION"), b16, Color.white); Txt(new Rect(290f, y, 100f, 20f), nanoL.Count.ToString(), b16, Color.white, TextAnchor.UpperLeft); y += 30f;
            if (expNano) y = DrawCompleted(nanoL, y, en);
            y += 10f;
            expGuide = Toggle(new Rect(5f, y, 22f, 22f), expGuide, itg); DrawIconText(new Rect(30f, y, 250f, 24f), missionIcon[GuideIcon], " " + Loc.T("GUIDE MISSION"), b16, Color.white); Txt(new Rect(290f, y, 100f, 20f), guideL.Count.ToString(), b16, Color.white, TextAnchor.UpperLeft); y += 30f;
            if (expGuide) y = DrawCompleted(guideL, y, en);
            y += 10f;
            expWorld = Toggle(new Rect(5f, y, 22f, 22f), expWorld, itg); DrawIconText(new Rect(30f, y, 250f, 24f), missionIcon[6], " " + Loc.T("WORLD MISSIONS"), b16, Color.white); Txt(new Rect(290f, y, 100f, 20f), worldL.Count.ToString(), b16, Color.white, TextAnchor.UpperLeft); y += 30f;
            if (expWorld) y = DrawCompleted(worldL, y, en);
            return y + off + 10f;
        }

        void DrawNotice(Event e)
        {
            var oc = GUI.color; if (e.type == EventType.Repaint) { GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = oc; }
            var r = new Rect(300f, 220f, 436f, 170f); if (e.type == EventType.Repaint) { GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = oc; }
            Txt(new Rect(r.x + 15f, r.y + 15f, r.width - 30f, 95f), notice, Skin.Get(SKL, "label"), Color.white, TextAnchor.UpperCenter, true);
            if (Skin.Button(new Rect(r.x + 123f, r.y + 120f, 190f, 28f), Loc.T("OK"), Skin.Get(SKL, "CancelButton"), true)) { Sfx.Button(); notice = null; }
        }

        // ---- confirmacion de DELETE MISSION (cnSystemMessageManager eMsg_DeleteMission) ----
        void DrawConfirm(Event e)
        {
            var oc = GUI.color; if (e.type == EventType.Repaint) { GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = oc; }
            var r = new Rect(300f, 220f, 436f, 200f); var box = Skin.Get(Skin.Menu, "box") ?? Skin.Get(SKL, "box"); if (e.type == EventType.Repaint) { GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = oc; }
            var lab = Skin.Get(SKL, "label");
            Txt(new Rect(r.x + 15f, r.y + 15f, r.width - 30f, 120f), Loc.T("Delete") + "\n " + title + "\n" + Loc.T("This mission will no longer appear in your mission journal,\nbut you can get it again later by visiting the mission giver."), lab, Color.white, TextAnchor.UpperCenter, true);
            if (Skin.Button(new Rect(r.x + 20f, r.y + 150f, 190f, 28f), Loc.T("DELETE MISSION"), Skin.Get(SKL, "RedButton"), true)) { confirmDelete = false; DeleteMission(); }
            if (Skin.Button(new Rect(r.x + 226f, r.y + 150f, 190f, 28f), Loc.T("CANCEL"), Skin.Get(SKL, "CancelButton"), true)) confirmDelete = false;
        }

        void DeleteMission()
        {   // REQ_PC_TASK_STOP { int iTaskNum }
            if (cur == null || NpcUI.Sender == null) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_TASK_STOP, 4); BitConverter.GetBytes(cur.m_iHTaskID).CopyTo(p, 4);
            try { NpcUI.Sender(p); cooldown = 1f; } catch (Exception ex) { Debug.LogWarning("[journal] TASK_STOP: " + ex.Message); }
        }
    }
}
