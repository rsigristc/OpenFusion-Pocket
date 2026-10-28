using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using OpenFusion.Api;
using OpenFusion.Assets;
using OpenFusion.Tables;
using OpenFusion.World;

namespace OpenFusion.EditorTools
{
    // Enumerates actual current data. Reference resolution is not a visual,
    // gameplay, server transaction or full Legacy-equivalence certification.
    public static class LegacySystemAudit
    {
        public static async void Batch(){try{SocialGroupRegressionChecks.Run();await Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static async Task Run()
        {
            Directory.CreateDirectory("Logs/legacy-audit");
            var api=new ServerApi("https://api.ffretrobution.net");var info=await api.GetInfo();var chain=await api.GetChain(info.GameVersions.First());
            var cache=new BundleCache(Path.GetFullPath("Logs/retrobution-303/live"),BundleCatalog.FromChain(chain));
            var main=await cache.EnsureMainFileAsync(chain[0]);var game=MainGameData.FromMain(main);
            var names=new[]{"TableData.resourceFile","Tutorial.resourceFile","Effects.resourceFile","Nano.resourceFile","FutureNano.resourceFile","NpcTexture.resourceFile","CharacterSelection.resourceFile","Retro_shared.resourceFile","Retro_shared_part2.resourceFile"}.Where(n=>chain[0].bundles.ContainsKey(n)).ToArray();
            await cache.EnsureAsync(names);var tables=TableLoader.Load(cache.Load("TableData.resourceFile")).Xdt;
            var manager=new BundleManager(cache,game.Manifest);var store=new AssetStore();var builder=new DongBuilder(store);
            foreach(string name in names)foreach(var f in manager.Load(name))try{store.Add(f.Key,f.Value);}catch(InvalidDataException){}
            var factory=new ModelFactory(store,manager,builder){GameData=game};var catalog=ModelCatalog.From(tables);
            var skills=tables.m_pSkillTable.m_pSkillData.Where(s=>s!=null&&s.m_iSkillNumber>0).GroupBy(s=>s.m_iSkillNumber).ToDictionary(g=>g.Key,g=>g.First());
            var nanoRows=new JArray();var usedSkills=new HashSet<int>();var effectIds=new HashSet<int>();var bulletIds=new HashSet<int>();
            foreach(var nano in tables.m_pNanoTable.m_pNanoData.Where(n=>n!=null&&n.m_iNanoNumber>0)) {
                var powers=new JArray();
                foreach(int tune in nano.m_iTune??Array.Empty<int>()) {
                    var tunes=tables.m_pNanoTable.m_pNanoTuneData;var td=tune>0&&tune<tunes.Length?tunes[tune]:null;
                    var s=td!=null&&skills.TryGetValue(td.m_iSkillID,out var found)?found:null;
                    if(s!=null)usedSkills.Add(s.m_iSkillNumber);
                    string gate=s==null?"missing-skill":s.m_iBatteryDrainType!=1?"passive-or-nonactive":s.m_iEffectType!=1?"no-active-request-path":
                        s.m_iSkillType==27||s.m_iSkillType==28?"recall-instance-point-gated-gameplay-unverified":new[]{1,2,3,5,6}.Contains(s.m_iEffectTarget)?"request-path-present-gameplay-unverified":"target-mode-not-handled";
                    powers.Add(new JObject{["tune"]=tune,["skill"]=td?.m_iSkillID??0,["skillType"]=s?.m_iSkillType??0,["effectType"]=s?.m_iEffectType??0,["targetMode"]=s?.m_iEffectTarget??0,["sourceEffect"]=s?.m_iEffect??0,["buffEffect"]=s?.m_iBuffEffect??0,["targetBullet"]=s?.m_iTargetEffect??0,["gate"]=gate,["resultStride"]=s!=null?SkillResults.Stride(s.m_iSkillType):0});
                }
                int ni=nano.m_iNanoName;var ns=tables.m_pNanoTable.m_pNanoStringData;
                nanoRows.Add(new JObject{["id"]=nano.m_iNanoNumber,["name"]=ni>=0&&ni<ns.Length?ns[ni]?.m_strName:"",["modelReferenceResolves"]=factory.IsAvailable(catalog.Nano(nano.m_iNanoNumber)),["powers"]=powers});
            }
            var npcSkills=new HashSet<int>();foreach(var npc in tables.m_pNpcTable.m_pNpcData.Where(n=>n!=null))foreach(int skill in new[]{npc.m_iActiveSkill1,npc.m_iActiveSkill2,npc.m_iSupportSkill})if(skill>0)npcSkills.Add(skill);
            foreach(int id in usedSkills.Concat(npcSkills).Distinct())if(skills.TryGetValue(id,out var s)){if(s.m_iEffect>0)effectIds.Add(s.m_iEffect);if(s.m_iBuffEffect>0)effectIds.Add(s.m_iBuffEffect);if(s.m_iTargetEffect>0)bulletIds.Add(s.m_iTargetEffect);}
            var invalidBullets=new JArray();foreach(int id in bulletIds){if(game.Projectiles==null||id>=game.Projectiles.Length){invalidBullets.Add(id);continue;}var b=game.Projectiles[id];foreach(int effect in new[]{b.Part,b.Fire,b.Succ})if(effect>0)effectIds.Add(effect);}
            var missingEffects=new JArray(effectIds.Where(id=>!factory.Locate(TutorialAssets.EffectPath(id)).Ok).OrderBy(id=>id));
            var instanceRows=new JArray();var it=tables.m_pInstanceTable;
            for(int i=0;i<it.m_pInstanceData.Length;i++){var row=it.m_pInstanceData[i];if(row==null||row.m_iInstanceNameID<=0&&row.m_iZoneX==0&&row.m_iZoneY==0)continue;int ni=row.m_iInstanceNameID;instanceRows.Add(new JObject{["id"]=i,["name"]=ni>=0&&ni<it.m_pWarpNameData.Length?it.m_pWarpNameData[ni]?.m_pstrNameString:"",["zoneX"]=row.m_iZoneX,["zoneY"]=row.m_iZoneY,["racing"]=row.m_iIsEP>0,["gameplayValidated"]=false});}
            var warps=new JArray();foreach(var w in it.m_pWarpData.Where(w=>w!=null&&w.m_iWarpNumber>0))warps.Add(new JObject{["id"]=w.m_iWarpNumber,["npc"]=w.m_iNpcNumber,["instance"]=w.m_iIsInstance,["map"]=w.m_iToMapNum,["taskRestriction"]=w.m_iLimit_TaskID,["levelRestriction"]=w.m_iLimit_Level,["itemRestriction"]=w.m_iLimit_ItemID,["cost"]=w.m_iCost});
            var weapons=new JArray();foreach(var w in tables.m_pWeaponItemTable.m_pItemData.Where(w=>w!=null&&w.m_iItemNumber>0))weapons.Add(new JObject{["id"]=w.m_iItemNumber,["equipType"]=w.m_iEquipType,["singleRating"]=w.m_iPointRat,["areaRating"]=w.m_iGroupRat,["gameplayValidated"]=false});
            var reviveItems=new JArray(tables.m_pGeneralItemTable.m_pItemData.Where(i=>i!=null&&i.m_iItemNumber>0&&i.m_iItemType==10).Select(i=>i.m_iItemNumber));
            var allTypes=new JArray(skills.Values.GroupBy(s=>s.m_iSkillType).OrderBy(g=>g.Key).Select(g=>new JObject{["type"]=g.Key,["rows"]=g.Count(),["resultStride"]=SkillResults.Stride(g.Key),["nanoReferenced"]=g.Count(s=>usedSkills.Contains(s.m_iSkillNumber)),["npcReferenced"]=g.Count(s=>npcSkills.Contains(s.m_iSkillNumber))}));
            var report=new JObject{["generatedUtc"]=DateTime.UtcNow.ToString("O"),["scope"]="Current Retrobution tables, manifest and shared reference resolution. No claims of full visual/gameplay parity; no purchases, invitations or resource consumption.",
                ["nanos"]=nanoRows,["skillTypes"]=allTypes,["npcSkillIds"]=new JArray(npcSkills.OrderBy(i=>i)),["missingNpcSkillRows"]=new JArray(npcSkills.Where(i=>!skills.ContainsKey(i))),["effectReferenceCount"]=effectIds.Count,["unresolvedEffectIds"]=missingEffects,["invalidTargetBulletRows"]=invalidBullets,
                ["instances"]=instanceRows,["warps"]=warps,["mapBundles"]=new JArray(chain[0].bundles.Keys.Where(n=>n.StartsWith("Map_")).OrderBy(n=>n)),["weapons"]=weapons,["revivalItemIds"]=reviveItems};
            File.WriteAllText("Logs/legacy-audit/catalog.json",report.ToString());
            File.WriteAllLines("Logs/legacy-audit/catalog-summary.txt",new[]{"Nano rows: "+nanoRows.Count,"Distinct Nano skill references: "+usedSkills.Count,"Distinct NPC active/support skill references: "+npcSkills.Count,"Instance rows: "+instanceRows.Count,"Warp rows: "+warps.Count,"Weapon rows: "+weapons.Count,"Referenced effect IDs: "+effectIds.Count,"Unresolved shared effect references: "+missingEffects.Count,"Invalid target projectile rows: "+invalidBullets.Count,"Revival item IDs: "+reviveItems.Count,"These are enumerated data/reference checks, not completed gameplay tests."});
            Debug.Log("[legacy-system audit] catalog exported; "+nanoRows.Count+" Nanos, "+instanceRows.Count+" instances, "+weapons.Count+" weapons. Gameplay parity is not certified.");
        }
    }
}
