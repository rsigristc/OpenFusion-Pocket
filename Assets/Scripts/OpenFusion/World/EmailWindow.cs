// Shared mail model and Legacy window: EmailMode / Panel_EmailList / Panel_NewEmail / cnEmailSystemManager.
// Skin = FFGuiSkin del InventoryManager (Skin.Inven 1366). Dos pestanas: GUIDE EMAIL (correos de mision/guia generados desde la tabla de misiones) y PLAYER EMAIL (servidor).
// Paquetes: REQ_PC_RECV_EMAIL_PAGE_LIST / READ_EMAIL / DELETE_EMAIL / SEND_EMAIL / RECV_EMAIL_CANDY / RECV_EMAIL_ITEM_ALL y sus respuestas.
// Destinatarios = lista de amigos (REP_PC_BUDDYLIST_INFO_SUCC, se guarda aqui al llegar).
// DS uses this same model. Composer supports up to four inventory attachments; received items are claimed together into server-selected slots.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed partial class EmailWindow : MonoBehaviour
    {
        public Func<string, Texture> TexLoader;
        public static bool Typing;                      // un campo de texto tiene el foco (bloquea los atajos de teclado)
        const int SK = Skin.Inven;
        public static EmailWindow Ensure(GameObject host) { var w = host.GetComponent<EmailWindow>(); if (w == null) w = host.AddComponent<EmailWindow>(); return w; }

        // ---- geometria del original ----
        static readonly Rect WindowRect = new Rect(0, 0, 1036, 654);
        static readonly Rect ListBack = new Rect(0, 15, 561, 229), DataBack = new Rect(0, 248, 560, 382), NewBut = new Rect(390, 0, 160, 25);
        static readonly Rect PlayerTabBut = new Rect(180, 5, 135, 15), PlayerTab = new Rect(146, 0, 209, 29), GuideTabBut = new Rect(13, 5, 130, 15), GuideTab = new Rect(0, 0, 181, 25), GuideTabBack = new Rect(0, 0, 343, 30);
        static readonly Rect FromTitle = new Rect(1, 36, 203, 20), SubjTitle = new Rect(204, 36, 219, 20), DateTitle = new Rect(421, 36, 100, 20), ClipTitle = new Rect(522, 36, 35, 20);
        static readonly Rect FromBox = new Rect(1, 58, 203, 28), SubjBox = new Rect(204, 58, 219, 28), DateBox = new Rect(421, 58, 100, 28), ClipBox = new Rect(522, 58, 35, 28);
        static readonly Rect PrevBut = new Rect(1, 202, 120, 26), NextBut = new Rect(440, 202, 120, 26), PosL = new Rect(210, 205, 30, 20), OfStr = new Rect(240, 205, 20, 20), PageN = new Rect(260, 205, 30, 20), PageStr = new Rect(280, 205, 60, 20);
        static readonly Rect Click = new Rect(0, 0, 560, 23);
        static readonly Rect GuideIcon = new Rect(10, 10, 79, 68), RFrom = new Rect(10, 20, 60, 20), RSubject = new Rect(10, 45, 80, 20), RFromName = new Rect(70, 17, 220, 20), RSubjName = new Rect(95, 42, 200, 20);
        static readonly Rect DataBoxG = new Rect(10, 92, 538, 238), DataBoxG2 = new Rect(10, 82, 538, 140), Received = new Rect(378, 45, 90, 20), ReceivedDay = new Rect(465, 42, 90, 20);
        static readonly Rect DelBuddyBut = new Rect(377, 12, 156, 20), DelMailBut = new Rect(10, 342, 152, 26), ReplyBut = new Rect(437, 342, 114, 26), ItemBox = new Rect(40, 230, 67, 67);
        static readonly Rect AllItemBut = new Rect(40, 305, 264, 20), TarosBut = new Rect(324, 305, 205, 20), TarosLbl = new Rect(324, 260, 160, 20), NoteR = new Rect(23, 335, 520, 20);
        // nuevo correo
        static readonly Rect NewPanel = new Rect(6, 3, 565, 600), MailTitle = new Rect(14, 2, 130, 15), CloseB = new Rect(534, 15, 30, 30), ToR = new Rect(20, 37, 80, 20), SubjR = new Rect(20, 77, 80, 20), ToName = new Rect(115, 40, 280, 20), SubjField = new Rect(115, 80, 234, 20);
        static readonly Rect BuddyBut = new Rect(408, 36, 115, 22), DataNew = new Rect(18, 120, 536, 206), ClipNew = new Rect(25, 347, 26, 20), AttStr = new Rect(50, 344, 130, 20), ItemNew = new Rect(30, 392, 68, 64);
        static readonly Rect AddTarosBut = new Rect(327, 453, 206, 20), TarosNew = new Rect(341, 411, 145, 20), PostageR = new Rect(10, 510, 545, 18), CancelB = new Rect(15, 550, 150, 25), SendB = new Rect(416, 550, 130, 25);
        static readonly Rect BuddyBox = new Rect(568, 28, 358, 255), BuddyTitle = new Rect(96, 14, 130, 17), BuddyClose = new Rect(326, 2, 30, 30), BuddyList = new Rect(20, 54, 310, 178);
        static readonly Rect CacuBox = new Rect(564, 287, 210, 255), CacuTitle = new Rect(20, 15, 140, 20), CacuType = new Rect(8, 47, 188, 20), CacuWin = new Rect(25, 63, 136, 109), CacuEnter = new Rect(30, 200, 132, 27), CacuClose = new Rect(180, 2, 30, 30), CacuVal = new Rect(1, 1, 132, 20);
        static readonly Rect[] CacuBtn = {
            new Rect(46, 89, 44, 19), new Rect(1, 29, 44, 19), new Rect(46, 29, 44, 19), new Rect(91, 29, 44, 19), new Rect(1, 49, 44, 19), new Rect(46, 49, 44, 19), new Rect(91, 49, 44, 19),
            new Rect(1, 69, 44, 19), new Rect(46, 69, 44, 19), new Rect(91, 69, 44, 19), new Rect(1, 89, 44, 19), new Rect(91, 89, 44, 19) };
        static readonly int[] GuideNpc = { 707, 728, 731, 732, 1171 };

        sealed class Node { public int Mode; public MissionElement Mission; }
        struct Info { public long Index, FromUid; public string First, Last, Subject; public int Read; public int SendDay, SendMonth, SendYear; public int ItemCandy; }
        struct Buddy { public long Uid; public bool Blocked; public string First, Last; public int NameCheck; }
        struct Att { public int Type, Id, Opt; }

        PacketDispatcher disp; WorldEntities ents; XdtTableScript tables; NanocomHud nh; PlayerHud hud; MissionTableScript mt; NpcTableScript npcT; MessageTableScript msgT;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        bool open, texTried; Texture back;
        Texture2D tBack, tClip, tList, tUserTab, tGuideBut, tGuideButOver, tUserBut, tUserButOver, tBuddy, tCacu, tCacuBack, tTaros, tNew;
        // estado
        bool comMail = true, composing, readMail, prevB, nextB, busy;
        int click, guidePage, userPage = 1, userCount; float busyT;
        readonly List<Node> nodes = new List<Node>(); readonly Info[] user = new Info[5];
        Texture npcIcon; string bodyText = "";
        long readIndex; string readContent = ""; readonly Att[] readItems = new Att[4]; int readCash;
        static readonly List<Buddy> buddies = new List<Buddy>(); static readonly object bl = new object();
        // nuevo correo
        string toName = "", subject = "", content = ""; long toUid; int taros, calc; bool buddyWin, calcWin; float buddyScroll, bodyScroll, newScroll; bool sending;
        // mensajes
        string msg; string[] msgBtns; Action msgYes; GUIStyle fieldSt, areaSt;

        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, NanocomHud n, PlayerHud h)
        {
            if (disp != null) {disp.Unhandled -= OnPacket;disp.SelfEnter-=OnMailEnter;} disp = d; ents = e; tables = t; nh = n; hud = h; if (disp != null) {disp.Unhandled += OnPacket;disp.SelfEnter+=OnMailEnter;}
            mt = t != null ? t.m_pMissionTable : null; npcT = t != null ? t.m_pNpcTable : null; msgT = t != null ? t.m_pMessageTable : null;
        }
        void Awake() { Instance=this;GameMenu.Implemented.Add(GameMenu.Win.EMail); }
        void OnDestroy() { if (disp != null) {disp.Unhandled -= OnPacket;disp.SelfEnter-=OnMailEnter;} if(Instance==this)Instance=null;Typing=false; }

        // ============================================================ red ============================================================
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static long L(byte[] b, int o) { return BitConverter.ToInt64(b, o); }
        static string S(byte[] b, int o, int chars) { var s = System.Text.Encoding.Unicode.GetString(b, o, chars * 2); int z = s.IndexOf('\0'); return z >= 0 ? s.Substring(0, z) : s; }
        void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[email] envio: " + ex.Message); } } }
        void Busy() { busy = true; busyT = 8f; }

        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_REP_PC_BUDDYLIST_INFO_SUCC && b.Length >= 16)
            {   // cnBuddySystemManager.func_FE2CL_PC_BUDDYLIST_INFO_SUCC: cabecera 16 B + sBuddyBaseInfo (72 B) x iBuddyCnt
                int cnt = b[13]; var tmp = new List<Buddy>();
                for (int i = 0; i < cnt; i++) { int o = 16 + 72 * i; if (o + 72 > b.Length) break; tmp.Add(new Buddy { Uid = L(b, o + 4), Blocked = (sbyte)b[o + 12] != 0, First = S(b, o + 16, 9), Last = S(b, o + 34, 17), NameCheck = (sbyte)b[o + 69] }); }
                lock (bl) foreach (var x in tmp) { int k = buddies.FindIndex(y => y.Uid == x.Uid); if (k >= 0) buddies[k] = x; else if (x.Uid > 0) buddies.Add(x); }
                return;
            }
            if (type < PacketIds.P_FE2CL_REP_PC_NEW_EMAIL || type > PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_CANDY_FAIL)
            {
                if (type != PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_ITEM_ALL_SUCC && type != PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_ITEM_ALL_FAIL) return;
            }
            var body = b; q.Enqueue(() => Handle(type, body));
        }

        void Handle(uint type, byte[] b)
        {
            if (type == PacketIds.P_FE2CL_REP_PC_NEW_EMAIL) {if(b.Length>=4)playerUnread=Mathf.Max(0,I(b,0));return;}
            if(type==PacketIds.P_FE2CL_REP_PC_READ_EMAIL_SUCC && (b.Length<1084 || L(b,0)!=requestedRead))return;
            if(type==PacketIds.P_FE2CL_REP_PC_READ_EMAIL_FAIL && (b.Length<12 || L(b,0)!=requestedRead))return;
            if(type==PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_PAGE_LIST_SUCC && (comMail || b.Length<1024 || (byte)b[0]!=requestedPage))return;
            if((type==PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_ITEM_ALL_SUCC || type==PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_CANDY_SUCC) && (b.Length<8 || L(b,0)!=readIndex))return;
            busy = false;
            if (type == PacketIds.P_FE2CL_REP_PC_READ_EMAIL_SUCC && b.Length >= 1084)
            {
                readIndex = L(b, 0); readContent = S(b, 8, 512); for (int i = 0; i < 4; i++) { int o = 1032 + 12 * i; readItems[i] = new Att { Type = BitConverter.ToInt16(b, o), Id = BitConverter.ToInt16(b, o + 2), Opt = I(b, o + 4) }; }
                readCash = I(b, 1080); readMail = true; bodyText = readContent; bodyScroll = 0f; Sfx.Ui("Email_arrived");
                for(int i=0;i<user.Length;i++)if(user[i].Index==readIndex&&user[i].Read==0){user[i].Read=1;playerUnread=Mathf.Max(0,playerUnread-1);}
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_READ_EMAIL_FAIL) Msg(167, (b.Length >= 12 ? L(b, 0) : 0).ToString(), (b.Length >= 12 ? I(b, 8) : 0).ToString());
            else if (type == PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_PAGE_LIST_SUCC && b.Length >= 4 + 5 * 204)
            {
                int page = (sbyte)b[0]; userCount = 0; readMail = false;
                for (int i = 0; i < 5; i++)
                {
                    int o = 4 + 204 * i; var f = new Info { Index = L(b, o), FromUid = L(b, o + 8), First = S(b, o + 16, 9), Last = S(b, o + 34, 17), Subject = S(b, o + 68, 32), Read = I(b, o + 132), SendYear = I(b, o + 136), SendMonth = I(b, o + 140), SendDay = I(b, o + 148), ItemCandy = I(b, o + 200) };
                    user[i] = f; if (f.Index > 0) userCount++;
                }
                int observedUnread=0;foreach(var info in user)if(info.Index>0&&info.Read==0)observedUnread++;playerUnread=Mathf.Max(playerUnread,observedUnread);
                SetUserPage(page);
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_PAGE_LIST_FAIL) { for (int i = 0; i < 5; i++) user[i] = default(Info); userCount = 0; SetUserPage(b.Length > 0 ? (sbyte)b[0] : userPage); }
            else if (type == PacketIds.P_FE2CL_REP_PC_DELETE_EMAIL_SUCC) RequestPage(userPage);
            else if (type == PacketIds.P_FE2CL_REP_PC_DELETE_EMAIL_FAIL) Msg(168);
            else if (type == PacketIds.P_FE2CL_REP_PC_SEND_EMAIL_SUCC)
            {
                if (b.Length >= 12 && hud != null) hud.Data.Taros = I(b, 8);
                composing = false; sending = false; GuideTab_(); Sfx.Ui("Action_Sucess");
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_SEND_EMAIL_FAIL)
            {
                sending = false; int c = b.Length >= 12 ? I(b, 8) : 0;
                switch (c)
                {
                    case 1: Text("ERROR!\nInsufficient taros."); break; case 2: Text("ERROR!\nInvalid Player to send email to."); break; case 3: Text("ERROR!\nFATAL Email ERROR!"); break;
                    case 4: Text("ERROR!\nYour message contains improper language and cannot be sent. Please make corrections and try sending the email again."); break;
                    case 5: Msg(171); break; case 9: Text("EMAIL TIME TRAVEL ERROR!\nYou cannot send messages with attachments (items and/or Taros) through time. Please remove any attachments and try again. "); break;
                    default: Text("ERROR!\nMail Error. " + c); break;
                }
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_CANDY_SUCC)
            {
                if(b.Length>=8&&L(b,0)==readIndex)readCash = 0; if (b.Length >= 12 && hud != null) hud.Data.Taros = I(b, 8);
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_CANDY_FAIL)
            {
                int c = b.Length >= 12 ? I(b, 8) : 0; if (c == 1) Msg(225); else if (c == 5) Msg(222); else Text("Email taros fail. " + c);
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_ITEM_ALL_SUCC && b.Length>=8 && L(b,0)==readIndex) { for (int i = 0; i < 4; i++) readItems[i] = default(Att); }
            else if (type == PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_ITEM_ALL_FAIL)
            {
                int c = b.Length >= 12 ? I(b, 8) : 0; if (c == 1) Msg(221); else if (c == 5) Msg(222); else Text("Email item all fail. " + c);
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_ITEM_SUCC || type == PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_ITEM_FAIL) { }
        }

        void RequestPage(int page) { requestedPage=Mathf.Clamp(page,1,4);var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_RECV_EMAIL_PAGE_LIST, 1); p[4] = (byte)requestedPage; Busy(); Send(p); }
        void RequestRead(long idx) { requestedRead=idx;var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_READ_EMAIL, 8); BitConverter.GetBytes(idx).CopyTo(p, 4); Busy(); Send(p); }
        void SetUserPage(int page)
        {
            bodyText = ""; click = -1; userPage = page; nextB = page != 4 && userCount == 5; prevB = page != 1;
            if (userCount > 0 && !dualPrepared) ClickRow(0);
        }

        // ============================================================ mensajes (cnSystemMessageManager) ============================================================
        void Msg(int id, params string[] a)
        {
            string s = null; int bt = 1;
            if (msgT != null && msgT.m_pMessageData != null && id >= 0 && id < msgT.m_pMessageData.Length && msgT.m_pMessageData[id] != null) { s = msgT.m_pMessageData[id].m_szString; bt = msgT.m_pMessageData[id].m_iButtonType; }
            if (s == null) s = "(message " + id + ")";
            int k = 0; while (a != null && k < a.Length) { int p = s.IndexOf("%s"); if (p < 0) break; s = s.Substring(0, p) + a[k] + s.Substring(p + 2); k++; }
            msg = Loc.T(s); msgBtns = bt == 2 ? new[] { "OK", "CANCEL" } : bt == 3 ? new[] { "YES", "NO" } : new[] { "OK" }; msgYes = null; Sfx.Ui("Action_Failure01");
        }
        void Confirm(int id, Action yes, params string[] a) { Msg(id, a); if (msgBtns.Length == 1) msgBtns = new[] { "OK", "CANCEL" }; msgYes = yes; }
        void Text(string s) { msg = Loc.T(s); msgBtns = new[] { "OK" }; msgYes = null; }

        // ============================================================ apertura ============================================================
        void Update()
        {
            while (q.TryDequeue(out var a)) a();
            UpdateMailNotice();
            if (busy) { busyT -= Time.unscaledDeltaTime; if (busyT <= 0f) busy = false; }
            bool now = GameMenu.Current == GameMenu.Win.EMail;
            if (now && !open) OnOpen(); else if (!now && open) { back = null; texTried = false; Typing = false; if (InventoryWindow.Instance != null) InventoryWindow.Instance.ResetOverlays(); }
            if(now&&pendingCompose){pendingCompose=false;StartNew(pendingRecipient,pendingRecipientUid);}
            open = now;
        }
        Texture Load(string path)
        {
            if (TexLoader == null) return null;
            foreach (var ext in new[] { ".png", ".psd", ".dds", ".tga", "" }) { try { var t = TexLoader(path + ext); if (t != null) return t; } catch (Exception) { } }
            return null;
        }
        void OnOpen()
        {
            if (!texTried)
            {
                texTried = true; back = Load("TutorialAssets/panelback"); tNew = Load("TutorialAssets/emailnew") as Texture2D;
                tBack = UiKit.Tex("emailback"); tClip = UiKit.Tex("clip"); tList = UiKit.Tex("comemaillist"); tUserTab = UiKit.Tex("emailusertab"); tGuideBut = UiKit.Tex("emailcombut"); tGuideButOver = UiKit.Tex("emailcombutover");
                tUserBut = UiKit.Tex("emailuserbut"); tUserButOver = UiKit.Tex("emailuserbutover"); tBuddy = UiKit.Tex("emailbudylist"); tCacu = UiKit.Tex("emailcacu"); tCacuBack = UiKit.Tex("cacuback"); tTaros = UiKit.Tex("tarosicon") ?? UiKit.Tex("Taros");
            }
            composing = false; buddyWin = calcWin = false; msg = null; busy = false; readMail = false; sending = false; for (int i = 0; i < 5; i++) user[i] = default(Info);
            GuideTab_();
        }
        void GuideTab_()
        {   // ComEmail
            comMail = true; guidePage = 0; click = 0; bodyText = ""; npcIcon = null; BuildGuideMail(); ClickRow(0);
        }

        // ---- cnEmailSystemManager.RefreshEmailInfo ----
        void BuildGuideMail()
        {
            nodes.Clear(); if (mt == null || mt.m_pMissionData == null || nh == null) return;
            var act = nh.JournalActive(); int g1 = 0, g2 = 0;
            foreach (var me in act)
            {
                if (me.m_iSTMessageType == 4 || me.m_iSTMessageType == 6) { if (!nodes.Exists(n => n.Mode == 1 && n.Mission.m_iHTaskID == me.m_iHTaskID)) nodes.Add(new Node { Mode = 1, Mission = me }); }
                if (me.m_iHMissionType == 1) g1++; else if (me.m_iHMissionType == 2) g2++;
            }
            int guide = nh.JournalGuide; if (guide < 1 || guide > 5) return;
            var done = new HashSet<int>(); foreach (var me in nh.JournalCompleted()) done.Add(me.m_iHMissionID);
            foreach (var m in mt.m_pMissionData)
            {
                if (m == null) continue; bool isAct = false; foreach (var me in act) if (me.m_iHMissionID == m.m_iHMissionID) { isAct = true; break; } if (isAct) continue;
                if (m.m_iHMissionType == 1) { if (g1 >= 1) continue; } else if (m.m_iHMissionType != 2 || g2 >= 1) continue;
                int sum = 0; if (m.m_iMentorEmailID != null) for (int j = 0; j < Mathf.Min(5, m.m_iMentorEmailID.Length); j++) sum += m.m_iMentorEmailID[j]; if (sum <= 0) continue;
                if (done.Contains(m.m_iHMissionID)) continue;
                if (nodes.Exists(n => n.Mode == 1 && n.Mission.m_iHTaskID == m.m_iHTaskID)) continue;
                if(ents?.Missions==null || !ents.Missions.CanStart(m.m_iHTaskID))continue;
                nodes.Add(new Node { Mode = 2, Mission = m }); if (m.m_iHMissionType == 1) g1++; else g2++;
            }
        }
        string MStr(int id) { if (mt == null || mt.m_pMissionStringData == null || id < 0 || id >= mt.m_pMissionStringData.Length || mt.m_pMissionStringData[id] == null) return ""; return mt.m_pMissionStringData[id].m_pstrNameString ?? ""; }
        string NpcNm(int type) { return nh != null ? (nh.NpcNameOf(type) ?? "") : ""; }
        Texture Portrait(int npcType)
        {
            if (npcT == null || npcT.m_pNpcData == null || npcType < 0 || npcType >= npcT.m_pNpcData.Length || npcT.m_pNpcData[npcType] == null) return null;
            var row = npcT.m_pNpcData[npcType]; var ic = npcT.m_pNpcIconData; if (ic == null || row.m_iIcon1 < 0 || row.m_iIcon1 >= ic.Length) return null;
            string n = NanoWheelHud.IconName(ic[row.m_iIcon1]); if (string.IsNullOrEmpty(n)) return null;
            return Resources.Load<Texture2D>("OpenFusion/Portraits/" + n) ?? Resources.Load<Texture2D>("OpenFusion/Icons/" + n);
        }
        int GuideNum { get { return nh != null ? nh.JournalGuide : 0; } }

        void ClickRow(int row)
        {   // OnClick
            click = row;
            if (comMail)
            {
                int idx = row + guidePage * 5; prevB = guidePage > 0; nextB = guidePage * 5 + 5 < nodes.Count;
                if (idx >= nodes.Count || idx < 0) { click = -1; bodyText = ""; npcIcon = null; return; }
                var n = nodes[idx]; int g = GuideNum; int textId = 0;
                if (n.Mode == 1)
                {
                    npcIcon = Portrait(n.Mission.m_iSTMessageSendNPC);
                    foreach (var me in nh.JournalActive()) if (me.m_iHMissionID == n.Mission.m_iHMissionID) { textId = me.m_iSTMessageTextID; break; }
                    if (textId <= 0 && g >= 1 && g <= 5 && n.Mission.m_iMentorEmailID != null && n.Mission.m_iMentorEmailID.Length >= g) textId = n.Mission.m_iMentorEmailID[g - 1];
                }
                else
                {
                    npcIcon = g >= 1 && g <= 5 ? Portrait(GuideNpc[g - 1]) : null;
                    if (g >= 1 && g <= 5 && n.Mission.m_iMentorEmailID != null && n.Mission.m_iMentorEmailID.Length >= g) textId = n.Mission.m_iMentorEmailID[g - 1];
                }
                bodyText = MStr(textId); bodyScroll = 0f;
                if(!string.IsNullOrEmpty(guideScope)){PlayerPrefs.SetInt(GuideReadKey(n),1);PlayerPrefs.Save();}
            }
            else if (row >= 0 && row < 5 && user[row].Index > 0) { readMail = false;readIndex=0;bodyText="";readCash=0;Array.Clear(readItems,0,readItems.Length); RequestRead(user[row].Index); }
        }
        void PlayerTab_()
        {
            comMail = false; bodyText = ""; click = 0; userPage = 1; prevB = nextB = false; readMail = false; RequestPage(1);
        }

        static string Date(Info f)
        {
            var now = DateTime.Now; int n = now.Day - f.SendDay; n += (now.Month - f.SendMonth) * 30; if (f.SendDay == 31) n++; n += (now.Year - f.SendYear) * 365;
            return n <= 0 ? Loc.T("today") : n + " " + Loc.T("day");
        }
        string BuddyName(Info f)
        {
            string t = f.First; lock (bl) foreach (var b in buddies) if (b.Uid > 0 && b.Uid == f.FromUid && b.NameCheck != 1) t = Loc.T("Player") + " " + b.Uid; return t;
        }

        // ============================================================ helpers de dibujo ============================================================
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        static Color Mul(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }
        static readonly Color Pale = new Color(0.8f, 1f, 1f), Dark = new Color(0.1f, 0.1f, 0.1f);
        SkinStyle St(string n) { return Skin.Get(SK, n); }
        void Txt(Rect r, string t, SkinStyle st, Color tint, TextAnchor? an = null, bool? wrap = null, SkinStyle fontFrom = null)
        {
            if (st == null || string.IsNullOrEmpty(t) || Event.current.type != EventType.Repaint) return; var f = fontFrom != null && fontFrom.Font != null ? fontFrom.Font : st.Font; if (f == null) return;
            f.Draw(r, t, an ?? st.Align, wrap ?? st.Wrap, Mul(st.cN, tint), st.Padding);
        }
        bool Hit(Rect r, bool en)
        {
            var e = Event.current; int id = GUIUtility.GetControlID(FocusType.Passive, r); bool hov = r.Contains(e.mousePosition); if (!en) return false;
            if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); return false; }
            if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); if (hov) { Sfx.Button(); return true; } }
            return false;
        }
        bool Btn(Rect r, string text, string style, bool en, Color? tint = null)
        {
            var st = St(style); if (st == null) return false;
            if (Event.current.type == EventType.Repaint && tint.HasValue) { }
            return Skin.Button(r, text, st, en);
        }
        bool Plain(Rect r, string text, SkinStyle st, Color tint, bool en, TextAnchor? an = null)
        {   // GUI.Button con estilo "blankbox": solo texto
            bool hit = Hit(r, en); Txt(r, text, st, tint, an); return hit;
        }
        float ScrollText(Rect view, string text, SkinStyle st, Color tint, float scroll, TextAnchor an = TextAnchor.UpperLeft)
        {
            var e = Event.current; if (st == null || st.Font == null) return scroll; float w = view.width - 14f, h = Mathf.Max(view.height, st.Font.Height(Loc.T(text ?? ""), w) + 6f), max = Mathf.Max(0f, h - view.height);
            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) { scroll = Mathf.Clamp(scroll + e.delta.y * 20f, 0f, max); e.Use(); }
            scroll = Mathf.Clamp(scroll, 0f, max); GUI.BeginGroup(view); if (e.type == EventType.Repaint) st.Font.Draw(new Rect(0f, -scroll, w, h), text ?? "", an, true, Mul(st.cN, tint), null); GUI.EndGroup();
            if (max > 0f && e.type == EventType.Repaint) { var tr = new Rect(view.xMax - 8f, view.y, 6f, view.height); float th = Mathf.Max(18f, tr.height * view.height / h); var oc = GUI.color; GUI.color = new Color(0f, 0.2f, 0.4f, 0.4f); GUI.DrawTexture(tr, Texture2D.whiteTexture); GUI.color = new Color(0.4f, 0.8f, 1f, 0.9f); GUI.DrawTexture(new Rect(tr.x, tr.y + (tr.height - th) * scroll / max, tr.width, th), Texture2D.whiteTexture); GUI.color = oc; }
            return scroll;
        }
        GUIStyle FieldStyle(bool area)
        {
            var s = new GUIStyle(area ? GUI.skin.textArea : GUI.skin.textField) { fontSize = 14, wordWrap = area, alignment = area ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, richText = false };
            s.normal.background = null; s.focused.background = null; s.hover.background = null; s.active.background = null; s.onNormal.background = null; s.onFocused.background = null;
            var c = area ? Dark : new Color(0.8f, 1f, 1f); s.normal.textColor = c; s.focused.textColor = c; s.hover.textColor = c; s.active.textColor = c; s.padding = new RectOffset(2, 2, 2, 2); return s;
        }

        // ============================================================ ventana ============================================================
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
                GUI.BeginGroup(wr);
                bool en = !busy && msg == null && !sending && !InventoryWindow.Busy;
                if (composing) DrawNew(e, en); else DrawList(e, en);
                if (msg != null) DrawMsg(e);
                GUI.EndGroup();
                if (composing) Typing = GUIUtility.keyboardControl != 0; else { Typing = false; }
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        void DrawMsg(Event e)
        {
            var oc = GUI.color; if (e.type == EventType.Repaint) { GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = oc; }
            var r = new Rect(300f, 220f, 436f, 200f); if (e.type == EventType.Repaint) { GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = oc; }
            Txt(new Rect(r.x + 15f, r.y + 15f, r.width - 30f, 120f), msg, St("label"), Color.white, TextAnchor.UpperCenter, true);
            float bw = 150f, gap = 20f, tot = msgBtns.Length * bw + (msgBtns.Length - 1) * gap, x = r.x + (r.width - tot) / 2f;
            for (int i = 0; i < msgBtns.Length; i++)
            {
                string key = msgBtns[i]; bool cancel = key == "CANCEL" || key == "NO";
                if (Skin.Button(new Rect(x + i * (bw + gap), r.y + 150f, bw, 28f), Loc.T(key), cancel ? St("CancelButton") : St("RedButton"), true)) { var y = msgYes; msg = null; msgYes = null; if (!cancel && y != null) y(); return; }
            }
        }

        // ---- Panel_EmailList ----
        void DrawList(Event e, bool en)
        {
            var panel = new Rect(0f, 0f, 585f, 653f);
            if (e.type == EventType.Repaint && tBack != null) UiKit.Slice(panel, tBack, 5, 5, 0, 0);
            GUI.BeginGroup(new Rect(8f, 7f, 567f, 634f));
            if (Skin.Button(NewBut, Loc.T("SEND MAIL"), St("button"), en)) { StartNew("", 0); GUI.EndGroup(); return; }
            GUI.BeginGroup(new Rect(6, 3, 561, 631));
            OnList(e, en); OnSub(e, en);
            GUI.EndGroup(); GUI.EndGroup();
            if (InventoryWindow.Instance != null) InventoryWindow.Instance.DrawPanel(e, en, true);   // Panel_PCStuff en modo E-Mail (solo pestana EQUIPMENT)
        }
        void OnList(Event e, bool en)
        {
            var click_ = St("emaillistclick"); var blank = St("blankbox"); var center = St("centerLabel"); var lab = St("label"); var left = St("leftLabel");
            GUI.BeginGroup(ListBack);
            DoListBut(e, en, blank);
            Txt(FromTitle, Loc.T("FROM"), lab, Color.white, TextAnchor.MiddleCenter, false, left); Txt(SubjTitle, Loc.T("SUBJECT"), lab, Color.white, TextAnchor.MiddleCenter, false, left); Txt(DateTitle, Loc.T("DAY"), lab, Color.white, TextAnchor.MiddleCenter, false, left);
            if (e.type == EventType.Repaint && tClip != null) GUI.DrawTexture(new Rect(ClipTitle.x + (ClipTitle.width - tClip.width) / 2f, ClipTitle.y + (ClipTitle.height - tClip.height) / 2f, tClip.width, tClip.height), tClip);
            if (comMail)
            {
                if (5 * guidePage >= nodes.Count)
                {
                    Txt(new Rect(0f, FromBox.y, Click.width, Click.height * 5f), Loc.T("There are no messages in this folder."), lab, Pale, TextAnchor.MiddleCenter, false, left);
                    Txt(PageN, "0", center, Color.yellow); Txt(PageStr, Loc.T("Emails"), center, Color.white);
                }
                else
                {
                    for (int n = 0, i = 5 * guidePage; i < 5 + 5 * guidePage; i++, n++)
                    {
                        if (i >= nodes.Count) continue; Color tint = Color.white;
                        if (n == click) { if (e.type == EventType.Repaint) Skin.Bg(new Rect(2f, FromBox.y + FromBox.height * n, Click.width - 4f, Click.height), click_ != null ? click_.N : null, click_); tint = new Color(0.2f, 0.5f, 1f); }
                        var nd = nodes[i]; int g = GuideNum; string from = nd.Mode == 1 ? NpcNm(nd.Mission.m_iSTMessageSendNPC) : (g >= 1 && g <= 5 ? NpcNm(GuideNpc[g - 1]) : "");
                        string sj = MStr(nd.Mission.m_iHMissionName); if (sj.Length > 24) sj = sj.Remove(21) + "...";
                        if (Plain(new Rect(FromBox.x + 2f, FromBox.y + FromBox.height * n, FromBox.width - 2f, FromBox.height), from, blank, tint, en)) ClickRow(n);
                        if (Plain(new Rect(SubjBox.x, SubjBox.y + SubjBox.height * n, SubjBox.width, SubjBox.height), sj, blank, tint, en)) ClickRow(n);
                    }
                    if (nodes.Count > 0) { Txt(PosL, (guidePage + 1) + " - " + (click + 1), center, Color.yellow); Txt(OfStr, Loc.T("of"), center, Color.white); }
                    Txt(PageN, (guidePage + 1).ToString(), center, Color.yellow); Txt(PageStr, Loc.T("Emails"), center, Color.white);
                }
            }
            else
            {
                if (userCount == 0) Txt(new Rect(0f, FromBox.y, Click.width, Click.height * 5f), Loc.T("There are no messages in this folder."), lab, Pale, TextAnchor.MiddleCenter, false, left);
                else
                {
                    int n = 0;
                    for (int j = 0; j < 5; j++)
                    {
                        var f = user[j]; if (f.Index <= 0) { n++; continue; } Color tint = Color.white;
                        if (n == click) { if (e.type == EventType.Repaint) Skin.Bg(new Rect(2f, FromBox.y + FromBox.height * n, Click.width - 4f, Click.height), click_ != null ? click_.N : null, click_); tint = Dark; }
                        string sj = f.Subject ?? ""; if (sj.Length > 24) sj = sj.Remove(21) + "...";
                        if (Plain(new Rect(FromBox.x, FromBox.y + FromBox.height * n, FromBox.width, FromBox.height), BuddyName(f), blank, tint, en)) ClickRow(n);
                        if (Plain(new Rect(SubjBox.x, SubjBox.y + SubjBox.height * n, SubjBox.width, SubjBox.height), sj, blank, tint, en)) ClickRow(n);
                        if (Plain(new Rect(DateBox.x, DateBox.y + DateBox.height * n, DateBox.width, DateBox.height), Date(f), blank, tint, en)) ClickRow(n);
                        if (f.ItemCandy != 0 && tClip != null) { if (e.type == EventType.Repaint) GUI.DrawTexture(new Rect(ClipBox.x + (ClipBox.width - tClip.width) / 2f, ClipBox.y + ClipBox.height * n, tClip.width, tClip.height), tClip); if (Hit(new Rect(ClipBox.x, ClipBox.y + ClipBox.height * n, ClipBox.width, ClipBox.height), en)) ClickRow(n); }
                        n++;
                    }
                }
                if (userPage != 0 || userCount > 0) { Txt(PosL, userPage + " - " + (click + 1), center, Color.yellow); Txt(OfStr, Loc.T("of"), center, Color.white); }
                Txt(PageN, userPage.ToString(), center, Color.yellow); Txt(PageStr, Loc.T("Emails"), center, Color.white);
            }
            GUI.EndGroup();
        }
        void DoListBut(Event e, bool en, SkinStyle blank)
        {
            var prev = St("mailprevbut"); var next = St("mailnextbut");
            if (comMail)
            {
                bool hov = PlayerTabBut.Contains(e.mousePosition); if (e.type == EventType.Repaint) { var t = hov ? tUserButOver : tUserBut; if (t != null) GUI.DrawTexture(PlayerTab, t); if (tList != null) GUI.DrawTexture(new Rect(0, 0, ListBack.width, ListBack.height), tList); }
                if (Hit(PlayerTabBut, en)) { Sfx.Ui("Tab_Click01"); PlayerTab_(); return; }
            }
            else
            {
                bool hov = GuideTabBut.Contains(e.mousePosition); if (e.type == EventType.Repaint) { if (tList != null) GUI.DrawTexture(new Rect(0, 0, ListBack.width, ListBack.height), tList); var t = hov ? tGuideButOver : tGuideBut; if (t != null) GUI.DrawTexture(GuideTab, t); if (tUserTab != null) GUI.DrawTexture(GuideTabBack, tUserTab); }
                if (Hit(GuideTabBut, en)) { Sfx.Ui("Tab_Click01"); GuideTab_(); return; }
            }
            if (Skin.Button(PrevBut, Loc.T("PREVIOUS") + " 5", prev, en && prevB)) { if (comMail) { guidePage = Mathf.Max(0, guidePage - 1); ClickRow(0); } else { userPage = Mathf.Max(1, userPage - 1); RequestPage(userPage); } }
            if (Skin.Button(NextBut, Loc.T("NEXT") + " 5", next, en && nextB)) { if (comMail) { guidePage++; ClickRow(0); } else { userPage = Mathf.Min(4, userPage + 1); RequestPage(userPage); } }
            bool hp = PlayerTabBut.Contains(e.mousePosition), hg = GuideTabBut.Contains(e.mousePosition);
            Txt(GuideTabBut, Loc.T("GUIDE EMAIL"), blank, comMail || !hg ? Color.white : Color.black); Txt(PlayerTabBut, Loc.T("PLAYER EMAIL"), blank, !comMail || !hp ? Color.white : Color.black);
        }

        void OnSub(Event e, bool en)
        {
            var lab = St("label"); var left = St("leftLabel"); var box = St("emailsubbox"); var sub = St("emailsubback"); var slotE = St("slotboxempty");
            if (e.type == EventType.Repaint && sub != null) Skin.Bg(DataBack, sub.N, sub);
            GUI.BeginGroup(DataBack);
            if (comMail)
            {
                int idx = click + guidePage * 5; bool has = click > -1 && idx < nodes.Count;
                if (has) { if (e.type == EventType.Repaint) { Skin.Bg(GuideIcon, slotE != null ? slotE.N : null, slotE); if (npcIcon != null) GUI.DrawTexture(new Rect(GuideIcon.x + 7f, GuideIcon.y + 2f, GuideIcon.width - 14f, GuideIcon.height - 4f), npcIcon, ScaleMode.ScaleToFit); } }
                if (has)
                {
                    var nd = nodes[idx]; int g = GuideNum; string from = nd.Mode == 1 ? NpcNm(nd.Mission.m_iSTMessageSendNPC) : (g >= 1 && g <= 5 ? NpcNm(GuideNpc[g - 1]) : "");
                    Txt(new Rect(RFrom.x + GuideIcon.width, RFrom.y, RFrom.width, RFrom.height), Loc.T("FROM") + ": ", lab, Color.yellow);
                    Txt(new Rect(RFromName.x + GuideIcon.width, RFromName.y, RFromName.width, RFromName.height), from, lab, Pale, TextAnchor.MiddleLeft, false, left);
                    Txt(new Rect(RSubject.x + GuideIcon.width, RSubject.y, RSubject.width, RSubject.height), Loc.T("SUBJECT") + ": ", lab, Color.yellow);
                    Txt(new Rect(RSubjName.x + GuideIcon.width, RSubjName.y, RSubjName.width, RSubjName.height), MStr(nd.Mission.m_iHMissionName), lab, Pale, TextAnchor.MiddleLeft, false, left);
                    if (nd.Mode == 2) Txt(NoteR, Loc.T("Note: Guide emails are automatically deleted after the related mission is complete."), lab, Pale, TextAnchor.MiddleLeft, false, left);
                    if (e.type == EventType.Repaint) Skin.Bg(DataBoxG, box != null ? box.N : null, box);
                    bodyScroll = ScrollText(new Rect(DataBoxG.x + 10f, DataBoxG.y + 10f, DataBoxG.width - 13f, DataBoxG.height - 23f), bodyText, lab, Dark, bodyScroll);
                }
                else Txt(new Rect(DataBoxG.x + 10f, DataBoxG.y + 10f, DataBoxG.width - 13f, DataBoxG.height - 23f), Loc.T("There is no message selected."), lab, Pale, TextAnchor.MiddleCenter, false, left);
            }
            else
            {
                bool has = userCount > 0 && click >= 0 && click < 5 && user[click].Index > 0;
                if (has)
                {
                    var f = user[click];
                    Txt(RFrom, Loc.T("FROM") + ": ", lab, Color.yellow); Txt(RFromName, BuddyName(f), lab, Pale, TextAnchor.MiddleLeft, false, left);
                    Txt(RSubject, Loc.T("Subject") + ": ", lab, Color.yellow); Txt(RSubjName, f.Subject, lab, Pale, TextAnchor.MiddleLeft, false, left);
                    Txt(Received, Loc.T("RECEIVED") + ": ", lab, Color.yellow); Txt(ReceivedDay, Date(f), lab, Pale, TextAnchor.MiddleLeft, false, left);
                }
                if (has && readMail && (readIndex > 0 || readCash > 0))
                {
                    for (int j = 0; j < 4; j++)
                    {
                        var r = new Rect(ItemBox.x + ItemBox.width * j, ItemBox.y, ItemBox.width, ItemBox.height);
                        if (e.type == EventType.Repaint)
                        {
                            var sl = readItems[j].Id > 0 ? St("slotbox") : slotE; Skin.Bg(r, sl != null ? sl.N : null, sl);
                            if (readItems[j].Id > 0) { var ic = ItemIcon(readItems[j]); if (ic != null) GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), ic, ScaleMode.ScaleToFit); }
                        }
                        if (readItems[j].Id > 0 && readItems[j].Type == 7 && e.type == EventType.Repaint) Txt(new Rect(r.x + 5f, r.y + 5f, r.width, r.height), readItems[j].Opt.ToString(), lab, Color.white, TextAnchor.UpperLeft);
                    }
                    Txt(TarosLbl, Loc.T("TAROS") + " : " + readCash, lab, Color.white);
                    if (e.type == EventType.Repaint) Skin.Bg(DataBoxG2, box != null ? box.N : null, box);
                    bodyScroll = ScrollText(new Rect(DataBoxG2.x + 10f, DataBoxG2.y + 10f, DataBoxG2.width - 13f, DataBoxG2.height - 23f), bodyText, lab, Dark, bodyScroll);
                }
                else if (!has) Txt(new Rect(DataBoxG.x + 10f, DataBoxG.y + 10f, DataBoxG.width - 13f, DataBoxG.height - 23f), Loc.T("There is no message selected."), lab, Pale, TextAnchor.MiddleCenter, false, left);
                DoSubBut(has, en);
            }
            GUI.EndGroup();
        }
        Texture ItemIcon(Att a)
        {   // mismas tablas que ItemInfo del Journal (icono por tabla); si falta el grafico, marcador
            var jw = GetComponent<JournalWindow>(); return jw != null ? jw.ItemTexture(a.Type, a.Id) : null;
        }
        void DoSubBut(bool has, bool en)
        {
            var red = St("RedButton"); var btn = St("button");
            if (has && Skin.Button(DelBuddyBut, Loc.T("REMOVE BUDDY"), red, en))
            {
                if (readMail && readIndex > 0) { var f = user[click]; Confirm(165, () => RemoveBuddy(f), f.First); }
            }
            if (has || (readMail && readCash > 0))
            {
                if (Skin.Button(AllItemBut, Loc.T("ACCEPT ALL ITEMS"), btn, en))
                {
                    int c = 0; for (int i = 0; i < 4; i++) if (readItems[i].Id > 0) c++;
                    if (c > 0) { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_RECV_EMAIL_ITEM_ALL, 8); BitConverter.GetBytes(readIndex).CopyTo(p, 4); Busy(); Send(p); } else Msg(179);
                }
                if (Skin.Button(TarosBut, Loc.T("ACCEPT TAROS"), btn, en) && readMail && readIndex > 0 && readCash > 0) { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_RECV_EMAIL_CANDY, 8); BitConverter.GetBytes(readIndex).CopyTo(p, 4); Busy(); Send(p); }
            }
            if (has && Skin.Button(DelMailBut, Loc.T("DELETE"), red, en) && readMail && readIndex > 0) Confirm(166, DeleteMail);
            if (has && Skin.Button(ReplyBut, Loc.T("REPLY"), btn, en) && readMail && readIndex > 0)
            {
                for (int j = 0; j < 5; j++) if (user[j].Index == readIndex) { StartNew(user[j].First + user[j].Last, user[j].FromUid); break; }
            }
        }
        void DeleteMail() { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_DELETE_EMAIL, 40); BitConverter.GetBytes(readIndex).CopyTo(p, 4); Busy(); Send(p); }
        void RemoveBuddy(Info f)
        {   // REQ_REMOVE_BUDDY { iBuddyPCUID(8) @0, iBuddySlot(1) @8 }  (ranura = posicion entre amigos validos y no bloqueados)
            int slot = 0; long uid = 0; lock (bl) { foreach (var b in buddies) { if (b.Uid == 0 || b.Blocked) continue; if (b.First == f.First && b.Last == f.Last) { uid = b.Uid; break; } slot++; } }
            if (uid == 0) return; var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_REMOVE_BUDDY, 12); BitConverter.GetBytes(uid).CopyTo(p, 4); p[12] = (byte)slot; Send(p);
        }

        // ---- Panel_NewEmail ----
        void StartNew(string name, long uid)
        {
            ResetAttachments();
            composing = true; toName = name; toUid = uid; subject = ""; content = ""; taros = 0; calc = 0; buddyWin = calcWin = false; sending = false; Typing = false;
            if (uid != 0) { lock (bl) { int k = buddies.FindIndex(b => b.Uid == uid); if (k >= 0) toName = buddies[k].NameCheck != 1 ? Loc.T("Player") + " " + uid : buddies[k].First + buddies[k].Last; } }
        }
        void DrawNew(Event e, bool en)
        {
            var x0 = (WindowRect.width - 940f) / 2f; GUI.BeginGroup(new Rect(x0, 0f, 940f, 634f));
            bool popup = buddyWin || calcWin || attachmentPicker>=0 || attachmentAmount.Open; bool enMain = en && !popup;
            if (buddyWin) DrawBuddy(e, en);
            if (calcWin) DrawCalc(e, en);
            DrawAttachmentPicker(en);attachmentAmount.Draw(e,en);
            if (e.type == EventType.Repaint && tNew != null) GUI.DrawTexture(NewPanel, tNew);
            GUI.BeginGroup(NewPanel);
            var lab = St("label"); var blank = St("blankbox"); var box = St("emailsubbox"); var right = St("rightLabel");
            Txt(MailTitle, Loc.T("NEW EMAIL"), blank, new Color(0.1f, 0.2f, 1f));
            if (Skin.Button(CloseB, "", St("closebut"), enMain)) { composing = false; GuideTab_(); GUI.EndGroup(); GUI.EndGroup(); return; }
            if (Skin.Button(BuddyBut, Loc.T("BUDDY LIST"), St("button"), enMain)) buddyWin = true;
            Txt(ToR, Loc.T("TO") + ": ", lab, Color.white, TextAnchor.MiddleRight); Txt(SubjR, Loc.T("SUBJECT") + ": ", lab, Color.white, TextAnchor.MiddleRight);
            Txt(ToName, toName, lab, Color.yellow, TextAnchor.MiddleLeft);
            if (fieldSt == null) fieldSt = FieldStyle(false); if (areaSt == null) areaSt = FieldStyle(true);
            GUI.enabled = enMain; GUI.SetNextControlName("subject"); string s = GUI.TextField(SubjField, subject ?? "", 24, fieldSt); if (s != subject) subject = s.Replace("\n", "");
            if (e.type == EventType.Repaint && tClip != null) GUI.DrawTexture(ClipNew, tClip, ScaleMode.ScaleToFit);
            Txt(AttStr, Loc.T("ATTACHMENTS") + ": ", lab, Color.white);
            if (e.type == EventType.Repaint) Skin.Bg(DataNew, box != null ? box.N : null, box);
            GUI.SetNextControlName("body"); content = GUI.TextArea(new Rect(DataNew.x + 10f, DataNew.y + 10f, DataNew.width - 20f, DataNew.height - 20f), content ?? "", 512, areaSt); GUI.enabled = true;
            DrawAttachments(enMain);
            Txt(TarosNew, taros.ToString(), blank, Color.white, TextAnchor.MiddleRight);
            if (tTaros != null && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(TarosNew.xMax + 3f, TarosNew.y - (tTaros.height - TarosNew.height) / 2f, tTaros.width, tTaros.height), tTaros);
            if (Skin.Button(AddTarosBut, Loc.T("ADD TAROS"), St("button"), enMain)) { calcWin = true; calc = 0; }
            int postage = Postage; var pc = new Color(0f, 0.2f, 0.5f); string pl = Loc.T("postage") + " : ";
            Txt(new Rect(PostageR.x, PostageR.y + 3f, PostageR.width / 2f + 20f, PostageR.height), pl, St("vendorlistback") ?? lab, pc, TextAnchor.MiddleRight);
            Txt(new Rect(PostageR.x + PostageR.width / 2f + 25f, PostageR.y, 60f, PostageR.height), postage.ToString(), blank, pc, TextAnchor.MiddleLeft);
            if (tTaros != null && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(PostageR.x + PostageR.width / 2f + 25f + 40f, PostageR.y - (tTaros.height - PostageR.height) / 2f, tTaros.width, tTaros.height), tTaros);
            if (Skin.Button(CancelB, Loc.T("CANCEL"), St("RedButton"), enMain)) { composing = false; GuideTab_(); }
            if (Skin.Button(SendB, Loc.T("SEND"), St("button"), enMain))
            {
                int have = hud != null ? hud.Data.Taros : 0;
                if (toUid == 0) Text("Please choose a recipient from your buddy list.");
                else if (postage + taros > have) Msg(181);
                else DoSend(postage);
            }
            GUI.EndGroup(); GUI.EndGroup();
        }
        void DoSend(int postage)
        {   // REQ_PC_SEND_EMAIL (1164 B): iTo_PCUID @0, szSubject[32] @8, szContent[512] @72, aItem[4] (16 B) @1096, iCash @1160
            byte[] p;try{p=BuildOutgoingMail();}catch(InvalidOperationException error){Text(error.Message);return;}
            sending = true; Busy(); Send(p); Sfx.Ui("Outgoing_chat");
        }
        void DrawBuddy(Event e, bool en)
        {
            var oc = GUI.color; GUI.BeginGroup(BuddyBox);
            if (e.type == EventType.Repaint && tBuddy != null) GUI.DrawTexture(new Rect(0, 0, BuddyBox.width, BuddyBox.height), tBuddy);
            Txt(BuddyTitle, Loc.T("BUDDY LIST"), St("label"), Color.white);
            if (Skin.Button(BuddyClose, "", St("closebut"), en)) buddyWin = false;
            var right = St("rightLabel"); var list = new List<Buddy>(); lock (bl) foreach (var b in buddies) if (b.Uid > 0 && !b.Blocked) list.Add(b);
            float h = 30f * list.Count, max = Mathf.Max(0f, h - BuddyList.height);
            if (e.type == EventType.ScrollWheel && BuddyList.Contains(e.mousePosition)) { buddyScroll = Mathf.Clamp(buddyScroll + e.delta.y * 20f, 0f, max); e.Use(); }
            buddyScroll = Mathf.Clamp(buddyScroll, 0f, max);
            GUI.BeginGroup(BuddyList);
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i]; string t = b.NameCheck != 1 ? Loc.T("Player") + " " + b.Uid : b.First + " " + b.Last; var r = new Rect(0f, 30f * i - buddyScroll, BuddyList.width - 30f, 30f);
                if (Plain(r, t, right, Color.white, en)) { toName = b.NameCheck != 1 ? Loc.T("Player") + " " + b.Uid : b.First + b.Last; toUid = b.Uid; buddyWin = false; }
            }
            GUI.EndGroup(); GUI.EndGroup();
        }
        void DrawCalc(Event e, bool en)
        {
            GUI.BeginGroup(CacuBox);
            if (e.type == EventType.Repaint && tCacu != null) GUI.DrawTexture(new Rect(0, 0, CacuBox.width, CacuBox.height), tCacu);
            Txt(CacuTitle, Loc.T("ADD TAROS"), St("label"), Color.white); Txt(CacuType, Loc.T("Amount to Add"), St("centerLabel"), Color.white);
            GUI.BeginGroup(CacuWin);
            if (e.type == EventType.Repaint && tCacuBack != null) GUI.DrawTexture(new Rect(0, 0, CacuWin.width, CacuWin.height), tCacuBack);
            Txt(CacuVal, calc.ToString(), St("rightLabel"), Color.white);
            string[] sty = { "cacubut", "cacubut1", "cacubut", "cacubut3", "cacubut", "cacubut", "cacubut", "cacubut", "cacubut", "cacubut", "cacubutc", "cacubutn" };
            int max = hud != null ? hud.Data.Taros : 0;
            for (int i = 0; i < 12; i++)
            {
                string label = i < 10 ? i.ToString() : i == 10 ? "C" : "";
                if (Skin.Button(CacuBtn[i], label, St(sty[i]), en))
                {
                    if (i < 10) { long v = (long)calc * 10 + i; calc = (int)Math.Min(v, max); } else if (i == 10) calc = 0;
                }
            }
            GUI.EndGroup();
            if (Skin.Button(CacuEnter, Loc.T("ADD"), St("button"), en)) { calcWin = false; taros = calc; calc = 0; }
            if (Skin.Button(CacuClose, "", St("closebut"), en)) calcWin = false;
            GUI.EndGroup();
        }
    }
}
