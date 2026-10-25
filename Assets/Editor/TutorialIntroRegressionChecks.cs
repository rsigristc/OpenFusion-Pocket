using System;
using System.IO;
using System.Linq;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables;
using OpenFusion.World;
using UnityEngine;
namespace OpenFusion.EditorTools
{
    public static class TutorialIntroRegressionChecks
    {
        static void Check(bool value,string message) {if(!value)throw new Exception(message);}
        public static void Run()
        {
            var character = new CharInfo { Appearance = new PcAppearance { Uid=987,Style=new PlayerStyle {Gender=1,FaceStyle=1,HairStyle=1} },TutorialFlag=0 };
            Check(TutorialIntroScreen.ShouldPlay(character,false) && !TutorialIntroScreen.ShouldPlay(character,true),"Intro must be limited to unseen, tutorial-pending characters");
            character.TutorialFlag=1;Check(!TutorialIntroScreen.ShouldPlay(character,false),"Completed tutorial must not replay intro");
            character.TutorialFlag=-1;Check(!TutorialIntroScreen.ShouldPlay(character,false),"Unknown tutorial flag must not infer a new character");
            character.TutorialFlag=0;character.Appearance.Style=default;Check(!TutorialIntroScreen.ShouldPlay(character,false),"Name-only characters must finish customization first");
            var packet = new byte[ClientPackets.CharInfoSize];packet[81]=1;
            Check(ClientPackets.ParseCharInfo(packet).TutorialFlag==1,"CHAR_INFO sPCStyle2 tutorial byte is at body offset 81");
            packet = new byte[100];packet[81]=0;
            Check(ClientPackets.ParseCharCreateSucc(packet).TutorialFlag==0,"CHAR_CREATE_SUCC must retain the pending tutorial flag");
            Check(TutorialIntroScreen.Key("https://a.example",1)!=TutorialIntroScreen.Key("https://a.example",2) && TutorialIntroScreen.Key("https://a.example",1)!=TutorialIntroScreen.Key("https://b.example",1),"Local intro history must be isolated by server and character");
            var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ffcache/238bd38f-3add-4dc5-a29a-06213461a206"));
            if(Directory.Exists(root)) {
                var source = new FfCacheBundleSource(root);var store = new AssetStore();var index = new AssetIndex();
                foreach(var name in new[]{"CharacterCreation.resourceFile","NpcTexture.resourceFile","Effects.resourceFile"}) foreach(var file in source.Load(name)) {if(file.Key=="__info")continue;var sf=store.Add(file.Key,file.Value);index.Add(file.Key,sf);}
                foreach(var path in new[]{"mob/npc_dexter2.kfm","mob/npc_deedee2.kfm","mob/npc_computress.kfm","charactercreationassets/charcreationbg.png",TutorialAssets.EffectPath(740)}) Check(index.Has(path),"Missing original introduction asset: "+path);
                var tables = TableLoader.Load(source.Load("TableData.resourceFile"));
                for(int line=4;line<=10;line++)Check(!string.IsNullOrEmpty(TutorialIntroScreen.SceneText(tables.Xdt.m_pCutSceneTable,line)),"Missing original intro subtitle line "+line);
                var missing = new[]{"Dexter_CC01","Computress_CC01","Dexter_CCEnginePwr","Dexter_CCReadings","Dexter_CCLaunch","DeeDee_CCButton","Dexter_CCDeeDeeNo"}.Where(n=>!index.Has("cc sound/"+n.ToLowerInvariant()+".wav")).ToArray();
                Check(missing.Length==0,"Missing original intro voices: "+string.Join(",",missing));
                var models = new ModelFactory(store,index,new DongBuilder(store));
                foreach(var entry in new[]{
                    new[]{"npc_dexter2","stand1","excellent","deedeeno","busyrun"},
                    new[]{"npc_deedee2","stand1","walk"},
                    new[]{"npc_computress","stand1","event1","event2","stand2"}}) {
                    GameObject actor=null;
                    try {
                        actor=models.Instantiate(new ModelRef {Kind=ModelKind.Npc,Model="mob/"+entry[0]+".kfm"});
                        Check(actor!=null,"Cannot instantiate original intro actor "+entry[0]);
                        var names=actor.GetComponentsInChildren<Animation>(true).SelectMany(a=>a.Cast<AnimationState>()).Select(a=>a.name).ToArray();
                        foreach(var clip in entry.Skip(1)) Check(names.Contains(clip),"Missing intro clip "+entry[0]+":"+clip+"; available="+string.Join(",",names));
                    } finally {if(actor!=null)UnityEngine.Object.DestroyImmediate(actor);}
                }
            }
            Debug.Log("[intro regression] explicit tutorial flags, per-character/server history and original local cinematic models/voices/subtitles passed; no server completion mutation.");
        }
    }
}
