using System;
using System.IO;
using System.Linq;
using OpenFusion.Tables;
using OpenFusion.World;
using UnityEditor;
using UnityEngine;
namespace OpenFusion.EditorTools
{
    public static class TutorialAssetAudit
    {
        public static void Batch(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Run()
        {
                string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ffcache/238bd38f-3add-4dc5-a29a-06213461a206"));
                var source=new FfCacheBundleSource(root);var store=new AssetStore();var index=new AssetIndex();
                foreach(string bundle in new[]{"CharacterCreation.resourceFile","CharacterSelection.resourceFile","CharTexture.resourceFile","NpcTexture.resourceFile","Tutorial.resourceFile","Effects.resourceFile","Nano.resourceFile","FutureNano.resourceFile"})
                    foreach(var file in source.Load(bundle))if(file.Key!="__info")index.Add(file.Key,store.Add(file.Key,file.Value));
                var models=new ModelFactory(store,index,new DongBuilder(store));
                var dir="Logs/tutorial-audit/resources";Directory.CreateDirectory(dir);
                File.WriteAllLines(dir+"/assets.txt",index.Keys.OrderBy(k=>k));
                foreach(string path in index.Keys.Where(k=>k.StartsWith("tutorialassets/tut_")&&k.EndsWith(".png"))) {
                    var t=models.Texture(path);if(t!=null)File.WriteAllBytes(dir+"/"+Path.GetFileName(path),t.EncodeToPNG());
                }
                var tables=TableLoader.Load(source.Load("TableData.resourceFile"));
                File.WriteAllLines(dir+"/warps.jsonl",tables.Xdt.m_pInstanceTable.m_pWarpData.Where(w=>w!=null&&new[]{2694,2695,2696}.Contains(w.m_iNpcNumber)).Select(w=>JsonUtility.ToJson(w)));
                File.WriteAllLines(dir+"/rewards.jsonl",tables.Xdt.m_pMissionTable.m_pRewardData.Where(r=>r!=null&&r.m_iMissionRewardID==531).Select(r=>JsonUtility.ToJson(r)));
                var look=new PlayerLookBuilder(tables.Xdt).Build(new PlayerStyle{Gender=1,FaceStyle=1,HairStyle=1},new PlayerEquip{Weapon=328});
                File.WriteAllLines(dir+"/weapon-assets.txt",look.RequiredPaths().Select(p=>p+" available="+models.Locate(p).Ok));
                TutorialAssets.Validate(models,tables.Xdt,null,null);
                var catalog=ModelCatalog.From(tables.Xdt);var clipAudit=new System.Collections.Generic.List<string>();
                foreach(int npc in new[]{2674,2675,2676,2677,2678}){
                    GameObject model=null;try{
                        model=models.Instantiate(catalog.Npc(npc));WorldEntities.ActivateAll(model);
                        var animation=model!=null?model.GetComponentInChildren<Animation>(true):null;
                        if(animation==null||animation["melee1"]==null||animation["death"]==null||animation["wound"]==null)throw new Exception("Missing original tutorial combat animation for NPC "+npc);
                        clipAudit.Add(npc+": "+string.Join(", ",animation.Cast<AnimationState>().Select(s=>s.name)));
                        var animator=model.AddComponent<NpcAnimator>();animator.Init(model,tables.Xdt.m_pNpcTable.m_pNpcData.First(n=>n!=null&&n.m_iNpcNumber==npc),null);
                        if(!animator.PlayAction("melee1")||!animation.IsPlaying("melee1"))throw new Exception("Tutorial attack clip did not start for NPC "+npc);
                        animator.Wound();animator.Die(null,1,2);if(!animator.Dying||!animation.IsPlaying("death"))throw new Exception("Tutorial death clip did not start for NPC "+npc);
                    }finally{if(model!=null)UnityEngine.Object.DestroyImmediate(model);}
                }
                File.WriteAllLines(dir+"/combat-clips.txt",clipAudit);
                var factory=new PlayerAvatarFactory(tables.Xdt,models);
                foreach(int gender in new[]{1,2}) {
                    var appearance=new OpenFusion.Net.PcAppearance{Style=new PlayerStyle{Gender=gender,FaceStyle=1,HairStyle=1,SkinColor=1,HairColor=1,EyeColor=1}};
                    GameObject avatar=null;try {
                        avatar=factory.Create(appearance);appearance.Equip[0]=new OpenFusion.Net.ItemBase{Type=0,Id=328,Opt=1};factory.RefreshEquipment(avatar,appearance);
                        var attachment=avatar.GetComponentsInChildren<ActorAttachTag>(true).FirstOrDefault(a=>a.Slot==3);
                        if(attachment==null||!attachment.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled&&r.gameObject.activeInHierarchy))throw new Exception("Practice weapon is invisible for gender "+gender);
                    }finally{if(avatar!=null)UnityEngine.Object.DestroyImmediate(avatar);}
                }
                Debug.Log("[tutorial asset audit] decoded "+Directory.GetFiles(dir,"tut_*.png").Length+" original tutorial illustrations; indexed "+index.Count+" assets");
        }
    }
}
