using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OpenFusion.Api;
using OpenFusion.Assets;
using OpenFusion.Net;
using OpenFusion.Tables;
using OpenFusion.Unity25;
using OpenFusion.World;
using UnityEditor;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class NpcRecoveryRegressionChecks
    {
        public static Func<GameObject,Task> Capture;
        public static async void Batch(){try{await Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private).Invoke(o,args);
        static void Field(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
        public static async Task Run()
        {
            Directory.CreateDirectory("Logs/npc-recovery");
            var api=new ServerApi("https://api.ffretrobution.net");var info=await api.GetInfo();var chain=await api.GetChain(info.GameVersions.First());
            var cache=new BundleCache(Path.GetFullPath("Logs/retrobution-303/live"),BundleCatalog.FromChain(chain));
            var main=await cache.EnsureMainFileAsync(chain[0]);var manifest=MainGameData.FromMain(main).Manifest;
            await cache.EnsureAsync(new[]{"TableData.resourceFile"});var tables=TableLoader.Load(cache.Load("TableData.resourceFile")).Xdt;
            var catalog=ModelCatalog.From(tables);var boss=catalog.Npc(7);
            Check(boss?.Model=="mob/fusion_numbuhtwo.kfm","Fusion Numbuh Two table model changed; re-audit.");
            var required=new[]{boss.Model,boss.Tex1,boss.Tex2}.Where(p=>!string.IsNullOrEmpty(p)).ToArray();
            var mapping=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            string root=Path.GetFullPath("../ffcache/238bd38f-3add-4dc5-a29a-06213461a206");
            foreach(string bundle in chain[0].bundles.Keys.Where(b=>!b.StartsWith("Map_")))
            {
                string dir=Path.Combine(root,FfCacheBundleSource.Escape(bundle));if(!Directory.Exists(dir))continue;
                foreach(string file in Directory.GetFiles(dir).Where(f=>Path.GetFileName(f).StartsWith("CustomAssetBundle",StringComparison.OrdinalIgnoreCase)))
                {
                    var sf=SerializedFile.Parse(File.ReadAllBytes(file));var index=new AssetIndex();index.Add(Path.GetFileName(file),sf);
                    foreach(string path in required)if(index.Has(path))mapping[path]=bundle;
                    foreach(string path in index.Keys.Where(k=>k.Contains("numbuhtwo")))Debug.Log("[npc asset] "+bundle+" "+path);
                }
                GC.Collect();
            }
            foreach(string path in required)Check(mapping.ContainsKey(path),"Real boss asset not found: "+path);
            var names=mapping.Values.Concat(new[]{"CharacterCreation.resourceFile","CharacterSelection.resourceFile","Retro_shared.resourceFile","Retro_shared_part2.resourceFile"}).Distinct().ToArray();
            await cache.EnsureAsync(names);
            var manager=new BundleManager(cache,manifest);var store=new AssetStore();var builder=new DongBuilder(store);var factory=new ModelFactory(store,manager,builder);
            var host=new GameObject("NPC late asset recovery fixture");
            try
            {
                var world=host.AddComponent<WorldEntities>();world.NpcTable=tables.m_pNpcTable;
                Field(world,"catalog",catalog);Field(world,"models",factory);Field(world,"root",host.transform);
                Call(world,"SpawnNpc",new NpcAppearance{Id=90007,Type=7,Hp=1234,X=100000,Y=100000,Z=2000});
                var holder=world.NpcById(90007);var stats=holder.GetComponent<NpcInfo>();
                Check(holder!=null&&holder.GetComponentInChildren<NpcVisualPlaceholder>(true)!=null&&!WorldEntities.HasNpcGeometry(holder),"An unavailable boss must retain a tagged placeholder rather than pretend it loaded.");
                foreach(var renderer in holder.GetComponentsInChildren<Renderer>(true))Check(renderer.sharedMaterial?.shader?.name=="OpenFusion/Legacy","Fallback marker must use a URP-compatible shader, not the default pink material.");
                // Simulate movement and damage before the streamed model arrives.
                holder.transform.position=new Vector3(1003,22,1004);stats.Hp=777;
                var mover=holder.GetComponent<RemoteMover>();mover.OnNpcMove(new Vector3(1005,22,1006),.5f);Call(mover,"LateUpdate");
                Call(world,"SpawnNpc",new NpcAppearance{Id=90008,Type=7,Hp=100,X=100000,Y=100000,Z=2000});var dead=world.NpcById(90008);dead.GetComponent<NpcInfo>().Hp=0;
                foreach(string name in names)
                    foreach(var file in manager.Load(name))
                        if(!name.StartsWith("DongResources_") || !file.Key.EndsWith(".sharedAssets")) try{store.Add(file.Key,file.Value);}catch(InvalidDataException){}
                foreach(string path in required)Check(factory.Locate(path).Ok,"Boss asset must resolve through the current real router: "+path);
                Field(world,"missingNpcVisuals",new Dictionary<int,float>{{90007,0}});Field(world,"nextNpcRepair",0f);
                Call(world,"UpdateNpcVisuals");
                Check(world.NpcById(90007)==holder&&holder.GetComponent<NpcInfo>()==stats&&stats.Hp==777&&holder.transform.position==new Vector3(1003,22,1004),"Repair cannot respawn, heal or reposition the enemy.");
                Check(!WorldEntities.HasNpcGeometry(dead),"A dead pending NPC must not be restored/resurrected.");
                Check(holder.GetComponent<RemoteMover>()==mover&&(Vector3)typeof(RemoteMover).GetField("target",Private).GetValue(mover)==new Vector3(1005,22,1006),"Model recovery must retain the existing movement controller and destination.");
                Check(WorldEntities.HasNpcGeometry(holder)&&holder.GetComponent<NpcAnimator>()!=null,"Real recovered boss must contain drawable geometry and its original animator.");
                Check(!holder.GetComponentsInChildren<NpcVisualPlaceholder>(true).Any(p=>p.gameObject.activeSelf),"Recovered boss must retire the placeholder.");
                var rendererCount=holder.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;Check(rendererCount>0,"Boss should have a real skinned mesh.");
                foreach(var renderer in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true))Check(renderer.sharedMesh.vertexCount>0&&renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.name!="Hidden/InternalErrorShader","Recovered boss material/mesh is invalid.");
                var animation=holder.GetComponentInChildren<Animation>(true);Check(animation!=null&&animation.GetClipCount()>0,"Recovered boss animation clips must be present.");
                Field(world,"nextNpcRepair",0f);Call(world,"UpdateNpcVisuals");Check(holder.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==rendererCount,"Repeated repair must not duplicate the boss model.");
                if(Capture!=null)await Capture(holder);
                File.WriteAllLines("Logs/npc-recovery/results.txt",new[]{"PASS: current model/textures resolve: "+string.Join("; ",mapping.Select(p=>p.Key+" -> "+p.Value)),"PASS: missing-model spawn, compatible placeholder, late asset recovery, unchanged holder/HP/position, skinned meshes/materials/animation, placeholder retirement and idempotence."});
                Debug.Log("[npc-recovery regression] Fusion Numbuh Two real assets, delayed loading, preserved HP/position, model/animation recovery and placeholder retirement passed.");
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
        }
    }
}
