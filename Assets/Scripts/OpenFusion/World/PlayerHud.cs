// PlayerHud: reconstruccion de la ventana de estado del jugador propio del cliente original (FusionFall / Retrobution, Unity Web Player 2.5).
//
// ORIGINAL (decompilado):
//   CnGuiCharacter_info.OnOwnGUI  (GameObject "GameHUD", skin "FusionFallHUDSkin" = GUISkin#8)   -> ventana, nombre, nivel, barra HP, retrato, disco, icono de chat libre
//   cnNanoWheel.OnOwnGUI (bloque final) -> contadores de pociones nano (iBatteryNano) y boosts de arma (iBatteryWpn), abajo a la derecha
//   CnGuiNano_info.OnOwnGUI       -> ventana del nano activo (nombre + barra de stamina), a la derecha de la ventana de estado (101,42)
// NO implementado aqui (solo se documenta en REPORT.md): minimapa + medidor de Fusion Matter circular (cnGUINanocom), rueda de nanos A/B/C (cnNanoWheel.DoWindow),
// iconos de buffs (CnGuiSkillBuffIcon), chat (CnGuiChat), quickslot, barra de EXP superior (localized.expBartype == eCircleType => NO se dibuja en esta localizacion).
//
// Maquetacion: todo en "espacio de diseno" (rects tal cual el original) y se transforma p' = pivot + (p - pivot) * s,  s = Screen.height / 681 (FFGUIUtility.GetUiScale con uiScaleFactor = 1),
// pivote TopLeft (0,0) para la ventana (ScreenPivot 1) y BottomRight (W,H) para los contadores (ScreenPivot 2). Igual que NpcUI.Xf.
//
// Dependencias del proyecto: BitmapFontLoader.cs (BitmapFont), OpenFusion.Net.PacketDispatcher / PacketIds / PcAppearance (esqueleto). WorldEntities (Self, SelfSpawned).
// Texturas (Resources/OpenFusion/UI/Textures/, copias ya volteadas): character_info, DiskFront, HP_BAR, freechat_icon, nano_info, potion_counter, boost_counter.
// SIN COMPILAR EN UNITY (revisado a mano contra la API 2022 LTS). Si SubmitRenderRequest/StandardRequest no compilara en tu version, borra el bloque marcado [RR] y deja cam.Render().
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using OpenFusion.Net;

namespace OpenFusion.World
{
    /// <summary>Datos que muestra el HUD. Un unico objeto mutable; el hilo principal lo lee en OnGUI.</summary>
    public sealed class HudData
    {
        public string Name = "";            // original: szFirstName + " " + szLastName (cnAvatarStatus)  <- sPCStyle.szFirstName/szLastName de ENTER_SUCC (PcAppearance.FirstName/LastName)
        public int Level = 1;               // Status.iLevel; texto = iLevel.ToString("00")                 <- sPCLoadData2CL.iLevel (short)
        public int Hp = 1;                  // Status.iCurHP                                                 <- sPCLoadData2CL.iHP, luego TICK / NPC_ATTACK_PCs / REGEN_SUCC / SET_VALUE
        public int MaxHp = 0;               // Status.iMaxHP = AvatarGrowElement(tabla 1,1)[nivel].m_iMaxHP * (1 + ClassTypeElement.m_iBonusHP*0.01). 0 => se calcula con MaxHpProvider/formula
        public int Taros;                   // cnOwnAvatarStatus.iTaros   <- iCandy. (No se dibuja en el HUD: solo en el inventario)
        public int FusionMatter;            // cnOwnAvatarStatus.iFusionMatter  (medidor circular del minimapa, no implementado)
        public int MaxFusionMatter;         // AvatarGrowElement.m_iReqBlob_NanoCreate[nivel] (tabla); 0 = desconocido
        public int BatteryWpn;              // iBatteryWpn  (boosts de arma, contador "boost_counter")
        public bool UseWeaponBattery = true;
        public int BatteryNano;             // iBatteryNano (pociones nano, contador "potion_counter")
        public bool FreeChat = true;        // bFreeChat = !(iSpecialState & 64)  => icono freechat_icon
        public short[] NanoSlots = { 0, 0, 0 };           // ids de nano en los slots A/B/C (sPCLoadData2CL.aNanoSlots)
        public short[] NanoStamina = { 0, 0, 0 };         // stamina de cada slot (TICK / REGEN)
        public int ActiveNanoSlot = -1;     // -1 ninguno (sPCLoadData2CL.iActiveNanoSlotNum / PCRegenData.iActiveNanoSlotNum)
        public short[] NanoBankStamina;     // aNanoBank[37] stamina por id de nano (de ENTER_SUCC), opcional
        public const int NanoStaminaMax = 150;   // OpenFusion NANO_STAMINA_MAX; el original usa Status.iMaxHP del nano (tabla m_iNanoBattery1)

