using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenFusion.World;
using OpenFusion.Unity25;
using OpenFusion.Tables;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace OpenFusion.EditorTools
{
    public static class WeaponAudioRegressionChecks
    {
        static object[] Arr(object o) => o as object[] ?? new object[0];
        static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run()
        {
            var results = new List<string>(); var root = new GameObject("WeaponAudioRegression");
            var local = new GameObject("Local"); var remote = new GameObject("Remote");
            try
            {
                var rockets = root.AddComponent<RocketSystem>();
                var item = new OpenFusion.Tables.Gen.ItemElement { m_iAtkRange = 3000, m_iDurationTime = 30 };
                var start = Vector3.zero; var serverTarget = new Vector3(10, 1, 20); var target = serverTarget;
                var aimedTarget = new Vector3(10, -4, 20); var muzzle = new Vector3(1, 1.5f, 1);
                RocketSystem.ResolveFlight(true, true, false, Vector3.zero, muzzle, aimedTarget, ref start, ref target);
                Check(start == Vector3.up * .8f && target == aimedTarget, "Compatibility rocket lost reticle height or inherited native muzzle flattening");
                target = serverTarget;
                RocketSystem.ResolveFlight(true, false, false, Vector3.zero, muzzle, aimedTarget, ref start, ref target);
                Check(start == Vector3.up * .8f && target == serverTarget, "Remote rocket used local player's aim");
                RocketSystem.ResolveFlight(false, true, false, Vector3.zero, muzzle, aimedTarget, ref start, ref target);
                Check(start == muzzle && target.y == muzzle.y, "Native rocket response deviates from Legacy muzzle behavior");
                target = serverTarget;
                RocketSystem.ResolveFlight(true, true, true, Vector3.zero, muzzle, aimedTarget, ref start, ref target);
                Check(start == Vector3.up * .8f && target == serverTarget, "Grenade arc target changed by rocket compensation");
                results.Add("PASS: compatibility rocket preserves local reticle aim and Legacy origin; remote/native rockets and grenades retain their separate trajectories.");
                var spawn = typeof(RocketSystem).GetMethod("SpawnHead", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int i = 0; i < 3; i++) spawn.Invoke(rockets, new object[] { i, 1, item, false, Vector3.zero, Vector3.forward * 30, false, local.transform });
                Check(!rockets.CanShoot, "Own projectile limit must be three");
                spawn.Invoke(rockets, new object[] { 0, 1, item, false, Vector3.zero, Vector3.forward * 30, true, remote.transform });
                var heads = (IList)typeof(RocketSystem).GetField("heads", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rockets);
                Check(heads.Count == 4 && !rockets.CanShoot, "Remote projectile replaced own projectile");
                foreach (var head in heads) UnityEngine.Object.DestroyImmediate((GameObject)head.GetType().GetField("Go").GetValue(head));
                results.Add("PASS: three local slots plus simultaneous remote slot 0; owners remain isolated.");
                var clip25 = new Clip25 { Name = "stickattack1", SampleRate = 30 };
                foreach (string name in new[] { "swordtail_on", "sound", "attacksound", "swordtail_off" })
                    clip25.Events.Add(new Clip25.Ev25 { Time = .2f, Func = name, Data = "" });
                var clip = ClipBuilder.ToClip(clip25);
                Check(clip.events.Length == 4, "Melee/audio animation events lost during conversion");
                UnityEngine.Object.DestroyImmediate(clip);
                var animation = local.AddComponent<Animation>(); local.AddComponent<AnimEventSink>();
                var weapon = new GameObject("Weapon"); weapon.transform.SetParent(local.transform);
                foreach (string name in new[] { "Stag01", "Stag02", "Stag03", "Stag04" }) new GameObject(name).transform.SetParent(weapon.transform);
                MeleeTrail.Attach(weapon, animation, Color.red);
                Check(weapon.GetComponentsInChildren<MeleeTrail>().Length == 2, "All sequential blade marker pairs must get trails");
                local.GetComponent<AnimEventSink>().swordtail_on();
                Check(weapon.GetComponentsInChildren<MeshRenderer>().All(r => r.enabled), "Trail on event not delivered");
                local.GetComponent<AnimEventSink>().swordtail_off();
                Check(weapon.GetComponentsInChildren<MeshRenderer>().All(r => !r.enabled), "Trail off event not delivered");
                results.Add("PASS: melee sound/trail events retained; both blade pairs start/stop through animation receiver.");
                Check(Sfx.AudioBytes(new byte[8], uint.MaxValue, 20, false, out _) == null, "Malformed clip bounds accepted");
                Check(Sfx.AudioBytes(new byte[8], 0, 8, false, out _) == null, "Invalid audio signature accepted");
                results.Add("PASS: malformed audio rejected without out-of-bounds reads.");
                var present = new HashSet<string> { "sound/Aku_NanDance01.wav", "sound/Aku_NanDance02.wav", "sound/Aku_NanDance03.wav" };
                for (int seed = 0; seed < 100; seed++)
                    Check(present.Contains(AnimationAudioPaths.Choose("Aku_NanDance0(RAND:1-9).wav", present.Contains, seed)), "Random audio selected an absent variant");
                results.Add("PASS: animation random ranges use existing variants of the same sound family.");
                var box = new Bounds(new Vector3(0, 1, 2), new Vector3(1, 2, .1f));
                Check(RocketSystem.SweepBounds(box, Vector3.up, Vector3.forward * 4, .2f, out float distance) && distance > 1 && distance < 2, "Fast rocket tunneled through narrow target");
                Check(!RocketSystem.SweepBounds(box, Vector3.up + Vector3.right * 3, Vector3.forward * 4, .2f, out _), "Rocket hit a target outside its path");
                var info = remote.AddComponent<NpcInfo>(); var before = info.GetBounds(); remote.transform.position += Vector3.right * 12;
                Check(Mathf.Abs(info.GetBounds().center.x - before.center.x - 12) < .001f, "Cached target bounds lag behind moving NPC");
                Check(InventoryWindow.EquipmentMoveSound(1, 4, 0, 0) == "Weapon_Equipped" && InventoryWindow.EquipmentMoveSound(0, 1, 1, 4) == "Clothing_Unequipped" && InventoryWindow.EquipmentMoveSound(1, 1, 1, 2) == null, "Equipment move sound routing");
                results.Add("PASS: fast rocket crosses thin target; misses outside path; cached bounds follow moving NPC; equip sounds use acknowledged slot type.");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform);
                var origin = new Vector3(10000, 1, 10000); wall.transform.position = origin + Vector3.forward * 2; wall.transform.localScale = new Vector3(2, 3, .5f);
                Physics.SyncTransforms();
                Check(!CombatSystem.HasLineOfSight(origin, origin + Vector3.forward * 4, .5f, null, null), "Direct weapon attack passed through wall");
                wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms();
                Check(CombatSystem.HasLineOfSight(origin, origin + Vector3.forward * 4, .5f, null, null), "Nonblocking trigger prevented direct attack");
                var weaponRow = new OpenFusion.Tables.Gen.ItemElement { m_iEffect1 = 10, m_iEffect2 = 20 };
                var hud = new HudData { BatteryWpn = 100, UseWeaponBattery = true };
                Check(CombatSystem.WeaponBullet(weaponRow, hud) == 20, "Enabled boost ignored");
                hud.UseWeaponBattery = false; Check(CombatSystem.WeaponBullet(weaponRow, hud) == 10, "Disabled boost still used charged effects");
                hud.UseWeaponBattery = true; hud.BatteryWpn = 0; Check(CombatSystem.WeaponBullet(weaponRow, hud) == 10, "Empty battery still used charged effects");
                results.Add("PASS: direct attacks respect walls and ignore triggers; boost effects honor toggle and remaining battery.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(local); UnityEngine.Object.DestroyImmediate(remote);
                Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/weapon-audio-regression-results.txt", results);
            }
            AuditCache(results);
        }
        static IEnumerable<string> Expand(string path)
        {
            var match = Regex.Match(path, @"\(RAND:(\d+)-(\d+)\)");
            if (!match.Success) { yield return path; yield break; }
            int min = int.Parse(match.Groups[1].Value), max = Math.Min(min + 100, int.Parse(match.Groups[2].Value));
            for (int i = min; i <= max; i++) foreach (var value in Expand(path.Substring(0, match.Index) + i + path.Substring(match.Index + match.Length))) yield return value;
        }
        static void AuditCache(List<string> results)
        {
            string cache = Path.GetFullPath("../ffcache/238bd38f-3add-4dc5-a29a-06213461a206");
            if (!Directory.Exists(cache)) return;
            var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unsupported = new List<string>(); var env = new List<string>();
            var randomGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var animationNames = new HashSet<string>();
            OpenFusion.Tables.Gen.EquipmentItemTableScript weapons = null;
            int liveBulletRows = 0;
            var eventNames = new Dictionary<string, int>(); int clips = 0, audio = 0, files = 0;
            foreach (string file in Directory.EnumerateFiles(cache, "*", SearchOption.AllDirectories).Where(p => Path.GetFileName(p) != "__info" && !Path.GetFileName(Path.GetDirectoryName(p)).StartsWith("map_5f")))
            {
                var sf = SerializedFile.Parse(File.ReadAllBytes(file)); files++;
                var index = new AssetIndex(); index.Add(Path.GetFileName(file), sf);
                foreach (string key in index.Keys) assets.Add(key);
                foreach (var obj in sf.Objects)
                {
                    if (obj.ClassId == 114 && Path.GetFileName(Path.GetDirectoryName(file)) == "effects_2eresourceFile")
                    {
                        var data = sf.ReadObject(obj, out _);
                        if (data.ContainsKey("m_pBulletData"))
                        {
                            var rows = BulletFx.ReadRows(data); liveBulletRows = rows.Length;
                            Check(rows.Length >= 170 && rows[6].Succ == 770 && Mathf.Abs(rows[6].Hide - .4f) < .001f && rows[146].Succ == 773, "Mineral Hammer live projectile/impact table differs from game data");
                            foreach (var row in rows) if (!string.IsNullOrEmpty(row.Sound) && !row.Sound.Contains("...") && !row.Sound.Contains("\"")) required.Add("sound/" + row.Sound + ".wav");
                        }
                    }
                    if (obj.ClassId == 83)
                    {
                        audio++; if (Sfx.AudioBytes(sf.Data, obj.Offset, obj.Size, sf.BigEndian, out _) == null)
                            unsupported.Add(Path.GetFileName(Path.GetDirectoryName(file)) + ":" + obj.PathId);
                    }
                    else if (obj.ClassId == 74)
                    {
                        clips++; var data = sf.ReadObject(obj, out _); animationNames.Add(data["m_Name"] as string ?? "");
                        if (!data.TryGetValue("m_Events", out var events)) continue;
                        foreach (var value in Arr(events))
                        {
                            var e = Obj(value); string fn = e["functionName"] as string;
                            if (string.IsNullOrEmpty(fn)) continue;
                            eventNames[fn] = eventNames.TryGetValue(fn, out int n) ? n + 1 : 1;
                            if (fn == "sound" && e["data"] is string path && !string.IsNullOrEmpty(path))
                            {
                                if (path.Contains("(RAND:")) randomGroups.Add("sound/" + path);
                                foreach (var expanded in Expand("sound/" + path)) required.Add(expanded);
                            }
                        }
                    }
                }
                foreach (string key in index.Keys.Where(k => k.StartsWith("evsound/")))
                {
                    if (!index.TryGet(key, out var entry) || entry.Target.FileId != 0) continue;
                    var obj = sf.Objects.First(o => o.PathId == entry.Target.PathId); var data = sf.ReadObject(obj, out _);
                    if (!data.TryGetValue("EvSoundArray", out var rows)) continue;
                    int count = 0; foreach (var row in Arr(rows)) { if (Obj(row)["FileName"] is string name && !string.IsNullOrEmpty(name)) required.Add("enviro sound/" + name); count++; }
                    env.Add(key + ": " + count);
                }
                if (Path.GetFileName(Path.GetDirectoryName(file)) == "tabledata_2eresourceFile")
                {
                    var tables = TableLoader.Load(new Dictionary<string, byte[]> { [Path.GetFileName(file)] = sf.Data }).Xdt;
                    weapons = tables.m_pWeaponItemTable;
                    foreach (var row in tables.m_pFirstUseTable.m_pFirstUseString)
                        if (!string.IsNullOrWhiteSpace(row?.m_strComment1)) required.Add("fu sound/" + row.m_strComment1 + ".wav");
                    foreach (var row in tables.m_pWeaponItemTable.m_pItemSoundData)
                        if (row != null) foreach (string sound in new[] { row.m_pstrSoundString1, row.m_pstrSoundString2, row.m_pstrSoundString3 })
                            if (!string.IsNullOrEmpty(sound) && !sound.Contains("null") && !sound.Contains("...")) required.Add("sound/" + sound);
                    results.Add("WEAPONS: " + tables.m_pWeaponItemTable.m_pItemData.Length + " table rows inspected.");
                }
                // Release large uncompressed bundles between iterations on development PCs.
                if (files % 8 == 0) GC.Collect();
            }
            foreach (string file in Directory.EnumerateFiles("Assets/Scripts/OpenFusion", "*.cs", SearchOption.AllDirectories))
            {
                string code = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(code, "Sfx\\.Ui\\(\"([^\"]+)\"\\)")) required.Add("ui sound/" + m.Groups[1].Value + ".wav");
                foreach (Match m in Regex.Matches(code, "(?:Sfx\\.PlayAt|\\.Play2D|\\.At)\\(\"([^\"]+\\.(?:wav|ogg|mp3))\"")) required.Add(m.Groups[1].Value);
            }
            foreach (var row in JObject.Parse(File.ReadAllText("Assets/Resources/OpenFusion/Data/bullets.json"))["b"])
            {
                string sound = row[3].Value<string>();
                if (!string.IsNullOrEmpty(sound) && !sound.Contains("...") && !sound.Contains("\"")) required.Add("sound/" + sound + ".wav");
            }
            var music = JObject.Parse(File.ReadAllText("Assets/Resources/OpenFusion/Data/music.json"));
            foreach (string key in new[] { "m", "a" }) foreach (var row in music[key])
            { string sound = row[1].Value<string>(); if (!string.IsNullOrEmpty(sound) && sound != "none.ogg") required.Add("bgm/" + sound); }
            string mainFixture = Path.Combine(Path.GetTempPath(), "openfusion-main-audit.unity3d");
            if (File.Exists(mainFixture))
            {
                var bytes = File.ReadAllBytes(mainFixture); string derived = Path.GetFullPath("Temp/main-data-regression.json");
                if (File.Exists(derived)) File.Delete(derived);
                var main = MainGameData.LoadCached(bytes, derived);
                var selection = new OpenFusion.Assets.BundleStreamer(main.Manifest, null).FilesUpTo(OpenFusion.Assets.Phase.CharacterSelection);
                Check(selection.Contains("TableData.resourceFile"), "Character screens must have table data before world loading");
                Check(!selection.Any(OpenFusion.Assets.Phases.IsDongFile), "Character selection must not load map bundles");
                results.Add("PASS: character selection phases contain table data and no map/dong bundles (" + selection.Count + " shared bundles).");
                Check(!main.CacheHit && main.Projectiles?.Length == 202, "Current main.unity3d projectile table was not extracted");
                Check(main.Projectiles[6].Succ == 770 && main.Projectiles[146].Succ == 773, "Mineral Hammer embedded main impact IDs");
                var again = MainGameData.LoadCached(new byte[] { 0 }, derived, main.Hash);
                Check(again.CacheHit && again.Projectiles.Length == 202 && again.Manifest.Lists.Count == main.Manifest.Lists.Count, "Derived main cache re-entered LZMA decoding or lost data");
                File.WriteAllText(derived, "{broken"); var repaired = MainGameData.LoadCached(bytes, derived, main.Hash);
                Check(!repaired.CacheHit && repaired.Projectiles.Length == 202, "Corrupt derived main metadata was not repaired");
                liveBulletRows = main.Projectiles.Length;
                foreach (var row in main.Projectiles) if (!string.IsNullOrEmpty(row.Sound)) required.Add("sound/" + row.Sound + ".wav");
                foreach (var row in main.Music.Concat(main.Ambient)) if (!string.IsNullOrEmpty(row.File) && row.File != "none.ogg") required.Add("bgm/" + row.File);
                results.Add("PASS: current main.unity3d has 202 projectile rows; main overrides 170-row streamed table; derived cache skips LZMA on reuse and repairs corrupt JSON.");
                results.Add("MAIN MUSIC: " + main.Music.Length + " regions; " + main.Ambient.Length + " ambient regions.");
            }
            var weaponResults = new List<string>(); int models = 0;
            if (weapons != null) foreach (var item in weapons.m_pItemData.Where(i => i != null && i.m_iItemNumber > 0))
            {
                if (item.m_iMesh < 0 || item.m_iMesh >= weapons.m_pItemMeshData.Length) { weaponResults.Add("INVALID MESH ROW " + item.m_iItemNumber); continue; }
                var mesh = weapons.m_pItemMeshData[item.m_iMesh];
                foreach (string model in new[] { mesh.m_pstrMMeshModelString, mesh.m_pstrFMeshModelString })
                { models++; string path = "wear/" + model + ".nif"; if (!assets.Contains(path)) weaponResults.Add("MISSING MODEL item=" + item.m_iItemNumber + " " + path); }
            }
            weaponResults.Insert(0, $"WEAPON MODELS: {models} male/female references; {weaponResults.Count} unresolved.");
            if (weapons != null) foreach (var item in weapons.m_pItemData.Where(i => i != null && (i.m_iEffect1 >= liveBulletRows || i.m_iEffect2 >= liveBulletRows)))
                weaponResults.Add("MISSING PROJECTILE ROW item=" + item.m_iItemNumber + " effects=" + item.m_iEffect1 + "," + item.m_iEffect2);
            results.Add("EFFECTIVE BULLET TABLE: " + liveBulletRows + " rows; Mineral Hammer normal/boost impact IDs and delay verified against downloaded data.");
            foreach (string name in new[] { "stickattack1", "stickattack1upper", "stickattack2", "stickattack3", "rocketattack1", "rocketattack1upper" })
                weaponResults.Add((animationNames.Contains(name) ? "PRESENT CLIP " : "MISSING CLIP ") + name);
            File.WriteAllLines("Logs/weapon-model-audit.txt", weaponResults);
            var missing = required.Where(k => !assets.Contains(k)).OrderBy(k => k).ToArray();
            results.Add("RANDOM GROUPS: " + randomGroups.Count + "; without any available variant: " + randomGroups.Count(g => !AnimationAudioPaths.Variants(g).Any(assets.Contains)));
            results.Add($"CACHE: {files} serialized bundles; {audio} audio clips; {clips} animation clips; {assets.Count} asset paths.");
            results.Add($"REFERENCES: {required.Count} distinct sound paths; {missing.Length} absent from this cache; {unsupported.Count} unsupported audio payloads.");
            results.AddRange(eventNames.OrderBy(k => k.Key).Select(k => "EVENT " + k.Key + ": " + k.Value));
            results.AddRange(missing.Select(k => "MISSING " + k)); results.AddRange(unsupported.Select(k => "UNSUPPORTED " + k));
            File.WriteAllLines("Logs/weapon-audio-audit.txt", results);
            File.WriteAllLines("Logs/environment-audio-audit.txt", env);
            File.WriteAllLines("Logs/audio-asset-paths.txt", assets.Where(p => Regex.IsMatch(p, @"\.(wav|ogg|mp3)$", RegexOptions.IgnoreCase)).OrderBy(p => p));
            Debug.Log($"[weapon-audio] Audit complete: {audio} audio objects, {missing.Length} unresolved references, {unsupported.Count} unsupported payloads.");
        }
    }
}
