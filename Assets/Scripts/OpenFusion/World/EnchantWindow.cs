// EnchantWindow: port de cnEnchantMode + cnGuiEnchant (NPC tipo 27, icono 22 -> NpcIconMode.StartEnchant, "ENCHANT ITEMS"). SIN COMPILAR NI PROBAR EN UNITY.
// Fuente original: Assembly-CSharp/cnEnchantMode.cs, cnGuiEnchant.cs, NpcIconMode.StartEnchant, csDefines (ENCHANT_WEAPON_MATERIAL_ID 101, ENCHANT_DEFENCE_MATERIAL_ID 102, ayudas 141/142).
// Paquetes (cuerpos sin los 4 bytes de tipo):
//   REQ_PC_ITEM_ENCHANT (318767268, 20 B): iEnchantItemSlot, iWeaponMaterialItemSlot, iDefenceMaterialItemSlot, iCashItemSlot1, iCashItemSlot2 (-1 = ninguno; ranuras = indice de inventario 0..49).
//   REP_PC_ITEM_ENCHANT_SUCC (822083885, 64 B): iEnchantItemSlot@0, sItemBase@4 (12), iWeaponMaterialItemSlot@16, sItemBase@20, iDefenceMaterialItemSlot@32, sItemBase@36, iCashItemSlot1@48, iCashItemSlot2@52,
//        iCandy@56 (taros absolutos), iSuccessFlag@60 (1 = exito, 0 = fallo: el objeto baja de nivel). REP_PC_ITEM_ENCHANT_FAIL (822083886, 24 B): iErrorCode@0 + 5 ranuras.
// Tabla de encantamiento: XdtTableScript.m_pEnchantTable.m_pEnchantData (EnchantElement: coste, clase, material de arma/ropa, probabilidad, ...). Se busca la fila de grado (nivel actual + 1).
//   El nivel del objeto es (Opt & 0xFFFF); 1 = sin encantar ("+0"); el icono muestra "+" + (nivel - 1).
// Interaccion (el original arrastra desde el inventario): aqui se TOCA un objeto del panel de inventario de la derecha (IInvMode): equipo (tipos 0..3) -> objeto a encantar;
//   material 101 / 102 -> material de arma / ropa; 141 / 142 -> ayudas. Tocar una casilla de la izquierda la vacia (el material cicla entre pilas). Al elegir el objeto se adjuntan solas las pilas de material.
// Flujo: ENCHANT -> caja 254 (confirmar) o 256 (taros insuficientes) -> barra de 4 s ("Enchanting....") -> REQ_PC_ITEM_ENCHANT -> SUCC/FAIL.
//   exito: ficha del objeto nuevo (JournalWindow.ShowItemExternal) con "ENCHANT MORE ITEMS" / "GO TO MY STUFF"; fallo: caja 259 con los niveles perdidos.
// Estilos: GUISkin 1367 (Jeff12LightBlue, Cha12Yellow, EnchantLevel, RawCover, ItemCover, RawCover2, EnchantWaiting...), 1366 (slotbox, button, RedButton, CancelButton). Texturas UiKit: EnchantBG, EnchantXmark, EnchantWaitProgress, CombiBlackShade.
// Diferencias asumidas: (1) sin camara del NPC (EndSubTarget / DrawCamera ni animacion in_make_out); (2) las ayudas 141/142 no se descuentan localmente (SUCC no trae su objeto); (3) el nombre y la descripcion de los materiales
//   salen de la tabla de objetos generales (el original lee la de equipo por un descuido); (4) solo se encanta equipo del inventario (el REQ no lleva eIL); (5) se muestra tambien el porcentaje de exito y los bonos de la tabla.
using System;
using System.Collections.Concurrent;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class EnchantWindow : MonoBehaviour, IInvMode
    {
        public static EnchantWindow Instance;
        public static EnchantWindow Ensure(GameObject host) { var w = host.GetComponent<EnchantWindow>(); if (w == null) w = host.AddComponent<EnchantWindow>(); Instance = w; return w; }
        /// <summary>Ventana (o su caja de mensaje) abierta: bloquea clics al mundo, movimiento, ataques y atajos (anadir a GameMenu.Blocking y busyPrev).</summary>
        public static bool Active { get { return Instance != null && (Instance.open || Instance.msg != null); } }

        /// <summary>Carga de texturas del bundle ("TutorialAssets/panelback" + ext). Opcional.</summary>
        public Func<string, Texture> TexLoader;

        const int SK = 1367;                                   // GUISkin del enchant (cnGuiEnchant.skinCurSkin)
        const int WpnMat = 101, ArmMat = 102, Help1 = 141, Help2 = 142;
        static readonly Rect WindowRect = new Rect(0, 0, 1036, 654);
        // 0 objeto, 1 material arma, 2 material ropa, 3 ayuda 1, 4 ayuda 2 (iSlotType 21..25 del original)
        static readonly Rect[] SlotR = { new Rect(28, 189, 64, 64), new Rect(28, 302, 64, 64), new Rect(273, 302, 64, 64), new Rect(28, 425, 64, 64), new Rect(273, 425, 64, 64) };
        static readonly Rect CloseBut = new Rect(993, 12, 30, 30), HelpBut = new Rect(983, 597, 32, 32);

        PacketDispatcher disp; WorldEntities ents; XdtTableScript tables; PlayerHud hud; MessageTableScript msgT; JournalWindow jw;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();

        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h)
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            disp = d; ents = e; tables = t; hud = h; msgT = t != null ? t.m_pMessageTable : null; if (disp != null) disp.Unhandled += OnPacket;
        }
        void Awake() { Instance = this; }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; ReleaseInv(); if (Instance == this) Instance = null; }

        // ---------------------------------------------------------------- estado ----------------------------------------------------------------
        bool open, busy, anim, succOpen, prevOpen, closeReq; float busyUntil, animT, noteUntil; int npcId; string note = "";
        readonly int[] att = { -1, -1, -1, -1, -1 };
        int lastSig = int.MinValue;
        // derivados (Recalc)
        int itemErr, level, needWpnId, needWpnNum, needArmId, needArmNum, cost, chanceLv, wpnErr, armErr; bool ready; EnchantElement row; string chanceStr = "", bonusStr = "", failStr = "";
        ItemBase succItem;
        Texture back; float backRetry;
        string msg; string[] msgBtns = { "OK" }; Action msgYes, msgAny;

        JournalWindow Journal { get { if (jw == null) jw = GetComponent<JournalWindow>(); return jw; } }
        static ItemBase[] Inv { get { return InventoryWindow.Items; } }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static ItemBase ReadItem(byte[] b, int o) { return new ItemBase { Type = BitConverter.ToInt16(b, o), Id = BitConverter.ToInt16(b, o + 2), Opt = BitConverter.ToInt32(b, o + 4), TimeLimit = BitConverter.ToInt32(b, o + 8) }; }
        static void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[enchant] envio: " + ex.Message); } } }
        static int Lv(ItemBase it) { int l = it.Opt & 0xFFFF; return l > 1 ? l : 1; }
        static bool InRange(int s) { return s >= 0 && s < Inv.Length; }

        // ---------------------------------------------------------------- apertura / cierre ----------------------------------------------------------------
        /// <summary>StartEnchant / InitEnchantMode: el original no pide nada al servidor; se abre al momento.</summary>
        public void Open(int npcId)
        {
            if (open) return;
            if (tables == null) { ChatHud.AddEventString(4, "Enchant: tables not ready."); return; }
            this.npcId = npcId; ClearAll(); busy = false; anim = false; succOpen = false; prevOpen = false; msg = null; note = ""; lastSig = int.MinValue; back = null; backRetry = 0f;
            var inv = InventoryWindow.Instance; if (inv != null) { inv.ResetOverlays(); inv.EnsureTextures(); inv.Mode = this; }
            open = true; Sfx.Ui("Open_Screen");
        }
        void ReleaseInv() { var inv = InventoryWindow.Instance; if (inv != null && inv.Mode == (IInvMode)this) { inv.Mode = null; inv.ResetOverlays(); } }
        void Exit()
        {
            if (!open) return; open = false; busy = false; anim = false; succOpen = false; prevOpen = false; msg = null; msgYes = null; msgAny = null; ClearAll();
            var j = Journal; if (j != null) j.CloseExternal(); ReleaseInv(); Sfx.Ui("Close_Screen");
        }
        void TryClose() { if (busy || anim || msg != null) return; if (succOpen) { EnchantMore(); return; } Exit(); }

        // ---------------------------------------------------------------- red ----------------------------------------------------------------
        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_REP_PC_ITEM_ENCHANT_SUCC && b.Length >= 64) { var body = b; q.Enqueue(() => OnSucc(body)); }
            else if (type == PacketIds.P_FE2CL_REP_PC_ITEM_ENCHANT_FAIL && b.Length >= 4) { var body = b; q.Enqueue(() => OnFail(I(body, 0))); }
        }

        /// <summary>cnEnchantMode.Enchant: REQ_PC_ITEM_ENCHANT con las ranuras adjuntas (-1 = vacia).</summary>
        void SendEnchant()
        {
            if (att[0] < 0 || (att[1] < 0 && att[2] < 0)) { busy = false; return; }
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_ITEM_ENCHANT, 20);
            for (int i = 0; i < 5; i++) ClientPackets.PutInt(p, 4 + 4 * i, att[i]);   // item, arma, ropa, ayuda1, ayuda2
            Send(p); busy = true; busyUntil = Time.unscaledTime + 12f;
        }

        void OnFail(int code)
        {   // el original solo libera el bloqueo; aqui ademas se avisa
            Debug.Log("[enchant] REP_PC_ITEM_ENCHANT_FAIL code=" + code); busy = false; anim = false;
            Text("ERROR\nThe enchant could not be completed.");
        }

        void OnSucc(byte[] b)
        {
            int slot = I(b, 0); int flag = I(b, 60), candy = I(b, 56);
            if (hud != null && hud.Data != null) hud.Data.Taros = candy;                               // ownstatus.iTaros = iCandy
            var item = ReadItem(b, 4); int oldRaw = InRange(slot) ? (Inv[slot].Opt & 0xFFFF) : 0;
            if (InRange(slot)) Inv[slot] = item;                                                      // evento (5,16,2): actualiza la ranura del objeto
            int ws = I(b, 16), ds = I(b, 32);
            if (ws != -1 && InRange(ws)) Inv[ws] = ReadItem(b, 20);                                   // material consumido (Id 0 si se agoto)
            if (ds != -1 && InRange(ds)) Inv[ds] = ReadItem(b, 36);
            busy = false; anim = false; lastSig = int.MinValue;
            if (flag == 1)
            {
                Sfx.Ui("Action_Sucess"); succItem = item; ClearAll(); succOpen = true;
                var inv = InventoryWindow.Instance; if (inv != null) inv.ResetOverlays();
                var j = Journal; if (j != null) j.ShowItemExternal(item.Type, item.Id, item.Opt, "Enchant");
            }
            else
            {
                Sfx.Ui("Action_Failure01"); int lost = oldRaw - (item.Opt & 0xFFFF);
                MsgThen(259, () => ClearAll(), lost.ToString());                                      // EnchantFailed: ClearAll(0)
            }
        }

        // ---------------------------------------------------------------- adjuntos ----------------------------------------------------------------
        void ClearAll() { for (int i = 0; i < 5; i++) att[i] = -1; lastSig = int.MinValue; }
        void Notice(string s) { note = Loc.T(s); noteUntil = Time.unscaledTime + 3f; Sfx.Ui("No_Button"); }
        static bool Enchantable(int type) { return type >= 0 && type <= 3; }   // Hand, UpperBody, LowerBody, Foot

        int BestStack(int id, int need)
        {   // primera pila con cantidad suficiente (la de menor cantidad); si no, la mayor
            int best = -1, bestMax = -1;
            for (int i = 0; i < Inv.Length; i++)
            {
                if (Inv[i].Type != 7 || Inv[i].Id != id) continue;
                if (Inv[i].Opt >= need && (best < 0 || Inv[i].Opt < Inv[best].Opt)) best = i;
                if (bestMax < 0 || Inv[i].Opt > Inv[bestMax].Opt) bestMax = i;
            }
            return best >= 0 ? best : bestMax;
        }
        int NextStack(int id, int cur)
        {
            for (int k = 1; k <= Inv.Length; k++) { int i = (cur + k) % Inv.Length; if (i != cur && Inv[i].Type == 7 && Inv[i].Id == id) return i; }
            return cur;
        }

        /// <summary>Adjunta la ranura de inventario donde corresponda (equivale a soltarla en la casilla 21..25).</summary>
        void Attach(int slot)
        {
            if (!InRange(slot)) return; var it = Inv[slot]; if (it.Id <= 0) return;
            if (Enchantable(it.Type))
            {
                if (att[0] == slot) { ClearAll(); return; }
                ClearAll(); att[0] = slot; Recalc(); AutoMaterials(); Sfx.Ui("Select_Color"); return;
            }
            if (it.Type == 7 && (it.Id == WpnMat || it.Id == ArmMat || it.Id == Help1 || it.Id == Help2))
            {
                if (att[0] < 0 || itemErr != 0) { Notice("Please Select Item to Enchant First."); return; }
                int idx = it.Id == WpnMat ? 1 : it.Id == ArmMat ? 2 : it.Id == Help1 ? 3 : 4;
                if (idx == 1 && needWpnId != WpnMat) { Notice("That material is not needed for this item."); return; }
                if (idx == 2 && needArmId != ArmMat) { Notice("That material is not needed for this item."); return; }
                att[idx] = att[idx] == slot ? -1 : slot; lastSig = int.MinValue; Sfx.Ui("Select_Color"); return;
            }
            Notice("This item cannot be used for enchanting.");
        }
        void AutoMaterials()
        {
            if (att[0] < 0 || itemErr != 0) return;
            if (needWpnId > 0) att[1] = BestStack(needWpnId, needWpnNum);
            if (needArmId > 0) att[2] = BestStack(needArmId, needArmNum);
            lastSig = int.MinValue;
        }
        /// <summary>Toque sobre una casilla de la izquierda: vacia el objeto, cicla la pila del material o pone/quita la ayuda.</summary>
        void TapSlot(int idx)
        {
            if (idx == 0) { ClearAll(); Sfx.Button(); return; }
            if (att[0] < 0) return;
            if (idx <= 2)
            {
                int id = idx == 1 ? needWpnId : needArmId, need = idx == 1 ? needWpnNum : needArmNum; if (id <= 0) return;
                att[idx] = att[idx] < 0 ? BestStack(id, need) : NextStack(id, att[idx]);
            }
            else
            {
                int id = idx == 3 ? Help1 : Help2;
                att[idx] = att[idx] >= 0 ? -1 : FirstOf(id);
                if (att[idx] < 0 && FirstOf(id) < 0) Notice("You do not have that helping item.");
            }
            lastSig = int.MinValue; Sfx.Button();
        }
        static int FirstOf(int id) { for (int i = 0; i < Inv.Length; i++) if (Inv[i].Type == 7 && Inv[i].Id == id) return i; return -1; }

        void Validate()
        {
            if (att[0] >= 0 && (!InRange(att[0]) || Inv[att[0]].Id <= 0 || !Enchantable(Inv[att[0]].Type))) ClearAll();
            for (int i = 1; i < 5; i++)
            {
                int s = att[i]; if (s < 0) continue;
                int id = i == 1 ? WpnMat : i == 2 ? ArmMat : i == 3 ? Help1 : Help2;
                if (att[0] < 0 || !InRange(s) || Inv[s].Type != 7 || Inv[s].Id != id || Inv[s].Opt <= 0) att[i] = -1;
            }
        }

        // ---------------------------------------------------------------- calculos (ReceiveSetItemAttached) ----------------------------------------------------------------
        EnchantElement RowFor(int grade)
        {
            var t = tables != null ? tables.m_pEnchantTable : null; if (t == null || t.m_pEnchantData == null) return null;
            var d = t.m_pEnchantData; bool anyGrade = false;
            for (int i = 0; i < d.Length; i++) { if (d[i] == null) continue; if (d[i].m_iEnchantGrade > 0) anyGrade = true; if (d[i].m_iEnchantGrade == grade) return d[i]; }
            if (!anyGrade && grade >= 0 && grade < d.Length) return d[grade];                         // sin columna de grado: indice como GetAt(1, grado)
            return null;
        }
        bool CashItem(ItemBase it)
        {
            var t = ItemTbl.Eq(tables, it.Type); if (t == null || t.m_pItemData == null || it.Id < 0 || it.Id >= t.m_pItemData.Length || t.m_pItemData[it.Id] == null) return false;
            return t.m_pItemData[it.Id].m_iCashAble > 0;
        }

        int Signature()
        {
            unchecked
            {
                int h = 17; for (int i = 0; i < 5; i++) { int s = att[i]; h = h * 31 + s; if (InRange(s)) h = h * 31 + Inv[s].Id * 7 + Inv[s].Opt; }
                return h * 31 + (hud != null && hud.Data != null ? hud.Data.Taros : 0);
            }
        }

        void Recalc()
        {
            itemErr = 0; row = null; needWpnId = needWpnNum = needArmId = needArmNum = cost = chanceLv = 0; chanceStr = bonusStr = failStr = ""; wpnErr = armErr = 0; ready = false; level = 1;
            int s = att[0]; if (!InRange(s) || Inv[s].Id <= 0) return;
            var it = Inv[s]; level = Lv(it);
            if (CashItem(it)) { itemErr = 1; return; }
            row = RowFor(level + 1); if (row == null) { itemErr = 2; return; }
            if (row.m_iClass == 0)
            {
                if (it.Type == 0) { needWpnId = WpnMat; needWpnNum = row.m_iWpnMatter; }
                else { needArmId = ArmMat; needArmNum = row.m_iCostumeMatter; }
            }
            else { needWpnId = WpnMat; needWpnNum = row.m_iWpnMatter; needArmId = ArmMat; needArmNum = row.m_iCostumeMatter; }
            cost = row.m_iCost; int p = row.m_iProbability;
            string tier;
            if (p < 10) { chanceLv = 1; tier = "Very Low"; } else if (p < 30) { chanceLv = 2; tier = "Low"; } else if (p < 70) { chanceLv = 3; tier = "Medium"; } else if (p < 90) { chanceLv = 4; tier = "High"; } else { chanceLv = 5; tier = "Very High"; }
            chanceStr = chanceLv + "(" + Loc.T(tier) + ") " + p + "%";
            if (row.m_iOffenceUp > 0 || row.m_iDefenceUp > 0) bonusStr = Loc.T("Attack") + " +" + row.m_iOffenceUp + "    " + Loc.T("Defense") + " +" + row.m_iDefenceUp;
            int[] dp = { row.m_iNoDropProb, row.m_iOneDropProb, row.m_iTwoDropProb, row.m_iThreeDropProb, row.m_iFourDropProb };
            string f = ""; for (int i = 0; i < dp.Length; i++) if (dp[i] > 0) f += (f.Length > 0 ? "   " : "") + "-" + i + ": " + dp[i] + "%";
            if (f.Length > 0) failStr = Loc.T("If it fails, levels lost") + "  " + f;
            if (InRange(att[1]) && needWpnId > 0) wpnErr = Inv[att[1]].Opt < needWpnNum ? 1 : 0;
            if (InRange(att[2]) && needArmId > 0) armErr = Inv[att[2]].Opt < needArmNum ? 1 : 0;
            bool ok = true;
            if (needWpnId > 0 && (att[1] < 0 || wpnErr != 0)) ok = false;
            if (needArmId > 0 && (att[2] < 0 || armErr != 0)) ok = false;
            ready = ok;
            Debug.Log("[enchant] nivel " + level + " -> fila grado " + row.m_iEnchantGrade + " coste " + cost + " prob " + p + " clase " + row.m_iClass + " matArma " + needWpnNum + " matRopa " + needArmNum);
        }

        // ---------------------------------------------------------------- IInvMode (panel de inventario) ----------------------------------------------------------------
        bool Locked { get { return busy || anim || succOpen || msg != null || prevOpen; } }
        public bool OnClick(int slot) { if (!Locked) Attach(slot); return true; }
        public void OnQuick(int slot) { if (!Locked) Attach(slot); }
        public bool OnDrop(int slot, Vector2 wm)
        {   // soltar sobre una casilla = adjuntar; en cualquier otro sitio no hace nada (ni mover ni tirar)
            if (Locked) return true;
            if (SlotR[0].Contains(wm) || SlotR[1].Contains(wm) || SlotR[2].Contains(wm) || SlotR[3].Contains(wm) || SlotR[4].Contains(wm)) Attach(slot);
            return true;
        }
        public void InfoButtons(int slot, Rect ext) { }

        // ---------------------------------------------------------------- mensajes (cnSystemMessageManager) ----------------------------------------------------------------
        string Message(int id) { return msgT != null && msgT.m_pMessageData != null && id >= 0 && id < msgT.m_pMessageData.Length && msgT.m_pMessageData[id] != null ? msgT.m_pMessageData[id].m_szString : "(message " + id + ")"; }
        void MsgThen(int id, Action then, params string[] a)
        {
            string s = Message(id); int bt = 1;
            if (msgT != null && msgT.m_pMessageData != null && id >= 0 && id < msgT.m_pMessageData.Length && msgT.m_pMessageData[id] != null) bt = msgT.m_pMessageData[id].m_iButtonType;
            int k = 0; while (a != null && k < a.Length) { int p = s.IndexOf("%s"); if (p < 0) break; s = s.Substring(0, p) + a[k] + s.Substring(p + 2); k++; }
            msg = Loc.T(s); msgBtns = bt == 2 ? new[] { "OK", "CANCEL" } : bt == 3 ? new[] { "YES", "NO" } : new[] { "OK" }; msgYes = null; msgAny = then; Sfx.Ui("Action_Failure01");
        }
        void Msg(int id, params string[] a) { MsgThen(id, null, a); }
        void Confirm(int id, Action yes) { Msg(id); if (msgBtns.Length == 1) msgBtns = new[] { "OK", "CANCEL" }; msgYes = yes; }
        void Text(string s) { msg = Loc.T(s); msgBtns = new[] { "OK" }; msgYes = null; msgAny = null; Sfx.Ui("Action_Failure01"); }

        /// <summary>cnGuiEnchant boton ENCHANT -> EnchantReady.</summary>
        void EnchantReady()
        {
            int taros = hud != null && hud.Data != null ? hud.Data.Taros : 0;
            if (taros >= cost) Confirm(254, StartAnim); else Msg(256);
        }
        void StartAnim() { anim = true; animT = 0f; Sfx.Ui("Yes_Button"); }   // EnchantConfirmed: espera de 4 s y luego Enchant()

        void Preview()
        {   // PopupControll.Popup(9, 21, ...) con iOpt + 1: ficha del objeto con el nivel siguiente
            if (att[0] < 0 || itemErr != 0) return; var it = Inv[att[0]]; var j = Journal; if (j == null) return;
            var inv = InventoryWindow.Instance; if (inv != null) inv.ResetOverlays();
            j.ShowItemExternal(it.Type, it.Id, it.Opt + 1, "Preview"); prevOpen = j.ExternalOpen; if (prevOpen) Sfx.Button();
        }
        void EnchantMore() { succOpen = false; var j = Journal; if (j != null) j.CloseExternal(); ClearAll(); busy = false; }
        void GoToMyStuff() { EnchantMore(); Exit(); GameMenu.OpenWindow(GameMenu.Win.MyStuff); }

        // ---------------------------------------------------------------- bucle ----------------------------------------------------------------
        void Update()
        {
            Action a; int guard = 0; while (guard++ < 64 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[enchant] " + ex.Message); } }
            if (!open) return;
            if (busy && Time.unscaledTime > busyUntil) { busy = false; Debug.LogWarning("[enchant] sin respuesta del servidor"); }
            if (anim) { animT += Time.unscaledDeltaTime; if (animT >= 4f) { anim = false; SendEnchant(); } }
            var j = Journal; if (prevOpen && (j == null || !j.ExternalOpen)) prevOpen = false;
            if (!succOpen && !anim) Validate();
            int sg = Signature(); if (sg != lastSig) { lastSig = sg; Recalc(); }
            if (Input.GetKeyDown(KeyCode.Escape) && msg == null)
            {
                if (prevOpen) { if (j != null) j.CloseExternal(); prevOpen = false; }
                else TryClose();
            }
        }

        // ---------------------------------------------------------------- dibujo ----------------------------------------------------------------
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        static SkinStyle St(string n) { return Skin.Get(SK, n); }
        static SkinStyle StI(string n) { return Skin.Get(Skin.Inven, n); }
        static void Lbl(string style, Rect r, string text, Color? c = null) { Skin.Label(r, text, St(style), c); }
        static void Pic(Rect r, string tex, float dim = 1f)
        {
            var t = UiKit.Tex(tex); if (t == null) return; if (dim >= 1f) UiKit.Img(r, t); else UiKit.Img(r, t, new Color(dim, dim, dim, 1f));
        }
        static void Fill(Rect r, Color c) { UiKit.Fill(r, c); }

        void LoadBack()
        {
            if (back != null || Time.unscaledTime < backRetry) return; backRetry = Time.unscaledTime + 2f;
            if (TexLoader != null) foreach (var ext in new[] { ".png", ".psd", ".dds", ".tga", "" }) { try { back = TexLoader("TutorialAssets/panelback" + ext); } catch (Exception) { back = null; } if (back != null) break; }
            if (back == null) back = UiKit.Tex("panelback");
        }

        bool Tap(Rect r, bool en)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, r); var e = Event.current; bool res = false; if (!en) return false;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; if (r.Contains(e.mousePosition)) res = true; e.Use(); }
            return res;
        }

        void OnGUI()
        {
            if (!open || Screen.height <= 0) return; var e = Event.current; if (e == null) return;
            if (back == null) LoadBack();
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f);
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1500; GUI.color = Color.white;
            try
            {
                GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
                if (e.type == EventType.Repaint)
                {
                    GUI.color = new Color(0f, 0f, 0f, back != null ? 1f : 0.85f); GUI.DrawTexture(new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc), Texture2D.whiteTexture); GUI.color = Color.white;
                    if (back != null) GUI.DrawTexture(new Rect(-960f, -720f, 1920f, 1440f), back, ScaleMode.ScaleToFit);
                }
                GUI.BeginGroup(new Rect(-WindowRect.width * 0.5f, -WindowRect.height * 0.5f, WindowRect.width, WindowRect.height));
                bool free = !Locked;
                // el boton de cerrar y el de ayuda del panel de inventario cerrarian GameMenu: se interceptan antes
                if (e.type == EventType.MouseDown && e.button == 0 && (CloseBut.Contains(e.mousePosition) || HelpBut.Contains(e.mousePosition)) && msg == null && !prevOpen) { if (CloseBut.Contains(e.mousePosition)) closeReq = true; e.Use(); }
                DrawLeft(e, free);
                var inv = InventoryWindow.Instance; if (inv != null) inv.DrawPanel(e, free, true);
                DrawOverlays(e);
                GUI.EndGroup();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
            if (closeReq) { closeReq = false; TryClose(); }
        }

        void DrawSlotItem(int idx, bool en)
        {
            var r = SlotR[idx]; var slotSt = StI("slotbox"); var emptySt = StI("slotboxempty"); int s = att[idx]; bool has = InRange(s) && Inv[s].Id > 0;
            var st = has ? slotSt : emptySt; if (st != null) Skin.Bg(r, st.N != null ? st.N : (slotSt != null ? slotSt.N : null), st);
            if (has)
            {
                var j = Journal; var ic = j != null ? j.ItemTexture(Inv[s].Type, Inv[s].Id) : null; if (ic != null) UiKit.Img(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), ic, ScaleMode.ScaleToFit);
                if (idx == 0) { int raw = Inv[s].Opt & 0xFFFF; if (raw > 1) Skin.Box(new Rect(28, 241, 25, 12), St("EnchantLevel"), "+" + (raw - 1)); }
                else if (idx <= 2) Skin.Label(new Rect(r.x + 5, r.y + 5, r.width, 20), Inv[s].Opt.ToString(), StI("label"), Color.white);
            }
            if (Tap(r, en && has)) TapSlot(idx);
        }

        string NameOf(int type, int id) { var j = Journal; return j != null ? j.ItemName(type, id) : ""; }
        string DescOf(int type, int id)
        {
            if (tables == null || id <= 0) return "";
            if (type == 7)
            {
                var g = tables.m_pGeneralItemTable; if (g == null || g.m_pItemData == null || id >= g.m_pItemData.Length || g.m_pItemData[id] == null) return "";
                int n = g.m_pItemData[id].m_iItemName; var sd = g.m_pItemStringData; return sd != null && n >= 0 && n < sd.Length && sd[n] != null && sd[n].m_strComment != null ? sd[n].m_strComment : "";
            }
            var t = ItemTbl.Eq(tables, type); if (t == null || t.m_pItemData == null || id >= t.m_pItemData.Length || t.m_pItemData[id] == null) return "";
            int m = t.m_pItemData[id].m_iItemName; var sd2 = t.m_pItemStringData; return sd2 != null && m >= 0 && m < sd2.Length && sd2[m] != null && sd2[m].m_strComment != null ? sd2[m].m_strComment : "";
        }

        /// <summary>Casilla de material (arma / ropa): icono real si esta adjunto, o el requerido atenuado con la cantidad necesaria; X si no hace falta.</summary>
        void DrawMaterial(int idx, int needId, int needNum, int err, float tx, bool en)
        {
            var r = SlotR[idx]; int s = att[idx]; bool has = InRange(s);
            DrawSlotItem(idx, en);
            if (needId == 0) { Pic(new Rect(r.x + 1, r.y + 1, 62, 62), "EnchantXmark", 0.3f); return; }
            var j = Journal;
            if (!has && Tap(r, en)) TapSlot(idx);
            if (has)
            {
                if (err == 0) { Lbl("Jeff8LightBlueLeft", new Rect(tx, r.y + 2, 100, 60), NameOf(7, needId)); Lbl("Cha10SkyBlue", new Rect(tx, r.y + 29, 130, 40), DescOf(7, needId)); }
                else { Pic(new Rect(tx - 5, r.y - 2, 147, 68), "EnchantRawCover"); Lbl("Cha10Blue", new Rect(tx + 15, r.y + 24, 120, 37), Loc.T("Item is not enough. Please check again.")); }
            }
            else
            {
                var ic = j != null ? j.ItemTexture(7, needId) : null; if (ic != null) UiKit.Img(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), ic, new Color(0.3f, 0.3f, 0.3f, 1f), ScaleMode.ScaleToFit);
                Lbl("Jeff8LightBlueLeft", new Rect(tx, r.y + 2, 100, 60), NameOf(7, needId), new Color(0.3f, 0.3f, 0.3f)); Lbl("Cha10SkyBlue", new Rect(tx, r.y + 29, 130, 40), DescOf(7, needId), new Color(0.3f, 0.3f, 0.3f));
                Lbl("Jeff40LightBlue", r, needNum.ToString());
            }
        }
        void DrawHelp(int idx, int id, float tx, bool en)
        {
            var r = SlotR[idx]; bool has = InRange(att[idx]); DrawSlotItem(idx, en);
            var dim = has ? Color.white : new Color(0.3f, 0.3f, 0.3f);
            if (!has) { var j = Journal; var ic = j != null ? j.ItemTexture(7, id) : null; if (ic != null) UiKit.Img(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), ic, dim, ScaleMode.ScaleToFit); if (Tap(r, en)) TapSlot(idx); }
            Lbl("Jeff8LightBlueLeft", new Rect(tx, r.y + 2, 100, 60), NameOf(7, id), dim); Lbl("Cha10SkyBlue", new Rect(tx, r.y + 26, 130, 40), DescOf(7, id), dim);
        }

        void DrawLeft(Event e, bool free)
        {
            bool rep = e.type == EventType.Repaint;
            var bg = UiKit.Tex("EnchantBG"); if (bg != null) UiKit.Img(new Rect(15, 19, 481, 600), bg); else Fill(new Rect(15, 19, 481, 600), new Color(0.01f, 0.06f, 0.14f, 0.92f));
            Lbl("Jeff12LightBlue", new Rect(28, 28, 299, 16), Loc.T("ITEM ENCHANT"));
            Lbl("Cha12Yellow", new Rect(28, 38, 327, 49), Loc.T("Select an item. We'll display your chance for a successful enchant."));
            Lbl("Cha12Yellow", new Rect(28, 155, 150, 20), Loc.T("Item to enchant"));
            Lbl("Cha12Yellow", new Rect(28, 268, 110, 20), Loc.T("Items needed"));
            Lbl("Cha12Yellow", new Rect(28, 390, 110, 20), Loc.T("Helping items"));
            Lbl("Jeff12LightBlueRight", new Rect(100, 519, 143, 25), Loc.T("CHANCE"));
            Lbl("Jeff12LightBlueRight", new Rect(365, 519, 112, 25), Loc.T("TAROS"));
            if (Time.unscaledTime < noteUntil && !string.IsNullOrEmpty(note)) Lbl("Cha12Yellow", new Rect(28, 100, 327, 40), note, new Color(1f, 0.55f, 0.4f));

            bool hasItem = InRange(att[0]) && Inv[att[0]].Id > 0;
            DrawSlotItem(0, free);
            if (hasItem && itemErr == 0)
            {
                var it = Inv[att[0]];
                Lbl("Jeff8LightBlueLeft", new Rect(100, 192, 100, 60), NameOf(it.Type, it.Id)); Lbl("Cha10SkyBlue", new Rect(100, 219, 380, 40), DescOf(it.Type, it.Id));
                int taros = hud != null && hud.Data != null ? hud.Data.Taros : 0;
                Lbl("Cha12YellowRight", new Rect(100, 545, 143, 20), chanceStr); Lbl("Cha12YellowRight", new Rect(365, 545, 112, 20), cost.ToString(), taros >= cost ? (Color?)null : new Color(1f, 0.3f, 0.3f));
                if (bonusStr.Length > 0) Lbl("Cha10SkyBlue", new Rect(28, 562, 460, 14), bonusStr);
                if (failStr.Length > 0) Lbl("Cha10SkyBlue", new Rect(28, 574, 460, 14), failStr);
                DrawMaterial(1, needWpnId, needWpnNum, wpnErr, 100f, free);
                DrawMaterial(2, needArmId, needArmNum, armErr, 345f, free);
                DrawHelp(3, Help1, 100f, free); DrawHelp(4, Help2, 345f, free);
            }
            else
            {
                for (int k = 1; k < 5; k++) DrawSlotItem(k, false);
                if (hasItem && itemErr != 0)
                {
                    Pic(new Rect(100, 190, 385, 68), "EnchantItemCover");
                    Lbl("Char12Blue", new Rect(142, 200, 325, 46), Loc.T("Warning") + "\n" + Loc.T(itemErr == 2 ? "This item cannot be enchanted any further." : "This item cannot be enchanted."));
                }
                Pic(new Rect(16, 266, 479, 237), "EnchantCover");
                Lbl("Char12Blue", new Rect(115, 384, 280, 20), Loc.T("Please Select Item to Enchant First."));
            }
            // botones (24,585) PREVIEW, (185,585) CLEAR, (345,585) ENCHANT, 142x24
            var btn = St("button") ?? StI("button");
            if (Skin.Button(new Rect(24, 585, 142, 24), Loc.T("PREVIEW"), btn, free && hasItem && itemErr == 0)) Preview();
            bool any = false; for (int i = 0; i < 5; i++) if (att[i] >= 0) any = true;
            if (Skin.Button(new Rect(185, 585, 142, 24), Loc.T("CLEAR"), btn, free && any)) ClearAll();
            if (Skin.Button(new Rect(345, 585, 142, 24), Loc.T("ENCHANT"), btn, free && hasItem && itemErr == 0 && ready)) EnchantReady();
        }

        // ---------------------------------------------------------------- superposiciones ----------------------------------------------------------------
        void DrawOverlays(Event e)
        {
            var j = Journal;
            if (prevOpen && j != null)
            {
                Fill(WindowRect, new Color(0f, 0f, 0f, 0.4f)); j.DrawExternal(e); if (!j.ExternalOpen) prevOpen = false;
            }
            if (anim) DrawWaiting(e);
            if (succOpen) DrawSucc(e, j);
            if (msg != null) DrawMsg(e);
        }

        /// <summary>DoEnchantWaiting: sombra, caja EnchantWaiting 434x435 con barra de 4 s (sin camara del NPC: se muestra el objeto).</summary>
        void DrawWaiting(Event e)
        {
            var shade = UiKit.Tex("CombiBlackShade"); if (e.type == EventType.Repaint) { if (shade != null) UiKit.Img(WindowRect, shade, new Color(1f, 1f, 1f, 0.75f)); else Fill(WindowRect, new Color(0f, 0f, 0f, 0.75f)); }
            var box = new Rect((WindowRect.width - 434f) * 0.5f, (WindowRect.height - 435f) * 0.5f, 434f, 435f);
            var st = St("EnchantWaiting"); if (st != null && st.N != null) Skin.Bg(box, st.N, st); else Fill(box, new Color(0.02f, 0.1f, 0.2f, 0.97f));
            GUI.BeginGroup(box);
            var bar = new Rect(12, 386, 414f * Mathf.Clamp01(animT / 4f), 13); var pt = UiKit.Tex("EnchantWaitProgress"); if (pt != null) UiKit.Img(bar, pt); else Fill(bar, new Color(0.2f, 0.8f, 1f, 0.9f));
            Lbl("Cha12Yellow", new Rect(25, 20, 170, 22), Loc.T("Enchanting...."), Color.black);
            if (InRange(att[0]) && Inv[att[0]].Id > 0)
            {
                var j = Journal; var ic = j != null ? j.ItemTexture(Inv[att[0]].Type, Inv[att[0]].Id) : null; float pulse = 0.85f + 0.15f * Mathf.Sin(animT * 6f);
                if (ic != null) UiKit.Img(new Rect(217f - 70f * pulse, 213f - 70f * pulse, 140f * pulse, 140f * pulse), ic, ScaleMode.ScaleToFit);
            }
            GUI.EndGroup();
        }

        /// <summary>DoEnchantSucc: ficha del objeto nuevo (la misma del inventario) con el aviso "HOORAY!" y los dos botones.</summary>
        void DrawSucc(Event e, JournalWindow j)
        {
            var shade = UiKit.Tex("CombiBlackShade"); if (e.type == EventType.Repaint) { if (shade != null) UiKit.Img(WindowRect, shade, new Color(1f, 1f, 1f, 0.75f)); else Fill(WindowRect, new Color(0f, 0f, 0f, 0.75f)); }
            Rect banner, btnArea;
            if (j != null && j.ExternalOpen)
            {
                j.DrawExternal(e);
                if (j.ExternalOpen) { var rr = j.ExtRect; banner = new Rect(rr.x, Mathf.Max(2f, rr.y - 62f), Mathf.Max(rr.width, 300f), 56f); btnArea = new Rect(rr.x, rr.yMax + 6f, 210f, 25f); }
                else { banner = new Rect(300, 220, 436, 70); btnArea = new Rect(413, 300, 210, 25); }
            }
            else { banner = new Rect(300, 220, 436, 70); btnArea = new Rect(413, 300, 210, 25); }
            if (e.type == EventType.Repaint) { Fill(banner, new Color(0.02f, 0.1f, 0.2f, 0.97f)); Fill(new Rect(banner.x, banner.y, banner.width, 2f), new Color(0f, 0.8f, 0.9f, 1f)); }
            Pic(new Rect(banner.x + 6, banner.y + 6, 44, 44), "CombiIcon2");
            Lbl("Jeff12SkyBlue", new Rect(banner.x + 56, banner.y + 5, 200, 18), Loc.T("HOORAY!"));
            Lbl("Cha10LightBlueLeft", new Rect(banner.x + 56, banner.y + 24, banner.width - 62, 30), Loc.T("Enchant was a success! Your new item is ready and piping hot."));
            var btn = St("button") ?? StI("button");
            if (Skin.Button(btnArea, Loc.T("ENCHANT MORE ITEMS"), btn)) EnchantMore();
            else if (Skin.Button(new Rect(btnArea.x, btnArea.y + 33f, btnArea.width, btnArea.height), Loc.T("GO TO MY STUFF"), btn)) GoToMyStuff();
        }

        void DrawMsg(Event e)
        {
            var oc = GUI.color; if (e.type == EventType.Repaint) { GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = oc; }
            var r = new Rect(300f, 220f, 436f, 200f); if (e.type == EventType.Repaint) { GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = oc; }
            var lab = StI("label"); if (lab != null) Skin.Label(new Rect(r.x + 15f, r.y + 15f, r.width - 30f, 120f), msg, lab, Color.white);
            float bw = 150f, gap = 20f, tot = msgBtns.Length * bw + (msgBtns.Length - 1) * gap, x = r.x + (r.width - tot) / 2f;
            for (int i = 0; i < msgBtns.Length; i++)
            {
                string key = msgBtns[i]; bool cancel = key == "CANCEL" || key == "NO";
                if (Skin.Button(new Rect(x + i * (bw + gap), r.y + 150f, bw, 28f), Loc.T(key), cancel ? StI("CancelButton") : StI("RedButton"), true))
                { var y = msgYes; var any = msgAny; msg = null; msgYes = null; msgAny = null; if (!cancel && y != null) y(); if (any != null) any(); return; }
            }
        }
    }
}
