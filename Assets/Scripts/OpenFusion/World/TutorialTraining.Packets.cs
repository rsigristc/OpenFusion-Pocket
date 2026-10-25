using System;
using System.Collections.Generic;
using System.Linq;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed partial class TutorialTraining
    {
        static void I(byte[] p,int offset,int value)=>BitConverter.GetBytes(value).CopyTo(p,offset);
        static void S(byte[] p,int offset,int value)=>BitConverter.GetBytes((short)value).CopyTo(p,offset);
        public static byte[] InitialPacket(PcAppearance real,int maxHp)
        {
            var p=ClientPackets.New(PacketIds.P_FE2CL_REP_PC_ENTER_SUCC,WorldPackets.EnterSuccSize);
            I(p,4,1);const int load=8,style=load+4;
            BitConverter.GetBytes(real.Uid).CopyTo(p,style);p[style+8]=real.NameChecked?(byte)1:(byte)0;
            ClientPackets.PutStr(p,style+10,real.FirstName??"",9);ClientPackets.PutStr(p,style+28,real.LastName??"",17);
            var st=real.Style;p[style+62]=(byte)st.Gender;p[style+63]=(byte)st.FaceStyle;p[style+64]=(byte)st.HairStyle;p[style+65]=(byte)st.HairColor;p[style+66]=(byte)st.SkinColor;p[style+67]=(byte)st.EyeColor;p[style+68]=(byte)st.Height;p[style+69]=(byte)st.Body;I(p,style+72,real.Class);
            S(p,load+84,1);S(p,load+86,5);I(p,load+92,maxHp);
            I(p,load+120,54700);I(p,load+124,65500);I(p,load+128,-10540);I(p,load+132,270);
            for(int slot=0;slot<9;slot++)if(slot!=0&&slot!=7&&slot!=8&&real.Equip!=null&&slot<real.Equip.Length)Item(p,load+136+slot*12,real.Equip[slot]);
            S(p,load+1672,-1);
            // First-use popups are superseded by explicit guided lessons.
            BitConverter.GetBytes(-1L).CopyTo(p,load+2536);BitConverter.GetBytes(-1L).CopyTo(p,load+2544);
            return p;
        }
        static void Item(byte[] p,int offset,ItemBase item){S(p,offset,item.Type);S(p,offset+2,item.Id);I(p,offset+4,item.Opt);I(p,offset+8,item.TimeLimit);}
        void Emit(uint type,int size,Action<byte[]> fill=null){var p=ClientPackets.New(type,size);fill?.Invoke(p);dispatcher.Dispatch(p);}
        void Spawn(int id,int type,Vector3 pos,bool enemy=false,int hp=200)
        {
            var row=tables.m_pNpcTable?.m_pNpcData?.FirstOrDefault(n=>n!=null&&n.m_iNpcNumber==type);if(row!=null)hp=row.m_iHP;
            if(enemy){enemyHp[id]=hp;dead.Remove(id);}
            Emit(PacketIds.P_FE2CL_NPC_ENTER,36,p=>{I(p,4,id);I(p,8,type);I(p,12,hp);I(p,20,Mathf.RoundToInt(pos.x*100));I(p,24,Mathf.RoundToInt(pos.z*100));I(p,28,Mathf.RoundToInt(pos.y*100));I(p,32,270);});
        }
        void Remove(int id){enemyHp.Remove(id);Emit(PacketIds.P_FE2CL_NPC_EXIT,4,p=>I(p,4,id));}
        void MoveNpc(int id,Vector3 position,int speed)=>Emit(PacketIds.P_FE2CL_NPC_MOVE,24,p=>{I(p,4,id);I(p,8,Mathf.RoundToInt(position.x*100));I(p,12,Mathf.RoundToInt(position.z*100));I(p,16,Mathf.RoundToInt(position.y*100));I(p,20,speed);S(p,24,1);});
        readonly ItemBase[] practiceEquip=new ItemBase[9],practiceInventory=new ItemBase[50];
        int practiceFusion,practiceTaros,practiceNanoStamina=100,practiceHp,practiceMaxHp;
        public void SetSnapshot(EnterSucc snapshot){Array.Copy(snapshot.Self.Equip,practiceEquip,9);practiceFusion=snapshot.Self.FusionMatter;practiceHp=practiceMaxHp=snapshot.Self.Hp;}
        // cnVirtualServer.Update: +200 HP every five seconds outside combat.
        // Emit the same tick consumed by the HUD; never change the online save.
        void TickRecovery() => RecoverAt(Time.time);
        void RecoverAt(float now)
        {
            if(now<nextRecovery||now<combatUntil||client!=null&&now<client.CombatUntil)return;
            nextRecovery=now+5;
            if(practiceHp<=0||practiceMaxHp<=0)return;
            int previous=practiceHp;practiceHp=Mathf.Min(practiceMaxHp,practiceHp+200);
            var hud=GetComponent<PlayerHud>();if(hud==null)return;
            if(hud.Data.NanoSlots[0]==1)practiceNanoStamina=HudData.NanoStaminaMax;
            Emit(PacketIds.P_FE2CL_REP_PC_TICK,32,p=>{I(p,4,practiceHp);for(int slot=0;slot<3;slot++){int id=hud.Data.NanoSlots[slot],o=8+slot*6;S(p,o,id);S(p,o+2,id==1?1:0);S(p,o+4,id==1?practiceNanoStamina:hud.Data.NanoStamina[slot]);}I(p,28,hud.Data.BatteryNano);});
            if(practiceHp!=previous)Debug.Log("[training recovery] hp="+previous+"->"+practiceHp+"/"+practiceMaxHp+"; local practice only");
        }
        void EquipWeapon(int id){practiceEquip[0]=new ItemBase{Id=(short)id,Type=0,Opt=1};Emit(PacketIds.P_FE2CL_PC_EQUIP_CHANGE,20,p=>{I(p,4,1);I(p,8,0);Item(p,12,practiceEquip[0]);});}
        void TaskStart(int task){lastStartedTask=task;Emit(PacketIds.P_FE2CL_REP_PC_TASK_START_SUCC,8,p=>I(p,4,task));}
        void TaskEnd(int task){if(!completedTasks.Add(task))return;lastEndedTask=task;Emit(PacketIds.P_FE2CL_REP_PC_TASK_END_SUCC,4,p=>I(p,4,task));int next=Task(task)?.m_iSUOutgoingTask??0;if(next>0)TaskStart(next);}
        void GiveQuestItem(int id,int count)=>Emit(PacketIds.P_FE2CL_REP_PC_GIVE_ITEM_SUCC,20,p=>{I(p,4,2);I(p,8,0);Item(p,12,new ItemBase{Type=8,Id=(short)id,Opt=count});});
        void GrantMissionReward(int task,int choice1,int choice2)
        {
            if(completedTasks.Contains(task))return;
            var mission=Task(task);var reward=tables.m_pMissionTable?.m_pRewardData?.FirstOrDefault(r=>r!=null&&r.m_iMissionRewardID==mission?.m_iSUReward);
            if(reward==null)return;
            practiceFusion+=reward.m_iFusionMatter;practiceTaros+=reward.m_iCash;
            var received=new List<(ItemBase item,int slot)>();
            void Add(int[] ids,int[] types,int choice,int allowed){if(ids==null||types==null)return;for(int i=0;i<ids.Length&&i<types.Length;i++){if(ids[i]<=0||allowed>0&&(choice&(1<<i))==0)continue;int slot=Array.FindIndex(practiceInventory,item=>item.Id<=0);if(slot<0)break;var item=new ItemBase{Type=(short)types[i],Id=(short)ids[i],Opt=1};practiceInventory[slot]=item;received.Add((item,slot));}}
            Add(reward.m_iMissionRewardItemID,reward.m_iMissionRewarItemType,choice1,reward.m_iBox1Choice);Add(reward.m_iMissionRewardItemID2,reward.m_iMissionRewardItemType2,choice2,reward.m_iBox2Choice);
            Emit(PacketIds.P_FE2CL_REP_REWARD_ITEM,36+20*received.Count,p=>{I(p,4,practiceTaros);I(p,8,practiceFusion);p[20]=(byte)received.Count;for(int i=0;i<received.Count;i++){int o=40+i*20;Item(p,o,received[i].item);I(p,o+12,1);I(p,o+16,received[i].slot);}});
            Debug.Log("[training reward] task="+task+" fusion="+practiceFusion+" taros="+practiceTaros+" items="+received.Count+"; local practice only");
        }
        void CreateNano()
        {
            practiceNanoStamina=100;practiceFusion=0;
            Emit(PacketIds.P_FE2CL_REP_PC_NANO_CREATE_SUCC,28,p=>{I(p,8,-1);S(p,24,1);S(p,26,1);S(p,28,practiceNanoStamina);S(p,30,1);});
            Emit(PacketIds.P_FE2CL_REP_NANO_EQUIP_SUCC,8,p=>{S(p,4,1);S(p,6,0);});
        }
        public void Handle(byte[] p)
        {
            // Sole outbound sink during practice. Unknown requests are deliberately
            // consumed too; a new feature cannot accidentally send to a live shard.
            if(!running||p==null||p.Length<4)return;
            if(OverlayBlocking||Current==Lesson.Finished)return;
            uint type=PacketIds.Unstamp(BitConverter.ToUInt32(p,0));
            if(type==PacketIds.P_CL2FE_REQ_PC_JUMP){jumped=true;return;}
            if(type==PacketIds.P_CL2FE_REQ_PC_ATTACK_NPCs&&Current>=Lesson.Target&&p.Length>=8)
            {
                int count=BitConverter.ToInt32(p,4);if(count<0||count>(p.Length-8)/4)return;
                var targets=new HashSet<int>();for(int n=0;n<count;n++){int id=BitConverter.ToInt32(p,8+n*4);var go=world.Entities.NpcById(id);if(go!=null&&enemyHp.TryGetValue(id,out int hp)&&hp>0&&Near(go.transform.position,40))targets.Add(id);}
                if(targets.Count>0)Damage(targets,50);return;
            }
            if(type==PacketIds.P_CL2FE_REQ_NANO_ACTIVE&&p.Length>=6)
            {
                short slot=BitConverter.ToInt16(p,4);if(Current>=Lesson.Summon&&(slot==0||slot==-1)){Emit(PacketIds.P_FE2CL_REP_NANO_ACTIVE_SUCC,8,b=>S(b,4,slot));if(slot==0&&world.Entities.Self!=null)BulletFx.Instance?.Spawn(668,world.Player.position,world.Player.rotation,1,null,3);}return;
            }
            if(type==PacketIds.P_CL2FE_REQ_NANO_SKILL_USE&&p.Length>=24&&Current==Lesson.Power)
            {
                int count=BitConverter.ToInt32(p,20);if(count<1||count>(p.Length-24)/4||GetComponent<PlayerHud>().Data.ActiveNanoSlot!=0)return;
                int id=BitConverter.ToInt32(p,24);if(!enemyHp.TryGetValue(id,out int hp)||hp<=0||!NearNpc(id,30))return;
                var skill=tables.m_pSkillTable.m_pSkillData[1];int drain=skill.m_iBatteryDrainUse!=null&&skill.m_iBatteryDrainUse.Length>0?skill.m_iBatteryDrainUse[0]:26;
                practiceNanoStamina=Mathf.Max(0,practiceNanoStamina-drain);stunnedUntil[id]=Time.time+(skill.m_iDurationTime?[0]??30)*.1f;
                // Buttercup's original tutorial skill 1: stun (eST 8), normal Nano
                // animation/projectile/result distribution, with the actual NPC HP.
                Emit(PacketIds.P_FE2CL_NANO_SKILL_USE_SUCC,68,b=>{I(b,4,1);b[8]=255;S(b,10,1);S(b,28,1);S(b,30,practiceNanoStamina);I(b,32,skill.m_iSkillType);I(b,36,1);I(b,40,2);I(b,44,id);I(b,56,hp);S(b,60,practiceNanoStamina);I(b,68,512);});
                var target=world.Entities.NpcById(id);target?.GetComponent<RemoteMover>()?.OnNpcMove(target.transform.position,0);
                target?.GetComponent<NpcAnimator>()?.Wound();
                if(target!=null)BulletFx.Instance?.Spawn(366,target.transform.position+Vector3.up*3,Quaternion.identity,2,target.transform,Mathf.Max(.1f,stunnedUntil[id]-Time.time));
                powerUsed=true;Set(Lesson.Escape,"Well done! Talk to Numbuh Two to escape.",91,"Computress_Tut62",new Vector3(907,11,685));return;
            }
            if(type==PacketIds.P_CL2FE_REQ_PC_TASK_START&&p.Length>=8)
            {
                int task=BitConverter.ToInt32(p,4);
                if(offered==null||task!=offered.m_iHTaskID)return;
                if(Current!=Lesson.AcceptMission)return;
                TaskStart(task);
                Spawn(1100,2676,new Vector3(697,-82,794),true,250);Set(Lesson.Transmitter,"Find and defeat the Transmitter Critter for your mission.",49,"Computress_Tut29",new Vector3(697,-82,794));
                return;
            }
            if(type==PacketIds.P_CL2FE_REQ_PC_TASK_END&&p.Length>=8)
            {
                int task=BitConverter.ToInt32(p,4);if(completedTasks.Contains(task))return;
                if(task==2249&&Current==Lesson.CompleteMission){GrantMissionReward(task,p.Length>12?p[12]:0,p.Length>13?p[13]:0);TaskEnd(task);TaskStart(2250);Spawn(1007,2672,new Vector3(624,-82,772));Set(Lesson.Buttercup,"Talk to Buttercup to investigate the Infected Zone.",44,"Computress_Tut41",new Vector3(624,-82,772));}
                else if(task==2250&&Current==Lesson.AcceptButtercup){TaskEnd(task);Spawn(1009,2694,new Vector3(596,-82,784));Set(Lesson.Dexbot,"Use the Dexbot warp gate to enter the Infected Zone.",79,"Computress_Tut47",new Vector3(596,-82,784));}
                else if(task==2253&&Current==Lesson.AcceptDexter){TaskEnd(task);Spawn(4000,2678,new Vector3(563,-133,967),true,800);Set(Lesson.FusionButtercup,"Find and defeat Fusion Buttercup.",65,"Dexter_Tut09",new Vector3(563,-133,967));}
                return;
            }
            if(type==PacketIds.P_CL2FE_REQ_ITEM_MOVE&&p.Length>=20&&Current>=Lesson.Target)
            {
                int from=BitConverter.ToInt32(p,4),fs=BitConverter.ToInt32(p,8),to=BitConverter.ToInt32(p,12),ts=BitConverter.ToInt32(p,16);
                var a=from==0?practiceEquip:from==1?practiceInventory:null;var b=to==0?practiceEquip:to==1?practiceInventory:null;
                if(a==null||b==null||fs<0||ts<0||fs>=a.Length||ts>=b.Length)return;
                var swap=a[fs];a[fs]=b[ts];b[ts]=swap;
                Emit(PacketIds.P_FE2CL_PC_ITEM_MOVE_SUCC,40,result=>{I(result,4,from);I(result,8,fs);Item(result,12,a[fs]);I(result,24,to);I(result,28,ts);Item(result,32,b[ts]);});
                return;
            }
        }
        void Damage(IEnumerable<int> ids,int damage)
        {
            var hitsList=new List<int>();
            foreach(int id in ids)if(enemyHp.TryGetValue(id,out int hp)&&hp>0){enemyHp[id]=id==5000&&!powerUsed?hp:Mathf.Max(0,hp-damage);hitsList.Add(id);hits++;if(enemyHp[id]==0&&dead.Add(id)){kills++;lastKillAt=Time.time;}}
            if(hitsList.Count>0)combatUntil=Time.time+5;
            Emit(PacketIds.P_FE2CL_PC_ATTACK_NPCs_SUCC,8+24*hitsList.Count,p=>{I(p,4,GetComponent<PlayerHud>().Data.BatteryWpn);I(p,8,hitsList.Count);for(int n=0;n<hitsList.Count;n++){int o=12+n*24,id=hitsList[n];I(p,o,2);I(p,o+4,id);I(p,o+8,id==5000&&!powerUsed?1:0);I(p,o+12,id==5000&&!powerUsed?0:damage);I(p,o+16,enemyHp[id]);p[o+20]=1;}});
        }
        // Legacy's three initial targets do not retaliate. Scripted attackers
        // start with the larger monster, then the mission/lair/bridge enemies.
        public static bool EnemyCanAttack(int id,Lesson lesson) => id==1005&&lesson==Lesson.Boss || id==1100&&lesson==Lesson.Transmitter ||
            id==4000&&lesson==Lesson.FusionButtercup || id==5000&&(lesson==Lesson.Summon||lesson==Lesson.Power);
        void TickEnemies()
        {
            if(GameMenu.Blocking)return;
            if(Time.time>=nextEnemyMove){nextEnemyMove=Time.time+.35f;foreach(var enemy in enemyHp){
                if(enemy.Value<=0||!EnemyCanAttack(enemy.Key,Current)||stunnedUntil.TryGetValue(enemy.Key,out float stun)&&Time.time<stun)continue;
                var go=world.Entities.NpcById(enemy.Key);if(go==null)continue;
                var row=tables.m_pNpcTable.m_pNpcData.FirstOrDefault(n=>n!=null&&n.m_iNpcNumber==go.GetComponent<NpcInfo>()?.NpcNumber);if(row==null)continue;
                float distance=Vector3.Distance(go.transform.position,world.Player.position),range=Mathf.Max(1,row.m_iAtkRange*.01f);
                bool visible=CombatSystem.HasLineOfSight(go.transform.position+Vector3.up,world.Player.position+Vector3.up,.4f,go.transform,world.Player);
                if(distance>Mathf.Max(25,row.m_iSightRange*.01f)||distance<=range&&visible)continue;
                var direction=(world.Player.position-go.transform.position).normalized;var next=go.transform.position+direction*Mathf.Min(2,Mathf.Max(0,distance-(visible?range:1)));
                next.y=Ground.Y(next,40);
                MoveNpc(enemy.Key,next,Mathf.Max(100,row.m_iRunSpeed));
            }}
            if(Time.time<nextEnemyAttack)return;nextEnemyAttack=Time.time+2;
            foreach(var entry in enemyHp)if(entry.Value>0&&EnemyCanAttack(entry.Key,Current)){
                var attacker=world.Entities.NpcById(entry.Key);if(attacker==null)continue;
                var info=attacker.GetComponent<NpcInfo>();var row=tables.m_pNpcTable.m_pNpcData.FirstOrDefault(n=>n!=null&&n.m_iNpcNumber==info?.NpcNumber);
                if(row==null||!NearNpc(entry.Key,Mathf.Max(1,row.m_iAtkRange*.01f))||!CombatSystem.HasLineOfSight(attacker.transform.position+Vector3.up,world.Player.position+Vector3.up,.4f,attacker.transform,world.Player))continue;
                if(stunnedUntil.TryGetValue(entry.Key,out float stun)&&Time.time<stun)continue;
                practiceHp=Mathf.Max(100,practiceHp-50);combatUntil=Time.time+5;
                var facing=world.Player.position-attacker.transform.position;facing.y=0;if(facing.sqrMagnitude>.001f)attacker.transform.rotation=Quaternion.LookRotation(facing);
                attacker.GetComponent<RemoteMover>()?.OnNpcMove(attacker.transform.position,0);
                var anim=attacker.GetComponent<NpcAnimator>();if(anim!=null&&!anim.PlayAction(UnityEngine.Random.value<.5f?"melee1":"melee2"))anim.PlayAction("melee1");
                if(row.m_iEffect>0)BulletFx.Instance?.Fire(row.m_iEffect,attacker.transform,world.Player,1.8f,world.Player.position+Vector3.up);
                if(!hurtExplained){hurtExplained=true;subtitle=Text(tables,33);Speak("Computress_Tut18");PresentDialogue("Computress_Tut18",subtitle);}
                Emit(PacketIds.P_FE2CL_NPC_ATTACK_PCs,32,p=>{I(p,4,entry.Key);I(p,8,1);I(p,12,1);I(p,16,1);I(p,24,50);I(p,28,practiceHp);p[32]=1;});break;
            }
        }
    }
}
