// BankWindow: port de Panel_BankScript + cnBank (Win.Bank). SIN COMPILAR NI PROBAR EN UNITY.
// Flujo: NpcUI (NPC tipo 12 / 50) -> BankWindow.Instance.Open(npcId) -> REQ_PC_BANK_OPEN {iPC_ID, iNPC_ID}; con BANK_OPEN_SUCC (aBank[N] sItemBase + iExtraBank) se abre la ventana.
// Mover objetos = REQ_ITEM_MOVE (eIL Inven 1 <-> Bank 3, o Bank <-> Bank); la respuesta es PC_ITEM_MOVE_SUCC con ambos lados. No hay dinero de banco en este cliente.
// Interaccion (InventoryManagerScript/EquipPopup): clic = ficha con "MOVE TO VAULT" / "MOVE TO MY EQUIPMENT"; clic derecho o doble clic = mover directo a la primera ranura libre; arrastrar = ranura concreta.
using System;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class BankWindow : ModalWindow, IInvMode
    {
        public static BankWindow Instance;
        public static BankWindow Ensure(GameObject host) { var w = host.GetComponent<BankWindow>(); if (w == null) w = host.AddComponent<BankWindow>(); return w; }
        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h, JournalWindow j) { InitBase(d, e, t, h, j); }
        protected override bool SlideIn { get { return true; } }
        protected override GameMenu.Win WinId { get { return GameMenu.Win.Bank; } }
        protected override string FrameName { get { return "tradeback"; } }
        protected override IInvMode InvMode { get { return this; } }
        protected override void Awake() { Instance = this; base.Awake(); }

        public static ItemBase[] Bank = new ItemBase[200];
        static readonly Rect Panel = new Rect(8, 7, 495, 638), Head = new Rect(3, 9, 481, 87), Dlg = new Rect(11, 105, 482, 531), View = new Rect(29, 34, 450, 445);
        // origen de la vista de rejilla en coordenadas de ventana
        static Vector2 ViewOrigin { get { return new Vector2(Panel.x + Dlg.x + View.x, Panel.y + Dlg.y + View.y); } }
        const int Cell = 66, Step = 67, Cols = 6;

        string query = ""; float scroll; GUIStyle field; bool pendingOpen; float pendT;
        int pend = -1; Vector2 pendPos; bool dragging; int dragFrom = -1;

        // ============================================================ apertura / red ============================================================
        /// <summary>StartBank: pide los datos al servidor; la ventana se abre al llegar BANK_OPEN_SUCC.</summary>
        public void Open(int npcId)
        {
            if (ents == null || pendingOpen) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_BANK_OPEN, 8); PutI(p, 4, ents.SelfId); PutI(p, 8, npcId); Send(p); pendingOpen = true; pendT = Time.unscaledTime + 6f;
        }

        protected override void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_REP_PC_BANK_OPEN_SUCC && b.Length >= 12)
            {
                var body = b; q.Enqueue(() =>
                {
                    int n = Mathf.Clamp((body.Length - 4) / 12, 0, 400); if (Bank.Length != n) Bank = new ItemBase[n]; for (int i = 0; i < n; i++) Bank[i] = ReadItem(body, i * 12);
                    pendingOpen = false; query = ""; scroll = 0f; GameMenu.OpenWindow(GameMenu.Win.Bank);
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_BANK_OPEN_FAIL)
            {
                var body = b; q.Enqueue(() =>
                {
                    pendingOpen = false; int code = body.Length >= 4 ? I(body, 0) : 0;
                    if (code == 2) ChatHud.AddEventString(4, "You must purchase access to this bank before you can use it.");
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_BANK_CLOSE_SUCC || type == PacketIds.P_FE2CL_REP_PC_BANK_CLOSE_FAIL) { q.Enqueue(() => { if (open) GameMenu.CloseWindow(); }); }
            else if (type == PacketIds.P_FE2CL_PC_ITEM_MOVE_SUCC && b.Length >= 40)
            {
                var body = b; q.Enqueue(() =>
                {   // eFrom @0, iFromSlot @4, item @8, eTo @20, iToSlot @24, item @28 ; el lado de inventario lo aplica InventoryWindow
                    int ef = I(body, 0), fs = I(body, 4), et = I(body, 20), ts = I(body, 24); MoveAck.Fix(ref ef, fs, ref et, ts, ReadItem(body, 8), ReadItem(body, 28));
                    if (ef == 3 && fs >= 0 && fs < Bank.Length) Bank[fs] = ReadItem(body, 8); if (et == 3 && ts >= 0 && ts < Bank.Length) Bank[ts] = ReadItem(body, 28);
                    Release(); CloseInfo(); dragging = false; pend = -1;
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_ITEM_DELETE_SUCC) q.Enqueue(Release);
        }

        protected override void OnOpened() { dragging = false; pend = -1; dragFrom = -1; GUIUtility.keyboardControl = 0; }
        protected override void OnClosed() { InventoryWindow.Typing = false; pendingOpen = false; dragging = false; }
        protected override void Tick() { }
        void LateUpdate() { if (pendingOpen && Time.unscaledTime > pendT) pendingOpen = false; }

        // ============================================================ acciones ============================================================
        static int FirstFreeBank() { for (int i = 0; i < Bank.Length; i++) if (Bank[i].Id <= 0) return i; return -1; }
        void Move(int ef, int fs, int et, int ts)
        {
            if (waiting) return; if (ef == et && fs == ts) return; Send(ClientPackets.ItemMove(ef, fs, et, ts)); Wait(3f);   // sin FAIL en el protocolo: temporizador de 3 s
        }
        void ToVault(int invSlot)
        {
            if (InventoryWindow.Items[invSlot].Id <= 0) return; int to = FirstFreeBank(); if (to < 0) { Text("Bank is full!"); return; } Move(1, invSlot, 3, to);
        }
        void ToStuff(int bankSlot)
        {
            if (bankSlot < 0 || bankSlot >= Bank.Length || Bank[bankSlot].Id <= 0) return; int to = InventoryWindow.FirstFreeSlot(); if (to < 0) { Text("Inventory is full"); return; } Move(3, bankSlot, 1, to);
        }

        // ---- IInvMode (panel de inventario) ----
        public bool OnClick(int slot) { return false; }
        public void OnQuick(int slot) { if (!Blocked(false)) ToVault(slot); }
        public bool OnDrop(int slot, Vector2 wm)
        {
            int t = CellAt(wm); if (t < 0 || slot < 0) return false; Move(1, slot, 3, t); return true;
        }
        public void InfoButtons(int slot, Rect ext)
        {
            bool eq = ext.height > 300f; float by = eq ? ext.y + 394f : ext.yMax + 6f;   // EquipPopup: botones a y+14+380 dentro de la ficha de equipo
            if (Skin.Button(new Rect(eq ? ext.x + 140f : ext.x + 10f, by, eq ? 150f : 220f, 28f), Loc.T("MOVE TO VAULT"), Skin.Get(Skin.Menu, "RedButton") ?? St("RedButton"), !waiting)) { InventoryWindow.Instance.CloseInfoPub(); ToVault(slot); }
            else if (Skin.Button(new Rect(ext.x + 10f, by, 32f, 32f), "", St("trashbut"), !waiting)) { InventoryWindow.Instance.CloseInfoPub(); InventoryWindow.Instance.AskDelete(slot); }
        }

        // ============================================================ rejilla ============================================================
        int Visible(int[] map)
        {
            int n = 0; string q = (query ?? "").Trim().ToLowerInvariant();
            for (int i = 0; i < Bank.Length; i++)
            {
                if (q.Length > 0) { if (Bank[i].Id <= 0) continue; if (ItemNameOf(Bank[i].Type, Bank[i].Id).ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) continue; }
                if (map != null) map[n] = i; n++;
            }
            return n;
        }
        int[] vis = new int[400]; int visN;
        /// <summary>Ranura de banco bajo una posicion en coordenadas de ventana (-1 si ninguna).</summary>
        int CellAt(Vector2 wm)
        {
            var o = ViewOrigin; var v = new Rect(o.x, o.y, View.width, View.height); if (!v.Contains(wm)) return -1;
            float lx = wm.x - o.x, ly = wm.y - o.y + scroll; int c = (int)(lx / Step), r = (int)(ly / Step); if (c < 0 || c >= Cols || r < 0) return -1; if (lx - c * Step > Cell || ly - r * Step > Cell) return -1;
            int idx = r * Cols + c; return idx >= 0 && idx < visN ? vis[idx] : -1;
        }

        protected override void DrawContent(Event e, bool en)
        {
            bool rep = e.type == EventType.Repaint; Vector2 wm = e.mousePosition; var lab = St("label"); var bf = St("blankbox");
            if (vis.Length < Bank.Length) vis = new int[Bank.Length]; visN = Visible(vis);
            float contentH = ((visN + Cols - 1) / Cols) * Step, maxS = Mathf.Max(0f, contentH - View.height); scroll = Mathf.Clamp(scroll, 0f, maxS);
            var o = ViewOrigin; var viewW = new Rect(o.x, o.y, View.width, View.height);
            // entrada de rejilla (coordenadas de ventana)
            if (en)
            {
                if (e.type == EventType.ScrollWheel && viewW.Contains(wm)) { scroll = Mathf.Clamp(scroll + e.delta.y * 25f, 0f, maxS); e.Use(); }
                int hit = CellAt(wm);
                if (e.type == EventType.MouseDown && e.button == 0 && hit >= 0 && Bank[hit].Id > 0) { pend = hit; pendPos = wm; dragging = false; e.Use(); }
                else if (e.type == EventType.MouseDown && e.button == 1 && hit >= 0 && Bank[hit].Id > 0) { ToStuff(hit); e.Use(); }
                else if (e.type == EventType.MouseDrag && pend >= 0 && !dragging && (wm - pendPos).magnitude > 6f) { dragging = true; dragFrom = pend; }
                else if (e.type == EventType.MouseUp && e.button == 0 && pend >= 0)
                {
                    int from = pend; pend = -1;
                    if (dragging)
                    {
                        dragging = false;
                        if (hit >= 0 && hit != from) Move(3, from, 3, hit);
                        else { int inv = InventoryWindow.Instance != null ? InventoryWindow.Instance.InvSlotAt(wm) : -1; if (inv >= 0) Move(3, from, 1, inv); }
                    }
                    else if (e.clickCount >= 2) ToStuff(from);
                    else { int s = from; ShowInfo(Bank[s].Type, Bank[s].Id, Bank[s].Opt, rr => { bool eqf = rr.height > 300f; if (Skin.Button(new Rect(eqf ? rr.x + 70f : rr.x + 10f, eqf ? rr.y + 394f : rr.yMax + 6f, 220f, 28f), Loc.T("MOVE TO MY EQUIPMENT"), Skin.Get(Skin.Menu, "RedButton") ?? St("RedButton"), !waiting)) { CloseInfo(); ToStuff(s); } }, "Item", Bank[s].TimeLimit); }
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && dragging) { dragging = false; pend = -1; }
            }
            else if (e.type == EventType.MouseUp) { dragging = false; pend = -1; }

            GUI.BeginGroup(Panel);
            // cabecera (RenderNpc)
            GUI.BeginGroup(Head); Pic(new Rect(0, 0, Head.width, Head.height), "vendorinfo"); Skin.Label(new Rect(14, 10, 400, 20), Loc.T("Morbucks Savings and Loan"), lab, Color.white); GUI.EndGroup();
            // area de objetos (DrawBankItem)
            GUI.BeginGroup(Dlg);
            var bb = St("bankslotback"); if (rep && bb != null && bb.N != null) Skin.Bg(new Rect(0, 0, Dlg.width, Dlg.height), bb.N, bb);
            Pic(new Rect(231, 0, 250, 32), "ui_searchtab2.png"); Skin.Label(new Rect(15, 0, 130, 29), Loc.T("BANK"), bf ?? lab, Color.white);
            DrawSearch(en);
            GUI.BeginGroup(View);
            var slotSt = St("slotbox"); var emptySt = St("slotboxempty"); var redSt = St("redItemBox");
            for (int k = 0; k < visN; k++)
            {
                int c = k % Cols, r = k / Cols; var rc = new Rect(c * Step, r * Step - scroll, Cell, Cell); if (rc.yMax < 0f || rc.y > View.height) continue;
                int idx = vis[k]; var it = Bank[idx]; bool has = it.Id > 0; if (!rep) continue;
                var old = GUI.color; if (dragging && dragFrom == idx) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                bool red = has && InventoryWindow.Instance != null && InventoryWindow.Instance.IsRed(it);
                var st = has ? (red && redSt != null ? redSt : slotSt) : emptySt; if (st != null) Skin.Bg(rc, st.N != null ? st.N : (slotSt != null ? slotSt.N : null), st);
                if (has && journal != null) { var ic = journal.ItemTexture(it.Type, it.Id, it.Opt); if (ic != null) GUI.DrawTexture(new Rect(rc.x + 3, rc.y + 3, rc.width - 6, rc.height - 6), ic, ScaleMode.ScaleToFit); if (it.Type == 7 && lab != null) Skin.Label(new Rect(rc.x + 5, rc.y + 5, rc.width, 20), it.Opt.ToString(), lab, Color.white); }
                GUI.color = old;
            }
            GUI.EndGroup();
            if (maxS > 0f) scroll = VScrollBar(e, new Rect(View.x + View.width - 16f, View.y, 16f, View.height), scroll, contentH, View.height, en, new Vector2(Panel.x + Dlg.x, Panel.y + Dlg.y));
            var sh = St("shadow"); if (rep && sh != null && sh.N != null) Skin.Bg(new Rect(6, 32, 447, 449), sh.N, sh);
            GUI.EndGroup();
            GUI.EndGroup();
            if (InventoryWindow.Instance != null) InventoryWindow.Instance.DrawEquipReadOnly(e);
            // objeto arrastrado
            if (rep && dragging && dragFrom >= 0 && dragFrom < Bank.Length && Bank[dragFrom].Id > 0 && journal != null) { var ic = journal.ItemTexture(Bank[dragFrom].Type, Bank[dragFrom].Id, Bank[dragFrom].Opt); if (ic != null) { var oc = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.8f); GUI.DrawTexture(new Rect(wm.x - 30f, wm.y - 30f, 60f, 60f), ic, ScaleMode.ScaleToFit); GUI.color = oc; } }
        }

        void DrawSearch(bool en)
        {
            if (field == null)
            {
                field = new GUIStyle(GUI.skin.textField) { fontSize = 13, alignment = TextAnchor.MiddleLeft }; field.normal.background = null; field.focused.background = null; field.hover.background = null; field.active.background = null;
                var c = new Color(0.8f, 1f, 1f); field.normal.textColor = c; field.focused.textColor = c; field.hover.textColor = c; field.active.textColor = c; field.padding = new RectOffset(4, 4, 1, 1);
            }
            var r = new Rect(295, 7, 177, 18);
            if (Event.current.type == EventType.Repaint) { var oc = GUI.color; GUI.color = new Color(0f, 0.05f, 0.12f, 0.9f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = oc; }
            GUI.enabled = en; GUI.SetNextControlName("banksearch"); string s = GUI.TextField(r, query ?? "", 40, field); if (s != query) { query = s.Replace("\n", ""); scroll = 0f; } GUI.enabled = true;
            bool focus = GUI.GetNameOfFocusedControl() == "banksearch"; InventoryWindow.Typing = focus;
            if (!focus && string.IsNullOrEmpty(query)) Skin.Label(r, Loc.T("Search by item name..."), St("label"), new Color(0.8f, 1f, 1f, 0.6f));
        }
    }
}
