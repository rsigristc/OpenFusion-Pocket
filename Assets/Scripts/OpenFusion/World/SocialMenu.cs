// SocialMenu: acciones del menu de jugador de NpcIconMode (INVITE TO GROUP / MAKE A BUDDY / IGNORE / REPORT PLAYER) + cajas de invitacion recibidas (grupo y buddy) + lista local de bloqueados.
// Referencias: NpcIconMode.StartGroup/StartBuddy/StartBuddyOut/ReportPlayer/DoReportWindow, cnBuddySystemManager.func_*, cnGroupManager.func_FE2CL_GROUP_INVITE.
//   Paquetes (cuerpo con +4 de cabecera): REQ_PC_GROUP_INVITE {iID_To}; REQ_REQUEST_MAKE_BUDDY {iBuddyID, long iBuddyPCUID}; REQ_ACCEPT_MAKE_BUDDY {sbyte flag @0, iBuddyID @4, long uid @8};
//   REQ_SET_PC_BLOCK {iBlock_ID, long uid}; REQ_SET_BUDDY_BLOCK / REQ_REMOVE_BUDDY {long uid, sbyte slot @8}; reporte = chat libre "/report \"nombre\" \"motivo\"".
// W23: datos completos de la lista de buddies (sBuddyBaseInfo 72 B: iID@0, iPCUID@4, bBlocked@12, bFreeChat@13, iPCState@14, szFirstName[9]@16, szLastName[17]@34, iGender@68, iNameCheckFlag@69),
//   GET_BUDDY_STATE_SUCC (aBuddyID[50]@0 + aBuddyState[50]@200), mensaje "X is now online/offline." y peticion de RECALL (Status evento 28: caja de 20 s; REQ_WARP_USE_RECALL Size 4).
//   BUDDY LIST + WARP: BuddyListPanel.cs. ADD por nombre: SocialMenu.Buddies.cs. Pendiente: StartStore (User Store).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed partial class SocialMenu : MonoBehaviour
    {
        public static SocialMenu Instance;
        public static bool Active { get { return Instance != null && (Instance.pops.Count > 0 || Instance.reporting || Instance.addingBuddy); } }
        public static SocialMenu Ensure(GameObject host) { var s = host.GetComponent<SocialMenu>(); if (s == null) s = host.AddComponent<SocialMenu>(); Instance = s; return s; }

        class Pop { public int Kind; public string Title, Text, First, Last; public int FromId; public long FromUid; public float Left = 30f, Deadline; }   // Kind 4: buddy por nombre
        readonly List<Pop> pops = new List<Pop>();
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        PacketDispatcher disp; WorldEntities ents; MessageTableScript msgT;
        const int Slots = 50;
        readonly long[] slotUid = new long[Slots]; readonly bool[] slotBlk = new bool[Slots];

        /// <summary>Entrada de la lista de buddies (sBuddyBaseInfo).</summary>
        public sealed class Buddy
        {
            public int Slot, Id, State, NameCheck, Gender; public long Uid; public bool Blocked, FreeChat; public string First = "", Last = "";
            /// <summary>Texto de la lista: si iNameCheckFlag != 1 el original pone "Player" + iPCUID.</summary>
            public string ListName { get { return NameCheck != 1 ? "Player " + Uid : First + " " + Last; } }
            /// <summary>Nombre de los mensajes de warp/borrado: (iNameCheckFlag != 0) ? "Nombre Apellido" : "Player uid".</summary>
            public string MsgName { get { return NameCheck != 0 ? First + " " + Last : "Player " + Uid; } }
        }
        readonly Buddy[] bud = new Buddy[Slots];
        public const int BuddySlots = Slots;
        public Buddy BuddyAt(int slot) { return slot >= 0 && slot < Slots ? bud[slot] : null; }
        /// <summary>Sube cuando GET_BUDDY_STATE_SUCC cambia el estado de alguien (el original pone m_iBuddyIndex = -1).</summary>
        public int StateVersion;
        /// <summary>CnGuiChat.IsGroupMember: el buddy (por PCUID) esta en el grupo propio.</summary>
        public bool IsGroupMemberUid(long uid)
        {
            return GroupState.IsMemberUid(uid);
        }
        /// <summary>Buddies no bloqueados (los que muestra la lista y cuenta ChatHud.BuddyCountProvider).</summary>
        public int VisibleBuddyCount() { int n = 0; for (int i = 0; i < Slots; i++) { var x = bud[i]; if (x != null && x.Uid != 0 && !x.Blocked) n++; } return n; }
        /// <summary>Nombre de la lista para un PCUID (null si no es buddy): lo usa ChatHud.BuddyNameProvider.</summary>
        public string BuddyNameByUid(long uid) { for (int i = 0; i < Slots; i++) { var x = bud[i]; if (x != null && x.Uid == uid && uid != 0) return x.ListName; } return null; }
        /// <summary>REQ_GET_BUDDY_STATE (cuerpo 1 B, el struct vacio mide 1).</summary>
        public void RequestBuddyState() { Send(ClientPackets.New(PacketIds.P_CL2FE_REQ_GET_BUDDY_STATE, 1)); }
        /// <summary>CnGuiChat.RemoveBuddy: REQ_SET_BUDDY_BLOCK {long uid, sbyte slot} (12 B); el buddy queda bloqueado (y fuera de la lista visible).</summary>
        public void BlockBuddy(int slot)
        {
            var x = BuddyAt(slot); if (x == null) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_SET_BUDDY_BLOCK, 12); PutLong(p, 4, x.Uid); p[12] = (byte)slot; Send(p);
        }

        // ---- recall (Status evento 28 -> cnMainGame.RecallOk / RecallNo) ----
        bool recallLock;
        /// <summary>Hay una peticion de recall sin contestar: el avatar no envia movimiento (bEnableSendPacket = false).</summary>
        public static bool RecallPending { get { return Instance != null && Instance.recallLock; } }
        /// <summary>Un jugador del grupo usa la habilidad de recall sobre mi (sSkillResult_Move, iPC_ID del que la lanza != yo): caja Accept/Decline con cuenta atras de 20 s.</summary>
        public void OfferRecall(int casterId)
        {
            q.Enqueue(() =>
            {
                if (recallLock || casterId <= 0 || ents == null || ents.SelfId <= 0 || casterId == ents.SelfId) return;
                recallLock = true;
                pops.Add(new Pop { Kind = 3, FromId = casterId, Left = 20f, Deadline = Time.unscaledTime + 20f, Title = Loc.T("Skill Message"), Text = NameOf(casterId) + " is trying to recall you. Accept?" }); Sfx.Ui("Open_Screen");
            });
        }
        /// <summary>cnMainGame.RecallOk (iGroupMemberID = quien lanza) / RecallNo (0): REQ_WARP_USE_RECALL Size 4.</summary>
        void RecallAnswer(int groupMemberId)
        {
            var r = ClientPackets.New(PacketIds.P_CL2FE_REQ_WARP_USE_RECALL, 4); ClientPackets.PutInt(r, 4, groupMemberId); Send(r);
            recallLock = false;
            if (groupMemberId != 0) {
                WarpFlow.Sent();
                var self = ents != null ? ents.Self : null;
                if (self != null && BulletFx.Instance != null) BulletFx.Instance.Fire(93, self.transform, self.transform, 1.5f, self.transform.position + Vector3.up);
            }
        }
        void ClearRecall() { recallLock = false; pops.RemoveAll(p => p.Kind == 3); }
        void OnEnter(EnterSucc snapshot) { q.Enqueue(() => ResetSocial(snapshot.Self != null ? snapshot.Self.Uid : 0)); }
        void OnRelocated(int x, int y, int z) { q.Enqueue(ClearRecall); }

        // informe
        bool reporting; string repName = "", repReason = "", repNote; int repId; long repUid;

        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t)
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnter -= OnEnter; disp.Teleport -= OnRelocated; }
            ClearRecall();
            ResetSocial(0);
            disp = d; ents = e; msgT = t != null ? t.m_pMessageTable : null;
            if (disp != null) { disp.Unhandled += OnPacket; disp.SelfEnter += OnEnter; disp.Teleport += OnRelocated; }
            BindChat();
        }
        ChatHud boundChat;
        void BindChat()
        {
            if(boundChat!=null) {
                if(boundChat.IsBlockedPc==(Func<int,bool>)IsBlockedId)boundChat.IsBlockedPc=null;
                if(boundChat.IsBlockedUid==(Func<long,bool>)IsBlockedUid)boundChat.IsBlockedUid=null;
            }
            boundChat=GetComponent<ChatHud>();
            if(boundChat!=null) {boundChat.IsBlockedPc=IsBlockedId;boundChat.IsBlockedUid=IsBlockedUid;}
        }
        void Awake() { Instance = this; }
        void OnDestroy() { if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnter -= OnEnter; disp.Teleport -= OnRelocated; } if(boundChat!=null) {if(boundChat.IsBlockedPc==(Func<int,bool>)IsBlockedId)boundChat.IsBlockedPc=null;if(boundChat.IsBlockedUid==(Func<long,bool>)IsBlockedUid)boundChat.IsBlockedUid=null;} if (Instance == this) Instance = null; }

        // ---------------- datos ----------------
        string Msg(int id) { var m = msgT != null ? msgT.m_pMessageData : null; return m != null && id >= 0 && id < m.Length && m[id] != null ? m[id].m_szString ?? "" : "(message " + id + ")"; }
        static string NameOf(int id) { return TradeWindow.Instance != null ? TradeWindow.Instance.FullName(id) : "?"; }
        static long UidOf(int id) { return TradeWindow.Instance != null ? TradeWindow.Instance.UidOf(id) : 0L; }
        int SlotOf(long uid) { if (uid == 0) return -1; for (int i = 0; i < Slots; i++) if (slotUid[i] == uid) return i; return -1; }
        int FreeSlot() { for (int i = 0; i < Slots; i++) if (slotUid[i] == 0) return i; return -1; }
        public bool IsBlockedUid(long uid) { int s = SlotOf(uid); return s >= 0 && slotBlk[s]; }
        public bool IsBlockedId(int id) { if(id<=0)return false;long uid=UidOf(id);if(uid!=0)return IsBlockedUid(uid);foreach(var b in bud)if(b!=null&&b.Id==id&&b.State!=0&&b.Blocked)return true;return false; }

        static void PutLong(byte[] b, int o, long v) { BitConverter.GetBytes(v).CopyTo(b, o); }
        static void Send(byte[] p) { if (NpcUI.Sender != null) NpcUI.Sender(p); }
        void Info(int msgId, string name, bool fail) { pops.Add(new Pop { Kind = 0, Title = "", Text = Msg(msgId).Replace("%s", name) }); Sfx.Ui(fail ? "Action_Failure01" : "Action_Sucess"); }

        /// <summary>Caja informativa con boton OK (texto fijo; seguro desde cualquier hilo).</summary>
        public void Notice(string text) { q.Enqueue(() => { pops.Add(new Pop { Kind = 0, Text = text }); Sfx.Ui("Action_Failure01"); }); }

        // ---------------- acciones del menu ----------------
        public void InviteGroup(int id) { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_GROUP_INVITE, 4); ClientPackets.PutInt(p, 4, id); Send(p); }

        public void MakeBuddy(int id)
        {
            long uid = UidOf(id); int s = SlotOf(uid);
            if (s >= 0) { Info(slotBlk[s] ? 105 : 104, NameOf(id), true); return; }   // ya es buddy (104) o esta bloqueado (105)
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_REQUEST_MAKE_BUDDY, 12); ClientPackets.PutInt(p, 4, id); PutLong(p, 8, uid); Send(p);
        }

        public bool IsIgnored(int id) { return IsBlockedId(id); }
        /// <summary>StartBuddyOut: si esta bloqueado se quita de la lista; si ya esta en la lista de buddies se marca bloqueado; si no, SET_PC_BLOCK.</summary>
        public void ToggleIgnore(int id)
        {
            long uid = UidOf(id); int s = SlotOf(uid);
            if (s >= 0 && slotBlk[s]) { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_REMOVE_BUDDY, 12); PutLong(p, 4, uid); p[12] = (byte)s; Send(p); }
            else if (s >= 0) { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_SET_BUDDY_BLOCK, 12); PutLong(p, 4, uid); p[12] = (byte)s; Send(p); }
            else { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_SET_PC_BLOCK, 12); ClientPackets.PutInt(p, 4, id); PutLong(p, 8, uid); Send(p); }
        }

        public void Report(int id) { repId = id; repUid = UidOf(id); repName = TradeWindow.Instance != null ? TradeWindow.Instance.FullName(id) : "Player " + repUid; repReason = ""; reporting = true; }

        // ---------------- red ----------------
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static long L(byte[] b, int o) { return BitConverter.ToInt64(b, o); }
        static string WStr(byte[] b, int o, int chars)
        {
            int n = 0; while (n < chars && o + 2 * n + 1 < b.Length && (b[o + 2 * n] != 0 || b[o + 2 * n + 1] != 0)) n++;
            return n == 0 ? "" : System.Text.Encoding.Unicode.GetString(b, o, n * 2);
        }
        /// <summary>sBuddyBaseInfo en el desplazamiento o (72 B).</summary>
        static Buddy ParseBuddy(byte[] b, int o, int slot)
        {
            if (o < 0 || o + 70 > b.Length) return null;
            return new Buddy { Slot = slot, Id = I(b, o), Uid = L(b, o + 4), Blocked = b[o + 12] != 0, FreeChat = b[o + 13] != 0, State = (sbyte)b[o + 14], First = WStr(b, o + 16, 9), Last = WStr(b, o + 34, 17), Gender = (sbyte)b[o + 68], NameCheck = (sbyte)b[o + 69] };
        }
        void OnPacket(uint type, byte[] b)   // hilo de red
        {
            if (b == null) return;
            if (HandleNamedBuddyPacket(type, b)) return;
            if (type == PacketIds.P_FE2CL_PC_GROUP_INVITE_FAIL && b.Length >= 4) { q.Enqueue(() => Info(59, "", true)); return; }
            if (type == PacketIds.P_FE2CL_PC_GROUP_JOIN_FAIL && b.Length >= 8) { q.Enqueue(() => Info(60, "", true)); return; }
            if (type == PacketIds.P_FE2CL_PC_GROUP_LEAVE_FAIL && b.Length >= 8) { q.Enqueue(() => Sfx.Ui("Action_Failure01")); return; }
            if (type == PacketIds.P_FE2CL_PC_GROUP_INVITE_REFUSE && b.Length >= 4) { int id = I(b, 0); q.Enqueue(() => Info(62, NameOf(id), true)); return; }
            OnPacket2(type, b);
            if (type == PacketIds.P_FE2CL_PC_GROUP_INVITE && b.Length >= 4)
            {   // con "ALLOW GROUP REQUESTS" = No ya lo rechaza GroupState
                int host = I(b, 0); q.Enqueue(() =>
                {
                    if (!GameOptions.Cur.AllowGroup || host <= 0 || ents != null && host == ents.SelfId) return;
                    if (IsBlockedId(host)) { var decline = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_GROUP_INVITE_REFUSE, 4); ClientPackets.PutInt(decline, 4, host); Send(decline); return; }
                    if (pops.Exists(p => p.Kind == 1 && p.FromId == host)) return;
                    pops.Add(new Pop { Kind = 1, FromId = host, Deadline = Time.unscaledTime + 30f, Title = Loc.T("GROUP INVITE RECEIVED"), Text = Msg(61).Replace("%s", NameOf(host)) }); Sfx.Ui("Open_Screen");
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_REQUEST_MAKE_BUDDY_SUCC_TO_ACCEPTER && b.Length >= 60)
            {   // iRequestID @0, iBuddyID @4 (yo), szFirstName/LastName
                int req = I(b, 0), me = I(b, 4); q.Enqueue(() =>
                {
                    if (ents == null || me != ents.SelfId || req <= 0 || req == ents.SelfId) return; long uid = UidOf(req); if (uid <= 0 || IsBlockedUid(uid)) return;
                    if (!GameOptions.Cur.AllowBuddy) { SendAccept(0, req, uid); return; }
                    if (pops.Exists(p => p.Kind == 2 && p.FromId == req)) return;
                    pops.Add(new Pop { Kind = 2, FromId = req, FromUid = uid, Deadline = Time.unscaledTime + 30f, Title = Loc.T("BUDDY INVITE RECEIVED"), Text = Msg(69).Replace("%s", NameOf(req)) }); Sfx.Ui("Open_Screen");
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_REQUEST_MAKE_BUDDY_FAIL && b.Length >= 16)
            {
                int id = I(b, 0), err = I(b, 12); q.Enqueue(() => { int m = err == 3 ? 145 : err == 4 ? 139 : err == 5 ? 146 : 105; Info(m, NameOf(id), true); });
            }
            else if (type == PacketIds.P_FE2CL_REP_ACCEPT_MAKE_BUDDY_SUCC && b.Length >= 76)
            {   // iBuddySlot @0, BuddyInfo @4 (iID, long iPCUID @8, bBlocked @16)
                int slot = (sbyte)b[0], id = I(b, 4); long uid = L(b, 8); bool blk = b[16] != 0;
                var nb = ParseBuddy(b, 4, slot); q.Enqueue(() => { if (slot < 0 || slot >= Slots || nb == null || uid <= 0) return; slotUid[slot] = uid; slotBlk[slot] = blk; bud[slot] = nb; StateVersion++; Info(140, nb.MsgName, false); });
            }
            else if (type == PacketIds.P_FE2CL_REP_ACCEPT_MAKE_BUDDY_FAIL && b.Length >= 16)
            {
                int id = I(b, 0), err = I(b, 12); long uid = L(b, 4);
                q.Enqueue(() => { if (err == 6) Info(138, NameOf(id), true); });
            }
            else if (type == PacketIds.P_FE2CL_REP_SET_PC_BLOCK_SUCC && b.Length >= 13)
            {
                int bid = I(b, 0); long uid = L(b, 4); int slot = (sbyte)b[12]; q.Enqueue(() => { if (slot >= 0 && slot < Slots) { slotUid[slot] = uid; slotBlk[slot] = true; bud[slot] = new Buddy { Slot = slot, Id = bid, Uid = uid, Blocked = true, First = NameOf(bid), NameCheck = 1 }; } });
            }
            else if (type == PacketIds.P_FE2CL_REP_SET_BUDDY_BLOCK_SUCC && b.Length >= 9)
            {
                long uid = L(b, 0); int slot = (sbyte)b[8]; q.Enqueue(() => { if (slot >= 0 && slot < Slots && slotUid[slot] == uid) { slotBlk[slot] = true; if (bud[slot] != null) bud[slot].Blocked = true; } });
            }
            else if (type == PacketIds.P_FE2CL_REP_REMOVE_BUDDY_SUCC && b.Length >= 9)
            {
                long uid = L(b, 0); int slot = (sbyte)b[8]; q.Enqueue(() => { if (slot >= 0 && slot < Slots && slotUid[slot] == uid) { slotUid[slot] = 0; slotBlk[slot] = false; bud[slot] = null; RequestBuddyState(); } });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_BUDDYLIST_INFO_SUCC && b.Length >= 16)
            {   // iID @0, iPCUID @4, iListNum @12, iBuddyCnt @13, sBuddyBaseInfo[72] @16 (iID @0, iPCUID @4, bBlocked @12)
                int first = (sbyte)b[12], cnt = (sbyte)b[13]; var body = b;
                if (first < 0 || cnt < 0 || first + cnt > Slots || 16 + 72 * cnt > b.Length) return;
                q.Enqueue(() =>
                {
                    for (int i = 0; i < cnt && first >= 0 && first + i < Slots; i++)
                    {
                        int o = 16 + 72 * i; if (o + 13 > body.Length) break; int sl = first + i;
                        var nb = ParseBuddy(body, o, sl); var old = bud[sl];
                        slotUid[sl] = L(body, o + 4); slotBlk[sl] = body[o + 12] != 0; bud[sl] = nb != null && nb.Uid != 0 ? nb : null;
                        if (nb != null && nb.Uid > 0 && old != null && old.Uid == nb.Uid && old.State != nb.State && !nb.Blocked)
                        {   // cnBuddySystemManager.func_FE2CL_PC_BUDDYLIST_INFO_SUCC: aviso de conexion
                            string nm = nb.First + " " + nb.Last; if (old.NameCheck != 1) nm = "Player " + old.Uid;
                            ChatHud.AddEventString(2, nm + " is now " + (nb.State > 0 ? "online" : "offline") + ".");
                        }
                    }
                });
            }
        }

        // (continuacion de OnPacket) GET_BUDDY_STATE_SUCC y RECALL_FAIL se tratan en OnPacket2, llamado desde OnPacket.
        void OnPacket2(uint type, byte[] b)
        {
            if (type == PacketIds.P_FE2CL_REP_GET_BUDDY_STATE_SUCC && b.Length >= 250)
            {   // aBuddyID[50] @0 + aBuddyState[50] @200 (252 B)
                var body = b; q.Enqueue(() =>
                {
                    bool ch = false;
                    for (int i = 0; i < Slots; i++) { var x = bud[i]; if (x == null) continue; x.Id = I(body, 4 * i); int st = (sbyte)body[200 + i]; if (x.State != st) ch = true; x.State = st; }
                    if (ch) StateVersion++;
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_WARP_USE_RECALL_FAIL)
            {   // GameFrame 822083804: bRecallLock = false; bEnableSendPacket = true
                q.Enqueue(ClearRecall);
            }
        }

        void SendAccept(int flag, int id, long uid)
        {
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_ACCEPT_MAKE_BUDDY, 16); p[4] = (byte)flag; ClientPackets.PutInt(p, 8, id); PutLong(p, 12, uid); Send(p);
        }

        void Update()
        {
            Action a; int n = 0; while (n++ < 16 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[social] " + ex.Message); } }
            UpdateAddBuddy();
            // Invitations expire even behind an older informational popup.
            foreach (var pending in pops.ToArray()) if (pending.Kind != 0) {
                pending.Left = Mathf.Max(0, pending.Deadline - Time.unscaledTime);
                if (pending.Left <= 0) Resolve(pending, false);
            }
            if (pops.Count > 0)
            {
                var p = pops[0];
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { if (p.Kind == 0) pops.RemoveAt(0); }
            }
            if (reporting && Input.GetKeyDown(KeyCode.Escape)) reporting = false;
        }

        void Resolve(Pop p, bool yes)
        {
            if (!pops.Remove(p)) return;
            if (p.Kind == 1) { var r = ClientPackets.New(yes ? PacketIds.P_CL2FE_REQ_PC_GROUP_JOIN : PacketIds.P_CL2FE_REQ_PC_GROUP_INVITE_REFUSE, 4); ClientPackets.PutInt(r, 4, p.FromId); Send(r); }
            else if (p.Kind == 2) SendAccept(yes ? 1 : 0, p.FromId, p.FromUid);
            else if (p.Kind == 4) SendNamedBuddyAnswer(p, yes);
            else if (p.Kind == 3) { RecallAnswer(yes ? p.FromId : 0); if (yes) Sfx.Ui("Recall_Warp"); }
            Sfx.Ui(yes ? "Yes_Button" : "No_Button");
        }

        // ---------------- interfaz ----------------
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        GUIStyle field;
        void OnGUI()
        {
            if (!Active || Screen.height <= 0) return; var e = Event.current; if (e == null) return;
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f); var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1450;
            try
            {
                var red = Skin.Get(Skin.Menu, "RedButton") ?? Skin.Get(Skin.Inven, "RedButton"); var cancel = Skin.Get(Skin.Menu, "CancelButton") ?? Skin.Get(Skin.Inven, "CancelButton"); var lab = Skin.Get(Skin.Inven, "label");
                GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc); GUI.color = Color.white;
                if (pops.Count > 0) DrawPop(e, pops[0], red, cancel, lab);
                else if (addingBuddy) DrawAddBuddy(e, red, cancel, lab);
                else if (reporting) DrawReport(e, red, cancel, lab);
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        static void Panel(Rect r, Event e)
        {
            if (e.type != EventType.Repaint) return;
            GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = new Color(0f, 0.8f, 0.9f, 1f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture); GUI.color = Color.white;
        }

        void DrawPop(Event e, Pop p, SkinStyle red, SkinStyle cancel, SkinStyle lab)
        {   // NanoPopupRect (520x164) centrada, como la invitacion de intercambio
            var r = new Rect(-260f, -82f, 520f, 164f); Panel(r, e); GUI.BeginGroup(r);
            if (!string.IsNullOrEmpty(p.Title)) Skin.Label(new Rect(100, 0, 400, 28), p.Title, lab, new Color(1f, 0.92f, 0.016f));
            string t = p.Text; if (p.Kind == 3) t += "\n" + Loc.T("Skill will expire") + " :" + Mathf.CeilToInt(p.Left) + " " + Loc.T("seconds"); else if (p.Kind != 0) t += "\n" + Mathf.CeilToInt(p.Left) + " " + Loc.T("seconds");
            Skin.Label(new Rect(40, 30, 440, 90), t, lab, Color.white);
            if (p.Kind == 0) { if (Skin.Button(new Rect(185, 124, 150, 25), Loc.T("OK"), red)) { pops.Remove(p); GUI.EndGroup(); return; } }
            else
            {
                if (Skin.Button(new Rect(44, 124, 150, 25), Loc.T("Decline"), cancel)) { Resolve(p, false); GUI.EndGroup(); return; }
                if (Skin.Button(new Rect(334, 124, 150, 25), Loc.T("Accept"), red)) { Resolve(p, true); GUI.EndGroup(); return; }
            }
            GUI.EndGroup();
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && r.Contains(e.mousePosition)) e.Use();
        }

        void DrawReport(Event e, SkinStyle red, SkinStyle cancel, SkinStyle lab)
        {   // DoReportWindow: 526x164 centrada, fondo negro 0.75
            if (e.type == EventType.Repaint) { GUI.color = new Color(0f, 0f, 0f, 0.75f); GUI.DrawTexture(new Rect(-4000f, -3000f, 8000f, 6000f), Texture2D.whiteTexture); GUI.color = Color.white; }
            var r = new Rect(-263f, -82f, 526f, 164f); Panel(r, e); GUI.BeginGroup(r);
            Skin.Label(new Rect(100, 15, 326, 20), Loc.T("REPORT PLAYER - %s").Replace("%s", repName), lab, new Color(1f, 0.92f, 0.016f));
            Skin.Label(new Rect(40, 36, 446, 34), Loc.T("Please enter your reason for reporting this player:\nNOTE: FALSE REPORTS CAN AND WILL LEAD TO ACTION TAKEN ON YOUR ACCOUNT."), lab, Color.white);
            if (field == null) { field = new GUIStyle(GUI.skin.textField) { fontSize = 14 }; }
            GUI.SetNextControlName("reportname"); repReason = GUI.TextField(new Rect(75, 73, 378, 24), repReason ?? "", 55, field); if (repReason.IndexOf('\n') >= 0) repReason = repReason.Replace("\n", "");
            if (Skin.Button(new Rect(40, 125, 152, 27), Loc.T("CANCEL"), cancel)) { reporting = false; GUI.EndGroup(); return; }
            if (Skin.Button(new Rect(335, 125, 152, 27), Loc.T("SUBMIT REPORT"), red))
            {
                if (repReason.Length >= 4)
                {
                    Send(ChatPackets.FreeChat("/report \"" + repName + "\" \"" + repReason + "\"")); reporting = false;
                    pops.Add(new Pop { Kind = 0, Text = Loc.T("Your report has been received. Thanks for helping us make FusionFall a better place!") }); Sfx.Ui("Yes_Button");
                }
                else { pops.Insert(0, new Pop { Kind = 0, Text = Loc.T("You must provide a detailed reason for reporting this player.") }); Sfx.Ui("Action_Failure01"); }
                GUI.EndGroup(); return;
            }
            GUI.EndGroup(); GUI.FocusControl("reportname");
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition) == false) { /* el fondo bloquea clics */ e.Use(); }
        }
    }
}