        /// <summary>Formula del servidor OpenFusion PC_MAXHEALTH(level) = 925*level + 100 - 925. ES UNA SUPOSICION para MaxHp: sustituir por la tabla AvatarGrow si el puerto la carga.</summary>
        public static int DefaultMaxHp(int level) { return 925 * Math.Max(1, level) + 100 - 925; }
    }

    /// <summary>Lectores puros (sin Unity) de los campos de HUD de cada paquete. body = cuerpo sin los 4 bytes del tipo (lo que recibe PacketDispatcher.Unhandled).</summary>
    public static class HudPackets
    {
        // Header: eCT, iID, short iTB_ID + pack-4 padding. Results use their own target fields.
        public static bool ApplyTimeBuff(byte[] b, int selfId, HudData data, out int kind, out int amount, out int conditions)
        {
            kind = amount = 0; conditions = -1;
            if (b == null || b.Length < 12 || I(b, 0) != 1 || I(b, 4) != selfId) return false;
            int tb = S(b, 8), size = tb == 17 ? 32 : tb == 19 ? 20 : tb == 24 ? 16 : 0;
            if (size == 0 || b.Length < 12 + size || I(b, 12) != 1 || I(b, 16) != selfId) return false;
            kind = tb;
            if (tb == 24) { amount = I(b, 20); data.Hp = I(b, 24); return true; }
            amount = I(b, 20) != 0 ? -2 : I(b, 24); data.Hp = I(b, 28);
            if (tb == 17 && I(b, 20) != 0)
            {
                int slot = data.ActiveNanoSlot;
                if (slot >= 0 && slot < data.NanoSlots.Length && slot < data.NanoStamina.Length)
                {
                    short stamina = S(b, 32); data.NanoStamina[slot] = stamina;
                    int id = data.NanoSlots[slot];
                    if (data.NanoBankStamina != null && id >= 0 && id < data.NanoBankStamina.Length) data.NanoBankStamina[id] = stamina;
                }
                if (I(b, 36) != 0) data.ActiveNanoSlot = -1;
                conditions = I(b, 40);
            }
            return true;
        }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static short S(byte[] b, int o) { return BitConverter.ToInt16(b, o); }
        static void Need(byte[] b, int n, string w) { if (b == null || b.Length < n) throw new ArgumentException(w + " necesita " + n + " bytes"); }

        public const int EnterSuccSize = 2564;

        /// <summary>REP_PC_ENTER_SUCC (iID(4) + sPCLoadData2CL(2552) + ...). Offsets dentro de sPCLoadData2CL (Pack 4): iLevel 84 (short), iHP 92, iBatteryW 96, iBatteryN 100, iCandy 104, iFusionMatter 108,
        /// iSpecialState 112 (sbyte), aNanoBank 1444 (37 x sNano{iID,iSkillID,iStamina} 6 B), aNanoSlots 1666 (3 x short), iActiveNanoSlotNum 1672 (short). Los de nanos se han CALCULADO sumando tamanos
        /// (equip 9x12 @136, inven 50x12, qinven 50x12) y no estan validados contra un paquete real.</summary>
        public static void ApplyEnterSucc(byte[] body, HudData d)
        {
            Need(body, EnterSuccSize, "ENTER_SUCC"); const int L = 4;
            d.Level = S(body, L + 84); d.Hp = I(body, L + 92); d.BatteryWpn = I(body, L + 96); d.BatteryNano = I(body, L + 100); d.Taros = I(body, L + 104); d.FusionMatter = I(body, L + 108);
            d.UseWeaponBattery = true;
            d.FreeChat = ((sbyte)body[L + 112] & 64) == 0;
            var bank = new short[37]; for (int i = 0; i < 37; i++) bank[i] = S(body, L + 1444 + i * 6 + 4); d.NanoBankStamina = bank;
            for (int i = 0; i < 3; i++) { d.NanoSlots[i] = S(body, L + 1666 + i * 2); int id = d.NanoSlots[i]; d.NanoStamina[i] = id >= 0 && id < 37 ? bank[id] : (short)0; }
            d.ActiveNanoSlot = S(body, L + 1672);
        }

        /// <summary>REP_PC_TICK (32 B): iHP @0, aNano[3] @4 (iStamina en +4 de cada sNano), iBatteryN @24. Llega periodicamente (~cada 2 s) y es la fuente de HP "de regeneracion".</summary>
        public static bool ApplyTick(byte[] b, HudData d)
        {
            if (b == null || b.Length < 28) return false; d.Hp = I(b, 0);
            for (int i = 0; i < 3; i++) d.NanoStamina[i] = S(b, 4 + i * 6 + 4);
            d.BatteryNano = I(b, 24); return true;
        }

