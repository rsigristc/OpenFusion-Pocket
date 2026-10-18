// CombineWindow: port de cnCombiMode + cnGuiCombi (NPC tipo 26: icono 20 -> NpcIconMode.StartCombi "COMBINE ITEMS", icono 21 -> StartCombiRule "COMBINE RULES"). SIN COMPILAR NI PROBAR EN UNITY.
// Fuente original: Assembly-CSharp/cnCombiMode.cs, cnGuiCombi.cs, InventoryManagerScript (AttachCombiItem/DetachCombiItem/EnableEquipCombi), NpcIconMode.StartCombi/StartCombiRule.
// Paquetes: REQ_PC_ITEM_COMBINATION {iCostumeItemSlot, iStatItemSlot, iCashItemSlot1, iCashItemSlot2} (16 B, 318767256)
//   -> REP_PC_ITEM_COMBINATION_SUCC (36 B, 822083862): iNewItemSlot @0, sNewItem @4 (12 B), iStatItemSlot @16, iCashItemSlot1 @20, iCashItemSlot2 @24, iCandy @28, iSuccessFlag @32 (1 = exito, 0 = fallo de la tirada)
//   -> REP_PC_ITEM_COMBINATION_FAIL (20 B, 822083863): iErrorCode @0 (+ las 4 ranuras echo). El original solo muestra "Item Combination error. (n)".
// Flujo original: el jugador arrastra un objeto a la ranura STYLE (slot 19: aspecto) y otro a STATS (slot 20: estadisticas y nivel); el cliente calcula "chance of success" y coste en taros con CombiningTable
//   (tabla 35, fila |nivelLook - nivelStat| + 1) y los precios; COMBINE -> mensaje 254 (confirmar) / 256 (taros insuficientes) -> animacion del NPC de 4 s -> REQ -> SUCC. Con flag 1: ranura iNewItemSlot = sNewItem (el objeto STATS
//   con (idLook << 16) en Opt), ranura STATS vacia y panel "HOORAY!"; con flag 0: mensaje 255 con los iconos de ambos objetos.
// Interfaz: MonoBehaviour independiente (OnGUI propio, marco 1036x654 como ModalWindow) con el panel de inventario a la derecha (InventoryWindow.DrawPanel + IInvMode, sin tocar esos archivos).
//   Las ranuras STYLE/STATS son locales (como PlayerInventoryItem.iSlotType 19/20): guardan el indice de InventoryWindow.Items; el objeto sigue visible en la rejilla de la derecha (no hay gancho para ocultarlo).
//   Se asignan con: arrastrar desde el panel de inventario a la zona STYLE/STATS, clic derecho/doble clic (primera ranura libre) o la ficha del objeto (botones USE AS STYLE ITEM / USE AS STATS ITEM). Clic en una ranura = quitar.
// Modo reglas (icono 21): el original NO dibuja nada en GUIMode_Rule (cnGuiCombi.OnGUI vacio). Aqui se muestra una pagina informativa con las restricciones reales del cliente y la tabla CombiningTable
//   (probabilidades por salto de nivel y diferencia de rareza + constantes de coste). Tambien se abre con el boton "?" de la ventana de combinar.
// Diferencias asumidas: (1) no hay camara 3D del NPC (cNPC/cNPC2): se usa CombiIcon2 como retrato y la espera de 4 s muestra CombiWaitBG con barra de progreso; (2) sonido de exito "Action_Sucess" (el original CrocPot_success);
//   (3) con iSuccessFlag 0 el original no toca el inventario (ClearAll solo desasocia): se replica; si el servidor destruye ambos objetos, poner CombineWindow.FailDestroysItems = true;
//   (4) EnableEquipCombi se aproxima con InventoryWindow.CanEquip (nivel); (5) COMBINE exige taros > coste (como el original) salvo coste 0 (tabla ausente), donde el servidor decide;
//   (6) el boton X / "?" del panel de inventario no cierran/abren nada del menu del juego: se interceptan aqui (X = cerrar esta ventana, ? = reglas).
using System;
using System.Collections.Concurrent;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class CombineWindow : MonoBehaviour, IInvMode
    {
        public static CombineWindow Instance;
        public static CombineWindow Ensure(GameObject host) { var w = host.GetComponent<CombineWindow>(); if (w == null) w = host.AddComponent<CombineWindow>(); Instance = w; return w; }
        /// <summary>Ventana abierta: bloquea clics al mundo y movimiento (anadir a GameMenu.Blocking / busyPrev).</summary>
        public static bool Active { get { return Instance != null && Instance.open; } }
        /// <summary>Si el servidor destruye ambos objetos cuando la tirada falla (iSuccessFlag 0), ponerlo a true para vaciar las ranuras locales de inventario.</summary>
        public static bool FailDestroysItems;

        /// <summary>Carga de texturas del bundle ("TutorialAssets/panelback" + ext). Opcional.</summary>
        public Func<string, Texture> TexLoader;

        const int SK = 1367;                                                  // GUISkin "FusionFallCombi" (path_id en bindings.json)
        static readonly Rect WindowRect = new Rect(0, 0, 1036, 654), Frame = new Rect(0, 0, 585, 653);
        static readonly Rect LookRect = new Rect(168, 167, 305, 133), StatRect = new Rect(168, 321, 305, 230), LookSlotR = new Rect(45, 176, 64, 64), StatSlotR = new Rect(44, 363, 64, 64);
        static readonly Rect PanelClose = new Rect(993, 12, 30, 30), PanelHelp = new Rect(983, 597, 32, 32);   // geometria de InventoryWindow (CloseBut / HelpBut)
        static readonly Color ClrReady = new Color(0.15f, 0.31f, 0.5f), ClrBad = new Color(0.6f, 0f, 0f);

        PacketDispatcher disp; WorldEntities ents; XdtTableScript tables; PlayerHud hud; MessageTableScript msgT; JournalWindow journal;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();

        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h)
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            disp = d; ents = e; tables = t; hud = h; msgT = t != null ? t.m_pMessageTable : null; if (disp != null) disp.Unhandled += OnPacket;
        }
        void Awake() { Instance = this; }
        void OnDestroy()
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            var inv = InventoryWindow.Instance; if (inv != null && ReferenceEquals(inv.Mode, this)) inv.Mode = null;
            if (open && ModalWindow.Current == null) ModalWindow.Overlayed = false;
            if (Instance == this) Instance = null;
        }

        // ============================================================ estado ============================================================
        /// <summary>Vista calculada (panelCombi.*): textos, errores, estadisticas, probabilidad y coste.</summary>
        struct View
        {
            public bool HasLook, HasStat, LookCombined, StatCombined, CannotEquip, LookRed, StatRed;
            public int LookErr, StatErr, Chance, Cost, StatLevel, Single, Multi, Defense;
            public string ChanceStr, LookName, LookDesc, LookLevel, StatType, StatRange;
            public Color ChanceClr, ClrS, ClrM, ClrD;
            public Texture LookIcon, StatIcon;
        }

        bool open, rulesOnly, showRules, waitAnim, sending, succOpen, busyPrev; float waitT, sendT; int npcId;
        int lookSlot = -1, statSlot = -1; string sig = "";
        View v, res;                                                           // v = vista actual, res = copia al enviar (panel de exito)
        string dlg; string[] dlgBtns = { "OK" }; Action dlgYes, dlgAny; Texture dlgI1, dlgI2;
        Texture back; float backRetry;
        // reglas
        struct RuleRow { public int Gap; public float Same, One, Two, Three; public int LookC, StatC; }
        RuleRow[] rules; float ruleScroll; bool ruleDrag;

        // ============================================================ apertura / cierre ============================================================
        /// <summary>StartCombi (rules = false) / StartCombiRule (rules = true).</summary>
        public void Open(int npcId, bool rules)
        {
            if (open) return; if (tables == null) return;
            var inv = InventoryWindow.Instance;
            if (!rules && inv == null) { ChatHud.AddEventString(4, "Combine: inventory not ready."); return; }
            journal = GetComponent<JournalWindow>(); if (journal == null) journal = FindObjectOfType<JournalWindow>();
            this.npcId = npcId; rulesOnly = rules; showRules = false; waitAnim = sending = succOpen = false; dlg = null; lookSlot = statSlot = -1; sig = ""; ruleScroll = 0f; ruleDrag = false;
            BuildRules();
            if (!rules) { inv.ResetOverlays(); inv.EnsureTextures(); inv.Mode = this; }
            Recalc();
            open = true; backRetry = 0f; Sfx.Ui("Open_Screen");
        }

        void Exit(bool sound = true)
        {
            if (!open) return; open = false;
            var inv = InventoryWindow.Instance; if (inv != null) { if (ReferenceEquals(inv.Mode, this)) inv.Mode = null; inv.ResetOverlays(); }
            waitAnim = sending = succOpen = false; dlg = null; lookSlot = statSlot = -1; sig = ""; showRules = false;
            if (ModalWindow.Current == null) ModalWindow.Overlayed = false;
            if (sound) Sfx.Ui("Close_Screen");
        }

        // ============================================================ red ============================================================
        static void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[combi] envio: " + ex.Message); } } }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static ItemBase ReadItem(byte[] b, int o) { return new ItemBase { Type = BitConverter.ToInt16(b, o), Id = BitConverter.ToInt16(b, o + 2), Opt = BitConverter.ToInt32(b, o + 4), TimeLimit = BitConverter.ToInt32(b, o + 8) }; }
        int Taros { get { return hud != null && hud.Data != null ? hud.Data.Taros : 0; } }

        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_REP_PC_ITEM_COMBINATION_SUCC && b.Length >= 36) { var body = b; q.Enqueue(() => OnSucc(body)); }
            else if (type == PacketIds.P_FE2CL_REP_PC_ITEM_COMBINATION_FAIL && b.Length >= 4) { var body = b; q.Enqueue(() => OnFail(body)); }
        }

        /// <summary>CombinationSucc: se aplica siempre (taros e inventario), aunque la ventana ya se haya cerrado.</summary>
        void OnSucc(byte[] b)
        {
            int newSlot = I(b, 0); var ni = ReadItem(b, 4); int statSl = I(b, 16), candy = I(b, 28), flag = I(b, 32);
            if (hud != null && hud.Data != null) hud.Data.Taros = candy;                      // ownstatus.iTaros = iCandy
            var items = InventoryWindow.Items;
            if (flag == 1)
            {
                if (newSlot >= 0 && newSlot < items.Length) items[newSlot] = ni;                // evento (5,16,2): ranura = sNewItem
                if (statSl >= 0 && statSl < items.Length) items[statSl] = default(ItemBase);  // la ranura STATS queda vacia
                if (open) { sending = false; waitAnim = false; succOpen = true; Sfx.Ui("Action_Sucess"); }
            }
            else if (flag == 0)
            {
                if (open)
                {
                    sending = false; waitAnim = false;
                    // iconos de ambos objetos para el mensaje 255 (IconTexture2/3)
                    Texture i1 = res.LookIcon, i2 = res.StatIcon;
                    ShowMsg(255, "The combination failed.", i1, i2, () => { lookSlot = statSlot = -1; Recalc(); });
                }
                if (FailDestroysItems)
                {
                    if (newSlot >= 0 && newSlot < items.Length) items[newSlot] = default(ItemBase);
                    if (statSl >= 0 && statSl < items.Length) items[statSl] = default(ItemBase);
                }
            }
        }

        void OnFail(byte[] b)
        {
            int code = I(b, 0);
            if (!open) return;
            sending = false; waitAnim = false;
            ShowText("Item Combination error. (" + code + ")", null);
        }

        // ============================================================ acciones ============================================================
        bool Blocked { get { return dlg != null || waitAnim || sending || succOpen; } }

        void Attach(int slot, int which)
        {   // AttachCombiItem(slot, 19 = STYLE | 20 = STATS): si estaba en la otra ranura se mueve
            if (rulesOnly || Blocked) return; var items = InventoryWindow.Items; if (slot < 0 || slot >= items.Length || items[slot].Id <= 0) return;
            if (which == 19) { if (statSlot == slot) statSlot = -1; lookSlot = slot; } else { if (lookSlot == slot) lookSlot = -1; statSlot = slot; }
            Sfx.Button(); Recalc();
        }
        void Detach(int which) { if (Blocked) return; if (which == 19) lookSlot = -1; else statSlot = -1; Recalc(); }
        void ClearAll() { if (Blocked) return; lookSlot = statSlot = -1; Recalc(); }

        /// <summary>Combine(): confirmacion (254) si hay taros de sobra, aviso 256 si no.</summary>
        void Combine()
        {
            if (Blocked || !v.HasLook || !v.HasStat || v.LookErr != 0 || v.StatErr != 0) return;
            if (v.Cost <= 0 || Taros >= v.Cost) ShowConfirm(254, "Do you want to combine these items?", StartWait);
            else ShowMsg(256, "You do not have enough Taros to combine these items.", null, null, null);
        }
        void StartWait() { waitAnim = true; waitT = 0f; res = v; }                           // CombiWaiting: animacion del NPC 4 s antes de enviar

        void SendCombine()
        {   // CombiConfirmed
            waitAnim = false;
            if (lookSlot < 0 || statSlot < 0 || InventoryWindow.Items[lookSlot].Id <= 0 || InventoryWindow.Items[statSlot].Id <= 0) { sending = false; return; }
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_ITEM_COMBINATION, 16);
            ClientPackets.PutInt(p, 4, lookSlot); ClientPackets.PutInt(p, 8, statSlot); ClientPackets.PutInt(p, 12, 0); ClientPackets.PutInt(p, 16, 0);
            Send(p); sending = true; sendT = Time.unscaledTime + 12f;
        }

        void CombineMore() { succOpen = false; sending = false; lookSlot = statSlot = -1; Recalc(); }             // CombineMoreItem
        void GoToMyStuff()
        {   // GoToMyStuff: cierra y abre My Stuff
            succOpen = false; sending = false; Exit(false); GameMenu.OpenWindow(GameMenu.Win.MyStuff);
        }

        // ============================================================ IInvMode (panel de inventario) ============================================================
        public bool OnClick(int slot) { return false; }
        public void OnQuick(int slot) { if (Blocked) return; Attach(slot, lookSlot < 0 ? 19 : (statSlot < 0 ? 20 : 20)); }
        public bool OnDrop(int slot, Vector2 wm)
        {
            if (Blocked || slot < 0) return false;
            if (LookRect.Contains(wm) || LookSlotR.Contains(wm)) { Attach(slot, 19); return true; }
            if (StatRect.Contains(wm) || StatSlotR.Contains(wm)) { Attach(slot, 20); return true; }
            return false;
        }
        public void InfoButtons(int slot, Rect ext)
        {
            var red = Skin.Get(Skin.Menu, "RedButton") ?? Skin.Get(Skin.Inven, "RedButton"); bool en = !Blocked;
            if (Skin.Button(new Rect(ext.x + 10f, ext.yMax + 6f, 220f, 28f), Loc.T("USE AS STYLE ITEM"), red, en)) { var inv = InventoryWindow.Instance; if (inv != null) inv.CloseInfoPub(); Attach(slot, 19); }
            if (Skin.Button(new Rect(ext.x + 10f, ext.yMax + 40f, 220f, 28f), Loc.T("USE AS STATS ITEM"), red, en)) { var inv = InventoryWindow.Instance; if (inv != null) inv.CloseInfoPub(); Attach(slot, 20); }
        }

        // ============================================================ tablas ============================================================
        ItemElement Row(int type, int id)
        {
            if (type < 0 || type > 3 || id <= 0) return null; var t = ItemTbl.Eq(tables, type);
            return t != null && t.m_pItemData != null && id < t.m_pItemData.Length ? t.m_pItemData[id] : null;
        }
        void Names(int type, ItemElement r, out string name, out string desc)
        {
            name = desc = ""; var t = ItemTbl.Eq(tables, type);
            if (t != null && t.m_pItemStringData != null && r != null && r.m_iItemName >= 0 && r.m_iItemName < t.m_pItemStringData.Length && t.m_pItemStringData[r.m_iItemName] != null)
            { name = t.m_pItemStringData[r.m_iItemName].m_strName ?? ""; desc = t.m_pItemStringData[r.m_iItemName].m_strComment ?? ""; }
        }
        Texture Icon(int type, int id) { return journal != null && id > 0 ? journal.ItemTexture(type, id) : null; }

        static View Fresh(string chance, int level, Color clr)
        {
            return new View { Chance = level, ChanceStr = chance, ChanceClr = clr, ClrS = Color.white, ClrM = Color.white, ClrD = Color.white, LookName = "", LookDesc = "", LookLevel = "", StatType = "", StatRange = "" };
        }

        /// <summary>ReceiveSetItemAttached: errores por ranura, estadisticas, probabilidad de exito y coste.</summary>
        void Recalc()
        {
            var inv = InventoryWindow.Instance; var items = InventoryWindow.Items;
            int gender = inv != null && inv.SelfApp != null ? (int)inv.SelfApp.Style.Gender : 0;
            var n = Fresh("NOT\nREADY", -1, ClrReady);
            n.HasLook = lookSlot >= 0 && lookSlot < items.Length && items[lookSlot].Id > 0; n.HasStat = statSlot >= 0 && statSlot < items.Length && items[statSlot].Id > 0;
            if (!n.HasLook) lookSlot = -1; if (!n.HasStat) statSlot = -1;
            ItemBase li = n.HasLook ? items[lookSlot] : default(ItemBase), si = n.HasStat ? items[statSlot] : default(ItemBase);
            ItemElement lv = null, sv = null;

            if (n.HasLook)
            {
                int lid = 0;
                if (li.Type >= 0 && li.Type < 4)
                {
                    lid = (li.Opt >> 16) & 0xFFFF; var own = Row(li.Type, li.Id); lv = lid > 0 ? Row(li.Type, lid) : own;
                    string nm, ds; Names(li.Type, lv, out nm, out ds); n.LookName = nm; n.LookDesc = ds; n.LookLevel = Loc.T("LEVEL ") + (own != null ? own.m_iMinReqLev : 0);
                    n.LookErr = lv != null && lv.m_iReqSex > 0 && lv.m_iReqSex != gender ? 1 : 0;
                }
                else n.LookErr = 3;
                n.LookCombined = lid > 0; n.LookIcon = Icon(li.Type, lid > 0 ? lid : li.Id); n.LookRed = inv != null && !inv.CanEquip(li.Type, li.Id);
            }
            if (n.HasStat)
            {
                int sid = 0;
                if (si.Type >= 0 && si.Type < 4)
                {
                    sid = (si.Opt >> 16) & 0xFFFF; var look2 = sid > 0 ? Row(si.Type, sid) : null; sv = Row(si.Type, si.Id);
                    switch (si.Type)
                    {
                        case 0: n.StatType = Loc.T("Weapon"); break;
                        case 1: n.StatType = Loc.T("Body"); break;
                        case 2: n.StatType = Loc.T("Legs"); break;
                        default: n.StatType = Loc.T("Shoes"); break;
                    }
                    if ((look2 != null && look2.m_iMentor > 0) || (sv != null && sv.m_iMentor > 0)) n.StatErr = 1; else if (sv != null && sv.m_iCashAble > 0) n.StatErr = 2; else n.StatErr = 0;
                    if (sv != null)
                    {
                        n.StatLevel = sv.m_iMinReqLev; n.Single = sv.m_iPointRat; n.Multi = sv.m_iGroupRat; n.Defense = sv.m_iDefenseRat;
                        var eq = inv != null ? inv.EquippedAt(si.Type) : default(ItemBase); var er = eq.Id > 0 ? Row(si.Type, eq.Id) : null;   // objeto equipado del mismo tipo
                        if (er != null)
                        {
                            n.ClrS = sv.m_iPointRat > er.m_iPointRat ? Color.green : sv.m_iPointRat < er.m_iPointRat ? new Color(0.9f, 0.2f, 0f) : Color.white;
                            n.ClrM = sv.m_iGroupRat > er.m_iGroupRat ? Color.green : sv.m_iGroupRat < er.m_iGroupRat ? new Color(0.9f, 0.2f, 0f) : Color.white;
                            n.ClrD = sv.m_iDefenseRat > er.m_iDefenseRat ? Color.green : sv.m_iDefenseRat < er.m_iDefenseRat ? new Color(0.9f, 0.2f, 0f) : Color.white;
                        }
                        if (si.Type == 0)
                        {
                            n.StatRange = sv.m_iDelayTime > 0 ? (1f / (sv.m_iDelayTime * 0.1f)).ToString("0.00") : "";
                            switch (sv.m_iEquipType)
                            {
                                case 0: case 1: case 2: case 3: case 7: case 8: n.StatRange = Loc.T("Short"); break;
                                case 5: case 6: case 9: case 10: n.StatRange = Loc.T("Medium"); break;
                                case 4: case 11: n.StatRange = Loc.T("Long"); break;
                            }
                        }
                        else n.StatRange = Loc.T("N/A");
                    }
                }
                else n.StatErr = 3;
                n.StatCombined = sid > 0; n.StatIcon = Icon(si.Type, sid > 0 ? sid : si.Id); n.StatRed = inv != null && !inv.CanEquip(si.Type, si.Id);
            }

            if (n.HasLook && n.HasStat)
            {
                if (li.Type >= 0 && li.Type < 4 && si.Type >= 0 && si.Type < 4)
                {
                    bool ok = false;
                    if (li.Type == si.Type) { if (li.Type == 0) ok = lv != null && sv != null && lv.m_iTargetMode == sv.m_iTargetMode; else ok = lv != null && sv != null; }
                    if (ok)
                    {
                        n.CannotEquip = inv != null && !inv.CanEquip(si.Type, si.Id);
                        int gap = Math.Abs(lv.m_iMinReqLev - sv.m_iMinReqLev) + 1, rar = Math.Abs(lv.m_iRarity - sv.m_iRarity);
                        var ct = tables.m_pCombiningTable; CombiningElement ce = ct != null && ct.m_pCombiningData != null && ct.m_pCombiningData.Length > 0 ? ct.m_pCombiningData[Mathf.Clamp(gap, 0, ct.m_pCombiningData.Length - 1)] : null;
                        float c = 0f;
                        if (ce != null)
                        {
                            n.Cost = sv.m_iItemPrice * ce.m_iStatConstant + lv.m_iItemPrice * ce.m_iLookConstant;
                            c = rar == 0 ? ce.m_fSameGrade : rar == 1 ? ce.m_fOneGrade : rar == 2 ? ce.m_fTwoGrade : rar == 3 ? ce.m_fThreeGrade : 0f;
                        }
                        else Debug.LogWarning("[combi] sin CombiningTable: coste 0");
                        if (c < 10f) { n.ChanceStr = "VERY\nLOW"; n.Chance = 1; n.ChanceClr = new Color(1f, 0f, 0f); }
                        else if (c < 30f) { n.ChanceStr = "LOW"; n.Chance = 2; n.ChanceClr = new Color(0.8f, 0.6f, 0f); }
                        else if (c < 70f) { n.ChanceStr = "MEDIUM"; n.Chance = 3; n.ChanceClr = new Color(1f, 1f, 0f); }
                        else if (c < 90f) { n.ChanceStr = "GOOD"; n.Chance = 4; n.ChanceClr = new Color(0.2f, 1f, 0f); }
                        else { n.ChanceStr = "VERY\nGOOD"; n.Chance = 5; n.ChanceClr = new Color(0f, 0.8f, 0.2f); }
                    }
                    else { n.LookErr = 4; n.StatErr = 4; n.ChanceStr = "NOT\nPOSSIBLE"; n.Chance = 0; n.Cost = 0; n.ChanceClr = ClrBad; }
                }
            }
            else { n.ChanceStr = ""; n.Chance = 0; n.Cost = 0; n.ChanceClr = ClrBad; }

            if (!n.HasStat && !n.HasLook) { n.Chance = -1; n.ChanceClr = ClrReady; n.ChanceStr = "NOT\nREADY"; n.Cost = 0; }
            else if (n.HasStat && !n.HasLook) { if (n.StatErr != 0) { n.ChanceStr = "NOT\nPOSSIBLE"; n.Chance = 0; n.Cost = 0; n.ChanceClr = ClrBad; } else { n.Chance = -1; n.ChanceClr = ClrReady; n.ChanceStr = "NOT\nREADY"; n.Cost = 0; } }
            else if (n.HasLook && !n.HasStat) { if (n.LookErr != 0) { n.ChanceStr = "NOT\nPOSSIBLE"; n.Chance = 0; n.Cost = 0; n.ChanceClr = ClrBad; } else { n.Chance = -1; n.ChanceClr = ClrReady; n.ChanceStr = "NOT\nREADY"; n.Cost = 0; } }
            else if (n.LookErr != 0 || n.StatErr != 0) { n.ChanceStr = "NOT\nPOSSIBLE"; n.Chance = 0; n.Cost = 0; n.ChanceClr = ClrBad; }
            v = n; sig = Signature();
        }

        string Signature()
        {
            var it = InventoryWindow.Items; string s = lookSlot + "," + statSlot;
            if (lookSlot >= 0 && lookSlot < it.Length) s += "|" + it[lookSlot].Type + "." + it[lookSlot].Id + "." + it[lookSlot].Opt;
            if (statSlot >= 0 && statSlot < it.Length) s += "|" + it[statSlot].Type + "." + it[statSlot].Id + "." + it[statSlot].Opt;
            var inv = InventoryWindow.Instance; if (inv != null) { s += "|" + (hud != null && hud.Data != null ? hud.Data.Level : 0); for (int k = 0; k < 4; k++) s += "." + inv.EquippedAt(k).Id; }
            return s;
        }

        void BuildRules()
        {
            rules = new RuleRow[0]; var ct = tables != null ? tables.m_pCombiningTable : null; if (ct == null || ct.m_pCombiningData == null) return;
            var l = new System.Collections.Generic.List<RuleRow>();
            for (int i = 1; i < ct.m_pCombiningData.Length; i++)
            {   // fila i = salto de nivel i - 1 (el cliente usa |dNivel| + 1 como indice)
                var r = ct.m_pCombiningData[i]; if (r == null) continue;
                if (r.m_fSameGrade == 0f && r.m_fOneGrade == 0f && r.m_fTwoGrade == 0f && r.m_fThreeGrade == 0f && r.m_iLookConstant == 0 && r.m_iStatConstant == 0) continue;
                l.Add(new RuleRow { Gap = i - 1, Same = r.m_fSameGrade, One = r.m_fOneGrade, Two = r.m_fTwoGrade, Three = r.m_fThreeGrade, LookC = r.m_iLookConstant, StatC = r.m_iStatConstant });
            }
            rules = l.ToArray();
        }

        // ============================================================ mensajes (cnSystemMessageManager) ============================================================
        string MsgText(int id, string fallback, out int btn)
        {
            btn = 1;
            if (msgT != null && msgT.m_pMessageData != null && id >= 0 && id < msgT.m_pMessageData.Length && msgT.m_pMessageData[id] != null && !string.IsNullOrEmpty(msgT.m_pMessageData[id].m_szString))
            { btn = msgT.m_pMessageData[id].m_iButtonType; return msgT.m_pMessageData[id].m_szString; }
            return fallback;
        }
        void ShowMsg(int id, string fallback, Texture i1, Texture i2, Action then)
        {
            int bt; string s = MsgText(id, fallback, out bt); int p = s.IndexOf("%s"); if (p >= 0) s = s.Substring(0, p) + v.Cost + s.Substring(p + 2);
            dlg = Loc.T(s); dlgBtns = new[] { "OK" }; dlgYes = null; dlgAny = then; dlgI1 = i1; dlgI2 = i2; Sfx.Ui("Action_Failure01");
        }
        void ShowConfirm(int id, string fallback, Action yes)
        {
            int bt; string s = MsgText(id, fallback, out bt); int p = s.IndexOf("%s"); if (p >= 0) s = s.Substring(0, p) + v.Cost + s.Substring(p + 2);
            dlg = Loc.T(s); dlgBtns = bt == 3 ? new[] { "YES", "NO" } : new[] { "OK", "CANCEL" }; dlgYes = yes; dlgAny = null; dlgI1 = dlgI2 = null; Sfx.Ui("Open_Screen");
        }
        void ShowText(string s, Action then) { dlg = Loc.T(s); dlgBtns = new[] { "OK" }; dlgYes = null; dlgAny = then; dlgI1 = dlgI2 = null; Sfx.Ui("Action_Failure01"); }

        // ============================================================ bucle ============================================================
        void Update()
        {
            Action a; int guard = 0; while (guard++ < 64 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[combi] " + ex.Message); } }
            if (!open) return;
            if (ModalWindow.Current == null) ModalWindow.Overlayed = true;      // respaldo: busyPrev de GameMenu ya ignora ESC/atajos con ModalWindow.Overlayed
            if (waitAnim) { waitT += Time.unscaledDeltaTime; if (waitT > 4f) SendCombine(); }
            if (sending && Time.unscaledTime > sendT) { sending = false; ShowText("Item Combination error. (no response)", null); }
            if (!rulesOnly && !succOpen && !sending && !waitAnim && Signature() != sig) Recalc();   // el inventario cambio (objeto movido, nivel, equipo)
            if (Input.GetKeyDown(KeyCode.Escape) && !busyPrev && dlg == null && !waitAnim && !sending && !succOpen)
            {   // boton Atras de Android / ConfigurableInput Key 4
                Sfx.Button(); if (showRules && !rulesOnly) showRules = false; else Exit();
            }
        }
        void LateUpdate() { busyPrev = InventoryWindow.Busy; }

        // ============================================================ dibujo ============================================================
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        static Color Mul(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }

        void LoadBack()
        {
            if (back != null || Time.unscaledTime < backRetry) return; backRetry = Time.unscaledTime + 2f;
            if (TexLoader != null) foreach (var ext in new[] { ".png", ".psd", ".dds", ".tga", "" }) { try { back = TexLoader("TutorialAssets/panelback" + ext); } catch (Exception) { back = null; } if (back != null) break; }
            if (back == null) back = UiKit.Tex("panelback");
        }

        /// <summary>Texto con un estilo del GUISkin 1367 (fuente de mapa de bits); tint = GUI.color del original (se multiplica por el color del estilo).</summary>
        static void T(Rect r, string text, string style, Color? tint = null, bool wrap = false)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            var st = Skin.Get(SK, style); if (st == null || st.Font == null) return;
            st.Font.Draw(r, text, st.Align, wrap || st.Wrap, tint.HasValue ? Mul(st.cN, tint.Value) : st.cN, st.Padding);
        }
        static void Bg(Rect r, string style) { var st = Skin.Get(SK, style); if (st != null) Skin.Box(r, st); }

        void OnGUI()
        {
            if (!open || Screen.height <= 0) return; var e = Event.current; if (e == null) return;
            if (back == null) LoadBack();
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f);
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; var oe = GUI.enabled; GUI.depth = -1500; GUI.color = Color.white; GUI.enabled = true;
            try
            {
                GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
                if (e.type == EventType.Repaint)
                {
                    GUI.color = new Color(0f, 0f, 0f, back != null ? 1f : 0.75f); GUI.DrawTexture(new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc), Texture2D.whiteTexture); GUI.color = Color.white;
                    if (back != null) GUI.DrawTexture(new Rect(-960f, -720f, 1920f, 1440f), back, ScaleMode.ScaleToFit);
                }
                GUI.BeginGroup(new Rect(-WindowRect.width * 0.5f, -WindowRect.height * 0.5f, WindowRect.width, WindowRect.height));
                var inv = InventoryWindow.Instance; bool en = !Blocked && !InventoryWindow.Busy;
                if (rulesOnly)
                {
                    GUI.BeginGroup(new Rect((WindowRect.width - Frame.width) * 0.5f, 0f, Frame.width, WindowRect.height));
                    DrawRules(e, en, true);
                    GUI.EndGroup();
                }
                else
                {
                    if (showRules) DrawRules(e, en, false); else DrawCombi(e, en);
                    // X y "?" del panel de inventario: se interceptan antes (GameMenu.CloseWindow / Guide no aplican a esta ventana)
                    if (Skin.Button(PanelClose, "", Skin.Get(Skin.Inven, "closebut"), en)) { Exit(); GUI.EndGroup(); return; }
                    if (Skin.Button(PanelHelp, "", Skin.Get(Skin.Inven, "helpbut"), en)) { showRules = !showRules; ruleScroll = 0f; }
                    if (inv != null) inv.DrawPanel(e, !Blocked, true);
                    if (waitAnim) DrawWaiting(e);
                    if (succOpen) DrawSucc(e);
                }
                if (dlg != null) DrawDlg(e);
                GUI.EndGroup();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();    // la ventana es modal
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; GUI.enabled = oe; }
        }

        // ---- pantalla de combinacion (cnGuiCombi.DoCombiWnd) ----
        void DrawCombi(Event e, bool en)
        {
            bool rep = e.type == EventType.Repaint;
            if (rep) { var f = UiKit.Tex("CombiWndBG"); if (f != null) GUI.DrawTexture(Frame, f, ScaleMode.ScaleToFit); else UiKit.Fill(Frame, new Color(0.01f, 0.06f, 0.14f, 0.92f)); }
            UiKit.Img(new Rect(395f, 20f, 80f, 80f), UiKit.Tex("CombiIcon2"), ScaleMode.ScaleToFit);                       // retrato del NPC (cNPC) -> icono de Croc Pot
            T(new Rect(28, 19, 299, 16), Loc.T("Croc Pot Catering Company"), "Jeff12LightBlue");
            T(new Rect(28, 36, 327, 49), Loc.T("Select one item for the STYLE slot and one item for the STATS slot. We'll display your chance for a successful combination."), "Cha12Yellow", null, true);
            T(new Rect(52, 154, 55, 11), Loc.T("STYLE"), "Jeff8LightBlue");
            T(new Rect(52, 340, 55, 11), Loc.T("STATS"), "Jeff8LightBlue");
            T(new Rect(48, 518, 55, 11), v.Cost.ToString(), "Jeff8LightBlue");
            T(new Rect(48, 537, 55, 11), Loc.T("TAROS"), "Jeff8LightBlue");
            Bg(new Rect(106, 532, 23, 24), "TarosIcon");
            T(new Rect(81, 581, 69, 30), Loc.T("CHANCE OF SUCCESS:"), "Jeff6LightBlue", null, true);
            T(new Rect(75, 603, 80, 28), Loc.T(v.ChanceStr), "Jeff8LightBlue", v.ChanceClr);
            T(new Rect(39, 595, 25, 25), v.Chance == -1 ? "?" : v.Chance.ToString(), "Jeff24", v.ChanceClr);
            T(new Rect(259, 151, 120, 10), Loc.T("NEW COMBINED ITEM"), "Jeff6Blue");

            // zona STYLE
            if (v.HasLook)
            {
                Bg(LookRect, "LookItemBG");
                if (v.CannotEquip) { var rs = Skin.Get(Skin.Inven, "redItemBox"); if (rs != null) Skin.Bg(new Rect(191, 180, 60, 60), rs.N, rs); }
                if (rep && v.LookIcon != null) GUI.DrawTexture(new Rect(191, 180, 60, 60), v.LookIcon, ScaleMode.ScaleToFit);
                Bg(new Rect(225, 215, 25, 25), "CIcon");
                T(new Rect(258, 179, 201, 12), v.LookName, "Jeff8LightBlueLeft");
                T(new Rect(180, 250, 285, 36), v.LookDesc, "Cha10SkyBlue", null, true);
                T(new Rect(258, 214, 81, 14), v.LookLevel, "Jeff8LightBlueLeft", new Color(1f, 1f, 0f, 0.3f));
                if (v.LookErr != 0)
                {
                    Bg(new Rect(168, 169, 303, 131), "GenderError");
                    if (v.LookErr == 1) T(new Rect(245, 221, 152, 24), Loc.T("DOES NOT MATCH PLAYER GENDER"), "Jeff8LightBlue", null, true);
                    else if (v.LookErr == 3) T(new Rect(215, 221, 202, 24), Loc.T("ONLY WEAPONS OR ARMOR CAN BE COMBINED"), "Jeff8LightBlue", null, true);
                    else if (v.LookErr == 4) T(new Rect(245, 221, 152, 24), Loc.T("DOES NOT MATCH STATS ITEM TYPE"), "Jeff8LightBlue", null, true);
                }
            }
            else T(new Rect(233, 203, 176, 50), Loc.T("Place an item here to choose what your new combined item will LOOK like."), "Jeff8LightBlue", null, true);

            // zona STATS
            if (v.HasStat)
            {
                Bg(StatRect, "StatItemBG");
                T(new Rect(280, 328, 81, 14), Loc.T("LEVEL") + " " + v.StatLevel, "Jeff8LightBlue", Color.yellow);
                T(new Rect(175, 352, 51, 13), Loc.T("STATS"), "Jeff8LightBlueLeft");
                T(new Rect(200, 430, 59, 14), v.Single.ToString(), "Jeff8LightBlue", v.ClrS);
                T(new Rect(291, 430, 59, 14), v.Multi.ToString(), "Jeff8LightBlue", v.ClrM);
                T(new Rect(383, 430, 59, 14), v.Defense.ToString(), "Jeff8LightBlue", v.ClrD);
                T(new Rect(175, 453, 42, 13), Loc.T("INFO"), "Jeff8LightBlueLeft");
                T(new Rect(274, 470, 27, 14), Loc.T("Type"), "Cha10SkyBlue");
                T(new Rect(267, 491, 34, 14), Loc.T("Range"), "Cha10SkyBlue");
                T(new Rect(269, 512, 32, 14), Loc.T("Rarity"), "Cha10SkyBlue");
                T(new Rect(213, 532, 88, 14), Loc.T("Trade Availability"), "Cha10SkyBlue");
                T(new Rect(335, 470, 115, 14), v.StatType, "Cha10LightBlue");
                T(new Rect(335, 491, 115, 14), v.StatRange, "Cha10LightBlue");
                T(new Rect(335, 512, 115, 14), Loc.T("Special"), "Cha10LightBlue");
                T(new Rect(335, 532, 115, 14), Loc.T("Non-tradable"), "Cha10LightBlue");
                if (v.StatErr != 0)
                {
                    Bg(new Rect(168, 323, 304, 228), "StatItemErrorBG");
                    if (v.StatErr == 1) T(new Rect(245, 423, 152, 24), Loc.T("CANNOT PLACE GUIDE ITEM HERE"), "Jeff8LightBlue", null, true);
                    else if (v.StatErr == 2) T(new Rect(245, 423, 152, 24), Loc.T("CANNOT PLACE SHOP ITEM HERE"), "Jeff8LightBlue", null, true);
                    else if (v.StatErr == 3) T(new Rect(215, 423, 202, 24), Loc.T("ONLY WEAPONS OR ARMOR CAN BE COMBINED"), "Jeff8LightBlue", null, true);
                    else if (v.StatErr == 4) T(new Rect(245, 423, 152, 24), Loc.T("DOES NOT MATCH STYLE ITEM TYPE"), "Jeff8LightBlue", null, true);
                }
            }
            else T(new Rect(233, 396, 163, 45), Loc.T("Place an item here to choose your new combined item's STATS and LEVEL."), "Jeff8LightBlue", null, true);

            // botones
            var btn = Skin.Get(SK, "button");
            if (Skin.Button(new Rect(180, 579, 104, 20), Loc.T("CLEAR ALL"), btn, en && (v.HasLook || v.HasStat))) ClearAll();
            if (Skin.Button(new Rect(327, 576, 131, 24), Loc.T("COMBINE"), btn, en && v.HasLook && v.HasStat && v.LookErr == 0 && v.StatErr == 0)) Combine();

            // ranuras pequenas STYLE / STATS (clic = quitar)
            DrawSlot(LookSlotR, v.HasLook, v.LookIcon, v.LookCombined, v.LookRed, new Rect(83, 212, 26, 26));
            DrawSlot(StatSlotR, v.HasStat, v.StatIcon, v.StatCombined, v.StatRed, new Rect(82, 399, 26, 26));
            if (en) { if (v.HasLook && Btn(LookSlotR)) Detach(19); if (v.HasStat && Btn(StatSlotR)) Detach(20); }
        }
        static bool Btn(Rect r) { return GUI.Button(r, GUIContent.none, GUIStyle.none); }

        void DrawSlot(Rect r, bool has, Texture icon, bool combined, bool red, Rect cicon)
        {
            if (Event.current.type != EventType.Repaint) return;
            var slot = Skin.Get(Skin.Inven, "slotbox"); var empty = Skin.Get(Skin.Inven, "slotboxempty"); var rs = Skin.Get(Skin.Inven, "redItemBox");
            var st = has ? (red && rs != null ? rs : slot) : empty; if (st != null) Skin.Bg(r, st.N != null ? st.N : (slot != null ? slot.N : null), st);
            if (has && icon != null) GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), icon, ScaleMode.ScaleToFit);
            if (has && combined) Bg(cicon, "CIcon");
        }

        // ---- espera de 4 s (DoCombiWaiting) ----
        void DrawWaiting(Event e)
        {
            if (e.type != EventType.Repaint) return;
            var sh = UiKit.Tex("CombiBlackShade"); if (sh != null) UiKit.Img(WindowRect, sh, new Color(1f, 1f, 1f, 0.75f)); else UiKit.Fill(WindowRect, new Color(0f, 0f, 0f, 0.75f));
            var box = new Rect((WindowRect.width - 357f) * 0.5f, (WindowRect.height - 384f) * 0.5f, 357f, 384f);
            var st = Skin.Get(SK, "CombiWaitingBG"); if (st != null) Skin.Bg(box, st.N, st); else UiKit.Fill(box, new Color(0.02f, 0.1f, 0.2f, 0.97f));
            float p = Mathf.Clamp01(waitT / 4f); var bar = new Rect(box.x + 40f, box.yMax - 50f, box.width - 80f, 12f);
            UiKit.Fill(bar, new Color(0f, 0.05f, 0.12f, 0.9f)); UiKit.Fill(new Rect(bar.x, bar.y, bar.width * p, bar.height), new Color(0.2f, 0.8f, 1f, 0.95f)); UiKit.Outline(bar, new Color(0f, 0.8f, 0.9f, 1f));
            float a = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
            T(new Rect(box.x, box.yMax - 90f, box.width, 24f), Loc.T("COMBINING..."), "Jeff12LightBlue", new Color(1f, 1f, 1f, a));
        }

        // ---- exito (DoCombiSucc): panel 352x550 centrado ----
        void DrawSucc(Event e)
        {
            bool rep = e.type == EventType.Repaint;
            if (rep) { var sh = UiKit.Tex("CombiBlackShade"); if (sh != null) UiKit.Img(WindowRect, sh, new Color(1f, 1f, 1f, 0.75f)); else UiKit.Fill(WindowRect, new Color(0f, 0f, 0f, 0.75f)); }
            var box = new Rect((WindowRect.width - 352f) * 0.5f, (WindowRect.height - 550f) * 0.5f, 352f, 550f);
            var st = Skin.Get(SK, "CombiSucc"); if (rep) { if (st != null) Skin.Bg(box, st.N, st); else UiKit.Fill(box, new Color(0.02f, 0.1f, 0.2f, 0.97f)); }
            GUI.BeginGroup(box);
            Bg(new Rect(10, 10, 64, 64), "CombiNpc");
            T(new Rect(80, 7, 98, 18), Loc.T("HOORAY!"), "Jeff12SkyBlue");
            T(new Rect(80, 28, 250, 36), Loc.T("The combination was a success! Your new item is ready and piping hot."), "Cha10LightBlueLeft", null, true);
            if (res.CannotEquip) { var rs = Skin.Get(Skin.Inven, "redItemBox"); if (rs != null) Skin.Bg(new Rect(38, 102, 60, 60), rs.N, rs); }
            if (rep && res.LookIcon != null) GUI.DrawTexture(new Rect(38, 102, 60, 60), res.LookIcon, ScaleMode.ScaleToFit);
            Bg(new Rect(70, 136, 25, 25), "CIcon");
            T(new Rect(106, 101, 201, 25), res.LookName, "Jeff8LightBlueLeft");
            T(new Rect(106, 138, 81, 14), Loc.T("LEVEL ") + res.StatLevel, "Jeff8LightBlueLeft", Color.yellow);
            T(new Rect(32, 180, 285, 40), res.LookDesc, "Cha10SkyBlue", null, true);
            T(new Rect(58, 305, 59, 14), res.Single.ToString(), "Jeff8LightBlue", res.ClrS);
            T(new Rect(146, 305, 59, 14), res.Multi.ToString(), "Jeff8LightBlue", res.ClrM);
            T(new Rect(236, 305, 59, 14), res.Defense.ToString(), "Jeff8LightBlue", res.ClrD);
            T(new Rect(127, 348, 27, 14), Loc.T("Type"), "Cha10SkyBlue");
            T(new Rect(120, 368, 34, 14), Loc.T("Range"), "Cha10SkyBlue");
            T(new Rect(122, 389, 32, 14), Loc.T("Rarity"), "Cha10SkyBlue");
            T(new Rect(66, 408, 88, 14), Loc.T("Trade Availability"), "Cha10SkyBlue");
            T(new Rect(168, 348, 115, 14), res.StatType, "Cha10LightBlue");
            T(new Rect(168, 368, 115, 14), res.StatRange, "Cha10LightBlue");
            T(new Rect(168, 388, 115, 14), Loc.T("Special"), "Cha10LightBlue");
            T(new Rect(168, 408, 115, 14), Loc.T("Non-tradable"), "Cha10LightBlue");
            var btn = Skin.Get(SK, "button"); bool on = dlg == null;
            if (Skin.Button(new Rect(70, 478, 210, 25), Loc.T("COMBINE MORE ITEMS"), btn, on)) { CombineMore(); GUI.EndGroup(); return; }
            if (Skin.Button(new Rect(70, 513, 210, 25), Loc.T("GO TO MY STUFF"), btn, on)) { GoToMyStuff(); GUI.EndGroup(); return; }
            GUI.EndGroup();
        }

        // ---- reglas (pagina informativa; el original no dibuja nada en GUIMode_Rule) ----
        static readonly string[] RuleText = {
            "Only weapons and armor (body, legs and shoes) can be combined.",
            "The STYLE item must be the same type as the STATS item. Weapons must also have the same attack type.",
            "The STYLE item must match your gender.",
            "Guide items and shop items cannot be used as the STATS item.",
            "The new item keeps the stats and level of the STATS item and takes on the look of the STYLE item." };

        void DrawRules(Event e, bool en, bool standalone)
        {
            bool rep = e.type == EventType.Repaint; var btn = Skin.Get(SK, "button");
            if (rep) { var f = UiKit.Tex("CombiWndBG"); if (f != null) GUI.DrawTexture(Frame, f, ScaleMode.ScaleToFit); else UiKit.Fill(Frame, new Color(0.01f, 0.06f, 0.14f, 0.92f)); }
            UiKit.Img(new Rect(395f, 20f, 80f, 80f), UiKit.Tex("CombiIcon2"), ScaleMode.ScaleToFit);
            T(new Rect(28, 19, 299, 16), Loc.T("Croc Pot Catering Company"), "Jeff12LightBlue");
            T(new Rect(28, 42, 299, 18), Loc.T("COMBINE RULES"), "Jeff12SkyBlue");
            var panel = new Rect(16, 104, 553, 462); UiKit.Fill(panel, new Color(0.01f, 0.05f, 0.12f, 0.96f)); UiKit.Outline(panel, new Color(0f, 0.6f, 0.75f, 0.9f), 2f);
            T(new Rect(30, 112, 525, 34), Loc.T("Combine one STYLE item and one STATS item into a single new item that looks like the first and performs like the second."), "Cha12Yellow", null, true);
            for (int i = 0; i < RuleText.Length; i++) T(new Rect(36, 154 + 30 * i, 515, 28), "- " + Loc.T(RuleText[i]), "Cha10LightBlueLeft", null, true);
            T(new Rect(30, 306, 525, 30), Loc.T("Cost = (STATS item price x STATS value) + (STYLE item price x STYLE value). Chance of success depends on the level gap and the rarity difference between both items."), "Cha10SkyBlue", null, true);

            // cabecera de la tabla CombiningTable
            float[] cx = { 62f, 150f, 236f, 322f, 408f, 468f, 528f };
            string[] hd = { "LEVEL GAP", "SAME RARITY", "1 GRADE", "2 GRADES", "3 GRADES", "STYLE X", "STATS X" };
            for (int i = 0; i < hd.Length; i++) T(new Rect(cx[i] - 42f, 342, 84, 14), Loc.T(hd[i]), "Jeff6LightBlue");
            var vp = new Rect(24, 360, 537, 200); const float rh = 22f;
            int n = rules != null ? rules.Length : 0; float content = n * rh, max = Mathf.Max(0f, content - vp.height); ruleScroll = Mathf.Clamp(ruleScroll, 0f, max);
            Vector2 m = e.mousePosition;
            if (en)
            {
                if (e.type == EventType.ScrollWheel && vp.Contains(m)) { ruleScroll = Mathf.Clamp(ruleScroll + e.delta.y * rh, 0f, max); e.Use(); }
                else if (e.type == EventType.MouseDown && e.button == 0 && vp.Contains(m)) { ruleDrag = true; e.Use(); }
                else if (e.type == EventType.MouseDrag && ruleDrag) { ruleScroll = Mathf.Clamp(ruleScroll - e.delta.y, 0f, max); e.Use(); }
                else if (e.type == EventType.MouseUp) ruleDrag = false;
            }
            if (n == 0) T(new Rect(vp.x, vp.y + 80f, vp.width, 20f), Loc.T("No combination data available."), "Jeff8LightBlue");
            GUI.BeginGroup(vp);
            for (int k = 0; k < n; k++)
            {
                float y = k * rh - ruleScroll; if (y + rh < 0f || y > vp.height) continue; var r = rules[k];
                if (rep && (k & 1) == 0) UiKit.Fill(new Rect(0, y, vp.width, rh - 2f), new Color(0.05f, 0.15f, 0.28f, 0.45f));
                float ox = vp.x;
                T(new Rect(cx[0] - ox - 42f, y + 3f, 84, 14), r.Gap.ToString(), "Jeff8LightBlue");
                T(new Rect(cx[1] - ox - 42f, y + 3f, 84, 14), Pct(r.Same), "Jeff8LightBlue", ChanceColor(r.Same));
                T(new Rect(cx[2] - ox - 42f, y + 3f, 84, 14), Pct(r.One), "Jeff8LightBlue", ChanceColor(r.One));
                T(new Rect(cx[3] - ox - 42f, y + 3f, 84, 14), Pct(r.Two), "Jeff8LightBlue", ChanceColor(r.Two));
                T(new Rect(cx[4] - ox - 42f, y + 3f, 84, 14), Pct(r.Three), "Jeff8LightBlue", ChanceColor(r.Three));
                T(new Rect(cx[5] - ox - 30f, y + 3f, 60, 14), r.LookC.ToString(), "Jeff8LightBlue");
                T(new Rect(cx[6] - ox - 30f, y + 3f, 60, 14), r.StatC.ToString(), "Jeff8LightBlue");
            }
            GUI.EndGroup();
            if (max > 0f && rep)
            {   // barra de desplazamiento simple
                var tr = new Rect(vp.xMax - 6f, vp.y, 4f, vp.height); UiKit.Fill(tr, new Color(0f, 0.1f, 0.2f, 0.8f));
                float th = Mathf.Max(16f, vp.height * vp.height / content); UiKit.Fill(new Rect(tr.x, tr.y + (tr.height - th) * (ruleScroll / max), tr.width, th), new Color(0f, 0.8f, 0.9f, 0.9f));
            }
            if (Skin.Button(new Rect(240, 574, 110, 24), Loc.T(standalone ? "CLOSE" : "BACK"), btn, en)) { if (standalone) Exit(); else showRules = false; }
            if (standalone && Skin.Button(new Rect(Frame.width - 42f, 10f, 30f, 30f), "", Skin.Get(Skin.Inven, "closebut"), en)) Exit();
        }
        static string Pct(float f) { return f.ToString("0.#") + "%"; }
        static Color ChanceColor(float c)
        {
            if (c < 10f) return new Color(1f, 0.3f, 0.3f); if (c < 30f) return new Color(1f, 0.75f, 0.2f); if (c < 70f) return new Color(1f, 1f, 0.3f); if (c < 90f) return new Color(0.4f, 1f, 0.3f); return new Color(0.2f, 1f, 0.5f);
        }

        // ---- caja de mensaje (cnSystemMessageManager) ----
        void DrawDlg(Event e)
        {
            bool rep = e.type == EventType.Repaint; var oc = GUI.color;
            if (rep) { GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = oc; }
            var r = new Rect(300f, 220f, 436f, 200f); if (rep) { GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = oc; }
            bool icons = dlgI1 != null || dlgI2 != null;
            var lab = Skin.Get(Skin.Inven, "label"); if (lab != null) Skin.Label(new Rect(r.x + 15f, r.y + 15f, r.width - 30f, icons ? 70f : 120f), dlg, lab, Color.white);
            if (rep && icons)
            {
                if (dlgI1 != null) GUI.DrawTexture(new Rect(r.x + 120f, r.y + 80f, 60f, 60f), dlgI1, ScaleMode.ScaleToFit);
                if (dlgI2 != null) GUI.DrawTexture(new Rect(r.xMax - 180f, r.y + 80f, 60f, 60f), dlgI2, ScaleMode.ScaleToFit);
            }
            float bw = 150f, gap = 20f, tot = dlgBtns.Length * bw + (dlgBtns.Length - 1) * gap, x = r.x + (r.width - tot) / 2f;
            for (int i = 0; i < dlgBtns.Length; i++)
            {
                string key = dlgBtns[i]; bool cancel = key == "CANCEL" || key == "NO";
                if (Skin.Button(new Rect(x + i * (bw + gap), r.y + 150f, bw, 28f), Loc.T(key), cancel ? Skin.Get(Skin.Inven, "CancelButton") : Skin.Get(Skin.Inven, "RedButton"), true))
                {
                    var y = dlgYes; var any = dlgAny; dlg = null; dlgYes = null; dlgAny = null; dlgI1 = dlgI2 = null;
                    if (!cancel && y != null) y(); if (any != null) any(); return;
                }
            }
        }
    }
}
