using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using OpenFusion.Net;
using OpenFusion.Tables;
using OpenFusion.World;
using UnityEditor;
using UnityEngine;
namespace OpenFusion.EditorTools
{
    public static class TutorialTrainingRegressionChecks
    {
        static void Check(bool test,string message){if(!test)throw new Exception(message);}
        public static void Batch(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Run()
        {
            Check(TutorialTraining.IllustrationFor(TutorialTraining.Lesson.Forward,2)=="tut_move_w"&&TutorialTraining.IllustrationFor(TutorialTraining.Lesson.Backward,0)=="tut_move_s"&&TutorialTraining.IllustrationFor(TutorialTraining.Lesson.Jump,0)=="tut_jump"&&TutorialTraining.IllustrationFor(TutorialTraining.Lesson.Power,0)=="tut_rmouse","Legacy keyboard/mouse art must follow the actual lesson");
            Check(TutorialTraining.DirectionFor(TutorialTraining.Lesson.LookRight)=="tut_right"&&TutorialTraining.DirectionFor(TutorialTraining.Lesson.LookUp)=="tut_up","Camera direction art must match instruction");
            Exception failure=null;var guarded=TutorialAssets.Guard(OuterFailure(),e=>failure=e);while(guarded.MoveNext()){}Check(failure is InvalidOperationException,"Nested coroutine failure must reach the awaited startup error boundary");
            var real=new PcAppearance{Uid=1234,FirstName="Test",LastName="Hero",Level=9,Hp=999,MapNum=9,FusionMatter=12345,Style=new PlayerStyle{Gender=1,FaceStyle=2,HairStyle=3},Equip=new ItemBase[9]};
            real.Equip[0]=new ItemBase{Type=0,Id=123,Opt=456};real.Equip[1]=new ItemBase{Type=1,Id=7,Opt=1};
            var p=TutorialTraining.InitialPacket(real,400);var body=new byte[p.Length-4];Buffer.BlockCopy(p,4,body,0,body.Length);
            var enter=WorldPackets.ParseEnterSucc(body);var hud=new HudData();HudPackets.ApplyEnterSucc(body,hud);
            Check(enter.Id==1&&enter.Self.Uid==1234&&enter.Self.FirstName=="Test"&&enter.Self.LastName=="Hero","Practice identity must preserve the selected character");
            Check(enter.Self.Level==1&&enter.Self.Hp==400&&enter.Self.FusionMatter==0&&enter.Self.Equip[0].Id==0&&enter.Self.Equip[1].Id==7,"Practice stats and weapon must be independent of saved character");
            Check(real.Level==9&&real.Hp==999&&real.FusionMatter==12345&&real.Equip[0].Id==123,"Practice entry must not mutate the saved character");
            Check(enter.Self.X==54700&&enter.Self.Y==65500&&enter.Self.Z==-10540&&hud.ActiveNanoSlot==-1,"Legacy Tech Square starting coordinates and empty Nano state");
            Check(enter.Quest.Inven.All(i=>i.Id==0)&&enter.Quest.Running.All(i=>i==0)&&hud.NanoSlots.All(i=>i==0),"Practice must not import online inventory/quests/Nanos");
            var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ffcache/238bd38f-3add-4dc5-a29a-06213461a206"));
            if(Directory.Exists(root)){
                var source=new FfCacheBundleSource(root);var tables=TableLoader.Load(source.Load("TableData.resourceFile"));
                Directory.CreateDirectory("Logs/tutorial-audit");
                var missions=tables.Xdt.m_pMissionTable.m_pMissionData.Where(m=>m!=null&&(m.m_iHTaskID>=2240&&m.m_iHTaskID<=2260||new[]{2671,2672,2673}.Contains(m.m_iHNPCID))).ToArray();
                File.WriteAllLines("Logs/tutorial-audit/mission-data.txt",missions.Select(m=>JsonUtility.ToJson(m)));
                Check(tables.Xdt.m_pWeaponItemTable.m_pItemData[328]!=null,"Original practice weapon 328 must exist");
                Check(tables.Xdt.m_pSkillTable.m_pSkillData[1]!=null,"Original Buttercup tutorial power must exist");
                File.WriteAllText("Logs/tutorial-audit/skill-data.txt",JsonUtility.ToJson(tables.Xdt.m_pSkillTable.m_pSkillData[1]));
                File.WriteAllLines("Logs/tutorial-audit/npc-data.txt",tables.Xdt.m_pNpcTable.m_pNpcData.Where(n=>n!=null&&new[]{2669,2670,2671,2672,2673,2674,2675,2676,2677,2678,2694,2695,2696,2800}.Contains(n.m_iNpcNumber)).Select(n=>JsonUtility.ToJson(n)));
                CheckFlow(tables.Xdt);
                var exit=TutorialTraining.WarpDestination(tables.Xdt,2696);
                Check(Vector3.Distance(exit,new Vector3(907,11,685))>28,"Original lair exit must preserve the bridge traversal instead of teleporting beside Numbuh Two");
            }
            Debug.Log("[training regression] Legacy start, identity, isolated inventory/quests/stats/Nanos, and original weapon/power data passed.");
        }
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static System.Collections.IEnumerator InnerFailure(){yield return null;throw new InvalidOperationException("Expected tutorial error");}
        static System.Collections.IEnumerator OuterFailure(){yield return InnerFailure();}
        static void Field(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        static void CheckFlow(OpenFusion.Tables.Gen.XdtTableScript tables)
        {
            var host=new GameObject("Offline training flow check");var player=new GameObject("Practice player");
            var spawned=new List<GameObject>();var oldSender=NpcUI.Sender;var oldClient=GameClient.FindSelf;
            try{
                var world=host.AddComponent<WorldBootstrap>();world.Player=player.transform;
                var entities=host.AddComponent<WorldEntities>();typeof(WorldBootstrap).GetProperty("Entities").SetValue(world,entities);
                typeof(WorldEntities).GetProperty("Self").SetValue(entities,player);
                var npcMap=(Dictionary<int,GameObject>)typeof(WorldEntities).GetField("npcs",Private).GetValue(entities);
                var hud=host.AddComponent<PlayerHud>();hud.Data.Hp=400;
                var dispatcher=new PacketDispatcher();var emitted=new List<uint>();byte[] damageResult=null,skillResult=null,rewardResult=null,moveResult=null,tickResult=null;
                dispatcher.Raw+=(type,b)=>{emitted.Add(type);if(type==PacketIds.P_FE2CL_PC_ATTACK_NPCs_SUCC)damageResult=b;if(type==PacketIds.P_FE2CL_NANO_SKILL_USE_SUCC)skillResult=b;if(type==PacketIds.P_FE2CL_REP_REWARD_ITEM)rewardResult=b;if(type==PacketIds.P_FE2CL_PC_ITEM_MOVE_SUCC)moveResult=b;if(type==PacketIds.P_FE2CL_REP_PC_TICK)tickResult=b;};
                dispatcher.NpcEnter+=npc=>{var go=new GameObject("Practice NPC "+npc.Id);spawned.Add(go);go.transform.position=PlayerAvatarFactory.ToClient(npc.X,npc.Y,npc.Z);var info=go.AddComponent<NpcInfo>();info.Id=npc.Id;info.NpcNumber=npc.Type;info.Hp=npc.Hp;var row=tables.m_pNpcTable.m_pNpcData.First(n=>n!=null&&n.m_iNpcNumber==npc.Type);info.Type=row.m_iNpcType;info.Team=row.m_iTeam;info.TableHeight=row.m_iHeight*.01f;npcMap[npc.Id]=go;};
                var training=host.AddComponent<TutorialTraining>();
                typeof(TutorialTraining).GetProperty("Instance").SetValue(null,training);
                Field(training,"running",true);Field(training,"world",world);Field(training,"tables",tables);Field(training,"dispatcher",dispatcher);
                void Lesson(TutorialTraining.Lesson lesson)=>typeof(TutorialTraining).GetProperty("Current").SetValue(training,lesson);
                void Offer(int id)=>Field(training,"offered",tables.m_pMissionTable.m_pMissionData.First(m=>m!=null&&m.m_iHTaskID==id));
                void Request(uint type,int task){var request=ClientPackets.New(type,16);ClientPackets.PutInt(request,4,task);training.Handle(request);}
                Lesson(TutorialTraining.Lesson.LookRight);Check(!TutorialTraining.MovementAllowed&&!TutorialTraining.ActionAllowed(ControlMap.K.Fire1),"Camera lesson must gate locomotion and combat");
                foreach(TutorialTraining.Lesson lesson in Enum.GetValues(typeof(TutorialTraining.Lesson))){
                    Lesson(lesson);
                    Check(!TutorialTraining.WindowAllowed(GameMenu.Win.Map)&&!TutorialTraining.ActionAllowed(ControlMap.K.WorldMap)&&!GameMenu.OpenWindow(GameMenu.Win.Map),"World-map hotkeys and direct HUD/menu calls must both stay blocked throughout training");
                    Check(!TutorialTraining.WindowAllowed(GameMenu.Win.MyStuff)&&!TutorialTraining.WindowAllowed(GameMenu.Win.EMail)&&!TutorialTraining.ChatAllowed&&!DualScreenController.SecondaryCommandAllowed("tab"),"Inventory/mail/chat and stale companion commands must not bypass tutorial gates");
                }
                Check(!TutorialTraining.WindowAllowed(TutorialTraining.Lesson.Target,GameMenu.Win.Journal)&&TutorialTraining.WindowAllowed(TutorialTraining.Lesson.AcceptMission,GameMenu.Win.Journal)&&TutorialTraining.WindowAllowed(TutorialTraining.Lesson.Journal,GameMenu.Win.Journal),"Mission offer and journal lessons must retain their real mission UI");
                Check(TutorialTraining.WindowAllowed(TutorialTraining.Lesson.Target,GameMenu.Win.Settings)&&TutorialTraining.WindowAllowed(TutorialTraining.Lesson.Target,GameMenu.Win.Quit),"Settings and leaving the game must remain reachable");
                Check(!TutorialTraining.EnemyCanAttack(1001,TutorialTraining.Lesson.ThreeEnemies)&&TutorialTraining.EnemyCanAttack(1005,TutorialTraining.Lesson.Boss)&&!TutorialTraining.EnemyCanAttack(5000,TutorialTraining.Lesson.Escape),"Initial targets stay passive, the boss retaliates and the final stun ends retaliation");
                Field(training,"practiceHp",100);Field(training,"practiceMaxHp",400);Field(training,"nextRecovery",5f);Field(training,"combatUntil",10f);
                hud.Data.NanoBankStamina=new short[NanoWheelHud.NanoBankSize];
                var wheel=host.AddComponent<NanoWheelHud>();wheel.Init(hud,dispatcher,tables);
                var wheelQueue=(System.Collections.Concurrent.ConcurrentQueue<Action>)typeof(NanoWheelHud).GetField("q",Private).GetValue(wheel);
                void FlushWheel(){while(wheelQueue.TryDequeue(out var action))action();}
                typeof(TutorialTraining).GetMethod("CreateNano",Private).Invoke(training,null);FlushWheel();
                Check(wheel.BankSkill(1)==1&&hud.Data.NanoSlots[0]==1,"The actual Nano bank consumer must receive Buttercup's starting power");
                var recover=typeof(TutorialTraining).GetMethod("RecoverAt",Private);
                recover.Invoke(training,new object[]{6f});Check(tickResult==null,"Health must not regenerate during combat");
                hud.Data.NanoSlots[0]=1;hud.Data.BatteryNano=17;
                recover.Invoke(training,new object[]{10f});var recovered=new HudData();Check(HudPackets.ApplyTick(tickResult,recovered)&&recovered.Hp==300&&recovered.NanoStamina[0]==HudData.NanoStaminaMax&&recovered.BatteryNano==17,"Recovery must use the shared tick format and preserve potion count");
                int ticks=emitted.Count(t=>t==PacketIds.P_FE2CL_REP_PC_TICK);recover.Invoke(training,new object[]{14f});Check(emitted.Count(t=>t==PacketIds.P_FE2CL_REP_PC_TICK)==ticks,"Recovery must wait five seconds between ticks");
                recover.Invoke(training,new object[]{15f});HudPackets.ApplyTick(tickResult,recovered);Check(recovered.Hp==400,"Recovery must clamp to the practice character's maximum HP");hud.Data.NanoSlots[0]=0;
                FlushWheel();Check(wheel.BankSkill(1)==1,"Repeated health/stamina ticks must retain Buttercup's assigned power in the actual Nano bank");
                var messages=NanoMessages.Ensure(host);messages.InitGuide(dispatcher,tables);NanoMessages.Tutorial("Computress","First lesson",1171,10);NanoMessages.Tutorial("Computress","Current lesson",1171,10);
                var notices=(System.Collections.IList)typeof(NanoMessages).GetField("list",Private).GetValue(messages);Check(notices.Count==1,"A new tutorial reminder must replace stale instructions");
                var notice=notices[0];string portrait=(string)notice.GetType().GetField("Icon").GetValue(notice);
                Check(!string.IsNullOrEmpty(portrait)&&Resources.Load<Texture2D>("OpenFusion/Portraits/"+portrait)!=null,"Computress tutorial notification must resolve its real original portrait");
                Lesson(TutorialTraining.Lesson.AcceptMission);Offer(2248);Request(PacketIds.P_CL2FE_REQ_PC_TASK_START,2248);
                Check(training.Current==TutorialTraining.Lesson.Transmitter&&npcMap.ContainsKey(1100),"Real mission acceptance must spawn the practice objective");
                player.transform.position=npcMap[1100].transform.position;
                for(int i=0;i<26;i++)training.Handle(ClientPackets.AttackNpcs(new[]{1100}));
                var hits=AttackResults.Read(damageResult,4,8);Check(hits.Count==1&&hits[0].Hp==0&&hits[0].IsNpc,"Practice kills must use normal combat result layout for wounds/death/damage numbers");
                Request(PacketIds.P_CL2FE_REQ_PC_TASK_END,2249);Check(training.Current==TutorialTraining.Lesson.Transmitter,"Out-of-order mission completion must be rejected");
                Lesson(TutorialTraining.Lesson.CompleteMission);Field(training,"lastStartedTask",2249);Request(PacketIds.P_CL2FE_REQ_PC_TASK_END,2249);
                Check(training.Current==TutorialTraining.Lesson.Buttercup&&npcMap.ContainsKey(1007),"Mission hand-in must lead to Buttercup");
                var granted=new HudData();Check(HudPackets.ApplyRewardItem(rewardResult,granted)&&granted.FusionMatter==tables.m_pMissionTable.m_pRewardData.First(r=>r!=null&&r.m_iMissionRewardID==531).m_iFusionMatter,"Mission hand-in must emit the table's real Fusion Matter reward through the existing HUD parser");
                int rewardCount=emitted.Count(t=>t==PacketIds.P_FE2CL_REP_REWARD_ITEM);Lesson(TutorialTraining.Lesson.CompleteMission);Request(PacketIds.P_CL2FE_REQ_PC_TASK_END,2249);Check(emitted.Count(t=>t==PacketIds.P_FE2CL_REP_REWARD_ITEM)==rewardCount,"Repeated mission completion must not duplicate practice rewards");
                Lesson(TutorialTraining.Lesson.AcceptButtercup);Request(PacketIds.P_CL2FE_REQ_PC_TASK_END,2250);
                Check(training.Current==TutorialTraining.Lesson.Dexbot&&npcMap.ContainsKey(1009),"Buttercup mission must lead to Dexbot");
                Lesson(TutorialTraining.Lesson.AcceptDexter);Request(PacketIds.P_CL2FE_REQ_PC_TASK_END,2253);
                Check(training.Current==TutorialTraining.Lesson.FusionButtercup&&npcMap.ContainsKey(4000),"Dexter hand-in must spawn the real Fusion Buttercup objective");
                typeof(TutorialTraining).GetMethod("Spawn",Private).Invoke(training,new object[]{5000,2677,new Vector3(907,17,710),true,500});player.transform.position=new Vector3(907,17,708);
                Lesson(TutorialTraining.Lesson.Power);hud.Data.ActiveNanoSlot=0;
                hud.Data.NanoSlots[0]=1;hud.Data.NanoStamina[0]=HudData.NanoStaminaMax;
                int staminaBefore=(int)typeof(TutorialTraining).GetField("practiceNanoStamina",Private).GetValue(training);
                var nanoSystem=host.AddComponent<NanoSystem>();nanoSystem.Init(entities,dispatcher,tables,hud,wheel,null,null,null);
                var previousTarget=NpcUI.MobTarget;NpcUI.MobTarget=npcMap[5000].GetComponent<NpcInfo>();player.transform.rotation=Quaternion.identity;
                byte[] skillRequest=null;NpcUI.Sender=request=>{skillRequest=request;training.Handle(request);};
                try{nanoSystem.UseSkill();}finally{NpcUI.MobTarget=previousTarget;}
                Check(skillRequest!=null&&PacketIds.Unstamp(BitConverter.ToUInt32(skillRequest,0))==PacketIds.P_CL2FE_REQ_NANO_SKILL_USE,"Actual NanoPower action must send its request after recovery, not silently return with skill zero");
                Check(skillResult!=null&&BitConverter.ToInt32(skillResult,28)==tables.m_pSkillTable.m_pSkillData[1].m_iSkillType&&SkillResults.Stride(BitConverter.ToInt32(skillResult,28))==32,"Buttercup power must use the actual skill type and packed result stride");
                Check(training.Current==TutorialTraining.Lesson.Escape,"Actual targeted Nano skill must unlock the escape lesson");
                Check(BitConverter.ToInt32(skillResult,64)==512&&BitConverter.ToInt16(skillResult,26)<staminaBefore,"Stun must carry its actual condition flag and consume Nano energy");
                typeof(TutorialTraining).GetMethod("EquipWeapon",Private).Invoke(training,new object[]{328});
                training.Handle(ClientPackets.ItemMove(0,0,0,7));Check(moveResult!=null&&BitConverter.ToInt16(moveResult,10)==0&&BitConverter.ToInt16(moveResult,30)==328,"Weapon swap must acknowledge the post-swap equipment contents");
                training.Handle(ClientPackets.ItemMove(0,7,0,0));Check(BitConverter.ToInt16(moveResult,10)==0&&BitConverter.ToInt16(moveResult,30)==328,"Weapon swap back must restore the practice weapon through the normal item response");
                int count=emitted.Count;training.Handle(ClientPackets.SaveCharTutor(1234,1));training.Handle(ClientPackets.New(0x1300ffffu,0));
                Check(emitted.Count==count,"Unsupported requests and tutorial-save requests must be swallowed by the offline sink");
            }finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(player);foreach(var go in spawned)UnityEngine.Object.DestroyImmediate(go);NpcUI.Sender=oldSender;GameClient.FindSelf=oldClient;GameMenu.CloseWindow();}
        }
    }
}