        /// <summary>NPC_ATTACK_PCs: sP(8 B){iNPC_ID, iPCCnt} + iPCCnt x sAttackResult(24 B){eCT, iID, bProtected, iDamage, iHP, iHitFlag}. Si iID == selfId => HP propio = iHP. Devuelve el dano (0 si no habia).
        /// (El cliente original usa un stride de 8 B por un bug; con 1 resultado da igual. Aqui se usa 24 B, el tamano real de sAttackResult.)</summary>
        public static int ApplyNpcAttackPcs(byte[] b, int selfId, HudData d)
        {
            if (b == null || b.Length < 8) return 0; int n = I(b, 4); int dmg = 0;
            for (int i = 0; i < n; i++) { int o = 8 + 24 * i; if (o + 24 > b.Length) break; if (I(b, o + 4) == selfId) { d.Hp = I(b, o + 16); dmg += I(b, o + 12); } }
            return dmg;
        }

        /// <summary>REP_PC_REGEN_SUCC (48 B): sPCRegenData{iHP@0, iMapNum, X, Y, Z, iActiveNanoSlotNum@20 (short), Nanos[3]@22 (sNano 6 B)} + bMoveLocation@40 + iFusionMatter@44.</summary>
        public static bool ApplyRegenSucc(byte[] b, HudData d, bool applyFusionMatter)
        {
            if (b == null || b.Length < 48) return false; d.Hp = I(b, 0); d.ActiveNanoSlot = S(b, 20);
            for (int i = 0; i < 3; i++) d.NanoStamina[i] = S(b, 22 + i * 6 + 4);
            if (applyFusionMatter) d.FusionMatter = I(b, 44); return true;
        }

        /// <summary>GM_REP_PC_SET_VALUE (12 B){iPC_ID, iSetValueType, iSetValue}: 1 HP, 2 BatteryW, 3 BatteryN, 4 FusionMatter, 5 Taros (solo si iPC_ID == selfId).</summary>
        public static bool ApplySetValue(byte[] b, int selfId, HudData d)
        {
            if (b == null || b.Length < 12 || I(b, 0) != selfId) return false; int v = I(b, 8);
            switch (I(b, 4)) { case 1: d.Hp = v; break; case 2: d.BatteryWpn = v; break; case 3: d.BatteryNano = v; break; case 4: d.FusionMatter = v; break; case 5: d.Taros = v; break; default: return false; }
            return true;
        }

        /// <summary>REP_REWARD_ITEM (36 B): m_iCandy @0, m_iFusionMatter @4, m_iBatteryN @8, m_iBatteryW @12 (totales absolutos).</summary>
        public static bool ApplyRewardItem(byte[] b, HudData d) { if (b == null || b.Length < 16) return false; d.Taros = I(b, 0); d.FusionMatter = I(b, 4); d.BatteryNano = I(b, 8); d.BatteryWpn = I(b, 12); return true; }

        /// <summary>REP_PC_NANO_CREATE_SUCC (28 B): iPC_FusionMatter @0, ..., iPC_Level @26 (short).</summary>
        public static bool ApplyNanoCreate(byte[] b, HudData d) { if (b == null || b.Length < 28) return false; d.FusionMatter = I(b, 0); d.Level = S(b, 26); return true; }

        /// <summary>REP_CHARGE_NANO_STAMINA (8 B): iBatteryN @0, iNanoID @4, iNanoStamina @6.</summary>
        public static bool ApplyChargeNano(byte[] b, HudData d)
        {
            if (b == null || b.Length < 8) return false; d.BatteryNano = I(b, 0); short id = S(b, 4), st = S(b, 6);
            for (int i = 0; i < 3; i++) if (d.NanoSlots[i] == id) d.NanoStamina[i] = st; return true;
        }

        /// <summary>PC_ATTACK_NPCs_SUCC (8 B): iBatteryW @0 (boosts restantes tras atacar).</summary>
        public static bool ApplyAttackNpcsSucc(byte[] b, HudData d) { if (b == null || b.Length < 4) return false; d.BatteryWpn = I(b, 0); return true; }
    }

