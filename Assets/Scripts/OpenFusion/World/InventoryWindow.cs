// InventoryWindow: port de Panel_PCStuffScript + parte de InventoryManagerScript (Win.MyStuff) y panel de inventario reutilizable (E-Mail). SIN COMPILAR NI PROBAR EN UNITY.
// Skin = FFGuiSkin del InventoryManager (Skin.Inven 1366): invslotback, slotbox, slotboxempty, shadow, closebut, trashbut, helpbut, blankbox, button.
// Datos: inventario de 50 ranuras (sPCLoadData2CL.aInven, QuestSave.Inven) y actualizacion por paquetes ITEM_MOVE_SUCC / ITEM_DELETE_SUCC / ITEM_USE_SUCC / GIVE_ITEM_SUCC / REWARD_ITEM.
// Interaccion: clic = ficha del objeto (la misma de las recompensas del Journal) con botones EQUIP/USE/DELETE; clic derecho o doble clic = equipar/usar; arrastrar = mover o tirar a la papelera.
// Pendiente (ver doc del proyecto): pestana NANOS, vista 3D del personaje, estadisticas, banco/tienda/intercambio, encantamiento/combinacion, cooldown de objetos.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed partial class InventoryWindow : MonoBehaviour
    {
        public Func<string, Texture> TexLoader;
        public static InventoryWindow Instance;
        /// <summary>Inventario del jugador (eIL_Inven, 50 ranuras).</summary>
        public static readonly ItemBase[] Items = new ItemBase[50];
        public static bool Typing;                       // campo de codigo con foco
        /// <summary>Hay una ficha/confirmacion/dialogo abierto: bloquea atajos y el resto de la ventana.</summary>
        public static bool Busy { get { return Instance != null && Instance.Overlay; } }
        public bool Overlay { get { return infoOpen || confirmSlot >= 0 || redeemOpen || nanoPop > 0 || Sorting || gpad.Open; } }
        const int SK = Skin.Inven;
        public static InventoryWindow Ensure(GameObject host) { var w = host.GetComponent<InventoryWindow>(); if (w == null) w = host.AddComponent<InventoryWindow>(); return w; }

        // ---- geometria del original (coordenadas de la ventana de 1036x654) ----
        static readonly Rect WindowRect = new Rect(0, 0, 1036, 654);
        static readonly Rect PanelBack = new Rect(585, 0, 451, 653), Panel = new Rect(593, 7, 380, 632);
        static readonly Rect NanoTabTex = new Rect(96, 0, 129, 29), NanoTabBut = new Rect(135, 5, 60, 15), ItemTabBut = new Rect(10, 5, 105, 15);
        static readonly Rect Content = new Rect(6, 36, 366, 500), Shadow = new Rect(3, 34, 350, 503);
        static readonly Rect DexBut = new Rect(170, 561, 203, 67), TarosBox = new Rect(20, 560, 149, 32), RedeemBut = new Rect(15, 598, 149, 25);
        static readonly Rect CloseBut = new Rect(993, 12, 30, 30), TrashBut = new Rect(983, 562, 32, 32), HelpBut = new Rect(983, 597, 32, 32);
        const int Slot = 67, Step = 69, Cols = 5, Rows = 10;
        // columna de equipo (My Stuff): orden visual -> ranura de equipo (Head 4, Face 5, Back 6, Chest 1, Legs 2, Feet 3, Weapon1 0, Weapon2 7, Vehicle 8)
        static readonly int[] EqOrder = { 4, 5, 6, 1, 2, 3, 0, 7, 8 };
        static readonly string[] EqName = { "HEAD", "FACE", "BACK", "CHEST", "LEGS", "FEET", "WEAPON 1", "WEAPON 2", "VEHICLE" };
        static readonly Rect EqCol = new Rect(512, 21, 64, 560);

        PacketDispatcher disp; WorldEntities ents; XdtTableScript tables; PlayerHud hud; JournalWindow journal; PcAppearance selfApp;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        readonly NumPad gpad = new NumPad();
        bool open, texTried; Texture back; Texture2D tItemTab, tItemTabOver, tPanel, tNano, tNanoOver, tDex, tTaros, tTarosAlt, tLeft;
        float scroll; int pendSlot = -1, pendEq = -1; Vector2 pendPos; bool dragging; int dragFrom = -1, dragEq = -1;
        // ficha
        bool infoOpen; int infoSlot = -1, infoEq = -1; int infoType, infoId, infoOpt;
        int confirmSlot = -1, confirmEq = -1;
        bool redeemOpen; string code = ""; GUIStyle fieldSt;
        float busyT;

        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h, JournalWindow j)
        {
            NanoDetach(); if (disp != null) disp.Unhandled -= OnPacket; disp = d; ents = e; tables = t; hud = h; journal = j; if (disp != null) disp.Unhandled += OnPacket; NanoInit();
            if (e != null)
            {
                if (e.QuestData != null) LoadFrom(e.QuestData);
                e.SelfSpawned += (go, es) => { selfApp = es.Self; if (es.Quest != null) LoadFrom(es.Quest); };
            }
        }
        void Awake() { Instance = this; GameMenu.Implemented.Add(GameMenu.Win.MyStuff); }
        void OnDestroy() { NanoDetach(); if (disp != null) disp.Unhandled -= OnPacket; if (Instance == this) Instance = null; Typing = false; }
        static void LoadFrom(QuestSave qs) { if (qs == null || qs.Inven == null) return; for (int i = 0; i < 50 && i < qs.Inven.Length; i++) Items[i] = qs.Inven[i]; snapDirty = true; }

        // ============================================================ red ============================================================
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static ItemBase It(byte[] b, int o) { return new ItemBase { Type = BitConverter.ToInt16(b, o), Id = BitConverter.ToInt16(b, o + 2), Opt = BitConverter.ToInt32(b, o + 4), TimeLimit = BitConverter.ToInt32(b, o + 8) }; }
        static void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[inv] envio: " + ex.Message); } } }

        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return; var body = b; NanoPacket(type, b);
            if (type == PacketIds.P_FE2CL_PC_ITEM_MOVE_SUCC && b.Length >= 40)
            {   // eFrom, iFromSlotNum, sItemBase(12), eTo, iToSlotNum, sItemBase(12); eIL 1 = inventario
                q.Enqueue(() => { int ef = I(body, 0), fs = I(body, 4), et = I(body, 20), ts = I(body, 24); MoveAck.Fix(ref ef, fs, ref et, ts, It(body, 8), It(body, 28)); if (ef == 1 && fs >= 0 && fs < 50) Items[fs] = It(body, 8); if (et == 1 && ts >= 0 && ts < 50) Items[ts] = It(body, 28); var sound = EquipmentMoveSound(ef, fs, et, ts); if (sound != null) Sfx.Ui(sound); OnMoveAck(ef, fs, et, ts); busyT = 0f; });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_ITEM_DELETE_SUCC && b.Length >= 8)
            {
                q.Enqueue(() => { int il = I(body, 0), sl = I(body, 4); if (il == 1 && sl >= 0 && sl < 50) Items[sl] = default(ItemBase); busyT = 0f; });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_ITEM_USE_SUCC && b.Length >= 24)
            {   // iPC_ID, eIL, iSlotNum, RemainItem(12)
                q.Enqueue(() => { if (ents != null && I(body, 0) != ents.SelfId) return; int il = I(body, 4), sl = I(body, 8); if (il == 1 && sl >= 0 && sl < 50) { GumUsed(sl); Items[sl] = It(body, 12); } });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_GIVE_ITEM_SUCC && b.Length >= 20)
            {   // eIL, iSlotNum, Item
                q.Enqueue(() => { int il = I(body, 0), sl = I(body, 4); if (il == 1 && sl >= 0 && sl < 50) Items[sl] = It(body, 8); });
            }
            else if (type == PacketIds.P_FE2CL_REP_REWARD_ITEM && b.Length >= 36)
            {   // 36 B + iItemCnt (sbyte @16) x sItemReward (20 B: sItemBase @0, eIL @12, iSlotNum @16)
                q.Enqueue(() =>
                {
                    int n = (sbyte)body[16];
                    for (int i = 0; i < n; i++) { int o = 36 + 20 * i; if (o + 20 > body.Length) break; int il = I(body, o + 12), sl = I(body, o + 16); if (il == 1 && sl >= 0 && sl < 50) Items[sl] = It(body, o); }
                });
            }
        }

        // ============================================================ utilidades ============================================================
        static int FirstEmpty() { for (int i = 0; i < 50; i++) if (Items[i].Id <= 0) return i; return -1; }
        static int EquipSlotFor(int type) { if (type >= 0 && type <= 6) return type; if (type == 10) return 8; return -1; }
        static bool IsEquip(int type) { return (type >= 0 && type <= 6) || type == 10; }
        static bool IsUsable(int type) { return type == 7 || type == 9; }
        /// <summary>m_iItemType del objeto general (-1 si no existe).</summary>
        int GenType(int id)
        {
            var gt = tables != null ? tables.m_pGeneralItemTable : null; return gt != null && gt.m_pItemData != null && id > 0 && id < gt.m_pItemData.Length && gt.m_pItemData[id] != null ? gt.m_pItemData[id].m_iItemType : -1;
        }
        /// <summary>InventoryManagerScript.OneClickItem: cofres y generales de tipo 4, 5, 7, 8, 9 y 50 (los gumballs no se usan con clic derecho).</summary>
        bool QuickUsable(ItemBase it)
        {
            if (it.Type == 9) return true; if (it.Type != 7) return false; int g = GenType(it.Id); return g == 4 || g == 5 || g == 7 || g == 8 || g == 9 || g == 50;
        }
        ItemBase EquipAt(int sl) { return selfApp != null && selfApp.Equip != null && sl >= 0 && sl < selfApp.Equip.Length ? selfApp.Equip[sl] : default(ItemBase); }
        Texture Icon(int type, int id, int opt) { return journal != null ? journal.ItemTexture(type, id, opt) : null; }
        int MinLevel(int type, int id)
        {
            if (tables == null || id <= 0) return 0; EquipmentItemTableScript t = null;
            switch (type) { case 0: t = tables.m_pWeaponItemTable; break; case 1: t = tables.m_pShirtsItemTable; break; case 2: t = tables.m_pPantsItemTable; break; case 3: t = tables.m_pShoesItemTable; break; case 4: t = tables.m_pHatItemTable; break; case 5: t = tables.m_pGlassItemTable; break; case 6: t = tables.m_pBackItemTable; break; case 10: t = tables.m_pVehicleItemTable; break; }
            if (t == null || t.m_pItemData == null || id >= t.m_pItemData.Length || t.m_pItemData[id] == null) return 0; return t.m_pItemData[id].m_iMinReqLev;
        }
        /// <summary>InventoryManagerScript.EnableEquip: nivel minimo, genero y (con combinacion) el objeto de aspecto; vehiculos incluidos.</summary>
        bool CanEquip(ItemBase it)
        {
            if (it.Id <= 0 || tables == null || hud == null) return false; if (!IsEquip(it.Type)) return false;
            var t = EqTable(it.Type); if (t == null || t.m_pItemData == null || it.Id >= t.m_pItemData.Length || t.m_pItemData[it.Id] == null) return false;
            var row = t.m_pItemData[it.Id]; var look = row; int style = (it.Opt >> 16) & 0xFFFF;
            if (style > 0 && it.Type <= 3 && style < t.m_pItemData.Length && t.m_pItemData[style] != null) look = t.m_pItemData[style];
            if (hud.Data.Level < row.m_iMinReqLev) return false;
            int gender = selfApp != null ? selfApp.Style.Gender : 0;
            if (look.m_iReqSex > 0 && gender > 0 && look.m_iReqSex != gender) return false;
            return true;
        }
        void Equip(int slot, bool second = false)
        {
            var it = Items[slot]; int es = EquipSlotFor(it.Type); if (es < 0) return;
            if (!CanEquip(it)) { Sfx.Ui("No_Button"); return; }
            if (second && it.Type == 0) es = 7;   // EQUIP WEAPON 2 (SendEquip con iEquipOpt > 0)
            Send(ClientPackets.ItemMove(1, slot, 0, es));
        }
        public static string EquipmentMoveSound(int from, int fromSlot, int to, int toSlot)
        {
            if (from == 1 && to == 0) return toSlot == 0 || toSlot == 7 ? "Weapon_Equipped" : "Clothing_Equipped";
            if (from == 0 && to == 1) return fromSlot == 0 || fromSlot == 7 ? "Weapon_Unequipped" : "Clothing_Unequipped";
            return null;
        }
        /// <summary>MessageTable 239 ("Not Ready CoolTime") al usar un objeto en enfriamiento.</summary>
        void NotReady()
        {
            var mt = tables != null ? tables.m_pMessageTable : null; var m = mt != null ? mt.m_pMessageData : null; string t = m != null && m.Length > 239 && m[239] != null ? m[239].m_szString : null;
            if (string.IsNullOrEmpty(t)) t = "Not Ready CoolTime"; if (ChatHud.Instance != null) ChatHud.Instance.AddSystem(t);
        }
        bool IsTransportItem(int id)
        {
            var gt = tables != null ? tables.m_pGeneralItemTable : null; return gt != null && gt.m_pItemData != null && id > 0 && id < gt.m_pItemData.Length && gt.m_pItemData[id] != null && gt.m_pItemData[id].m_iItemType == 9;
        }
        /// <summary>MessageTable 153 (confirmar borrado) con el nombre del objeto en %s y la cantidad en %d.</summary>
        string DeleteText(ItemBase it)
        {
            var mt = tables != null ? tables.m_pMessageTable : null; var m = mt != null ? mt.m_pMessageData : null; string t = m != null && m.Length > 153 && m[153] != null ? m[153].m_szString : null;
            if (string.IsNullOrEmpty(t)) return Loc.T("Are you sure you want to delete this item?");
            string nm = journal != null ? journal.ItemName(it.Type, it.Id, it.Opt) : ""; return t.Replace("%s", nm).Replace("%d", (it.Type == 7 ? Mathf.Max(1, it.Opt) : 1).ToString());
        }
        void SysMsg(int id)
        {
            var mt = tables != null ? tables.m_pMessageTable : null; var m = mt != null ? mt.m_pMessageData : null; string t = m != null && id >= 0 && id < m.Length && m[id] != null ? m[id].m_szString : null;
            if (!string.IsNullOrEmpty(t) && ChatHud.Instance != null) ChatHud.Instance.AddSystem(t);
        }
        void Use(int slot)
        {
            var cu = Items[slot]; if (cu.Type == 7 && GenType(cu.Id) == 4 && ItemCooldown.TryUseItem(tables, cu.Id)) { Sfx.Ui("No_Button"); NotReady(); return; }
            if (cu.Type == 7 && IsTransportItem(cu.Id))
            {   // CnEquip.UseItem: objeto de transporte (m_iItemType 9) -> ventana de transporte en modo objeto, sin REQ_ITEM_USE
                if (InCombatNow() || GameMenu.Riding || TransportWindow.Instance == null) return;
                if (TransportWindow.Active) { SysMsg(246); return; }
                GameMenu.CloseWindow(); TransportWindow.Instance.OpenItem(slot); return;
            }
            if (cu.Type == 9)
            {   // CnEquip.UseItem: las cajas se abren con REQ_ITEM_CHEST_OPEN { eIL, iSlotNum, ChestItem sItemBase{iType, iID, iOpt, iTimeLimit} } (20 B)
                var c = ClientPackets.New(PacketIds.P_CL2FE_REQ_ITEM_CHEST_OPEN, 20); ClientPackets.PutInt(c, 4, 1); ClientPackets.PutInt(c, 8, slot);
                BitConverter.GetBytes((short)cu.Type).CopyTo(c, 12); BitConverter.GetBytes((short)cu.Id).CopyTo(c, 14); ClientPackets.PutInt(c, 16, cu.Opt); ClientPackets.PutInt(c, 20, 0); Send(c); return;
            }
            short nanoSlot = 0;
            var gt = tables != null ? tables.m_pGeneralItemTable : null; var ge = gt != null && gt.m_pItemData != null && cu.Id > 0 && cu.Id < gt.m_pItemData.Length ? gt.m_pItemData[cu.Id] : null;
            if (ge != null && ge.m_iStimPackAttri > 0)
            {   // gumball: se aplica al primer nano equipado de su estilo (m_iStimPackAttri == estilo + 1); sin ninguno -> mensaje 103
                nanoSlot = -1; if (hud != null) for (int k = 0; k < 3; k++) { var nr = NanoRow(hud.Data.NanoSlots[k]); if (nr != null && (ge.m_iStimPackAttri == 4 || ge.m_iStimPackAttri == nr.m_iStyle + 1)) { nanoSlot = (short)k; break; } }
                if (nanoSlot < 0) { Sfx.Ui("No_Button"); SysMsg(103); return; }
            }
            if (ge != null && ge.m_iStimPackAttri > 0 && nanoSlot >= 0) GumMark(slot, nanoSlot);
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_ITEM_USE, 12); ClientPackets.PutInt(p, 4, 1); ClientPackets.PutInt(p, 8, slot); p[12] = (byte)(nanoSlot & 0xFF); p[13] = (byte)((nanoSlot >> 8) & 0xFF); Send(p);   // iNanoSlot
        }
        // temporizador de gumballs: se anota (ranura, nano, item, hora) al enviar REQ_ITEM_USE y se arranca en NanoWheelHud al llegar ITEM_USE_SUCC (buff de la skill 144, duracion de la tabla)
        readonly int[] gumNano = new int[50], gumItem = new int[50]; readonly float[] gumTime = new float[50];
        void GumMark(int slot, int nano) { if (slot < 0 || slot >= 50 || Items[slot].Id <= 0) return; gumNano[slot] = nano; gumItem[slot] = Items[slot].Id; gumTime[slot] = Time.unscaledTime; }
        void GumUsed(int slot)
        {
            if (gumItem[slot] <= 0) return; int id = gumItem[slot], nano = gumNano[slot]; bool fresh = Time.unscaledTime - gumTime[slot] < 6f; gumItem[slot] = 0;
            if (fresh && Items[slot].Id == id && Wheel != null) Wheel.StartGumTimer(nano, 144);
        }
        /// <summary>GumPopup.SendUse: gumball dado al nano de la ranura k.</summary>
        void UseNano(int slot, int k)
        {
            if (slot < 0 || slot >= 50 || Items[slot].Id <= 0 || k < 0 || k > 2) return;
            GumMark(slot, k);
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_ITEM_USE, 12); ClientPackets.PutInt(p, 4, 1); ClientPackets.PutInt(p, 8, slot); p[12] = (byte)k; p[13] = 0; Send(p);
        }
        /// <summary>PopupControll (eGui_Equip, clic en objeto del inventario): cofres y generales abren TuringPopup / GumPopup en (610,90). true = consumido.</summary>
        bool GeneralPopup(int slot)
        {
            if (slot < 0 || slot >= 50 || journal == null) return false; var it = Items[slot];
            if (it.Id <= 0 || (it.Type != 7 && it.Type != 9)) return false;
            Texture ic = Icon(it.Type, it.Id, it.Opt); string nm = journal.ItemName(it.Type, it.Id); string ds = journal.ItemComment(it.Type, it.Id); int s = slot; Sfx.Button();
            if (it.Type == 9) { gpad.ShowUseIn(ic, nm, ds, "OPEN", () => { Sfx.Ui("Container_Open01"); Use(s); }, () => AskDelete(s), null); return true; }
            int g = GenType(it.Id);
            if (g == 4 || g == 5 || g == 7 || g == 8 || g == 9) { gpad.ShowUseIn(ic, nm, ds, "USE ITEM", () => Use(s), () => AskDelete(s), "Level 0"); return true; }
            if (g == 3)
            {
                var gt = tables != null ? tables.m_pGeneralItemTable : null; int attri = gt != null && gt.m_pItemData != null && it.Id < gt.m_pItemData.Length && gt.m_pItemData[it.Id] != null ? gt.m_pItemData[it.Id].m_iStimPackAttri : 0;
                var nano = new Texture[3]; var okn = new bool[3];
                if (hud != null) for (int k = 0; k < 3; k++) { var nr = NanoRow(hud.Data.NanoSlots[k]); if (nr == null) continue; nano[k] = NanoIconTex(nr, false); okn[k] = attri == 4 || attri == nr.m_iStyle + 1; }
                gpad.ShowGive(ic, nm, ds, true, 0, nano, okn, k => UseNano(s, k), () => AskDelete(s)); return true;
            }
            gpad.ShowGive(ic, nm, ds, false, Mathf.Max(1, it.Opt), null, null, null, () => AskDelete(s)); return true;
        }
        void Delete(int slot)
        {
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_ITEM_DELETE, 8); ClientPackets.PutInt(p, 4, 1); ClientPackets.PutInt(p, 8, slot); Send(p);
        }
        void Unequip(int eqSlot)
        {
            int to = FirstEmpty(); if (to < 0) { Sfx.Ui("No_Button"); return; }
            Send(ClientPackets.ItemMove(0, eqSlot, 1, to));
        }
        void Quick(int slot)
        {
            if (Mode != null) { Mode.OnQuick(slot); return; }
            var it = Items[slot]; if (it.Id <= 0) return;
            if (IsEquip(it.Type)) Equip(slot); else if (QuickUsable(it)) Use(slot);
        }
        void OpenInfo(int slot, int eq)
        {
            ItemBase it = slot >= 0 ? Items[slot] : EquipAt(eq); if (it.Id <= 0 || journal == null) return;
            infoSlot = slot; infoEq = eq; infoType = it.Type; infoId = it.Id; infoOpt = it.Opt;
            // las ranuras de equipo guardan el tipo segun la ranura
            if (slot < 0) infoType = eq == 8 ? 10 : eq == 7 ? 0 : eq;
            if (it.Type == 8) return;     // objetos de mision: sin ficha
            journal.ShowItemExternal(infoType, infoId, infoOpt, slot >= 0 ? "Item" : "Equipped", it.TimeLimit); infoOpen = journal.ExternalOpen; if (infoOpen) Sfx.Button();
        }
        void CloseInfo() { EndTryOn(); infoOpen = false; infoSlot = infoEq = -1; if (journal != null) journal.CloseExternal(); }

        // ============================================================ bucle ============================================================
        void Update()
        {
            NanoUpdate(); Action a; while (q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[inv] " + ex.Message); } }
            try { StackTick(); } catch (Exception ex2) { Debug.LogWarning("[inv] pilas: " + ex2.Message); AbortQueue(); }
            bool now = GameMenu.Current == GameMenu.Win.MyStuff; if (now && Machine) tab = 1;
            if (now && !open) { open = true; if (Preview != null) Preview.Yaw = AvatarPreview.FrontYaw; scroll = 0f; nanoPop = -1; CloseInfo(); confirmSlot = confirmEq = -1; redeemOpen = false; LoadTextures(); }
            else if (!now && open) { open = false; VendorWindow.ReturnToStore = false; Machine = false; machineBusy = false; nanoPop = -1; turnDir = 0f; CloseInfo(); confirmSlot = confirmEq = -1; redeemOpen = false; Typing = false; back = null; }
            if (journal != null && infoOpen && !journal.ExternalOpen) infoOpen = false;
            if (Overlay && Input.GetKeyDown(KeyCode.Escape)) { if (gpad.Open) gpad.Open = false; else if (nanoPop > 0) nanoPop = -1; else if (redeemOpen) { redeemOpen = false; Typing = false; } else if (confirmSlot >= 0 || confirmEq >= 0) { confirmSlot = confirmEq = -1; } else CloseInfo(); }
            DualItemTick();
        }

        public void ResetOverlays() { gpad.Open = false; nanoPop = -1; CloseInfo(); confirmSlot = confirmEq = -1; redeemOpen = false; Typing = false; pendSlot = -1; dragging = false; }
        public void OpenTab(bool nanos) { if (GameMenu.OpenWindow(GameMenu.Win.MyStuff)) tab = nanos ? 1 : 0; }
        public void EnsureTextures() { if (!texTried) LoadTextures(); }
        void LoadTextures()
        {
            texTried = true;
            tPanel = UiKit.Tex("equipmentback"); tNano = UiKit.Tex("nanotab"); tNanoOver = UiKit.Tex("nanotabover"); tDex = UiKit.Tex("dexlabsbut"); tTaros = UiKit.Tex("taros_count"); tTarosAlt = UiKit.Tex("tarosicon"); tLeft = UiKit.Tex("left_back"); tItemTab = UiKit.Tex("inventab"); tItemTabOver = UiKit.Tex("inventabover");
            if (TexLoader != null) { foreach (var ext in new[] { ".psd", ".png", ".dds", ".tga", "" }) { try { back = TexLoader("TutorialAssets/panelback" + ext); } catch (Exception) { back = null; } if (back != null) break; } }
        }

        // ============================================================ ventana My Stuff ============================================================
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
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
                GUI.BeginGroup(new Rect(-WindowRect.width * 0.5f, -WindowRect.height * 0.5f, WindowRect.width, WindowRect.height));
                if (DualItemPreview && DualScreenController.Instance != null && DualScreenController.Instance.Active) DrawOverlays(e);
                else { DrawLeft(e, !Overlay); DrawPanel(e, true, false); }
                GUI.EndGroup();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        // ============================================================ panel derecho (reutilizable) ============================================================
        /// <summary>Dibuja el panel de inventario (451x653 a la derecha de la ventana) y sus fichas/dialogos. Se llama dentro del grupo de ventana de 1036x654.
        /// emailMode = modo E-Mail (sin pestana de nanos). El boton de cerrar cierra la ventana actual.</summary>
        public void DrawPanel(Event e, bool en, bool emailMode)
        {
            if (!texTried) LoadTextures();
            bool rep = e.type == EventType.Repaint; en = en && !Overlay; bool nano = !emailMode && tab == 1;
            var inv = Skin.Get(SK, nano ? "nanoslotback" : "invslotback") ?? Skin.Get(SK, "invslotback"); var bf = Skin.Get(SK, "blankbox"); var btn = Skin.Get(SK, "button");
            // fondo
            if (rep && tPanel != null) UiKit.Slice(PanelBack, tPanel, 5, 5, 0, 0);
            Vector2 wm = e.mousePosition; var view = new Rect(Panel.x + Content.x, Panel.y + Content.y, Content.width, Content.height);
            float contentH = nano ? Mathf.Max(NanoRowsCount() * Step, 1) : Rows * Step; float max = Mathf.Max(0f, contentH - view.height);
            // ---- entrada (en coordenadas de ventana) ----
            if (en)
            {
                if (e.type == EventType.ScrollWheel && view.Contains(wm)) { scroll = Mathf.Clamp(scroll + e.delta.y * 25f, 0f, max); e.Use(); }
                int hs = nano ? -1 : SlotAt(wm, view);
                if (e.type == EventType.MouseDown && e.button == 0 && hs >= 0 && Items[hs].Id > 0) { pendSlot = hs; pendPos = wm; dragging = false; e.Use(); }
                else if (e.type == EventType.MouseDown && e.button == 1 && hs >= 0 && Items[hs].Id > 0) { Quick(hs); e.Use(); }
                else if (e.type == EventType.MouseDrag && pendSlot >= 0 && !dragging && (wm - pendPos).magnitude > 6f) { dragging = true; dragFrom = pendSlot; }
                else if (e.type == EventType.MouseUp && e.button == 0 && pendSlot >= 0)
                {
                    int from = pendSlot; pendSlot = -1;
                    if (dragging) { dragging = false; Drop(from, wm, view); }
                    else if (e.clickCount >= 2) Quick(from); else if (Mode != null ? !Mode.OnClick(from) : (Machine || emailMode || !GeneralPopup(from))) OpenInfo(from, -1);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 0 && dragging) { dragging = false; pendSlot = -1; }
            }
            else if (e.type == EventType.MouseUp) { dragging = false; pendSlot = -1; }
            // ---- dibujo del panel ----
            GUI.BeginGroup(Panel);
            if (rep && !emailMode)
            {
                if (nano && !Machine) { var t = (new Rect(Panel.x + ItemTabBut.x, Panel.y + ItemTabBut.y, ItemTabBut.width, ItemTabBut.height)).Contains(wm) ? tItemTabOver : tItemTab; if (t != null) GUI.DrawTexture(new Rect(0, 0, 142, 29), t); }
                else { var t = (new Rect(Panel.x + NanoTabBut.x, Panel.y + NanoTabBut.y, NanoTabBut.width, NanoTabBut.height)).Contains(wm) ? tNanoOver : tNano; if (t != null) GUI.DrawTexture(NanoTabTex, t); }
            }
            if (rep && inv != null) Skin.Bg(new Rect(0, 0, Panel.width, Panel.height), inv.N, inv);
            if (bf != null)
            {
                if (nano) { if (Machine) { /* NanoMachine: el original no dibuja la pestana EQUIPMENT */ } else if (Skin.Button(ItemTabBut, Loc.T("EQUIPMENT"), bf, en, true)) { tab = 0; scroll = 0f; Sfx.Ui("Tab_Click01"); } }
                else Skin.Label(ItemTabBut, Loc.T("EQUIPMENT"), bf, Color.white);
                if (!emailMode) { if (!nano) { if (Skin.Button(NanoTabBut, Loc.T("NANOS"), bf, en, true)) { tab = 1; scroll = 0f; Sfx.Ui("Tab_Click01"); } } else Skin.Label(NanoTabBut, Loc.T("NANOS"), bf, Color.white); }
            }
            if (bf != null && !nano && SortAllowed()) {
                if (Skin.Button(SortBut, "", btn, en && !StackBusy, false)) StartSort();
                if (rep) DrawFittedLegacyText(new Rect(SortBut.x + 3, SortBut.y + 2, SortBut.width - 6, SortBut.height - 4), Loc.T("SORT"), btn, en && !StackBusy ? Color.white : Color.gray);
            }
            // rejilla
            var slotSt = Skin.Get(SK, "slotbox"); var emptySt = Skin.Get(SK, "slotboxempty"); var redSt = Skin.Get(SK, "redItemBox"); var cnt = Skin.Get(SK, "label") ?? bf;
            GUI.BeginGroup(Content);
            if (nano) DrawNanoGrid(e, en);
            else if (rep)
            {
                for (int i = 0; i < 50; i++)
                {
                    int c = i % Cols, r = i / Cols; var rc = new Rect(c * Step, r * Step - scroll, Slot, Slot); if (rc.yMax < 0f || rc.y > Content.height) continue;
                    var it = Items[i]; bool has = it.Id > 0; bool dim = dragging && dragFrom == i;
                    var st = has ? ((IsRed(it) && redSt != null) ? redSt : slotSt) : emptySt;
                    var old = GUI.color; if (dim) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                    if (st != null) Skin.Bg(rc, st.N != null ? st.N : (slotSt != null ? slotSt.N : null), st);
                    if (has) { var ic = Icon(it.Type, it.Id, it.Opt); if (ic != null) GUI.DrawTexture(new Rect(rc.x + 3, rc.y + 3, rc.width - 6, rc.height - 6), ic, ScaleMode.ScaleToFit); }
                    if (has) ItemCooldown.Draw(tables, new Rect(rc.x + 2, rc.y + 2, 63, 63), it.Type, it.Id);
                    GUI.color = old;
                    if (has && it.Type == 7 && cnt != null) Skin.Label(new Rect(rc.x + 5, rc.y + 5, rc.width, 20), it.Opt.ToString(), cnt, Color.white);
                }
            }
            GUI.EndGroup();
            DrawSortingBanner(rep, cnt);
            // barra de desplazamiento
            if (max > 0f) DrawScrollbar(e, new Rect(Content.x + Content.width - 16f, Content.y, 16f, Content.height), contentH, max, wm, Panel.x, Panel.y);
            var sh = Skin.Get(SK, "shadow"); if (sh != null && rep) Skin.Bg(Shadow, sh.N, sh);
            // taros
            if (rep && tDex != null) GUI.DrawTexture(DexBut, tDex);
            if (Skin.Button(RedeemBut, Loc.T("REDEEM CODE"), btn, en)) { redeemOpen = true; code = ""; }
            if (rep)
            {
                var tt = tTaros != null ? tTaros : tTarosAlt; if (tt != null) GUI.DrawTexture(TarosBox, tt);
                string amount = Mathf.Clamp(hud != null ? hud.Data.Taros : 0, 0, 999999999).ToString("D9");
                DrawFittedLegacyText(TarosDigitsRect, amount, bf ?? btn, Color.white);

            }
            GUI.EndGroup();
            // botones fuera del grupo
            if (Skin.Button(CloseBut, "", Skin.Get(SK, "closebut"), en)) { GameMenu.CloseWindow(); return; }
            var trash = Skin.Get(SK, "trashbut"); bool trashHot = dragging && TrashBut.Contains(wm);
            if (trash != null) { if (rep) Skin.Bg(TrashBut, trashHot && trash.H != null ? trash.H : trash.N, trash); }
            if (Skin.Button(HelpBut, "", Skin.Get(SK, "helpbut"), en) && GameMenu.Implemented.Contains(GameMenu.Win.Guide)) { GameMenu.CloseWindow(); GameMenu.OpenWindow(GameMenu.Win.Guide); return; }
            // objeto arrastrado
            if (rep && dragging && dragFrom >= 0 && Items[dragFrom].Id > 0) { var ic = Icon(Items[dragFrom].Type, Items[dragFrom].Id, Items[dragFrom].Opt); if (ic != null) { var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.8f); GUI.DrawTexture(new Rect(wm.x - 30f, wm.y - 30f, 60f, 60f), ic, ScaleMode.ScaleToFit); GUI.color = old; } }
            DrawOverlays(e);
        }

        int SlotAt(Vector2 wm, Rect view)
        {
            if (!view.Contains(wm)) return -1; float lx = wm.x - view.x, ly = wm.y - view.y + scroll; int c = (int)(lx / Step), r = (int)(ly / Step); if (c < 0 || c >= Cols || r < 0 || r >= Rows) return -1;
            if (lx - c * Step > Slot || ly - r * Step > Slot) return -1; return r * Cols + c;
        }
        void Drop(int from, Vector2 wm, Rect view)
        {
            if (Mode != null && Mode.OnDrop(from, wm)) return;
            int to = SlotAt(wm, view);
            if (to >= 0 && to != from) { Send(ClientPackets.ItemMove(1, from, 1, to)); return; }
            if (TrashBut.Contains(wm)) { confirmSlot = from; confirmEq = -1; return; }
            if (EqCol.Contains(wm) && GameMenu.Current == GameMenu.Win.MyStuff)
            {
                int es = EquipSlotFor(Items[from].Type); if (es >= 0) { int row = (int)((wm.y - EqCol.y) / 62f); if (row >= 0 && row < 9 && EqOrder[row] == es) Equip(from); }
            }
        }
        void DrawScrollbar(Event e, Rect track, float contentH, float max, Vector2 wm, float ox, float oy)
        {   // track en coordenadas del grupo Panel
            var tb = Skin.Get(SK, "verticalScrollbar"); var th = Skin.Get(SK, "verticalScrollbarThumb");
            float frac = Content.height / contentH, thumbH = Mathf.Max(16f, track.height * frac), ty = track.y + (track.height - thumbH) * (max > 0f ? scroll / max : 0f); var thumb = new Rect(track.x, ty, 16f, thumbH);
            if (e.type == EventType.Repaint)
            {
                if (tb != null && tb.N != null) GUI.DrawTexture(track, tb.N); if (th != null && th.N != null) GUI.DrawTexture(thumb, th.N);
            }
            var wt = new Rect(track.x + ox, track.y + oy, track.width, track.height); int id = GUIUtility.GetControlID(FocusType.Passive, track);
            if (!Overlay)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && wt.Contains(wm)) { GUIUtility.hotControl = id; e.Use(); }
                if (GUIUtility.hotControl == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)) { float t = Mathf.Clamp01((wm.y - wt.y - thumbH * 0.5f) / Mathf.Max(1f, wt.height - thumbH)); scroll = t * max; e.Use(); }
                else if (GUIUtility.hotControl == id && e.type == EventType.MouseUp) { GUIUtility.hotControl = 0; e.Use(); }
            }
        }

        // ============================================================ ficha, confirmacion, codigo ============================================================
        void DrawOverlays(Event e)
        {
            bool rep = e.type == EventType.Repaint;
            if (infoOpen && journal != null)
            {
                if (rep) { var oc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.4f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = oc; }
                journal.DrawExternal(e);
                if (!journal.ExternalOpen) { infoOpen = false; return; }
                var rr = journal.ExtRect; float y = rr.yMax + 6f, x = rr.x; var red = Skin.Get(Skin.Menu, "RedButton") ?? Skin.Get(SK, "RedButton"); var cancel = Skin.Get(Skin.Menu, "CancelButton") ?? Skin.Get(SK, "CancelButton");
                if (infoSlot >= 0 && Mode != null) Mode.InfoButtons(infoSlot, rr);
                else if (infoSlot >= 0)
                {
                    var it = Items[infoSlot];
                    if (it.Type == 0)
                    {   // rectWpn1But (120,360,170x28) / rectWpn2But (120,390,170x28)
                        bool ce = CanEquip(it);
                        if (Skin.Button(new Rect(x + 120f, y - 34f, 170f, 28f), Loc.T("EQUIP WEAPON") + " 1", red, ce)) { int s = infoSlot; CloseInfo(); Equip(s, false); return; }
                        if (Skin.Button(new Rect(x + 120f, y - 4f, 170f, 28f), Loc.T("EQUIP WEAPON") + " 2", red, ce)) { int s = infoSlot; CloseInfo(); Equip(s, true); return; }
                    }
                    else if (IsEquip(it.Type) && Skin.Button(new Rect(x + 160f, y, 130f, 28f), Loc.T("EQUIP"), red, CanEquip(it))) { int s = infoSlot; CloseInfo(); Equip(s); return; }
                    if (IsUsable(it.Type) && Skin.Button(new Rect(x + 160f, y, 130f, 28f), Loc.T("USE"), red)) { int s = infoSlot; CloseInfo(); Use(s); return; }
                    if (Skin.Button(new Rect(x + 10f, y - 6f, 100f, 28f), Loc.T("DELETE"), cancel)) { confirmSlot = infoSlot; confirmEq = -1; CloseInfo(); return; }
                }
                else if (infoEq >= 0)
                {
                    if (Skin.Button(new Rect(x + 160f, y, 130f, 28f), Loc.T("UNEQUIP"), red)) { int s = infoEq; CloseInfo(); Unequip(s); return; }
                }
            }
            if (confirmSlot >= 0)
            {
                if (rep) { var oc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(new Rect(300f, 220f, 436f, 200f), Texture2D.whiteTexture); GUI.color = oc; }
                var lab = Skin.Get(SK, "label"); var it = Items[confirmSlot];
                if (lab != null) Skin.Label(new Rect(315f, 235f, 406f, 120f), DeleteText(it), lab, Color.white);
                var red = Skin.Get(SK, "RedButton") ?? Skin.Get(Skin.Menu, "RedButton"); var cancel = Skin.Get(SK, "CancelButton") ?? Skin.Get(Skin.Menu, "CancelButton");
                if (Skin.Button(new Rect(330f, 370f, 150f, 28f), Loc.T("YES"), red)) { int s = confirmSlot; confirmSlot = -1; if (it.Id > 0) Delete(s); }
                else if (Skin.Button(new Rect(556f, 370f, 150f, 28f), Loc.T("NO"), cancel)) confirmSlot = -1;
            }
            if (nanoPop > 0) DrawNanoPopup(e);
            if (redeemOpen) DrawRedeem(e);
            if (gpad.Open) gpad.Draw(e, true);
        }

        GUIStyle FieldStyle()
        {
            var s = new GUIStyle(GUI.skin.textField) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            s.normal.background = null; s.focused.background = null; s.hover.background = null; s.active.background = null; var c = new Color(0.8f, 1f, 1f); s.normal.textColor = c; s.focused.textColor = c; s.hover.textColor = c; s.active.textColor = c; s.padding = new RectOffset(4, 4, 2, 2); return s;
        }
        void DrawRedeem(Event e)
        {   // Panel_PCStuffScript.DoRedeemWindow: 526x164 centrado; REDEEM envia "/redeem <codigo> " por el chat libre
            bool rep = e.type == EventType.Repaint; var box = new Rect((WindowRect.width - 526f) / 2f, (WindowRect.height - 164f) / 2f, 526f, 164f);
            if (rep) { var oc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.98f); GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = new Color(0f, 0.8f, 0.9f, 1f); GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(box.x, box.yMax - 2f, box.width, 2f), Texture2D.whiteTexture); GUI.color = oc; }
            var big = Skin.Get(Skin.Menu, "bigfont16") ?? Skin.Get(SK, "label"); var lab = Skin.Get(SK, "label");
            GUI.BeginGroup(box);
            if (big != null) Skin.Label(new Rect(0f, 15f, box.width, 20f), Loc.T("REDEEM CODE"), big, Color.white);
            if (lab != null) Skin.Label(new Rect(20f, 40f, box.width - 40f, 20f), Loc.T("Have a code? Try entering it below to get an exclusive item!"), lab, Color.white);
            if (fieldSt == null) fieldSt = FieldStyle();
            if (rep) { var oc = GUI.color; GUI.color = new Color(0f, 0.05f, 0.12f, 1f); GUI.DrawTexture(new Rect(75f, 73f, 378f, 24f), Texture2D.whiteTexture); GUI.color = oc; }
            GUI.SetNextControlName("redeemcode"); string s = GUI.TextField(new Rect(75f, 73f, 378f, 24f), code ?? "", 32, fieldSt); if (s != code) code = s.Replace("\n", "");
            if (GUI.GetNameOfFocusedControl() != "redeemcode") GUI.FocusControl("redeemcode"); Typing = true;
            var red = Skin.Get(SK, "RedButton") ?? Skin.Get(Skin.Menu, "RedButton"); var cancel = Skin.Get(SK, "CancelButton") ?? Skin.Get(Skin.Menu, "CancelButton");
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            if (Skin.Button(new Rect(40f, 125f, 152f, 27f), Loc.T("CANCEL"), cancel)) { redeemOpen = false; code = ""; Typing = false; Sfx.Ui("No_Button"); }
            else if (Skin.Button(new Rect(335f, 125f, 152f, 27f), Loc.T("REDEEM"), red) || enter)
            {
                if (code.Length >= 3) { if (!code.Contains(" ")) { Send(ChatPackets.FreeChat("/redeem " + code + " ")); Sfx.Ui("Action_Sucess"); } redeemOpen = false; code = ""; Typing = false; }
            }
            GUI.EndGroup();
            if (!redeemOpen) Typing = false;
        }
    }
}
