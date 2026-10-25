using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed partial class NanocomHud
    {
        // cnMissionManager.Update / KillMissionNPC / CheckMissionItemCount.
        // Detection only requests TASK_END; progress, rewards and next tasks remain
        // acknowledgement-driven. Never use map names or client waypoints as proof.
        readonly Dictionary<int,float> startRequests = new Dictionary<int,float>();
        readonly Dictionary<int,float> endRequests = new Dictionary<int,float>();
        readonly HashSet<int> acknowledgedEnds = new HashSet<int>();
        float nextObjectiveTick;
        float nextObjectiveReport; int objectiveReports;
        bool RequestPending(Dictionary<int,float> pending, int task) => pending.TryGetValue(task,out float until) && Time.realtimeSinceStartup < until;
        void ResetObjectiveRequests() { startRequests.Clear(); endRequests.Clear(); acknowledgedEnds.Clear(); nextObjectiveTick=nextObjectiveReport=0; objectiveReports=0; }

        static void RestoreEnemyCounts(TaskNode node, byte[] packet, int offset)
        {
            for (int slot=0;slot<3;slot++)
            {
                int enemy=At(node.Me.m_iCSUEnemyID,slot); if(enemy<=0)continue;
                for(int saved=0;saved<3;saved++) if(enemy==I(packet,offset+4+4*saved))
                { node.RemainEnemy[slot]=Mathf.Clamp(I(packet,offset+16+4*saved),0,Mathf.Max(0,At(node.Me.m_iCSUNumToKill,slot))); break; }
            }
        }
        void TickMissionObjectives()
        {
            if (Time.realtimeSinceStartup < nextObjectiveTick) return;
            nextObjectiveTick=Time.realtimeSinceStartup+1f;
            var client=GameClient.FindSelf; var self=SelfT();
            // The isolated Tech Square controller owns its own scripted lesson order.
            if (TutorialTraining.Active || client==null || !client.WorldConnected || self==null ||
                WorldBootstrap.Travelling || WarpFlow.Blocking || PlayerLife.Active ||
                OpenFusion.Screens.LoadingScreen.I != null && OpenFusion.Screens.LoadingScreen.I.Visible && !OpenFusion.Screens.LoadingScreen.I.Mini) return;
            if (hud?.Data!=null && hud.Data.Hp<=0) return;
            EvaluateMissionObjectives(self.position);
        }
        void EvaluateMissionObjectives(Vector3 position)
        {
            foreach(var node in active)
            {
                var me=node.Me; if(me==null || RequestPending(endRequests,me.m_iHTaskID))continue;
                if(objectiveReports<12 && Time.realtimeSinceStartup>=nextObjectiveReport && me.m_iHTaskType==2)
                {
                    var marker=ObjectiveNpc(me.m_iHTerminatorNPCID,position);var definition=NpcRow(me.m_iHTerminatorNPCID);
                    Debug.Log("[missions] destination task="+me.m_iHTaskID+" marker="+me.m_iHTerminatorNPCID+" instance="+(marker!=null?marker.Id:0)+" distance="+(marker!=null?Vector3.Distance(position,marker.transform.position).ToString("F2"):"unavailable")+" radius="+(definition!=null?definition.m_iSightRange*.01f:0));
                    nextObjectiveReport=Time.realtimeSinceStartup+15f;objectiveReports++;
                }
                if(me.m_iSTGrantTimer>0 && node.Life>=0 && RemainTime(node)<=0)
                { RequestTaskEnd(me.m_iHTaskID,0,0,0); continue; }
                if(TaskCompleteState(me.m_iHTaskID)!=3)continue;
                int npc=0;
                if(me.m_iHTerminatorNPCID>0)
                {
                    if(me.m_iHTaskType!=2 && me.m_iHTaskType!=6)continue;
                    var target=ObjectiveNpc(me.m_iHTerminatorNPCID,position);
                    var row=NpcRow(me.m_iHTerminatorNPCID);
                    if(target==null || row==null || row.m_iSightRange<=0 ||
                        Vector3.Distance(position,target.transform.position)>=row.m_iSightRange*.01f)continue;
                    npc=target.Id;
                    if(me.m_iHTaskType==6 && me.m_iCSUDEFNPCID>0)
                    {
                        var escort=ObjectiveNpc(me.m_iCSUDEFNPCID,position);
                        if(escort==null || Vector3.Distance(escort.transform.position,target.transform.position)>=5f)continue;
                    }
                }
                else
                {
                    bool counted=false;
                    for(int i=0;i<3;i++) if(At(me.m_iCSUNumToKill,i)>0 || At(me.m_iCSUItemNumNeeded,i)>0)counted=true;
                    if(!counted)continue; // Dialogue and script-only tasks are not auto-completed.
                }
                int reward=WarpFlow.LevelExpansion==1 ? me.m_iKorSuccRewardID : me.m_iSUReward;
                if(reward>0)
                {
                    // Never choose a reward on behalf of the player or replace a modal.
                    if(!GameMenu.Blocking && !NpcUI.Active && !WelcomePopup.Active && JournalWindow.Instance!=null)
                        JournalWindow.Instance.OpenMode(2,me,npc);
                }
                else RequestTaskEnd(me.m_iHTaskID,npc,0,0);
            }
        }
        NpcInfo ObjectiveNpc(int number,Vector3 position)
        {
            if(ents==null || number<=0)return null;
            NpcInfo nearest=null;float best=float.PositiveInfinity;
            foreach(var obj in ents.NpcObjects)
            {
                if(obj==null || !obj.activeInHierarchy)continue;
                var info=obj.GetComponent<NpcInfo>();
                if(info==null || info.Id<=0 || info.NpcNumber!=number || info.Hp<=0)continue;
                float d=(info.transform.position-position).sqrMagnitude;
                if(d<best){best=d;nearest=info;}
            }
            return nearest;
        }
    }
}