    public sealed class PlayerHud : MonoBehaviour
    {
        // ---------------- API publica ----------------
        public readonly HudData Data = new HudData();
        public bool Visible = true;
        public bool ShowNanoInfo = true;          // ventana del nano activo (CnGuiNano_info)
        public bool ShowBatteryCounters = true;   // potion_counter / boost_counter (cnNanoWheel)
        public Func<int, int> MaxHpProvider;      // nivel -> HP maximo (tabla AvatarGrow). null => HudData.DefaultMaxHp
        public Func<int, string> NanoNameProvider;// id de nano -> nombre (NpcTable/NanoStringData). null => "Nano <id>"
        /// <summary>REWARD_ITEM aplicado: (taros, fm, pociones, boosts) previos + cuerpo del paquete. Lo usa RewardHud.</summary>
        public event Action<int, int, int, int, byte[]> RewardApplied;
        public Func<int, Texture> NanoSkillIconProvider; // id de nano -> icono de su skill (26x26), opcional
        /// <summary>Retrato: capa temporal para aislar el avatar. Elige una capa libre.</summary>
        public int PortraitLayer = 30;
        /// <summary>Datos de la camara del retrato (cnCharRenderCamera "Player_RTCamera" original): hueso "Bip01 Neck", vAngle (0,-20,0), Distance 0.5, fHeight 0.2. El FOV del Camera original no esta en los datos extraidos: 60 (defecto de Unity) es lo mas probable.</summary>
        public float PortraitFov = 60f, PortraitDistance = 0.5f, PortraitHeight = 0.2f, PortraitYaw = -20f;
        public string NeckBoneName = "Bip01 Neck", HeadBoneName = "Bip01 Head";
        /// <summary>false = no usar el retrato de camara (solo disco vacio), p.ej. si el modelo propio aun no existe.</summary>
        public bool ShowPortrait = true;
        public int PortraitSupersample = 2;

        /// <summary>Atajo. Engancha el HUD: SelfSpawned (nombre/nivel/HP/FM desde ENTER_SUCC.Self) y, si se pasa, los paquetes de la red (Unhandled).</summary>
        public void Init(WorldEntities ents, PacketDispatcher dispatcher = null)
        {
            Detach(); this.ents = ents; this.dispatcher = dispatcher;
            if (ents != null) { ents.SelfSpawned += OnSelfSpawned; if (ents.Self != null) self = ents.Self.transform; selfId = ents.SelfId; }
            if (dispatcher != null) { dispatcher.Unhandled += OnPacket; dispatcher.SelfEnter += OnSelfEnterNet; }
        }
        public void Detach()
        {
            if (ents != null) ents.SelfSpawned -= OnSelfSpawned;
            if (dispatcher != null) { dispatcher.Unhandled -= OnPacket; dispatcher.SelfEnter -= OnSelfEnterNet; }
            ents = null; dispatcher = null;
        }

        /// <summary>Alternativa sin eventos: rellenar desde el PcAppearance propio.</summary>
        public void SetFromAppearance(PcAppearance a)
        {
            if (a == null) return;
            Data.Name = ((a.FirstName ?? "") + " " + (a.LastName ?? "")).Trim(); Data.Level = a.Level; Data.Hp = a.Hp; Data.FusionMatter = a.FusionMatter; hpTarget = Data.Hp; hpShown = hpTarget; initHp = true;
        }
        public void SetHp(int hp) { Data.Hp = hp; }
        public void SetSelf(Transform t, int id) { self = t; selfId = id; }

