using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public static class TutorialAssets
    {
        public static readonly int[] Npcs={2669,2670,2671,2672,2673,2674,2675,2676,2677,2678,2694,2695,2696,2800};
        public static readonly int[] Effects={366,372,394,668,740,750};
        public static string EffectPath(int id)=>"prefabs/particle/effectscripts/es["+id+"].prefab";
        public static IEnumerable<string> Required(ModelCatalog catalog,PcAppearance appearance,PlayerAvatarFactory avatars)
        {
            foreach(var image in TutorialTraining.IllustrationNames)yield return "tutorialassets/"+image+".png";
            foreach(int effect in Effects)yield return EffectPath(effect);
            foreach(int npc in Npcs){var model=catalog.Npc(npc);if(!string.IsNullOrEmpty(model?.Model))yield return model.Model;if(!string.IsNullOrEmpty(model?.Tex1))yield return model.Tex1;}
            var nano=catalog.Nano(1);if(nano?.Model!=null)yield return nano.Model;
            if(appearance!=null&&avatars!=null)foreach(string path in avatars.Required(appearance))yield return path;
            var weapon=catalog.Item(EquipSlot.Weapon,328);if(weapon?.Model!=null)yield return weapon.Model;
            if(weapon?.ModelFemale!=null)yield return weapon.ModelFemale;
            if(weapon?.Tex1!=null)yield return weapon.Tex1;
        }
        public static void Validate(ModelFactory models,XdtTableScript tables,PcAppearance appearance,PlayerAvatarFactory avatars)
        {
            foreach(int npc in new[]{2694,2695,2696})TutorialTraining.WarpDestination(tables,npc);
            foreach(int task in new[]{2248,2249,2250,2251,2252,2253,2254})if(!tables.m_pMissionTable.m_pMissionData.Any(m=>m!=null&&m.m_iHTaskID==task))throw new InvalidOperationException("Missing tutorial mission "+task);
            var missing=Required(ModelCatalog.From(tables),appearance,avatars).Where(p=>!models.Locate(p).Ok).Distinct().ToArray();
            if(missing.Length>0)throw new InvalidOperationException("Missing tutorial resources: "+string.Join(", ",missing.Take(8))+". Return to launcher and retry loading the game build.");
        }
        // Nested iterator exceptions must fault the awaited task, not leave startup
        // waiting forever after Unity silently stops an inner coroutine.
        public static IEnumerator Guard(IEnumerator routine,Action<Exception> failed)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0){object next=null;bool moved=false;Exception error=null;
                try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                catch(Exception e){error=e;}
                if(error!=null){failed(error);yield break;}
                if(!moved){try{(stack.Pop() as IDisposable)?.Dispose();}catch(Exception e){error=e;}if(error!=null){failed(error);yield break;}continue;}
                if(next is IEnumerator nested)stack.Push(nested);else yield return next;
            }
        }
    }
}
