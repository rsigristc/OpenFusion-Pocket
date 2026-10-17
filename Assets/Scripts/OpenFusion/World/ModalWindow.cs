// ModalWindow: base comun de las ventanas de servicio a pantalla completa (Banco, Tienda, Intercambio). SIN COMPILAR NI PROBAR EN UNITY.
// Marco = el de InventoryWindow/EmailWindow: grupo de 1036x654 centrado con GUI.matrix escalada (Sc = min(UiScale, W/1050, H/681)), fondo panelback + textura propia de la mitad izquierda
// (TutorialAssets/<FrameName>) y, a la derecha, el panel de inventario reutilizable (InventoryWindow.DrawPanel en modo E-Mail: solo pestana EQUIPMENT).
// Incluye: cola de paquetes, cierre por GameMenu.CloseWindow, caja de mensajes (cnSystemMessageManager, tabla xdt de mensajes), ficha de objeto externa con botones, teclado numerico (CacuPopup/GumPopup) y tablas de objetos (ItemTbl).
using System;
using System.Collections.Concurrent;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    /// <summary>Datos de tabla de un objeto (equipo, general, cofre).</summary>
    public struct ItemMeta { public bool Ok; public int Price, SellPrice, SellAble, TradeAble, Stack, Level, ItemType, ItemNumber, BatteryRecharge, RunSpeed; }

    /// <summary>
    /// El servidor de referencia (Items.cpp itemMoveHandler) invierte los tipos de lista en la respuesta: eFrom = eTo de la peticion y eTo = eFrom.
    /// En un intercambio los numeros de ranura tambien van invertidos (todo cuadra), pero al APILAR (iFromSlotNum/iToSlotNum = los de la peticion) un movimiento
    /// inventario &lt;-&gt; banco queda con el tipo de lista cruzado. Fix() devuelve los tipos correctos: el objeto A (@8) va a (ef, fs) y el B (@28) a (et, ts).
    /// </summary>
    public static class MoveAck
    {
        public static void Fix(ref int ef, int fs, ref int et, int ts, ItemBase a, ItemBase b)
        {
            if (ef == et) return;
            int qEf = ClientPackets.LastMoveEf, qFs = ClientPackets.LastMoveFs, qEt = ClientPackets.LastMoveEt, qTs = ClientPackets.LastMoveTs;
            if (qEf == qEt) return;
            bool stackShape = ef == qEt && et == qEf && fs == qFs && ts == qTs;
            bool swapShape = ef == qEt && et == qEf && fs == qTs && ts == qFs;
            if (!stackShape) return;
            bool same = a.Id > 0 && a.Id == b.Id && a.Type == b.Type;
            if (swapShape && !(same || a.Id <= 0)) return;
            int t = ef; ef = et; et = t;
        }
    }

    public static class ItemTbl
    {
        public static EquipmentItemTableScript Eq(XdtTableScript t, int type)
        {
            if (t == null) return null;
            switch (type) { case 0: return t.m_pWeaponItemTable; case 1: return t.m_pShirtsItemTable; case 2: return t.m_pPantsItemTable; case 3: return t.m_pShoesItemTable; case 4: return t.m_pHatItemTable; case 5: return t.m_pGlassItemTable; case 6: return t.m_pBackItemTable; case 10: return t.m_pVehicleItemTable; }
            return null;
        }
        public static ItemMeta Get(XdtTableScript t, int type, int id)
        {
            var m = new ItemMeta(); if (t == null || id <= 0) return m;
            if (type <= 6 || type == 10)
            {
                var e = Eq(t, type); if (e == null || e.m_pItemData == null || id >= e.m_pItemData.Length || e.m_pItemData[id] == null) return m; var r = e.m_pItemData[id];
                m.Ok = true; m.Price = r.m_iItemPrice; m.SellPrice = r.m_iItemSellPrice; m.SellAble = r.m_iSellAble; m.TradeAble = r.m_iTradeAble; m.Stack = Mathf.Max(1, r.m_iStackNumber); m.Level = r.m_iMinReqLev; m.RunSpeed = r.m_iUp_runSpeed; return m;
            }
            if (type == 9)
            {
                var c = t.m_pChestItemTable; if (c == null || c.m_pItemData == null || id >= c.m_pItemData.Length || c.m_pItemData[id] == null) return m; var r = c.m_pItemData[id];
                m.Ok = true; m.Price = r.m_iItemPrice; m.SellPrice = r.m_iItemSellPrice; m.SellAble = r.m_iSellAble; m.TradeAble = r.m_iTradeAble; m.Stack = Mathf.Max(1, r.m_iStackNumber); return m;
            }
            if (type == 7)
            {
                var g = t.m_pGeneralItemTable; if (g == null || g.m_pItemData == null || id >= g.m_pItemData.Length || g.m_pItemData[id] == null) return m; var r = g.m_pItemData[id];
                m.Ok = true; m.Price = r.m_iItemPrice; m.SellPrice = r.m_iItemSellPrice; m.SellAble = r.m_iSellAble; m.TradeAble = r.m_iTradeAble; m.Stack = Mathf.Max(1, r.m_iStackNumber); m.ItemType = r.m_iItemType; m.ItemNumber = r.m_iItemNumber; m.BatteryRecharge = r.m_iBatteryRecharge; return m;
            }
            return m;
        }
        public static bool IsEquip(int type) { return (type >= 0 && type <= 6) || type == 10; }
    }

    /// <summary>Teclado numerico de CacuPopup / GumPopup: valor 0..Max, boton de confirmar con texto propio.</summary>
    public sealed class NumPad
    {
        public bool Open, first; public int Value, Max; public string Title = "", Label = "", OkText = "ADD"; public Action<int> OnOk; public int Cost;   // Cost > 0: muestra "COST n"
        public static readonly Rect Box = new Rect(413, 200, 210, 255), TitleR = new Rect(20, 15, 140, 20), TypeR = new Rect(8, 47, 188, 20), WinR = new Rect(25, 63, 136, 109), EnterR = new Rect(20, 200, 170, 27), CloseR = new Rect(180, 2, 30, 30), ValR = new Rect(1, 1, 132, 20);
        static readonly Rect[] Btn = {
            new Rect(46, 89, 44, 19), new Rect(1, 29, 44, 19), new Rect(46, 29, 44, 19), new Rect(91, 29, 44, 19), new Rect(1, 49, 44, 19), new Rect(46, 49, 44, 19), new Rect(91, 49, 44, 19),
            new Rect(1, 69, 44, 19), new Rect(46, 69, 44, 19), new Rect(91, 69, 44, 19), new Rect(1, 89, 44, 19), new Rect(91, 89, 44, 19) };
        static readonly string[] Sty = { "cacubut", "cacubut1", "cacubut", "cacubut3", "cacubut", "cacubut", "cacubut", "cacubut", "cacubut", "cacubut", "cacubutc", "cacubutn" };
        Texture2D tBox, tBack;

        // GumPopup (objetos generales en tienda): ventana "gumdlg" 310x355 con icono, nombre, COST, descripcion y teclado
        public bool Gum; public Texture GumIcon; public string GumName = "", GumDesc = "";
        public void ShowGum(Texture icon, string name, string desc, string ok, int max, int start, Action<int> onOk, int cost, bool inv = false)
        { Show(name, "Amount", ok, max, start, onOk, cost); Gum = true; GumIcon = icon; GumName = name ?? ""; GumDesc = desc ?? ""; GumWin = new Rect(inv ? 610 : 200, 90, 310, 355); }   // GumPopup: eIn (objeto del inventario) en (610,90), eOut (objeto de la tienda) en (200,90)
        Rect GumWin = new Rect(200, 90, 310, 355); static readonly Rect GumCacu = new Rect(87, 162, 136, 109), GumEnter = new Rect(79, 301, 150, 25);
        void DrawGum(Event e, bool en)
        {
            bool rep = e.type == EventType.Repaint; var lab = Skin.Get(Skin.Inven, "label"); var cen = Skin.Get(Skin.Inven, "centerLabel") ?? lab; var right = Skin.Get(Skin.Inven, "rightLabel") ?? lab; var dlg = Skin.Get(Skin.Inven, "gumdlg");
            if (tBack == null) tBack = UiKit.Tex("cacuback");
            if (rep) { var oc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(new Rect(0, 0, 1036, 654), Texture2D.whiteTexture); GUI.color = oc; }
            GUI.BeginGroup(GumWin);
            if (rep) { if (dlg != null && dlg.N != null) GUI.DrawTexture(new Rect(0, 0, GumWin.width, GumWin.height), dlg.N); else { var oc = GUI.color; GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(new Rect(0, 0, GumWin.width, GumWin.height), Texture2D.whiteTexture); GUI.color = oc; } }
            var yel = new Color(1f, 1f, 0f);
            if (rep && GumIcon != null) GUI.DrawTexture(new Rect(16, 16, 64, 64), GumIcon);
            Skin.Label(new Rect(82, 16, 170, 40), GumName, lab, yel);
            if (Cost > 0) Skin.Label(new Rect(82, 50, 170, 20), Loc.T("COST") + " " + Cost, lab, yel);
            Skin.Label(new Rect(12, 95, 280, 40), GumDesc, lab, Color.white);
            Skin.Label(new Rect(87, 140, 137, 20), Loc.T("Amount"), cen, Color.white);
            GUI.BeginGroup(GumCacu);
            if (rep && tBack != null) GUI.DrawTexture(new Rect(0, 0, GumCacu.width, GumCacu.height), tBack);
            Skin.Label(ValR, Value.ToString(), right, yel);
            for (int i = 0; i < 12; i++)
            {
                string t = i < 10 ? i.ToString() : i == 10 ? "C" : "";
                if (Skin.Button(Btn[i], t, Skin.Get(Skin.Inven, Sty[i]), en)) { if (i < 10) { if (first) { Value = 0; first = false; } Value = (int)Math.Min((long)Value * 10 + i, Max); } else if (i == 10) Value = 0; }
            }
            GUI.EndGroup();
            if (Skin.Button(GumEnter, Loc.T(OkText), Skin.Get(Skin.Menu, "RedButton") ?? Skin.Get(Skin.Inven, "button"), en && Value > 0)) { Open = false; var f = OnOk; int v = Value; if (f != null) f(v); }
            else if (Skin.Button(new Rect(277, 0, 32, 32), "", Skin.Get(Skin.Inven, "closebut"), en)) Open = false;
            GUI.EndGroup();
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Open = false; e.Use(); }
        }

        // TuringPopup (cofres en tienda): ventana "usedlg" 310x235 en (200,90), icono (16,12,68), nombre (82,16), COST (82,50), descripcion (12,95) y boton BUY (160,180,130x28)
        public bool Use;
        public void ShowUse(Texture icon, string name, string desc, string ok, int cost, Action onOk)
        { Show(name, "", ok, 1, 1, v => { if (onOk != null) onOk(); }, cost); Use = true; GumIcon = icon; GumName = name ?? ""; GumDesc = desc ?? ""; UseWin = new Rect(200, 90, 310, 235); OnTrash = null; LevelLine = null; }
        /// <summary>TuringPopup en el panel de inventario (eIn): ventana en (610,90), boton de papelera (10,180,32x32) opcional y linea de nivel opcional.</summary>
        public void ShowUseIn(Texture icon, string name, string desc, string ok, Action onOk, Action onTrash, string levelLine)
        { ShowUse(icon, name, desc, ok, 0, onOk); UseWin = new Rect(610, 90, 310, 235); OnTrash = onTrash; LevelLine = levelLine; }
        Rect UseWin = new Rect(200, 90, 310, 235); Action OnTrash; string LevelLine;
        void DrawUse(Event e, bool en)
        {
            bool rep = e.type == EventType.Repaint; var lab = Skin.Get(Skin.Inven, "label"); var left = Skin.Get(Skin.Inven, "leftLabel") ?? lab; var dlg = Skin.Get(Skin.Inven, "usedlg");
            if (rep) { var oc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(new Rect(0, 0, 1036, 654), Texture2D.whiteTexture); GUI.color = oc; }
            GUI.BeginGroup(UseWin);
            if (rep) { if (dlg != null && dlg.N != null) GUI.DrawTexture(new Rect(0, 0, UseWin.width, UseWin.height), dlg.N); else { var oc = GUI.color; GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(new Rect(0, 0, UseWin.width, UseWin.height), Texture2D.whiteTexture); GUI.color = oc; } }
            if (rep && GumIcon != null) GUI.DrawTexture(new Rect(16, 12, 68, 68), GumIcon, ScaleMode.ScaleToFit);
            Skin.Label(new Rect(82, 16, 170, 40), GumName, lab, Color.white);
            if (Cost > 0) Skin.Label(new Rect(82, 50, 170, 20), Loc.T("COST") + " " + Cost, lab, new Color(1f, 1f, 0f));
            if (!string.IsNullOrEmpty(LevelLine)) Skin.Label(new Rect(82, 50, 170, 20), LevelLine, lab, Color.white);
            Skin.Label(new Rect(12, 95, 280, 40), GumDesc, left, new Color(0.21f, 1f, 1f, 0.72f));
            if (Skin.Button(new Rect(160, 180, 130, 28), Loc.T(OkText), Skin.Get(Skin.Menu, "RedButton") ?? Skin.Get(Skin.Inven, "button"), en)) { Open = false; var f = OnOk; if (f != null) f(Value); }
            else if (OnTrash != null && Skin.Button(new Rect(10, 180, 32, 32), "", Skin.Get(Skin.Inven, "trashbut"), en)) { Open = false; var tf = OnTrash; tf(); }
            else if (Skin.Button(new Rect(277, 0, 32, 32), "", Skin.Get(Skin.Inven, "closebut"), en)) Open = false;
            GUI.EndGroup();
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Open = false; e.Use(); }
        }

        // GumPopup (My Stuff, popupType eEquip): ventana "gumdlg" en (610,90). Gumball (m_iItemType 3): tres cuadros de nano (33/125/215,160, 62x62) con GIVE (66x25, y=232);
        // resto de generales: calculadora desactivada con la cantidad de la pila y boton DELETE (79,301,150x25). La linea de nivel muestra "COST info.iLevel" (0 en generales), igual que el original.
        public bool Give; bool giveGum; Texture[] giveNano; bool[] giveOk; Action<int> onGive; Action onDelete;
        public void ShowGive(Texture icon, string name, string desc, bool gum, int stack, Texture[] nano, bool[] ok, Action<int> give, Action del)
        { Show(name, "Amount", "DELETE", stack, stack, null, 0); Give = true; giveGum = gum; GumIcon = icon; GumName = name ?? ""; GumDesc = desc ?? ""; giveNano = nano; giveOk = ok; onGive = give; onDelete = del; first = false; }
        static readonly Rect GiveWin = new Rect(610, 90, 310, 355);
        static readonly Rect[] NanoBox = { new Rect(33, 160, 62, 62), new Rect(125, 160, 62, 62), new Rect(215, 160, 62, 62) };
        static readonly Rect[] NanoBut = { new Rect(31, 232, 66, 25), new Rect(125, 232, 66, 25), new Rect(215, 232, 66, 25) };
        void DrawGive(Event e, bool en)
        {
            bool rep = e.type == EventType.Repaint; var lab = Skin.Get(Skin.Inven, "label"); var cen = Skin.Get(Skin.Inven, "centerLabel") ?? lab; var right = Skin.Get(Skin.Inven, "rightLabel") ?? lab; var dlg = Skin.Get(Skin.Inven, "gumdlg"); var slot = Skin.Get(Skin.Inven, "slotbox");
            if (tBack == null) tBack = UiKit.Tex("cacuback");
            if (rep) { var oc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(new Rect(0, 0, 1036, 654), Texture2D.whiteTexture); GUI.color = oc; }
            int act = 0, nanoIdx = -1;   // 1 = GIVE, 2 = DELETE
            GUI.BeginGroup(GiveWin);
            if (rep) { if (dlg != null && dlg.N != null) GUI.DrawTexture(new Rect(0, 0, GiveWin.width, GiveWin.height), dlg.N); else { var oc = GUI.color; GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(new Rect(0, 0, GiveWin.width, GiveWin.height), Texture2D.whiteTexture); GUI.color = oc; } }
            var yel = new Color(1f, 1f, 0f);
            if (rep && GumIcon != null) GUI.DrawTexture(new Rect(16, 16, 64, 64), GumIcon);
            Skin.Label(new Rect(82, 16, 170, 40), GumName, lab, yel);
            Skin.Label(new Rect(82, 50, 170, 20), Loc.T("COST") + " 0", lab, yel);
            Skin.Label(new Rect(12, 95, 280, 40), GumDesc, lab, Color.white);
            var gb = Skin.Get(Skin.Inven, "button");
            if (giveGum)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (giveNano == null || i >= giveNano.Length || giveNano[i] == null) continue;
                    if (rep) { if (slot != null) Skin.Bg(NanoBox[i], slot.N, slot); GUI.DrawTexture(NanoBox[i], giveNano[i]); }
                    bool ok = giveOk != null && i < giveOk.Length && giveOk[i];
                    if (Skin.Button(NanoBut[i], Loc.T("GIVE"), gb, en && ok)) { act = 1; nanoIdx = i; }
                }
            }
            else
            {
                Skin.Label(new Rect(87, 140, 137, 20), Loc.T("Amount"), cen, Color.white);
                GUI.BeginGroup(GumCacu);
                if (rep && tBack != null) GUI.DrawTexture(new Rect(0, 0, GumCacu.width, GumCacu.height), tBack);
                Skin.Label(ValR, Value.ToString(), right, yel);
                for (int i = 0; i < 12; i++) Skin.Button(Btn[i], i < 10 ? i.ToString() : i == 10 ? "C" : "", Skin.Get(Skin.Inven, Sty[i]), false);
                GUI.EndGroup();
            }
            if (Skin.Button(GumEnter, Loc.T("DELETE"), Skin.Get(Skin.Inven, "RedButton") ?? Skin.Get(Skin.Menu, "RedButton") ?? gb, en && (giveGum || Value > 0))) act = 2;
            else if (Skin.Button(new Rect(277, 0, 32, 32), "", Skin.Get(Skin.Inven, "closebut"), en)) Open = false;
            GUI.EndGroup();
            if (act == 1) { Open = false; var f = onGive; if (f != null) f(nanoIdx); }
            else if (act == 2) { Open = false; var f2 = onDelete; if (f2 != null) f2(); }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Open = false; e.Use(); }
        }

        public void Show(string title, string label, string ok, int max, int start, Action<int> onOk, int cost = 0) { first = true; Gum = false; Use = false; Give = false; Title = title; Label = label; OkText = ok; Max = Mathf.Max(0, max); Value = Mathf.Clamp(start, 0, Max); OnOk = onOk; Cost = cost; Open = true; Sfx.Ui("Open_Screen"); }

        /// <summary>Se dibuja en coordenadas de ventana (1036x654).</summary>
        public void Draw(Event e, bool en)
        {
            if (!Open) return; if (Give) { DrawGive(e, en); return; } if (Use) { DrawUse(e, en); return; } if (Gum) { DrawGum(e, en); return; } bool rep = e.type == EventType.Repaint;
            if (tBox == null) tBox = UiKit.Tex("taroscacu") ?? UiKit.Tex("emailcacu"); if (tBack == null) tBack = UiKit.Tex("cacuback");
            if (rep) { var oc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(new Rect(0, 0, 1036, 654), Texture2D.whiteTexture); GUI.color = oc; }
            var lab = Skin.Get(Skin.Inven, "label"); var cen = Skin.Get(Skin.Inven, "centerLabel") ?? lab; var right = Skin.Get(Skin.Inven, "rightLabel") ?? lab;
            GUI.BeginGroup(Box);
            if (rep && tBox != null) GUI.DrawTexture(new Rect(0, 0, Box.width, Box.height), tBox);
            Skin.Label(TitleR, Loc.T(Title), lab, Color.white); Skin.Label(TypeR, Loc.T(Label), cen, Color.white);
            if (Cost > 0) Skin.Label(new Rect(8, 180, 188, 18), Loc.T("COST") + " " + Cost, cen, new Color(1f, 0.92f, 0.016f));
            GUI.BeginGroup(WinR);
            if (rep && tBack != null) GUI.DrawTexture(new Rect(0, 0, WinR.width, WinR.height), tBack);
            Skin.Label(ValR, Value.ToString(), right, new Color(1f, 0.92f, 0.016f));
            for (int i = 0; i < 12; i++)
            {
                string t = i < 10 ? i.ToString() : i == 10 ? "C" : "";
                if (Skin.Button(Btn[i], t, Skin.Get(Skin.Inven, Sty[i]), en)) { if (i < 10) { if (first) { Value = 0; first = false; } Value = (int)Math.Min((long)Value * 10 + i, Max); } else if (i == 10) Value = 0; }
            }
            GUI.EndGroup();
            if (Skin.Button(EnterR, Loc.T(OkText), Skin.Get(Skin.Inven, "button"), en)) { Open = false; var f = OnOk; int v = Value; if (f != null) f(v); }
            else if (Skin.Button(CloseR, "", Skin.Get(Skin.Inven, "closebut"), en)) Open = false;
            GUI.EndGroup();
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Open = false; e.Use(); }
        }
    }

    public abstract class ModalWindow : MonoBehaviour
    {
        public Func<string, Texture> TexLoader;
        protected PacketDispatcher disp; protected WorldEntities ents; protected XdtTableScript tables; protected PlayerHud hud; protected JournalWindow journal; protected MessageTableScript msgT;
        protected readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        protected bool open; protected bool waiting; protected float waitUntil;
        protected Texture back, frame; float frameRetry;
        protected static readonly Rect WindowRect = new Rect(0, 0, 1036, 654);
        protected readonly NumPad pad = new NumPad();
        protected string msg; protected string[] msgBtns = { "OK" }; protected Action msgYes, msgAny;
        protected bool infoOpen; Action<Rect> infoButtons;
        public static bool Overlayed;      // algun mensaje / ficha / teclado abierto: bloquea atajos y ESC global (lo lee GameMenu)
        public static ModalWindow Current;

        protected abstract GameMenu.Win WinId { get; }
        protected abstract string FrameName { get; }
        protected abstract void OnPacket(uint type, byte[] b);
        protected abstract void DrawContent(Event e, bool en);
        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }
        protected virtual void Tick() { }
        protected virtual bool ShowInventory { get { return true; } }
        protected virtual IInvMode InvMode { get { return null; } }

        public void InitBase(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h, JournalWindow j)
        {
            if (disp != null) disp.Unhandled -= OnPacket; disp = d; ents = e; tables = t; hud = h; journal = j; msgT = t != null ? t.m_pMessageTable : null; if (disp != null) disp.Unhandled += OnPacket;
        }
        protected virtual void Awake() { GameMenu.Implemented.Add(WinId); }
        protected virtual void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; if (Current == this) { Current = null; Overlayed = false; } }

        protected static void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[modal] envio: " + ex.Message); } } }
        protected static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        protected static void PutS(byte[] p, int o, short v) { BitConverter.GetBytes(v).CopyTo(p, o); }
        protected static void PutI(byte[] p, int o, int v) { BitConverter.GetBytes(v).CopyTo(p, o); }
        protected static ItemBase ReadItem(byte[] b, int o) { return new ItemBase { Type = BitConverter.ToInt16(b, o), Id = BitConverter.ToInt16(b, o + 2), Opt = BitConverter.ToInt32(b, o + 4), TimeLimit = BitConverter.ToInt32(b, o + 8) }; }
        protected void Wait(float s = 5f) { waiting = true; waitUntil = Time.unscaledTime + s; }
        protected void Release() { waiting = false; }
        protected static SkinStyle St(string n) { return Skin.Get(Skin.Inven, n); }
        protected static Texture2D Tx(string n) { return UiKit.Tex(n); }
        protected static void Pic(Rect r, string n) { var t = UiKit.Tex(n); if (t != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); }
        protected int ItemCount(int type, int id) { return 0; }
        protected string ItemNameOf(int type, int id) { return journal != null ? journal.ItemName(type, id) : ""; }
        protected int Taros { get { return hud != null ? hud.Data.Taros : 0; } }
        protected int Level { get { return hud != null ? hud.Data.Level : 1; } }


        /// <summary>Barra de desplazamiento vertical (coordenadas locales del grupo donde se dibuja). Devuelve el nuevo scroll.</summary>
        protected float VScrollBar(Event e, Rect track, float scroll, float contentH, float viewH, bool en, Vector2 unused = default(Vector2))
        {
            float max = Mathf.Max(0f, contentH - viewH); if (max <= 0f) return 0f;
            var tb = St("verticalScrollbar"); var th = St("verticalScrollbarThumb");
            float frac = viewH / contentH, thumbH = Mathf.Max(16f, track.height * frac), ty = track.y + (track.height - thumbH) * (scroll / max); var thumb = new Rect(track.x, ty, track.width, thumbH);
            if (e.type == EventType.Repaint) { if (tb != null && tb.N != null) GUI.DrawTexture(track, tb.N); if (th != null && th.N != null) GUI.DrawTexture(thumb, th.N); }
            int id = GUIUtility.GetControlID(FocusType.Passive, track); Vector2 m = e.mousePosition;
            if (en)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && track.Contains(m)) { GUIUtility.hotControl = id; e.Use(); }
                if (GUIUtility.hotControl == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)) { float t = Mathf.Clamp01((m.y - track.y - thumbH * 0.5f) / Mathf.Max(1f, track.height - thumbH)); scroll = t * max; e.Use(); }
                else if (GUIUtility.hotControl == id && e.type == EventType.MouseUp) { GUIUtility.hotControl = 0; e.Use(); }
            }
            return scroll;
        }

        // ---- mensajes (cnSystemMessageManager) ----
        protected void Msg(int id, params string[] a)
        {
            string s = null; int bt = 1;
            if (msgT != null && msgT.m_pMessageData != null && id >= 0 && id < msgT.m_pMessageData.Length && msgT.m_pMessageData[id] != null) { s = msgT.m_pMessageData[id].m_szString; bt = msgT.m_pMessageData[id].m_iButtonType; }
            if (s == null) s = "(message " + id + ")";
            int k = 0; while (a != null && k < a.Length) { int p = s.IndexOf("%s"); if (p < 0) break; s = s.Substring(0, p) + a[k] + s.Substring(p + 2); k++; }
            msg = Loc.T(s); msgBtns = bt == 2 ? new[] { "OK", "CANCEL" } : bt == 3 ? new[] { "YES", "NO" } : new[] { "OK" }; msgYes = null; msgAny = null; Sfx.Ui("Action_Failure01");
        }
        protected void Text(string s, Action onOk = null) { msg = Loc.T(s); msgBtns = new[] { "OK" }; msgYes = null; msgAny = onOk; Sfx.Ui("Action_Failure01"); }
        protected void Confirm(int id, Action yes, params string[] a) { Msg(id, a); if (msgBtns.Length == 1) msgBtns = new[] { "OK", "CANCEL" }; msgYes = yes; }
        protected void MsgThen(int id, Action then, params string[] a) { Msg(id, a); msgAny = then; }
        protected string Message(int id)
        {
            return msgT != null && msgT.m_pMessageData != null && id >= 0 && id < msgT.m_pMessageData.Length && msgT.m_pMessageData[id] != null ? msgT.m_pMessageData[id].m_szString : "(message " + id + ")";
        }

        // ---- ficha de objeto propia de la ventana ----
        protected void ShowInfo(int type, int id, int opt, Action<Rect> buttons, string title = "Item", int timeLimit = 0)
        {
            if (journal == null || id <= 0 || type == 8) return;
            if (InventoryWindow.Instance != null) InventoryWindow.Instance.ResetOverlays();
            journal.ShowItemExternal(type, id, opt, title, timeLimit); infoOpen = journal.ExternalOpen; infoButtons = buttons; if (infoOpen) Sfx.Button();
        }
        Texture2D tarosIco;
        /// <summary>EquipPopup (popupType 3): linea de precio (rectMoney 3,348,303,20 dentro del grupo desplazado 14) con el icono de taros y las etiquetas "price:" / "taros".</summary>
        protected void DrawPrice(Rect ext, int price)
        {
            var mb = St("moneybar") ?? St("label"); if (mb == null) return; if (tarosIco == null) tarosIco = UiKit.Tex("tarosicon");
            var r = new Rect(ext.x + 3f, ext.y + 14f + 348f, 303f, 20f); var txt = price.ToString();
            Skin.Box(r, mb, txt);
            var lab = St("label"); float tw = mb.Font != null ? mb.Font.Width(txt) : txt.Length * 10f;
            if (tarosIco != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(r.x + (int)(r.width / 2) + (int)(tw / 2) + 5f, r.y - 5f, 32f, 32f), tarosIco);
            if (lab != null) { var c = new Color(0f, 0.22f, 0.44f); Skin.Label(new Rect(ext.x + 3f + 20f, r.y, 112f, 20f), Loc.T("price:"), lab, c); Skin.Label(new Rect(ext.x + 3f + 205f, r.y, 80f, 20f), Loc.T("taros"), lab, c); }
        }
        protected void CloseInfo() { infoOpen = false; infoButtons = null; if (journal != null) journal.CloseExternal(); }
        void DrawInfo(Event e)
        {
            if (!infoOpen || journal == null) return;
            if (e.type == EventType.Repaint) { var oc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.4f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = oc; }
            journal.DrawExternal(e); if (!journal.ExternalOpen) { infoOpen = false; return; }
            if (infoButtons != null) infoButtons(journal.ExtRect);
        }
        protected bool Blocked(bool includeInventory = true) { return msg != null || infoOpen || pad.Open || (includeInventory && InventoryWindow.Busy) || waiting; }

        // ---- bucle ----
        void Update()
        {
            Action a; int guard = 0; while (guard++ < 128 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[modal] " + ex.Message); } }
            bool now = GameMenu.Current == WinId;
            if (now && !open) { open = true; slideT = SlideIn ? 0f : 1f; Current = this; msg = null; pad.Open = false; waiting = false; CloseInfo(); frameRetry = 0f; if (InventoryWindow.Instance != null) { InventoryWindow.Instance.ResetOverlays(); InventoryWindow.Instance.EnsureTextures(); InventoryWindow.Instance.Mode = InvMode; } OnOpened(); }
            else if (!now && open) { open = false; msg = null; pad.Open = false; CloseInfo(); if (InventoryWindow.Instance != null) { InventoryWindow.Instance.ResetOverlays(); InventoryWindow.Instance.Mode = null; } OnClosed(); if (Current == this) { Current = null; Overlayed = false; } }
            if (open)
            {
                if (slideT < 1f) slideT = Mathf.Min(1f, slideT + Time.unscaledDeltaTime);   // fScrollFos / fWindowScroll += dt * 1
                Overlayed = msg != null || infoOpen || pad.Open;
                if (waiting && Time.unscaledTime > waitUntil) waiting = false;
                if (Overlayed && Input.GetKeyDown(KeyCode.Escape)) { if (msg != null) { } else if (pad.Open) pad.Open = false; else CloseInfo(); }
                Tick();
            }
        }

        /// <summary>Entrada deslizante de 1 s desde la izquierda (banco y tienda: 1 - sin(pi/2 t)); la GUI esta deshabilitada mientras dura.</summary>
        protected virtual bool SlideIn { get { return false; } }
        float slideT = 1f;
        void LoadFrame()
        {
            if (TexLoader == null || Time.unscaledTime < frameRetry) return; frameRetry = Time.unscaledTime + 2f;
            if (back == null) foreach (var ext in new[] { ".png", ".psd", ".dds", ".tga", "" }) { try { back = TexLoader("TutorialAssets/panelback" + ext); } catch (Exception) { back = null; } if (back != null) break; }
            if (frame == null) foreach (var ext in new[] { ".psd", ".png", ".dds", ".tga", "" }) { try { frame = TexLoader("TutorialAssets/" + FrameName + ext); } catch (Exception) { frame = null; } if (frame != null) break; }
        }

        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        void OnGUI()
        {
            if (!open) return; var e = Event.current; if (e == null || Screen.height <= 0) return;
            if (back == null || frame == null) LoadFrame();
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
                float slideX = slideT < 1f ? -(1f - Mathf.Sin(Mathf.PI * 0.5f * slideT)) * (W * 0.5f / Sc + WindowRect.width * 0.5f) : 0f;
                GUI.BeginGroup(new Rect(-WindowRect.width * 0.5f + slideX, -WindowRect.height * 0.5f, WindowRect.width, WindowRect.height));
                if (e.type == EventType.Repaint)
                {
                    if (frame != null) GUI.DrawTexture(new Rect(0, 0, 585, 653), frame, ScaleMode.ScaleToFit);
                    else { GUI.color = new Color(0.01f, 0.06f, 0.14f, 0.92f); GUI.DrawTexture(new Rect(0, 0, 585, 653), Texture2D.whiteTexture); GUI.color = Color.white; }
                }
                bool en = !Blocked(false) && !InventoryWindow.Busy && slideT >= 1f;
                DrawContent(e, en);
                if (ShowInventory && InventoryWindow.Instance != null) InventoryWindow.Instance.DrawPanel(e, !Blocked(false) && slideT >= 1f, true);
                DrawInfo(e);
                pad.Draw(e, msg == null);
                if (msg != null) DrawMsg(e);
                GUI.EndGroup();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        void DrawMsg(Event e)
        {
            var oc = GUI.color; if (e.type == EventType.Repaint) { GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = oc; }
            var r = new Rect(300f, 220f, 436f, 200f); if (e.type == EventType.Repaint) { GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = oc; }
            var lab = St("label"); if (lab != null) Skin.Label(new Rect(r.x + 15f, r.y + 15f, r.width - 30f, 120f), msg, lab, Color.white);
            float bw = 150f, gap = 20f, tot = msgBtns.Length * bw + (msgBtns.Length - 1) * gap, x = r.x + (r.width - tot) / 2f;
            for (int i = 0; i < msgBtns.Length; i++)
            {
                string key = msgBtns[i]; bool cancel = key == "CANCEL" || key == "NO";
                if (Skin.Button(new Rect(x + i * (bw + gap), r.y + 150f, bw, 28f), Loc.T(key), cancel ? St("CancelButton") : St("RedButton"), true))
                { var y = msgYes; var any = msgAny; msg = null; msgYes = null; msgAny = null; if (!cancel && y != null) y(); if (any != null) any(); return; }
            }
        }
    }
}