        // ---------------- red (hilo de red -> cola -> Update) ----------------
        WorldEntities ents; PacketDispatcher dispatcher; Transform self; int selfId;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        void OnSelfSpawned(GameObject go, EnterSucc e) { var a = e.Self; q.Enqueue(() => { self = go != null ? go.transform : null; selfId = e.Id; SetFromAppearance(a); }); }
        void OnSelfEnterNet(EnterSucc e) { /* los extras (taros, pociones, nanos) llegan en el cuerpo crudo: ver OnPacket no cubre ENTER_SUCC porque el dispatcher lo trata. Usar ApplyEnterBody desde GameClient. */ }
        /// <summary>Llamar con el cuerpo crudo de REP_PC_ENTER_SUCC (sin los 4 bytes del tipo) para leer taros/pociones/nanos/chat libre. Opcional; ver REPORT.md.</summary>
        public void ApplyEnterBody(byte[] body)
        {
            var c = body;
            q.Enqueue(() =>
            {
                try
                {
                    string n = Data.Name; HudPackets.ApplyEnterSucc(c, Data); if (!string.IsNullOrEmpty(n)) Data.Name = n; hpTarget = Data.Hp; hpShown = hpTarget; initHp = true;
                    Debug.Log("[hud] ENTER_SUCC (" + c.Length + " B): nivel=" + Data.Level + " hp=" + Data.Hp + " hpMax=" + MaxHp() + " taros=" + Data.Taros + " nanos=[" + Data.NanoSlots[0] + "," + Data.NanoSlots[1] + "," + Data.NanoSlots[2] + "] activo=" + Data.ActiveNanoSlot);
                }
                catch (ArgumentException ex) { Debug.LogWarning("[hud] ENTER_SUCC no interpretado (" + (c == null ? -1 : c.Length) + " B, esperados " + HudPackets.EnterSuccSize + "): " + ex.Message + " -> HP=" + Data.Hp + " max=" + MaxHp()); }
            });
        }
        void OnPacket(uint type, byte[] body) { q.Enqueue(() => Handle(type, body)); }
        void Handle(uint type, byte[] b)
        {
            try
            {
                if (type == PacketIds.P_FE2CL_REP_PC_TICK) HudPackets.ApplyTick(b, Data);
                else if (type == PacketIds.P_FE2CL_CHAR_TIME_BUFF_TIME_TICK)
                {
                    if (HudPackets.ApplyTimeBuff(b, selfId, Data, out int kind, out int amount, out int conditions))
                    {
                        var self = ents != null ? ents.Self : null;
                        if (self != null)
                        {
                            FloatingPoints.AddOn(self, 1, kind == 24 ? FloatingPoints.Heal : FloatingPoints.Dot, amount, 1.8f);
                            int effect = kind == 24 ? 54 : amount == -2 ? (kind == 17 ? 804 : 806) : 385;
                            if (BulletFx.Instance != null) BulletFx.Instance.Spawn(effect, self.transform.position + Vector3.up, Quaternion.identity, 1f, null, 3f);
                        }
                        if (kind == 17) Sfx.Ui("SFX_PoisonDamage");
                        else if (kind == 19 && RaceSystem.InsMap) Sfx.Ui("Poision_Damager");
                        else if (kind == 24 && RaceSystem.InsMap) Sfx.Ui("Healing_Pool");
                        var nanocom = GetComponent<NanocomHud>(); if (conditions != -1 && nanocom != null) nanocom.SetConditionFlags(conditions);
                    }
                }
                else if (type == PacketIds.P_FE2CL_NPC_ATTACK_PCs) HudPackets.ApplyNpcAttackPcs(b, selfId, Data);
                else if (type == PacketIds.P_FE2CL_REP_PC_REGEN_SUCC) HudPackets.ApplyRegenSucc(b, Data, false);
                else if (type == PacketIds.P_FE2CL_GM_REP_PC_SET_VALUE) HudPackets.ApplySetValue(b, selfId, Data);
                else if (type == PacketIds.P_FE2CL_REP_REWARD_ITEM) { int pt = Data.Taros, pf = Data.FusionMatter, pn = Data.BatteryNano, pw = Data.BatteryWpn; HudPackets.ApplyRewardItem(b, Data); RewardApplied?.Invoke(pt, pf, pn, pw, b); }
                else if (type == PacketIds.P_FE2CL_REP_PC_NANO_CREATE_SUCC) HudPackets.ApplyNanoCreate(b, Data);
                else if (type == PacketIds.P_FE2CL_REP_CHARGE_NANO_STAMINA) HudPackets.ApplyChargeNano(b, Data);
                else if (type == PacketIds.P_FE2CL_PC_ATTACK_NPCs_SUCC) HudPackets.ApplyAttackNpcsSucc(b, Data);
            }
            catch (Exception e) { Debug.LogWarning("[hud] paquete " + type + ": " + e.Message); }
        }

        // ---------------- recursos ----------------
        const string UI = "OpenFusion/UI/Textures/";
        readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>(); readonly HashSet<string> missing = new HashSet<string>();
        Texture2D T(string n)
        {
            Texture2D t; if (tex.TryGetValue(n, out t)) return t; if (missing.Contains(n)) return null;
            t = Resources.Load<Texture2D>(UI + n); if (t == null) { missing.Add(n); Debug.LogWarning("[hud] falta textura " + UI + n); return null; }
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp; tex[n] = t; return t;
        }
        BitmapFont chalet, jeffe14; bool fontsTried;
        // GUISkin#8 (FusionFallHUDSkin): m_Font = "ChaletBook-Regular Small" (hay DOS con ese nombre en los datos: __1018 = 12 px/357 glifos [por defecto] y __949 = 13.6 px/12260 glifos), label = MiddleLeft, color 0.9
        public string LabelFontJson = "ChaletBook-Regular_Small__1018", CounterFontJson = "JEFFE___14__903";
        void Fonts() { if (fontsTried) return; fontsTried = true; chalet = BitmapFontLoader.Load(LabelFontJson); jeffe14 = BitmapFontLoader.Load(CounterFontJson); }

