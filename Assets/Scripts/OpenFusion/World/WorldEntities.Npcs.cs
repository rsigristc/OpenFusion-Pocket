using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class NpcVisualPlaceholder : MonoBehaviour { }
    public sealed class WorldPlaceholderMaterial : MonoBehaviour
    {
        public Material Owned;
        void OnDestroy(){if(Owned!=null){if(Application.isPlaying)Destroy(Owned);else DestroyImmediate(Owned);}}
    }
    public sealed partial class WorldEntities
    {
        readonly Dictionary<int,float> missingNpcVisuals=new Dictionary<int,float>();
        float nextNpcRepair;
        GameObject BuildNpcVisual(GameObject holder,ModelRef model,int type)
        {
            GameObject visual=null;
            if(model.HNpcIndex>=0 && hnpcs?.TableElement!=null && model.HNpcIndex<hnpcs.TableElement.Length && avatars!=null)
            {var look=ActorLook.FromHNpc(hnpcs.TableElement[model.HNpcIndex]);visual=avatars.Assembler.Build(look,ActorColors.From(look),holder.transform);}
            else if(models!=null)visual=models.Instantiate(model,holder.transform);
            if(visual==null)return null;
            ActivateAll(visual);
            if(!HasNpcGeometry(visual)){ReleaseNpcVisual(visual);return null;}
            HideIfLocation(visual);visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
            var row=NpcRow(type);string[] paths=null;
            if(row!=null && row.m_iHNpc!=0 && row.m_iMesh>0 && NpcTable?.m_pNpcMeshData!=null && row.m_iMesh<NpcTable.m_pNpcMeshData.Length)
            {var mesh=NpcTable.m_pNpcMeshData[row.m_iMesh];if(mesh!=null)paths=new[]{mesh.m_pstrMMeshModelString,mesh.m_pstrMTextureString,mesh.m_pstrMTextureString2};}
            var animation=holder.GetComponent<NpcAnimator>()??holder.AddComponent<NpcAnimator>();animation.Init(visual,row,paths);
            if(row!=null && row.m_iBarkerType!=0 && NpcTable?.m_pNpcBarkerData!=null && row.m_iBarkerNumber>0 && row.m_iBarkerNumber<NpcTable.m_pNpcBarkerData.Length && holder.GetComponent<NpcBarker>()==null)
            {var b=NpcTable.m_pNpcBarkerData[row.m_iBarkerNumber];if(b!=null)holder.AddComponent<NpcBarker>().Init(new[]{b.m_strName,b.m_strComment,b.m_strComment1,b.m_strComment2},()=>Self!=null?Self.transform:null);}
            SeasonalEvents.Instance?.TrackNpc(visual,model,models,holder.GetComponent<NpcInfo>()?.Id??0);
            if(model.NpcType>=100)foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            return visual;
        }
        public static bool HasNpcGeometry(GameObject visual)
        {
            if(visual==null)return false;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.gameObject.name=="blobshadow" || renderer.GetComponentInParent<NpcVisualPlaceholder>()!=null || renderer.GetComponentInParent<NpcFxMark>()!=null)continue;
                if(renderer is SkinnedMeshRenderer skin && skin.sharedMesh!=null && skin.sharedMesh.vertexCount>0)return true;
                var filter=renderer.GetComponent<MeshFilter>();if(filter?.sharedMesh!=null && filter.sharedMesh.vertexCount>0)return true;
            }
            return false;
        }
        void AddNpcPlaceholder(GameObject holder)
        {
            var placeholder=new GameObject("NpcModelPlaceholder");placeholder.transform.SetParent(holder.transform,false);
            placeholder.AddComponent<NpcVisualPlaceholder>();Marker(placeholder,Color.yellow);
        }
        static void ReleaseNpcVisual(GameObject visual)
        {visual.SetActive(false);if(Application.isPlaying)Destroy(visual);else DestroyImmediate(visual);}
        void UpdateNpcVisuals()
        {
            if(models==null || Time.unscaledTime<nextNpcRepair || missingNpcVisuals.Count==0)return;
            nextNpcRepair=Time.unscaledTime+.25f;
            int candidate=0;float nearest=float.PositiveInfinity;var remove=new List<int>();
            foreach(var entry in missingNpcVisuals)
            {
                if(!npcs.TryGetValue(entry.Key,out var holder)||holder==null){remove.Add(entry.Key);continue;}
                var info=holder.GetComponent<NpcInfo>();if(info==null || info.Hp<=0){remove.Add(entry.Key);continue;}
                if(Time.unscaledTime<entry.Value)continue;
                var model=catalog?.Npc(info.NpcNumber);if(model==null || model.HNpcIndex<0 && !models.IsAvailable(model))continue;
                float distance=Self!=null?(holder.transform.position-Self.transform.position).sqrMagnitude:0;
                if(distance<nearest){nearest=distance;candidate=entry.Key;}
            }
            foreach(int id in remove)missingNpcVisuals.Remove(id);
            if(candidate==0)return;
            var target=npcs[candidate];var data=target.GetComponent<NpcInfo>();var definition=catalog.Npc(data.NpcNumber);
            var visual=BuildNpcVisual(target,definition,data.NpcNumber);
            if(visual==null){missingNpcVisuals[candidate]=Time.unscaledTime+2f;return;}
            foreach(var placeholder in target.GetComponentsInChildren<NpcVisualPlaceholder>(true))ReleaseNpcVisual(placeholder.gameObject);
            data.InvalidateBounds();target.GetComponent<RemoteMover>()?.RebindAnimation();missingNpcVisuals.Remove(candidate);
            UnityEngine.Debug.Log("[npc-model] recovered id="+candidate+" type="+data.NpcNumber+" path="+definition.Model+" hp="+data.Hp);
        }
    }
}
