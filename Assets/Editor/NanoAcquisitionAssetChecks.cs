using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using OpenFusion.Api;
using OpenFusion.Assets;
using OpenFusion.Tables;
using OpenFusion.Unity25;
using OpenFusion.World;

namespace OpenFusion.EditorTools
{
    public static class NanoAcquisitionAssetChecks
    {
        public static async void Batch() { try { await Run(); EditorApplication.Exit(0); } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); } }
        static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
        public static async Task Run()
        {
            Directory.CreateDirectory("Logs/nano-acquisition");
            var api = new ServerApi("https://api.ffretrobution.net"); var info = await api.GetInfo(); var chain = await api.GetChain(info.GameVersions.First());
            var cache = new BundleCache(Path.GetFullPath("Logs/retrobution-303/live"), BundleCatalog.FromChain(chain));
            var main = await cache.EnsureMainFileAsync(chain[0]); var gameData = MainGameData.FromMain(main);
            var names = new[] { "TableData.resourceFile", "Nano.resourceFile", "FutureNano.resourceFile", "NpcTexture.resourceFile", "Effects.resourceFile", "CharacterSelection.resourceFile", "Retro_shared.resourceFile", "Retro_shared_part2.resourceFile" }.Where(n => chain[0].bundles.ContainsKey(n)).ToArray();
            await cache.EnsureAsync(names);
            var tables = TableLoader.Load(cache.Load("TableData.resourceFile")).Xdt;
            var manager = new BundleManager(cache, gameData.Manifest); var store = new AssetStore(); var builder = new DongBuilder(store);
            foreach (string name in names) foreach (var file in manager.Load(name)) try { store.Add(file.Key, file.Value); } catch (InvalidDataException) { }
            var factory = new ModelFactory(store, manager, builder) { GameData = gameData }; var model = ModelCatalog.From(tables).Nano(2);
            Check(model?.Model != null && factory.IsAvailable(model), "The real Numbuh Two Nano model must be available for the award.");
            var go = factory.Instantiate(model); Check(go != null, "Numbuh Two Nano could not be instantiated.");
            try {
                var animation = go.GetComponentInChildren<Animation>(true); Check(animation != null, "Acquired Nano must have an Animation component.");
                foreach (string clip in new[] { "call", "stand1", "skill1", "skill2", "skill3" }) Check(animation[clip] != null && animation[clip].length > 0, "Missing real award/choice animation: " + clip);
                foreach (var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Check(renderer.sharedMesh != null && renderer.sharedMesh.vertexCount > 0 && renderer.sharedMaterial?.shader?.name != "Hidden/InternalErrorShader", "Acquired Nano mesh/material is invalid.");
                foreach (int tune in tables.m_pNanoTable.m_pNanoData[2].m_iTune) {
                    var power = tables.m_pNanoTable.m_pNanoTuneData[tune];
                    Check(power != null && power.m_iSkillID > 0 && tables.m_pNanoTable.m_pNanoTuneStringData[power.m_iTuneName] != null, "Each initial choice must resolve its real skill and description.");
                }
                var effectsHost = new GameObject("Acquisition FX audit");
                try {
                    var fx = effectsHost.AddComponent<BulletFx>(); fx.Init(factory);
                    Check(fx.RowOf(76) != null && fx.RowOf(77) != null, "Legacy award projectiles must exist in the current game table.");
                    Check(factory.Locate("Prefabs/Particle/EffectScripts/ES[705].prefab").Ok, "Legacy Nano creation effect 705 must resolve.");
                } finally { UnityEngine.Object.DestroyImmediate(effectsHost); }
                File.WriteAllText("Logs/nano-acquisition/assets.txt", "PASS: current Numbuh Two Nano model " + model.Model + ", call/stand1/skill1/skill2/skill3 clips, skinned mesh/material, three real powers/descriptions, creation effect 705 and projectile rows 76/77.\n");
                Debug.Log("[nano-acquire] current asset audit passed");
            } finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