        // ---------------- constantes de maquetacion (bindings.json, GameHUD/CnGuiCharacter_info) ----------------
        public const float UiRefH = 681f;
        /// <summary>Rect en pantalla de la ventana de estado del personaje (se actualiza en cada Repaint).</summary>
        public static Rect StatusScreen;
        public static readonly Rect WindowRect = new Rect(0f, 6f, 255f, 66f), NameRect = new Rect(60f, 1f, 190f, 20f), LevelRect = new Rect(90f, 24f, 60f, 20f),
            HpRect = new Rect(58f, 23f, 186f, 4f), PortraitRect = new Rect(7f, 0f, 50f, 60f) /* Start(): 50x60 (el prefab dice 60x60) */, DiskRect = new Rect(1f, 49f, 69f, 23f), FreeChatRect = new Rect(3f, 10f, 19f, 14f);
        // CnGuiNano_info
        public static readonly Rect NanoRenderRect = new Rect(101f, 42f, 154f, 29f), NanoWindowRect = new Rect(0f, 0f, 154f, 29f), NanoIconRect = new Rect(125f, 2f, 26f, 26f), NanoNameRect = new Rect(20f, 0f, 100f, 20f), NanoBarRect = new Rect(18f, 19f, 104f, 4f);
        const float HpLerpSpeed = 5f, HpHoldTime = 0.4f;
        static readonly Color LabelCol = new Color(0.9f, 0.9f, 0.9f, 1f), CounterCol = new Color(1f, 0.9959677f, 1f, 1f);

        // ---------------- animacion de la barra (identica a CnGuiCharacter_info.Update) ----------------
        float hpTarget, hpShown, hpHold; bool initHp;
        float nanoTarget, nanoShown, nanoHold; int nanoLast = -2;
        int MaxHp() { if (Data.MaxHp > 0) return Data.MaxHp; return MaxHpProvider != null ? Math.Max(1, MaxHpProvider(Data.Level)) : HudData.DefaultMaxHp(Data.Level); }
        static void Lag(float target, ref float shown, ref float hold)
        {
            if (target < shown)
            {
                if (hold > 0f) hold -= Time.deltaTime;
                else { shown = Mathf.Lerp(shown, target, Time.deltaTime * HpLerpSpeed); if (shown - target < 0.001f) shown = target; }
            }
            else { shown = target; hold = HpHoldTime; }
        }

        void Update()
        {
            Action a; int guard = 0; while (guard++ < 64 && q.TryDequeue(out a)) { try { a(); } catch (Exception e) { Debug.LogWarning("[hud] " + e.Message); } }
            if (self == null && ents != null && ents.Self != null) { self = ents.Self.transform; selfId = ents.SelfId; }
            hpTarget = Mathf.Min(1f, (float)Data.Hp / (float)MaxHp()); if (!initHp) { hpShown = hpTarget; initHp = true; }
            Lag(hpTarget, ref hpShown, ref hpHold);
            int slot = Data.ActiveNanoSlot;
            if (slot >= 0 && slot < 3)
            {
                nanoTarget = Mathf.Clamp01((float)Data.NanoStamina[slot] / HudData.NanoStaminaMax);
                if (nanoLast != Data.NanoSlots[slot]) { nanoLast = Data.NanoSlots[slot]; nanoShown = nanoTarget; nanoHold = HpHoldTime; }   // al cambiar de nano no hay retardo (curHpValue = nano_hpValue)
                Lag(nanoTarget, ref nanoShown, ref nanoHold);
            }
            else nanoLast = -2;
        }

        // ---------------- retrato (AvatarUtil.DrawCamera + cnCharRenderCamera) ----------------
        Camera cam; RenderTexture rt; int rtW, rtH; Transform neck;
        readonly List<GameObject> swapObjs = new List<GameObject>(); readonly List<int> swapLayers = new List<int>();
        static Transform FindDeep(Transform r, string n) { if (r.name == n) return r; for (int i = 0; i < r.childCount; i++) { var f = FindDeep(r.GetChild(i), n); if (f != null) return f; } return null; }

