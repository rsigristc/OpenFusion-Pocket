// InventoryWindow (parcial): pantalla izquierda de My Stuff (Panel_UserClothes + Panel_Equip), circulos de stats, pestana NANOS con fichas (DoNanoPopup/DoNextNanoPopup) y datos de nanos. SIN COMPILAR NI PROBAR EN UNITY.
// Especificacion: claude/w20-lote3l.md (ver tambien Panel_UserClothes.RenderClothe, Panel_Equip.DoEquipPanel, Panel_PCStuffScript.DoNanoTab).
// Coordenadas: la ventana mide 1036x654; Panel_UserClothes/Equip dibujan en un grupo (8,7) de 512x649 (iWidth 1020, iHeight 638).
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed partial class InventoryWindow
    {
        // ---- proveedores (WorldBootstrap) ----
        public AvatarPreview Preview;                 // vista 3D del personaje
        public Func<int, Texture> NanoModel;          // NanoModelRt.Get
        public NanoWheelHud Wheel;                    // opcional: BankSkill

        // ---- datos de nanos del jugador (sPCLoadData2CL.aNanoBank + paquetes) ----
        const int NanoMax = 512;
        public static readonly short[] NanoOwned = new short[NanoMax];   // indice = id de nano; != 0 -> poseido
        public static readonly short[] NanoSkill = new short[NanoMax];   // tune elegida
        static int nanoBankSize = 37;
        public static int Guide;                                          // iMentor (1..5), 0 = ninguno
        public static bool OwnsNano(int id) { return id > 0 && id < NanoMax && NanoOwned[id] != 0; }

        int tab;                                      // 0 EQUIPMENT, 1 NANOS
        int nanoPop = -1;                             // nano de la ficha abierta
        bool nanoPopOwned;
        // ---- Estacion de nanos (GUIMode.eGui_NanoMachine): My Stuff solo con la pestana NANOS + equipar / desequipar / cambiar poder ----
        public static bool Machine; int machineNpc; bool machineBusy; string machineName = "";
        public Func<int, Texture> NpcModel;           // opcional: render 3D del NPC de la estacion (cmNanoMachine)
        Texture cloth2; float clothRetry2;
        /// <summary>NpcIconMode -> NANO STATION (StartNanoMachine): abre My Stuff en modo maquina.</summary>
        public void OpenStation(int npcId, string npcName)
        {
            Machine = true; machineNpc = npcId; machineName = npcName ?? ""; machineBusy = false; tab = 1; nanoPop = -1;
            if (!GameMenu.OpenWindow(GameMenu.Win.MyStuff)) Machine = false;
            if (Machine) DualScreenController.Instance?.StationOpened();
            Debug.Log("[DS Nanos] station npc=" + npcId + " opened=" + Machine + " window=" + GameMenu.Current);
        }
        List<int> nanoList; float turnDir; float clothRetry; Texture cloth;
        readonly System.Collections.Concurrent.ConcurrentQueue<Action> qn = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        static readonly Rect[] NanoWinR = { new Rect(24, 215, 126, 69), new Rect(335, 175, 126, 69), new Rect(348, 350, 126, 69) };
        static readonly Rect[] NanoRtR = { new Rect(45, 110, 128, 128), new Rect(355, 69, 128, 128), new Rect(368, 244, 128, 128) };
        static readonly string[] GuideName = { "EDD", "DEXTER", "MOJO JOJO", "BEN TENNYSON", "COMPUTRESS" };
        static readonly string[] GuideIcon = { "EddIcon", "DexIcon", "MojojoIcon", "BenIcon", "Computeres" };
        static readonly string[] TypeIcon = { "blue_icon", "red_icon", "yellow_icon" };
        static readonly Color Cy = new Color(0.8f, 1f, 1f);
        const int SN = Skin.Nano;

        // ============================================================ datos / red ============================================================
        void NanoInit()
        {
            if (disp != null) disp.SelfEnterRaw += OnEnterRaw;
        }
        void NanoDetach() { if (disp != null) disp.SelfEnterRaw -= OnEnterRaw; }
        void OnEnterRaw(byte[] b)
        {
            const int L = 4; if (b == null || b.Length < L + 1680) return; var body = b;
            qn.Enqueue(() =>
            {
                Guide = BitConverter.ToInt16(body, L + 86);
                for (int i = 0; i < 37; i++) { int o = L + 1444 + i * 6; NanoOwned[i] = BitConverter.ToInt16(body, o); NanoSkill[i] = BitConverter.ToInt16(body, o + 2); }
                nanoList = null;
            });
        }
        /// <summary>Paquetes de nanos que mantienen el banco local (el HUD ya procesa slots/estamina).</summary>
        void NanoPacket(uint type, byte[] b)
        {
            if (type == PacketIds.P_FE2CL_REP_PC_NANO_CREATE_SUCC && b.Length >= 26)
            {   // iPC_FusionMatter @0, iQuestItemSlotNum @4, QuestItem @8, Nano @20 (iID, iSkillID, iStamina)
                var body = b; qn.Enqueue(() => { int id = BitConverter.ToInt16(body, 20); if (id > 0 && id < NanoMax) { NanoOwned[id] = (short)id; NanoSkill[id] = BitConverter.ToInt16(body, 22); } });
            }
            else if (type == PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC && b.Length >= 4)
            {
                var body = b; qn.Enqueue(() =>
                {
                    int id = BitConverter.ToInt16(body, 0); if (id > 0 && id < NanoMax) NanoSkill[id] = BitConverter.ToInt16(body, 2);
                    // sP_FE2CL_REP_NANO_TUNE_SUCC (168 B): iPC_FusionMatter @4, aiItemSlotNum[10] @8 (-1 = sin usar), aItem[10] @48 (12 B)
                    if (body.Length >= 168)
                    {
                        if (hud != null) hud.Data.FusionMatter = BitConverter.ToInt32(body, 4);
                        for (int i = 0; i < 10; i++) { int sl = BitConverter.ToInt32(body, 8 + 4 * i); if (sl >= 0 && sl < 50) Items[sl] = It(body, 48 + 12 * i); }
                    }
                    machineBusy = false; nanoPop = -1; nanoList = null;
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_NANO_EQUIP_SUCC || type == PacketIds.P_FE2CL_REP_NANO_UNEQUIP_SUCC || type == PacketIds.P_FE2CL_REP_NANO_TUNE_FAIL)
            {   // la GUI se vuelve a habilitar y la ficha se cierra (los slots los actualiza el HUD de nanos)
                qn.Enqueue(() => { machineBusy = false; nanoPop = -1; });
            }
            else if (type == PacketIds.P_FE2CL_REP_NANO_BOOK_SUBSET && b.Length >= 76)
            {   // PCUID @0 (8), bookSize @8, elementOffset @12, element[10] @16 (sNano 6 B)
                var body = b; qn.Enqueue(() =>
                {
                    int size = (int)BitConverter.ToUInt32(body, 8), off = BitConverter.ToInt32(body, 12); if (size > nanoBankSize && size <= NanoMax) nanoBankSize = size;
                    for (int i = off == 0 ? 1 : 0; i < 10 && i + off < size && i + off < NanoMax; i++) { int o = 16 + i * 6; NanoOwned[i + off] = BitConverter.ToInt16(body, o); NanoSkill[i + off] = BitConverter.ToInt16(body, o + 2); }
                    nanoList = null;
                });
            }
        }
        void NanoUpdate()
        {
            Action a; while (qn.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[inv] nano " + ex.Message); } }
            if (open && !Machine && Preview != null && !Typing)
            {   // flechas del teclado como las del panel
                float kd = (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f); if (kd != 0f) Preview.Yaw += kd * 60f * Time.unscaledDeltaTime;
            }
            if (turnDir != 0f && Preview != null) Preview.Yaw += turnDir * 60f * Time.unscaledDeltaTime;
            if (!open) turnDir = 0f;
        }

        // ---- tablas ----
        NanoTableScript NT { get { return tables != null ? tables.m_pNanoTable : null; } }
        NanoElement NanoRow(int id) { var t = NT; return t != null && t.m_pNanoData != null && id > 0 && id < t.m_pNanoData.Length ? t.m_pNanoData[id] : null; }
        string NanoStr(NanoElement n, int field)
        {
            var t = NT; if (n == null || t == null || t.m_pNanoStringData == null || n.m_iNanoName < 0 || n.m_iNanoName >= t.m_pNanoStringData.Length || t.m_pNanoStringData[n.m_iNanoName] == null) return "";
            var s = t.m_pNanoStringData[n.m_iNanoName]; return field == 0 ? s.m_strName : field == 1 ? s.m_strComment : s.m_strComment1;
        }
        int SkillOfNano(int id) { int s = id > 0 && id < NanoMax ? NanoSkill[id] : 0; if (s == 0 && Wheel != null) s = Wheel.BankSkill(id); return s; }
        Texture NanoIconTex(NanoElement n, bool ready)
        {
            var t = NT; if (n == null || t == null || t.m_pNanoIconData == null || n.m_iIcon1 < 0 || n.m_iIcon1 >= t.m_pNanoIconData.Length || journal == null) return null; var ic = t.m_pNanoIconData[n.m_iIcon1]; if (ic == null) return null;
            return ready ? journal.IconTexture(new IconElement { m_iIconType = 5, m_iIconNumber = ic.m_iIconNumber }) : journal.IconTexture(ic);
        }
        Texture SkillIconTex(int skillId)
        {
            var st = tables != null ? tables.m_pSkillTable : null; if (st == null || st.m_pSkillData == null || skillId <= 0 || skillId >= st.m_pSkillData.Length || st.m_pSkillData[skillId] == null || journal == null) return null;
            int ic = st.m_pSkillData[skillId].m_iIcon; return st.m_pSkillIconData != null && ic >= 0 && ic < st.m_pSkillIconData.Length ? journal.IconTexture(st.m_pSkillIconData[ic]) : null;
        }
        EquipmentItemTableScript EqTable(int type)
        {
            if (tables == null) return null;
            switch (type) { case 0: return tables.m_pWeaponItemTable; case 1: return tables.m_pShirtsItemTable; case 2: return tables.m_pPantsItemTable; case 3: return tables.m_pShoesItemTable; case 4: return tables.m_pHatItemTable; case 5: return tables.m_pGlassItemTable; case 6: return tables.m_pBackItemTable; case 10: return tables.m_pVehicleItemTable; }
            return null;
        }
        AvatarGrowElement Grow(int lvl) { var at = tables != null ? tables.m_pAvatarTable : null; var gd = at != null ? at.m_pAvatarGrowData : null; return gd != null && lvl >= 0 && lvl < gd.Length ? gd[lvl] : null; }

        /// <summary>CurAttrib.iPointRat / iGroupRat / iDefenseRat (cnAvatarStatus.UpdateAttribute + cnOwnAvatarStatus.UpdateAttrib).</summary>
        public void ComputeStats(out int one, out int many, out int def)
        {
            one = many = def = 0; int lvl = hud != null ? hud.Data.Level : 0; var g = Grow(lvl);
            if (g != null) { one = many = (g.m_iAccuracy + g.m_iPower) / 2; def = g.m_iDodge + g.m_iProtection; }
            for (int s = 0; s <= 6; s++)
            {
                var it = EquipAt(s); if (it.Id <= 0) continue; var t = EqTable(s);
                if (t == null || t.m_pItemData == null || it.Id >= t.m_pItemData.Length || t.m_pItemData[it.Id] == null) continue;
                var row = t.m_pItemData[it.Id]; one += row.m_iPointRat; many += row.m_iGroupRat; def += row.m_iDefenseRat;
            }
            if (hud != null) for (int k = 0; k < 3; k++) { var n = NanoRow(hud.Data.NanoSlots[k]); if (n != null) def += n.m_iProtection + n.m_iDodge; }
        }
        int MaxHpNow() { int lvl = hud.Data.Level; if (hud.Data.MaxHp > 0) return hud.Data.MaxHp; var g = Grow(lvl); return g != null && g.m_iMaxHP > 0 ? g.m_iMaxHP : HudData.DefaultMaxHp(lvl); }
        int MaxFmNow() { if (hud.Data.MaxFusionMatter > 0) return hud.Data.MaxFusionMatter; var g = Grow(Mathf.Min(60, hud.Data.Level)); return g != null ? g.m_iReqBlob_NanoCreate : 0; }

        // ============================================================ pantalla izquierda ============================================================
        Texture EnsureCloth()
        {
            if (cloth != null) return cloth; if (TexLoader == null || Time.unscaledTime < clothRetry) return null; clothRetry = Time.unscaledTime + 2f;
            foreach (var ext in new[] { ".png", ".psd", ".dds", ".tga", "" }) { try { cloth = TexLoader("TutorialAssets/colthback" + ext); } catch (Exception) { cloth = null; } if (cloth != null) break; }
            return cloth;
        }
        static void Tx(Rect r, string name) { var t = UiKit.Tex(name); if (t != null) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); }
        static SkinStyle St(string n) { return Skin.Get(Skin.Inven, n); }

        void DrawLeft(Event e, bool en)
        {
            if (Machine) { DrawMachineLeft(e, en); DrawEquipColumn(e, en); return; }
            bool rep = e.type == EventType.Repaint; var back = EnsureCloth();
            if (rep) { if (back != null) GUI.DrawTexture(new Rect(0, 0, 585, 653), back, ScaleMode.ScaleToFit); else { GUI.color = new Color(0.01f, 0.06f, 0.14f, 0.9f); GUI.DrawTexture(new Rect(0, 0, 585, 653), Texture2D.whiteTexture); GUI.color = Color.white; } }
            GUI.BeginGroup(new Rect(8, 7, 512, 649));
            DrawClothes(e, en);
            GUI.EndGroup();
            DrawEquipColumn(e, en);
        }

        void DrawClothes(Event e, bool en)
        {
            bool rep = e.type == EventType.Repaint; if (hud == null) return;
            var lab = St("label"); var white = St("whitesmalllabel"); var lblue = St("lightbluesmalllabel");
            // 1) personaje 3D
            if (Preview != null) { var veh8 = EquipAt(8); Preview.Tables = tables; Preview.VehicleItem = VehicleRide.SelfRiding && veh8.Id > 0 ? veh8.Id : 0; }
            if (rep && Preview != null && selfApp != null) { var t = Preview.Get(selfApp, 500, 564); if (t != null) GUI.DrawTexture(new Rect(0, -50, 500, 564), t, ScaleMode.StretchToFill, true); }
            // 2) circulos + numeros
            int one, many, def; ComputeStats(out one, out many, out def);
            if (rep) { Tx(new Rect(100, 0, 92, 92), "OneVs"); Tx(new Rect(210, 0, 92, 92), "ManyVs"); Tx(new Rect(310, 0, 92, 92), "Defense"); }
            var labC = VendorWindow.Align(lab, TextAnchor.MiddleCenter); if (labC != null) { labC.pL = 0; labC.pR = 0; }   // el original centra el valor bajo cada circulo (rect de 92 px)
            Skin.Label(new Rect(100, 85, 92, 20), one.ToString(), labC, Cy); Skin.Label(new Rect(210, 85, 92, 20), many.ToString(), labC, Cy); Skin.Label(new Rect(310, 85, 92, 20), def.ToString(), labC, Cy);
            // 3) cuadros de nano
            for (int i = 0; i < 3; i++) DrawNanoWin(i, rep, NanoWinR[i]);
            // 4) vehiculo
            DrawVehicle(e, en, rep, white, lblue);
            if (VendorWindow.ReturnToStore && VendorWindow.Instance != null && Skin.Button(new Rect(388, 490, 101, 26), Loc.T("BACK TO STORE"), SmallButton(), en)) { GameMenu.CloseWindow(); GameMenu.OpenWindow(GameMenu.Win.Vendor); return; }
            // 5) modelos 3D de los nanos
            for (int i = 0; i < 3; i++)
            {
                int id = hud.Data.NanoSlots[i]; if (id <= 0 || !rep) continue; var r = NanoRtR[i]; Texture t = NanoModel != null ? NanoModel(id) : null;
                if (t != null) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
                else { var ic = NanoIconTex(NanoRow(id), false); if (ic != null) GUI.DrawTexture(new Rect(r.x + 32, r.y + 32, 64, 64), ic, ScaleMode.ScaleToFit); }
            }
            // 6) iconos de tipo (gumballs)
            for (int i = 0; i < 3 && rep; i++)
            {
                int id = hud.Data.NanoSlots[i]; var n = NanoRow(id); if (n == null) continue; Tx(new Rect(NanoRtR[i].x + 34, NanoRtR[i].y + 70, 59, 61), TypeIcon[Mathf.Clamp(n.m_iStyle, 0, 2)]);
            }
            // 7) datos de usuario
            DrawUserInfo(rep, lab, white, lblue);
            // 8) flechas de giro (RepeatButton; estilos cruzados a proposito)
            DrawTurn(e, en, new Rect(120, 440, 43, 78), "rightturn", "rightturnover", 1f);
            DrawTurn(e, en, new Rect(340, 440, 43, 78), "leftturn", "leftturnover", -1f);
        }

        SkinStyle smallBtn;
        SkinStyle SmallButton()
        {   // mismo estilo "button" con fuente menor y sin relleno lateral para que BACK TO STORE quepa en el boton
            if (smallBtn != null) return smallBtn; var b = St("button"); if (b == null) return null;
            var f = Skin.Font("JEFFE___12");
            smallBtn = new SkinStyle { Name = b.Name, N = b.N, H = b.H, A = b.A, ON = b.ON, bL = b.bL, bR = b.bR, bT = b.bT, bB = b.bB, pL = 2, pR = 2, pT = 0, pB = 0, Font = f != null ? f : b.Font, Align = TextAnchor.MiddleCenter, cN = b.cN, cH = b.cH, cA = b.cA, Wrap = false, FixedW = 0f, FixedH = 0f, Offset = b.Offset };
            return smallBtn;
        }

        void DrawNanoWin(int i, bool rep, Rect box)
        {
            GUI.BeginGroup(box);
            if (rep) Tx(new Rect(0, 0, 126, 69), "nanodlg");
            int id = hud.Data.NanoSlots[i]; var n = NanoRow(id); var lblue = St("lightbluesmalllabel");
            if (n == null) { Skin.Label(new Rect(5, 33, 118, 14), Loc.T("EMPTY"), lblue); }
            else
            {
                if (rep)
                {
                    float ratio = n.m_iNanoBattery1 > 0 ? Mathf.Min(1f, Mathf.Max(0f, (float)hud.Data.NanoStamina[i] / n.m_iNanoBattery1)) : 1f; var bar = UiKit.Tex("HP_BAR"); if (bar != null) GUI.DrawTexture(new Rect(7, 59, 90f * ratio, 4), bar, ScaleMode.StretchToFill, true);
                }
                Skin.Label(new Rect(5, 18, 48, 9), "nano " + (i + 1), St("whitesmalllabel"));
                Skin.Label(new Rect(5, 33, 118, 14), NanoStr(n, 0), lblue);
                int stl = Mathf.Clamp(n.m_iStyle, 0, 2); var ts = St(stl == 0 ? "bluesmalllabel" : stl == 1 ? "redsmalllabel" : "yellowsmalllabel");
                Skin.Label(new Rect(5, 45, 92, 14), NanoStr(n, 2), ts, stl == 0 ? new Color(0f, 0.353f, 1f) : (Color?)null);
                if (rep) { var sk = SkillIconTex(SkillOfNano(id)); if (sk != null) GUI.DrawTexture(new Rect(101, 44, 20, 20), sk, ScaleMode.StretchToFill, true); }
            }
            GUI.EndGroup();
        }

        float hopT = -10f; GameClient gclMy;
        bool InCombatNow() { if (gclMy == null) gclMy = FindObjectOfType<GameClient>(); return gclMy != null && Time.time < gclMy.CombatUntil; }   // GameCondition.IsEventCondition(eC_Combat)
        void DrawVehicle(Event e, bool en, bool rep, SkinStyle white, SkinStyle lblue)
        {
            var veh = EquipAt(8); GUI.BeginGroup(new Rect(0, 375, 167, 52));
            if (rep) Tx(new Rect(0, 0, 167, 52), "vehicledlg");
            Skin.Label(new Rect(6, 3, 60, 20), Loc.T("vehicle"), white);
            string name = Loc.T("EMPTY"); var t = EqTable(10);
            if (veh.Id > 0 && t != null && t.m_pItemData != null && veh.Id < t.m_pItemData.Length && t.m_pItemData[veh.Id] != null)
            { int ni = t.m_pItemData[veh.Id].m_iItemName; if (t.m_pItemStringData != null && ni >= 0 && ni < t.m_pItemStringData.Length && t.m_pItemStringData[ni] != null) name = t.m_pItemStringData[ni].m_strName; }
            Skin.Label(new Rect(5, 24, 158, 9), name, lblue);
            GUI.EndGroup();
            if (veh.Id > 0)
            {
                bool ok = en && hud.Data.Hp > 0 && !InCombatNow() && VehicleRide.CanUseUi();   // Panel_UserClothes: GUI.enabled = !eC_Combat & vehiculo equipado & !instancia & !animacion especial
                if (Skin.Button(new Rect(25, 431, 100, 25), Loc.T(GameMenu.Riding ? "HOP OFF" : "HOP ON"), St("button"), ok) && Time.unscaledTime - hopT >= 0.5f && (hopT = Time.unscaledTime) >= 0f) { if (!VehicleRide.Toggle(false)) Send(ClientPackets.New(GameMenu.Riding ? PacketIds.P_CL2FE_REQ_PC_VEHICLE_OFF : PacketIds.P_CL2FE_REQ_PC_VEHICLE_ON, 1)); }
            }
        }

        void DrawUserInfo(bool rep, SkinStyle lab, SkinStyle white, SkinStyle lblue)
        {
            GUI.BeginGroup(new Rect(0, 517, 498, 115));
            if (rep) { var bg = UiKit.Tex("userstback"); if (bg != null) UiKit.Slice(new Rect(0, 0, 498, 115), bg, 8, 8, 8, 8); }
            Skin.Label(new Rect(13, 12, 308, 40), hud.Data.Name, lab, Cy);
            Skin.Label(new Rect(18, 43, 200, 20), Loc.T("LEVEL") + " : " + hud.Data.Level, lab, new Color(1f, 0.92f, 0.016f));
            int mh = Mathf.Max(1, MaxHpNow()), hp = Mathf.Max(0, hud.Data.Hp); int mf = Mathf.Max(1, MaxFmNow()), fm = Mathf.Max(0, hud.Data.FusionMatter);
            if (rep)
            {
                var back = UiKit.Tex("infoHpBack"); var hb = UiKit.Tex("HP_BAR"); var fb = UiKit.Tex("FM_BAR");
                if (back != null) { GUI.DrawTexture(new Rect(13, 68, 472, 17), back, ScaleMode.StretchToFill, true); GUI.DrawTexture(new Rect(13, 90, 472, 17), back, ScaleMode.StretchToFill, true); }
                if (hb != null) GUI.DrawTexture(new Rect(14, 69, 470f * Mathf.Min(1f, (float)hp / mh), 15), hb, ScaleMode.StretchToFill, true);
                if (fb != null) GUI.DrawTexture(new Rect(14, 91, 470f * Mathf.Min(1f, (float)fm / mf), 15), fb, ScaleMode.StretchToFill, true);
            }
            var wl = white; var left = wl != null ? Left(wl) : null; var right = wl != null ? Right(wl) : null;
            Skin.Label(new Rect(17, 66, 200, 20), Loc.T("health"), left); Skin.Label(new Rect(17, 66, 450, 20), hp + "/" + mh, right);
            Skin.Label(new Rect(17, 88, 200, 20), Loc.T("fusion matter"), left); Skin.Label(new Rect(17, 88, 450, 20), fm + "/" + mf, right);
            if (Guide >= 1 && Guide <= 5)
            {
                if (rep) { Tx(new Rect(350, 7, 139, 57), "GuideBox"); Tx(new Rect(440, 16, 36, 36), GuideIcon[Guide - 1]); }
                Skin.Label(new Rect(360, 16, 100, 20), Loc.T("GUIDE") + ":", lblue); Skin.Label(new Rect(360, 36, 100, 20), Loc.T(GuideName[Guide - 1]), lblue, Cy);
            }
            GUI.EndGroup();
        }
        static readonly Dictionary<SkinStyle, SkinStyle> leftC = new Dictionary<SkinStyle, SkinStyle>(), rightC = new Dictionary<SkinStyle, SkinStyle>();
        static SkinStyle Variant(SkinStyle s, TextAnchor a, Dictionary<SkinStyle, SkinStyle> c)
        {
            SkinStyle v; if (c.TryGetValue(s, out v)) return v;
            v = new SkinStyle { Name = s.Name, N = s.N, H = s.H, A = s.A, ON = s.ON, bL = s.bL, bR = s.bR, bT = s.bT, bB = s.bB, pL = s.pL, pR = s.pR, pT = s.pT, pB = s.pB, Font = s.Font, Align = a, cN = s.cN, cH = s.cH, cA = s.cA, Wrap = s.Wrap, FixedW = s.FixedW, FixedH = s.FixedH, Offset = s.Offset }; c[s] = v; return v;
        }
        static SkinStyle Left(SkinStyle s) { return Variant(s, TextAnchor.MiddleLeft, leftC); }
        static SkinStyle Right(SkinStyle s) { return Variant(s, TextAnchor.MiddleRight, rightC); }
        static readonly Dictionary<SkinStyle, SkinStyle> centerC = new Dictionary<SkinStyle, SkinStyle>();
        static SkinStyle Center(SkinStyle s) { return Variant(s, TextAnchor.MiddleCenter, centerC); }

        void DrawTurn(Event e, bool en, Rect r, string n, string nOver, float dir)
        {
            bool hov = r.Contains(e.mousePosition);
            if (e.type == EventType.Repaint) Tx(r, hov && turnDir == dir ? nOver : n);
            if (!en) { if (turnDir == dir) turnDir = 0f; return; }
            if (e.type == EventType.MouseDown && e.button == 0 && hov) { turnDir = dir; e.Use(); }
            else if (e.type == EventType.MouseUp && turnDir == dir) { turnDir = 0f; }
        }

        // ---- columna de equipo (Panel_Equip.DoEquipPanel): 9 ranuras de 64x64 con paso 62 + BOOSTS / POTIONS ----
        void DrawEquipColumn(Event e, bool en)
        {
            bool rep = e.type == EventType.Repaint; var bar = St("equipbar"); var ef = St("equipfont"); var slotSt = St("slotbox"); var emptySt = St("slotboxempty"); var lab = St("label");
            Skin.Label(new Rect(512, 10, 64, 13), " " + Loc.T("Equipped"), bar);
            GUI.BeginGroup(new Rect(512, 21, 66, 625));
            for (int i = 0; i < 9; i++)
            {
                int es = EqOrder[i]; var r = new Rect(0, 62 * i, 64, 64); var it = EquipAt(es); bool has = it.Id > 0;
                if (rep) { var st = has ? slotSt : emptySt; if (st != null) Skin.Bg(r, st.N != null ? st.N : (slotSt != null ? slotSt.N : null), st); if (has) { int ty = es == 8 ? 10 : es == 7 ? 0 : es; var ic = Icon(ty, it.Id, it.Opt); if (ic != null) GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, 58, 58), ic, ScaleMode.ScaleToFit); } }
                Skin.Label(new Rect(0, 62 * i - 2, 60, 19), Loc.T(EqName[i]), ef != null ? Right(ef) : null, has ? new Color(0.6f, 1f, 0f, 0.8f) : new Color(1f, 1f, 1f, 0.8f));
                if (en && has && e.type == EventType.MouseUp && e.button == 0 && r.Contains(e.mousePosition) && !dragging) { OpenInfo(-1, es); e.Use(); }
            }
            // BOOSTS / POTIONS
            if (rep) { if (emptySt != null) { Skin.Bg(new Rect(0, 558, 64, 30), emptySt.N, emptySt); Skin.Bg(new Rect(0, 588, 64, 30), emptySt.N, emptySt); } Tx(new Rect(32, 563, 30, 28), "BoostIcon"); Tx(new Rect(32, 592, 30, 28), "PostionIcon"); }
            Skin.Label(new Rect(0, 556, 60, 19), Loc.T("BOOSTS"), ef != null ? Right(ef) : null); Skin.Label(new Rect(4, 566, 64, 30), hud != null ? hud.Data.BatteryWpn.ToString() : "0", lab);
            Skin.Label(new Rect(0, 585, 60, 19), Loc.T("POTIONS"), ef != null ? Right(ef) : null); Skin.Label(new Rect(4, 596, 64, 30), hud != null ? hud.Data.BatteryNano.ToString() : "0", lab);
            GUI.EndGroup();
        }

        // ============================================================ pestana NANOS ============================================================
        void BuildNanoList()
        {
            nanoList = new List<int>(); var t = NT; if (t == null || t.m_pNanoData == null) return;
            int top = Mathf.Min(Mathf.Max(nanoBankSize, 37), t.m_pNanoData.Length);
            for (int id = 1; id < top; id++) { var n = t.m_pNanoData[id]; if (n != null && n.m_iNanoSet > 0) nanoList.Add(id); }
            nanoList.Sort((a, b) => { int c = t.m_pNanoData[a].m_iNanoSet.CompareTo(t.m_pNanoData[b].m_iNanoSet); return c != 0 ? c : a.CompareTo(b); });
        }
        int NanoRowsCount() { if (nanoList == null) BuildNanoList(); return (nanoList.Count + Cols - 1) / Cols; }

        /// <summary>Rejilla de nanos dentro del grupo Content (coordenadas locales). Clic = ficha del nano.</summary>
        void DrawNanoGrid(Event e, bool en)
        {
            if (nanoList == null) BuildNanoList(); bool rep = e.type == EventType.Repaint;
            var slotSt = Skin.Get(SK, "slotbox"); var t = NT; int hit = -1;
            for (int i = 0; i < nanoList.Count; i++)
            {
                int c = i % Cols, r = i / Cols; var rc = new Rect(c * Step, r * Step - scroll, Slot, Slot); if (rc.yMax < 0f || rc.y > Content.height) continue;
                int id = nanoList[i]; bool own = OwnsNano(id);
                if (rep)
                {
                    if (slotSt != null) Skin.Bg(rc, slotSt.N, slotSt);
                    var ic = NanoIconTex(t.m_pNanoData[id], !own); var ir = new Rect(rc.x + 3, rc.y + 3, rc.width - 6, rc.height - 6);
                    if (ic != null) GUI.DrawTexture(ir, ic, ScaleMode.ScaleToFit);
                    else if (!own)
                    {   // sin el icono nanoready: silueta oscura del nano + su numero
                        var ic0 = NanoIconTex(t.m_pNanoData[id], false); if (ic0 != null) { var oc = GUI.color; GUI.color = new Color(0.04f, 0.1f, 0.2f, 0.9f); GUI.DrawTexture(ir, ic0, ScaleMode.ScaleToFit); GUI.color = oc; }
                        Skin.Label(new Rect(rc.x + 28, rc.y + rc.height - 26, 60, 22), t.m_pNanoData[id].m_iNanoNumber.ToString(), St("label"), Color.white);
                    }
                }
                if (rc.Contains(e.mousePosition)) hit = id;
            }
            if (en && hit > 0 && e.type == EventType.MouseUp && e.button == 0) { nanoPop = hit; nanoPopOwned = OwnsNano(hit); Sfx.Ui("Open_Screen"); e.Use(); }
        }

        void ClosePopup() { nanoPop = -1; }

        static bool CurTuneEq(int[] tunes, int j, int skill) { return tunes != null && j < tunes.Length && tunes[j] == skill; }
        // DoNanoPopup / DoNextNanoPopup (NanoSkin 1379). Coordenadas en la ventana de 1036x654 (el original usa W/2, H/2 = 518, 327).
        void DrawNanoPopup(Event e)
        {
            bool rep = e.type == EventType.Repaint; var n = NanoRow(nanoPop); if (n == null) { nanoPop = -1; return; }
            if (rep) { var oc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.4f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = oc; }
            bool next = !nanoPopOwned; int slotIdx = -1; if (!next && hud != null) for (int k = 0; k < 3; k++) if (hud.Data.NanoSlots[k] == nanoPop) slotIdx = k;
            bool equipped = slotIdx >= 0; float gx, gy, gw, gh;
            if (next) { gx = 518f + 100f; gy = 327f - 230f; gw = 356f; gh = 460f; }
            else if (equipped)
            {
                if (rep) { var eb = UiKit.Tex("NanoPopUpEquippedBG"); if (eb != null) GUI.DrawTexture(new Rect(518f - 402.5f, 327f - 315f, 370f, 640f), eb, ScaleMode.StretchToFill, true); }
                Skin.Label(new Rect(518f - 402.5f + 13.5f, 327f - 315f + 3f, 100f, 30f), Loc.T("equipped"), Skin.Get(SN, "popupTitle"));
                gx = 518f - 400f; gy = 327f - 297f; gw = 360f; gh = 620f;
            }
            else { gx = 518f + 100f; gy = 327f - 310f; gw = 360f; gh = 620f; }
            GUI.BeginGroup(new Rect(gx, gy, gw, gh));
            if (rep) { var bg = UiKit.Tex(next ? "NanoPopUpNextNano" : "NanoPopUp"); if (bg != null) GUI.DrawTexture(new Rect(0, 0, gw, gh), bg, ScaleMode.StretchToFill, true); }
            var lb = Skin.Get(SN, "TransparentLightBlue"); var ye = Skin.Get(SN, "TransparentYellow"); var bl = Skin.Get(SN, "TransparentBlue"); var yes = Skin.Get(SN, "TransparentYellowSmall"); var lbs = Skin.Get(SN, "TransparentLightBlueSmall");
            if (Skin.Button(new Rect(next ? 323 : 327, 0, 32, 32), "", Skin.Get(SN, "exitbutton"), true)) { nanoPop = -1; GUI.EndGroup(); return; }
            if (next) { DrawNanoHead(rep, n, true, lb, ye, bl, new Rect(17, 15, 62, 62), new Rect(86, 13, 200, 15), new Rect(86, 48, 100, 15), new Rect(12, 94, 335, 27)); }
            else
            {
                Skin.Label(new Rect(20, 120, 100, 10), Loc.T("current power"), bl, new Color(0f, 0.2f, 0.4f));
                DrawNanoHead(rep, n, false, lb, ye, bl, new Rect(17, 15, 62, 62), new Rect(88, 15, 200, 15), new Rect(88, 47, 100, 15), new Rect(17, 85, 335, 27));
            }
            // tunes (la actual primero salvo en "next")
            int cur = SkillOfNano(nanoPop); int[] order = { 0, 1, 2 };
            if (!next) { int j = -1; for (int k = 0; k < 3; k++) if (CurTuneEq(n.m_iTune, k, cur)) j = k; if (j == 1) order = new[] { 1, 0, 2 }; else if (j == 2) order = new[] { 2, 0, 1 }; }
            // posiciones (icono, nombre, tipo, descripcion) por fila
            Rect[] ic = next ? new[] { new Rect(18, 139, 35, 35), new Rect(18, 221, 35, 35), new Rect(18, 304, 35, 35) } : new[] { new Rect(18, 133, 35, 35), new Rect(16, 222, 35, 35), new Rect(15, 383, 35, 35) };
            Rect[] nm = next ? new[] { new Rect(50, 143, 200, 15), new Rect(51, 226, 200, 15), new Rect(51, 309, 200, 15) } : new[] { new Rect(53, 135, 200, 15), new Rect(53, 227, 200, 15), new Rect(53, 387, 200, 15) };
            Rect[] ty = next ? new[] { new Rect(50, 158, 150, 12), new Rect(51, 241, 150, 12), new Rect(51, 324, 150, 12) } : new[] { new Rect(53, 151, 150, 12), new Rect(53, 243, 150, 12), new Rect(53, 403, 150, 12) };
            Rect[] ds = next ? new[] { new Rect(20, 180, 317, 25), new Rect(20, 261, 315, 25), new Rect(20, 345, 315, 25) } : new[] { new Rect(20, 173, 317, 27), new Rect(20, 268, 310, 40), new Rect(20, 430, 310, 40) };
            if (Machine && !next) { ds[1] = new Rect(20, 261, 213, 40); ds[2] = new Rect(20, 423, 213, 40); }
            var nt = NT; var g = Grow(hud != null ? hud.Data.Level : 0);
            for (int row = 0; row < 3; row++)
            {
                int ti = n.m_iTune != null && order[row] < n.m_iTune.Length ? n.m_iTune[order[row]] : 0; var td = nt != null && nt.m_pNanoTuneData != null && ti > 0 && ti < nt.m_pNanoTuneData.Length ? nt.m_pNanoTuneData[ti] : null; if (td == null) continue;
                var ts = nt.m_pNanoTuneStringData != null && td.m_iTuneName >= 0 && td.m_iTuneName < nt.m_pNanoTuneStringData.Length ? nt.m_pNanoTuneStringData[td.m_iTuneName] : null;
                if (rep) { var ik = SkillIconTex(td.m_iSkillID); if (ik != null) GUI.DrawTexture(ic[row], ik, ScaleMode.StretchToFill, true); }
                if (ts != null) { Skin.Label(nm[row], ts.m_strName, lb); Skin.Label(ty[row], ts.m_strComment1, yes); Skin.Label(ds[row], ts.m_strComment, bl); }
                if (!next && row > 0)
                {   // requisitos: se muestran los nombres; las barras de proporcion solo existen en la Estacion de nanos
                    float y0 = row == 1 ? 310f : 474f; var up = Skin.Get(SN, "TransparentBlue");
                    Skin.Label(new Rect(70, y0, 250, 13), Loc.T("To activate this power you need:"), up);
                    Skin.Label(new Rect(20, y0 + 18f, 140, 13), Loc.T("FUSION MATTER"), up != null ? Right(up) : null);
                    string item = ItemName(td.m_iReqItemID); if (td.m_iReqItemCount > 1) item = td.m_iReqItemCount + " x " + item;
                    Skin.Label(new Rect(20, y0 + 35f, 140, 13), item, up != null ? Right(up) : null);
                    if (Machine) DrawTuneGauges(row, ti, td, g, up);
                    else if (g != null) Skin.Label(new Rect(177, y0 + 19f, 129, 13), g.m_iReqBlob_NanoTune.ToString(), up);
                }
            }
            if (!next) { if (Machine) DrawMachineButtons(lb, slotIdx); else Skin.Label(new Rect(37, 565, 280, 33), Loc.T("To equip this Nano or change its current power, you must use a Nano Station."), lbs); }
            GUI.EndGroup();
        }
        string ItemName(int id)
        {
            var gt = tables != null ? tables.m_pGeneralItemTable : null; if (gt == null || gt.m_pItemData == null || id <= 0 || id >= gt.m_pItemData.Length || gt.m_pItemData[id] == null) return "";
            int ni = gt.m_pItemData[id].m_iItemName; return gt.m_pItemStringData != null && ni >= 0 && ni < gt.m_pItemStringData.Length && gt.m_pItemStringData[ni] != null ? gt.m_pItemStringData[ni].m_strName : "";
        }
        void DrawNanoHead(bool rep, NanoElement n, bool next, SkinStyle lb, SkinStyle ye, SkinStyle bl, Rect icon, Rect name, Rect type, Rect desc)
        {
            if (rep) { var t = NanoIconTex(n, next); if (t != null) GUI.DrawTexture(icon, t, ScaleMode.StretchToFill, true); }
            Skin.Label(name, NanoStr(n, 0), lb); Skin.Label(type, NanoStr(n, 2), ye); Skin.Label(desc, NanoStr(n, 1), bl);
        }

        // ============================================================ Estacion de nanos ============================================================
        static readonly Rect[] NanoWin2R = { new Rect(27, 440, 126, 69), new Rect(192, 440, 126, 69), new Rect(357, 440, 126, 69) };
        static readonly Rect[] NanoRt2R = { new Rect(43, 328, 128, 128), new Rect(210, 328, 128, 128), new Rect(380, 328, 128, 128) };
        static readonly Rect[] NanoType2R = { new Rect(79, 405, 59, 61), new Rect(246, 405, 59, 61), new Rect(411, 405, 59, 61) };

        Texture EnsureCloth2()
        {
            if (cloth2 != null) return cloth2; if (TexLoader == null || Time.unscaledTime < clothRetry2) return null; clothRetry2 = Time.unscaledTime + 2f;
            foreach (var ext in new[] { ".png", ".psd", ".dds", ".tga", "" }) { try { cloth2 = TexLoader("TutorialAssets/colthback2" + ext); } catch (Exception) { cloth2 = null; } if (cloth2 != null) break; }
            return cloth2;
        }

        /// <summary>Rectangulo que ocupa una textura dibujada con ScaleMode.ScaleToFit dentro de r (centrada, conserva la proporcion).</summary>
        static Rect FitRect(Texture t, Rect r)
        {
            if (t == null || t.height <= 0 || r.height <= 0f) return r;
            float ta = (float)t.width / t.height, ra = r.width / r.height;
            if (ta > ra) { float h = r.width / ta; return new Rect(r.x, r.y + (r.height - h) * 0.5f, r.width, h); }
            float w = r.height * ta; return new Rect(r.x + (r.width - w) * 0.5f, r.y, w, r.height);
        }

        /// <summary>Panel_UserClothes en modo maquina: sin avatar, circulos, vehiculo ni flechas; cuadros de nano y datos de usuario (variante "2").</summary>
        void DrawMachineLeft(Event e, bool en)
        {
            bool rep = e.type == EventType.Repaint; var back = EnsureCloth2() ?? EnsureCloth();
            if (rep) { if (back != null) GUI.DrawTexture(new Rect(0, 0, 585, 653), back, ScaleMode.ScaleToFit); else { GUI.color = new Color(0.01f, 0.06f, 0.14f, 0.9f); GUI.DrawTexture(new Rect(0, 0, 585, 653), Texture2D.whiteTexture); GUI.color = Color.white; } }
            if (hud == null) return;
            GUI.BeginGroup(new Rect(8, 7, 512, 649));
            var lab = St("label"); var lblue = St("lightbluesmalllabel");
            // NPC de la estacion (cmNanoMachine, Rect 0,0,500,300)
            if (back != null) { NanoModelRt.StationBackdrop = back; NanoModelRt.StationBackdropFit = FitRect(back, new Rect(0, 0, 585, 653)); NanoModelRt.StationBackdropView = new Rect(8, 7, 500, 300); }   // fondo que el original deja bajo la camara (DepthOnly) para sumar lo aditivo
            Texture nt = NpcModel != null && machineNpc > 0 ? NpcModel(machineNpc) : null;
            if (rep && nt != null) GUI.DrawTexture(new Rect(0, 0, 500, 300), nt, ScaleMode.StretchToFill, true);
            else if (nt == null) Skin.Label(new Rect(20, 120, 460, 40), machineName, lab, Cy);
            for (int i = 0; i < 3; i++) DrawNanoWin(i, rep, NanoWin2R[i]);
            for (int i = 0; i < 3; i++)
            {
                int id = hud.Data.NanoSlots[i]; if (id <= 0 || !rep) continue; var r = NanoRt2R[i]; Texture t = NanoModel != null ? NanoModel(id) : null;
                if (t != null) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
                else { var ic = NanoIconTex(NanoRow(id), false); if (ic != null) GUI.DrawTexture(new Rect(r.x + 32, r.y + 32, 64, 64), ic, ScaleMode.ScaleToFit); }
                var n = NanoRow(id); if (n != null) Tx(NanoType2R[i], TypeIcon[Mathf.Clamp(n.m_iStyle, 0, 2)]);
            }
            // datos de usuario: el original dibuja DrawUserInfp (rectUserInfo) tambien en modo maquina; rectUserInfo2 no se usa
            DrawUserInfo(rep, lab, St("whitesmalllabel"), lblue);
            GUI.EndGroup();
        }

        /// <summary>Unidades del objeto general (tipo 7) en el inventario (suma de iOpt).</summary>
        static int CountItem(int id) { int n = 0; for (int i = 0; i < 50; i++) if (Items[i].Type == 7 && Items[i].Id == id) n += Mathf.Max(1, Items[i].Opt); return n; }

        /// <summary>Medidores de materia de fusion / objeto y boton ACTIVATE de una fila de poder (NanoMachineScript.DoNanoPopup).</summary>
        void DrawTuneGauges(int row, int tuneId, NanoTuneElement td, AvatarGrowElement g, SkinStyle up)
        {
            int fm = Mathf.Max(0, hud.Data.FusionMatter), fmReq = g != null ? g.m_iReqBlob_NanoTune : 0, have = CountItem(td.m_iReqItemID), need = Mathf.Max(1, td.m_iReqItemCount);
            float fr = fmReq > 0 ? (float)fm / fmReq : 1f, ir = (float)have / need;
            float x = row == 1 ? 177f : 176f, yF = row == 1 ? 331f : 492f, yI = row == 1 ? 348f : 509f; bool rep = Event.current.type == EventType.Repaint;
            if (rep)
            {
                var bf = UiKit.Tex("NanoPopupFMBar"); var bi = UiKit.Tex("NanoPopupItemBar");
                if (bf != null) GUI.DrawTexture(new Rect(x, yF, 126f * Mathf.Min(1f, fr), 9), bf, ScaleMode.StretchToFill, true);
                if (bi != null) GUI.DrawTexture(new Rect(x, yI, 126f * Mathf.Min(1f, ir), 9), bi, ScaleMode.StretchToFill, true);
            }
            var c = up != null ? Center(up) : null;
            Skin.Label(new Rect(177, yF - 2f, 129, 13), fm + " / " + fmReq, c); Skin.Label(new Rect(177, yI - 2f, 129, 13), have + " / " + need, c);
            bool ok = CanTuneNano(nanoPop, tuneId, out _, out _, out _);
            if (Skin.Button(new Rect(236, row == 1 ? 267f : 427f, 100, 26), Loc.T("ACTIVATE"), Skin.Get(SN, "button") ?? St("button"), ok)) TuneNano(tuneId, td);
        }

        void DrawMachineButtons(SkinStyle lb, int slotIdx)
        {
            var btn = Skin.Get(SN, "button") ?? St("button"); int act = hud != null ? hud.Data.ActiveNanoSlot : -1;
            if (slotIdx < 0)
            {
                Skin.Label(new Rect(155, 556, 55, 14), Loc.T("EQUIP"), lb);
                for (int k = 0; k < 3; k++) if (Skin.Button(new Rect(25 + 105 * k, 574, 101, 26), Loc.T("NANO") + " " + (k + 1), btn, act != k && !machineBusy)) { EquipNano(k + 1, nanoPop); return; }
            }
            else if (Skin.Button(new Rect(220, 574, 120, 26), Loc.T("UNEQUIP"), btn, !machineBusy)) EquipNano(0, nanoPop, slotIdx);
        }

        /// <summary>EquipNano(iSlotNum): 0 = desequipar (REQ_NANO_UNEQUIP), 1..3 = REQ_NANO_EQUIP { iNanoID, iNanoSlotNum = slot - 1 }.</summary>
        void EquipNano(int slot, int nanoId, int curSlot = -1)
        {
            byte[] p;
            if (slot == 0) { if (curSlot < 0) return; p = ClientPackets.New(PacketIds.P_CL2FE_REQ_NANO_UNEQUIP, 2); p[4] = (byte)curSlot; p[5] = 0; }
            else { p = ClientPackets.New(PacketIds.P_CL2FE_REQ_NANO_EQUIP, 4); p[4] = (byte)nanoId; p[5] = (byte)(nanoId >> 8); p[6] = (byte)(slot - 1); p[7] = 0; }
            Send(p); machineBusy = true; Sfx.Button();
        }

        /// <summary>TuneNano: REQ_NANO_TUNE { iNanoID, iTuneID, aiNeedItemSlotNum[10] } con las ranuras del objeto (tipo 7) hasta cubrir la cantidad pedida.</summary>
        void TuneNano(int tuneId, NanoTuneElement td)
        {
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_NANO_TUNE, 44); p[4] = (byte)nanoPop; p[5] = (byte)(nanoPop >> 8); p[6] = (byte)tuneId; p[7] = (byte)(tuneId >> 8);
            int n = 0, sum = 0;
            for (int i = 0; i < 50 && n < 10; i++)
            {
                if (Items[i].Type != 7 || Items[i].Id != td.m_iReqItemID) continue;
                ClientPackets.PutInt(p, 8 + 4 * n, i); n++; sum += Mathf.Max(1, Items[i].Opt); if (sum >= td.m_iReqItemCount) break;
            }
            Sfx.Ui("Add_Attribute"); Send(p); machineBusy = true;
        }
    }
}
