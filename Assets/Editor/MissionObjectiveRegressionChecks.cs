using System;
using System.Collections.Generic;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.World;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenFusion.Api;
using OpenFusion.Assets;
using OpenFusion.Tables;
using UnityEditor;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class MissionObjectiveRegressionChecks
    {
        public static async void Audit()
        {
            try
            {
                Directory.CreateDirectory("Logs/mission-audit");
                var api = new ServerApi("https://api.ffretrobution.net");
                var info = await api.GetInfo(); var chain = await api.GetChain(info.GameVersions.First());
                var cache = new BundleCache(Path.GetFullPath("Logs/retrobution-303/live"), BundleCatalog.FromChain(chain));
                await cache.EnsureAsync(new[] { "TableData.resourceFile" });
                var tables = TableLoader.Load(cache.Load("TableData.resourceFile")).Xdt;
                File.WriteAllLines("Logs/mission-audit/tasks.jsonl", tables.m_pMissionTable.m_pMissionData.Where(m => m != null).Select(JsonUtility.ToJson));
                File.WriteAllLines("Logs/mission-audit/npcs.jsonl", tables.m_pNpcTable.m_pNpcData.Where(m => m != null).Select(JsonUtility.ToJson));
                File.WriteAllText("Logs/mission-audit/strings.json", Newtonsoft.Json.JsonConvert.SerializeObject(tables.m_pMissionTable));
                RunChecks(tables);
                Debug.Log("[mission audit] Current task, NPC and mission strings exported; checks passed.");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static object Call(object obj,string name,params object[] args) => obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
        static T Field<T>(object obj,string name) => (T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(obj);
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        public static async Task Run()
        {
            var api=new ServerApi("https://api.ffretrobution.net");var info=await api.GetInfo();var chain=await api.GetChain(info.GameVersions.First());
            var cache=new BundleCache(Path.GetFullPath("Logs/retrobution-303/live"),BundleCatalog.FromChain(chain));
            await cache.EnsureAsync(new[]{"TableData.resourceFile"});RunChecks(TableLoader.Load(cache.Load("TableData.resourceFile")).Xdt);
        }
        static void RunChecks(XdtTableScript original)
        {
            Directory.CreateDirectory("Logs/mission-audit");
            var all=original.m_pMissionTable.m_pMissionData.Where(m=>m!=null).ToArray();
            var practice=all.Where(m=>m.m_iHMissionID==504).ToArray();
            Check(practice.Single(m=>m.m_iHTaskID==462).m_iHTerminatorNPCID==1099,"Practice Pranks destination changed; re-audit the data.");
            Check(original.m_pNpcTable.m_pNpcData.Single(n=>n!=null&&n.m_iNpcNumber==1099).m_iSightRange==1500,"Practice Pranks radius differs from current tables.");
            var oldSender=NpcUI.Sender;var results=new List<string>();
            void Scenario(MissionElement[] tasks,Action<WorldEntities,NanocomHud,List<byte[]>> test)
            {
                var host=new GameObject("Mission objective regression");var sent=new List<byte[]>();
                try{
                    var world=host.AddComponent<WorldEntities>();world.NpcTable=original.m_pNpcTable;
                    var mt=new MissionTableScript{m_pMissionData=tasks,m_pMissionStringData=original.m_pMissionTable.m_pMissionStringData,m_pRewardData=original.m_pMissionTable.m_pRewardData};
                    var tables=new XdtTableScript{m_pMissionTable=mt,m_pNpcTable=original.m_pNpcTable};
                    world.Missions=new MissionState(mt,new QuestSave(),9);
                    var nh=host.AddComponent<NanocomHud>();nh.Init(host.AddComponent<PlayerHud>(),world,null,tables);nh.SendCurrentMission=false;
                    foreach(var task in tasks){Call(nh,"InsertActive",Call(nh,"ResetNode",task.m_iHTaskID,task));world.Missions.OnTaskStart(task.m_iHTaskID);}
                    NpcUI.Sender=p=>sent.Add(p);test(world,nh,sent);
                }finally{UnityEngine.Object.DestroyImmediate(host);NpcUI.Sender=oldSender;}
            }
            NpcInfo Npc(WorldEntities world,int number,int id,Vector3 p){var go=new GameObject("Server NPC fixture");go.transform.SetParent(world.transform);go.transform.position=p;var n=go.AddComponent<NpcInfo>();n.NpcNumber=number;n.Id=id;n.Hp=1;Field<Dictionary<int,GameObject>>(world,"npcs")[id]=go;return n;}
            void Evaluate(NanocomHud nh,Vector3 p)=>Call(nh,"EvaluateMissionObjectives",p);
            void Kill(NanocomHud nh,int type){Call(nh,"Handle",PacketIds.P_FE2CL_REP_PC_KILL_QUEST_NPCs_SUCC,BitConverter.GetBytes(type));}
            var location=practice.Single(m=>m.m_iHTaskID==462);
            Scenario(new[]{location,practice.Single(m=>m.m_iHTaskID==466)},(w,n,s)=>{
                // Keep only the current stage; the next row is present for server-driven chaining.
                Call(n,"TaskStop",466);Call(n,"InsertActive",Call(n,"ResetNode",462,location));w.Missions.OnTaskStart(462);
                Npc(w,795,90795,Vector3.zero);Evaluate(n,Vector3.zero);Check(s.Count==0,"The outside teleporter is not the destination marker.");
                var marker=Npc(w,1099,91099,new Vector3(100,20,100));
                Evaluate(n,marker.transform.position+Vector3.up*15.01f);Check(s.Count==0,"Destination checks must include vertical distance and table radius.");
                Evaluate(n,marker.transform.position+Vector3.right*14.99f);
                Check(s.Count==1&&BitConverter.ToInt32(s[0],4)==462&&BitConverter.ToInt32(s[0],8)==91099,"Use actual destination NPC instance ID inside the 15m radius.");
                Evaluate(n,marker.transform.position);Check(s.Count==1&&n.TaskIsActive(462),"No duplicate completion or optimistic task removal.");
                Call(n,"TaskEnd",462);Check(s.Count==2&&BitConverter.ToInt32(s[1],4)==466&&!n.TaskIsActive(462),"Only the server ACK requests the next task.");
                Call(n,"TaskEnd",462);Check(s.Count==2,"Duplicate END ACK must not start the next task twice.");
            });results.Add("PASS: real Practice Pranks task 462, missing/outside/vertical destination, 15m radius, NPC instance ID, pending guard and ACK-only 462->466 transition.");
            Scenario(new[]{practice.Single(m=>m.m_iHTaskID==463)},(w,n,s)=>{
                Kill(n,55);Evaluate(n,Vector3.zero);Check(n.JournalProgress(504).Contains("0/4")&&s.Count==0,"Wrong mob type cannot count.");
                for(int i=0;i<3;i++){Kill(n,2513);Evaluate(n,Vector3.zero);}Check(s.Count==0&&n.JournalProgress(504).Contains("3/4"),"Respect all four required Shocktanglers.");
                Kill(n,2513);Evaluate(n,Vector3.zero);Check(s.Count==1&&n.TaskIsActive(463)&&n.JournalProgress(504).Contains("4/4"),"Four authoritative kill notices request completion without local completion.");
                Kill(n,2513);Evaluate(n,Vector3.zero);Check(s.Count==1&&n.JournalProgress(504).Contains("4/4"),"Extra kills cannot exceed target or duplicate completion.");
            });results.Add("PASS: real four-Shocktangler requirement, wrong species, 3/4, 4/4, surplus clamp and acknowledgement boundary.");
            MissionElement Task(int id,int kind,int dest=0)=>new MissionElement{m_iHMissionID=id,m_iHTaskID=id,m_iHMissionType=3,m_iHTaskType=kind,m_iHTerminatorNPCID=dest};
            var mixed=Task(50001,3);mixed.m_iCSUEnemyID=new[]{55,2513,0};mixed.m_iCSUNumToKill=new[]{2,1,0};mixed.m_iCSUItemID=new[]{133,0,0};mixed.m_iCSUItemNumNeeded=new[]{3,0,0};
            Scenario(new[]{mixed},(w,n,s)=>{
                Kill(n,55);Kill(n,55);Kill(n,2513);Call(n,"SetQuestItem",0,(short)133,2);Evaluate(n,Vector3.zero);Check(s.Count==0,"All enemy AND item requirements must pass.");
                Call(n,"SetQuestItem",0,(short)133,3);Evaluate(n,Vector3.zero);Check(s.Count==1&&n.QuestItemQty(133)==3,"Authoritative quest-item update releases collection objective.");
            });results.Add("PASS: mixed enemy types and item requirements, insufficient/complete collection.");
            var talk=Task(50002,1,795);Scenario(new[]{talk},(w,n,s)=>{Npc(w,795,90795,Vector3.zero);Evaluate(n,Vector3.zero);Check(s.Count==0,"Talking/delivery must not automatically complete merely by proximity.");});
            var timed=Task(50003,3);timed.m_iSTGrantTimer=20;timed.m_iCSUCheckTimer=1;
            Scenario(new[]{timed},(w,n,s)=>{Evaluate(n,Vector3.zero);Check(s.Count==0,"Unknown reconnect timer cannot be invented/expired.");Call(n,"TaskStart",50003,0);Evaluate(n,Vector3.zero);Check(s.Count==1,"Known expired timer must request server resolution.");});
            var escort=Task(50004,6,795);escort.m_iCSUDEFNPCID=769;
            Scenario(new[]{escort},(w,n,s)=>{Npc(w,795,90795,Vector3.zero);var e=Npc(w,769,90769,Vector3.right*6);Evaluate(n,Vector3.zero);Check(s.Count==0,"Escort NPC must also arrive.");e.transform.position=Vector3.right*4;Evaluate(n,Vector3.zero);Check(s.Count==1&&BitConverter.ToInt32(s[0],16)==90769,"Escort request carries escort instance, not destination instance.");});
            results.Add("PASS: manual dialogue, unknown/expired timers and escort 5m/instance-ID constraints.");
            Scenario(new[]{location},(w,n,s)=>{
                typeof(WorldEntities).GetField("catalog",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(w,ModelCatalog.From(original));typeof(WorldEntities).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(w,w.transform);
                Call(w,"SpawnNpc",new NpcAppearance{Id=91099,Type=1099,Hp=597,X=10000,Y=10000,Z=2000});
                var marker=w.NpcObjects.Single();Check(marker.GetComponent<NpcInfo>()?.NpcNumber==1099,"Missing streamed graphics must not remove mission marker metadata.");Check(marker.GetComponent<RemoteMover>().Flying&&Mathf.Approximately(marker.transform.position.y,20),"Invisible location marker must preserve server altitude rather than snap to terrain.");Evaluate(n,marker.transform.position);Check(s.Count==1,"The actual NPC spawn path must resolve destinations without a loaded model.");
            });results.Add("PASS: actual NPC spawn without visual assets retains invisible location metadata and server altitude.");
            Scenario(new[]{practice.Single(m=>m.m_iHTaskID==463)},(w,n,s)=>{
                var row=practice.Single(m=>m.m_iHTaskID==463);var node=Call(n,"ResetNode",463,row);var packet=new byte[52];BitConverter.GetBytes(2513).CopyTo(packet,12);BitConverter.GetBytes(1).CopyTo(packet,24);
                typeof(NanocomHud).GetMethod("RestoreEnemyCounts",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{node,packet,(object)0});Check(Field<int[]>(node,"RemainEnemy")[0]==1,"Reconnect must map server remaining counts by enemy ID, not slot order.");
                Call(n,"InsertActive",node);Check(n.JournalProgress(504).Contains("3/4"),"Restored progress must display 3/4 rather than restart at 0/4.");Evaluate(n,Vector3.zero);Check(s.Count==0,"Restored incomplete mission cannot finish early.");
            });results.Add("PASS: reconnect kill counters matched by species across reordered server slots; no progress reset or early completion.");
            var gateTask=Task(50005,4);gateTask.m_iCSTItemID=new[]{133,0,0};gateTask.m_iCSTItemNumNeeded=new[]{3,0,0};
            var saved=new QuestSave();var gate=new MissionState(new MissionTableScript{m_pMissionData=new[]{gateTask}},saved,9);
            Check(!gate.CanStart(50005),"Missing prerequisite quest items cannot offer a new mission.");gate.UpdateQuestItem(0,133,3);Check(gate.CanStart(50005),"Quest item eligibility must update without reconnecting.");gate.UpdateQuestItem(0,0,0);Check(!gate.CanStart(50005),"Removed prerequisite items must invalidate eligibility.");
            var repeatStart=Task(50006,3);repeatStart.m_iCSUEnemyID=new[]{55,0,0};repeatStart.m_iCSUNumToKill=new[]{4,0,0};
            Scenario(new[]{repeatStart},(w,n,s)=>{Kill(n,55);n.RequestTaskStart(50006,0);n.RequestTaskStart(50006,0);Check(s.Count==1&&n.JournalProgress(50006).Contains("1/4"),"Pending start requests cannot duplicate or reset authoritative progress.");Call(n,"Handle",PacketIds.P_FE2CL_REP_PC_TASK_START_FAIL,new byte[]{86,195,0,0,2,0,0,0});n.RequestTaskStart(50006,0);Check(s.Count==2,"A server start rejection must release the retry guard.");});
            results.Add("PASS: live prerequisite item updates; duplicate/pending start guard and progress preservation until ACK.");
            var award=Task(50007,5);award.m_iSUReward=1;award.m_iCSUEnemyID=new[]{55,0,0};award.m_iCSUNumToKill=new[]{1,0,0};
            var later=Task(50008,1);later.m_iHMissionID=50007;later.m_iSUReward=2;
            Scenario(new[]{award,later},(w,n,s)=>{
                Call(n,"TaskStop",50008);Call(n,"InsertActive",Call(n,"ResetNode",50007,award));w.Missions.OnTaskStart(50007);
                GameMenu.CloseWindow();GameMenu.SetOpen(false,false);
                var oldJournal=JournalWindow.Instance;bool wasRegistered=GameMenu.Implemented.Contains(GameMenu.Win.Journal);
                var j=JournalWindow.Ensure(w.gameObject);Call(j,"Awake");typeof(GameMenu).GetField("<Slide>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,0f);var mt=new MissionTableScript{m_pMissionData=new[]{award,later},m_pMissionStringData=original.m_pMissionTable.m_pMissionStringData,m_pRewardData=original.m_pMissionTable.m_pRewardData};
                j.Init(null,n,w,new XdtTableScript{m_pMissionTable=mt,m_pNpcTable=original.m_pNpcTable});
                try{Kill(n,55);Evaluate(n,Vector3.zero);Check(s.Count==0&&GameMenu.Current==GameMenu.Win.Journal,"Rewards must open the existing chooser, never be auto-selected/claimed.");Call(j,"OnOpen");Check(Field<RewardElement>(j,"reward")==original.m_pMissionTable.m_pRewardData[1],"Intermediate reward mode must show the current task reward, not the final task reward.");}
                finally{GameMenu.CloseWindow();UnityEngine.Object.DestroyImmediate(j);JournalWindow.Instance=oldJournal;if(!wasRegistered)GameMenu.Implemented.Remove(GameMenu.Win.Journal);}
            });results.Add("PASS: completion reward chooser; no automatic claim; current-task rather than final-task reward data.");
            int destinations=all.Count(m=>(m.m_iHTaskType==2||m.m_iHTaskType==6)&&m.m_iHTerminatorNPCID>0);
            foreach(var m in all.Where(m=>(m.m_iHTaskType==2||m.m_iHTaskType==6)&&m.m_iHTerminatorNPCID>0))Check(original.m_pNpcTable.m_pNpcData.Any(n=>n!=null&&n.m_iNpcNumber==m.m_iHTerminatorNPCID&&n.m_iSightRange>0),"Destination table/radius missing for task "+m.m_iHTaskID);
            results.Add("PASS: "+all.Length+" live task rows audited; "+destinations+" destination/escort rows have a resolvable NPC and positive table radius.");
            File.WriteAllLines("Logs/mission-audit/regression-results.txt",results);foreach(var line in results)Debug.Log("[mission audit] "+line);
        }
    }
}
