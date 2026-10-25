using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;
using UnityEngine;

namespace OpenFusion.World
{
    // Legacy's tutorial is an offline simulation, not a shard instance. All
    // practice packets go through the normal dispatcher, never a network stream.
    public sealed partial class TutorialTraining : MonoBehaviour
    {
        public static TutorialTraining Instance { get; private set; }
        public static bool Active => Instance != null && (Instance.running || Instance.finishing);
        public static bool OverlayBlocking => Active && (Instance.finishing || Instance.confirmSkip || Instance.travelling || !string.IsNullOrEmpty(Instance.failure));
        public static readonly Vector3 StartPosition = new Vector3(547,-105.4f,655);
        public enum Lesson { Arrival, LookRight, LookLeft, LookUp, LookDown, Forward, Backward, Steering, Ledge, Jump,
            Target, Attack, ThreeEnemies, Boss, Minimap, NumbuhTwo, AcceptMission, Transmitter, Journal, ReturnMission,
            CompleteMission, Buttercup, AcceptButtercup, Dexbot, Infection, Dexter, AcceptDexter, FusionButtercup,
            FirstNano, ExitLair, Bridge, Summon, Power, Escape, Finished }
        public Lesson Current { get; private set; }
        public bool AllowsMovement => running && !OverlayBlocking && Current >= Lesson.Forward && Current != Lesson.Finished;
        public static bool MovementAllowed => !Active || Instance.AllowsMovement;
        public static bool CameraAllowed => !Active || !OverlayBlocking && Instance.Current != Lesson.Finished;
        public static bool ActionAllowed(ControlMap.K key)
        {
            if(!Active) return true;
            if(OverlayBlocking || Instance.Current == Lesson.Finished) return false;
            if(key==ControlMap.K.Fire1) return Instance.Current>=Lesson.Target;
            if(key==ControlMap.K.Fire2) return Instance.Current>=Lesson.Power;
            if(key==ControlMap.K.Jump) return Instance.Current>=Lesson.Jump;
            if(key==ControlMap.K.Nano1 || key==ControlMap.K.Nano2 || key==ControlMap.K.Nano3) return Instance.Current>=Lesson.Summon;
            switch(key) {
                case ControlMap.K.WorldMap:case ControlMap.K.Inventory:case ControlMap.K.NanoBook:
                case ControlMap.K.QEmailBox:case ControlMap.K.Contacts:case ControlMap.K.Help:
                case ControlMap.K.VehicleToggle:case ControlMap.K.AutoRun:case ControlMap.K.CombatMode:
                case ControlMap.K.WpnCharge:case ControlMap.K.NanoCharge:return false;
                case ControlMap.K.Journal:case ControlMap.K.Menu:return Instance.Current>=Lesson.Journal;
                default:return true;
            }
        }
        public static bool WindowAllowed(GameMenu.Win window) => !Active || WindowAllowed(Instance.Current,window) && !OverlayBlocking;
        public static bool WindowAllowed(Lesson lesson,GameMenu.Win window) => window==GameMenu.Win.Settings || window==GameMenu.Win.Quit ||
            window==GameMenu.Win.Journal && lesson>=Lesson.AcceptMission && lesson<Lesson.Finished;
        public static bool ChatAllowed => !Active;
        GameClient client; WorldBootstrap world; XdtTableScript tables; PacketDispatcher dispatcher;
        TaskCompletionSource<bool> done; bool running, finishing, confirmSkip, travelling, jumped, powerUsed;
        float since, repeatAt, baselineYaw, baselinePitch, walked, nextEnemyAttack, nextEnemyMove, lastKillAt, nextRecovery, combatUntil;
        Vector3 stepOrigin, previousPosition, marker;
        bool showMarker, hurtExplained; string instruction="", subtitle="", failure="";
        AudioSource voice, ambient; int voiceGeneration, ambientGeneration; string lastVoice; GameObject guideEffect;
        readonly Dictionary<int,int> enemyHp = new Dictionary<int,int>();
        readonly HashSet<int> dead = new HashSet<int>();
        readonly Dictionary<int,float> stunnedUntil = new Dictionary<int,float>();
        readonly HashSet<int> completedTasks = new HashSet<int>();
        int lastStartedTask, lastEndedTask, hits, kills;
        MissionElement offered;
        public Task<bool> Run(GameClient owner, PacketDispatcher packets)
        {
            Instance=this; client=owner;world=owner.Bootstrap;tables=owner.Tables.Xdt;dispatcher=packets;
            done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);running=true;
            voice=gameObject.AddComponent<AudioSource>();voice.spatialBlend=0;
            StartCoroutine(TutorialAssets.Guard(Begin(),e=>failure="Training could not start: "+e.Message));return done.Task;
        }
        IEnumerator Begin()
        {
            float deadline=Time.realtimeSinceStartup+30;
            while(world.Entities?.Self==null){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("The practice character could not be created.");yield return null;}
            yield return null;
            if(MusicController.Instance!=null){MusicController.Instance.ResetAmbient();MusicController.Instance.enabled=false;}
            ambient=gameObject.AddComponent<AudioSource>();ambient.spatialBlend=0;ambient.loop=true;SetAmbient("TutorialMain_wAmbient_Loop");
            previousPosition=world.Player.position;
            nextRecovery=Time.time+5;
            Spawn(100,2669,new Vector3(541,-105,657));Spawn(101,2670,new Vector3(541,-105,653));
            Set(Lesson.Arrival,"Welcome to Tech Square. Computress will guide your training.",1,"Computress_tut01");
            yield return new WaitForSeconds(5);
            Set(Lesson.LookRight,"Move the camera to the RIGHT and find the marker.",8,"Computress_Tut03",StartPosition+new Vector3(0,1,-8));
        }
        static string Text(XdtTableScript t,int line) => t?.m_pCutSceneTable?.m_SceneData?.FirstOrDefault(s=>s!=null&&s.m_iEvent==2)?.m_TextElement?.FirstOrDefault(r=>r!=null&&r.m_iLine==line)?.m_strText ?? "";
        void Set(Lesson lesson,string hint,int line=0,string sound=null,Vector3? destination=null)
        {
            Current=lesson;since=Time.time;repeatAt=Time.time+25;walked=0;jumped=false;
            stepOrigin=world.Player.position; previousPosition=stepOrigin;
            baselineYaw=Camera.main!=null?Camera.main.transform.eulerAngles.y:0;
            baselinePitch=Camera.main!=null?Mathf.DeltaAngle(0,Camera.main.transform.eulerAngles.x):0;
            instruction=hint;subtitle=Text(tables,line);showMarker=destination.HasValue;marker=destination??Vector3.zero;
            var hud=GetComponent<NanocomHud>();if(hud!=null){hud.ViewWayPoint=showMarker;hud.WayPoint=marker;}
            if(guideEffect!=null)Destroy(guideEffect);
            if(destination.HasValue&&BulletFx.Instance!=null)guideEffect=BulletFx.Instance.Spawn(750,marker+Vector3.up,Quaternion.identity,2,null,100000);
            if(sound!=null) Speak(sound);else lastVoice=null;
            PresentDialogue(sound,subtitle);
            Debug.Log("[training] lesson="+lesson+"; offline practice, character save unchanged");
        }
        void Speak(string sound)
        {
            lastVoice=sound;int token=++voiceGeneration;
            Sfx.Instance?.Load("Tut Sound/"+sound+".wav",clip=>{if(!running||token!=voiceGeneration||voice==null||clip==null)return;voice.Stop();voice.clip=clip;voice.volume=Sfx.VoiceVolume;voice.Play();});
        }
        bool Near(Vector3 target,float distance) => (world.Player.position-target).sqrMagnitude<distance*distance;
        bool NearNpc(int id,float distance){var npc=world.Entities.NpcById(id);return npc!=null&&Near(npc.transform.position,distance);}
        void Update()
        {
            if(!running||world.Entities?.Self==null||travelling||confirmSkip)return;
            var position=world.Player.position;
            float delta=Vector3.Distance(previousPosition,position);if(delta<3)walked+=delta;previousPosition=position;
            if(ControlMap.Down(ControlMap.K.Jump))jumped=true;
            if(Time.time>repeatAt){repeatAt=Time.time+25;if(lastVoice!=null){Speak(lastVoice);PresentDialogue(lastVoice,subtitle);}}
            var camera=Camera.main;if(camera==null)return;
            float yaw=Mathf.DeltaAngle(baselineYaw,camera.transform.eulerAngles.y),pitch=Mathf.DeltaAngle(baselinePitch,camera.transform.eulerAngles.x);
            switch(Current)
            {
                case Lesson.LookRight: if(yaw>35)Set(Lesson.LookLeft,"Move the camera to the LEFT.",9,"Computress_Tut04",StartPosition+new Vector3(8,1,0));break;
                case Lesson.LookLeft: if(yaw< -35)Set(Lesson.LookUp,"Look UP using the camera controls.",10,"Computress_Tut05",StartPosition+new Vector3(7,5,0));break;
                case Lesson.LookUp: if(pitch< -15)Set(Lesson.LookDown,"Look DOWN using the camera controls.",11,"Computress_Tut06",StartPosition+new Vector3(4,1,0));break;
                case Lesson.LookDown: if(pitch>15)Set(Lesson.Forward,"Move FORWARD to the marker.",5,"Computress_Tut07",StartPosition+new Vector3(9,1,0));break;
                case Lesson.Forward: if(Near(marker,3)&&walked>4)Set(Lesson.Backward,"Move BACKWARD for five meters.",12,"Computress_Tut09");break;
                case Lesson.Backward: if(ControlMap.Forward< -.1f&&Vector3.Distance(stepOrigin,position)>5)Set(Lesson.Steering,"Move forward while turning the camera.",13,"Computress_Tut10");break;
                case Lesson.Steering: if(walked>5&&Mathf.Abs(yaw)>40)Set(Lesson.Ledge,"Move to the ledge marked ahead.",14,"Computress_Tut11",new Vector3(555.82f,-105,654.2f));break;
                case Lesson.Ledge: if(Near(marker,3))Set(Lesson.Jump,"JUMP onto the ledge. Use Jump while moving forward.",15,"Computress_Tut12",new Vector3(566,-99,665));break;
                case Lesson.Jump: if(jumped&&Near(marker,3)&&Time.time-since>1&&(client==null||!client.IsAirborne))BeginCombat();break;
                case Lesson.Target: if(NpcUI.MobTarget!=null&&enemyHp.ContainsKey(NpcUI.MobTarget.Id))Set(Lesson.Attack,"Hold ATTACK to fire at the target.",30,"Computress_Tut14");break;
                case Lesson.Attack: if(hits>0)Set(Lesson.ThreeEnemies,"Defeat all three monsters. Watch their health bars.",32,"Computress_Tut15");break;
                case Lesson.ThreeEnemies: if(kills>=3){Spawn(1005,2675,new Vector3(567,-97,658),true,600);Set(Lesson.Boss,"Defeat the larger monster.",23,"Ben_Tut06",new Vector3(567,-97,658));}break;
                case Lesson.Boss: if(dead.Contains(1005)&&DeathFinished(1005)){Remove(1005);Spawn(1005,2671,new Vector3(651,-82,739));Set(Lesson.Minimap,"Use the MINIMAP and waypoint to find Numbuh Two.",34,"Computress_Tut19",new Vector3(651,-82,739));}break;
                case Lesson.Minimap: if(NearNpc(1005,25))Set(Lesson.NumbuhTwo,"Talk to Numbuh Two: aim at him and use ATTACK / interact.",45,"Computress_Tut24",new Vector3(651,-82,739));break;
                case Lesson.Transmitter: if(dead.Contains(1100)){GiveQuestItem(537,1);TaskEnd(2248);Set(Lesson.Journal,"Open JOURNAL from the menu to review your mission.",51,"Computress_Tut30");}break;
                case Lesson.Journal: if(GameMenu.Current==GameMenu.Win.Journal&&Time.time-since>3){Set(Lesson.ReturnMission,"Read your objectives, close the Journal and return to Numbuh Two.",57,"Computress_Tut36",new Vector3(651,-82,739));}break;
                case Lesson.FusionButtercup: if(dead.Contains(4000)){TaskEnd(2254);CreateNano();Spawn(4001,2696,new Vector3(558,-133,949));Set(Lesson.FirstNano,"You have received Buttercup! Find the exit transporter.",66,"Dexter_Tut10",new Vector3(558,-133,949));}break;
                case Lesson.FirstNano: if(Time.time-since>5)Set(Lesson.ExitLair,"Use the transporter to leave the Fusion Lair.",68,"Dexter_Tut11",new Vector3(558,-133,949));break;
                case Lesson.Bridge: if(NearNpc(5100,28)){Spawn(5000,2677,new Vector3(907,17,710),true,500);Set(Lesson.Summon,"Summon Buttercup with NANO 1 (or tap her equipped card).",90,"Computress_Tut59");}break;
                case Lesson.Summon: if(GetComponent<PlayerHud>().Data.ActiveNanoSlot==0)Set(Lesson.Power,"Target the monster and use NANO POWER to stun it.",88,"Computress_Tut60");break;
                case Lesson.Escape: if(NearNpc(5100,8))instruction="Talk to Numbuh Two to escape Tech Square and finish training.";break;
            }
            TickEnemies();TickRecovery();
        }
        bool DeathFinished(int id){var anim=world.Entities.NpcById(id)?.GetComponent<NpcAnimator>();return Time.time-lastKillAt>1&&(anim==null||anim.DeathRemaining()<=0);}
        public static bool ClosePrompt(){if(!Active||!Instance.confirmSkip)return false;Instance.confirmSkip=false;return true;}
        void BeginCombat()
        {
            Remove(100);Remove(101);EquipWeapon(328);
            Spawn(1001,2674,new Vector3(573,-101,677),true,200);Spawn(1002,2674,new Vector3(573,-101,676),true,200);Spawn(1003,2674,new Vector3(574,-101,677),true,200);
            // Original entrance choreography separates the passive targets. Without
            // these move events all three remain overlapping at their spawn point.
            MoveNpc(1001,new Vector3(569,-101,675),300);MoveNpc(1002,new Vector3(570,-101,672),300);MoveNpc(1003,new Vector3(571,-101,672),300);
            Set(Lesson.Target,"Aim at a monster to select it.",29,"Computress_Tut13",new Vector3(573,-101,677));
        }
        public static bool TryInteract(NpcInfo npc) => Active && Instance.Interact(npc);
        bool Interact(NpcInfo npc)
        {
            if(npc==null||OverlayBlocking)return true;
            if((Current==Lesson.NumbuhTwo||Current==Lesson.AcceptMission)&&npc.Id==1005)Offer(npc,Lesson.AcceptMission);
            else if((Current==Lesson.ReturnMission||Current==Lesson.CompleteMission)&&npc.Id==1005){offered=Task(lastStartedTask);if(offered!=null){Set(Lesson.CompleteMission,"Select COMPLETE MISSION.",59,"Computress_Tut39");JournalWindow.Instance.OpenMode(2,offered,npc.Id);}}
            else if((Current==Lesson.Buttercup||Current==Lesson.AcceptButtercup)&&npc.Id==1007)Offer(npc,Lesson.AcceptButtercup);
            else if(Current==Lesson.Dexbot&&npc.Id==1009){_ = Warp(WarpDestination(tables,2694),()=>{TaskEnd(2251);Spawn(1012,2695,new Vector3(564.31f,-91,724.16f));Set(Lesson.Infection,"Find the Fusion Lair entrance and use it.",81,"Computress_Tut50",new Vector3(564.31f,-91,724.16f));});}
            else if(Current==Lesson.Infection&&npc.Id==1012){_ = Warp(WarpDestination(tables,2695),()=>{SetAmbient("ButtercupLair_wAmbient_Loop");TaskEnd(2252);Spawn(3000,2673,new Vector3(587,-133,984));Set(Lesson.Dexter,"Talk to Dexter inside the Fusion Lair.",83,"Computress_Tut53",new Vector3(587,-133,984));});}
            else if((Current==Lesson.Dexter||Current==Lesson.AcceptDexter)&&npc.Id==3000)Offer(npc,Lesson.AcceptDexter);
            else if(Current==Lesson.ExitLair&&npc.Id==4001){_ = Warp(WarpDestination(tables,2696),()=>{SetAmbient("TutorialMain_wAmbient_Loop");Spawn(5100,2671,new Vector3(907,11,685));Spawn(5101,2800,new Vector3(908,11,682));Set(Lesson.Bridge,"Cross the bridge and reach Numbuh Two.",84,"NumTwo_tut05",new Vector3(907,11,685));});}
            else if(Current==Lesson.Escape&&npc.Id==5100)StartCoroutine(TutorialAssets.Guard(Escape(),e=>failure=e.Message));
            return true; // Every tutorial NPC is local. Never open an unrelated live service.
        }
        MissionElement Task(int id) => tables.m_pMissionTable?.m_pMissionData?.FirstOrDefault(m=>m!=null&&m.m_iHTaskID==id);
        public static Vector3 WarpDestination(XdtTableScript data,int npc)
        {
            var warp=data?.m_pInstanceTable?.m_pWarpData?.FirstOrDefault(w=>w!=null&&w.m_iNpcNumber==npc);
            if(warp==null)throw new InvalidOperationException("Missing tutorial warp for NPC "+npc);
            return PlayerAvatarFactory.ToClient(warp.m_iToX,warp.m_iToY,warp.m_iToZ);
        }
        void Offer(NpcInfo npc,Lesson next)
        {
            offered = Task(next==Lesson.AcceptMission?2248:next==Lesson.AcceptButtercup?2250:2253);
            if(offered==null){failure="The original tutorial mission data is unavailable. Return to launcher and retry.";return;}
            Set(next,next==Lesson.AcceptMission?"Read the mission and select ACCEPT MISSION.":"Read the mission and select COMPLETE MISSION.",next==Lesson.AcceptMission?47:75,next==Lesson.AcceptMission?"Computress_Tut27":"Computress_Tut44");
            JournalWindow.Instance.OpenMode(next==Lesson.AcceptMission?1:2,offered,npc.Id);
        }
        async Task Warp(Vector3 position,Action arrived)
        {
            if(travelling)return;travelling=true;
            try {
                GameMenu.CloseWindow();GameMenu.SetOpen(false);
                if(BulletFx.Instance!=null)BulletFx.Instance.Spawn(394,world.Player.position,world.Player.rotation,1,null,5);
                Sfx.PlayAt("sound/Dexbot_Warp.wav",world.Player.position);
                await System.Threading.Tasks.Task.Delay(1500);
                if(this==null||!running)return;
                await world.LoadWorldAsync(position);
                if(this==null||!running)return;
                world.Entities.RaiseTeleport(Mathf.RoundToInt(position.x*100),Mathf.RoundToInt(position.z*100),Mathf.RoundToInt(position.y*100));
                arrived();
            } catch(Exception e){failure="Training map could not load: "+e.Message;}finally{travelling=false;}
        }
        IEnumerator Escape()
        {
            Set(Lesson.Finished,"Training complete. Preparing to enter the world…",0,"NumTwo_tut06");
            yield return new WaitForSeconds(4);Finish(true);
        }
        void Finish(bool completed){if(!running)return;finishing=completed;running=false;++voiceGeneration;if(voice!=null)voice.Stop();done.TrySetResult(completed);}
        void SetAmbient(string name){int token=++ambientGeneration;Sfx.Instance?.Load("Tut Sound/"+name+".mp3",clip=>{if(!running||token!=ambientGeneration||ambient==null||clip==null)return;ambient.Stop();ambient.clip=clip;ambient.volume=Sfx.AmbientVolume;ambient.Play();});}
        void OnDestroy(){if(Instance==this)Instance=null;++voiceGeneration;++ambientGeneration;if(guideEffect!=null)Destroy(guideEffect);if(MusicController.Instance!=null)MusicController.Instance.enabled=true;if(done!=null)done.TrySetResult(false);}
        void OnGUI() => DrawPresentation();
    }
}
