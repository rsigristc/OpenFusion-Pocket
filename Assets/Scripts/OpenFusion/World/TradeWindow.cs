// TradeWindow: port de cnTrade + Panel_Trade (Win.Trade) + menu de jugador (NpcIconMode.DoPcWindow: INVITE TO TRADE) + caja de invitacion (msg 26). SIN COMPILAR NI PROBAR EN UNITY.
// Ids de cada paquete: iID_Request (@0) = quien emite, iID_From (@4) = invitador, iID_To (@8) = invitado. Cada cliente guarda From/To de la invitacion y los usa en todos sus envios con Request = yo.
// Estados: Idle -> (invitador espera OFFER_SUCC / invitado acepta) -> Trading (ventana) -> Idle. El servidor decide el final (CONFIRM_SUCC); al cancelar se restaura el inventario local desde el snapshot.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class TradeWindow : ModalWindow, IInvMode
    {
        public static TradeWindow Instance;
        public static TradeWindow Ensure(GameObject host) { var w = host.GetComponent<TradeWindow>(); if (w == null) w = host.AddComponent<TradeWindow>(); return w; }
        public static bool Active { get { return Instance != null && Instance.active; } }
        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h, JournalWindow j)
        {
            InitBase(d, e, t, h, j);
            if (d != null) d.PcEnter += OnPcEnter;
        }
        protected override GameMenu.Win WinId { get { return GameMenu.Win.Trade; } }
        protected override string FrameName { get { return "tradeback"; } }
        protected override IInvMode InvMode { get { return this; } }
        protected override void Awake() { Instance = this; base.Awake(); if (GetComponent<TradeOverlay>() == null) gameObject.AddComponent<TradeOverlay>(); }
        protected override void OnDestroy() { if (disp != null) disp.PcEnter -= OnPcEnter; base.OnDestroy(); }

        // ---- datos ----
        readonly ConcurrentDictionary<int, string[]> names = new ConcurrentDictionary<int, string[]>();
        readonly ConcurrentDictionary<int, long> uids = new ConcurrentDictionary<int, long>();
        void OnPcEnter(PcAppearance a) { if (a == null) return; names[a.Id] = new[] { (a.FirstName ?? "").Trim(), (a.LastName ?? "").Trim() }; uids[a.Id] = a.Uid; apps[a.Id] = a; }
        readonly ConcurrentDictionary<int, PcAppearance> apps = new ConcurrentDictionary<int, PcAppearance>();
        AvatarPreview faceOwn, faceOther;
        /// <summary>rectOwnFace / rectFace (70x70): retrato 3D de la cabeza (camara del avatar).</summary>
        void DrawFace(Rect r, bool own, bool rep)
        {
            var iw = InventoryWindow.Instance; if (iw == null || iw.Preview == null || iw.Preview.Factory == null) return;
            PcAppearance a; if (own) a = iw.SelfApp; else if (!apps.TryGetValue(OtherId, out a)) a = null; if (a == null) return;
            var p = own ? faceOwn : faceOther; if (p == null) { p = AvatarPreview.CreateExtra(gameObject, iw.Preview.Factory, true); if (own) faceOwn = p; else faceOther = p; }
            var t = p.Get(a, 140, 140); if (rep && t != null) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
        }
        public long UidOf(int id) { long u; return uids.TryGetValue(id, out u) ? u : 0L; }
        public string FullName(int id) { string[] n; if (names.TryGetValue(id, out n)) return (n[0] + " " + n[1]).Trim(); return Loc.T("unknown"); }
        public string FirstName(int id) { string[] n; if (names.TryGetValue(id, out n)) return n[0]; return Loc.T("unknown"); }
        int Me { get { return ents != null ? ents.SelfId : 0; } }

        bool active; int iFrom, iTo; bool inviteeSide; bool completed;
        readonly ItemBase[] own = new ItemBase[5], other = new ItemBase[5];
        int ownMoney, otherMoney; bool ownReady, userReady; bool otherCanceled;
        ItemBase[] snap; readonly List<string> chat = new List<string>(); float chatScroll; string chatText = ""; GUIStyle field; float fade;
        public string InviteText() { return Message(26).Replace("%s", InviteName); }
        int OtherId { get { return Me == iTo ? iFrom : iTo; } }

        // ---- invitacion recibida (caja nano) ----
        public bool InviteShown; public int InviteFrom, InviteTo; public float InviteLeft; public string InviteName = "";

        // ============================================================ envio ============================================================
        byte[] Req(uint type, int extra = 0) { var p = ClientPackets.New(type, 12 + extra); PutI(p, 4, Me); PutI(p, 8, iFrom); PutI(p, 12, iTo); return p; }
        /// <summary>Menu de jugador: INVITE TO TRADE.</summary>
        public void Invite(int targetId)
        {
            if (ents == null || active || GameMenu.Current != GameMenu.Win.None) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_TRADE_OFFER, 12); PutI(p, 4, Me); PutI(p, 8, Me); PutI(p, 12, targetId); Send(p); ChatHud.AddEventString(4, "Trade request sent.");
        }
        public void AcceptInvite()
        {
            if (!InviteShown) return; InviteShown = false;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_TRADE_OFFER_ACCEPT, 12); PutI(p, 4, Me); PutI(p, 8, InviteFrom); PutI(p, 12, InviteTo); Send(p);
            iFrom = InviteFrom; iTo = InviteTo; StartTrade(true); Sfx.Ui("Yes_Button");
        }
        public void DeclineInvite()
        {
            if (!InviteShown) return; InviteShown = false;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_TRADE_OFFER_REFUSAL, 12); PutI(p, 4, Me); PutI(p, 8, InviteFrom); PutI(p, 12, InviteTo); Send(p); Sfx.Ui("No_Button");
        }
        void Cancel(uint type, int from, int to) { var p = ClientPackets.New(type, 12); PutI(p, 4, Me); PutI(p, 8, from); PutI(p, 12, to); Send(p); }

        void StartTrade(bool invitee)
        {
            if (active) return; active = true; inviteeSide = invitee; completed = false; otherCanceled = false;
            Array.Clear(own, 0, 5); Array.Clear(other, 0, 5); ownMoney = otherMoney = 0; ownReady = userReady = false; chat.Clear(); chatText = ""; waiting = false;
            snap = (ItemBase[])InventoryWindow.Items.Clone();
            if (!GameMenu.OpenWindow(GameMenu.Win.Trade)) { active = false; Cancel(PacketIds.P_CL2FE_REQ_PC_TRADE_OFFER_CANCEL, iFrom, iTo); }
        }
        /// <summary>TradeOut: cierra la ventana (sin enviar nada) y restaura el inventario si el trade no se completo.</summary>
        void TradeOut()
        {
            if (MenuChatPanel.Instance != null) MenuChatPanel.Instance.CloseInline();
            if (!active) return; active = false; if (!completed && snap != null) for (int i = 0; i < 50 && i < snap.Length; i++) InventoryWindow.Items[i] = snap[i]; snap = null;
            if (GameMenu.Current == GameMenu.Win.Trade) GameMenu.CloseWindow();
        }
        protected override void OnClosed()
        {   // cierre iniciado por el jugador (ESC / X): REQ CONFIRM_CANCEL y restaurar
            if (active) { Send(Req(PacketIds.P_CL2FE_REQ_PC_TRADE_CONFIRM_CANCEL)); TradeOut(); }
            InventoryWindow.Typing = false;
        }

        // ============================================================ red ============================================================
        protected override void OnPacket(uint type, byte[] b)
        {
            if (b == null || type < PacketIds.P_FE2CL_REP_PC_TRADE_OFFER || (type > PacketIds.P_FE2CL_REP_PC_TRADE_EMOTES_CHAT && type != PacketIds.P_FE2CL_REP_PC_TRADE_EMOTES_CHAT_FAIL) || b.Length < 12) return;
            var body = b; uint t = type; q.Enqueue(() => Handle(t, body));
        }

        void Handle(uint type, byte[] b)
        {
            int req = I(b, 0), from = I(b, 4), to = I(b, 8); int me = Me;
            if (type == PacketIds.P_FE2CL_REP_PC_TRADE_OFFER)
            {   // invitado: (a) rechazo por opcion, (b) no disponible, (d) caja de invitacion
                if (!GameOptions.Cur.AllowTrade) return;     // GroupState ya envia el REFUSAL
                if (InCombat() && names.ContainsKey(from)) { Cancel(PacketIds.P_CL2FE_REQ_PC_TRADE_OFFER_CANCEL, from, to); if (SocialMenu.Instance != null) SocialMenu.Instance.Notice(FullName(from) + " wanted to trade with you, but you could not accept because you are in combat."); return; }
                if (active || GameMenu.Current != GameMenu.Win.None || NpcUI.Active || InviteShown || !names.ContainsKey(from)) { Cancel(PacketIds.P_CL2FE_REQ_PC_TRADE_OFFER_CANCEL, from, to); return; }
                InviteShown = true; InviteFrom = from; InviteTo = to; InviteLeft = 20f; InviteName = FullName(from); Sfx.Ui("Open_Screen"); return;
            }
            if (type == PacketIds.P_FE2CL_REP_PC_TRADE_OFFER_SUCC)
            {
                if (active && from == iFrom && to == iTo) { Release(); return; }
                if (active) { Cancel(PacketIds.P_CL2FE_REQ_PC_TRADE_OFFER_CANCEL, from, to); return; }
                if (GameMenu.Current != GameMenu.Win.None) { Cancel(PacketIds.P_CL2FE_REQ_PC_TRADE_OFFER_CANCEL, from, to); return; }
                iFrom = from; iTo = to; StartTrade(false); return;
            }
            if (type == PacketIds.P_FE2CL_REP_PC_TRADE_OFFER_REFUSAL && !active)
            {
                if (req != me) ChatHud.AddEventString(4, Message(27).Replace("%s", FullName(to))); return;
            }
            if (type == PacketIds.P_FE2CL_REP_PC_TRADE_OFFER_ABORT && !active) { ChatHud.AddEventString(4, Loc.T("Trade cancelled.")); return; }
            if (type == PacketIds.P_FE2CL_REP_PC_TRADE_CONFIRM_CANCEL && !active) { ChatHud.AddEventString(4, Loc.T("Trade cancelled.")); return; }
            if (type == PacketIds.P_FE2CL_REP_PC_TRADE_OFFER_CANCEL && !active) { if (InviteShown && InviteFrom == from) InviteShown = false; return; }
            if (!active) return;
            switch (type)
            {
                case PacketIds.P_FE2CL_REP_PC_TRADE_OFFER_CANCEL:
                case PacketIds.P_FE2CL_REP_PC_TRADE_CONFIRM_CANCEL:
                case PacketIds.P_FE2CL_REP_PC_TRADE_CONFIRM_ABORT:
                case PacketIds.P_FE2CL_REP_PC_TRADE_OFFER_ABORT:
                case PacketIds.P_FE2CL_REP_PC_TRADE_OFFER_REFUSAL:
                    Release();
                    if (req == me) { TradeOut(); return; }
                    otherCanceled = true; int mid = type == PacketIds.P_FE2CL_REP_PC_TRADE_OFFER_ABORT ? 28 : type == PacketIds.P_FE2CL_REP_PC_TRADE_OFFER_CANCEL ? 29 : 30;
                    MsgThen(mid, TradeOut, FirstName(req)); return;
                case PacketIds.P_FE2CL_REP_PC_TRADE_CONFIRM_FAIL: Release(); otherCanceled = true; MsgThen(31, TradeOut); return;
                case PacketIds.P_FE2CL_REP_PC_TRADE_CONFIRM: Release(); if (req == me) ownReady = true; else userReady = true; return;
                case PacketIds.P_FE2CL_REP_PC_TRADE_ITEM_REGISTER_SUCC:
                    if (b.Length >= 44)
                    {
                        var tr = TItem(b, 12); var inv = TItem(b, 28);
                        if (req == me)
                        {
                            if (tr.Type == 7) { if (inv.InvIdx >= 0 && inv.InvIdx < 50) InventoryWindow.Items[inv.InvIdx] = inv.Opt == 0 ? default(ItemBase) : new ItemBase { Type = (short)inv.Type, Id = (short)inv.Id, Opt = inv.Opt }; }
                            else if (tr.InvIdx >= 0 && tr.InvIdx < 50) InventoryWindow.Items[tr.InvIdx] = default(ItemBase);
                            if (tr.Slot >= 0 && tr.Slot < 5) own[tr.Slot] = new ItemBase { Type = (short)tr.Type, Id = (short)tr.Id, Opt = tr.Opt };
                            Sfx.Ui("Action_Sucess");
                        }
                        else if (tr.Slot >= 0 && tr.Slot < 5) other[tr.Slot] = new ItemBase { Type = (short)tr.Type, Id = (short)tr.Id, Opt = tr.Opt };
                        ownReady = userReady = false; Release();
                    }
                    return;
                case PacketIds.P_FE2CL_REP_PC_TRADE_ITEM_UNREGISTER_SUCC:
                    if (b.Length >= 44)
                    {
                        var tr = TItem(b, 12); var inv = TItem(b, 28);
                        if (req == me) { if (inv.InvIdx >= 0 && inv.InvIdx < 50) InventoryWindow.Items[inv.InvIdx] = new ItemBase { Type = (short)inv.Type, Id = (short)inv.Id, Opt = inv.Opt }; if (tr.Slot >= 0 && tr.Slot < 5) own[tr.Slot] = default(ItemBase); }
                        else if (tr.Slot >= 0 && tr.Slot < 5) other[tr.Slot] = default(ItemBase);
                        ownReady = userReady = false; Release();
                    }
                    return;
                case PacketIds.P_FE2CL_REP_PC_TRADE_ITEM_REGISTER_FAIL: Release(); Msg(32); return;
                case PacketIds.P_FE2CL_REP_PC_TRADE_ITEM_UNREGISTER_FAIL: Release(); Msg(33); return;
                case PacketIds.P_FE2CL_REP_PC_TRADE_CASH_REGISTER_SUCC:
                    if (b.Length >= 16) { int c = I(b, 12); if (req == me) ownMoney = c; else otherMoney = c; ownReady = userReady = false; Sfx.Ui("Action_Sucess"); }
                    Release(); return;
                case PacketIds.P_FE2CL_REP_PC_TRADE_CASH_REGISTER_FAIL: Release(); Msg(33); return;
                case PacketIds.P_FE2CL_REP_PC_TRADE_CONFIRM_SUCC:
                    if (b.Length >= 208)
                    {
                        hud.Data.Taros = I(b, 204);
                        for (int i = 0; i < 12; i++) { var it = TItem(b, 12 + i * 16); if (it.Id > 0 && it.InvIdx >= 0 && it.InvIdx < 50) InventoryWindow.Items[it.InvIdx] = new ItemBase { Type = (short)it.Type, Id = (short)it.Id, Opt = it.Opt }; }
                    }
                    completed = true; Sfx.Ui("Action_Sucess"); Release(); TradeOut(); ChatHud.AddEventString(4, Message(160)); return;
                case PacketIds.P_FE2CL_REP_PC_TRADE_EMOTES_CHAT:
                    if (b.Length >= 268) { string s = ReadStr(b, 12, 128); chat.Add(FullName(req == me ? me : req) + ": " + s); if (req == me) { chat[chat.Count - 1] = hud.Data.Name + ": " + s; } while (chat.Count > 41) chat.RemoveAt(0); chatScroll = 1e6f; }
                    return;
            }
        }
        struct TI { public int Type, Id, Opt, InvIdx, Slot; }
        static TI TItem(byte[] b, int o) { return new TI { Type = BitConverter.ToInt16(b, o), Id = BitConverter.ToInt16(b, o + 2), Opt = BitConverter.ToInt32(b, o + 4), InvIdx = BitConverter.ToInt32(b, o + 8), Slot = BitConverter.ToInt32(b, o + 12) }; }
        static string ReadStr(byte[] b, int o, int chars) { var s = System.Text.Encoding.Unicode.GetString(b, o, Math.Min(chars * 2, b.Length - o)); int z = s.IndexOf('\0'); return z >= 0 ? s.Substring(0, z) : s; }

        // ============================================================ acciones ============================================================
        int FreeOwn() { for (int i = 0; i < 5; i++) if (own[i].Id <= 0) return i; return -1; }
        void Register(int invSlot, int hole, int opt)
        {
            var it = InventoryWindow.Items[invSlot]; var p = Req(PacketIds.P_CL2FE_REQ_PC_TRADE_ITEM_REGISTER, 16);
            PutS(p, 16, it.Type); PutS(p, 18, it.Id); PutI(p, 20, opt); PutI(p, 24, invSlot); PutI(p, 28, hole); Send(p); Wait(5f);
        }
        void TryAdd(int invSlot)
        {
            if (waiting || invSlot < 0 || invSlot >= 50) return; var it = InventoryWindow.Items[invSlot]; if (it.Id <= 0) return;
            int hole = FreeOwn(); if (hole < 0) return;
            if (it.Type == 8) { Msg(176); return; }
            var m = ItemTbl.Get(tables, it.Type, it.Id); if ((m.Ok && m.TradeAble == 0) || (it.Type <= 3 && ((it.Opt >> 16) & 0xFFFF) > 0)) { Msg(176); return; }
            if (it.Type == 7 && it.Opt > 1) { int s = invSlot, h = hole; pad.Show("ADD", "Amount to Add", "ADD", it.Opt, 1, c => { if (c > 0) Register(s, FreeOwn() >= 0 ? FreeOwn() : h, c); }); }
            else Register(invSlot, hole, it.Type == 7 ? 1 : it.Opt);
        }
        void Remove(int hole)
        {
            if (waiting || hole < 0 || hole >= 5 || own[hole].Id <= 0) return; if (InventoryWindow.FirstFreeSlot() < 0 && own[hole].Type != 7) { Msg(175); return; }
            var it = own[hole]; var p = Req(PacketIds.P_CL2FE_REQ_PC_TRADE_ITEM_UNREGISTER, 16);
            PutS(p, 16, 8); PutS(p, 18, it.Id); PutI(p, 20, it.Opt); PutI(p, 24, hole); PutI(p, 28, hole); Send(p); Wait(5f);
        }
        void SetTaros(int v)
        {
            if (waiting || v < 0 || v > Taros) return; var p = Req(PacketIds.P_CL2FE_REQ_PC_TRADE_CASH_REGISTER, 4); PutI(p, 16, v); Send(p); Wait(5f);
        }
        void Confirm() { if (waiting) return; Send(Req(PacketIds.P_CL2FE_REQ_PC_TRADE_CONFIRM)); Wait(5f); }
        /// <summary>cnTrade.MenuChat: frase de menu del intercambio (iEmoteCode = 0, iFreeChatUse = 0).</summary>
        void SendMenu(string text, int emote)
        {
            if (string.IsNullOrEmpty(text)) return;
            var p = Req(PacketIds.P_CL2FE_REQ_PC_TRADE_EMOTES_CHAT, 264); ClientPackets.PutStr(p, 16, text, 128); PutI(p, 272, 0); p[276] = 0; Send(p);
        }
        void SendChat()
        {
            string s = (chatText ?? "").Trim(); chatText = ""; if (s.Length == 0 || s.StartsWith("/")) return;
            var p = Req(PacketIds.P_CL2FE_REQ_PC_TRADE_EMOTES_CHAT, 264); ClientPackets.PutStr(p, 16, s, 128); PutI(p, 272, 1); p[276] = 1; Send(p);
        }

        // ---- IInvMode ----
        public bool OnClick(int slot) { return false; }
        public void OnQuick(int slot) { if (!Blocked(false)) TryAdd(slot); }
        static readonly Vector2 Origin = new Vector2(8f, 7f);
        Rect OwnSlotWin(int i) { return new Rect(Origin.x + 25 + 51 + i * 67, Origin.y + 35 + 36, 62, 62); }
        Rect OtherSlotWin(int i) { return new Rect(Origin.x + 118 + i * 67, Origin.y + 200 + 37, 62, 62); }
        public bool OnDrop(int slot, Vector2 wm) { var box = new Rect(Origin.x + 25, Origin.y + 35, 480, 157); if (box.Contains(wm)) { TryAdd(slot); return true; } return false; }
        public void InfoButtons(int slot, Rect ext)
        {
            if (Skin.Button(new Rect(ext.x + 10f, ext.yMax + 6f, 150f, 28f), Loc.T("ADD"), Skin.Get(Skin.Menu, "RedButton") ?? St("RedButton"), !waiting)) { InventoryWindow.Instance.CloseInfoPub(); TryAdd(slot); }
        }

        protected override void OnOpened() { fade = 0f; chatScroll = 1e6f; }
        GameClient gcl;
        bool InCombat() { if (gcl == null) gcl = FindObjectOfType<GameClient>(); return gcl != null && Time.time < gcl.CombatUntil; }
        protected override void Tick()
        {
            fade += Time.unscaledDeltaTime;
            if (active && InCombat())
            {   // cnTrade.Update: eC_Combat -> CloseDlg + caja fija en ingles
                Send(Req(PacketIds.P_CL2FE_REQ_PC_TRADE_CONFIRM_CANCEL)); TradeOut(); if (SocialMenu.Instance != null) SocialMenu.Instance.Notice("Your trade has been cancelled because combat has started."); Sfx.Ui("Action_Failure01");
            }
            if (!active) { if (GameMenu.Current == GameMenu.Win.Trade && msg == null) GameMenu.CloseWindow(); }
        }

        // ============================================================ dibujo ============================================================
        protected override void DrawContent(Event e, bool en)
        {
            bool rep = e.type == EventType.Repaint; Vector2 wm = e.mousePosition; var lab = St("label"); var left = St("leftLabel") ?? lab; var right = St("rightLabel") ?? lab;
            var slotSt = St("slotbox"); var emptySt = St("slotboxempty"); var redSt = St("redItemBox"); var btn = St("button"); int lvl = Level; var yellow = new Color(1f, 0.92f, 0.016f); var green = new Color(0.6f, 1f, 0f);
            string oname = FullName(OtherId);
            // entrada en las ranuras propias (coordenadas de ventana)
            if (en && e.type == EventType.MouseUp)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (own[i].Id > 0 && OwnSlotWin(i).Contains(wm))
                    {
                        if (e.button == 0) { int h = i; var oi = own[i]; ShowInfo(oi.Type, oi.Id, oi.Opt, rr => { if (Skin.Button(new Rect(rr.x + 10f, rr.yMax + 6f, 190f, 28f), Loc.T("REMOVE FROM TRADE"), Skin.Get(Skin.Menu, "RedButton") ?? St("RedButton"), !waiting)) { CloseInfo(); Remove(h); } }); }
                        else if (e.button == 1) Remove(i);
                        e.Use(); break;
                    }
                    if (e.button == 0 && other[i].Id > 0 && OtherSlotWin(i).Contains(wm)) { var ti = other[i]; ShowInfo(ti.Type, ti.Id, ti.Opt, null); e.Use(); break; }
                }
            }

            GUI.BeginGroup(new Rect(8, 7, 550, 700));
            var area = St("p2parea"); if (rep && area != null && area.N != null) Skin.Bg(new Rect(12, 29, 480, 448), area.N, area);
            // ---- caja propia ----
            var mine = St(ownReady ? "p2pmytradeready" : "p2pmytrade"); var ob = new Rect(25, 35, 480, 157);
            GUI.BeginGroup(ob);
            if (rep && mine != null && mine.N != null) Skin.Bg(new Rect(0, 0, ob.width, ob.height), mine.N, mine);
            Skin.Label(new Rect(50, 20, 320, 20), Loc.T("MY OFFER"), right, Color.white); DrawFace(new Rect(400, 20, 70, 70), true, rep);
            for (int i = 0; i < 5 && rep; i++)
            {
                var r = new Rect(51 + i * 67, 36, 62, 62); var it = own[i]; bool has = it.Id > 0; var m = has ? ItemTbl.Get(tables, it.Type, it.Id) : default(ItemMeta);
                var st = has ? (ItemTbl.IsEquip(it.Type) && m.Level > lvl && redSt != null ? redSt : slotSt) : emptySt; if (st != null) Skin.Bg(r, st.N != null ? st.N : (slotSt != null ? slotSt.N : null), st);
                if (has && journal != null) { var ic = journal.ItemTexture(it.Type, it.Id, it.Opt); if (ic != null) GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, 56, 56), ic, ScaleMode.ScaleToFit); if (it.Type == 7) Skin.Label(new Rect(r.x + 5, r.y + 5, 62, 20), it.Opt.ToString(), lab, Color.white); }
            }
            GUI.BeginGroup(new Rect(49, 103, 191, 32)); Skin.Label(new Rect(1, 1, 150, 28), ownMoney.ToString(), right, Color.white); Pic(new Rect(152, 0, 32, 32), "tarosicon"); GUI.EndGroup();
            if (Skin.Button(new Rect(247, 107, 132, 25), Loc.T("ADD TAROS"), btn, en && !waiting)) pad.Show("ADD TAROS", "Amount to Add", "ADD TO TRADE", Taros, ownMoney, v => SetTaros(v));
            GUI.EndGroup();
            // ---- caja ajena ----
            var theirs = St(userReady ? "p2ptradeready" : "p2ptrade"); var tb = new Rect(0, 200, 480, 172);
            GUI.BeginGroup(tb);
            if (rep && theirs != null && theirs.N != null) Skin.Bg(new Rect(0, 0, tb.width, tb.height), theirs.N, theirs);
            Skin.Label(new Rect(110, 15, 330, 20), Loc.T("%s's OFFER").Replace("%s", oname.ToUpperInvariant()), right, Color.white); DrawFace(new Rect(35, 12, 70, 70), false, rep);
            for (int i = 0; i < 5 && rep; i++)
            {
                var it = other[i]; if (it.Id <= 0) continue; var r = new Rect(118 + i * 67, 37, 62, 62); var m = ItemTbl.Get(tables, it.Type, it.Id);
                var st = ItemTbl.IsEquip(it.Type) && m.Level > lvl && redSt != null ? redSt : slotSt; if (st != null) Skin.Bg(r, st.N, st);
                if (journal != null) { var ic = journal.ItemTexture(it.Type, it.Id, it.Opt); if (ic != null) GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, 56, 56), ic, ScaleMode.ScaleToFit); if (it.Type == 7) Skin.Label(new Rect(r.x + 5, r.y + 5, 62, 20), it.Opt.ToString(), lab, Color.white); }
            }
            GUI.BeginGroup(new Rect(115, 105, 327, 32)); Skin.Label(new Rect(1, 1, 290, 28), otherMoney.ToString(), right, Color.white); Pic(new Rect(292, 0, 32, 32), "tarosicon"); GUI.EndGroup();
            GUI.EndGroup();
            // ---- estado + boton ----
            if (ownReady && !userReady)
            {
                Skin.Label(new Rect(32, 380, 440, 20), Loc.T("Waiting For %s").Replace("%s", oname), left, green); Skin.Label(new Rect(32, 395, 440, 20), Loc.T("to accept your offer."), left, green);
            }
            else if (userReady) { Skin.Label(new Rect(32, 380, 440, 20), oname, left, yellow); Skin.Label(new Rect(32, 395, 440, 20), Loc.T("WOULD LIKE YOU TO ACCEPT THIS TRADE."), left, green); }
            bool canSubmit = en && !waiting && !(ownReady && !userReady);
            if (Skin.Button(new Rect(340, 432, 133, 30), Loc.T(userReady ? "ACCEPT" : "SUBMIT"), btn, canSubmit)) { if (!userReady) ownReady = true; Confirm(); Sfx.Ui("Yes_Button"); }
            // ---- chat ----
            var cb = St("p2pchatbox"); if (rep && cb != null && cb.N != null) Skin.Bg(new Rect(10, 490, 483, 141), cb.N, cb);
            GUI.BeginGroup(new Rect(10, 490, 483, 141));
            DrawChat(e, en, left);
            GUI.EndGroup();
            if (MenuChatPanel.Instance != null) MenuChatPanel.Instance.DrawInline(e, new Vector2(20f, 500f));   // listas de frases sobre el boton (coordenadas del grupo izquierdo)
            GUI.EndGroup();
        }

        void DrawChat(Event e, bool en, SkinStyle left)
        {
            bool rep = e.type == EventType.Repaint; var list = new Rect(10, 47, 462, 53); float contentH = chat.Count * 15f, max = Mathf.Max(0f, contentH - list.height); chatScroll = Mathf.Clamp(chatScroll, 0f, max);
            if (rep) { var oc = GUI.color; GUI.color = new Color(0f, 0.05f, 0.12f, 0.55f); GUI.DrawTexture(list, Texture2D.whiteTexture); GUI.color = oc; }
            if (e.type == EventType.ScrollWheel && list.Contains(e.mousePosition)) { chatScroll = Mathf.Clamp(chatScroll + e.delta.y * 15f, 0f, max); e.Use(); }
            GUI.BeginGroup(list);
            for (int i = 0; i < chat.Count; i++) { float y = i * 15f - chatScroll; if (y + 15f < 0f || y > list.height) continue; Skin.Label(new Rect(2, y, list.width - 4, 15), chat[i], left, new Color(0.87f, 1f, 1f)); }
            GUI.EndGroup();
            if (field == null)
            {
                field = new GUIStyle(GUI.skin.textField) { fontSize = 13, alignment = TextAnchor.MiddleLeft }; field.normal.background = null; field.focused.background = null; field.hover.background = null; field.active.background = null;
                var c = new Color(0.87f, 1f, 1f); field.normal.textColor = c; field.focused.textColor = c; field.hover.textColor = c; field.active.textColor = c; field.padding = new RectOffset(4, 4, 2, 2);
            }
            {   // MENU CHAT (MenuChatScript en modo SetTransEmote): frases rapidas enviadas por TRADE_EMOTES_CHAT
                var mp = MenuChatPanel.Instance; if (mp != null)
                {
                    bool op = mp.InlineOpen; if (Skin.Button(new Rect(10, 10, 136, 26), Loc.T("MENU CHAT"), Skin.Get(Skin.Chat, op ? "ButtonLock" : "Button"), en && !op, true)) { mp.OpenInline(SendMenu); Sfx.Button(); }
                }
            }
            var fr = new Rect(18, 104, 400, 25); var bar = St("pc2pcchatbar"); if (rep && bar != null && bar.N != null) Skin.Bg(fr, bar.N, bar);
            GUI.enabled = en; GUI.SetNextControlName("tradechat"); string s = GUI.TextField(fr, chatText ?? "", 64, field); if (s != chatText) chatText = s.Replace("\n", ""); GUI.enabled = true;
            bool focus = GUI.GetNameOfFocusedControl() == "tradechat"; InventoryWindow.Typing = focus;
            bool enter = focus && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            if (Skin.Button(new Rect(415, 105, 57, 24), Loc.T("SEND"), St("pc2pcchatsendbut"), en) || enter) { SendChat(); if (enter) e.Use(); }
        }
    }

    /// <summary>Caja de invitacion (cnGUINanocom msg 26, 20 s) y menu de jugador (INVITE TO TRADE / CLOSE). Se dibuja siempre, sin ventana abierta.</summary>
    public sealed class TradeOverlay : MonoBehaviour
    {
        public static bool MenuActive; static int menuId = -1; static string menuName = "";
        public static bool Active { get { return MenuActive || SocialMenu.Active || (TradeWindow.Instance != null && TradeWindow.Instance.InviteShown); } }
        public static void OpenMenu(int id, string name) { menuId = id; menuName = name; MenuActive = true; Sfx.Button(); }
        /// <summary>Raycast del clic/toque contra los demas jugadores (NpcIconMode con pTarget = jugador).</summary>
        public static bool TryPick(WorldEntities ents, Ray ray, Vector3 selfPos, float range)
        {
            if (ents == null || TradeWindow.Instance == null || TradeWindow.Active) return false; int best = -1; float bd = 1e9f;
            foreach (var kv in ents.OtherPlayers)
            {
                var go = kv.Value; if (go == null) continue; if ((go.transform.position - selfPos).sqrMagnitude > range * range) continue; bool has = false; Bounds b = new Bounds();
                foreach (var rd in go.GetComponentsInChildren<Renderer>(false)) { if (!rd.enabled || rd is ParticleSystemRenderer) continue; if (!has) { b = rd.bounds; has = true; } else b.Encapsulate(rd.bounds); }
                if (!has) { b = new Bounds(go.transform.position + Vector3.up, new Vector3(1f, 2f, 1f)); } b.Expand(0.3f);
                float d; if (b.IntersectRay(ray, out d) && d < bd) { bd = d; best = kv.Key; }
            }
            if (best < 0) return false; OpenMenu(best, TradeWindow.Instance.FullName(best)); return true;
        }

        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        void Update()
        {
            var tw = TradeWindow.Instance; if (tw == null) return;
            if (MenuActive && (TradeWindow.Active || GameMenu.Current != GameMenu.Win.None || Input.GetKeyDown(KeyCode.Escape))) MenuActive = false;
            if (tw.InviteShown)
            {
                tw.InviteLeft -= Time.unscaledDeltaTime; if (tw.InviteLeft <= 0f) tw.DeclineInvite();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) tw.AcceptInvite();
            }
        }
        void OnGUI()
        {
            GUI.depth = -1400;   // se deja puesto (sin restaurar): el depth de un script es el que tiene al terminar su OnGUI
            var tw = TradeWindow.Instance; if (tw == null || Screen.height <= 0) return; var e = Event.current; if (e == null) return;
            if (!MenuActive && !tw.InviteShown) return;
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f); var om = GUI.matrix; var oc = GUI.color;
            try
            {
                var red = Skin.Get(Skin.Menu, "RedButton") ?? Skin.Get(Skin.Inven, "RedButton"); var cancel = Skin.Get(Skin.Menu, "CancelButton") ?? Skin.Get(Skin.Inven, "CancelButton"); var lab = Skin.Get(Skin.Inven, "label");
                if (MenuActive)
                {   // DoPcWindow: NpcUI.PcMenu (mismos estilos que el menu de NPC: multifunc_win, botones azules con icono, close/closeover)
                    var ui = NpcUI.Instance; var sm = SocialMenu.Instance; int id = menuId; bool ign = sm != null && sm.IsIgnored(id);
                    if (ui == null) MenuActive = false;
                    else
                    {
                        GUI.matrix = om; GUI.color = Color.white;
                        string[] txt = { "INVITE TO TRADE", "INVITE TO GROUP", "MAKE A BUDDY", ign ? "UNIGNORE PLAYER" : "IGNORE PLAYER", "REPORT PLAYER", "CLOSE" };
                        string[] ico = { "usericon_trade", "usericon_group", "usericon_buddy", "usericon_antibuddy", "usericon_antibuddy", "npcicon_exit" };   // pButtonTex 12,13,14,16,16,17
                        int k = ui.PcMenu(menuName, txt, ico, Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f));
                        if (k == -1 || k == 5) { MenuActive = false; Sfx.Button(); }
                        else if (k >= 0)
                        {
                            MenuActive = false; Sfx.Button();
                            if (k == 0) tw.Invite(id); else if (sm != null) { if (k == 1) sm.InviteGroup(id); else if (k == 2) sm.MakeBuddy(id); else if (k == 3) sm.ToggleIgnore(id); else sm.Report(id); }
                        }
                    }
                }
                if (tw.InviteShown)
                {   // NanoPopupRect (520x164) centrada
                    GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc); GUI.color = Color.white;
                    var r = new Rect(-260f, -82f, 520f, 164f);
                    if (e.type == EventType.Repaint) { GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = new Color(0f, 0.8f, 0.9f, 1f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture); GUI.color = Color.white; }
                    GUI.BeginGroup(r);
                    Skin.Label(new Rect(100, 0, 400, 28), Loc.T("TRADE INVITE RECEIVED"), lab, new Color(1f, 0.92f, 0.016f));
                    if (e.type == EventType.Repaint) { var ic = UiKit.Tex("messicon_trade"); if (ic != null) GUI.DrawTexture(new Rect(25, 15, 62, 62), ic, ScaleMode.ScaleToFit); }
                    string t = tw.InviteText() + "\n" + Loc.T("Trade will expire") + " :" + Mathf.CeilToInt(tw.InviteLeft) + " " + Loc.T("seconds");
                    Skin.Label(new Rect(90, 30, 400, 90), t, lab, Color.white);
                    if (Skin.Button(new Rect(44, 124, 150, 25), Loc.T("Decline"), cancel)) tw.DeclineInvite();
                    else if (Skin.Button(new Rect(334, 124, 150, 25), Loc.T("Accept"), red)) tw.AcceptInvite();
                    GUI.EndGroup();
                    if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && r.Contains(e.mousePosition)) e.Use();
                }
            }
            finally { GUI.matrix = om; GUI.color = oc; }
        }
    }
}
