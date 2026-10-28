// NanoWheelHud: reconstruccion de la rueda de nanos A/B/C del HUD del cliente original (FusionFall / Retrobution, Unity Web Player 2.5).
//
// ORIGINAL (decompilado, Assembly-CSharp):
//   cnNanoWheel.OnOwnGUI / DoWindow / CheckIcons / Update / SetNanoGumBallEffect  -> rueda: fondo nano_whell, marco por slot (blue/red/yellow_icon), icono del nano, barra de estamina,
//                                                                                    letra A/B/C, icono de skill con cooldown circular, boton con el numero de tecla (but_1..3), gumball
//   cnOwnAvatarStatus.Update (lineas ~945-1035)    -> teclas Nano1/2/3 (Alpha1..3 por defecto, ConfigurableInput) y envio de REQ_NANO_ACTIVE
//   cnOwnAvatarStatus.ReceiveNanoActive / ReceiveNanoEquip / ReceiveNanoUnEquip / ReceiveNanoTune / ReceiveNanoCreate / UpdateAttrib (cooltime max)  -> estado de slots
//   cnAvatarStatus.NanoSkillUseSucc                -> estamina del nano tras usar una skill; <= 0 => nano desactivado
//   cnAvatarAttack.CoolTimeCheck                   -> cooldown de las skills de los OTROS slots al usar una skill (mismo m_iCoolType)
//   CnGuiNano_info.DoWindow                        -> el disco de cooldown sobre el icono de skill de la ventana del nano activo (la ventana en si la dibuja PlayerHud)
//   AvatarUtil.GetIconName                         -> tabla Nano/Skill -> IconElement -> "nanoicon_NN" / "skillicon_NN" (Icons/<nombre>.png)
//
// Maquetacion: espacio de diseno del original; s = Screen.height / 681 (FFGUIUtility.GetUiScale, uiScaleFactor = 1), pivote BottomRight (ScreenPivot 2) para la rueda y TopLeft para el disco de nano_info.
// La rueda es un GUI.BeginGroup (recorta lo que sobresale: gumball por arriba, icono de skill del slot A por la izquierda), igual que el original.
//
// Dependencias: PlayerHud (HudData: NanoSlots, NanoStamina, ActiveNanoSlot, NanoBankStamina, Hp), PacketDispatcher (evento Unhandled + SelfEnter + SelfEnterRaw), NpcUI.Sender (envio),
// ClientPackets.New, PacketIds, OpenFusion.Tables.Gen (XdtTableScript.m_pNanoTable / m_pSkillTable).
// Texturas (Resources/OpenFusion/UI/Textures/): nano_whell, blue_icon, red_icon, yellow_icon, a_icon, b_icon, c_icon, but_1..3, buff_icon_back, cooltime01..07, CoolTime_00, nano_st_bar, HP_BAR, NanoGumballBlue/Red/Yellow.
// Iconos (Resources/OpenFusion/Icons/, ver IconPath): nanoicon_NN.png y skillicon_NN.png (ya volteados).
// SIN COMPILAR EN UNITY (revisado a mano contra la API 2022.3 LTS, C# 9, sin librerias externas).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class NanoWheelHud : MonoBehaviour
    {
        // ---------------- API publica ----------------
        /// <summary>cnNanoWheel.bRender.</summary>
        public bool Visible = true;
        /// <summary>El original solo maneja teclado (Nano1/2/3). En movil hace falta tocar: clic/toque izquierdo sobre un slot de la rueda = pulsar su tecla. Poner a false para el comportamiento exacto.</summary>
        public bool AllowPointer = true;
        /// <summary>Dibuja el disco de cooldown sobre el icono de skill de la ventana nano_info (CnGuiNano_info). La ventana la dibuja PlayerHud; el disco se superpone aqui.</summary>
        public bool DrawNanoInfoCooldown = true;
        /// <summary>Teclas de los slots A/B/C (ConfigurableInput.Key.Nano1..3; por defecto Alpha1..3).</summary>
        public KeyCode[] Keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
        /// <summary>No invocar si se esta escribiendo en un campo de texto IMGUI (el original comprueba MacroChat/chat). Desactivar si da falsos positivos.</summary>
        public bool RespectTextFocus = true;
        /// <summary>Hook: true = entrada bloqueada (equivale a !bInputEnable / IsSysPopUp / MoveTYPE != None / tutorial lock del original). Por defecto solo se bloquea con NpcUI.Active y HP &lt;= 0.</summary>
        public Func<bool> InputBlocked;
        /// <summary>Hook: true = el jugador va en vehiculo (ani.iVehicle &gt; 0): el original no deja invocar (suena Nano_FailSummon).</summary>
        public Func<bool> InVehicle;
        /// <summary>Hook: sonido de fallo "Nano_FailSummon" (estamina &lt;= 20 % o en vehiculo).</summary>
        public Action OnSummonFail;
        /// <summary>Hook opcional (modo "Animated Nanos", el defecto del original): textura (RenderTexture de la camara del modelo del nano) por id de nano. null = se usa el icono nanoicon_NN.</summary>
        public Func<int, Texture> NanoModelProvider;
        /// <summary>Hook opcional para cargar iconos por nombre ("nanoicon_01"); null = Resources.Load&lt;Texture2D&gt;(IconPath + nombre).</summary>
        public Func<string, Texture> IconLoader;
        public string IconPath = "OpenFusion/Icons/";
        /// <summary>Retardo minimo entre invocaciones: GameCondition.fArrCoolTimeMax[9] = 0.5 s.</summary>
        public float SummonDelay = 0.5f;

        public const float UiRefH = 681f;
        public const int SlotCount = 3;
        public const int NanoBankSize = 37;   // csDefines.SIZEOF_NANO_BANK_SLOT

        /// <summary>Engancha el modulo a la red y a los datos del HUD. Llamar una vez creado el PlayerHud (ver REPORT.md).</summary>
        public void Init(PlayerHud hud, PacketDispatcher dispatcher, XdtTableScript tables)
        {
            Detach();
            this.hud = hud; this.dispatcher = dispatcher; this.tables = tables;
            nanoT = tables != null ? tables.m_pNanoTable : null;
            skillT = tables != null ? tables.m_pSkillTable : null;
            for (int i = 0; i < SlotCount; i++) { slots[i].NanoId = -1; slots[i].SkillId = -1; }
            if (dispatcher != null) { dispatcher.Unhandled += OnPacket; dispatcher.SelfEnter += OnSelfEnter; dispatcher.SelfEnterRaw += OnEnterRaw; }
            // La ventana nano_info (PlayerHud) pide nombre e icono de skill por id de nano: se los damos si nadie lo ha hecho.
            if (hud != null)
            {
                if (hud.NanoNameProvider == null) hud.NanoNameProvider = NanoName;
                if (hud.NanoSkillIconProvider == null) hud.NanoSkillIconProvider = NanoSkillIcon;
            }
        }

        public void Detach()
        {
            if (dispatcher != null) { dispatcher.Unhandled -= OnPacket; dispatcher.SelfEnter -= OnSelfEnter; dispatcher.SelfEnterRaw -= OnEnterRaw; }
            dispatcher = null;
        }
        void OnDestroy() { Detach(); }

        /// <summary>cnNanoWheel.SetNanoGumBallEffect: -1 apaga los tres, n enciende el gumball del slot n. En el original nadie lo invoca en los scripts extraidos (evento cnMainGame 28), asi que aqui solo es una API.</summary>
        public void SetGumBallEffect(int slot)
        {
            if (slot < 0) { for (int i = 0; i < SlotCount; i++) slots[i].Gum = false; }
            else if (slot < SlotCount) slots[slot].Gum = true;
        }

        /// <summary>Gumball aplicado al nano del slot: buff de la skill `skillId` (el servidor usa la 144) con su durationTime[0] (unidades de 0,1 s: durationTime * 100 ms). Muestra el temporizador mm:ss sobre la rueda.</summary>
        public void StartGumTimer(int slot, int skillId)
        {
            if (slot < 0 || slot >= SlotCount) return;
            var sk = SkillAt(skillId); float sec = sk != null && sk.m_iDurationTime != null && sk.m_iDurationTime.Length > 0 ? sk.m_iDurationTime[0] * 0.1f : 0f;
            if (sec <= 0f) return;
            slots[slot].Gum = true; slots[slot].GumEnd = Time.time + sec;
        }
        public float GumballRemaining(int slot) => slot >= 0 && slot < SlotCount ? Mathf.Max(0, slots[slot].GumEnd - Time.time) : 0;
        static string GumTime(float rem) { int t = Mathf.Max(0, Mathf.CeilToInt(rem)); return (t / 60).ToString("00") + ":" + (t % 60).ToString("00"); }
        GUIStyle gumStyle;

        /// <summary>cnAvatarAttack.CoolTimeCheck: al usar una skill de nano con m_iCoolType != 0, los OTROS slots con el mismo m_iCoolType entran en cooldown. Se llama solo al recibir NANO_SKILL_USE_SUCC propio; es publico por si se prefiere llamarlo al enviar la peticion.</summary>
        public void NotifyNanoSkillUsed(int coolType)
        {
            if (hud == null || coolType == 0) return;
            for (int i = 0; i < SlotCount; i++)
                if (i != hud.Data.ActiveNanoSlot && hud.Data.NanoSlots[i] > 0 && slots[i].Has && slots[i].CoolType == coolType) slots[i].CoolCur = Time.time;
        }

        /// <summary>Equivale a pulsar la tecla del slot (cnOwnAvatarStatus.Update). true si se envio REQ_NANO_ACTIVE.</summary>
        public bool RequestSlot(int slot)
        {
            if (hud == null || slot < 0 || slot >= SlotCount) return false;
            var d = hud.Data;
            if (InVehicle != null && InVehicle()) { if (OnSummonFail != null) OnSummonFail(); return false; }
            if (Time.time - lastSummon < SummonDelay || Time.time < VehicleRide.NanoCoolUntil) return false;           // GameCondition.IsCoolTime(9)
            short req; bool send = false;
            if (d.ActiveNanoSlot != slot)
            {
                req = (short)slot;
                int id = d.NanoSlots[slot];
                if (id > 0)
                {
                    var nano = NanoAt(id);
                    int max = nano != null ? nano.m_iNanoBattery1 : 0; int st = d.NanoStamina[slot];
                    if (max > 0 && (float)st / (float)max > 0.2f) send = true;      // > 20 % de estamina
                    else if (OnSummonFail != null) OnSummonFail();
                }
            }
            else
            {
                req = -1;                                                           // mismo slot: desactivar
                if (d.ActiveNanoSlot >= 0) send = true;
            }
            if (!send) return false;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_NANO_ACTIVE, 2);       // sP_CL2FE_REQ_NANO_ACTIVE { short iNanoSlotNum } (Pack 2, 2 B)
            BitConverter.GetBytes(req).CopyTo(p, 4);
            var sender = NpcUI.Sender;
            if (sender == null) return false;
            sender(p); lastSummon = Time.time;                                      // GameCondition.SetUpdateCoolTime(9)
            return true;
        }

        // ---------------- estado ----------------
        PlayerHud hud; PacketDispatcher dispatcher; XdtTableScript tables; NanoTableScript nanoT; SkillTableScript skillT;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        volatile int selfId = int.MinValue;
        float lastSummon = -100f;
        readonly short[] bankSkill = new short[NanoBankSize];   // aNanoBank[id].iSkillID (la estamina vive en hud.Data.NanoBankStamina / NanoStamina)

        sealed class Slot
        {
            public int NanoId = -1, SkillId = -1;       // lo que mostraba la ultima reconstruccion (nanoids[] / nanoskillids[])
            public bool Has;                            // slot con nano valido
            public int Style = -1;                      // NanoElement.m_iStyle: 0 azul, 1 rojo, 2 amarillo
            public float MaxStamina;                    // NanoElement.m_iNanoBattery1
            public bool ActiveSkill;                    // SkillElement.m_iBatteryDrainType == 1
            public float CoolMax;                       // SkillElement.m_iCoolTime * 0.1 (UpdateAttrib -> GameCondition max 6+slot)
            public int CoolType;                        // SkillElement.m_iCoolType
            public float CoolCur = -1000000f;           // GameCondition.fArrCurCoolTime[6+slot]
            public Texture NanoIcon, SkillIcon;
            public float Remain = 1f;                   // nanoStaminaRemain
            public bool Gum;                            // GumBallEnabled
            public float GumEnd;                        // Time.time en que acaba el buff del gumball (0 = sin temporizador)
        }
        readonly Slot[] slots = { new Slot(), new Slot(), new Slot() };

        // ---------------- red (hilo de red -> cola -> Update) ----------------
        void OnSelfEnter(EnterSucc e) { selfId = e.Id; for (int i = 0; i < SlotCount; i++) { slots[i].GumEnd = 0f; slots[i].Gum = false; } }
        void OnEnterRaw(byte[] body)
        {
            var c = body;
            q.Enqueue(() =>
            {
                // iID(4) + sPCLoadData2CL: aNanoBank @ 4 + 1444, 37 x sNano{short iID, short iSkillID, short iStamina} (offsets de PlayerHud.HudPackets, ver su comentario)
                if (c == null || c.Length < 4 + 1444 + NanoBankSize * 6) return;
                for (int i = 0; i < NanoBankSize; i++) bankSkill[i] = BitConverter.ToInt16(c, 4 + 1444 + i * 6 + 2);
            });
        }
        static bool Relevant(uint t)
        {
            return t == PacketIds.P_FE2CL_REP_NANO_ACTIVE_SUCC || t == PacketIds.P_FE2CL_REP_NANO_EQUIP_SUCC || t == PacketIds.P_FE2CL_REP_NANO_UNEQUIP_SUCC
                || t == PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC || t == PacketIds.P_FE2CL_REP_PC_NANO_CREATE_SUCC || t == PacketIds.P_FE2CL_REP_CHARGE_NANO_STAMINA
                || t == PacketIds.P_FE2CL_REP_PC_TICK || t == PacketIds.P_FE2CL_REP_PC_REGEN_SUCC || t == PacketIds.P_FE2CL_NANO_SKILL_USE_SUCC;
        }
        void OnPacket(uint type, byte[] body) { if (Relevant(type)) q.Enqueue(() => Handle(type, body)); }

        static short S(byte[] b, int o) { return BitConverter.ToInt16(b, o); }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        void SetBankStamina(int id, short st) { var bk = hud.Data.NanoBankStamina; if (bk != null && id >= 0 && id < bk.Length) bk[id] = st; }
        short BankStamina(int id) { var bk = hud.Data.NanoBankStamina; return bk != null && id >= 0 && id < bk.Length ? bk[id] : (short)0; }

        void Handle(uint type, byte[] b)
        {
            if (hud == null || b == null) return;
            var d = hud.Data;
            try
            {
                switch (type)
                {
                    case PacketIds.P_FE2CL_REP_NANO_ACTIVE_SUCC:
                    {   // sP_FE2CL_REP_NANO_ACTIVE_SUCC (8 B, Pack 4): short iActiveNanoSlotNum @0, int eCSTB___Add @4
                        if (b.Length < 2) break; int slot = S(b, 0);
                        d.ActiveNanoSlot = (slot >= 0 && slot < SlotCount) ? slot : -1;   // ReceiveNanoActive: iCurActiveNanoSlot = valor recibido (negativo = ninguno)
                        break;
                    }
                    case PacketIds.P_FE2CL_REP_NANO_EQUIP_SUCC:
                    {   // sP_FE2CL_REP_NANO_EQUIP_SUCC (8 B): short iNanoID @0, short iNanoSlotNum @2, int bNanoDeactive @4
                        if (b.Length < 8) break; int id = S(b, 0), slot = S(b, 2);
                        if (slot < 0 || slot >= SlotCount) break;
                        d.NanoSlots[slot] = (short)id; d.NanoStamina[slot] = BankStamina(id);
                        Sfx.Ui("Nano_Equipped");
                        if (I(b, 4) == 1) d.ActiveNanoSlot = -1;
                        break;
                    }
                    case PacketIds.P_FE2CL_REP_NANO_UNEQUIP_SUCC:
                    {   // sP_FE2CL_REP_NANO_UNEQUIP_SUCC (8 B): short iNanoSlotNum @0, int bNanoDeactive @4
                        if (b.Length < 8) break; int slot = S(b, 0);
                        if (slot < 0 || slot >= SlotCount) break;
                        d.NanoSlots[slot] = 0; d.NanoStamina[slot] = 0;
                        Sfx.Ui("Nano_Unequipped");
                        if (I(b, 4) != 0) d.ActiveNanoSlot = -1;
                        break;
                    }
                    case PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC:
                    {   // sP_FE2CL_REP_NANO_TUNE_SUCC (168 B): short iNanoID @0, short iSkillID @2, int iPC_FusionMatter @4 ...
                        if (b.Length < 8) break; int id = S(b, 0);
                        if (id >= 0 && id < NanoBankSize) bankSkill[id] = S(b, 2);
                        break;
                    }
                    case PacketIds.P_FE2CL_REP_PC_NANO_CREATE_SUCC:
                    {   // sP_FE2CL_REP_PC_NANO_CREATE_SUCC (28 B): iPC_FusionMatter @0, iQuestItemSlotNum @4, QuestItem(12 B) @8, sNano Nano @20 {iID, iSkillID, iStamina}, iPC_Level @26
                        if (b.Length < 26) break; int id = S(b, 20);
                        if (id >= 0 && id < NanoBankSize) { bankSkill[id] = S(b, 22); SetBankStamina(id, S(b, 24)); }
                        break;
                    }
                    case PacketIds.P_FE2CL_REP_CHARGE_NANO_STAMINA:
                    {   // 8 B: iBatteryN @0, iNanoID @4, iNanoStamina @6 (los slots ya los actualiza PlayerHud; aqui solo el banco)
                        if (b.Length < 8) break; SetBankStamina(S(b, 4), S(b, 6));
                        Sfx.Ui("Nano_Potion");
                        break;
                    }
                    case PacketIds.P_FE2CL_REP_PC_TICK:
                    {   // 32 B: iHP @0, aNano[3] @4 (sNano 6 B), iBatteryN @24. Trae id + skill + estamina de los 3 slots.
                        if (b.Length < 28) break;
                        HudPackets.ApplySlotStamina(b, 4, d);
                        break;
                    }
                    case PacketIds.P_FE2CL_REP_PC_REGEN_SUCC:
                    {   // 48 B: Nanos[3] @22 (sNano 6 B)
                        if (b.Length < 48) break;
                        HudPackets.ApplySlotStamina(b, 22, d);
                        break;
                    }
                    case PacketIds.P_FE2CL_NANO_SKILL_USE_SUCC:
                    {   // 36 B (Pack 4): iPC_ID @0, sbyte iBulletID @4, short iSkillID @6, iArg1..3 @8/12/16, int bNanoDeactive @20, short iNanoID @24, short iNanoStamina @26, eST @28, iTargetCnt @32
                        if (b.Length < 36 || I(b, 0) != selfId) break;
                        int skill = S(b, 6), nid = S(b, 24); short st = S(b, 26);
                        SetBankStamina(nid, st);
                        for (int i = 0; i < SlotCount; i++) if (d.NanoSlots[i] == nid) d.NanoStamina[i] = st;
                        if (d.ActiveNanoSlot >= 0 && (st <= 0 || I(b, 20) != 0)) d.ActiveNanoSlot = -1;   // NanoSkillUseSucc: iNanoStamina <= 0 => se desactiva el nano
                        var se = SkillAt(skill);
                        if (se != null) {
                            Refresh();
                            for (int i = 0; i < SlotCount; i++) if (d.NanoSlots[i] == nid && nid > 0 && nid < bankSkill.Length && bankSkill[nid] == skill) slots[i].CoolCur = Time.time;
                            NotifyNanoSkillUsed(se.m_iCoolType);
                        }
                        break;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[nanowheel] paquete " + type + ": " + e.Message); }
        }

        // ---------------- tablas ----------------
        NanoElement NanoAt(int id) { return nanoT != null && nanoT.m_pNanoData != null && id > 0 && id < nanoT.m_pNanoData.Length ? nanoT.m_pNanoData[id] : null; }
        // --- API para NanoSystem (uso de skills) ---
        public int BankSkill(int nanoId) { return nanoId > 0 && nanoId < NanoBankSize ? bankSkill[nanoId] : 0; }
        public SkillElement SkillOf(int id) { return SkillAt(id); }
        public NanoElement NanoRow(int id) { return NanoAt(id); }
        /// GameCondition.IsCallCoolTime(6+slot): true si el slot sigue en cooldown; si no, arranca el cooldown y devuelve false.
        public bool CallCool(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return true; var sl = slots[slot];
            if (sl.CoolMax > 0f && Time.time - sl.CoolCur < sl.CoolMax) return true;
            sl.CoolCur = Time.time; return false;
        }
        public bool IsCooling(int slot) { if (slot < 0 || slot >= SlotCount) return true; var sl = slots[slot]; return sl.CoolMax > 0f && Time.time - sl.CoolCur < sl.CoolMax; }
        public float CooldownRemaining(int slot) { if (slot < 0 || slot >= SlotCount) return 0; var s = slots[slot]; return Mathf.Max(0, s.CoolMax - (Time.time - s.CoolCur)); }
        // Native input uses explicit text/modal state, not an IMGUI login field's retained focus ID.
        public bool CanSummonFromUi => hud != null && hud.Data.Hp > 0 && !NpcUI.Active && !GameMenu.Blocking &&
            !WelcomePopup.Active && !ChatHud.Typing && !EmailWindow.Typing && !InventoryWindow.Typing && !ControlMap.Capturing &&
            (InputBlocked == null || !InputBlocked());
        public bool RequestSlotFromUi(int slot) => CanSummonFromUi && RequestSlot(slot);
        // Equipped cards explicitly summon. Re-selecting the active Nano must not toggle it off.
        public bool CanSummonFromCard => CanSummonFromUi || (hud != null && hud.Data.Hp > 0 && GameMenu.NanoStationOnly &&
            !NpcUI.Active && !WelcomePopup.Active && !ChatHud.Typing && !EmailWindow.Typing && !InventoryWindow.Typing && !ControlMap.Capturing &&
            InventoryWindow.Instance != null && !InventoryWindow.Instance.DualNanoBusy && (InputBlocked == null || !InputBlocked()));
        public bool RequestSummonFromCard(int slot, int expectedId)
        {
            if (!CanSummonFromCard || slot < 0 || slot >= SlotCount || expectedId <= 0 ||
                hud.Data.NanoSlots[slot] != expectedId || hud.Data.ActiveNanoSlot == slot) return false;
            return RequestSlot(slot);
        }

        SkillElement SkillAt(int id) { return skillT != null && skillT.m_pSkillData != null && id > 0 && id < skillT.m_pSkillData.Length ? skillT.m_pSkillData[id] : null; }
        IconElement NanoIconElement(NanoElement n) { return n != null && nanoT.m_pNanoIconData != null && n.m_iIcon1 >= 0 && n.m_iIcon1 < nanoT.m_pNanoIconData.Length ? nanoT.m_pNanoIconData[n.m_iIcon1] : null; }
        IconElement SkillIconElement(SkillElement s) { return s != null && skillT.m_pSkillIconData != null && s.m_iIcon >= 0 && s.m_iIcon < skillT.m_pSkillIconData.Length ? skillT.m_pSkillIconData[s.m_iIcon] : null; }

        /// <summary>AvatarUtil.GetIconName: m_iIconType -> prefijo.</summary>
        public static string IconName(IconElement e)
        {
            if (e == null) return "";
            string p;
            switch (e.m_iIconType)
            {
                case 0: p = "wpnicon_"; break; case 1: p = "nanoicon_"; break; case 2: p = "skillicon_"; break; case 3: p = "cosicon_"; break;
                case 4: p = "npcicon_"; break; case 5: p = "nanoready_"; break; case 6: p = "questitemicon_"; break; case 7: p = "generalitemicon_"; break;
                case 8: p = "mobicon_"; break; case 9: p = "fusionicon_"; break; case 10: p = "hnpcicon_"; break; case 11: p = "transport_"; break; case 12: p = "vehicle_"; break;
                default: return "";
            }
            return p + e.m_iIconNumber.ToString("00");
        }

        string NanoName(int id)
        {
            var n = NanoAt(id);
            if (n != null && nanoT.m_pNanoStringData != null && n.m_iNanoName >= 0 && n.m_iNanoName < nanoT.m_pNanoStringData.Length && nanoT.m_pNanoStringData[n.m_iNanoName] != null) return nanoT.m_pNanoStringData[n.m_iNanoName].m_strName;
            return "Nano " + id;
        }
        Texture NanoSkillIcon(int id)
        {
            if (id <= 0 || id >= NanoBankSize) return null;
            return Icon(IconName(SkillIconElement(SkillAt(bankSkill[id]))));
        }

        // ---------------- recursos ----------------
        const string UI = "OpenFusion/UI/Textures/";
        readonly Dictionary<string, Texture> tex = new Dictionary<string, Texture>(); readonly HashSet<string> missing = new HashSet<string>();
        Texture Load(string key, string path, bool icon)
        {
            Texture t; if (tex.TryGetValue(key, out t)) return t; if (missing.Contains(key)) return null;
            if (icon && IconLoader != null) t = IconLoader(key); else t = Resources.Load<Texture2D>(path);
            if (t == null) { missing.Add(key); Debug.LogWarning("[nanowheel] falta textura " + path); return null; }
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp; tex[key] = t; return t;
        }
        Texture T(string n) { return Load(n, UI + n, false); }
        Texture Icon(string n) { return string.IsNullOrEmpty(n) ? null : Load(n, IconPath + n, true); }

        // ---------------- reconstruccion de slots (cnNanoWheel.CheckIcons + Update) ----------------
        void Refresh()
        {
            var d = hud.Data;
            for (int i = 0; i < SlotCount; i++)
            {
                var s = slots[i]; int id = d.NanoSlots[i]; int sk = id > 0 && id < NanoBankSize ? bankSkill[id] : 0;
                if (s.NanoId != id || s.SkillId != sk)
                {
                    s.NanoId = id; s.SkillId = sk; s.NanoIcon = null; s.SkillIcon = null; s.Style = -1; s.MaxStamina = 0f; s.ActiveSkill = false; s.CoolMax = 0f; s.CoolType = 0; s.Has = false;
                    var nano = NanoAt(id);
                    if (nano != null)
                    {
                        s.Has = true; s.Style = Mathf.Clamp(nano.m_iStyle, 0, 2); s.MaxStamina = nano.m_iNanoBattery1;
                        s.NanoIcon = Icon(IconName(NanoIconElement(nano)));
                        var skill = SkillAt(sk);
                        if (skill != null)
                        {
                            s.ActiveSkill = skill.m_iBatteryDrainType == 1; s.CoolMax = skill.m_iCoolTime * 0.1f; s.CoolType = skill.m_iCoolType;
                            s.SkillIcon = Icon(IconName(SkillIconElement(skill)));
                        }
                    }
                }
                s.Remain = s.Has && s.MaxStamina > 0f ? Mathf.Min(1f, Mathf.Max(0f, (float)d.NanoStamina[i] / s.MaxStamina)) : 1f;
            }
        }

        void Update()
        {
            Action a; int guard = 0; while (guard++ < 64 && q.TryDequeue(out a)) { try { a(); } catch (Exception e) { Debug.LogWarning("[nanowheel] " + e.Message); } }
            if (hud == null) return;
            Refresh();
            PollKeys();
        }

        // ---------------- entrada (cnOwnAvatarStatus.Update) ----------------
        bool Blocked()
        {
            if (hud.Data.Hp <= 0) return true;                       // status.iCurHP > 0
            if (NpcUI.Active) return true;                           // equivalente a cnSystemMessageManager.IsSysPopUp() (dialogo modal)
            if (RespectTextFocus && GUIUtility.keyboardControl != 0) return true;
            return InputBlocked != null && InputBlocked();
        }
        void PollKeys()
        {
            if (Blocked()) return;
            for (int i = 0; i < SlotCount && i < 3; i++)
                if (ControlMap.Down((ControlMap.K)((int)ControlMap.K.Nano1 + i))) { RequestSlot(i); break; }   // el original toma el ultimo pulsado en el mismo frame (num); con uno por frame es igual
        }

        // ---------------- maquetacion (bindings.json cnNanoWheel; Start() sobrescribe ABCBackRect) ----------------
        static readonly Rect WindowRect = new Rect(0f, 0f, 223f, 37f);
        static readonly Rect[] ABCIconRect = { new Rect(50f, 35f, 15f, 15f), new Rect(127f, 35f, 15f, 15f), new Rect(204f, 35f, 15f, 15f) };
        static readonly Rect[] SlotIconRect = { new Rect(18f, 75f, 36f, 36f), new Rect(96f, 75f, 36f, 36f), new Rect(172f, 75f, 36f, 36f) };
        static readonly Rect[] ABCBackRect = { new Rect(6f, 16f, 56f, 72f), new Rect(84f, 16f, 56f, 72f), new Rect(160f, 16f, 56f, 72f) };   // los de Start(), no los serializados (7/83/160)
        static readonly Rect[] NanoRect = { new Rect(8f, 20f, 64f, 64f), new Rect(85f, 20f, 64f, 64f), new Rect(162f, 20f, 64f, 64f) };
        static readonly Rect[] SkillRect = { new Rect(-5f, 5f, 26f, 26f), new Rect(72f, 5f, 26f, 26f), new Rect(149f, 5f, 26f, 26f) };
        static readonly Rect[] GumBallRect = { new Rect(-3f, -27f, 71f, 104f), new Rect(74f, -27f, 71f, 104f), new Rect(151f, -27f, 71f, 104f) };
        static readonly string[] ABCBackTex = { "blue_icon", "red_icon", "yellow_icon" };
        static readonly string[] ABCIconTex = { "a_icon", "b_icon", "c_icon" };
        static readonly string[] GumBallTex = { "NanoGumballBlue", "NanoGumballRed", "NanoGumballYellow" };
        static readonly string[] SlotIconTex = { "but_1", "but_2", "but_3" };
        static readonly string[] CoolTex = { "cooltime01", "cooltime02", "cooltime03", "cooltime04", "cooltime05", "cooltime06", "cooltime07" };
        // cnNanoWheel.OnOwnGUI: RenderRect = (W - 223 - 20, H - 37 - 64 - 10, 223, 37 + 64), pivote BottomRight.
        static readonly Vector2 GroupOffset = new Vector2(223f + 20f, 37f + 64f + 10f);   // distancia del origen del grupo a la esquina inferior derecha de la pantalla
        static readonly Vector2 GroupSize = new Vector2(223f, 37f + 64f);

        float sc;                        // escala de UI del frame actual
        Rect GroupRectScreen()
        {
            float W = Screen.width, H = Screen.height; float s = GameOptions.UiScale;
            // p' = pivot + (p - pivot) * s  con pivot = (W, H)
            float gx = W - GroupOffset.x, gy = H - GroupOffset.y;
            return new Rect(W + (gx - W) * s, H + (gy - H) * s, GroupSize.x * s, GroupSize.y * s);
        }
        Rect R(Rect r) { return new Rect(r.x * sc, r.y * sc, r.width * sc, r.height * sc); }   // coordenadas dentro del grupo -> pixeles
        void Draw(Rect r, string name) { var t = T(name); if (t != null) GUI.DrawTexture(R(r), t, ScaleMode.StretchToFill, true); }
        void DrawIcon(Rect r, Texture t) { if (t != null) GUI.DrawTexture(R(r), t, ScaleMode.ScaleToFit, true); }

        void OnGUI()
        {
            if (DualScreenController.ShowSecondaryNanos) return;
            if (GameMenu.HudHidden) return;
            if (hud == null || !Visible || !hud.Visible || Event.current == null) return;
            if (string.IsNullOrEmpty(hud.Data.Name)) return;                      // aun no hay jugador (igual que PlayerHud)
            var e = Event.current; sc = GameOptions.UiScale; var gs = GroupRectScreen();

            if (e.type == EventType.MouseDown && e.button == 0 && AllowPointer)
            {
                var m = e.mousePosition;
                for (int i = 0; i < SlotCount; i++)
                {
                    var hit = Union(R(ABCBackRect[i]), R(SlotIconRect[i])); hit.x += gs.x; hit.y += gs.y;
                    if (hit.Contains(m)) { if (!Blocked()) RequestSlot(i); e.Use(); return; }
                }
            }
            if (e.type != EventType.Repaint) return;

            var oldColor = GUI.color; var oldMatrix = GUI.matrix; GUI.color = Color.white; GUI.depth = 9;   // original: 11 (detras de PlayerHud=10); aqui delante para que el disco de nano_info quede sobre su icono. No se solapan.
            var d = hud.Data;
            for (int gi = 0; gi < SlotCount; gi++) if (slots[gi].GumEnd > 0f && Time.time >= slots[gi].GumEnd) { slots[gi].GumEnd = 0f; slots[gi].Gum = false; }
            { var gx = gs; gx.x -= 5f * sc; gx.width += 5f * sc; GUI.BeginGroup(gx); GUI.matrix = oldMatrix * Matrix4x4.Translate(new Vector3(5f * sc, 0f, 0f)); }   // 5 px de margen a la izquierda: el icono de skill del slot A (x=-5) ya no se corta
            Draw(new Rect(WindowRect.x, WindowRect.y + 64f, WindowRect.width, WindowRect.height), "nano_whell");
            for (int i = 0; i < SlotCount; i++)
            {
                var s = slots[i];
                if (s.Has)
                {
                    int st = s.Style;
                    if (s.Gum) Draw(GumBallRect[i], GumBallTex[st]);
                    Draw(ABCBackRect[i], ABCBackTex[st]);
                    bool notActive = i != d.ActiveNanoSlot;
                    bool low = s.Remain < 0.2f && notActive;
                    Texture model = GameOptions.Cur.AnimatedNanos && NanoModelProvider != null ? NanoModelProvider(s.NanoId) : null;   // bAnimatedNanos: cámara 3D viva o icono estático
                    if (model != null) NanoModelRt.SetSink(s.NanoId, low ? 0.48f * (1f - s.Remain / 0.2f) : 0f);   // DrawNanoCamera: offset (0, 0.48*(1-estamina/0.2), 0) con < 20 %
                    Rect nr = NanoRect[i];
                    if (model != null) { nr.x -= 8f; nr.y -= 8f; nr.width *= 1.125f; nr.height *= 1.125f; }   // modo "Animated Nanos": la camara se pinta un 12.5 % mayor
                    Texture art = model != null ? model : s.NanoIcon;
                    if (art != null)
                    {
                        if (low && model == null)
                        {   // el nano "se hunde": se recorta la parte baja segun la estamina restante (< 20 %)
                            float num = 24f + (nr.height - 24f) * s.Remain / 0.2f;
                            var cut = nr; cut.y += nr.height - num; cut.height = num;
                            GUI.BeginGroup(R(cut));
                            GUI.DrawTexture(R(new Rect(0f, 0f, nr.width, nr.height)), art, ScaleMode.ScaleToFit, true);
                            GUI.EndGroup();
                        }
                        else DrawIcon(nr, art);
                    }
                    if (notActive && s.Remain < 0.99f)
                    {   // nano_st_bar (42x6) + relleno HP_BAR (40 * restante x 4)
                        var br = NanoRect[i]; br.x += 14f; br.y -= 3f; br.width = 42f; br.height = 6f;
                        Draw(br, "nano_st_bar");
                        br.width = 40f * s.Remain; br.x += 1f; br.y += 1f; br.height = 4f;
                        Draw(br, "HP_BAR");
                    }
                    Draw(ABCBackRect[i], ABCBackTex[st]);
                    Draw(ABCIconRect[i], ABCIconTex[st]);
                }
                if (s.SkillIcon != null)
                {
                    Draw(SkillRect[i], "buff_icon_back");
                    DrawIcon(SkillRect[i], s.SkillIcon);
                    if (s.ActiveSkill && s.CoolMax > 0f && Time.time - s.CoolCur < s.CoolMax)
                    {
                        var pos = SkillRect[i]; pos.x -= 1f;
                        DrawCooldown(R(pos), s.CoolMax - (Time.time - s.CoolCur), s.CoolMax);
                    }
                }
                Draw(SlotIconRect[i], SlotIconTex[i]);
            }
            GUI.EndGroup(); GUI.matrix = oldMatrix;

            // temporizador del gumball (mm:ss) encima de cada nano con buff activo
            for (int gi = 0; gi < SlotCount; gi++)
            {
                if (slots[gi].GumEnd <= 0f || !slots[gi].Has) continue;
                if (gumStyle == null) { gumStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow }; }
                gumStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(12f * sc));
                float cx = gs.x + ABCBackRect[gi].center.x * sc, w = 46f * sc, h = 16f * sc; var br = new Rect(cx - w * 0.5f, gs.y - h - 2f * sc, w, h);
                GUI.color = new Color(0f, 0.05f, 0.12f, 0.75f); GUI.DrawTexture(br, Texture2D.whiteTexture); GUI.color = Color.white;
                string tx = GumTime(slots[gi].GumEnd - Time.time);
                gumStyle.normal.textColor = Color.black; GUI.Label(new Rect(br.x + 1f, br.y + 1f, br.width, br.height), tx, gumStyle);
                gumStyle.normal.textColor = slots[gi].GumEnd - Time.time < 30f ? new Color(1f, 0.85f, 0.3f, 1f) : Color.white; GUI.Label(br, tx, gumStyle);
            }

            GUI.matrix = oldMatrix; GUI.color = oldColor;
        }

        // Draw immediately after PlayerHud draws this very icon, including in DS mode.
        public void DrawActiveInfoCooldown(Rect iconRect)
        {
            if (!DrawNanoInfoCooldown || hud == null) return;
            int slot = hud.Data.ActiveNanoSlot; if (slot < 0 || slot >= SlotCount) return;
            var s = slots[slot]; float remaining = CooldownRemaining(slot);
            if (s.Has && s.ActiveSkill && s.CoolMax > 0 && remaining > 0) DrawCooldown(iconRect, remaining, s.CoolMax);
        }

        static Rect Union(Rect a, Rect b) { float x0 = Mathf.Min(a.xMin, b.xMin), y0 = Mathf.Min(a.yMin, b.yMin), x1 = Mathf.Max(a.xMax, b.xMax), y1 = Mathf.Max(a.yMax, b.yMax); return new Rect(x0, y0, x1 - x0, y1 - y0); }

        /// <summary>Disco de cooldown, algoritmo literal de cnNanoWheel.DoWindow / CnGuiNano_info.DoWindow. pos en pixeles (coordenadas del grupo actual); rem = segundos que faltan; max = duracion total.
        /// Siete texturas cooltime01..07 de 45 grados cada una + CoolTime_00 en pasos de 5 grados para el ultimo tramo.</summary>
        void DrawCooldown(Rect pos, float rem, float max)
        {
            int num3 = Mathf.Min(7, Mathf.FloorToInt(rem * 7f / max)) - 1;
            float num4 = rem * 360f / max;
            float num5 = num4 - (float)(num3 * 45);
            var c = new Vector2(pos.x + pos.width * 0.5f, pos.y + pos.height * 0.5f);
            var m = GUI.matrix;
            if (num4 > 45f && num3 >= 0)
            {
                GUIUtility.RotateAroundPivot(num5 - 45f, c);
                DrawRaw(pos, CoolTex[Mathf.Min(6, num3 + 1)]);
                GUI.matrix = m;
                DrawRaw(pos, CoolTex[Mathf.Max(1, num3)]);
            }
            else
            {
                int num6 = Mathf.FloorToInt(num5 / 5f);
                for (int j = 0; j < num6; j++)
                {
                    GUI.matrix = m; GUIUtility.RotateAroundPivot((float)(j * 5), c);
                    DrawRaw(pos, "CoolTime_00");
                }
                GUI.matrix = m;
            }
        }
        void DrawRaw(Rect r, string name) { var t = T(name); if (t != null) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); }
    }
}
