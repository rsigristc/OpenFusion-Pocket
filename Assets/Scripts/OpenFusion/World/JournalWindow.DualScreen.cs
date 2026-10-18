using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed partial class JournalWindow
    {
        [Serializable] public sealed class DualMissionEntry
        { public int id, task, category; public string name, objective; public bool current, ready; }
        [Serializable] public sealed class DualMissionReward
        { public int type, id, group, choices; public string name, image; }
        [Serializable] public sealed class DualMissionDetail
        {
            public int id, task, level, taros, fusion;
            public string name, difficulty, description, objective, progress, npc, location, npcImage, banner, nanoName, nanoImage;
            public bool current, canFollow, canAbandon, rewardsKnown;
            public List<DualMissionReward> rewards = new List<DualMissionReward>();
        }
        [Serializable] public sealed class DualMissionState
        {
            public bool completed, busy;
            public int listScroll, detailScroll;
            public string nanoIcon, guideIcon, worldIcon, tarosIcon, fusionIcon;
            public List<DualMissionEntry> entries = new List<DualMissionEntry>();
            public DualMissionDetail detail;
        }
        bool dualJournalPrepared;
        readonly int[] dualListScroll = new int[2], dualDetailScroll = new int[2];
        int dualStoppingTask;
        float dualStopUntil;
        List<MissionElement> DualMissionList() => nh == null ? new List<MissionElement>() : tab == 1 ? nh.JournalCompleted() : nh.JournalActive();
        MissionElement DualFindMission(int id, int task)
        {
            foreach (var mission in DualMissionList()) if (mission.m_iHMissionID == id && mission.m_iHTaskID == task) return mission;
            return null;
        }
        bool DualMissionActions => nh != null && mode == 0 && !GameMenu.Blocking && !NpcUI.Active && !PlayerLife.Active &&
            !WelcomePopup.Active && !ChatHud.Typing && NpcUI.Sender != null;
        public void DualMissionMode(bool completed)
        {
            if (mode != 0 || nh == null || mt == null) return;
            int desired = completed ? 1 : 0;
            if (tab != desired) { tab = desired; SetActive(true); }
            dualJournalPrepared = true;
        }
        public bool DualSelectMission(int id, int task)
        {
            if (mode != 0) return false;
            var mission = DualFindMission(id, task); if (mission == null) return false;
            if (cur == null || cur.m_iHTaskID != task) { SetCurrent(mission); dualDetailScroll[tab] = 0; }
            dualJournalPrepared = true; return true;
        }
        public void DualMissionScroll(bool completed, int list, int detail)
        { int index = completed ? 1 : 0; dualListScroll[index] = Mathf.Clamp(list, 0, 100000); dualDetailScroll[index] = Mathf.Clamp(detail, 0, 100000); }
        public bool DualFollowMission(int id, int task)
        {
            if (!DualMissionActions || tab != 0 || DualFindMission(id, task) == null) return false;
            nh.JournalSelect(id); return true;
        }
        // Called only after a native confirmation. Revalidate the exact active task here.
        public bool DualAbandonMission(int id, int task)
        {
            if (!DualMissionActions || tab != 0 || cooldown > 0 || (dualStoppingTask == task && Time.unscaledTime < dualStopUntil)) return false;
            var mission = DualFindMission(id, task); if (mission == null) return false;
            SetCurrent(mission);
            if (!DeleteMission()) return false;
            dualStoppingTask = task; dualStopUntil = Time.unscaledTime + 5;
            return true;
        }
        public DualMissionState DualMissions(Func<Texture, string> image)
        {
            var result = new DualMissionState { completed = tab == 1, busy = !DualMissionActions, listScroll = dualListScroll[tab], detailScroll = dualDetailScroll[tab] };
            if (nh == null || mt == null || mode != 0) return result;
            if (!texTried) { LoadTextures(); texTried = true; }
            result.nanoIcon = image(missionIcon[5]); result.worldIcon = image(missionIcon[6]);
            result.guideIcon = image(missionIcon[Mathf.Clamp(nh.JournalGuide - 1, 0, 4)]);
            result.tarosIcon = image(taros); result.fusionIcon = image(fm);
            var list = DualMissionList();
            if (tab == 1) list.Sort((a,b) => { int level = a.m_iCTRReqLvMin.CompareTo(b.m_iCTRReqLvMin); return level != 0 ? level : string.CompareOrdinal(Str(a.m_iHMissionName), Str(b.m_iHMissionName)); });
            MissionElement selected = null;
            foreach (var mission in list)
            {
                result.entries.Add(new DualMissionEntry { id = mission.m_iHMissionID, task = mission.m_iHTaskID, category = mission.m_iHMissionType,
                    name = Str(mission.m_iHMissionName), objective = tab == 0 ? Str(mission.m_iHCurrentObjective) : EntryOf(mission).Desc,
                    current = tab == 0 && nh.JournalSelected == mission.m_iHMissionID, ready = tab == 1 || nh.TaskCompleteState(mission.m_iHTaskID) == 3 });
                if (cur != null && cur.m_iHMissionID == mission.m_iHMissionID) selected = mission;
            }
            if (selected == null && tab == 0) selected = list.Find(m => m.m_iHMissionID == nh.JournalSelected);
            if (selected == null && list.Count > 0) selected = list[0];
            // Re-read the same journal presentation on every snapshot so task transitions,
            // translations and late-loaded portraits/rewards refresh without a separate model.
            SetCurrent(selected); dualJournalPrepared = true;
            if (selected == null) return result;
            var detail = new DualMissionDetail { id = cur.m_iHMissionID, task = cur.m_iHTaskID, level = cur.m_iCTRReqLvMin,
                name = Str(cur.m_iHMissionName), difficulty = difficulty, description = description, objective = taskDescription,
                progress = tab == 0 ? nh.JournalProgress(cur.m_iHMissionID) : "", npc = npcName, location = npcLoc,
                npcImage = image(npcTex), banner = image(banner[cur.m_iHMissionType == 2 ? 5 : cur.m_iHMissionType == 3 ? 6 : Mathf.Clamp(nh.JournalGuide - 1, 0, 4)]),
                current = tab == 0 && nh.JournalSelected == cur.m_iHMissionID, nanoName = nanoName, nanoImage = image(nanoTex), rewardsKnown = reward != null || nanoTex != null };
            detail.canFollow = tab == 0 && DualMissionActions && !detail.current;
            detail.canAbandon = tab == 0 && DualMissionActions && cooldown <= 0 && !(dualStoppingTask == detail.task && Time.unscaledTime < dualStopUntil);
            if (reward != null)
            {
                detail.taros = reward.m_iCash; detail.fusion = reward.m_iFusionMatter;
                for (int group = 0; group < 2; group++) for (int i = 0; i < 4; i++)
                {
                    int id = group == 0 ? At(reward.m_iMissionRewardItemID, i) : At(reward.m_iMissionRewardItemID2, i);
                    if (id <= 0) continue;
                    int type = group == 0 ? At(reward.m_iMissionRewarItemType, i) : At(reward.m_iMissionRewardItemType2, i);
                    var texture = RewardIcon(type, id, out string name);
                    detail.rewards.Add(new DualMissionReward { id = id, type = type, group = group, choices = group == 0 ? reward.m_iBox1Choice : reward.m_iBox2Choice, name = name, image = image(texture) });
                }
            }
            result.detail = detail; return result;
        }
    }
}
