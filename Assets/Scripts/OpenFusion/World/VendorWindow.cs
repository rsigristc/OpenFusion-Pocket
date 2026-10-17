// VendorWindow: port de cnVendor + Panel_Vendor (Win.Vendor). SIN COMPILAR NI PROBAR EN UNITY.
// Flujo: NpcUI "ENTER STORE" -> Open(npcInstanceId, npcNumber, nombre, servicio) -> REQ_PC_VENDOR_START; START_SUCC -> REQ_PC_VENDOR_TABLE_UPDATE; TABLE_UPDATE_SUCC (sItemVendor 24 B x N) abre la ventana.
// Compra: VENDOR_ITEM_BUY (equipo / cofre / general) o VENDOR_BATTERY_BUY (general m_iItemType == 2: Boosts / Potions); venta: VENDOR_ITEM_SELL; recompra: lista solo cliente (15) + VENDOR_ITEM_RESTORE_BUY.
// Precios de las tablas de objetos (m_iItemPrice / m_iItemSellPrice); el fBuyCost del servidor no se usa (igual que el original).
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class VendorWindow : ModalWindow, IInvMode
    {
        public static VendorWindow Instance;
        /// <summary>My Stuff se abrio desde esta tienda (GO TO MY STUFF): My Stuff muestra BACK TO STORE.</summary>
        public static bool ReturnToStore;
        public static VendorWindow Ensure(GameObject host) { var w = host.GetComponent<VendorWindow>(); if (w == null) w = host.AddComponent<VendorWindow>(); return w; }
        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h, JournalWindow j) { InitBase(d, e, t, h, j); }
        protected override bool SlideIn { get { return true; } }
        protected override GameMenu.Win WinId { get { return GameMenu.Win.Vendor; } }
        protected override string FrameName { get { return "vendorback"; } }
        protected override IInvMode InvMode { get { return this; } }
        protected override void Awake() { Instance = this; base.Awake(); }

        static readonly Rect Panel = new Rect(8, 7, 498, 638), InfoR = new Rect(3, 9, 481, 87), Dlg = new Rect(11, 132, 486, 528), TableR = new Rect(2, 32, 479, 410), ScrollR = new Rect(10, 10, 464, 400), GoBut = new Rect(313, 595, 161, 25);
        const int RowH = 80, RecentMax = 5;
        static Vector2 ViewOrigin { get { return new Vector2(Panel.x + Dlg.x + TableR.x + ScrollR.x, Panel.y + Dlg.y + TableR.y + ScrollR.y); } }

        sealed class VItem { public ItemBase It; public int Sort; }
        readonly List<VItem> list = new List<VItem>();
        static readonly List<ItemBase> recent = new List<ItemBase>();
        int npcNumber, vendorKey; string npcName = "", service = ""; bool pending; float pendT;
        int tabMode;          // 0 BUY, 3 BUY BACK
        float scroll;

        // ============================================================ apertura / red ============================================================
        public void Open(int npcInstanceId, int npcNum, string name, int serviceNumber)
        {
            if (pending) return; npcNumber = npcNum; npcName = name ?? ""; vendorKey = npcInstanceId;   // legacy (NpcIconMode.StartVendor): START {iNPC_ID = id de instancia (status.iID), iVendorID = m_iNpcNumber}
            var nt = tables != null ? tables.m_pNpcTable : null; var sv = nt != null ? nt.m_pNpcServiceData : null; service = sv != null && serviceNumber >= 0 && serviceNumber < sv.Length && sv[serviceNumber] != null ? sv[serviceNumber].m_strService : "";
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_VENDOR_START, 8); PutI(p, 4, npcInstanceId); PutI(p, 8, npcNumber); Send(p); pending = true; Debug.Log("[tienda] START inst=" + npcInstanceId + " num=" + npcNumber); pendT = Time.unscaledTime + 6f;
        }
        public bool Has(int npcNumber) { return HasStore(tables, npcNumber); }
        /// <summary>CheckVendor: el NPC aparece en la tabla de tiendas.</summary>
        public static bool HasStore(XdtTableScript t, int npcNumber)
        {
            var v = t != null ? t.m_pVendorTable : null; if (v == null || v.m_pItemData == null) return false;
            for (int i = 0; i < v.m_pItemData.Length; i++) if (v.m_pItemData[i] != null && v.m_pItemData[i].m_iNpcNumber == npcNumber) return true; return false;
        }

        protected override void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_START_SUCC && b.Length >= 8)
            {
                int ni = I(b, 0), vi = b.Length >= 8 ? I(b, 4) : npcNumber; vendorKey = ni; Debug.Log("[tienda] START_SUCC iNPC_ID=" + ni + " iVendorID=" + vi);
                var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_VENDOR_TABLE_UPDATE, 8); PutI(p, 4, ni); PutI(p, 8, vi); Send(p);
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_START_FAIL) q.Enqueue(() => { pending = false; ChatHud.AddEventString(4, Message(21)); });
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_TABLE_UPDATE_SUCC)
            {
                var body = b; q.Enqueue(() =>
                {
                    list.Clear(); int n = Mathf.Min(50, body.Length / 24);
                    for (int i = 0; i < n; i++) { var it = ReadItem(body, i * 24 + 8); if (it.Id <= 0) continue; list.Add(new VItem { It = it, Sort = I(body, i * 24 + 20) }); }
                    pending = false; tabMode = 0; scroll = 0f; GameMenu.OpenWindow(GameMenu.Win.Vendor);
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_TABLE_UPDATE_FAIL) q.Enqueue(() => { pending = false; ChatHud.AddEventString(4, Message(22)); });
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_ITEM_BUY_SUCC && b.Length >= 20)
            {
                var body = b; q.Enqueue(() => { hud.Data.Taros = I(body, 0); int s = I(body, 4); if (s >= 0 && s < 50) InventoryWindow.Items[s] = ReadItem(body, 8); Sfx.Ui("Money_Transfer0" + UnityEngine.Random.Range(1, 3)); Release(); });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_ITEM_BUY_FAIL) { var body = b; q.Enqueue(() => { Release(); int c = body.Length >= 4 ? I(body, 0) : 0; Debug.LogWarning("[tienda] BUY_FAIL codigo=" + c + " (vendorKey=" + vendorKey + "); el motivo exacto lo imprime la consola del servidor ([WARN] ...)"); if (c == 1) Msg(264); else Msg(23); }); }
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_BATTERY_BUY_SUCC && b.Length >= 12)
            {
                var body = b; q.Enqueue(() => { hud.Data.Taros = I(body, 0); hud.Data.BatteryWpn = I(body, 4); hud.Data.BatteryNano = I(body, 8); Sfx.Ui("Money_Transfer0" + UnityEngine.Random.Range(1, 3)); Release(); });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_BATTERY_BUY_FAIL) q.Enqueue(() => { Release(); Msg(24); });
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_ITEM_SELL_SUCC && b.Length >= 32)
            {
                var body = b; q.Enqueue(() =>
                {
                    hud.Data.Taros = I(body, 0); int s = I(body, 4); var sold = ReadItem(body, 8); var stay = ReadItem(body, 20);
                    if (s >= 0 && s < 50) InventoryWindow.Items[s] = sold.Type == 7 ? stay : default(ItemBase);
                    recent.Add(new ItemBase { Type = sold.Type, Id = sold.Id, Opt = sold.Opt, TimeLimit = sold.TimeLimit }); while (recent.Count > RecentMax) recent.RemoveAt(0);
                    Sfx.Ui("Money_Transfer0" + UnityEngine.Random.Range(1, 3)); Release();
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_ITEM_SELL_FAIL) q.Enqueue(() => { Release(); Msg(25); });
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_ITEM_RESTORE_BUY_SUCC && b.Length >= 20)
            {
                var body = b; q.Enqueue(() =>
                {
                    hud.Data.Taros = I(body, 0); int s = I(body, 4); var it = ReadItem(body, 8); it.TimeLimit = 0; if (s >= 0 && s < 50) InventoryWindow.Items[s] = it;
                    for (int i = 0; i < recent.Count; i++) if (recent[i].Id == it.Id && recent[i].Type == it.Type && recent[i].Opt == it.Opt) { recent.RemoveAt(i); break; }
                    Sfx.Ui("Money_Transfer0" + UnityEngine.Random.Range(1, 3)); Release();
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_VENDOR_ITEM_RESTORE_BUY_FAIL) q.Enqueue(() => { Release(); Msg(23); });
            else if (type == PacketIds.P_FE2CL_REP_PC_ITEM_DELETE_SUCC) q.Enqueue(Release);
        }
        void LateUpdate() { if (pending && Time.unscaledTime > pendT) pending = false; }
        protected override void OnOpened() { scroll = 0f; ReturnToStore = false; }

        // ============================================================ compra / venta ============================================================
        static int InvCount(int type, int id) { int n = 0; return n; }
        int PriceOf(ItemBase it, bool buyback)
        {
            if (it.Type <= 3 && ((it.Opt >> 16) & 0xFFFF) > 0) return 0;   // objeto combinado: sin precio
            var m = ItemTbl.Get(tables, it.Type, it.Id); if (!buyback) return m.Price; return it.Type == 7 ? m.SellPrice * Mathf.Max(1, it.Opt) : m.SellPrice;
        }
        bool CanBuyAny(ItemBase it, bool buyback) { return PriceOf(it, buyback) <= Taros; }

        void Buy(ItemBase it, int row, int qty)
        {
            if (waiting || qty <= 0) return; var m = ItemTbl.Get(tables, it.Type, it.Id);
            if (it.Type == 7 && m.ItemType == 2)
            {   // baterias: Boosts (m_iItemNumber 3) / Potions (4)
                if (m.BatteryRecharge == 0 && InventoryWindow.FirstFreeSlot() < 0) return;
                var pb = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_VENDOR_BATTERY_BUY, 24); PutI(pb, 4, vendorKey); PutI(pb, 8, vendorKey); pb[12] = (byte)(sbyte)vendorKey;
                PutS(pb, 16, it.Type); PutS(pb, 18, it.Id); PutI(pb, 20, qty); PutI(pb, 24, it.TimeLimit); Send(pb); Wait(5f); return;
            }
            int slot = InventoryWindow.FirstFreeSlot(); if (slot < 0) { Msg(12); return; }
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_VENDOR_ITEM_BUY, 28); PutI(p, 4, vendorKey); PutI(p, 8, vendorKey); p[12] = (byte)(sbyte)vendorKey;
            PutS(p, 16, it.Type); PutS(p, 18, it.Id); PutI(p, 20, it.Type == 7 ? qty : (it.Type == 9 ? 1 : 1)); PutI(p, 24, it.TimeLimit); PutI(p, 28, slot); Debug.Log("[tienda] BUY vendor=" + vendorKey + " type=" + it.Type + " id=" + it.Id + " opt=" + BitConverter.ToInt32(p, 20) + " slot=" + slot + " precio=" + m.Price + " taros=" + Taros + " slotOcupado=" + (InventoryWindow.Items[slot].Id > 0) + " hex=" + BitConverter.ToString(p)); Send(p); Wait(5f); Sfx.Ui("Purchase0" + UnityEngine.Random.Range(1, 3));
        }
        void Restore(int row)
        {
            if (waiting || row < 0 || row >= recent.Count) return; var it = recent[row]; int slot = InventoryWindow.FirstFreeSlot(); if (slot < 0) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_VENDOR_ITEM_RESTORE_BUY, 28); PutI(p, 4, vendorKey); PutI(p, 8, vendorKey); p[12] = (byte)(sbyte)(row + 1);
            PutS(p, 16, it.Type); PutS(p, 18, it.Id); PutI(p, 20, it.Opt); PutI(p, 24, 0); PutI(p, 28, slot); Send(p); Wait(5f);
        }
        void Sell(int slot, int cnt)
        {
            if (waiting || slot < 0 || slot >= 50) return; var it = InventoryWindow.Items[slot]; if (it.Id <= 0 || cnt <= 0) return;
            if (it.Type == 9) { Msg(107); return; }
            if (it.Type == 8) { Msg(108); return; }
            var m = ItemTbl.Get(tables, it.Type, it.Id); if (m.SellAble != 1) { Msg(109); return; }
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_VENDOR_ITEM_SELL, 8); PutI(p, 4, slot); PutI(p, 8, it.Type == 7 ? Mathf.Min(cnt, Mathf.Max(1, it.Opt)) : 1); Send(p); Wait(5f); Sfx.Ui("Money_Transfer01");
        }
        int MaxBuy(ItemBase it, bool buyback)
        {
            var m = ItemTbl.Get(tables, it.Type, it.Id); int price = m.Price; int stack = Mathf.Max(1, m.Stack);
            if (it.Type == 7 && m.ItemType == 2) { int have = m.ItemNumber == 3 ? hud.Data.BatteryWpn : m.ItemNumber == 4 ? hud.Data.BatteryNano : 0; stack = Mathf.Max(0, stack - have); }
            int aff = price > 0 ? Taros / price : stack; return Mathf.Max(0, Mathf.Min(stack, aff));
        }
        void AskBuy(ItemBase it, int row)
        {
            if (it.Type == 7) { int mx = MaxBuy(it, false); var m = ItemTbl.Get(tables, it.Type, it.Id); pad.ShowGum(journal != null ? journal.ItemTexture(it.Type, it.Id) : null, journal != null ? journal.ItemName(it.Type, it.Id) : "", journal != null ? journal.ItemComment(it.Type, it.Id) : "", "BUY", mx, 0, qty => Buy(it, row, qty), m.Price); }
            else if (it.Type == 9)
            {   // PopupControll (eGui_Vendor): los cofres (E.G.G.E.R.) abren TuringPopup (usedlg) con BUY, sin cantidad
                var m = ItemTbl.Get(tables, it.Type, it.Id); Sfx.Ui("Container_Open01");
                pad.ShowUse(journal != null ? journal.ItemTexture(it.Type, it.Id) : null, journal != null ? journal.ItemName(it.Type, it.Id) : "", journal != null ? journal.ItemComment(it.Type, it.Id) : "", "BUY", m.Price, () => Buy(it, row, 1));
            }
            else Buy(it, row, 1);
        }

        // ---- IInvMode: el inventario en modo tienda vende ----
        public bool OnClick(int slot)
        {   // PopupControll (eGui_Vendor, objeto del inventario): general -> GumPopup con SELL y cantidad; cofre -> TuringPopup (OPEN); equipo -> ficha con SELL
            if (slot < 0 || slot >= 50) return false;
            var it = InventoryWindow.Items[slot]; if (it.Id <= 0) return false;
            if (it.Type != 7 && it.Type != 9) return false;
            if (Blocked(false)) return true;
            var m = ItemTbl.Get(tables, it.Type, it.Id); int s = slot; Sfx.Button();
            Texture ic = journal != null ? journal.ItemTexture(it.Type, it.Id) : null; string nm = journal != null ? journal.ItemName(it.Type, it.Id) : ""; string ds = journal != null ? journal.ItemComment(it.Type, it.Id) : "";
            if (it.Type == 7)
            {
                if (m.SellAble != 1) { Msg(109); return true; }
                pad.ShowGum(ic, nm, ds, "SELL", Mathf.Max(1, it.Opt), 0, qty => Sell(s, qty), 0, true);
                return true;
            }
            var iw = InventoryWindow.Instance;
            pad.ShowUseIn(ic, nm, ds, "OPEN", () => { Sfx.Ui("Container_Open01"); if (iw != null) iw.UseItemPub(s); }, () => { if (iw != null) iw.AskDelete(s); }, null);
            return true;
        }
        public void OnQuick(int slot) { if (!Blocked(false)) Sell(slot, 1); }
        public bool OnDrop(int slot, Vector2 wm) { return false; }
        public void InfoButtons(int slot, Rect ext)
        {
            var it = InventoryWindow.Items[slot]; if (it.Id <= 0) return; var red = Skin.Get(Skin.Menu, "RedButton") ?? St("RedButton"); var cancel = Skin.Get(Skin.Menu, "CancelButton") ?? St("CancelButton");
            if (it.Type != 10) { var sm = ItemTbl.Get(tables, it.Type, it.Id); DrawPrice(ext, sm.SellPrice); }
            if (it.Type != 10 && Skin.Button(new Rect(ext.x + 160f, ext.y + 14f + 380f, 130f, 28f), Loc.T("SELL"), red, !waiting))
            {
                InventoryWindow.Instance.CloseInfoPub();
                if (it.Type == 7 && it.Opt > 1) { var m = ItemTbl.Get(tables, it.Type, it.Id); int s = slot; pad.Show("SELL", "Amount to Sell", "SELL", it.Opt, 1, c => Sell(s, c), m.SellPrice); }
                else Sell(slot, 1);
            }
            else if (Skin.Button(new Rect(ext.x + 10f, ext.y + 14f + 375f, 100f, 28f), Loc.T("DELETE"), cancel, true)) { InventoryWindow.Instance.CloseInfoPub(); InventoryWindow.Instance.AskDelete(slot); }
        }

        // ============================================================ dibujo ============================================================
        protected override void DrawContent(Event e, bool en)
        {
            bool rep = e.type == EventType.Repaint; Vector2 wm = e.mousePosition; var lab = St("label"); var bf = St("blankbox"); var left = St("leftLabel") ?? lab;
            bool buyback = tabMode == 3; int count = buyback ? recent.Count : list.Count;
            float contentH = count * RowH, viewH = ScrollR.height, maxS = Mathf.Max(0f, contentH - viewH); scroll = Mathf.Clamp(scroll, 0f, maxS);
            var o = ViewOrigin; var viewW = new Rect(o.x, o.y, ScrollR.width - 30f, viewH);
            // entrada (coordenadas de ventana)
            if (en)
            {
                if (e.type == EventType.ScrollWheel && viewW.Contains(wm)) { scroll = Mathf.Clamp(scroll + Mathf.Clamp(e.delta.y, -3f, 3f) * 25f, 0f, maxS); e.Use(); }
                int row = -1; if (viewW.Contains(wm)) { float ly = wm.y - o.y + scroll; int r = (int)(ly / RowH); if (r >= 0 && r < count && ly - r * RowH < 75f && wm.x - o.x < 433f) row = r; }
                if (row >= 0 && e.type == EventType.MouseUp && e.button == 1) { RowRight(row, buyback); e.Use(); }
                else if (row >= 0 && e.type == EventType.MouseUp && e.button == 0) { RowLeft(row, buyback); e.Use(); }
            }
            GUI.BeginGroup(Panel);
            // panel de informacion (RenderNpc)
            GUI.BeginGroup(InfoR); Pic(new Rect(0, 0, InfoR.width, InfoR.height), "vendorinfo");
            Skin.Label(new Rect(14, 10, 300, 20), Loc.T("SHOPKEEPER") + " - " + npcName, lab, Color.white);
            Skin.Label(new Rect(14, 30, 350, 80), service, left != null ? Align(left, TextAnchor.UpperLeft) : null, new Color(1f, 0.92f, 0.016f));
            GUI.EndGroup();
            // lista
            GUI.BeginGroup(Dlg);
            var lb = St("vendorlistback"); if (rep && lb != null && lb.N != null) Skin.Bg(new Rect(0, 20, 486, 485), lb.N, lb);
            DrawTabs(e, en, bf);
            GUI.BeginGroup(TableR); GUI.BeginGroup(ScrollR);
            var slotSt = St("slotbox"); var redSt = St("redItemBox"); var yellow = new Color(1f, 0.92f, 0.016f);
            for (int n = 0; n < count; n++)
            {
                float y = n * RowH - scroll; if (y + RowH < 0f || y > viewH) continue; if (!rep) continue;
                var it = buyback ? recent[n] : list[n].It; var m = ItemTbl.Get(tables, it.Type, it.Id);
                Pic(new Rect(0, y, 433, 75), "itembar");
                bool redBox = InventoryWindow.Instance != null ? InventoryWindow.Instance.IsRed(it) : ItemTbl.IsEquip(it.Type) && m.Level > Level; var cell = new Rect(4, y + 5, 66, 66); var st = redBox && redSt != null ? redSt : slotSt;
                if (st != null) Skin.Bg(cell, st.N, st);
                var ic = journal != null ? journal.ItemTexture(it.Type, it.Id, it.Opt) : null; if (ic != null) GUI.DrawTexture(new Rect(cell.x + 3, cell.y + 3, cell.width - 6, cell.height - 6), ic, ScaleMode.ScaleToFit);
                int price = PriceOf(it, buyback);
                if (price > Taros && st != null) { var oc = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.4f); Skin.Bg(cell, st.N, st); GUI.color = oc; }
                if (buyback && it.Type == 7) Skin.Label(new Rect(cell.x + 5, cell.y + 5, 62, 62), it.Opt.ToString(), lab, Color.white);
                Skin.Label(new Rect(82, y + 8, 300, 20), journal != null ? journal.ItemName(it.Type, it.Id, it.Opt) : "", lab, Color.white);
                if (ItemTbl.IsEquip(it.Type)) Skin.Label(new Rect(82, y + 22, 300, 20), Loc.T("LEVEL") + " " + m.Level, lab, yellow);
                if (it.Type == 10) Skin.Label(new Rect(82, y + 37, 300, 20), Loc.T("Speed: %s Class").Replace("%s", m.RunSpeed.ToString()), lab, yellow);
                Skin.Label(new Rect(262, y + 45, 134, 20), price + " " + Loc.T("TAROS"), Align(bf ?? left, TextAnchor.MiddleRight), price > Taros ? Color.red : Color.white);
                Pic(new Rect(402, y + 40, 30, 30), "CombiTarosIcon");
            }
            GUI.EndGroup();
            var sh = St("shadow"); if (rep && sh != null && sh.N != null) Skin.Bg(new Rect(2, 3, 450, 412), sh.N, sh);
            if (maxS > 0f) scroll = VScrollBar(e, new Rect(ScrollR.x + ScrollR.width - 16f, ScrollR.y, 16f, ScrollR.height), scroll, contentH, viewH, en);
            GUI.EndGroup();
            GUI.EndGroup();
            if (Skin.Button(GoBut, Loc.T("GO TO MY STUFF"), St("button"), en)) { GUI.EndGroup(); ReturnToStore = true; GameMenu.CloseWindow(); GameMenu.OpenWindow(GameMenu.Win.MyStuff); return; }
            GUI.EndGroup();
            if (InventoryWindow.Instance != null) InventoryWindow.Instance.DrawEquipReadOnly(e);
        }

        internal static SkinStyle Align(SkinStyle s, TextAnchor a)
        {
            if (s == null) return null; return new SkinStyle { Name = s.Name, N = s.N, H = s.H, A = s.A, ON = s.ON, bL = s.bL, bR = s.bR, bT = s.bT, bB = s.bB, pL = s.pL, pR = s.pR, pT = s.pT, pB = s.pB, Font = s.Font, Align = a, cN = s.cN, cH = s.cH, cA = s.cA, Wrap = s.Wrap, FixedW = s.FixedW, FixedH = s.FixedH, Offset = s.Offset };
        }

        void DrawTabs(Event e, bool en, SkinStyle bf)
        {
            var buyBut = new Rect(10, 5, 80, 15); var bbBut = new Rect(90, 5, 90, 15); bool rep = e.type == EventType.Repaint;
            Vector2 m = e.mousePosition; // local al grupo Dlg
            if (rep)
            {
                if (tabMode == 0) { Pic(new Rect(54, 0, 169, 27), bbBut.Contains(m) ? "vendorTab2Over" : "vendorTab2Nor"); Pic(new Rect(0, 20, 486, 13), "vendorlistbackbar"); Pic(new Rect(1, 0, 95, 27), "vendorTab1"); }
                else { Pic(new Rect(1, 0, 95, 27), buyBut.Contains(m) ? "vendorTab1Over" : "vendorTab1Nor"); Pic(new Rect(0, 20, 486, 13), "vendorlistbackbar"); Pic(new Rect(54, 0, 169, 27), "vendorTab2"); }
            }
            if (bf != null)
            {
                if (tabMode == 0) { Skin.Label(buyBut, Loc.T("BUY"), bf, Color.white); if (Skin.Button(bbBut, Loc.T("BUY BACK"), bf, en, true)) { tabMode = 3; scroll = 0f; Sfx.Ui("Tab_Click01"); } }
                else { if (Skin.Button(buyBut, Loc.T("BUY"), bf, en, true)) { tabMode = 0; scroll = 0f; Sfx.Ui("Tab_Click01"); } Skin.Label(bbBut, Loc.T("BUY BACK"), bf, Color.white); }
            }
        }

        void RowLeft(int row, bool buyback)
        {
            var it = buyback ? recent[row] : list[row].It; Sfx.Button(); if (!buyback && (it.Type == 7 || it.Type == 9)) { AskBuy(it, row); return; }   // GumPopup (generales, con cantidad) / TuringPopup (cofres): popup directo
            var red = Skin.Get(Skin.Menu, "RedButton") ?? St("RedButton"); int r = row;
            if (!buyback) ShowInfo(it.Type, it.Id, it.Opt, rr =>
            {
                var iw = InventoryWindow.Instance; bool tryOk = iw != null && iw.CanTryOn(it.Type, it.Id);
                DrawPrice(rr, PriceOf(it, false));
                if (Skin.Button(new Rect(rr.x + 210f, rr.y + 14f + 390f, 85f, 28f), Loc.T("BUY"), red, !waiting)) { CloseInfo(); AskBuy(it, r); return; }   // rectBuyBut (210,390) dentro del grupo desplazado 14
                if (tryOk) { if (Skin.Button(new Rect(rr.x + 94f, rr.y + 14f + 390f, 100f, 28f), Loc.T("TRY ON"), red)) { Sfx.Button(); iw.StartTryOn(it.Type, it.Id); } iw.DrawTryOn(rr, Event.current); }
            }, "For Sale", it.TimeLimit);
            else ShowInfo(it.Type, it.Id, it.Opt, rr => { DrawPrice(rr, PriceOf(it, true)); if (Skin.Button(new Rect(rr.x + 160f, rr.y + 14f + 380f, 130f, 28f), Loc.T("BUY BACK"), red, !waiting)) { CloseInfo(); Restore(r); } }, "Item", it.TimeLimit);
        }
        void RowRight(int row, bool buyback)
        {
            if (waiting) return; if (buyback) { Restore(row); return; }
            var it = list[row].It; Sfx.Button(); if (it.Type == 7) AskBuy(it, row); else Buy(it, row, 1);
        }
    }
}