        void EnsureCamera()
        {
            if (cam != null) return;
            var go = new GameObject("PlayerHud_PortraitCamera"); go.hideFlags = HideFlags.HideAndDontSave; DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>(); cam.enabled = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 8f; cam.allowHDR = false; cam.allowMSAA = false; cam.useOcclusionCulling = false;
        }
        void EnsureRt(float scale)
        {
            int w = Mathf.Max(32, Mathf.CeilToInt(PortraitRect.width * scale * PortraitSupersample)), h = Mathf.Max(32, Mathf.CeilToInt(PortraitRect.height * scale * PortraitSupersample));
            if (rt != null && rtW == w && rtH == h) return;
            if (rt != null) { rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.name = "PlayerHud_Portrait"; rt.antiAliasing = 1; rt.filterMode = FilterMode.Bilinear; rt.wrapMode = TextureWrapMode.Clamp; rt.Create(); rtW = w; rtH = h; portraitValid = false;
        }
        bool portraitValid;

        /// <summary>cam.Render debe llamarse FUERA de OnGUI: aqui, en LateUpdate (despues de que el avatar se anime/mueva).</summary>
        void LateUpdate()
        {
            if (!Visible || !ShowPortrait || self == null) { return; }
            try { RenderPortrait(); } catch (Exception e) { Debug.LogWarning("[hud] retrato: " + e.Message); portraitValid = false; }
        }

        void RenderPortrait()
        {
            EnsureCamera(); EnsureRt(GameOptions.UiScale);
            // El retrato se renderiza desde un CLON del avatar con stand1 en bucle, lejos del mapa: no toca las animaciones del avatar real y mantiene la pose idle.
            EnsureClone();
            if (clone == null) return;
            RenderPortraitCore();
        }
        Transform clone, cloneSrc; static readonly Vector3 CloneAt = new Vector3(0f, -4000f, 0f);
        void EnsureClone()
        {
            if (clone != null && cloneSrc == self) return;
            if (clone != null) Destroy(clone.gameObject);
            clone = null; cloneSrc = self; if (self == null) return;
            var go = Instantiate(self.gameObject); go.name = "PortraitClone"; go.SetActive(true);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = PortraitLayer; if (t.name == "BlobShadow" || t.name == "shadow") t.gameObject.SetActive(false); }
            go.transform.SetParent(null); go.transform.SetPositionAndRotation(CloneAt, Quaternion.identity);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            var an = go.GetComponentInChildren<Animation>(true);
            if (an != null) { an.enabled = true; an.cullingType = AnimationCullingType.AlwaysAnimate; var st = an["stand1"]; if (st != null) { st.wrapMode = WrapMode.Loop; an.Play("stand1"); } }
            clone = go.transform; neck = null;
        }
        void RenderPortraitCore()
        {
            if (neck == null || !neck.IsChildOf(clone)) { neck = FindDeep(clone, NeckBoneName); if (neck == null) neck = FindDeep(clone, HeadBoneName); }
            // cnCharRenderCamera.LateUpdate: pos = neck + Avatar.rot * Euler(vAngle) * forward * Distance; LookAt(neck); pos.y += fHeight
            Vector3 target; if (neck != null) target = neck.position; else { var b = GetBounds(); target = new Vector3(b.center.x, b.max.y - 0.25f, b.center.z); }
            Quaternion rot = clone.rotation;
            Vector3 pos = target + rot * Quaternion.Euler(0f, PortraitYaw, 0f) * Vector3.forward * PortraitDistance;
            cam.transform.position = pos; cam.transform.LookAt(target); pos = cam.transform.position; pos.y += PortraitHeight; cam.transform.position = pos;
            cam.fieldOfView = PortraitFov; cam.aspect = PortraitRect.width / PortraitRect.height; cam.cullingMask = 1 << PortraitLayer;
            try
            {
                cam.targetTexture = rt;
                bool done = false;
                // [RR] URP / SRP: Camera.Render() esta desaconsejado; SubmitRenderRequest renderiza a la textura destino
                var req = new RenderPipeline.StandardRequest(); req.destination = rt;
                if (RenderPipeline.SupportsRenderRequest(cam, req)) { RenderPipeline.SubmitRenderRequest(cam, req); done = true; }
                // [/RR]
                if (!done) cam.Render();
                portraitValid = true;
            }
            finally { cam.targetTexture = null; }
        }
        Bounds GetBounds() { var rs = (clone != null ? clone : self).GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return new Bounds((clone != null ? clone : self).position + Vector3.up, Vector3.one); var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); return b; }

        void OnDestroy() { if (clone != null) Destroy(clone.gameObject); Detach(); if (rt != null) { rt.Release(); Destroy(rt); } if (cam != null) Destroy(cam.gameObject); }

        // ---------------- dibujo ----------------
        struct Xf { public Vector2 P; public float S; public Rect R(Rect r) { return new Rect(P.x + (r.x - P.x) * S, P.y + (r.y - P.y) * S, r.width * S, r.height * S); } }
        static Rect Off(Rect r, float dx, float dy) { return new Rect(r.x + dx, r.y + dy, r.width, r.height); }

