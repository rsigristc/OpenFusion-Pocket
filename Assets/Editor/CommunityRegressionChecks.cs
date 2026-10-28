using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.World;
using UnityEngine;
namespace OpenFusion.EditorTools
{
    public static class CommunityRegressionChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
        static T Field<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);
        public static void Run()
        {
            Movement(); SlopesAndDialogue(); Presentation(); Mail();
            Debug.Log("[community regression] PASS: dash roof/wall safety, camera actor filtering, launcher sweep/terrain fallback, cosmetic attack style, skill charge/target preview, Nano matchups, full inventory, shared mail roster and account reset.");
        }
        static GameObject Box(Transform parent,string name,Vector3 pos,Vector3 size)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=pos;go.AddComponent<BoxCollider>().size=size;return go;}
        static void SlopesAndDialogue()
        {
            var root = new GameObject("Slope and warp fixtures"); TerrainData data = null;
            var previous = NpcUI.Instance;
            try
            {
                data = new TerrainData { heightmapResolution = 33, size = new Vector3(32, 64, 32) };
                var heights = new float[33,33];
                for (int z=0;z<33;z++) for(int x=0;x<33;x++) heights[z,x]=z/32f;
                data.SetHeights(0,0,heights);
                var terrain=Terrain.CreateTerrainGameObject(data);terrain.transform.SetParent(root.transform);terrain.transform.position=new Vector3(24000,0,24000);
                var feet=terrain.transform.position+new Vector3(16,32,16);
                Physics.SyncTransforms(); var velocity=new Vector3(0,-1,8);
                Check(Ground.SlideVelocity(feet,null,50,ref velocity)&&velocity.z< -1.99f&&Mathf.Abs(velocity.y+2)<.001f,"Legacy steep slopes must replace uphill input with downhill sliding and doubled vertical velocity.");
                velocity=Vector3.forward*8;
                Check(!Ground.SlideVelocity(feet+Vector3.up*2,null,50,ref velocity)&&velocity.z==8,"Airborne movement above a slope must remain free.");
                for(int z=0;z<33;z++)for(int x=0;x<33;x++)heights[z,x]=z/128f;
                data.SetHeights(0,0,heights); feet.y=8;Physics.SyncTransforms();velocity=Vector3.forward*8;
                Check(!Ground.SlideVelocity(feet,null,50,ref velocity)&&velocity.z==8,"Walkable slopes must retain normal uphill input.");
                var ui=root.AddComponent<NpcUI>();var visible=new GameObject("Dialogue-hidden body").AddComponent<MeshRenderer>();visible.transform.SetParent(root.transform);visible.enabled=false;
                var intentionallyHidden=new GameObject("Hidden gear").AddComponent<MeshRenderer>();intentionallyHidden.transform.SetParent(root.transform);intentionallyHidden.enabled=false;
                var hidden=Field<List<Renderer>>(ui,"hidden");hidden.Add(visible);
                Call(ui,"OnTeleported",0,0,0);
                Check(visible.enabled&&!intentionallyHidden.enabled&&hidden.Count==0,"Warp must restore dialogue-owned renderers without exposing intentionally hidden gear.");
                visible.enabled=false;hidden.Add(visible);Call(ui,"Tick");
                Check(visible.enabled&&hidden.Count==0,"A destroyed/streamed-out dialogue NPC must not strand a hidden avatar.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root);if(data!=null)UnityEngine.Object.DestroyImmediate(data);NpcUI.Instance=previous; }
        }
        static void Movement()
        {
            var root=new GameObject("Community physics fixtures");var actor=new GameObject("Actor");actor.transform.SetParent(root.transform);
            var oldSelf=GameClient.FindSelf;var oldDS=DualScreenController.Instance; TerrainData terrainData=null;
            Vector3 origin=new Vector3(20000,0,20000);
            try{
                actor.transform.position=origin;var game=actor.AddComponent<GameClient>();game.enabled=false;game.SendMovement=false;
                Box(root.transform,"Floor",origin+Vector3.down*.1f,new Vector3(30,.2f,30));
                Box(root.transform,"Overhead building",origin+new Vector3(0,5,8),new Vector3(30,1,30));
                Physics.SyncTransforms();
                foreach(int fps in new[]{30,60,120}){
                    var controller=actor.GetComponent<CharacterController>();if(controller!=null)controller.enabled=false;actor.transform.position=origin;
                    if(controller!=null)controller.enabled=true;Set(game,"camYaw",0f);Set(game,"camPitch",-80f);
                    game.SkillDash(35);for(int i=0;i<fps&&game.Dashing;i++)Call(game,"AdvanceDash",actor.transform,1f/fps);
                    Check(Mathf.Abs(actor.transform.position.y-origin.y)<.12f&&actor.transform.position.z>origin.z+8,"Dash must stay horizontal and never snap to a roof, regardless of camera pitch/FPS.");
                }
                var wall=Box(root.transform,"Wall",origin+new Vector3(0,1,3),new Vector3(10,2,.2f));
                FlightSafety.Relocate(actor.GetComponent<CharacterController>(),actor.transform,origin);game.SkillDash(35);
                for(int i=0;i<40&&game.Dashing;i++)Call(game,"AdvanceDash",actor.transform,1f/60);
                Check(actor.transform.position.z<origin.z+2.8f,"Dash must sweep through CharacterController rather than teleport through walls.");
                var fakeNpc=Box(root.transform,"NPC capsule",origin+new Vector3(0,1,1),Vector3.one);fakeNpc.AddComponent<NpcInfo>();Physics.SyncTransforms();
                Check(Ground.CameraObstacle(origin+Vector3.up,Vector3.forward,10,actor.transform,~0,out var obstacle)&&obstacle.collider.gameObject==wall,"Camera must ignore actors and select world geometry.");
                Check(!Ground.TrySupport(origin+new Vector3(8,1.5f,0),actor.transform,.5f,3,89,out var floor)||floor<origin.y+2.01f,"Camera support must not select a distant overhead roof.");
                var orbit=Quaternion.Euler(35,90,0);
                Check(Quaternion.Angle(GameClient.CameraOrientation(Vector3.zero,orbit),orbit)<.001f&&Quaternion.Angle(GameClient.CameraOrientation(Vector3.up,orbit),orbit)<.001f,"Close-wall and vertical-pivot camera poses must not produce unstable look rotations.");
                UnityEngine.Object.DestroyImmediate(fakeNpc);
                var launch=FlightSafety.LaunchPosition(origin+Vector3.up,Vector3.forward,10,actor.transform,null,actor.GetComponent<CharacterController>());
                Check(launch.z<origin.z+2.8f,"Initial launcher offset must not cross a wall.");
                UnityEngine.Object.DestroyImmediate(wall);
                terrainData=new TerrainData{heightmapResolution=33,size=new Vector3(32,10,32)};var heights=new float[33,33];for(int y=0;y<33;y++)for(int x=0;x<33;x++)heights[y,x]=.5f;terrainData.SetHeights(0,0,heights);
                var terrain=Terrain.CreateTerrainGameObject(terrainData);terrain.transform.SetParent(root.transform);terrain.transform.position=origin-new Vector3(16,0,16);terrain.GetComponent<TerrainCollider>().enabled=false;
                FlightSafety.Relocate(actor.GetComponent<CharacterController>(),actor.transform,origin-Vector3.up*3);
                Check(FlightSafety.CorrectTerrain(actor.GetComponent<CharacterController>(),actor.transform)&&Mathf.Abs(actor.transform.position.y-5)<.01f,"Launcher flight must retain Legacy terrain correction when its physical collider is unavailable.");
                Check(FlightSafety.LaunchPosition(origin-Vector3.up,Vector3.forward,10,actor.transform,null,actor.GetComponent<CharacterController>()).y>=5,"An embedded launcher origin must not place the player below visible terrain.");
            }finally{UnityEngine.Object.DestroyImmediate(root);if(terrainData!=null)UnityEngine.Object.DestroyImmediate(terrainData);GameClient.FindSelf=oldSelf;}
        }
        static void Presentation()
        {
            var bone=new GameObject("Animated mount bone");var monkey=new GameObject("Monkey scale fixture");
            try {
                monkey.transform.SetParent(bone.transform,false);monkey.transform.localRotation=Quaternion.Euler(90,0,0);
                var scale=monkey.AddComponent<VehicleWorldScale>();
                foreach(var boneScale in new[]{Vector3.one*.36f,new Vector3(.3f,.4f,.5f),Vector3.one}) {
                    bone.transform.localScale=boneScale;scale.ApplyScale();
                    Check(Vector3.Distance(monkey.transform.lossyScale,Vector3.one)<.001f,"Monkey/vehicle mounts must retain world size across animated attachment-bone scales.");
                }
            } finally { UnityEngine.Object.DestroyImmediate(bone); }
            var items=new ItemBase[50];for(int i=0;i<items.Length;i++)items[i]=new ItemBase{Id=1,Type=7};
            Check(RewardHud.InventoryIsFull(items),"All 50 occupied slots must show full inventory.");items[49]=default;
            Check(!RewardHud.InventoryIsFull(items)&&!RewardHud.InventoryIsFull(null),"A free slot or absent inventory cannot show full.");
            for(int nano=0;nano<3;nano++)for(int mob=0;mob<3;mob++)Check(NpcUI.NanoRelationTexture(mob,nano)==(mob==nano?"nano_draw":mob==(nano+1)%3?"nano_win":"nano_lose"),"Nano type advantage cycle differs from Legacy.");
            foreach(string texture in new[]{"nano_win","nano_lose","nano_draw","full_inven","target_n"})Check(Resources.Load<Texture2D>("OpenFusion/UI/Textures/"+texture)!=null,"Missing original HUD texture "+texture);
            var root=new GameObject("Community presentation fixtures");var oldSelf=GameClient.FindSelf;
            try{
                var game=root.AddComponent<GameClient>();game.enabled=false;
                var combat=root.AddComponent<CombatSystem>();combat.enabled=false;
                var stats=new ItemElement{m_iItemNumber=1,m_iEquipType=4,m_iAtkRange=2345,m_iTargetMode=0};var style=new ItemElement{m_iItemNumber=2,m_iEquipType=1,m_iAtkRange=10};
                Set(combat,"tables",new XdtTableScript{m_pWeaponItemTable=new EquipmentItemTableScript{m_pItemData=new[]{stats,style}}});Set(combat,"gc",game);
                var app=new PcAppearance();app.Equip[0]=new ItemBase{Id=1,Opt=(2<<16)|7};Call(combat,"ReadWeapon",app);
                Check(Field<int>(combat,"aniStyle")==1&&Field<int>(combat,"range")==2345&&ReferenceEquals(Field<ItemElement>(combat,"weaponIt"),stats),"Combined weapons must use style animation and base combat parameters.");
                Check(PcAppearance.VisibleWeaponId(app.Equip[0],false)==1&&PcAppearance.VisibleWeaponId(app.Equip[0])==2,"Cosmetic selection must respect combined appearance mode.");
                var model=new GameObject("Mob model");model.transform.SetParent(root.transform);var animation=model.AddComponent<Animation>();
                foreach(string clipName in new[]{"stand1","readyspell","ready","skill0"}){var clip=new AnimationClip{legacy=true};clip.SetCurve("",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,0,.2f,0));animation.AddClip(clip,clipName);}
                var animator=root.AddComponent<NpcAnimator>();animator.Init(model,null,null);animator.SkillReady();
                Check(animation["readyspell"].wrapMode==WrapMode.Loop&&Field<bool>(animator,"skillCharging"),"Drain charging must use and retain Legacy readyspell.");
                animator.PlayAction("skill0");Check(!Field<bool>(animator,"skillCharging"),"Casting must end the charge pose.");animator.SkillReady();animator.EndSkillReady();Check(!Field<bool>(animator,"skillCharging"),"A movement/interrupt packet must release the charge pose.");
                Check(WorldEntities.IsEffectOnlyNpcVisual(model,17)&&!WorldEntities.IsEffectOnlyNpcVisual(model,3),"An animated Recall Point anchor is valid without geometry; an ordinary missing NPC is not.");
                var entities=root.AddComponent<WorldEntities>();var npcs=Field<Dictionary<int,GameObject>>(entities,"npcs");
                var selected=new GameObject("Selected mob");selected.transform.SetParent(root.transform);selected.transform.position=new Vector3(20000,0,20005);var info=selected.AddComponent<NpcInfo>();info.Id=1;info.Team=2;info.Hp=100;info.Type=0;npcs.Add(1,selected);
                var other=new GameObject("Other mob");other.transform.SetParent(root.transform);other.transform.position=new Vector3(20001,0,20005);var otherInfo=other.AddComponent<NpcInfo>();otherInfo.Id=2;otherInfo.Team=2;otherInfo.Hp=100;otherInfo.Type=0;npcs.Add(2,other);
                var self=new GameObject("Self");self.transform.SetParent(root.transform);self.transform.position=new Vector3(20000,0,20000);
                var nano=root.AddComponent<NanoSystem>();nano.enabled=false;Set(nano,"ents",entities);var targets=new List<NpcInfo>();var skill=new SkillElement{m_iEffectType=1,m_iTargetType=1,m_iEffectTarget=1,m_iEffectRange=1000,m_iTargetNumber=1};
                Call(nano,"CollectEnemySkillTargets",targets,skill,self.transform,info,Vector3.forward);Check(targets.Count==1&&targets[0]==info,"Single-target skill preview must select the actual target.");
                skill.m_iEffectRange=100;Call(nano,"CollectEnemySkillTargets",targets,skill,self.transform,info,Vector3.forward);Check(targets.Count==0,"Skill preview must reject out-of-range targets.");
                skill.m_iEffectTarget=6;skill.m_iEffectArea=1000;skill.m_iTargetNumber=1;Call(nano,"CollectEnemySkillTargets",targets,skill,self.transform,info,Vector3.forward);Check(targets.Count==1&&targets[0]==info,"Area-target preview must prioritize selected mob and respect maximum count.");
            }finally{UnityEngine.Object.DestroyImmediate(root);GameClient.FindSelf=oldSelf;}
        }
        static void Mail()
        {
            var root=new GameObject("Community mail fixture");var oldSocial=SocialMenu.Instance;var oldEmail=EmailWindow.Instance;var sender=NpcUI.Sender;
            try{
                var social=SocialMenu.Ensure(root);social.Init(null,null,null);var mail=EmailWindow.Ensure(root);mail.Init(null,null,null,null,null);
                var roster=Field<SocialMenu.Buddy[]>(social,"bud");for(int i=0;i<50;i++)roster[i]=new SocialMenu.Buddy{Uid=10000+i,Slot=i,First="Fixture",Last=i.ToString(),NameCheck=1,Blocked=i%3==0};
                var method=typeof(EmailWindow).GetMethod("MailBuddies",BindingFlags.Static|BindingFlags.NonPublic);
                var recipients=(List<SocialMenu.Buddy>)method.Invoke(null,null);
                Check(recipients.Count==33&&recipients.Last().Slot==49,"Mail must use canonical roster slots, all pages and block filters.");
                roster[49]=null;Check(((List<SocialMenu.Buddy>)method.Invoke(null,null)).Count==32,"Removed buddies must disappear without relogging.");
                byte[] removal=null;NpcUI.Sender=packet=>removal=packet;
                var mailInfo=typeof(EmailWindow).GetNestedType("Info",BindingFlags.NonPublic);var info=Activator.CreateInstance(mailInfo);
                mailInfo.GetField("First").SetValue(info,"");mailInfo.GetField("Last").SetValue(info,"");mailInfo.GetField("FromUid").SetValue(info,99999L);
                roster[1].First=roster[1].Last="";roster[1].NameCheck=0;
                Call(mail,"RemoveBuddy",info);Check(removal==null,"An unknown sender must never match an unrelated buddy with a pending/empty name.");
                mailInfo.GetField("FromUid").SetValue(info,10047L);Call(mail,"RemoveBuddy",info);
                Check(removal!=null&&removal[12]==47&&BitConverter.ToInt64(removal,4)==10047,"Mail buddy removal must target the exact UID and original roster slot.");
                Set(mail,"toName","synthetic old account");Set(mail,"buddyScroll",500f);Set(mail,"composing",true);
                social.Init(null,null,null);mail.Init(null,null,null,null,null);
                Check(((List<SocialMenu.Buddy>)method.Invoke(null,null)).Count==0&&Field<string>(mail,"toName")==""&&Field<float>(mail,"buddyScroll")==0&&!Field<bool>(mail,"composing"),"Relogging must reset recipient data, draft and scroll across accounts.");
            }finally{NpcUI.Sender=sender;UnityEngine.Object.DestroyImmediate(root);SocialMenu.Instance=oldSocial;EmailWindow.Instance=oldEmail;}
        }
    }
}
