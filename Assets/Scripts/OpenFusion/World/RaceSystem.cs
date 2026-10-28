// RaceSystem: port de cnRaceMode (NPC tipo 13 START/CANCEL RACE, tipo 14 END), del HUD de carrera de cnGUINanocom (aros + cronometro + cuenta atras), de la lista RACE RANK
// (NpcIconMode.StartRank -> cnRaceRankMode) y del NPC RXCOM (tipo 17, NpcIconMode.StartRXCom). SIN COMPILAR NI PROBAR EN UNITY.
// Fuente original: Assembly-CSharp/cnRaceMode.cs, cnRaceRankMode.cs, NpcIconMode.cs (StartSECom/StartEECom/StartRank/StartRXCom), GameFrame.cs (casos 822083748..755, 822083738, 822083789),
//   cnGUINanocom.cs (HUD de aros / cronometro / cuenta atras), sP_CL2FE_REQ_EP_RACE_*.cs, sP_FE2CL_REP_EP_RACE_*.cs, sP_*_RXCOM*.cs.
// Paquetes (cuerpo con +4 de cabecera para los que se envian; los recibidos llegan SIN el tipo, via PacketDispatcher.Unhandled):
//   REQ_EP_RACE_START {iStartEcomID, iEPRaceMode=1, iEPTicketItemSlotNum=0} (318767195) | REQ_EP_RACE_END {iEndEcomID, iEPTicketItemSlotNum=0} (318767196) | REQ_EP_RACE_CANCEL {iStartEcomID} (318767197)
//   REQ_EP_GET_RING {iRingLID} (318767198) | REQ_REGIST_RXCOM {iNPCID} (318767217)
//   REP_EP_RACE_START_SUCC {ulong iStartTick @0, int iLimitTime @8} | _START_FAIL {iErrorCode} (8 = mensaje 150) | _END_SUCC (72 B, ver OnEnd) | _END_FAIL | _CANCEL_SUCC | _CANCEL_FAIL
//   REP_EP_GET_RING_SUCC {iRingLID, iRingCount_Get} | INSTANCE_MAP_INFO (iEP_ID @36, mejores marcas @40..52) | REP_REGIST_RXCOM {iMapNum (= id del NPC registrado), iX, iY, iZ}
// Pantallas: (1) HUD de carrera (aros "x 000", cronometro mm:ss:cc, "Limit : mm:ss", cuenta atras 10..1 en el centro); (2) ventana de resultado (OnEndGUI: barras negras arriba/abajo,
//   panel de 398x434 a la derecha con estrellas, TIME/PODS/SCORE, MY BEST RECORD, REWARD, ACCEPT); (3) lista RACE RANK (ver limitaciones); (4) cajas de aviso de RXCOM.
// Limitaciones: el ranking original es una web (http://www.fusionfall.com/tools/scripts/client_get.php) y OpenFusion no tiene ranking: la ventana muestra las carreras del InstanceTable y el mejor
//   resultado propio conocido, sin tabla de clasificacion. Los aros fisicos (GameFrameEpContainer / EpTrigger de los dong de instancia) los gestiona EpTriggers.cs (w23): InsMapInfo / RacingChanged
//   numeran y muestran los aros y la colision llama a RaceSystem.PickRing(ringLID). Los efectos de los NPC (icono de ECom / efecto 811 del RXCOM registrado) se refrescan via los eventos RacingChanged / RecallChanged.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class RaceSystem : MonoBehaviour
    {
        public static RaceSystem Instance;
        public static RaceSystem Ensure(GameObject host) { var w = host.GetComponent<RaceSystem>(); if (w == null) w = host.AddComponent<RaceSystem>(); Instance = w; return w; }
        /// <summary>true con una ventana (resultado / ranking) abierta o esperando respuesta del servidor: bloquear movimiento, ataques, atajos y ESC del menu. El HUD de carrera NO bloquea.</summary>
        public static bool Active { get { return Instance != null && (Instance.endOpen || Instance.rankOpen || Instance.waiting); } }

        // ---- estado de carrera (cnOwnAvatarStatus: bRingRace, iRingNum, fStartTime, fRaceLimitTime, iEp_ID, iEpTopRecord_*, iRXCom) ----
        /// <summary>bRingRace: hay una carrera en curso (desde REP_EP_RACE_START_SUCC hasta END/CANCEL).</summary>
        public static bool Racing { get; private set; }
        /// <summary>cnOwnAvatarStatus.bInsMap: INSTANCE_MAP_INFO lo activa; REP_PC_WARP_USE_NPC_SUCC y la entrada al mundo lo apagan.</summary>
        public static bool InsMap { get; private set; }
        /// <summary>cnOwnAvatarStatus.iInsMapNum / area de la instancia (cliente, metros: x,z) / interruptores ON (INSTANCE_MAP_INFO); lo usa EpTriggers.InitRings.</summary>
        public static int InsMapNum { get; private set; }
        public static Rect InsArea { get; private set; }
        public static int[] SwitchOn = new int[0];
        public static event Action<Rect, int[]> InsMapInfo;
        public static int RingNum { get; private set; }
        public static float RaceStartTime { get; private set; }
        public static float RaceLimitTime { get; private set; }
        public static int EpId { get; private set; }
        public static int TopScore, TopRank, TopTime, TopRingCount;
        /// <summary>iRXCom: numero de mapa confirmado por REGIST_RXCOM (0 = sin punto).</summary>
        public static int RecallMapId { get; private set; }
        int recallRequestNpc, registeredRecallNpc;
        float recallRequestUntil;
        /// <summary>Se dispara al empezar (true) / acabar o cancelar (false) la carrera: refrescar iconos de los NPC 13/14 (NpcMoveController.MakeGameIcon / MakeStrtECom / MakeEndECom).</summary>
        public static event Action<bool> RacingChanged;
        /// <summary>(mapaAnterior, mapaNuevo) al confirmar el servidor el punto de recuperacion, igual que GameFrame.</summary>
        public static event Action<int, int> RecallChanged;

        /// <summary>Carga de texturas del bundle (opcional; solo la usa la ventana de resultado para el icono de Fusion Matter si UiKit no lo tiene).</summary>
        public Func<string, Texture> TexLoader;
        /// <summary>Posicion del HUD de carrera respecto a la esquina superior derecha (unidades de referencia): bajo la ventana de mision del original.</summary>
        public static Vector2 HudPos = new Vector2(-232f, 190f);

        const int SkRace = 1374, SkGui = 1393;
        PacketDispatcher disp; WorldEntities ents; XdtTableScript tables; PlayerHud hud; MessageTableScript msgT;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        NanoWheelHud wheel; JournalWindow journal;
        bool waiting; float waitUntil;
        float ringFx;                       // fRingEffectTime: rebote del icono de aros al recoger uno
        bool timeoutSoundPlayed;            // bPlayRaceTimeOutSound

        // resultado (cnRaceMode.RaceScore)
        bool endOpen, invenFull; int eTime, eRing, eScore, eRank, eFM; ItemBase eItem;
        // ranking
        bool rankOpen; int rankPage, rankSel = -1; string rankNpc = ""; List<EpRow> eps = new List<EpRow>();
        sealed class EpRow { public int Id; public string Name; public int Sort; }

        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h)
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnterRaw -= OnEnterRaw; disp.WarpUseNpcSucc -= OnWarpSucc; }
            disp = d; ents = e; tables = t; hud = h; msgT = t != null ? t.m_pMessageTable : null;
            if (disp != null) { disp.Unhandled += OnPacket; disp.SelfEnterRaw += OnEnterRaw; disp.WarpUseNpcSucc += OnWarpSucc; }
        }
        /// <summary>GameFrame REP_PC_WARP_USE_NPC_SUCC: bRingRace = false, iRXCom = 0, bInsMap = false (OpenFusion lo manda tambien al salir de una instancia).</summary>
        void OnWarpSucc() { q.Enqueue(() => { ResetState(); }); }
        void Awake() { Instance = this; }
        void OnDestroy() { if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnterRaw -= OnEnterRaw; disp.WarpUseNpcSucc -= OnWarpSucc; } if (Instance == this) { ResetState(); Instance = null; } }

        /// <summary>Entrada al mundo (nuevo ENTER_SUCC): se pierde la carrera y el punto de recuperacion del cliente (el original los reinicia al cambiar de zona/warp).</summary>
        void OnEnterRaw(byte[] b) { q.Enqueue(() => { ResetState(); }); }
        /// <summary>GameFrame.REP_PC_WARP_USE_NPC_SUCC: bRingRace = false, iRXCom = 0, bInsMap = false. Llamar desde el manejador de warp si se quiere ser fiel.</summary>
        public static void ResetState()
        {
            bool was = Racing; Racing = false; InsMap = false; InsMapNum = 0; RingNum = 0; EpId = 0; int old = RecallMapId; RecallMapId = 0;
            InsArea = default; SwitchOn = new int[0]; TopScore = TopRank = TopTime = TopRingCount = 0; RaceStartTime = RaceLimitTime = 0;
            if (Instance != null) { Instance.waiting = false; Instance.recallRequestNpc = Instance.registeredRecallNpc = 0; Instance.recallRequestUntil = 0; }
            InsMapInfo?.Invoke(InsArea, SwitchOn);
            if (was && RacingChanged != null) RacingChanged(false);
            if (old != 0 && RecallChanged != null) RecallChanged(old, 0);
        }

        // ============================================================ acciones publicas (NpcIconMode) ============================================================
        /// <summary>NPC tipo 13, boton "START RACE" (icono 6). Con carrera en curso equivale a CancelRace (cnRaceMode.StartRace: bRingRace ? CANCEL : START).</summary>
        public void StartRace(int npcId)
        {
            if (Racing) { CancelRace(npcId); return; }
            if (waiting) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_EP_RACE_START, 12);
            ClientPackets.PutInt(p, 4, npcId); ClientPackets.PutInt(p, 8, 1); ClientPackets.PutInt(p, 12, 0);
            BeginWait(); Send(p);
        }

        /// <summary>NPC tipo 13 con carrera en curso, boton "CANCEL RACE".</summary>
        public void CancelRace(int npcId)
        {
            if (waiting) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_EP_RACE_CANCEL, 4); ClientPackets.PutInt(p, 4, npcId);
            BeginWait(); Send(p);
        }

        /// <summary>NPC tipo 14 (meta): el original lo ejecuta SIN menu al hablar con el si bRingRace y no hay mision pendiente (NpcIconMode.StartEECom). Sin carrera no hace nada.</summary>
        public void EndRace(int npcId)
        {
            if (!Racing || waiting) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_EP_RACE_END, 8); ClientPackets.PutInt(p, 4, npcId); ClientPackets.PutInt(p, 8, 0);
            BeginWait(); Send(p);
        }

        /// <summary>Colision con un aro de la carrera (EpTrigger tipo Ring): REQ_EP_GET_RING {iRingLID}. Solo valido con Racing = true.</summary>
        public static void PickRing(int ringLID)
        {
            if (!Racing) return; var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_EP_GET_RING, 4); ClientPackets.PutInt(p, 4, ringLID); Send(p);
        }

        /// <summary>Icono 8 "RACE RANK" (NPC 13 y 14): abre la lista de carreras (cnRaceRankMode, version sin web).</summary>
        public void ShowRank(int npcId)
        {
            if (rankOpen) return; rankNpc = "";
            if (ents != null) { var go = ents.NpcById(npcId); var info = go != null ? go.GetComponent<NpcInfo>() : null; if (info != null) rankNpc = info.Name ?? ""; }
            BuildEpList(); rankPage = 0; rankSel = eps.Count > 0 ? 0 : -1; rankOpen = true; Sfx.Ui("Open_Screen");
        }

        /// <summary>NPC tipo 17 (NpcIconMode.StartRXCom): registra el punto de recuperacion si hay un nano equipado con skill tipo 27/28 (recall). El original lo ejecuta al hablar con el NPC, sin menu.</summary>
        public static void RegisterRecall(int npcId) { if (Instance != null) Instance.DoRecall(npcId); else Debug.LogWarning("[rxcom] RaceSystem sin inicializar"); }

        void DoRecall(int npcId)
        {
            if (npcId <= 0 || NpcUI.Sender == null || Time.unscaledTime < recallRequestUntil) return;
            bool has = false;
            if (hud != null && hud.Data != null && hud.Data.NanoSlots != null)
            {
                if (wheel == null) wheel = FindObjectOfType<NanoWheelHud>();
                for (int i = 0; i < 3 && i < hud.Data.NanoSlots.Length; i++)
                {
                    int nano = hud.Data.NanoSlots[i]; if (nano <= 0 || wheel == null) continue;
                    int skillId = wheel.BankSkill(nano); var se = skillId > 0 ? wheel.SkillOf(skillId) : null;
                    if (se != null) Debug.Log("[rxcom] Recall Point: " + skillId + " " + se.m_iSkillType);
                    if (se != null && (se.m_iSkillType == 27 || se.m_iSkillType == 28)) { has = true; break; }
                }
            }
            if (!has) { Notice("Unable to register. No Recall Nano Equipped."); return; }
            if (RecallMapId != 0 && registeredRecallNpc == npcId) return;
            recallRequestNpc = npcId; recallRequestUntil = Time.unscaledTime + 10f;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_REGIST_RXCOM, 4); ClientPackets.PutInt(p, 4, npcId); Send(p);
        }

        // ============================================================ utilidades ============================================================
        void BeginWait() { waiting = true; waitUntil = Time.unscaledTime + 10f; }
        static void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[carrera] envio: " + ex.Message); } } }
        string Message(int id) { return msgT != null && msgT.m_pMessageData != null && id >= 0 && id < msgT.m_pMessageData.Length && msgT.m_pMessageData[id] != null ? msgT.m_pMessageData[id].m_szString : "(message " + id + ")"; }
        void Notice(string text) { if (SocialMenu.Instance != null) SocialMenu.Instance.Notice(text); else Debug.Log("[carrera] " + text); }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }

        // ============================================================ red ============================================================
        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_REP_EP_RACE_START_SUCC && b.Length >= 12)
            {
                int limit = I(b, 8);
                q.Enqueue(() =>
                {
                    waiting = false; bool was = Racing; Racing = true; RingNum = 0; RaceStartTime = Time.time; RaceLimitTime = limit; timeoutSoundPlayed = false; ringFx = 0f;
                    Sfx.Ui("RaceStart"); if (!was && RacingChanged != null) RacingChanged(true);
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_EP_RACE_START_FAIL)
            {
                int code = b.Length >= 4 ? I(b, 0) : 0;
                q.Enqueue(() => { waiting = false; SetNotRacing(); if (code == 8) Notice(Message(150)); });   // GameFrame: iErrorCode == 8 -> SendSystemMessageBox(150)
            }
            else if (type == PacketIds.P_FE2CL_REP_EP_RACE_END_SUCC && b.Length >= 64) { var body = b; q.Enqueue(() => OnEnd(body)); }
            else if (type == PacketIds.P_FE2CL_REP_EP_RACE_END_FAIL) { q.Enqueue(() => { waiting = false; SetNotRacing(); }); }
            else if (type == PacketIds.P_FE2CL_REP_EP_RACE_CANCEL_SUCC) { q.Enqueue(() => { waiting = false; SetNotRacing(); }); }
            else if (type == PacketIds.P_FE2CL_REP_EP_RACE_CANCEL_FAIL) { q.Enqueue(() => { waiting = false; }); }   // GameFrame solo apaga bRingRace con CANCEL_SUCC
            else if (type == PacketIds.P_FE2CL_REP_EP_GET_RING_SUCC && b.Length >= 8)
            {
                int n = I(b, 4); q.Enqueue(() => { RingNum = n; ringFx = 2f; Sfx.Ui("Ring"); });
            }
            else if (type == PacketIds.P_FE2CL_REP_EP_GET_RING_FAIL) { Debug.Log("[carrera] ring fail"); }
            else if (type == PacketIds.P_FE2CL_INSTANCE_MAP_INFO && ValidInstanceInfo(b))
            {   // Pack 4: iInstanceMapNum@0, ulong iCreateTick@4, X/Y/Z min/max @12..32, iEP_ID@36, TopScore@40, TopRank@44, TopTime@48, TopRingCount@52
                int ep = I(b, 36), ts = I(b, 40), tr = I(b, 44), tt = I(b, 48), tc = I(b, 52);
                int mn = I(b, 0); float ax0 = I(b, 12) * 0.01f, ax1 = I(b, 16) * 0.01f, ay0 = I(b, 20) * 0.01f, ay1 = I(b, 24) * 0.01f;
                int sc = I(b, 56);
                var sw = new int[sc]; for (int k = 0; k < sc; k++) sw[k] = I(b, 60 + 4 * k);
                q.Enqueue(() => { InsMap = true; InsMapNum = mn; EpId = ep; TopScore = ts; TopRank = tr; TopTime = tt; TopRingCount = tc; var rc = new Rect(ax0, ay0, ax1 - ax0, ay1 - ay0); InsArea = rc; SwitchOn = sw; if (InsMapInfo != null) InsMapInfo(rc, sw); });
            }
            else if (type == PacketIds.P_FE2CL_REP_REGIST_RXCOM && b.Length >= 16)
            {
                // Response: map number + XYZ, not the requested NPC ID.
                int map = I(b, 0); q.Enqueue(() => {
                    if (map <= 0) return;
                    int old = RecallMapId; RecallMapId = map; registeredRecallNpc = recallRequestNpc;
                    recallRequestNpc = 0; recallRequestUntil = 0; RecallChanged?.Invoke(old, map);
                    Notice("Recall Point registered! Use your Recall Nano to return to this point."); Sfx.Ui("Action_Sucess");
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_REGIST_RXCOM_FAIL)
                q.Enqueue(() => { recallRequestNpc = 0; recallRequestUntil = 0; Notice("Unable to register the Recall Point."); });
            else if (type == PacketIds.P_FE2CL_PC_INVEN_FULL_MSG) { q.Enqueue(() => { invenFull = true; }); }
        }

        void SetNotRacing() { bool was = Racing; Racing = false; RingNum = 0; if (was && RacingChanged != null) RacingChanged(false); }
        public static bool ValidInstanceInfo(byte[] b)
        {
            if (b == null || b.Length < 60) return false;
            int count = I(b, 56);
            return count >= 0 && count <= 4096 && count <= (b.Length - 60) / 4 && I(b, 12) <= I(b, 16) && I(b, 20) <= I(b, 24);
        }

        /// <summary>REP_EP_RACE_END_SUCC (72 B): iEPRaceMode@0, iEPRaceTime@4, iEPRingCnt@8, iEPScore@12, iEPRank@16, iEPRewardFM@20, iEPTopScore@24, iEPTopRank@28, iEPTopTime@32, iEPTopRingCount@36,
        /// iFusionMatter@40, RewardItem{sItemBase@44(12), eIL@56, iSlotNum@60}, iFatigue@64, iFatigue_Level@68.</summary>
        void OnEnd(byte[] b)
        {
            waiting = false; SetNotRacing();
            eTime = I(b, 4); eRing = I(b, 8); eScore = I(b, 12); eRank = I(b, 16); eFM = I(b, 20);
            TopScore = I(b, 24); TopRank = I(b, 28); TopTime = I(b, 32); TopRingCount = I(b, 36);
            int fm = I(b, 40); if (hud != null && hud.Data != null) hud.Data.FusionMatter = fm;
            eItem = new ItemBase { Type = BitConverter.ToInt16(b, 44), Id = BitConverter.ToInt16(b, 46), Opt = I(b, 48), TimeLimit = I(b, 52) };
            int eil = I(b, 56), slot = I(b, 60);
            if (eil == 1 && slot >= 0 && slot < InventoryWindow.Items.Length && eItem.Id > 0) InventoryWindow.Items[slot] = eItem;   // eIL 4 = sin premio
            invenFull = false; endOpen = true; Sfx.Ui("RaceFinish");
        }

        // ============================================================ bucle ============================================================
        void Update()
        {
            Action a; int guard = 0; while (guard++ < 64 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[carrera] " + ex.Message); } }
            if (waiting && Time.unscaledTime > waitUntil) waiting = false;
            if (ringFx > 0f) ringFx -= Time.deltaTime;
            if (Racing)
            {   // cnGUINanocom.Update: sonido Timer_Race cuando quedan menos de 11 s
                if (RaceLimitTime - (Time.time - RaceStartTime) < 11f && !timeoutSoundPlayed) { timeoutSoundPlayed = true; Sfx.Ui("Timer_Race"); }
            }
            else timeoutSoundPlayed = false;
            if (Input.GetKeyDown(KeyCode.Escape) && !waiting)
            {
                if (endOpen) { endOpen = false; Sfx.Ui("Close_Screen"); }
                else if (rankOpen) { rankOpen = false; Sfx.Ui("Close_Screen"); }
            }
        }

        // ============================================================ dibujo ============================================================
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        static SkinStyle R(string n) { return Skin.Get(SkRace, n); }
        static SkinStyle G(string n) { return Skin.Get(SkGui, n); }
        static string Clock(int sec) { return (sec / 3600).ToString("0") + ":" + (sec / 60 % 60).ToString("00") + ":" + (sec % 60).ToString("00"); }   // cnRaceMode.OnEndGUI
        static string N2(int v) { return v.ToString("N", CultureInfo.InvariantCulture); }

        void OnGUI()
        {
            if (Screen.height <= 0) return; var e = Event.current; if (e == null) return;
            bool hudOn = Racing && !endOpen; if (!hudOn && !endOpen && !rankOpen) return;
            float W = Screen.width, H = Screen.height;
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1450; GUI.color = Color.white;
            try
            {
                if (hudOn) DrawHud(e, W, H);
                if (endOpen) DrawEnd(e, W, H);
                if (rankOpen) DrawRank(e, W, H);
                if ((endOpen || rankOpen) && (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel)) e.Use();
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        // ---- HUD de carrera (cnGUINanocom: icono de aro + "x 000", Time, Limit, cuenta atras) ----
        void DrawHud(Event e, float W, float H)
        {
            if (e.type != EventType.Repaint) return;
            float Sc = Mathf.Max(0.4f, GameOptions.UiScale);
            GUI.matrix = Mat(new Vector2(W, 0f), Sc);
            var f16 = R("Font16"); var f14 = R("Font14"); var small = R("SmallFont");
            float bounce = ringFx > 0f ? Mathf.Abs(Mathf.Sin(Mathf.PI * 0.5f + Mathf.PI * (2f - ringFx) * (2f - ringFx) * (2f - ringFx))) * ringFx * 40f * 0.3f : 0f;
            var box = new Rect(HudPos.x, HudPos.y, 220f, 66f);
            UiKit.Fill(box, new Color(0f, 0.05f, 0.12f, 0.55f)); UiKit.Outline(box, new Color(0.4f, 0.8f, 1f, 0.6f));
            GUI.BeginGroup(box);
            Txt(new Rect(12f, 6f - bounce, 196f, 24f), "RINGS  x " + RingNum.ToString("000"), f16, Color.yellow, true);
            int cs = Mathf.Max(0, (int)((Time.time - RaceStartTime) * 100f)); int s = cs / 100, m = s / 60; s -= m * 60; cs -= (cs / 100) * 100;
            Txt(new Rect(12f, 28f, 196f, 18f), Loc.T("Time") + ": " + m.ToString("00") + ":" + s.ToString("00") + ":" + cs.ToString("00"), f14, Color.white, true);
            int lm = (int)(RaceLimitTime / 60f), ls = (int)RaceLimitTime - lm * 60;
            Txt(new Rect(12f, 46f, 196f, 16f), Loc.T("Limit") + " : " + lm.ToString("00") + ":" + ls.ToString("00"), small, Color.red, true);
            GUI.EndGroup();
            // cuenta atras: 10..1 en el centro, escala 1,5 -> 1 y alfa 0 -> 1 (MenuBigFont70 original)
            float rem = RaceLimitTime - (Time.time - RaceStartTime);
            if (rem > 0f && rem < 11f)
            {
                int num = Mathf.FloorToInt(rem); float f = Mathf.Min(0.5f, rem - num); float sc = (1.5f - f) * 4f * Mathf.Max(0.4f, GameOptions.UiScale);
                GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), sc);
                Txt(new Rect(-60f, -20f, 120f, 40f), num.ToString(), f16, new Color(1f, 1f, 1f, f * 2f), false);
            }
        }

        static void Txt(Rect r, string text, SkinStyle st, Color c, bool shadow)
        {
            if (st == null) return;
            if (shadow) Skin.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, st, new Color(0f, 0f, 0f, c.a));
            Skin.Label(r, text, st, c);
        }

        // ---- ventana de resultado (cnRaceMode.OnEndGUI) ----
        void DrawEnd(Event e, float W, float H)
        {
            float Sc = Mathf.Max(0.3f, Mathf.Min(GameOptions.UiScale, H / 450f, W / 420f)); float ph = 434f * Sc, bar = (H - ph) * 0.5f;
            if (e.type == EventType.Repaint) { UiKit.Fill(new Rect(0f, 0f, W, bar), Color.black); UiKit.Fill(new Rect(0f, H - bar, W, bar), Color.black); }
            GUI.matrix = Mat(new Vector2(W - 398f * Sc, bar), Sc);
            var panel = new Rect(0f, 0f, 398f, 434f);
            Skin.Box(panel, R("EndRaceBack"), "");
            if (R("EndRaceBack") == null) UiKit.Fill(panel, new Color(0f, 0.08f, 0.2f, 0.9f));
            GUI.BeginGroup(panel);
            var cyan = new Color(0.8f, 1f, 1f); var f1 = R("font1"); var f3 = R("font3"); var f4 = R("font4"); var fs = R("SmallFont");
            int num = Mathf.Clamp(6 - eRank, 0, 5);
            for (int i = 0; i < 5; i++) Skin.Box(new Rect(158f - i * 22f, 14f, 19f, 17f), R("StarBack"), "");
            for (int j = 0; j < num; j++) Skin.Box(new Rect(158f - j * 22f, 14f, 19f, 17f), R("Star"), "");
            string verdict = num == 5 ? Loc.T("Genius") + "!" : num == 4 ? Loc.T("Awesome") + "!" : num == 3 ? Loc.T("Good") : num == 2 ? Loc.T("Not Bad") : num == 1 ? Loc.T("Bleh") + "!" : "";
            Skin.Label(new Rect(202f, 14f, 120f, 20f), verdict, f3, cyan);
            Skin.Label(new Rect(20f, 83f, 62f, 12f), Loc.T("TIME"), f1, cyan); Skin.Label(new Rect(20f, 103f, 62f, 12f), Loc.T("PODS"), f1, cyan); Skin.Label(new Rect(20f, 133f, 62f, 12f), Loc.T("SCORE"), f1, cyan);
            Skin.Label(new Rect(100f, 78f, 65f, 16f), Clock(eTime), fs, Color.yellow); Skin.Label(new Rect(100f, 98f, 65f, 16f), eRing.ToString(), fs, Color.yellow); Skin.Label(new Rect(100f, 128f, 80f, 16f), N2(eScore), fs, Color.yellow);
            var dark = new Color(0f, 0.2f, 0.4f);
            Skin.Label(new Rect(205f, 50f, 100f, 10f), Loc.T("MY BEST RECORD"), f4, dark);
            var lb = new Color(0f, 0.8f, 1f);
            Skin.Label(new Rect(210f, 70f, 62f, 12f), Loc.T("TIME"), f1, lb); Skin.Label(new Rect(210f, 90f, 62f, 12f), Loc.T("PODS"), f1, lb); Skin.Label(new Rect(210f, 120f, 62f, 12f), Loc.T("SCORE"), f1, lb);
            Skin.Label(new Rect(295f, 65f, 65f, 16f), Clock(TopTime), fs, cyan); Skin.Label(new Rect(295f, 85f, 65f, 16f), TopRingCount.ToString(), fs, cyan); Skin.Label(new Rect(295f, 117f, 80f, 16f), N2(TopScore), fs, cyan);
            Skin.Label(new Rect(10f, 195f, 80f, 20f), Loc.T("REWARD") + ":", f3, dark);
            int row = 0;
            if (eItem.Id > 0)
            {
                float y = 224f + row * 85f; Skin.Box(new Rect(13f, 216f + row * 85f, 367f, 76f), R("bar"), "");
                if (journal == null) journal = FindObjectOfType<JournalWindow>();
                var ic = journal != null ? journal.ItemTexture(eItem.Type, eItem.Id) : null; if (ic != null && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(20f, y, 62f, 62f), ic, ScaleMode.ScaleToFit, true);
                string nm = journal != null ? journal.ItemName(eItem.Type, eItem.Id) : ""; Skin.Label(new Rect(100f, y, 200f, 20f), nm, f3, cyan);
                if (eItem.Type < 7) { var meta = ItemTbl.Get(tables, eItem.Type, eItem.Id); if (meta.Ok) Skin.Label(new Rect(100f, y + 20f, 200f, 20f), Loc.T("LEVEL") + " " + meta.Level, f3, Color.yellow); }
                if (invenFull) Skin.Label(new Rect(120f, y + 30f, 200f, 20f), Loc.T("INVENTORY FULL") + "!", f3, Color.red);
                row++;
            }
            { float y = 224f + row * 85f; Skin.Box(new Rect(13f, 216f + row * 85f, 367f, 76f), R("bar"), "");
              var fmt = UiKit.Tex("fmfpo"); if (fmt != null && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(20f, y, 62f, 62f), fmt, ScaleMode.ScaleToFit, true);
              Skin.Label(new Rect(100f, y, 200f, 40f), N2(eFM) + "\n" + Loc.T("FUSION MATTER"), f3, cyan); }
            if (Skin.Button(new Rect(230f, 393f, 154f, 26f), Loc.T("ACCEPT"), G("button"), !waiting, true)) { Sfx.Button(); endOpen = false; }
            GUI.EndGroup();
        }

        // ---- RACE RANK (cnRaceRankMode sin la web) ----
        void BuildEpList()
        {
            eps.Clear(); var it = tables != null ? tables.m_pInstanceTable : null; if (it == null || it.m_pInstanceData == null) return;
            for (int i = 0; i < it.m_pInstanceData.Length && eps.Count < 100; i++)
            {
                var d = it.m_pInstanceData[i]; if (d == null || d.m_iIsEP <= 0) continue;
                int ni = d.m_iInstanceNameID; if (it.m_pWarpNameData == null || ni < 0 || ni >= it.m_pWarpNameData.Length || it.m_pWarpNameData[ni] == null) continue;
                eps.Add(new EpRow { Id = i, Name = it.m_pWarpNameData[ni].m_pstrNameString, Sort = d.m_SortIndex });
            }
            eps.Sort((a, b) => a.Sort.CompareTo(b.Sort));
        }

        void DrawRank(Event e, float W, float H)
        {
            float Sc = Mathf.Max(0.3f, Mathf.Min(GameOptions.UiScale, W / 860f, H / 560f));
            GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
            if (e.type == EventType.Repaint) { UiKit.Fill(new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc), new Color(0f, 0f, 0f, 0.6f)); }
            var win = new Rect(-400f, -260f, 800f, 520f);
            Skin.Box(win, G("box"), "");
            GUI.BeginGroup(win);
            var big = G("BigFont16"); var lab = G("label");
            Skin.Label(new Rect(20f, 12f, 400f, 26f), Loc.T("RANKINGS"), big, Color.yellow);
            if (!string.IsNullOrEmpty(rankNpc)) Skin.Label(new Rect(300f, 12f, 440f, 26f), rankNpc, lab, Color.white);
            Skin.Label(new Rect(20f, 48f, 300f, 22f), Loc.T("locations"), big, Color.white);
            int pages = Mathf.Max(1, (eps.Count + 4) / 5);
            for (int i = 0; i < 5; i++)
            {
                int idx = rankPage * 5 + i; if (idx >= eps.Count) break;
                var r = new Rect(20f, 76f + i * 66f, 300f, 60f); bool sel = idx == rankSel;
                if (sel && e.type == EventType.Repaint) UiKit.Fill(r, new Color(0.2f, 0.6f, 1f, 0.35f));
                if (Skin.Button(r, "", G("button"), !waiting, true)) { Sfx.Button(); rankSel = idx; }
                Skin.Label(new Rect(r.x + 10f, r.y, r.width - 20f, r.height), eps[idx].Name, big, sel ? Color.yellow : Color.white);
            }
            if (Skin.Button(new Rect(20f, 412f, 110f, 25f), Loc.T("previous") + " 5", G("button"), rankPage > 0)) { rankPage--; }
            if (Skin.Button(new Rect(210f, 412f, 110f, 25f), Loc.T("next") + " 5", G("button"), rankPage < pages - 1)) { rankPage++; }
            Skin.Label(new Rect(20f, 442f, 300f, 20f), (rankPage + 1) + " " + Loc.T("of") + " " + pages, lab, Color.white);
            // panel derecho: carrera elegida y mejor resultado propio conocido
            var right = new Rect(340f, 48f, 440f, 400f); UiKit.Fill(right, new Color(0f, 0.08f, 0.2f, 0.7f));
            if (rankSel >= 0 && rankSel < eps.Count)
            {
                var ep = eps[rankSel]; Skin.Label(new Rect(right.x + 15f, right.y + 12f, right.width - 30f, 26f), Loc.T("location") + ": " + ep.Name, big, Color.yellow);
                if (ep.Id == EpId && TopScore > 0)
                {
                    Skin.Label(new Rect(right.x + 15f, right.y + 60f, right.width - 30f, 22f), Loc.T("MY ALL TIME BEST SCORE") + ":", lab, Color.white);
                    Skin.Label(new Rect(right.x + 15f, right.y + 86f, right.width - 30f, 22f), N2(TopScore) + "   (" + Loc.T("TIME") + " " + Clock(TopTime) + ", " + Loc.T("PODS") + " " + TopRingCount + ")", big, Color.white);
                }
                else Skin.Label(new Rect(right.x + 15f, right.y + 60f, right.width - 30f, 22f), Loc.T("No score registered yet."), lab, Color.white);
                Skin.Label(new Rect(right.x + 15f, right.y + 330f, right.width - 30f, 60f), "The online ranking table is not available in this client.", lab, new Color(1f, 1f, 1f, 0.6f));
            }
            if (Skin.Button(new Rect(win.width - 42f, 10f, 32f, 32f), "", G("CloseButton"), !waiting)) { rankOpen = false; Sfx.Ui("Close_Screen"); }
            GUI.EndGroup();
        }
    }
}
