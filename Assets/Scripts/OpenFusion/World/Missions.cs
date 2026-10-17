// Estado de misiones del jugador (a partir de PC_ENTER_SUCC) y lógica de los símbolos de misión sobre los NPC, copiada de cnMissionManager:
// NPCTypeHasNewMissionAvailable (símbolo '!', efecto 866) y NPCTypeHasMissionAdvanceAvailable (símbolo '?', efecto 865). SIN COMPILAR NI PROBAR EN UNITY.
// Estado vivo via OnTaskStart/End/Stop (NanocomHud); comprueba nanos requeridos con InventoryWindow.OwnsNano; no comprueba arrRequestStartTask.
using System.Collections.Generic;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class MissionState
    {
        readonly Dictionary<int, MissionElement> taskById = new Dictionary<int, MissionElement>();
        readonly Dictionary<int, List<MissionElement>> grant = new Dictionary<int, List<MissionElement>>(), terminate = new Dictionary<int, List<MissionElement>>();
        readonly HashSet<int> completed = new HashSet<int>(), activeMissions = new HashSet<int>(), activeTasks = new HashSet<int>();
        readonly Dictionary<int, List<int>> tasksOfMission = new Dictionary<int, List<int>>();
        readonly QuestSave q; public int Level;
        public int CompletedCount => completed.Count; public int ActiveCount => activeTasks.Count;
        public int Mentor => q.Mentor; public IEnumerable<int> CompletedIds => completed; public void MarkCompleted(int missionId) { completed.Add(missionId); }

        public MissionState(MissionTableScript t, QuestSave q, int level)
        {
            this.q = q; this.Level = level; var data = t.m_pMissionData; var missionIds = new HashSet<int>();
            for (int i = 0; i < data.Length; i++)
            {
                var m = data[i]; if (m == null) continue; taskById[m.m_iHTaskID] = m; missionIds.Add(m.m_iHMissionID); { List<int> tl0; if (!tasksOfMission.TryGetValue(m.m_iHMissionID, out tl0)) tasksOfMission[m.m_iHMissionID] = tl0 = new List<int>(); tl0.Add(m.m_iHTaskID); }
                if (i >= 1) { if (m.m_iHNPCID > 0) Add(grant, m.m_iHNPCID, m); if (m.m_iHTerminatorNPCID > 0) Add(terminate, m.m_iHTerminatorNPCID, m); }
            }
            for (int i = 0; i < 16; i++) for (int j = 0; j < 64; j++) { int id = 1 + i * 64 + j; if (missionIds.Contains(id) && ((q.Flags[i] >> j) & 1) != 0) completed.Add(id); }
            foreach (var tid in q.Running) if (tid > 0 && taskById.TryGetValue(tid, out var m)) { activeTasks.Add(tid); activeMissions.Add(m.m_iHMissionID); }
        }
        static void Add(Dictionary<int, List<MissionElement>> d, int k, MissionElement m) { if (!d.TryGetValue(k, out var l)) d[k] = l = new List<MissionElement>(); foreach (var x in l) if (x.m_iHTaskID == m.m_iHTaskID) return; l.Add(m); }

        // arrRequestStartTask: tareas pedidas al servidor que aun no han respondido (bloquean otra peticion de la misma mision); caducan a los 10 s por si no hay respuesta
        readonly Dictionary<int, float> requested = new Dictionary<int, float>();
        public void RequestedStart(int taskId) { requested[taskId] = UnityEngine.Time.realtimeSinceStartup; }
        bool StartPending(int missionId)
        {
            if (requested.Count == 0) return false; List<int> old = null; bool hit = false; float now = UnityEngine.Time.realtimeSinceStartup;
            foreach (var kv in requested)
            {
                if (now - kv.Value > 10f) { if (old == null) old = new List<int>(); old.Add(kv.Key); continue; }
                MissionElement pm; if (taskById.TryGetValue(kv.Key, out pm) && pm.m_iHMissionID == missionId) hit = true;
            }
            if (old != null) foreach (var k in old) requested.Remove(k);
            return hit;
        }
        bool RepeatDone(int flag) { int n = flag / 8, b = flag % 8; if (b == 0) { n--; b = 8; } return n > -1 && n < q.Repeat.Length && ((q.Repeat[n] >> (b - 1)) & 1) != 0; }
        int QuestItem(int id) { for (int i = 0; i < q.QItemId.Length; i++) if (q.QItemId[i] == id && id != 0) return q.QItemOpt[i]; return 0; }

        /// 0 = se puede empezar; otro valor = no (como CheckToStartTaskCondition).
        int CheckStart(int taskId)
        {
            if (taskId <= 0 || !taskById.TryGetValue(taskId, out var m)) return -1;
            if (StartPending(m.m_iHMissionID)) return 1;   // foreach (arrRequestStartTask): misma mision pendiente
            if (m.m_iRepeatflag == 0) { if (completed.Contains(m.m_iHMissionID)) return 5; } else if (RepeatDone(m.m_iRepeatflag)) return 5;
            if (activeMissions.Contains(m.m_iHMissionID)) return 2;
            if (m.m_iCSTReqMission != null) foreach (var r in m.m_iCSTReqMission) if (r > 0 && !completed.Contains(r)) return 1;
            if (m.m_iCTRReqLvMin > 0 && Level < m.m_iCTRReqLvMin) return 1;
            if (m.m_iCTRReqLvMax > 0 && Level < m.m_iCTRReqLvMax) return 1;   // (sic) el original compara con el nivel maximo igual que con el minimo
            if (m.m_iCSTRReqNano != null) foreach (var nn in m.m_iCSTRReqNano) if (nn > 0 && !InventoryWindow.OwnsNano(nn)) return 1;   // nano requerido (aNanoBank)
            if (m.m_iCSTReqGuide > 0 && q.Mentor != m.m_iCSTReqGuide) return 1;
            if (m.m_iCSTItemID != null) for (int i = 0; i < m.m_iCSTItemID.Length; i++) if (m.m_iCSTItemID[i] > 0 && QuestItem(m.m_iCSTItemID[i]) < (m.m_iCSTItemNumNeeded != null && i < m.m_iCSTItemNumNeeded.Length ? m.m_iCSTItemNumNeeded[i] : 1)) return 1;
            if (m.m_iCSTTrigger > 0 && !activeTasks.Contains(m.m_iCSTTrigger)) return 1;
            return 0;
        }

        // ---- actualizacion en vivo (cnMissionManager.ProcessStartSucc / ProcessEndSucc / DelActiveMissionSucc) ----
        void DropMission(int missionId) { List<int> l; if (tasksOfMission.TryGetValue(missionId, out l)) foreach (var t in l) activeTasks.Remove(t); activeMissions.Remove(missionId); }
        public void OnTaskStart(int taskId) { requested.Remove(taskId); MissionElement m; if (!taskById.TryGetValue(taskId, out m)) return; DropMission(m.m_iHMissionID); activeTasks.Add(taskId); activeMissions.Add(m.m_iHMissionID); }
        /// <summary>cnMissionManager.AddRepeatFlag (ProcessEndSucc de una mision repetible).</summary>
        public void MarkRepeat(int flag) { int n = flag / 8, b = flag % 8; if (b == 0) { n--; b = 8; } if (n > -1 && n < q.Repeat.Length) q.Repeat[n] |= 1L << (b - 1); }
        public void OnTaskEnd(int taskId) { MissionElement m; if (!taskById.TryGetValue(taskId, out m)) return; DropMission(m.m_iHMissionID); }
        public void OnTaskStop(int taskId) { OnTaskEnd(taskId); }
        public bool IsTaskActive(int taskId) { return activeTasks.Contains(taskId); }
        public bool IsMissionActive(int missionId) { return activeMissions.Contains(missionId); }

        // ---- listas del dialogo de NPC (NpcIconMode.CheckQuest): misiones que el NPC da (CheckStart == 0) y que termina (tarea activa lista para completar, estado 3) ----
        public void StartableAt(int npcNumber, List<MissionElement> into)
        {
            List<MissionElement> gl; if (!grant.TryGetValue(npcNumber, out gl)) return;
            foreach (var t in gl)
            {
                if (CheckStart(t.m_iHTaskID) != 0) continue; bool dup = false;
                foreach (var x in into) if (x.m_iHMissionID == t.m_iHMissionID) { dup = true; break; }
                if (!dup) into.Add(t);
            }
        }
        public void CompletableAt(int npcNumber, System.Func<int, int> completeState, List<MissionElement> into)
        {
            List<MissionElement> tl; if (!terminate.TryGetValue(npcNumber, out tl)) return;
            foreach (var t in tl)
            {
                if (!activeTasks.Contains(t.m_iHTaskID) || completeState(t.m_iHTaskID) != 3) continue; bool dup = false;
                foreach (var x in into) if (x.m_iHMissionID == t.m_iHMissionID) { dup = true; break; }
                if (!dup) into.Add(t);
            }
        }

        public static bool NoMissionNpc(int type) => type == 0 || type == 25 || type == 100 || type == 101 || type == 105 || type == 110 || type == 111;

        /// 0 = nada, 1 = misión nueva ('!'), 2 = se puede avanzar/terminar ('?').
        public int SymbolFor(int npcNumber, int npcType)
        {
            if (NoMissionNpc(npcType)) return 0;
            if (terminate.TryGetValue(npcNumber, out var tl)) foreach (var t in tl) if (activeTasks.Contains(t.m_iHTaskID)) return 2;
            if (grant.TryGetValue(npcNumber, out var gl))
                foreach (var t in gl)
                {
                    if (t.m_iRepeatflag == 0) { if (completed.Contains(t.m_iHMissionID)) continue; } else if (RepeatDone(t.m_iRepeatflag)) continue;
                    if (activeMissions.Contains(t.m_iHMissionID) || CheckStart(t.m_iHTaskID) != 0) continue;
                    return 1;
                }
            return 0;
        }
    }
}
