using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenFusion.Api;
using OpenFusion.Assets;
using OpenFusion.Tables;
using OpenFusion.Unity25;
using OpenFusion.World;
using UnityEditor;
using UnityEngine;
namespace OpenFusion.EditorTools
{
    public static class CommunityAssetAudit
    {
        public static async void BatchMonkey()
        {
            try
            {
                var api=new ServerApi("https://api.ffretrobution.net");var info=await api.GetInfo();var chain=await api.GetChain(info.GameVersions.First());
                var cache=new BundleCache(Path.GetFullPath("Logs/retrobution-303/live"),BundleCatalog.FromChain(chain));
                var main=await cache.EnsureMainFileAsync(chain[0]);var manifest=MainGameData.FromMain(main).Manifest;
                var files=new[]{"CharacterCreation.resourceFile","CharacterSelection.resourceFile"};await cache.EnsureAsync(files);
                var manager=new BundleManager(cache,manifest);var store=new AssetStore();
                foreach(var bundle in files)foreach(var kv in manager.Load(bundle))store.Add(kv.Key,kv.Value);
                var factory=new ModelFactory(store,manager,new DongBuilder(store));var fixture=new GameObject("Real Monkey skeleton fixture");
                try
                {
                    foreach(string body in new[]{"actor/m.kfm","actor/w.kfm"})
                    {
                        var actor=factory.Instantiate(new ModelRef{Model=body},fixture.transform);
                        if(actor==null)throw new InvalidOperationException("Missing real actor "+body);
                        WorldEntities.ActivateAll(actor);
                        var bone=actor.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Bip01 Broomstick");
                        var entities=fixture.AddComponent<WorldEntities>();
                        ((Dictionary<int,GameObject>)typeof(WorldEntities).GetField("players",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(entities)).Add(42,actor);
                        var flight=fixture.AddComponent<BroomFlight>();flight.Init(entities,null);flight.GetModels=()=>factory;
                        typeof(BroomFlight).GetMethod("SetMounted",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(flight,new object[]{42,true});
                        typeof(BroomFlight).GetMethod("TickMonkey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(flight,null);
                        var mount=((Dictionary<int,GameObject>)typeof(BroomFlight).GetField("brooms",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(flight))[42];
                        var scale=mount.GetComponent<VehicleWorldScale>();
                        if(scale==null)throw new InvalidOperationException("Monkey must preserve Legacy world scale on the real animated bone.");
                        var animation=actor.GetComponentInChildren<Animation>(true);
                        foreach(string name in new[]{"mount1","mount2"})foreach(float time in new[]{0f,.25f,.75f,1f})
                        {
                            animation.Play(name);animation[name].normalizedTime=time;animation.Sample();
                            scale.ApplyScale();
                            var renderers=mount.GetComponentsInChildren<Renderer>(true);
                            if(Vector3.Distance(mount.transform.lossyScale,Vector3.one)>.01f||!renderers.Any(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.bounds.size.magnitude>1.5f))throw new InvalidOperationException("Monkey shrank inside rider during "+body+" "+name+" at "+time);
                            foreach(var mountAnimation in mount.GetComponentsInChildren<Animation>(true))
                                if(mountAnimation.GetClipCount()>0&&!mountAnimation.Cast<AnimationState>().Any(st=>mountAnimation.IsPlaying(st.name)))throw new InvalidOperationException("Monkey embedded animation must be playing.");
                            Debug.Log("[monkey skeleton] "+body+" "+name+" "+time+" bone local="+bone.localScale+" world="+bone.lossyScale+" mount="+mount.transform.lossyScale+" bounds="+string.Join(";",renderers.Select(r=>r.bounds.size.ToString("F4"))));
                        }
                        UnityEngine.Object.DestroyImmediate(mount);flight.Init(entities,null);UnityEngine.Object.DestroyImmediate(flight);UnityEngine.Object.DestroyImmediate(entities);UnityEngine.Object.DestroyImmediate(actor);
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(fixture);}
                Debug.Log("[monkey skeleton] PASS: real male/female skeletons, both riding clips, full-sized animated Monkey and mount lifecycle.");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static async void Batch(){try{CommunityRegressionChecks.Run();await Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static async Task Run()
        {
            Directory.CreateDirectory("Logs/community-audit");
            var output=new List<string>();
            var api=new ServerApi("https://api.ffretrobution.net");var info=await api.GetInfo();var chain=await api.GetChain(info.GameVersions.First());
            var cache=new BundleCache(Path.GetFullPath("Logs/retrobution-303/live"),BundleCatalog.FromChain(chain));
            var main=await cache.EnsureMainFileAsync(chain[0]);var manifest=MainGameData.FromMain(main).Manifest;
            await cache.EnsureAsync(new[]{"TableData.resourceFile"});var game=TableLoader.Load(cache.Load("TableData.resourceFile"));var x=game.Xdt;
            var catalog=ModelCatalog.From(x);var target=new List<(string name,ModelRef model)>();
            foreach(var row in x.m_pNpcTable.m_pNpcData){
                if(row==null)continue;var names=x.m_pNpcTable.m_pNpcStringData;
                string name=row.m_iNpcName>=0&&row.m_iNpcName<names.Length?names[row.m_iNpcName]?.m_strName??"":"";
                if(new[]{"larry","father","recall"}.Any(s=>name.IndexOf(s,StringComparison.OrdinalIgnoreCase)>=0))target.Add((name,catalog.Npc(row.m_iNpcNumber)));
            }
            target.Add(("Monkey mount",new ModelRef{Model="wear/item_broom_01.nif"}));
            var required=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var model in catalog.All().Where(m=>m.Kind==ModelKind.Npc)) foreach(string path in new[]{model.Model,model.Tex1,model.Tex2}) if(!string.IsNullOrEmpty(path))required.Add(path);
            foreach(var item in target){var m=item.model;if(m==null)continue;foreach(var path in new[]{m.Model,m.Tex1,m.Tex2})if(!string.IsNullOrEmpty(path))required.Add(path);
                if(m.HNpcIndex>=0&&m.HNpcIndex<game.HNpc.TableElement.Length)foreach(string path in ActorLook.FromHNpc(game.HNpc.TableElement[m.HNpcIndex]).RequiredPaths())if(path!=null)required.Add(path);
            }
            var mapping=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            var localOwner=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string root=Path.GetFullPath("../ffcache/238bd38f-3add-4dc5-a29a-06213461a206");
            foreach(string bundle in chain.SelectMany(c=>c.bundles.Keys).Distinct().Where(b=>!b.StartsWith("Map_")&&!b.Contains("Music")&&!b.Contains("Sound"))){
                string dir=Path.Combine(root,FfCacheBundleSource.Escape(bundle));if(!Directory.Exists(dir))continue;
                foreach(string file in Directory.GetFiles(dir).Where(f=>Path.GetFileName(f).StartsWith("CustomAssetBundle",StringComparison.OrdinalIgnoreCase))){
                    var sf=SerializedFile.Parse(File.ReadAllBytes(file));var index=new AssetIndex();index.Add(Path.GetFileName(file),sf);
                    var objectIds=new HashSet<long>(sf.Objects.Select(o=>o.PathId));
                    foreach(string path in required)if(index.TryGet(path,out var entry)&&entry.Target.PathId!=0){
                        bool local=entry.Target.FileId==0;if(local&&!objectIds.Contains(entry.Target.PathId))continue;
                        if(!mapping.ContainsKey(path)||local&&!localOwner.Contains(path)){mapping[path]=bundle;if(local)localOwner.Add(path);}
                    }
                }GC.Collect();
            }
            var targetPaths=new HashSet<string>(target.SelectMany(t=>new[]{t.model?.Model,t.model?.Tex1,t.model?.Tex2}).Where(p=>p!=null),StringComparer.OrdinalIgnoreCase);
            foreach(string path in targetPaths)output.Add("asset "+path+" => "+(mapping.TryGetValue(path,out var bundle)?bundle:"NOT IN LEGACY CACHE"));
            Directory.CreateDirectory("Assets/Resources/OpenFusion");
            File.WriteAllText("Assets/Resources/OpenFusion/NpcAssetRoutes.json",Newtonsoft.Json.JsonConvert.SerializeObject(mapping,Newtonsoft.Json.Formatting.Indented));
            var files=mapping.Where(kv=>targetPaths.Contains(kv.Key)).Select(kv=>kv.Value).Concat(new[]{"CharacterCreation.resourceFile","CharacterSelection.resourceFile","Retro_shared.resourceFile","Retro_shared_part2.resourceFile"}).Distinct().ToArray();
            await cache.EnsureAsync(files);var manager=new BundleManager(cache,manifest);var store=new AssetStore();var combined=new AssetIndex();
            foreach(string bundle in files)foreach(var kv in manager.Load(bundle))try{store.Add(kv.Key,kv.Value);combined.Add(kv.Key,SerializedFile.Parse(kv.Value));}catch{}
            var builder=new DongBuilder(store);var factory=new ModelFactory(store,manager,builder);var avatars=new PlayerAvatarFactory(x,factory);
            foreach(var item in target){var m=item.model;if(m==null)continue;GameObject go=null;
                try{
                    if(m.Model!=null){var reference=factory.Locate(m.Model);var routed=new ModelFactory(store,manager,builder).Locate(m.Model);output.Add(" REF "+item.name+" ok="+reference.Ok+" file="+reference.FileName+" class="+(reference.Ok?store.TypeOf(reference):"missing")+" routed="+routed.Ok+" target="+(reference.Ok?reference.Info.PathId:0));}
                    bool human=m.HNpcIndex>=0&&m.HNpcIndex<game.HNpc.TableElement.Length;
                    go=human?avatars.Assembler.Build(ActorLook.FromHNpc(game.HNpc.TableElement[m.HNpcIndex]),null,null):factory.Instantiate(m);
                    output.Add(item.name+" id="+m.Id+" kind="+m.NpcType+" hnpc="+m.HNpcIndex+" model="+m.Model+" router="+(m.Model==null?"null":manager.Router.Locate(m.Model))+" geometry="+WorldEntities.HasNpcGeometry(go));
                    if(go!=null){WorldEntities.ActivateAll(go);output.Add(" nodes="+go.GetComponentsInChildren<Transform>(true).Length+" renderers="+go.GetComponentsInChildren<Renderer>(true).Length+" clips="+string.Join(",",go.GetComponentsInChildren<Animation>(true).SelectMany(a=>a.Cast<AnimationState>().Select(st=>st.name)).Take(30)));}
                    if((m.Id==692||m.Id==709||m.Id==1386||item.name=="Monkey mount")&&!WorldEntities.HasNpcGeometry(go)&&!WorldEntities.IsEffectOnlyNpcVisual(go,m.NpcType))throw new InvalidOperationException("Required real NPC/mount model failed: "+item.name);
                }catch(Exception e){output.Add(item.name+" failed "+e.GetType().Name+": "+e.Message);}finally{if(go!=null)UnityEngine.Object.DestroyImmediate(go);}
            }
            File.WriteAllLines("Logs/community-audit/assets.txt",output);Debug.Log("[community asset audit] "+string.Join("\n",output));
            if(output.Any(line=>line.StartsWith("Recall Point failed")||line.StartsWith("Larry 3000 failed")||line.StartsWith("Father failed")||line.StartsWith("Monkey mount failed")))throw new InvalidOperationException("Required NPC/mount audit failed; see Logs/community-audit/assets.txt");
            var fixture=new GameObject("Resource recovery integration fixture");
            try{
                AssetDatabase.Refresh();
                var loader=fixture.AddComponent<DongLoader>();loader.Paused=true;loader.Init(main,manager,Vector3.zero,manifest);
                var recoveredModels=loader.CreateModelFactory(manager);
                var recovery=fixture.AddComponent<AssetRecovery>();recovery.Configure(cache,BundleCatalog.FromChain(chain),manager,loader,recoveredModels);
                if(recoveredModels.Locate("mob/npc_larry_real.kfm").Ok)throw new InvalidOperationException("Recovery fixture must start without resource bytes in its store.");
                typeof(AssetRecovery).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(recovery,null);
                var deadline=DateTime.UtcNow.AddSeconds(30);
                while(!recoveredModels.Locate("mob/npc_larry_real.kfm").Ok&&DateTime.UtcNow<deadline)await Task.Delay(25);
                if(!recoveredModels.Locate("mob/npc_larry_real.kfm").Ok)throw new InvalidOperationException("Actual runtime recovery did not import Larry's missing resource bundle.");
                var npc=recoveredModels.Instantiate(catalog.Npc(692),fixture.transform);
                if(!WorldEntities.HasNpcGeometry(npc))throw new InvalidOperationException("Recovered Larry must contain his real mesh, not a placeholder.");
                recoveredModels.Locate("mob/rxcom.kfm");deadline=DateTime.UtcNow.AddSeconds(30);
                while(!recoveredModels.Locate("mob/rxcom.kfm").Ok&&DateTime.UtcNow<deadline){typeof(AssetRecovery).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(recovery,null);await Task.Delay(25);}
                var recall=recoveredModels.Instantiate(catalog.Npc(1386),fixture.transform);
                if(!WorldEntities.IsEffectOnlyNpcVisual(recall,17))throw new InvalidOperationException("Recall recovery must load the canonical prefab owner, not only an external alias bundle.");
                var entities=fixture.AddComponent<WorldEntities>();var rider=new GameObject("Rider fixture");rider.transform.SetParent(fixture.transform);
                var animation=rider.AddComponent<Animation>();var clip=new AnimationClip{legacy=true};clip.SetCurve("",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,0,1,0));animation.AddClip(clip,"mount1");
                var animator=rider.AddComponent<NpcAnimator>();animator.Player=true;animator.Init(rider,null,null);
                ((Dictionary<int,GameObject>)typeof(WorldEntities).GetField("players",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(entities)).Add(42,rider);
                var flight=fixture.AddComponent<BroomFlight>();flight.Init(entities,null);flight.GetModels=()=>factory;
                typeof(BroomFlight).GetMethod("SetMounted",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(flight,new object[]{42,true});
                typeof(BroomFlight).GetMethod("TickMonkey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(flight,null);
                if(!animator.External||!rider.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled&&r.gameObject.activeInHierarchy))throw new InvalidOperationException("Real Monkey mount must be active and its pose protected from normal locomotion.");
                flight.Init(entities,null);if(animator.External)throw new InvalidOperationException("A new session must release the mounted pose.");
                Debug.Log("[community integration] PASS: actual missing-bundle recovery imports a real Larry mesh; real Monkey attachment is active, animated and session-safe.");
            }finally{UnityEngine.Object.DestroyImmediate(fixture);}
        }
    }
}
