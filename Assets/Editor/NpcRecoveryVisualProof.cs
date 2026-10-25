using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenFusion.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    [InitializeOnLoad]
    public static class NpcRecoveryVisualProof
    {
        const string Flag="OpenFusion.NpcRecoveryVisualProof";
        static NpcRecoveryVisualProof(){EditorApplication.playModeStateChanged+=Changed;}
        public static void Batch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Proof frame camera").AddComponent<Camera>();camera.tag="MainCamera";camera.cullingMask=0;
            SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
        }
        static async void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag,false))return;
            SessionState.SetBool(Flag,false);
            try{ShaderUtil.allowAsyncCompilation=false;NpcRecoveryRegressionChecks.Capture=Capture;await NpcRecoveryRegressionChecks.Run();EditorApplication.Exit(0);}
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
            finally{NpcRecoveryRegressionChecks.Capture=null;}
        }
        static async Task Capture(GameObject holder)
        {
            holder.GetComponent<RemoteMover>().enabled=false;
            foreach(var t in holder.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            var renderers=holder.GetComponentsInChildren<SkinnedMeshRenderer>(true);foreach(var renderer in renderers)renderer.updateWhenOffscreen=true;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var host=new GameObject("Boss proof camera");var camera=host.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.orthographic=true;
            camera.orthographicSize=Mathf.Max(bounds.extents.x,bounds.extents.y)*1.25f;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.transform.position=bounds.center+new Vector3(0,0,Mathf.Max(5,bounds.size.magnitude*2));camera.transform.LookAt(bounds.center);
            Camera.main.transform.position=camera.transform.position;
            Shader.SetGlobalVector("_OF_Light0Dir",new Vector4(.3f,.8f,.5f,0));Shader.SetGlobalColor("_RimColor",SeasonalEvents.HalloweenRim);
            Shader.DisableKeyword("FOG_LINEAR");Shader.DisableKeyword("FOG_EXP");Shader.DisableKeyword("FOG_EXP2");
            var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);target.Create();Texture2D image=null;
            try
            {
                PreviewRender.Render(camera,target);await Task.Delay(120);PreviewRender.Render(camera,target);
                var previous=RenderTexture.active;RenderTexture.active=target;image=new Texture2D(512,512,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();RenderTexture.active=previous;
                var pixels=image.GetPixels32();int opaque=pixels.Count(p=>p.a>127),pink=pixels.Count(p=>p.a>127&&p.r>220&&p.b>220&&p.g<40);
                File.WriteAllBytes("Logs/npc-recovery/fusion-numbuh-two.png",image.EncodeToPNG());
                if(opaque<1000||pink>opaque/100)throw new Exception("Boss render is empty or contains error-magenta pixels: "+opaque+" / "+pink);
                Debug.Log("[npc-recovery visual] actual boss opaque pixels="+opaque+" error-magenta="+pink);
            }
            finally{if(image!=null)UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(host);}
        }
    }
}
