// NanocomHud: reconstruccion del "nanocom" de la esquina superior derecha del cliente original (FusionFall / Retrobution, Unity Web Player 2.5):
//   - marco + medidor circular de Fusion Matter (cnGUINanocom.RenderMenu, rama expBartype == eCircleType)
//   - minimapa (cnGUINanocom.RenderMaps / RenderMinimap): 16 teselas all_minimap_NN, mascara circular nanocom_minimap_a, iconos de NPC, flecha del jugador, cono de camara, lineas
//   - nombre de la zona (dongName) bajo el minimapa
//   - texto de la mision actual (MissionTitle / MissionBody de cnGUINanocom.Update + RenderMenu) y destello de "tarea completada" (burst)
//   - iconos de buffs/debuffs del jugador (CnGuiSkillBuffIcon.UpdateIconList / OnOwnGUI) y de buffs "cash" con su temporizador
// NO incluido (ver REPORT.md): menu desplegable del nanocom (MY STUFF/JOURNAL/...), reloj/ping, buzon de correo, mensajes del nanocom (RenderNanoMessage), carrera de anillos,
//   waypoints de usuario (UserWaypoints), iconos de buffs sobre el objetivo (UpdateIconListForNPC).
//
// Maquetacion: todo en "espacio de diseno" (rects tal cual el original, tras InitCycleFMBar) y se transforma p' = pivot + (p - pivot) * s, s = Screen.height / 681
// (FFGUIUtility.GetUiScale con uiScaleFactor = 1). El nanocom usa ScaleAroundPivot(TopRight), los iconos de buffs propios ScaleAroundPivot(TopLeft), igual que el original.
// Se dibuja en coordenadas de pantalla (sin GUI.BeginGroup): los recortes del grupo original (NanoRect) se reproducen recortando el rect/UV a mano.
//
// Dependencias del proyecto: PlayerHud (Data.Level/FusionMatter/MaxFusionMatter, fuentes), WorldEntities (Self, SelfId, NpcObjects, NpcTable, Missions), PacketDispatcher (Unhandled, SelfEnterRaw),
// BitmapFontLoader/BitmapFont/MiniJson (BitmapFontLoader.cs), NpcInfo/NpcUI.Sender (NpcUI.cs), ClientPackets.New, PacketIds, XdtTableScript (Tables.g.cs).
// Texturas: Resources/OpenFusion/UI/Textures/<nombre> (ya volteadas). Iconos de skill: Resources/OpenFusion/Icons/skillicon_NN. Datos opcionales: Resources/OpenFusion/UI/Data/{worldname,clientnpc}.json.
// SIN COMPILAR EN UNITY (revisado a mano contra la API de 2022.3 LTS, C# 9).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class NanocomHud : MonoBehaviour
    {
        // ====================================================================== API publica ======================================================================
        public bool Visible = true;
        public bool ShowMinimap = true, ShowFusionMeter = true, ShowMission = true, ShowBuffs = true;

        /// <summary>fMiniMapRatio del original (8 por defecto, 4..16; el boton '+' lo resta, el '-' lo suma). Metros visibles = 256 * ratio / 8 (a 8: 256 m de lado).</summary>
        public float MiniMapRatio = 8f;
        public void ZoomIn() { MiniMapRatio = Mathf.Max(4f, MiniMapRatio - 1f); }
        public void ZoomOut() { MiniMapRatio = Mathf.Min(16f, MiniMapRatio + 1f); }

        /// <summary>bViewMob / bViewShiny: se activan solos con los buffs MiniMapEnemy (bit 4096) y MiniMapTresure (bit 8192); se pueden forzar a mano.</summary>
        public bool ViewMob, ViewShiny;
        /// <summary>bViewWayPoint + WayPointpt (flecha de la mision seleccionada, m_iSTGrantWayPoint) y cnOption.displayOption.bWayPoint.</summary>
        public bool ViewWayPoint, WayPointOption = true; public Vector3 WayPoint;
        /// <summary>cnOption.displayOption.bCurrentObjective.</summary>
        public bool CurrentObjective = true;
        /// <summary>Enviar CL2FE_REQ_PC_SET_CURRENT_MISSION_ID cuando cambia la mision seleccionada (como cnMissionManager.StartSelectMissionTask).</summary>
        public bool SendCurrentMission = true;
        /// <summary>cnOwnAvatarStatus.iFatigureLevel / iFatigureValue: el cliente original los fija a 1 / 100 al entrar (el medidor "bonus" parpadea siempre).</summary>
        public int FatigueLevel = 1, FatigueValue = 100;
        /// <summary>Si el jugador gira distinto de lo esperado (el avatar propio del puerto tiene el contenedor a angulo+180), corrige el giro de la flecha del minimapa.</summary>
        public float PlayerYawOffset = 0f;
        /// <summary>Segundos entre refrescos de la lista de NPC visibles y de la zona (fCheckInterval = 0.5).</summary>
        public float CheckInterval = 0.5f;

        /// <summary>Shader/material del minimapa (OpenFusion/MiniMap). Si ambos son null se hace Shader.Find("OpenFusion/MiniMap"): debe estar en "Always Included Shaders" o referenciado por un material.
        /// Sin shader se dibuja un minimapa SIN mascara circular (cuadrado) y por debajo del marco.</summary>
        public Shader MinimapShader; public Material MinimapMaterial;

        /// <summary>Nombre de la zona (dongName) en una posicion del mundo. null => se usa Data/worldname.json (WorldNameData del original).</summary>
        public Func<Vector3, string> DongNameProvider;
        /// <summary>Posicion de un "ClientNPC" (MiniMapNpc.m_pElements: m_iType -> kPos) para el waypoint. null => Data/clientnpc.json.</summary>
        public Func<int, Vector3?> ClientNpcPos;
        /// <summary>Companeros de grupo (icono 17) y tesoros (icono 26, solo con ViewShiny). Opcionales: el puerto aun no tiene grupos ni shinies.</summary>
        public Func<IEnumerable<Vector3>> GroupMemberPositions, ShinyPositions;
        /// <summary>Nombre de icono ("skillicon_12") -> textura. null => Resources "OpenFusion/Icons/<nombre>".</summary>
        public Func<string, Texture> IconProvider;

        public string LabelFontJson, CounterFontJson;   // por defecto los de PlayerHud

        /// <summary>Nombre y zona actuales (solo lectura).</summary>
        public string DongName { get { return dongName; } }
        public string MissionTitle { get { return title; } }
        public string MissionBody { get { return body; } }

        public void Init(PlayerHud hud, WorldEntities ents, PacketDispatcher dispatcher, XdtTableScript tables)
        {
            Detach();
            this.hud = hud; this.ents = ents; this.dispatcher = dispatcher; this.tables = tables;
            mt = tables != null ? tables.m_pMissionTable : null;
            npcTable = tables != null ? tables.m_pNpcTable : null;
            questItemTable = tables != null ? tables.m_pQuestItemTable : null;
            skillTable = tables != null ? tables.m_pSkillTable : null;
            avatarTable = tables != null ? tables.m_pAvatarTable : null;
            BuildTables();
            if (dispatcher != null) { dispatcher.Unhandled += OnPacket; dispatcher.SelfEnterRaw += OnEnterRaw; }
        }

        public void Detach()
        {
            if (dispatcher != null) { dispatcher.Unhandled -= OnPacket; dispatcher.SelfEnterRaw -= OnEnterRaw; }
            dispatcher = null;
        }

        /// <summary>Para el codigo de combate: actualiza iSkillconditionBitFlag del jugador propio (p. ej. al recibir un sCAttackResult con iConditionBitFlag).</summary>
        public void SetConditionFlags(int flags) { buffFlags = flags; buffsDirty = true; }
        /// <summary>Destello de "tarea completada" (cnGUINanocom.ReceiveTaskEnd): 2 s, fundido el ultimo segundo.</summary>
        public void ReceiveTaskEnd() { taskEnd = true; taskEndTimer = 2f; }

        // ====================================================================== estado ======================================================================
        PlayerHud hud; WorldEntities ents; PacketDispatcher dispatcher; XdtTableScript tables;
        MissionTableScript mt; NpcTableScript npcTable; QuestItemTableScript questItemTable; SkillTableScript skillTable; AvatarTableScript avatarTable;
        readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

        void OnPacket(uint type, byte[] body) { queue.Enqueue(() => Handle(type, body)); }
        void OnEnterRaw(byte[] body) { queue.Enqueue(() => EnterRaw(body)); }

        // ---- recursos ----
        const string UI = "OpenFusion/UI/Textures/";
        readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>(); readonly HashSet<string> missing = new HashSet<string>();
        Texture2D T(string n)
        {
            Texture2D t; if (tex.TryGetValue(n, out t)) return t; if (missing.Contains(n)) return null;
            t = Resources.Load<Texture2D>(UI + n);
            if (t == null) { missing.Add(n); Debug.LogWarning("[nanocom] falta textura " + UI + n); return null; }
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp; tex[n] = t; return t;
        }
        readonly Dictionary<string, Texture> iconCache = new Dictionary<string, Texture>();
        Texture Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null; Texture t;
            if (iconCache.TryGetValue(name, out t)) return t;
            t = IconProvider != null ? IconProvider(name) : Resources.Load<Texture2D>("OpenFusion/Icons/" + name);
            if (t == null) Debug.LogWarning("[nanocom] falta icono " + name);
            iconCache[name] = t; return t;
        }

        BitmapFont chalet, jeffe14; bool fontsTried;
        void EnsureFonts()
        {
            if (fontsTried) return; fontsTried = true;
            string lf = !string.IsNullOrEmpty(LabelFontJson) ? LabelFontJson : (hud != null && !string.IsNullOrEmpty(hud.LabelFontJson) ? hud.LabelFontJson : "ChaletBook-Regular_Small__1018");
            string cf = !string.IsNullOrEmpty(CounterFontJson) ? CounterFontJson : (hud != null && !string.IsNullOrEmpty(hud.CounterFontJson) ? hud.CounterFontJson : "JEFFE___14__903");
            chalet = BitmapFontLoader.Load(lf); jeffe14 = BitmapFontLoader.Load(cf);
        }

        // ---- datos opcionales (zona y ClientNPC) ----
        sealed class DongArea { public Rect Area; public string Dong, Zone; }
        readonly List<DongArea> dongs = new List<DongArea>(); readonly Dictionary<int, Vector3> clientNpc = new Dictionary<int, Vector3>(); readonly List<KeyValuePair<int, Vector3>> clientNpcAll = new List<KeyValuePair<int, Vector3>>(); bool dataTried;
        static float F(object o) { return o is double ? (float)(double)o : 0f; }
        void LoadData()
        {
            if (dataTried) return; dataTried = true;
            try
            {
                var ta = Resources.Load<TextAsset>("OpenFusion/UI/Data/worldname");
                var root = ta != null ? MiniJson.Parse(ta.text) as Dictionary<string, object> : null;
                var arr = root != null && root.ContainsKey("a") ? root["a"] as List<object> : null;
                if (arr != null) foreach (var o in arr)
                {
                    var r = o as List<object>; if (r == null || r.Count < 6) continue;
                    dongs.Add(new DongArea { Area = new Rect(F(r[0]), F(r[1]), F(r[2]), F(r[3])), Dong = r[4] as string ?? "", Zone = r[5] as string ?? "" });
                }
                var tb = Resources.Load<TextAsset>("OpenFusion/UI/Data/clientnpc");
                var rb = tb != null ? MiniJson.Parse(tb.text) as Dictionary<string, object> : null;
                var pa = rb != null && rb.ContainsKey("p") ? rb["p"] as List<object> : null;
                if (pa != null) foreach (var o in pa)
                {
                    var r = o as List<object>; if (r == null || r.Count < 4) continue;
                    int id = (int)F(r[0]); var cp = new Vector3(F(r[1]), F(r[2]), F(r[3])); clientNpcAll.Add(new KeyValuePair<int, Vector3>(id, cp)); if (!clientNpc.ContainsKey(id)) clientNpc[id] = cp;
                }
            }
            catch (Exception e) { Debug.LogWarning("[nanocom] datos opcionales: " + e.Message); }
        }

        // ---- tablas ----
        readonly Dictionary<int, MissionElement> taskById = new Dictionary<int, MissionElement>();
        readonly Dictionary<int, List<MissionElement>> tasksOfMission = new Dictionary<int, List<MissionElement>>();
        readonly Dictionary<int, NpcTableElement> npcRows = new Dictionary<int, NpcTableElement>();
        readonly Dictionary<int, QuestItemElement> qiRows = new Dictionary<int, QuestItemElement>();
        void BuildTables()
        {
            taskById.Clear(); tasksOfMission.Clear(); npcRows.Clear(); qiRows.Clear();
            var md = mt != null ? mt.m_pMissionData : null;
            if (md != null) foreach (var m in md)
            {
                if (m == null) continue;
                if (!taskById.ContainsKey(m.m_iHTaskID)) taskById[m.m_iHTaskID] = m;
                List<MissionElement> l; if (!tasksOfMission.TryGetValue(m.m_iHMissionID, out l)) tasksOfMission[m.m_iHMissionID] = l = new List<MissionElement>(); l.Add(m);   // orden de la tabla = hijos de cnMissionNode (MakeMissionGroup)
            }
            var nd = npcTable != null ? npcTable.m_pNpcData : null; if (nd != null) foreach (var e in nd) if (e != null && !npcRows.ContainsKey(e.m_iNpcNumber)) npcRows[e.m_iNpcNumber] = e;
            var qd = questItemTable != null ? questItemTable.m_pItemData : null; if (qd != null) foreach (var e in qd) if (e != null && !qiRows.ContainsKey(e.m_iItemNumber)) qiRows[e.m_iItemNumber] = e;
        }
        NpcTableElement NpcRow(int id)
        {
            NpcTableElement e; if (npcRows.TryGetValue(id, out e)) return e;
            var nd = npcTable != null ? npcTable.m_pNpcData : null; return nd != null && id >= 0 && id < nd.Length ? nd[id] : null;   // el original indexa m_pNpcData[id] directamente
        }
        string NpcName(int id)
        {
            var r = NpcRow(id); var sd = npcTable != null ? npcTable.m_pNpcStringData : null;
            return r != null && sd != null && r.m_iNpcName >= 0 && r.m_iNpcName < sd.Length && sd[r.m_iNpcName] != null ? sd[r.m_iNpcName].m_strName ?? "" : "";
        }
        string QuestItemName(int id)
        {
            QuestItemElement e; if (!qiRows.TryGetValue(id, out e)) { var qd = questItemTable != null ? questItemTable.m_pItemData : null; e = qd != null && id >= 0 && id < qd.Length ? qd[id] : null; }
            var sd = questItemTable != null ? questItemTable.m_pItemStringData : null;
            return e != null && sd != null && e.m_iItemName >= 0 && e.m_iItemName < sd.Length && sd[e.m_iItemName] != null ? sd[e.m_iItemName].m_strName ?? "" : "";
        }
        static int At(int[] a, int i) { return a != null && i >= 0 && i < a.Length ? a[i] : 0; }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static short Sh(byte[] b, int o) { return BitConverter.ToInt16(b, o); }

        // ====================================================================== red ======================================================================
        void Handle(uint type, byte[] b)
        {
            try
            {
                if (b == null) return;
                if (type == PacketIds.P_FE2CL_GM_REP_PC_SET_VALUE) { if (b.Length >= 12 && I(b, 8) == pingValue && pingValue != 0) { pingMs = (uint)((Time.time - pingSent) * 1000f); } }   // NetworkStats.ReceivePing
                else if (type == PacketIds.P_FE2CL_PC_BUFF_UPDATE) { if (b.Length >= 44) { SetConditionFlags(I(b, 40)); GameClient.ApplyBuff(I(b, 0), I(b, 4), I(b, 32)); } }                       // sP_FE2CL_PC_BUFF_UPDATE (44 B): iConditionBitFlag @40
                else if (type == PacketIds.P_FE2CL_PC_CASH_BUFF_UPDATE) { if (b.Length >= 40) CashUpdate(I(b, 0), I(b, 4), BitConverter.ToUInt64(b, 16), I(b, 36)); }   // eCSTB@0 eTBU@4 TimeBuff@8 (iTimeDuration @16) iConditionBitFlag@36
                else if (type == PacketIds.P_FE2CL_CHAR_TIME_BUFF_TIME_OUT) { if (b.Length >= 12 && I(b, 0) == 1 && ents != null && I(b, 4) == ents.SelfId) { int fl = I(b, 8); SetConditionFlags(fl); if ((fl & 1) == 0) GameClient.ApplyBuff(1, 2, 0); if ((fl & 2) == 0) GameClient.ApplyBuff(3, 2, 0); } }   // eCT==1 (PC)
                else if (type == PacketIds.P_FE2CL_REP_PC_TASK_START_SUCC) { if (b.Length >= 8) TaskStart(I(b, 0), I(b, 4)); }
                else if (type == PacketIds.P_FE2CL_REP_PC_TASK_END_SUCC) { if (b.Length >= 4) TaskEnd(I(b, 0)); }
                else if (type == PacketIds.P_FE2CL_REP_PC_TASK_END_FAIL) { if (b.Length >= 8) TaskEndFail(I(b, 0), I(b, 4)); }
                else if (type == PacketIds.P_FE2CL_REP_PC_TASK_STOP_SUCC) { if (b.Length >= 4) TaskStop(I(b, 0)); }
                else if (type == PacketIds.P_FE2CL_REP_PC_KILL_QUEST_NPCs_SUCC) { if (b.Length >= 4) KillMissionNpc(I(b, 0)); }
                else if (type == PacketIds.P_FE2CL_REP_PC_GIVE_ITEM_SUCC) { if (b.Length >= 20 && (I(b, 0) == 2 || Sh(b, 8) == 8)) SetQuestItem(I(b, 4), Sh(b, 10), I(b, 12)); }   // eIL@0 slot@4 sItemBase@8 {iType, iID, iOpt}
                else if (type == PacketIds.P_FE2CL_REP_PC_ITEM_DELETE_SUCC) { if (b.Length >= 8 && I(b, 0) == 2) SetQuestItem(I(b, 4), 0, 0); }
                else if (type == PacketIds.P_FE2CL_REP_REWARD_ITEM)
                {   // sP_FE2CL_REP_REWARD_ITEM (36 B, iItemCnt sbyte @16) + iItemCnt x sItemReward (20 B: sItemBase @0, eIL @12, iSlotNum @16)
                    if (b.Length >= 36) { int n = (sbyte)b[16]; for (int i = 0; i < n; i++) { int o = 36 + 20 * i; if (o + 20 > b.Length) break; if (Sh(b, o) == 8) SetQuestItem(I(b, o + 16), Sh(b, o + 2), I(b, o + 4)); } }
                }
                else if (type == PacketIds.P_FE2CL_REP_PC_CHANGE_LEVEL_SUCC)
                {   // iLevel @0, iFusionMatter @4. PlayerHud no lo procesa: se refleja en hud.Data (el original tambien actualiza iFusionMatter e iMaxFusionMatter aqui)
                    if (b.Length >= 8 && hud != null) { hud.Data.Level = I(b, 0); hud.Data.FusionMatter = I(b, 4); }
                }
            }
            catch (Exception e) { Debug.LogWarning("[nanocom] paquete " + type + ": " + e.Message); }
        }

        // sPCLoadData2CL dentro de REP_PC_ENTER_SUCC (iID @0, struct @4, Pack 4): aQInven @4+844 (50 x sItemBase 12 B), iConditionBitFlag @4+1676, aRunningQuest @4+2032 (9 x 52 B), iCurrentMissionID @4+2500.
        public const int EnterBodySize = 2564;
        void EnterRaw(byte[] b)
        {
            if (b == null || b.Length < EnterBodySize) { Debug.LogWarning("[nanocom] ENTER_SUCC con " + (b == null ? -1 : b.Length) + " B (esperados " + EnterBodySize + ")"); return; }
            for (int i = 0; i < 50; i++) { int o = 4 + 844 + 12 * i; qId[i] = Sh(b, o + 2); qOpt[i] = I(b, o + 4); }
            SetConditionFlags(I(b, 4 + 1676));
            active.Clear(); nodes.Clear(); curSel = null; selectedMission = 0; ViewWayPoint = false;
            for (int k = 0; k < 9; k++)
            {
                int o = 4 + 2032 + 52 * k; int taskId = I(b, o); if (taskId <= 0) continue;
                MissionElement me; if (!taskById.TryGetValue(taskId, out me)) { Debug.LogWarning("[nanocom] mision en curso con tarea desconocida " + taskId); continue; }
                if (me.m_iHMissionType == 2 && k != 0) continue;           // "nano mission must 0 slot"
                var node = ResetNode(taskId, me); InsertActive(node);
                for (int l = 0; l < 3; l++) for (int m = 0; m < 3; m++) if (At(me.m_iCSUEnemyID, l) == I(b, o + 4 + 4 * m)) node.RemainEnemy[l] = I(b, o + 16 + 4 * m);   // m_aKillNPCID[3] @4, m_aKillNPCCount[3] @16
            }
            int cur = I(b, 4 + 2500);
            if (cur != 0) { selectedMission = cur; curSel = FindSelected(); }
            else if (active.Count > 0) selectedMission = active[active.Count - 1].Me.m_iHMissionID;
            StartSelect(); textVersion++;
        }

        // ====================================================================== misiones (cnMissionManager) ======================================================================
        sealed class TaskNode { public MissionElement Me; public int[] RemainEnemy = new int[3]; public float Life = -1f, StartedAt; }
        readonly Dictionary<int, TaskNode> nodes = new Dictionary<int, TaskNode>();      // GetTask(taskId): un nodo persistente por tarea
        readonly List<TaskNode> active = new List<TaskNode>();                           // m_ActivateMissionList
        TaskNode curSel; int selectedMission;                                            // SelectMissionTask / m_iMenualSelectedMissionID
        readonly short[] qId = new short[50]; readonly int[] qOpt = new int[50];         // inventario de objetos de mision
        int textVersion, builtVersion = -1, builtSecs = -1; string title = "", body = "";
        bool taskEnd; float taskEndTimer;

        TaskNode ResetNode(int taskId, MissionElement me)   // cnMissionNode.InitializeBeforeStart -> Init
        {
            TaskNode n; if (!nodes.TryGetValue(taskId, out n)) { n = new TaskNode(); nodes[taskId] = n; }
            n.Me = me; for (int i = 0; i < 3; i++) n.RemainEnemy[i] = At(me.m_iCSUNumToKill, i); n.Life = -1f; n.StartedAt = 0f; return n;
        }
        void RemoveActiveMission(int missionId) { for (int i = 0; i < active.Count; i++) if (active[i].Me.m_iHMissionID == missionId) { active.RemoveAt(i); break; } }
        void InsertActive(TaskNode n)
        {
            for (int i = 0; i < active.Count; i++) if (active[i] == n) return;   // IsExistTaskInActiveMission
            RemoveActiveMission(n.Me.m_iHMissionID);
            if (n.Me.m_iHMissionType == 2) for (int i = 0; i < active.Count; i++) if (active[i].Me.m_iHMissionType == 2) { active.RemoveAt(i); break; }
            active.Add(n);
        }
        bool ActiveHasMission(int missionId) { for (int i = 0; i < active.Count; i++) if (active[i].Me.m_iHMissionID == missionId) return true; return false; }
        TaskNode FindSelected() { for (int i = 0; i < active.Count; i++) if (active[i].Me != null && selectedMission == active[i].Me.m_iHMissionID) return active[i]; return null; }
        bool IsFinalTask(MissionElement me) { List<MissionElement> l; return tasksOfMission.TryGetValue(me.m_iHMissionID, out l) && l.Count > 0 && l[l.Count - 1].m_iHTaskID == me.m_iHTaskID; }

        void StartSelect()   // StartSelectMissionTask
        {
            var prev = curSel; curSel = FindSelected();
            if (curSel != null)
            {
                int wp = curSel.Me.m_iSTGrantWayPoint;
                if (wp > 0) { Vector3? p = ClientNpcPosOf(wp); if (p.HasValue && p.Value != Vector3.zero) { ViewWayPoint = true; WayPoint = p.Value; } }
                else ViewWayPoint = false;
                if (prev != curSel && SendCurrentMission && NpcUI.Sender != null)
                {
                    var pk = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_SET_CURRENT_MISSION_ID, 4); BitConverter.GetBytes(curSel.Me.m_iHMissionID).CopyTo(pk, 4);
                    try { NpcUI.Sender(pk); } catch (Exception e) { Debug.LogWarning("[nanocom] SET_CURRENT_MISSION_ID: " + e.Message); }
                }
            }
            else ViewWayPoint = false;
            textVersion++;
        }
        void ResetSelect()
        {
            selectedMission = 0; curSel = null;
            if (active.Count > 0) { selectedMission = active[active.Count - 1].Me.m_iHMissionID; StartSelect(); } else ViewWayPoint = false;
            textVersion++;
        }
        Vector3? ClientNpcPosOf(int type)
        {
            if (ClientNpcPos != null) return ClientNpcPos(type);
            LoadData(); Vector3 v; return clientNpc.TryGetValue(type, out v) ? (Vector3?)v : null;
        }

        void TaskStart(int taskNum, int remain)   // ProcessStartSucc
        {
            MissionElement me; if (!taskById.TryGetValue(taskNum, out me)) return;
            var n = ResetNode(taskNum, me);
            if (ents != null && ents.Missions != null) ents.Missions.OnTaskStart(taskNum);
            if (me.m_iSTGrantTimer > 0) { n.Life = remain; n.StartedAt = Time.realtimeSinceStartup; }
            SetBubbleChat(0, me);
            InsertActive(n);
            bool nanoShown = false;
            if (me.m_iHMissionType == 2)
            {   // mision de nano: mensaje "New Nano Mission!" solo en la primera tarea de la mision
                List<MissionElement> tl; if (tasksOfMission.TryGetValue(me.m_iHMissionID, out tl) && tl.Count > 0 && tl[0].m_iHTaskID == taskNum)
                { SetMissionMessage(me.m_iSTMessageSendNPC, me.m_iSTMessageType, me.m_iHMissionName, me.m_iSTMessageTextID, 10, me.m_iSTNanoID); nanoShown = true; }
            }
            if (!nanoShown) SetMissionMessage(me.m_iSTMessageSendNPC, me.m_iSTMessageType, me.m_iHMissionName, me.m_iSTMessageTextID, 9, 0);
            if (!ActiveHasMission(selectedMission)) selectedMission = me.m_iHMissionID;
            StartSelect();
            if (me.m_iHTaskType == 6 && me.m_iCSUDEFNPCID > 0) EscortGroup(me.m_iCSUDEFNPCID, true);   // acompañante: REQ_NPC_GROUP_INVITE
        }
        void TaskEnd(int taskNum)   // ProcessEndSucc
        {
            MissionElement me; if (!taskById.TryGetValue(taskNum, out me)) return;
            RemoveActiveMission(me.m_iHMissionID);
            if (ents != null && ents.Missions != null) ents.Missions.OnTaskEnd(taskNum);
            if ((IsFinalTask(me) || me.m_iSUOutgoingTask == 0) && ents != null && ents.Missions != null) ents.Missions.MarkCompleted(me.m_iHMissionID);
            if (IsFinalTask(me) || me.m_iSUOutgoingTask == 0)
            {
                if (me.m_iRepeatflag != 0 && ents != null && ents.Missions != null) ents.Missions.MarkRepeat(me.m_iRepeatflag);   // AddRepeatFlag
                SetBubbleChat(2, me); Sfx.Ui("Mission_Completed");
                if (me.m_iHMissionID == selectedMission) ResetSelect();
            }
            else { Sfx.Ui("Task_Completed"); SetBubbleChat(1, me); ReceiveTaskEnd(); }
            SetMissionMessage(me.m_iSUMessageSendNPC, me.m_iSUMessageType, me.m_iHMissionName, me.m_iSUMessagetextID, 9, 0);
            if (me.m_iHTaskType == 6 && me.m_iCSUDEFNPCID > 0) EscortGroup(me.m_iCSUDEFNPCID, false);   // REQ_NPC_GROUP_KICK
            if (me.m_iSUOutgoingTask > 0) RequestTaskStart(me.m_iSUOutgoingTask, 0);   // ProcessEndSucc: pide la siguiente tarea de la mision
            textVersion++;
        }
        // ---- cnMissionManager.SetBubbleChat / SetMissionMessage / NPC escolta ----
        NpcInfo NearNpc(int npcNumber)
        {
            if (ents == null || npcNumber <= 0) return null;
            foreach (var go in ents.NpcObjects) { if (go == null) continue; var i = go.GetComponent<NpcInfo>(); if (i != null && i.NpcNumber == npcNumber) return i; }
            return null;
        }
        string MissionStr(int idx) { var sd = mt != null ? mt.m_pMissionStringData : null; return sd != null && idx >= 0 && idx < sd.Length && sd[idx] != null ? sd[idx].m_pstrNameString ?? "" : ""; }
        /// <summary>process: 0 Start, 1 Success (tarea intermedia), 2 Complete (mision), 3 Fail. Burbuja QuestChat sobre el NPC + voz del NPC que da/cierra la mision.</summary>
        void SetBubbleChat(int process, MissionElement me)
        {
            string text = ""; int bubbleNpc = 0, voiceNpc = 0; var sd = mt != null ? mt.m_pMissionStringData : null; int cnt = sd != null ? sd.Length : 0;
            switch (process)
            {
                case 0: if (me.m_iSTDialogBubble > 0 && cnt > me.m_iSTDialogBubble) { text = MissionStr(me.m_iSTDialogBubble); bubbleNpc = me.m_iSTDialogBubbleNPCID; } voiceNpc = me.m_iHNPCID; break;
                case 1: case 2: if (me.m_iSUDialogBubble > 0 && cnt > me.m_iSUDialogBubble) { text = MissionStr(me.m_iSUDialogBubble); bubbleNpc = me.m_iSUDialogBubbleNPCID; } voiceNpc = me.m_iHTerminatorNPCID; break;
                case 3: if (me.m_iFDialogBubble > 0 && cnt > me.m_iFDialogBubble) { text = MissionStr(me.m_iFDialogBubble); bubbleNpc = me.m_iFDialogBubbleNPCID; } break;
            }
            if (bubbleNpc > 0 && text.Length > 1)
            {
                var n = NearNpc(bubbleNpc); if (n == null) return;   // (el original sale aqui si el NPC no esta cerca: tampoco suena la voz)
                if (NpcUI.Instance != null) NpcUI.Instance.AddNpcBubble(n.Id, n.transform, n.Name, (n.TableHeight > 0.1f ? n.TableHeight : n.Height) * 0.8f, text);
            }
            if (voiceNpc <= 0) return;
            string voice = NpcVoice.CommentOf(npcTable, voiceNpc); var near = NearNpc(voiceNpc); var tr = near != null ? near.transform : null;
            if (process == 0) NpcVoice.Play(tr, voice, me.m_iHTaskType == 6 ? NpcVoice.V.QuestStartEscort : NpcVoice.V.QuestAcce);
            else if (process == 2) NpcVoice.Play(tr, voice, NpcVoice.V.QuestEnd);
        }
        /// <summary>SetMissionMessage: caja del nanocom (btn 9 = NPC, 10 = mision de nano) + linea en el chat. messageType bit 2 = caja, bit 4 = aviso (sin portar).</summary>
        void SetMissionMessage(int npc, int messageType, int nameIdx, int textIdx, int btn, int nano)
        {
            if (nameIdx <= 0 || textIdx <= 0 || (messageType & 2) == 0) return;
            string txt = MissionStr(textIdx); if (txt.Length <= 1) return;
            string title = "", icon = null, voice = null; var kind = NanoMessages.Kind.Npc;
            if (npc > 0 && btn == 9)
            {
                var row = NpcRow(npc);
                if (row != null)
                {
                    title = NpcName(npc); voice = NpcVoice.CommentOf(npcTable, npc);
                    var idt = npcTable != null ? npcTable.m_pNpcIconData : null; if (idt != null && row.m_iIcon1 >= 0 && row.m_iIcon1 < idt.Length) icon = IconName(idt[row.m_iIcon1]);
                }
            }
            else if (npc > 0 && btn == 10)
            {
                kind = NanoMessages.Kind.Nano; title = Loc.T("New Nano Mission!"); voice = NpcVoice.CommentOf(npcTable, npc);
                var nt = tables != null ? tables.m_pNanoTable : null; int ei = nano;
                if (nt != null && nt.m_pNanoData != null && ei >= 0 && ei < nt.m_pNanoData.Length && nt.m_pNanoData[ei] != null && nt.m_pNanoIconData != null)
                { int ii = nt.m_pNanoData[ei].m_iIcon1; if (ii >= 0 && ii < nt.m_pNanoIconData.Length) icon = IconName(nt.m_pNanoIconData[ii]); }
            }
            NanoMessages.Post(title, txt, kind, 10f, icon);   // fNanoMessageTime 20 * 0.5 (ButtonType < 11)
            if (btn == 10) NanoTimeEffect(); ChatHud.AddEventString(4, title + ": " + txt);
            if (!string.IsNullOrEmpty(voice)) NpcVoice.Play(null, voice, NpcVoice.V.NanoComMessage);
        }
        /// <summary>cnGUINanocom.PlayVO con ButtonType 10: cnOwnAvatarStatus.NanoTimeEffect = efecto 710 sobre el jugador (hijo) + sonido Nano_Creation_Complete.</summary>
        void NanoTimeEffect()
        {
            var s = ents != null ? ents.Self : null; if (s == null) return;
            if (BulletFx.Instance != null) BulletFx.Instance.Spawn(710, s.transform.position, s.transform.rotation, 1f, s.transform, 8f);
            Sfx.Ui("Nano_Creation_Complete");
        }
        /// <summary>m_iSTGrantWayPoint de la tarea activa de la mision seleccionada (cnMissionManager.RefreshQuestSymbol -> SetSmartIndicator), 0 = ninguna.</summary>
        public int SelectedWayPointNpc { get { return curSel != null && curSel.Me != null ? curSel.Me.m_iSTGrantWayPoint : 0; } }
        void EscortGroup(int npcNumber, bool invite)
        {
            int id = NpcInstanceOf(npcNumber); if (id == 0 || NpcUI.Sender == null) return;
            var p = ClientPackets.New(invite ? PacketIds.P_CL2FE_REQ_NPC_GROUP_INVITE : PacketIds.P_CL2FE_REQ_NPC_GROUP_KICK, 4); BitConverter.GetBytes(id).CopyTo(p, 4);
            try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[nanocom] NPC_GROUP: " + ex.Message); }
        }
        // ---- consultas para NpcUI (WarpOK) ----
        public int PlayerTaros { get { return hud != null && hud.Data != null ? hud.Data.Taros : 0; } }
        public int PlayerLevel { get { return hud != null && hud.Data != null ? hud.Data.Level : 0; } }
        public bool TaskIsActive(int taskId) { return ents != null && ents.Missions != null && ents.Missions.IsTaskActive(taskId); }
        public bool MissionIsActive(int missionId) { return ents != null && ents.Missions != null && ents.Missions.IsMissionActive(missionId); }
        public int QuestItemQty(int itemId) { return QuestItemCount(itemId); }
        public string MessageText(int i) { var mtb = tables != null ? tables.m_pMessageTable : null; var m = mtb != null ? mtb.m_pMessageData : null; return m != null && i >= 0 && i < m.Length && m[i] != null ? m[i].m_szString ?? "" : ""; }

        void TaskStop(int taskNum)   // DelActiveMissionSucc
        {
            MissionElement me; if (!taskById.TryGetValue(taskNum, out me)) return;
            if (ents != null && ents.Missions != null) ents.Missions.OnTaskStop(taskNum);
            for (int i = 0; i < active.Count; i++) if (active[i].Me.m_iHTaskID == taskNum)
            {
                active.RemoveAt(i);
                if (selectedMission == me.m_iHMissionID || active.Count <= 0) ResetSelect();
                break;
            }
            textVersion++;
        }
        void KillMissionNpc(int npcId)   // KillMissionNPC (solo la parte del contador)
        {
            for (int i = 0; i < active.Count; i++) for (int j = 0; j < 3; j++) if (At(active[i].Me.m_iCSUEnemyID, j) == npcId) active[i].RemainEnemy[j]--;
            textVersion++;
        }
        void SetQuestItem(int slot, short id, int opt) { if (slot < 0 || slot >= 50) return; qId[slot] = id; qOpt[slot] = opt; textVersion++; }
        int QuestItemCount(int itemId) { for (int i = 0; i < 50; i++) if (qId[i] == itemId && itemId != 0) return qOpt[i]; return 0; }   // SearchQuestItem

        float RemainTime(TaskNode n) { return n.Life - (Time.realtimeSinceStartup - n.StartedAt); }

        void RebuildMissionText()   // cnGUINanocom.Update: MissionTitle / MissionBody
        {
            builtVersion = textVersion; title = ""; body = "";
            var n = curSel; if (n == null || n.Me == null || active.Count == 0 || !CurrentObjective || mt == null) { builtSecs = -1; return; }
            var me = n.Me; var sd = mt.m_pMissionStringData; int ni = me.m_iHMissionName, oi = me.m_iHCurrentObjective;
            title = sd != null && ni >= 0 && ni < sd.Length && sd[ni] != null ? sd[ni].m_pstrNameString ?? "" : "";
            var sb = new StringBuilder(sd != null && oi >= 0 && oi < sd.Length && sd[oi] != null ? sd[oi].m_pstrNameString ?? "" : "");
            if (me.m_iSTGrantTimer > 0) { builtSecs = (int)Mathf.Max(0f, RemainTime(n)); sb.Append("\n").Append("Remaining time").Append(": ").Append(builtSecs); } else builtSecs = -1;
            bool flag = false;
            for (int i = 0; i < 3; i++)
            {
                int id = At(me.m_iCSUEnemyID, i), need = At(me.m_iCSUNumToKill, i);
                if (id > 0 && need > 0)
                {
                    int done = need - n.RemainEnemy[i]; if (done > need) done = need;
                    if (!flag) sb.Append("\n");
                    sb.Append(NpcName(id)).Append(" : ").Append(Mathf.Min(done, need)).Append("/").Append(need); flag = true; sb.Append("\n");
                }
            }
            flag = false;
            for (int j = 0; j < 3; j++)
            {
                int id = At(me.m_iCSUItemID, j), need = At(me.m_iCSUItemNumNeeded, j);
                if (id > 0 && need > 0)
                {
                    int remain = need - QuestItemCount(id); int done = need - remain; if (done > need) done = need;   // m_aiCurrentRemainItemNum = needed - cantidad en el inventario
                    if (!flag) sb.Append("\n");
                    sb.Append(QuestItemName(id)).Append(" : ").Append(Mathf.Min(done, need)).Append("/").Append(need); flag = true; sb.Append("\n");
                }
            }
            body = sb.ToString();
        }

        // ====================================================================== buffs (CnGuiSkillBuffIcon) ======================================================================
        static readonly uint[] BuffFlag = { 1u, 4u, 8u, 16u, 32u, 64u, 4096u, 8192u, 16384u, 32768u, 131072u, 524288u, 8388608u, 1048576u, 2097152u, 4194304u };
        static readonly int[] BuffId = { 1, 3, 4, 5, 6, 7, 13, 14, 15, 16, 18, 20, 24, 21, 22, 23 };         // eCharStatusTimeBuffID
        static readonly uint[] DebuffFlag = { 128u, 256u, 512u, 1024u, 65536u, 262144u };
        static readonly int[] DebuffId = { 8, 9, 10, 11, 17, 19 };
        int buffFlags; bool buffsDirty = true;
        readonly List<Texture> buffTex = new List<Texture>();

        static string IconName(IconElement e)   // AvatarUtil.GetIconTexture
        {
            if (e == null) return null; string n = e.m_iIconNumber.ToString("00");
            switch (e.m_iIconType)
            {
                case 0: return "wpnicon_" + n; case 1: return "nanoicon_" + n; case 2: return "skillicon_" + n; case 3: return "cosicon_" + n; case 4: return "npcicon_" + n;
                case 5: return "nanoready_" + n; case 6: return "questitemicon_" + n; case 7: return "generalitemicon_" + n; case 8: return "mobicon_" + n; case 9: return "fusionicon_" + n;
                case 10: return "hnpcicon_" + n; case 11: return "transport_" + n; case 12: return "vehicle_" + n;
            }
            return null;
        }
        Texture BuffIcon(int id, bool cash)   // TableContainer.GetTableData(12,1,id).m_iBuffIcon/m_iBuffCashIcon -> (12,2) IconElement -> textura
        {
            var bd = skillTable != null ? skillTable.m_pSkillBuffData : null; var ic = skillTable != null ? skillTable.m_pSkillIconData : null;
            if (bd == null || ic == null || id < 0 || id >= bd.Length || bd[id] == null) return null;
            int k = cash ? bd[id].m_iBuffCashIcon : bd[id].m_iBuffIcon; if (k < 0 || k >= ic.Length) return null;
            return Icon(IconName(ic[k]));
        }
        void RebuildBuffs()   // UpdateIconList: primero los debuffs, luego los buffs; los 3 bits de nanos (1048576/2097152/4194304) no dibujan icono propio
        {
            buffsDirty = false; buffTex.Clear(); uint f = (uint)buffFlags;
            for (int i = 0; i < DebuffFlag.Length; i++) if ((f & DebuffFlag[i]) != 0) buffTex.Add(BuffIcon(DebuffId[i], false));
            for (int j = 0; j < BuffFlag.Length; j++)
                if ((f & BuffFlag[j]) != 0) { if (BuffFlag[j] == 1048576u || BuffFlag[j] == 2097152u || BuffFlag[j] == 4194304u) continue; buffTex.Add(BuffIcon(BuffId[j], false)); }
            ViewMob = (f & 4096u) != 0; ViewShiny = (f & 8192u) != 0;   // eCSTB_MiniMapEnemy / eCSTB_MiniMapTresure -> cnGUINanocom.bViewMob / bViewShiny
        }

        readonly ulong[] cashLimit = new ulong[27]; int cashFlags; readonly List<Texture> cashTex = new List<Texture>(); readonly List<ulong> cashTime = new List<ulong>(); float cashStart;
        void CashUpdate(int eCstb, int eTbu, ulong duration, int flags)
        {
            if (eCstb >= 0 && eCstb < cashLimit.Length) { if (eTbu == 1 || eTbu == 3) cashLimit[eCstb] = duration; else if (eTbu == 2) cashLimit[eCstb] = 0UL; }
            cashFlags = flags; cashTex.Clear(); cashTime.Clear(); uint f = (uint)flags;
            for (int i = 0; i < BuffFlag.Length; i++)
                if ((f & BuffFlag[i]) != 0) { int id = BuffId[i]; if (id != 0) { cashTex.Add(BuffIcon(id, true)); cashTime.Add(id < cashLimit.Length ? cashLimit[id] : 0UL); cashStart = Time.time; } }
        }

        // ====================================================================== Update ======================================================================
        readonly List<NpcInfo> npcList = new List<NpcInfo>(); float checkAt = -10f; string dongName = "map name"; int curDong = -1;
        float fDegree;

        Transform SelfT() { return ents != null && ents.Self != null ? ents.Self.transform : null; }

        void Update()
        {
            Action a; int guard = 0; while (guard++ < 64 && queue.TryDequeue(out a)) { try { a(); } catch (Exception e) { Debug.LogWarning("[nanocom] " + e.Message); } }
            if (taskEnd && taskEndTimer > 0f) { taskEndTimer -= Time.deltaTime; if (taskEndTimer < 0f) taskEnd = false; }
            if (Time.time - cashStart >= 1f) { for (int i = 0; i < cashTime.Count; i++) cashTime[i] = cashTime[i] >= 1000UL ? cashTime[i] - 1000UL : 0UL; cashStart = Time.time; }
            if (buffsDirty) RebuildBuffs();
            TickPing(); if (ClockEnabled && Input.GetKeyDown(KeyCode.F8) && !ChatHud.Typing) { clockPing = !clockPing; Sfx.Ui("tab_click01"); }
            // medidor de FM: fDegree = 360 * min(1, FM / max)  (solo cuando cambia el valor en el original; aqui es barato y se recalcula)
            if (hud != null) { int fm = Mathf.Max(0, hud.Data.FusionMatter), max = MaxFusionMatter(); fDegree = max > 0 ? 360f * Mathf.Min(1f, (float)fm / max) : (fm > 0 ? 360f : 0f); }
            if (curSel != null && curSel.Me != null && curSel.Me.m_iSTGrantTimer > 0 && active.Count > 0 && (int)Mathf.Max(0f, RemainTime(curSel)) != builtSecs) textVersion++;
            if (builtVersion != textVersion) RebuildMissionText();
            var self = SelfT();
            if (self != null && Time.time - checkAt > CheckInterval)
            {
                checkAt = Time.time; RefreshNpcs(); UpdateDong(self.position); UpdateFutureZone(self.position);
            }
        }

        /// <summary>cnOwnAvatarStatus.iMaxFusionMatter = AvatarGrowElement[min(PC_LEVEL_MAX = 60, nivel)].m_iReqBlob_NanoCreate (NO m_iFMLimit). hud.Data.MaxFusionMatter > 0 manda.</summary>
        public int MaxFusionMatter()
        {
            if (hud != null && hud.Data.MaxFusionMatter > 0) return hud.Data.MaxFusionMatter;
            var gd = avatarTable != null ? avatarTable.m_pAvatarGrowData : null; if (gd == null || gd.Length == 0 || hud == null) return 0;
            int lvl = Mathf.Clamp(Mathf.Min(60, hud.Data.Level), 0, gd.Length - 1); return gd[lvl] != null ? gd[lvl].m_iReqBlob_NanoCreate : 0;
        }

        void RefreshNpcs()
        {
            npcList.Clear(); if (ents == null) return;
            foreach (var go in ents.NpcObjects) { if (go == null) continue; var ni = go.GetComponent<NpcInfo>(); if (ni != null) npcList.Add(ni); }
        }
        void UpdateDong(Vector3 p)   // WorldDataContainer.Update: pCurDong se conserva mientras el jugador siga dentro de su Area
        {
            if (DongNameProvider != null) { string s = DongNameProvider(p); if (!string.IsNullOrEmpty(s)) dongName = s; return; }
            LoadData(); var pt = new Vector2(p.x, p.z);
            if (curDong >= 0 && curDong < dongs.Count && Contains(dongs[curDong].Area, pt)) return;
            for (int i = 0; i < dongs.Count; i++) if (Contains(dongs[i].Area, pt)) { curDong = i; dongName = dongs[i].Dong; return; }
            if (curDong < 0) dongName = "unknown";
        }
        static bool Contains(Rect r, Vector2 p) { return p.x >= r.xMin && p.x < r.xMax && p.y >= r.yMin && p.y < r.yMax; }   // Rect.Contains(Vector2)

        void OnDestroy() { Detach(); if (mm != null) Destroy(mm); }

        // ====================================================================== dibujo ======================================================================
        // Espacio de diseno -> pantalla. Nanocom: pivote TopRight (W,0). Buffs propios: pivote TopLeft (0,0).
        float W, Sc;
        Rect SR(Rect d) { return new Rect(W + (d.x - W) * Sc, d.y * Sc, d.width * Sc, d.height * Sc); }
        Rect SL(Rect d) { return new Rect(d.x * Sc, d.y * Sc, d.width * Sc, d.height * Sc); }
        Vector2 SRp(float x, float y) { return new Vector2(W + (x - W) * Sc, y * Sc); }
        // Origen del grupo NanoRect del original (x = Screen.width - WindowRect.width, y = WindowRect.y = -4) y rects locales (tras InitCycleFMBar)
        const float GroupW = 187f, GroupH = 183f + 294f, GroupY = -4f;
        Rect G(Rect local) { return new Rect(W - GroupW + local.x, GroupY + local.y, local.width, local.height); }
        static readonly Rect WindowRect = new Rect(7f, -4f, 187f, 183f), FmRect = new Rect(12f, 3f, 176f, 176f), MiniLocal = new Rect(26f, 13f, 148f, 148f),
            DongLocal = new Rect(42f, 121f, 120f, 60f), BurstLocal = new Rect(7f, 186f, 180f, 111f), MissionLocal = new Rect(29f, 186f, 148f, 264f);
        static readonly Vector2 FmPivot = new Vector2(99f, 91f);

        /// <summary>Rects en pantalla (GameMenu: clic en el minimapa abre el mapa; clic en el texto de mision abre el diario). Se actualizan en cada Repaint.</summary>
        public static Rect MinimapScreen, MissionScreen;

        void OnGUI()
        {
            {   // W24: boton "time machine" (cnGUINanocom, Future Zone): el clic se atiende aqui (el resto del nanocom solo dibuja en Repaint)
                var ev0 = Event.current;
                if (ev0 != null && ev0.type == EventType.Repaint) timeShown = false;
                else if (ev0 != null && ev0.type == EventType.MouseDown && ev0.button == 0 && timeShown && timeEnabled && Visible && !GameMenu.HudHidden && timeBox.Contains(ev0.mousePosition)) { ev0.Use(); PressTimeMachine(); return; }
            }
            if (GameMenu.HudHidden) return;
            if (!Visible || Event.current == null || Event.current.type != EventType.Repaint) return;
            var self = SelfT(); if (self == null || Screen.height <= 0) return;
            EnsureFonts(); W = Screen.width; Sc = GameOptions.UiScale;
            var oldColor = GUI.color; var oldMatrix = GUI.matrix; int oldDepth = GUI.depth; GUI.depth = 8; GUI.color = Color.white; GUI.matrix = Matrix4x4.identity;
            try
            {
                bool shaderOk = ShowMinimap && MapMaterial() != null && T("nanocom_minimap_a") != null;
                Rect mapD = new Rect(W - GroupW + MiniLocal.x, MiniLocal.y, MiniLocal.width, MiniLocal.height);   // RenderMinimap: x += Screen.width - WindowRect.width, y sin desplazar
                if (ShowMinimap && !shaderOk) DrawMinimapPieces(self, mapD, false);                               // sin mascara: va DEBAJO del marco para que las esquinas queden tapadas
                if (ShowFusionMeter) DrawFusionMeter();
                if (ShowMinimap && shaderOk) DrawMinimapPieces(self, mapD, true);
                if (ShowMinimap) DrawMinimapOverlay(self, mapD);
                MinimapScreen = ShowMinimap ? SR(mapD) : Rect.zero;
                MissionScreen = Rect.zero;
                if (ShowMission && !GameMenu.Open && GameMenu.Slide <= 0f) DrawMissionPanel();   // el original oculta el texto de mision mientras el menu esta abierto/deslizandose
                if (ShowMinimap) DrawDongName();
                if (ShowBuffs) DrawBuffs();
                DrawClock();
                DrawTimeMachine();
            }
            finally { GUI.color = oldColor; GUI.matrix = oldMatrix; GUI.depth = oldDepth; }
        }

        // ---- boton "time machine" (cnGUINanocom.OnGUI, rama iPaymentFlag != 0; timeSkin: timeBut / timeButBack / timeicon) ----
        // Grupo timeRect: x = Screen.width - 410 + (-52), y = 0, 410x95; boton 270x95 (timeMBut; hover timeMButUp solo con el raton dentro de timeMachineInBoxRect 43,15,187x38); icono TimeMachine_Icon en (52,20,38,33).
        // Visible: cuenta de pago, zona "The Future" y (LevelExpansion 0: nivel 4 | 1: nivel >= 10). Desactivado en instancia (bInsMap) o si MoveTYPE != 0 (escoba, tirolina, cuerda, lanzador, habilidad de movimiento). Sin tooltip en el original.
        // Cuenta gratuita (iPaymentFlag == 0): el original muestra premBut (REQ_PC_EXIT + pagina de pago); no portado.
        /// <summary>Hook opcional: devuelve true si el avatar no esta a pie por algo que este archivo no ve (MoveTYPE != None, p. ej. habilidad de movimiento).</summary>
        public static Func<bool> NotOnFoot;
        bool futureZone, timeShown, timeEnabled; Rect timeBox;
        void UpdateFutureZone(Vector3 p)
        {   // WorldDataContainer.GetZoneName() == GetLocalZoneName("The Future")
            string d, z; futureZone = DongAt(new Vector2(p.x, p.z), out d, out z) && string.Equals(z, "The Future", StringComparison.OrdinalIgnoreCase);
        }
        bool OnFoot() { return !(BroomFlight.Active || ZiplineRide.Riding || (NotOnFoot != null && NotOnFoot())); }
        void DrawTimeMachine()
        {
            timeShown = false;
            int lv = hud != null && hud.Data != null ? hud.Data.Level : 0;
            if (!WarpFlow.TimeWarpVisible(GuideChangerWindow.PaymentFlag != 0, futureZone, lv, WarpFlow.LevelExpansion)) return;
            float gx = W - 410f - 52f;
            Rect btn = SR(new Rect(gx, 0f, 270f, 95f)), box = SR(new Rect(gx + 43f, 15f, 187f, 38f));
            bool en = !RaceSystem.InsMap && OnFoot() && !SysMessageBox.Active;
            bool hover = en && box.Contains(Event.current.mousePosition);
            DrawTex(btn, T(hover ? "timeMButUp" : "timeMBut"));
            DrawTex(SR(new Rect(gx + 52f, 20f, 38f, 33f)), T("TimeMachine_Icon"));
            timeShown = true; timeEnabled = en; timeBox = box;
        }
        void PressTimeMachine()
        {   // cnGUINanocom.TimeWarp: un solo envio y oculta el menu del nanocom (ShowNanoMenu(false))
            WarpFlow.TimeWarp(); GameMenu.SetOpen(false, false);
        }

        // ---- reloj / ping (cnGUINanocom.RenderClock + NetworkStats) ----
        /// <summary>cnOption.displayOption.bNanocomClock (activo por defecto). F8 alterna hora <-> ping (tecla NanocomClock del original).</summary>
        public bool ClockEnabled = true; bool clockPing; int pingValue; float pingSent = -10f; uint pingMs;
        void TickPing()
        {   // NetworkStats.SendPing: cada 3 s, GM_REQ_PC_SET_VALUE {iPC_ID, iSetValueType = 99, iSetValue = ticks}; la respuesta con el mismo valor da el ping real
            if (!ClockEnabled || ents == null || ents.SelfId == 0 || NpcUI.Sender == null || Time.time - pingSent < 3f) return;
            pingValue = (int)(DateTime.Now.Ticks & 0x7FFFFFFF); if (pingValue == 0) pingValue = 1; pingSent = Time.time;
            var pk = ClientPackets.New(PacketIds.P_CL2FE_GM_REQ_PC_SET_VALUE, 12); ClientPackets.PutInt(pk, 4, ents.SelfId); ClientPackets.PutInt(pk, 8, 99); ClientPackets.PutInt(pk, 12, pingValue);
            try { NpcUI.Sender(pk); } catch (Exception e) { Debug.LogWarning("[reloj] " + e.Message); }
        }
        void DrawClock()
        {
            if (!ClockEnabled) return;
            var bg = T("pingbar_bg"); var empty = T("pingbar_empty"); if (bg == null || empty == null) return;
            string text; if (clockPing) text = pingMs <= 99 ? pingMs + " ms" : pingMs + "ms"; else { var now = DateTime.Now; text = now.Hour.ToString("00") + ":" + now.Minute.ToString("00"); }
            Texture2D lvl = pingMs < 125 ? T("pingbar_3") : pingMs < 250 ? T("pingbar_2") : pingMs < 500 ? T("pingbar_1") : null;   // GetPingLevel
            float gx = W - 208f, gy = 3f; float bw = 119f, bh = 24f;
            DrawTex(SR(new Rect(gx, gy, bw, bh)), bg);
            var pr = new Rect(gx + 7f, gy + 7f, 13f, 11f); DrawTex(SR(pr), empty); if (lvl != null) DrawTex(SR(pr), lvl);
            Text(chalet, new Rect(gx + 7f + 13f + 3f, gy + 2f, 90f, 20f), text, TextAnchor.UpperLeft, new Color32(0xE5, 0xE5, 0xE5, 255), false);
        }

        // ---- utilidades de dibujo ----
        void DrawTex(Rect screen, Texture t) { if (t != null) GUI.DrawTexture(screen, t, ScaleMode.StretchToFill, true); }
        void DrawTexD(Rect design, string name) { DrawTex(SR(design), T(name)); }
        /// <summary>Dibuja 't' en 'dst' (diseno) recortado a 'clip' (diseno): equivalente al recorte de GUI.BeginGroup(NanoRect).</summary>
        void DrawClipped(Texture t, Rect dst, Rect clip)
        {
            if (t == null) return;
            float x0 = Mathf.Max(dst.xMin, clip.xMin), x1 = Mathf.Min(dst.xMax, clip.xMax), y0 = Mathf.Max(dst.yMin, clip.yMin), y1 = Mathf.Min(dst.yMax, clip.yMax);
            if (x1 <= x0 || y1 <= y0) return;
            float u0 = (x0 - dst.x) / dst.width, u1 = (x1 - dst.x) / dst.width, vTop = 1f - (y0 - dst.y) / dst.height, vBot = 1f - (y1 - dst.y) / dst.height;
            GUI.DrawTextureWithTexCoords(SR(Rect.MinMaxRect(x0, y0, x1, y1)), t, new Rect(u0, vBot, u1 - u0, vTop - vBot), true);
        }
        void DrawRotated(Rect screen, Texture t, float angle, Vector2 screenPivot)
        {
            if (t == null) return;
            var m = GUI.matrix; GUIUtility.RotateAroundPivot(angle, screenPivot); GUI.DrawTexture(screen, t, ScaleMode.StretchToFill, true); GUI.matrix = m;
        }
        void Text(BitmapFont f, Rect design, string text, TextAnchor anchor, Color c, bool left, RectOffset pad = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (f != null) { f.Draw(design, text, anchor, true, c, pad, left ? Vector2.zero : new Vector2(W, 0f), Sc); return; }
            var r = left ? SL(design) : SR(design);
            var st = new GUIStyle(GUI.skin.label) { alignment = anchor, fontSize = Mathf.Max(8, Mathf.RoundToInt(12 * Sc)), wordWrap = true, clipping = TextClipping.Overflow }; st.normal.textColor = c;
            GUI.Label(r, text, st);
        }

        // ---- medidor de Fusion Matter + marco (RenderMenu, rama circular) ----
        void DrawFusionMeter()
        {
            Texture fmReg = T("fusionmeter_regular"), fmRegR = T("fusionmeter_regular_right"), bonus = T("fusionmeter_bonus"), bonusR = T("fusionmeter_bonus_right");
            Texture rest = T("fusionmeter_fatigued"), restR = T("fusionmeter_fatigued_right"), backR = T("fusionmeter_back_right"), backL = T("fusionmeter_back_left");
            Rect clip = new Rect(W - GroupW, GroupY, GroupW, GroupH); Rect pos = G(FmRect); Vector2 piv = SRp(W - GroupW + FmPivot.x, GroupY + FmPivot.y);
            // alfa del anillo parpadeante (FMFatigureColor, cnGUINanocom.Update)
            float t = Time.time, al;
            if (FatigueLevel <= 1) al = Mathf.Abs(Mathf.Cos(t * Mathf.PI * (FatigueValue < 50 ? 1f : 0.5f)));
            else al = FatigueValue <= 0 ? 0f : (FatigueValue < 50 ? Mathf.Abs(Mathf.Cos(t * Mathf.PI)) : 1f);
            var fat = new Color(1f, 1f, 1f, al);
            bool normal = FatigueLevel <= 1;
            Texture baseT = normal ? fmReg : rest, baseR = normal ? fmRegR : restR, overT = normal ? bonus : fmReg, overR = normal ? bonusR : fmRegR;
            DrawClipped(baseT, pos, clip);
            GUI.color = fat; DrawClipped(overT, pos, clip); GUI.color = Color.white;
            DrawRotated(SR(pos), backR, fDegree, piv);                         // mitad derecha negra girada fDegree (cubre lo vacio)
            if (fDegree > 180f) { DrawClipped(baseR, pos, clip); GUI.color = fat; DrawClipped(overR, pos, clip); GUI.color = Color.white; }
            else DrawClipped(backL, pos, clip);                                // mitad izquierda negra
            DrawClipped(T("fusionmatter_us"), G(WindowRect), clip);            // marco (rtBackImage); recortado por el grupo (7 px a la derecha, 4 arriba)
            if (taskEnd && taskEndTimer > 0f)                                  // burst (TaskBustTexture) bajo el marco
            {
                GUI.color = new Color(1f, 1f, 1f, taskEndTimer < 1f ? Mathf.Clamp01(taskEndTimer) : 1f); DrawClipped(T("burst"), G(BurstLocal), clip); GUI.color = Color.white;
            }
        }

        // ---- minimapa ----
        Material mm;
        Material MapMaterial()
        {
            if (MinimapMaterial != null) return MinimapMaterial;
            if (mm == null)
            {
                var sh = MinimapShader != null ? MinimapShader : Shader.Find("OpenFusion/MiniMap");
                if (sh == null || !sh.isSupported) return null;
                mm = new Material(sh); mm.hideFlags = HideFlags.HideAndDontSave;
            }
            return mm;
        }
        Texture TileTex(int col, int row)   // MapTexture[(3 - fila) * 4 + columna]; fuera de la rejilla 4x4 (o sin textura): blackBack
        {
            Texture t = null;
            if (col >= 0 && col < 4 && row >= 0 && row < 4) t = T("all_minimap_" + ((3 - row) * 4 + col + 1).ToString("00"));
            if (t == null) t = T("back"); if (t == null) t = Texture2D.blackTexture; return t;
        }

        // Datos del frame (RenderMinimap): UserPosition = pos / 8192; num2 = fMiniMapRatio / 256; tempRect = recuadro visible en [0,1]^2 del mundo (x, z).
        float unit, tx, ty;
        void MapFrame(Transform self)
        {
            unit = 0.00390625f * Mathf.Clamp(MiniMapRatio, 4f, 16f); Vector3 up = self.position / 8192f; tx = up.x - unit * 0.5f; ty = up.z - unit * 0.5f;
        }

        /// <summary>RenderMaps: parte el recuadro visible entre las teselas 512x512 que toca (hasta 4) y dibuja cada trozo con el material (mascara nanocom_minimap_a en _DecalTex).</summary>
        void DrawMinimapPieces(Transform self, Rect mapD, bool masked)
        {
            MapFrame(self); Rect mapS = SR(mapD);
            float tnx = tx * 4f, tny = ty * 4f, tnw = unit * 4f;
            int left = Mathf.FloorToInt(tnx), right = Mathf.FloorToInt(tnx + tnw), top = Mathf.FloorToInt(tny), bottom = Mathf.FloorToInt(tny + tnw);
            Material mat = masked ? MapMaterial() : null; Texture mask = masked ? T("nanocom_minimap_a") : null;
            for (int ri = top; ri <= bottom; ri++)
                for (int ci = left; ci <= right; ci++)
                {
                    float u0 = Mathf.Max(0f, tnx - ci), u1 = Mathf.Min(1f, tnx + tnw - ci), v0 = Mathf.Max(0f, tny - ri), v1 = Mathf.Min(1f, tny + tnw - ri);
                    if (u1 <= u0 || v1 <= v0) continue;
                    float fx0 = (ci + u0 - tnx) / tnw, fx1 = (ci + u1 - tnx) / tnw, fyTop = 1f - (ri + v1 - tny) / tnw, fyBot = 1f - (ri + v0 - tny) / tnw;   // fracciones del recuadro (y desde arriba)
                    var dst = new Rect(mapS.x + fx0 * mapS.width, mapS.y + fyTop * mapS.height, (fx1 - fx0) * mapS.width, (fyBot - fyTop) * mapS.height);
                    Texture tile = TileTex(ci, ri);
                    if (mat != null && mask != null)
                    {
                        mat.SetTexture("_MainTex", tile); mat.SetTexture("_DecalTex", mask);
                        mat.SetVector("_MainRect", new Vector4(u0, v0, u1 - u0, v1 - v0)); mat.SetVector("_DecalRect", new Vector4(fx0, 1f - fyBot, fx1 - fx0, fyBot - fyTop));   // equivale a SetTextureScale/Offset del original (que Graphics.DrawTexture ignora en URP)
                        Graphics.DrawTexture(dst, tile, new Rect(u0, v0, u1 - u0, v1 - v0), 0, 0, 0, 0, Color.white, mat);
                    }
                    else GUI.DrawTextureWithTexCoords(dst, tile, new Rect(u0, v0, u1 - u0, v1 - v0), false);
                }
        }

        static string MapIconName(int i)   // cnGUINanocom.mapIcons (+ map_icon_woosh al final): 1 = camera, 28/29 = plus_but_1/minus_but_1
        {
            if (i == 1) return "camera"; if (i == 28) return "plus_but_1"; if (i == 29) return "minus_but_1"; if (i == 34) return "map_icon_woosh";
            if (i < 0 || i > 33) return null; return "map_icon_" + i.ToString("00");
        }
        Texture MapIcon(int i) { string n = MapIconName(i); return n != null ? T(n) : null; }

        /// <summary>Rect de diseno de un icono en el mapa (retPos.x/y del original: (pos / 8192 - tempRect) / num2, Y invertida).</summary>
        Rect IconRectD(Rect mapD, float nx, float nz, float w, float h)
        {
            return new Rect(mapD.x + (nx - tx) / unit * mapD.width - w * 0.5f, mapD.y + mapD.height - (nz - ty) / unit * mapD.height - h * 0.5f, w, h);
        }
        void DrawMapIconAt(Rect mapD, Vector3 world, int icon)
        {
            Texture t = MapIcon(icon); if (t == null) return; float nx = world.x / 8192f, nz = world.z / 8192f;
            Vector2 d = new Vector2(nx - (tx + unit * 0.5f), nz - (ty + unit * 0.5f)); if (d.magnitude >= unit * 0.5f) return;
            DrawTex(SR(IconRectD(mapD, nx, nz, t.width, t.height)), t);
        }

        void DrawMinimapOverlay(Transform self, Rect mapD)
        {
            MapFrame(self); Vector2 center = new Vector2(tx + unit * 0.5f, ty + unit * 0.5f);
            // NPC: los mobs (m_iNpcType == 0) solo con ViewMob o m_iSound == 2; simbolo de mision ('?' icono 16, '!' icono 15) o m_iMapIcon > 0
            var missions = ents != null ? ents.Missions : null;
            for (int i = 0; i < npcList.Count; i++)
            {
                var ni = npcList[i]; if (ni == null) continue;
                var row = NpcRow(ni.NpcNumber); int snd = row != null ? row.m_iSound : 0;
                if (ni.Type == 0 && snd != 2 && !ViewMob) continue;
                Vector3 p = ni.transform.position; Vector2 d = new Vector2(p.x / 8192f, p.z / 8192f) - center; if (d.magnitude >= unit * 0.5f) continue;
                int sym = missions != null ? missions.SymbolFor(ni.NpcNumber, ni.Type) : 0;
                if (sym == 2) DrawMapIconAt(mapD, p, 16); else if (sym == 1) DrawMapIconAt(mapD, p, 15); else if (ni.MapIcon > 0) DrawMapIconAt(mapD, p, ni.MapIcon);
            }
            if (ViewShiny && ShinyPositions != null) foreach (var p in ShinyPositions()) DrawMapIconAt(mapD, p, 26);
            if (GroupMemberPositions != null) foreach (var p in GroupMemberPositions()) DrawMapIconAt(mapD, p, 17);
            if (ViewWayPoint && WayPointOption) DrawWaypoint(mapD, self, WayPoint);
            // camara (icono 1 = cono de 148x148 girado con el yaw de la camara) y jugador (icono 0 girado con el yaw del avatar), ambos en el centro
            Rect mapS = SR(mapD); Vector2 c = mapS.center;
            Texture cone = MapIcon(1); Texture arrow = MapIcon(0);
            Camera cam = Camera.main; float camYaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            if (cone != null) DrawRotated(new Rect(c.x - cone.width * Sc * 0.5f, c.y - cone.height * Sc * 0.5f, cone.width * Sc, cone.height * Sc), cone, camYaw, c);
            if (arrow != null) DrawRotated(new Rect(c.x - arrow.width * Sc * 0.5f, c.y - arrow.height * Sc * 0.5f, arrow.width * Sc, arrow.height * Sc), arrow, self.eulerAngles.y + PlayerYawOffset, c);
            DrawTex(mapS, T("line_minimap"));   // LineEffect (lineas de barrido) sobre todo el mapa
        }

        /// <summary>DrawWaypoint: icono base (2) si esta a nivel, flecha (3) girada hacia el punto si cae fuera del circulo, (28/29) si esta >5 m por encima/debajo del jugador.</summary>
        void DrawWaypoint(Rect mapD, Transform self, Vector3 worldPos)
        {
            Texture baseT = MapIcon(2), arrowT = MapIcon(3), upT = MapIcon(28), downT = MapIcon(29); if (baseT == null) return;
            Vector2 center = new Vector2(tx + unit * 0.5f, ty + unit * 0.5f); Vector2 ret = new Vector2(worldPos.x / 8192f, worldPos.z / 8192f); bool clamped = false;
            Vector2 v = ret - center; if (v.magnitude >= unit * 0.5f) { ret = v / v.magnitude * unit * 0.5f + center; clamped = true; }
            float w = arrowT != null ? arrowT.width : 17f, h = arrowT != null ? arrowT.height : 17f;
            Rect r = IconRectD(mapD, ret.x, ret.y, w, h);
            if (clamped)
            {
                Rect mapS = SR(mapD); Rect rs = SR(r);
                float ax = (rs.center.x - mapS.center.x) / mapS.width, ay = (rs.center.y - mapS.center.y) / mapS.height; float ang = Mathf.Atan2(ay, ax) * 180f / 3.14f + 90f;
                DrawRotated(rs, arrowT, ang, new Vector2(rs.x + 8f * Sc, rs.y + 8f * Sc));
            }
            else if (worldPos.y > self.position.y + 5f) DrawTex(SR(r), upT);
            else if (worldPos.y < self.position.y - 5f) DrawTex(SR(r), downT);
            else DrawTex(SR(r), baseT);
        }

        // ---- nombre de la zona ----
        static RectOffset dongPad; static RectOffset DongPad { get { if (dongPad == null) dongPad = new RectOffset(10, 6, 4, 6); return dongPad; } }   // estilo "centerbox2": UpperCenter, relleno 10/6/4/6, fuente Chalet
        void DrawDongName()
        {
            Rect r = G(DongLocal); Rect rs = new Rect(r.x + 1f, r.y + 1f, r.width, r.height);
            Text(chalet, rs, dongName, TextAnchor.UpperCenter, Color.black, false, DongPad);
            Text(chalet, r, dongName, TextAnchor.UpperCenter, Color.yellow, false, DongPad);
        }

        // ---- panel de mision (GUILayout: titulo "centerbox" JEFFE 14 UpperLeft, 10 px, cuerpo "imagewindow" Chalet UpperLeft). Sombra negra +1,+1; titulo amarillo, cuerpo blanco ----
        void DrawMissionPanel()
        {
            if (curSel == null || active.Count == 0 || !CurrentObjective || (title.Length == 0 && body.Length == 0)) return;
            Rect a = G(MissionLocal); BitmapFont ft = jeffe14, fb = chalet;
            float th = ft != null ? Mathf.Max(1, ft.Lines(title, a.width).Count) * ft.LineHeight : 16f, bh = fb != null ? fb.Height(body, a.width) : 100f;
            Rect t = new Rect(a.x, a.y, a.width, th), bd = new Rect(a.x, a.y + th + 10f, a.width, bh);
            MissionScreen = SR(new Rect(a.x, a.y, a.width, th + 10f + bh));
            Text(ft, new Rect(t.x + 1f, t.y + 1f, t.width, t.height), title, TextAnchor.UpperLeft, Color.black, false);
            Text(fb, new Rect(bd.x + 1f, bd.y + 1f, bd.width, bd.height), body, TextAnchor.UpperLeft, Color.black, false);
            Text(ft, t, title, TextAnchor.UpperLeft, Color.yellow, false);
            Text(fb, bd, body, TextAnchor.UpperLeft, Color.white, false);
        }

        // ---- buffs / debuffs ----
        static readonly Color LabelCol = new Color(0.9f, 0.9f, 0.9f, 1f);
        void DrawBuffs()
        {
            Texture back = T("buff_icon_back");
            // propios: TopLeft, rectCenter = (117,72), 26x26, xMin = 117 - (n / 2) * 26
            int n = buffTex.Count; float x0 = 117f - (n / 2) * 26f;
            for (int i = 0; i < n; i++)
            {
                Rect r = SL(new Rect(x0 + i * 26f, 72f, 26f, 26f)); DrawTex(r, back); DrawTex(r, buffTex[i]);
            }
            // cash: TopRight, rectCashFirst = (630,40), cada uno con su tiempo restante ("12s", "5m", "3h")
            for (int j = 0; j < cashTex.Count; j++)
            {
                Rect d = new Rect(630f + j * 26f, 40f, 26f, 26f); Rect r = SR(d); DrawTex(r, back); DrawTex(r, cashTex[j]);
                ulong num = cashTime[j] / 1000UL; string unitS = "s";
                if (num >= 60UL) { num /= 60UL; unitS = "m"; }
                if (unitS == "m" && num >= 60UL) { num /= 60UL; unitS = "h"; }
                if (unitS == "h" && num >= 24UL) { num /= 60UL; unitS = "d"; }   // (sic) el original divide por 60 en vez de 24
                Text(chalet, new Rect(d.x + 2f, d.y + 19f, d.width, d.height), num.ToString() + unitS, TextAnchor.MiddleLeft, LabelCol, false);
            }
        }

        // ---- API para WorldMapWindow (mapa del mundo) ----
        /// <summary>MiniMapNpc.m_pElements completo (tipo de NPC -> posicion), de Data/clientnpc.json.</summary>
        public List<KeyValuePair<int, Vector3>> AllClientNpcs() { LoadData(); return clientNpcAll; }
        /// <summary>WorldNameData: zona y "dong" que contienen el punto (x, z). false si no hay ninguno ("unknown").</summary>
        public bool DongAt(Vector2 pt, out string dong, out string zone)
        {
            LoadData(); for (int i = 0; i < dongs.Count; i++) if (Contains(dongs[i].Area, pt)) { dong = dongs[i].Dong; zone = dongs[i].Zone; return true; }
            dong = "unknown"; zone = "unknown"; return false;
        }
        public Texture MapIconTexture(int i) { return MapIcon(i); }
        public NpcTableElement NpcRowOf(int type) { return NpcRow(type); }
        public string NpcNameOf(int type) { return NpcName(type); }
        /// <summary>cnMissionManager.NPCTypeHasMissionAdvanceAvailable (2) / NPCTypeHasNewMissionAvailable (1) para un tipo de NPC.</summary>
        public int MissionSymbolOf(int npcType)
        {
            var row = NpcRow(npcType); var m = ents != null ? ents.Missions : null; return m != null && row != null ? m.SymbolFor(npcType, row.m_iNpcType) : 0;
        }

        // ---- servicio de misiones de NPC (cnMissionManager: RequestTaskStart / RequestTaskComplete / CheckToCompleteTaskCondition / NpcIconMode.CheckQuest) ----
        /// <summary>Instancia (iID) de un NPC por su numero de tabla (NpcContainer.SearchTableID), 0 si no esta cerca.</summary>
        int NpcInstanceOf(int npcNumber)
        {
            if (ents == null || npcNumber <= 0) return 0;
            foreach (var go in ents.NpcObjects) { if (go == null) continue; var i = go.GetComponent<NpcInfo>(); if (i != null && i.NpcNumber == npcNumber) return i.Id; }
            return 0;
        }
        /// <summary>REQ_PC_TASK_START { iTaskNum, iNPC_ID, iEscortNPC_ID }.</summary>
        public void RequestTaskStart(int taskId, int npcInstId)
        {
            MissionElement me; if (taskId <= 0 || NpcUI.Sender == null || !taskById.TryGetValue(taskId, out me)) return;
            if (ents != null && ents.Missions != null) ents.Missions.RequestedStart(taskId);   // arrRequestStartTask.Add
            if (npcInstId == 0 && me.m_iHNPCID > 0) npcInstId = NpcInstanceOf(me.m_iHNPCID);
            int escort = me.m_iHTaskType == 6 && me.m_iCSUDEFNPCID > 0 ? NpcInstanceOf(me.m_iCSUDEFNPCID) : 0;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_TASK_START, 12); BitConverter.GetBytes(taskId).CopyTo(p, 4); BitConverter.GetBytes(npcInstId).CopyTo(p, 8); BitConverter.GetBytes(escort).CopyTo(p, 12);
            try { NpcUI.Sender(p); ResetNode(taskId, me); } catch (Exception ex) { Debug.LogWarning("[nanocom] TASK_START: " + ex.Message); }
        }
        /// <summary>REQ_PC_TASK_END { iTaskNum, iNPC_ID, sbyte iBox1Choice, sbyte iBox2Choice, iEscortNPC_ID }.</summary>
        public void RequestTaskEnd(int taskId, int npcInstId, int box1, int box2)
        {
            MissionElement me; if (taskId <= 0 || NpcUI.Sender == null || !taskById.TryGetValue(taskId, out me)) return;
            int escort = me.m_iHTaskType == 6 && me.m_iCSUDEFNPCID > 0 ? npcInstId : 0;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_TASK_END, 16); BitConverter.GetBytes(taskId).CopyTo(p, 4); BitConverter.GetBytes(npcInstId).CopyTo(p, 8);
            p[12] = (byte)box1; p[13] = (byte)box2; BitConverter.GetBytes(escort).CopyTo(p, 16);
            try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[nanocom] TASK_END: " + ex.Message); }
        }
        void TaskEndFail(int taskNum, int err)   // ProcessEndFail (codigos 1/11/12: la mision se pierde y se inicia la tarea de fallo)
        {
            MissionElement me; if (!taskById.TryGetValue(taskNum, out me)) return;
            if (err == 1 || err == 11 || err == 12)
            {
                if (me.m_iHTaskType == 6 && me.m_iCSUDEFNPCID > 0) EscortGroup(me.m_iCSUDEFNPCID, false);
                SetBubbleChat(3, me); SetMissionMessage(me.m_iFMessageSendNPC, me.m_iFMessageType, me.m_iHMissionName, me.m_iFMessageTextID, 9, 0);
                RemoveActiveMission(me.m_iHMissionID); if (ents != null && ents.Missions != null) ents.Missions.OnTaskEnd(taskNum);
                if (me.m_iHMissionID == selectedMission) ResetSelect();
                if (me.m_iFOutgoingTask > 0) RequestTaskStart(me.m_iFOutgoingTask, 0);
            }
            textVersion++;
        }
        /// <summary>CheckToCompleteTaskCondition: 3 = lista para completar, 4 = faltan objetivos / no activa, 6 = tiempo agotado.</summary>
        public int TaskCompleteState(int taskId)
        {
            TaskNode n = null; for (int i = 0; i < active.Count; i++) if (active[i].Me.m_iHTaskID == taskId) { n = active[i]; break; }
            if (n == null) return 4; var me = n.Me;
            if (me.m_iCSUCheckTimer > 0 && n.Life >= 0f && RemainTime(n) <= 0f) return 6;
            for (int i = 0; i < 3; i++) if (At(me.m_iCSUEnemyID, i) > 0 && n.RemainEnemy[i] > 0) return 4;
            for (int j = 0; j < 3; j++) { int id = At(me.m_iCSUItemID, j); if (id > 0 && At(me.m_iCSUItemNumNeeded, j) - QuestItemCount(id) > 0) return 4; }
            return 3;
        }
        /// <summary>NpcIconMode.CheckQuest: misiones que el NPC ofrece (start) y las que puede cerrar (complete).</summary>
        public void MissionLists(int npcNumber, List<MissionElement> start, List<MissionElement> complete)
        {
            start.Clear(); complete.Clear(); var m = ents != null ? ents.Missions : null; if (m == null) return;
            if (hud != null && hud.Data != null && hud.Data.Level > 0) m.Level = hud.Data.Level;
            m.CompletableAt(npcNumber, TaskCompleteState, complete); m.StartableAt(npcNumber, start);
        }
        /// <summary>Cuantas misiones activas hay de un tipo (1 guia, 2 nano, 3 mundo) y si cabe otra (cnMissionJournal.CheckMissionSlot: 1/1/4).</summary>
        public bool HasMissionSlot(int missionType)
        {
            int a = 0, b = 0, c = 0; foreach (var n in active) { switch (n.Me.m_iHMissionType) { case 1: a++; break; case 2: b++; break; case 3: c++; break; } }
            switch (missionType) { case 1: return a < 1; case 2: return b < 1; case 3: return c < 4; default: return false; }
        }
        public string MissionNameOf(MissionElement me) { var sd = mt != null ? mt.m_pMissionStringData : null; int i = me.m_iHMissionName; return sd != null && i >= 0 && i < sd.Length && sd[i] != null ? sd[i].m_pstrNameString ?? "" : ""; }
        // ---- API para JournalWindow (cnMissionJournal) ----
        /// <summary>m_ActivateMissionList (una tarea por mision).</summary>
        public List<MissionElement> JournalActive() { var l = new List<MissionElement>(); for (int i = 0; i < active.Count; i++) l.Add(active[i].Me); return l; }
        /// <summary>Misiones completadas (ultima tarea de cada una), solo tipos 1..3 con diario.</summary>
        public List<MissionElement> JournalCompleted()
        {
            var l = new List<MissionElement>(); var m = ents != null ? ents.Missions : null; if (m == null) return l;
            foreach (int id in m.CompletedIds) { List<MissionElement> t; if (!tasksOfMission.TryGetValue(id, out t) || t.Count == 0) continue; var me = t[t.Count - 1]; if (me.m_iHMissionType >= 1 && me.m_iHMissionType <= 3) l.Add(me); }
            return l;
        }
        public MissionElement JournalFirstTask(int missionId) { List<MissionElement> t; return tasksOfMission.TryGetValue(missionId, out t) && t.Count > 0 ? t[0] : null; }
        public MissionElement JournalFinalTask(int missionId) { List<MissionElement> t; return tasksOfMission.TryGetValue(missionId, out t) && t.Count > 0 ? t[t.Count - 1] : null; }
        public int JournalSelected { get { return curSel != null && curSel.Me != null ? curSel.Me.m_iHMissionID : 0; } }
        /// <summary>"Make current mission" (m_iMenualSelectedMissionID + StartSelectMissionTask).</summary>
        public void JournalSelect(int missionId) { selectedMission = missionId; StartSelect(); }
        public XdtTableScript Tables { get { return tables; } }
        public int JournalGuide { get { return ents != null && ents.Missions != null ? ents.Missions.Mentor : 0; } }
        /// <summary>Texto de progreso del Journal: objetivo actual + tiempo restante + muertes + objetos.</summary>
        public string JournalProgress(int missionId)
        {
            TaskNode n = null; for (int i = 0; i < active.Count; i++) if (active[i].Me.m_iHMissionID == missionId) { n = active[i]; break; }
            if (n == null || mt == null) return ""; var me = n.Me; var sd = mt.m_pMissionStringData; int oi = me.m_iHCurrentObjective;
            var sb = new StringBuilder(sd != null && oi >= 0 && oi < sd.Length && sd[oi] != null ? sd[oi].m_pstrNameString ?? "" : "");
            if (me.m_iSTGrantTimer > 0) sb.Append("\n").Append(Loc.T("Remaining time")).Append(": ").Append((int)Mathf.Max(0f, RemainTime(n)));
            bool flag = false;
            for (int i = 0; i < 3; i++)
            {
                int id = At(me.m_iCSUEnemyID, i), need = At(me.m_iCSUNumToKill, i);
                if (id > 0 && need > 0) { int done = Mathf.Min(need - n.RemainEnemy[i], need); if (!flag) sb.Append("\n"); sb.Append(NpcName(id)).Append(" : ").Append(done).Append("/").Append(need).Append("\n"); flag = true; }
            }
            flag = false;
            for (int j = 0; j < 3; j++)
            {
                int id = At(me.m_iCSUItemID, j), need = At(me.m_iCSUItemNumNeeded, j);
                if (id > 0 && need > 0) { int done = Mathf.Min(QuestItemCount(id), need); if (!flag) sb.Append("\n"); sb.Append(QuestItemName(id)).Append(" : ").Append(done).Append("/").Append(need).Append("\n"); flag = true; }
            }
            return sb.ToString();
        }
        /// <summary>"Dong - Zona" donde esta el NPC (cnEvent 9,2); " " si se desconoce.</summary>
        public string JournalNpcLocation(int npcType)
        {
            var p = ClientNpcPosOf(npcType); if (!p.HasValue || p.Value == Vector3.zero) return " ";
            string d, z; if (!DongAt(new Vector2(p.Value.x, p.Value.z), out d, out z)) return " "; return d + " - " + z;
        }
        public string NpcNameForJournal(int type) { return NpcName(type); }

        public string CurrentDongName { get { return dongName; } }
    }
}