        void OnGUI()
        {
            if (GameMenu.HudHidden) return;
            if (!Visible || Event.current == null || Event.current.type != EventType.Repaint) return;
            if (self == null && Data.Name.Length == 0) return;                // aun no hay jugador (GameFrame.IsReadyForPlay)
            Fonts(); float s = GameOptions.UiScale; var old = GUI.color; GUI.depth = 10;
            // --- ventana de estado, pivote TopLeft ---
            var xf = new Xf { P = Vector2.zero, S = s };
            StatusScreen = xf.R(WindowRect);   // zona de clic (GameMenu): abre el inventario
            Draw(xf, WindowRect, "character_info");
            Label(chalet, xf, NameRect, Data.Name, TextAnchor.MiddleLeft, LabelCol);
            Label(chalet, xf, LevelRect, Data.Level.ToString("00"), TextAnchor.MiddleLeft, LabelCol);
            // barra HP: primero la franja "retrasada" teñida de rojo (damageBarColor), luego la actual encima. DrawTexture estira la textura entera => el degradado se comprime con la fraccion.
            var bar = T("HP_BAR");
            if (bar != null)
            {
                var r = HpRect; r.width *= hpShown; GUI.color = Color.red; GUI.DrawTexture(xf.R(r), bar, ScaleMode.StretchToFill, true);
                GUI.color = Color.white; r.width = HpRect.width * hpTarget; GUI.DrawTexture(xf.R(r), bar, ScaleMode.StretchToFill, true);
            }
            if (ShowPortrait && portraitValid && rt != null) GUI.DrawTexture(xf.R(PortraitRect), rt, ScaleMode.StretchToFill, true);
            Draw(xf, DiskRect, "DiskFront");
            if (Data.FreeChat) Draw(xf, FreeChatRect, "freechat_icon");
            // --- ventana del nano activo (CnGuiNano_info) ---
            if (ShowNanoInfo && Data.ActiveNanoSlot >= 0 && Data.ActiveNanoSlot < 3) DrawNanoInfo(xf);
            // --- contadores (cnNanoWheel), pivote BottomRight ---
            if (ShowBatteryCounters)
            {
                float W = Screen.width, H = Screen.height; var xb = new Xf { P = new Vector2(W, H), S = s };
                var rr = new Rect(W - 223f - 20f - 81f, H - 85f, 76f, 29f); var rr2 = Off(rr, 12f, 8f);
                bool low = (Data.BatteryNano > 0 && Data.BatteryNano < 100) || (Data.BatteryWpn > 0 && Data.BatteryWpn < 100);
                Color bc = low ? Color.red : Color.white;
                if (Data.BatteryNano > 0) { GUI.color = bc; Draw(xb, rr, "potion_counter"); GUI.color = Color.white; Label(jeffe14, xb, rr2, Data.BatteryNano.ToString("0000"), TextAnchor.UpperCenter, CounterCol, bc); }
                rr.y += 35f; rr2.y += 35f;
                if (Data.BatteryWpn > 0) { GUI.color = bc; Draw(xb, rr, "boost_counter"); GUI.color = Color.white; Label(jeffe14, xb, rr2, Data.BatteryWpn.ToString("0000"), TextAnchor.UpperCenter, CounterCol, bc); }
            }
            GUI.color = old;
        }

        void Draw(Xf xf, Rect r, string name) { var t = T(name); if (t != null) GUI.DrawTexture(xf.R(r), t, ScaleMode.StretchToFill, true); }

        void Label(BitmapFont f, Xf xf, Rect r, string text, TextAnchor anchor, Color col, Color? tint = null)
        {
            if (string.IsNullOrEmpty(text)) return; var c = col; if (tint.HasValue) c = new Color(col.r * tint.Value.r, col.g * tint.Value.g, col.b * tint.Value.b, col.a);
            if (f != null) { f.Draw(r, text, anchor, true, c, null, xf.P, xf.S); return; }
            var rr = xf.R(r); var st = new GUIStyle(GUI.skin.label) { alignment = anchor, fontSize = Mathf.Max(8, Mathf.RoundToInt(12 * xf.S)), clipping = TextClipping.Clip }; st.normal.textColor = c; GUI.Label(rr, text, st);
        }

        void DrawNanoInfo(Xf xf)
        {
            // GUI.BeginGroup(RenderRect): coordenadas del interior relativas a (101,42)
            var o = NanoRenderRect; int slot = Data.ActiveNanoSlot; int id = Data.NanoSlots[slot];
            Draw(xf, new Rect(o.x + NanoWindowRect.x, o.y + NanoWindowRect.y, NanoWindowRect.width, NanoWindowRect.height), "nano_info");
            Texture icon = NanoSkillIconProvider != null ? NanoSkillIconProvider(id) : null;
            if (icon != null) GUI.DrawTexture(xf.R(Off(NanoIconRect, o.x, o.y)), icon, ScaleMode.StretchToFill, true);
            string nm = NanoNameProvider != null ? NanoNameProvider(id) : "Nano " + id;
            Label(chalet, xf, Off(NanoNameRect, o.x, o.y), nm, TextAnchor.MiddleLeft, LabelCol);
            var bar = T("HP_BAR"); if (bar == null) return;
            var r = Off(NanoBarRect, o.x, o.y); var full = r; r.width *= nanoShown; var old = GUI.color; GUI.color = Color.red; GUI.DrawTexture(xf.R(r), bar, ScaleMode.StretchToFill, true);
            GUI.color = Color.white; full.width *= nanoTarget; GUI.DrawTexture(xf.R(full), bar, ScaleMode.StretchToFill, true); GUI.color = old;
        }
    }
}
