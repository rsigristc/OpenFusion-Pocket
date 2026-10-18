using System;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class DualMissionRegressionChecks
    {
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        public static void Run()
        {
            var sender = NpcUI.Sender; bool registered = GameMenu.Implemented.Contains(GameMenu.Win.Journal);
            var host = new GameObject("DS mission fixture");
            try
            {
                GameMenu.CloseWindow();
                var world = host.AddComponent<WorldEntities>(); var hud = host.AddComponent<PlayerHud>();
                var missions = new MissionElement[5];
                for (int i = 0; i < missions.Length; i++) missions[i] = new MissionElement {
                    m_iHMissionID = i + 1, m_iHTaskID = 101 + i, m_iHMissionType = i % 3 + 1,
                    m_iHMissionName = 1, m_iHCurrentObjective = 2, m_iSTJournalIDAdd = 1, m_iSUReward = 0,
                    m_iCSUEnemyID = new[] { 99, 0, 0 }, m_iCSUNumToKill = new[] { 7, 0, 0 }
                };
                var mt = new MissionTableScript { m_pMissionData = missions,
                    m_pMissionStringData = new[] { new NameStringElement(), new NameStringElement { m_pstrNameString = "Fixture mission" }, new NameStringElement { m_pstrNameString = "Defeat enemies" } },
                    m_pJournalData = new[] { new JournalElement(), new JournalElement { m_iDetaileMissionDesc = 2, m_iDetailedTaskDesc = 2 } },
                    m_pRewardData = new[] { new RewardElement { m_iCash = 224, m_iFusionMatter = 600 } } };
                var tables = new XdtTableScript { m_pMissionTable = mt };
                world.Missions = new MissionState(mt, new QuestSave(), 9); world.Missions.MarkCompleted(4);
                var nh = host.AddComponent<NanocomHud>(); nh.Init(hud, world, null, tables);
                for (int i = 0; i < 3; i++) Call(nh, "InsertActive", Call(nh, "ResetNode", missions[i].m_iHTaskID, missions[i]));
                var journal = JournalWindow.Ensure(host); journal.Init(null, nh, world, tables);
                int sends = 0; byte[] packet = null; NpcUI.Sender = p => { sends++; packet = p; };
                var state = journal.DualMissions(t => "");
                Check(state.entries.Count == 3 && state.detail.taros == 224 && state.detail.fusion == 600 && state.detail.progress.Contains("0/7"), "DS must export actual journal entries, rewards and live enemy counters.");
                Check(journal.DualSelectMission(3, 103) && sends == 0 && nh.JournalSelected == 0, "Inspecting a mission must not change tracking or send a packet.");
                Check(journal.DualFollowMission(3, 103) && nh.JournalSelected == 3, "Track must update the original HUD mission selection.");
                Check(!journal.DualFollowMission(3, 999) && !journal.DualAbandonMission(3, 999), "Reject commands referring to a stale task.");
                int before = sends;
                Check(journal.DualAbandonMission(3, 103) && sends == before + 1 && BitConverter.ToInt32(packet, 4) == 103, "Abandon must send the existing stop packet for the exact task.");
                Check(!journal.DualAbandonMission(3, 103) && journal.DualMissions(t => "").entries.Count == 3, "Block duplicate requests and retain the mission until the server acknowledges it.");
                Call(nh, "TaskStop", 103);
                Check(journal.DualMissions(t => "").entries.Count == 2, "Server stop acknowledgement must remove the mission from DS.");
                journal.DualMissionScroll(false, 42, 84); journal.DualMissionMode(true);
                state = journal.DualMissions(t => "");
                Check(state.entries.Count == 1 && state.detail.id == 4 && !state.detail.canFollow && !state.detail.canAbandon && !journal.DualFollowMission(4, 104), "Completed missions use server completion state and have no active actions.");
                journal.DualMissionMode(false); state = journal.DualMissions(t => "");
                Check(state.listScroll == 42 && state.detailScroll == 84, "Tab changes preserve independent native scroll state.");
                journal.DualSelectMission(1, 101); Call(journal, "OnOpen");
                Check(journal.DualMissions(t => "").detail.id == 1, "Opening the original Journal preserves the DS inspection selection.");
                Debug.Log("[DS mission regression] live categories/progress/rewards, inspection vs tracking, stale tasks, acknowledged abandonment, completed mode and retained state passed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); NpcUI.Sender = sender; if (!registered) GameMenu.Implemented.Remove(GameMenu.Win.Journal); }
        }
    }
}
