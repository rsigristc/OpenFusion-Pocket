using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OpenFusion.Api;
using OpenFusion.Assets;
using OpenFusion.Materials;
using OpenFusion.Tables;
using OpenFusion.Unity25;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class Retrobution303RegressionChecks
    {
        public static Func<ModelFactory, ModelCatalog, OpenFusion.Tables.Gen.XdtTableScript, Task> AssetsReady;
        public static async void Batch()
        {
            try { await Run(); UnityEditor.EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
        }
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static async Task Run()
        {
            var results = new List<string>();
            var previous = SeasonalEvents.Instance;
            var field = typeof(SeasonalEvents).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var host = new GameObject("Retrobution303Regression");
            try
            {
                var seasonal = host.AddComponent<SeasonalEvents>();
                field.SetValue(null, seasonal);
                Check(SeasonalEvents.TryParse("\uFEFF Halloween,eventsound,\n", out bool active, out bool sound) && active && sound, "Seasonal endpoint response parsing failed.");
                Check(!SeasonalEvents.TryParse("<html>404</html>", out _, out _), "An HTTP error page cannot activate a season.");
                Check(SeasonalEvents.TryParse("none", out active, out sound) && !active && !sound, "The server must be able to disable the event.");
                var endpoints = SeasonalEvents.Endpoints("https://server.test/academy", "http://cdn.test/build/main.unity3d");
                Check(endpoints.SequenceEqual(new[] { "https://server.test/academy/themedEvent.php", "http://cdn.test/build/themedEvent.php" }), "Event URLs must preserve API subpaths and CDN version directories.");
                seasonal.Apply(true, true);
                Color fog = Color.white, sky = Color.blue, light = Color.white, rim = Color.red;
                float density = SeasonalEvents.Ambience(ref fog, ref sky, ref light, ref rim, 2, false);
                Check(Mathf.Approximately(density, .033f) && rim == SeasonalEvents.HalloweenRim && light == new Color(.5f,.5f,.6f,1), "Halloween free/tutorial ambience differs from 3.0.3.");
                density = SeasonalEvents.Ambience(ref fog, ref sky, ref light, ref rim, 2, true);
                Check(Mathf.Approximately(density, .014f), "Halloween paid-zone fog differs from Legacy.");
                Check(SeasonalEvents.Sound("Sector V.ogg", false, true) == "Halloween.ogg" && SeasonalEvents.Sound("ambient.ogg", false, false) == "Ambient Halloween.ogg", "Halloween soundtrack routing failed.");
                Check(SeasonalEvents.Sound("Main Theme.ogg", false, true) == "Main Theme.ogg" && SeasonalEvents.Sound("lair.ogg", true, true) == "lair.ogg" && SeasonalEvents.Sound("none.ogg", false, false) == "none.ogg" && SeasonalEvents.Sound("Ambient IceSpire.ogg", false, false) == "Ambient IceSpire.ogg", "Lobby/instance/silence/IceSpire exclusions must survive seasonal overrides.");
                Check(SeasonalEvents.Delay(600) == 120, "Seasonal replay delay must match GameEventManager.");
                seasonal.Apply(false, false);
                fog = Color.cyan; sky = Color.red; light = Color.white; rim = Color.magenta;
                Check(Mathf.Approximately(SeasonalEvents.Ambience(ref fog, ref sky, ref light, ref rim, 2, true), .01f) && fog == Color.cyan && rim == Color.magenta && SeasonalEvents.Sound("world.ogg", false, true) == "world.ogg", "Disabling the event must restore ordinary map colours/audio.");
                Check(SeasonalNpcCosmetic.Hats.Length == 5 && SeasonalNpcCosmetic.Hats.All(h => !h.Model.ToLowerInvariant().Contains("billy")), "Billy Disguise must not be in the Halloween rotation.");
                for (int i = -20; i < 20; i++) Check(SeasonalNpcCosmetic.Pick("mob/npc_frogwarp.kfm", i) == 2, "Warp Frogs must always use Black Hat's Top Hat.");
                var spec = new MaterialSpec { ShaderName = "Custom/SkinnedToonShading_blendSrcalphaInvsrcalpha_Rim", Name = "rim-test", Shader = ShaderCatalog.All["Custom/SkinnedToonShading_blendSrcalphaInvsrcalpha_Rim"] };
                spec.Floats["_RimPower"] = 8; spec.Floats["_RimIntensity"] = .4f;
                var material = MaterialBinder.Create(spec, _ => null);
                try { Check(material != null && material.IsKeywordEnabled("_OF_RIM") && material.GetFloat("_RimPower") == 8 && Mathf.Approximately(material.GetFloat("_RimIntensity"), .4f), "Original rim material parameters were dropped by the shader binder."); }
                finally { if (material != null) UnityEngine.Object.DestroyImmediate(material); }
                results.Add("PASS: server-driven event parsing, API/CDN paths, exact rim/fog values, music exclusions, ordinary-mode restoration, five hats without Billy, frog restriction and material bindings.");
                var api = new ServerApi("https://api.ffretrobution.net");
                var info = await api.GetInfo(); var chain = await api.GetChain(info.GameVersions.First());
                var cache = new BundleCache(Path.GetFullPath("Logs/retrobution-303/live"), BundleCatalog.FromChain(chain));
                var main = await cache.EnsureMainFileAsync(chain[0]); var manifest = MainGameData.FromMain(main).Manifest;
                var required = new HashSet<string>(SeasonalNpcCosmetic.Hats.SelectMany(h => new[] { h.Model, h.Texture }), StringComparer.OrdinalIgnoreCase) {
                    "tutorialassets/sky_night_back.psd", "tutorialassets/fusionstar-1.dds", "bgm/halloween.ogg", "bgm/ambient halloween.ogg", "mob/mob_sneakyspawn.kfm", "mob/npc_frogwarp.kfm"
                };
                await cache.EnsureAsync(new[] { "TableData.resourceFile" });
                var tables = TableLoader.Load(cache.Load("TableData.resourceFile")).Xdt;
                var itemModels = ModelCatalog.From(tables);
                var itemSlots = new Dictionary<int, EquipSlot> { { 1, EquipSlot.Shirts }, { 2, EquipSlot.Pants }, { 3, EquipSlot.Shoes }, { 4, EquipSlot.Hat }, { 6, EquipSlot.Back } };
                foreach (var slot in itemSlots)
                {
                    var table = ItemTbl.Eq(tables, slot.Key);
                    foreach (var row in table.m_pItemData)
                    {
                        if (row == null || row.m_iItemName < 0 || row.m_iItemName >= table.m_pItemStringData.Length ||
                            (table.m_pItemStringData[row.m_iItemName]?.m_strName ?? "").IndexOf("Mystery Mech", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var model = itemModels.Item(slot.Value, row.m_iItemNumber); Check(model != null, "Mystery Mech model table entry missing.");
                        foreach (string path in new[] { model.Model, model.ModelFemale, model.Tex1, model.Tex2, model.TexF1, model.TexF2 })
                            if (!string.IsNullOrEmpty(path)) required.Add(path);
                    }
                }
                var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ffcache/238bd38f-3add-4dc5-a29a-06213461a206"));
                foreach (string bundle in chain[0].bundles.Keys.Where(b => !b.StartsWith("Map_") && !b.StartsWith("DongResources_")))
                {
                    string directory = Path.Combine(root, FfCacheBundleSource.Escape(bundle)); if (!Directory.Exists(directory)) continue;
                    foreach (string file in Directory.GetFiles(directory).Where(f => Path.GetFileName(f).StartsWith("CustomAssetBundle", StringComparison.OrdinalIgnoreCase)))
                    {
                        var sf = SerializedFile.Parse(File.ReadAllBytes(file)); var index = new AssetIndex(); index.Add(Path.GetFileName(file), sf);
                        foreach (string path in required) if (index.Has(path)) mapping[path] = bundle;
                    }
                    GC.Collect();
                }
                Check(required.All(mapping.ContainsKey), "Seasonal paths missing from resource index: " + string.Join(", ", required.Where(p => !mapping.ContainsKey(p))));
                var names = mapping.Values.Concat(new[] { "CharacterCreation.resourceFile", "CharacterSelection.resourceFile", "NpcTexture.resourceFile", "TableData.resourceFile" }).Distinct().ToArray();
                await cache.EnsureAsync(names);
                var bundles = new BundleManager(cache, manifest); var store = new AssetStore();
                foreach (string bundle in names) foreach (var file in bundles.Load(bundle)) if (file.Key != "__info") store.Add(file.Key, file.Value);
                var models = new ModelFactory(store, bundles, new DongBuilder(store));
                foreach (string path in required) Check(models.Locate(path).Ok, "Current 3.0.3 resource unavailable through the runtime router: " + path);
                results.Add("PASS: latest manifest " + chain[0].name + " / " + chain[0].uuid + "; all seasonal models, textures, moon, sky and audio resolve through the real router, from hash-verified CDN bundles.");
                if (AssetsReady != null) await AssetsReady(models, itemModels, tables);
                seasonal.Apply(true, true);
                foreach (var fixture in Enumerable.Range(0, 5).Select(seed => (path: "mob/mob_sneakyspawn.kfm", seed)).Concat(new[] { (path: "mob/npc_frogwarp.kfm", seed: 2) }))
                {
                    string path = fixture.path;
                    GameObject model = null;
                    try
                    {
                        var data = new ModelRef { Kind = ModelKind.Npc, Model = path, Scale = 1 };
                        model = models.Instantiate(data); Check(model != null, "Could not instantiate " + path); WorldEntities.ActivateAll(model);
                        var tail = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Tail01"); var originalScale = tail != null ? tail.localScale : Vector3.one;
                        seasonal.TrackNpc(model, data, models, fixture.seed);
                        var cosmetic = model.GetComponent<SeasonalNpcCosmetic>();
                        Check(cosmetic != null && cosmetic.AttachedHat != null && cosmetic.AttachedHat.GetComponentsInChildren<Renderer>(true).Length > 0, "Seasonal hat/bone attachment missing on " + path);
                        cosmetic.Refresh(); Check(model.GetComponentsInChildren<Transform>(true).Count(t => t.name == "SeasonalHalloweenHat") == 1, "Repeated updates duplicated a hat.");
                        seasonal.Apply(false, false); Check(cosmetic.AttachedHat == null && (tail == null || tail.localScale == originalScale), "Disabling event must remove hat and restore animated tail.");
                        seasonal.Apply(true, true); Check(cosmetic.AttachedHat != null, "Re-enabling event must update an already spawned NPC.");
                        results.Add("PASS: real " + path + " + " + SeasonalNpcCosmetic.Hats[SeasonalNpcCosmetic.Pick(path, fixture.seed)].Model + ", renderer/bone, idempotent refresh, disable/restore/re-enable.");
                    }
                    finally { if (model != null) UnityEngine.Object.DestroyImmediate(model); }
                }
                int mech = 0;
                for (int type = 0; type <= 6; type++)
                {
                    var table = ItemTbl.Eq(tables, type); if (table == null) continue;
                    foreach (var row in table.m_pItemData)
                    {
                        if (row == null || row.m_iItemName < 0 || row.m_iItemName >= table.m_pItemStringData.Length) continue;
                        string name = table.m_pItemStringData[row.m_iItemName]?.m_strName ?? "";
                        if (name.IndexOf("Mystery Mech", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        mech++; results.Add("ITEM: type=" + type + " id=" + row.m_iItemNumber + " " + name);
                    }
                }
                Check(mech > 0, "Mystery Mech equipment is missing from live tables.");
                results.Add("PASS: Mystery Mech item records decode with current item tables; C.R.A.T.E. distribution remains server-authoritative.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host); field.SetValue(null, previous);
                File.WriteAllLines("Logs/retrobution-303/regression-results.txt", results);
            }
            Debug.Log("[retrobution-3.0.3] " + string.Join("\n", results));
        }
    }
}
