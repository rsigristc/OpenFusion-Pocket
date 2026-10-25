using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenFusion.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFusion.EditorTools
{
    [InitializeOnLoad]
    public static class Retrobution303VisualProof
    {
        const string Flag = "OpenFusion.Retrobution303VisualProof";
        static Retrobution303VisualProof() { EditorApplication.playModeStateChanged += Changed; }
        public static void Batch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Proof frame camera").AddComponent<Camera>(); camera.cullingMask = 0; camera.tag = "MainCamera";
            SessionState.SetBool(Flag,true); EditorApplication.isPlaying = true;
        }
        static async void Changed(PlayModeStateChange state)
        {
            if(state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag,false))return;
            SessionState.SetBool(Flag,false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Retrobution303RegressionChecks.AssetsReady = Capture;
                await Retrobution303RegressionChecks.Run();
                Retrobution303RegressionChecks.AssetsReady = null;
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static async Task Capture(ModelFactory models, ModelCatalog catalog, OpenFusion.Tables.Gen.XdtTableScript tables)
        {
            Shader.SetGlobalVector("_OF_Light0Dir", new Vector4(.3f,.8f,.5f,0));
            Shader.SetGlobalColor("_OF_Light0Color", Color.white); Shader.SetGlobalColor("_OF_Ambient", Color.gray);
            Shader.DisableKeyword("FOG_LINEAR"); Shader.DisableKeyword("FOG_EXP"); Shader.DisableKeyword("FOG_EXP2");
            var host = new GameObject("Seasonal proof camera"); var camera = host.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.cullingMask = 1 << 31; camera.nearClipPlane = .01f; camera.farClipPlane = 2000;
            var target = new RenderTexture(384,384,24,RenderTextureFormat.ARGB32); target.Create();
            try
            {
                SeasonalEvents.Instance.Apply(true,true);
                for (int i = 0; i < 6; i++)
                {
                    string path = i == 5 ? "mob/npc_frogwarp.kfm" : "mob/mob_sneakyspawn.kfm";
                    var data = catalog.All().First(m => m.Kind == ModelKind.Npc && string.Equals(m.Model,path,StringComparison.OrdinalIgnoreCase));
                    var model = models.Instantiate(data);
                    try
                    {
                        WorldEntities.ActivateAll(model);
                        model.AddComponent<NpcAnimator>().Init(model,tables.m_pNpcTable.m_pNpcData.First(n=>n!=null&&n.m_iNpcNumber==data.Id),null);
                        SeasonalEvents.Instance.TrackNpc(model,data,models,i);
                        foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
                        Bounds bounds = model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).First().bounds;
                        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy)) bounds.Encapsulate(renderer.bounds);
                        Fit(camera,bounds); Shader.SetGlobalColor("_RimColor",SeasonalEvents.HalloweenRim);
                        // The first offscreen request warms the GPU shader/material bindings.
                        // Read the following request rather than asserting on that warm-up.
                        UnityEngine.Object.DestroyImmediate(Render(camera,target));
                        await Task.Delay(100);
                        var picture = Render(camera,target);
                        try
                        {
                            int pixels = picture.GetPixels32().Count(p => p.a > 127);
                            File.WriteAllBytes("Logs/retrobution-303/model-"+i+".png",picture.EncodeToPNG());
                            if (i == 0 && picture.GetPixels32().Count(p => p.a > 127 && p.b > p.g + 3 && p.b > p.r + 3) < 1000) throw new Exception("Enid hat lighting lookup is not visible.");
                            if (pixels < 100) throw new Exception("Seasonal model render is empty: " + path);
                            File.WriteAllBytes("Logs/retrobution-303/model-"+i+".png",picture.EncodeToPNG());
                            Debug.Log("[seasonal visual] "+path+" hat="+SeasonalNpcCosmetic.Pick(path,i)+" opaque pixels="+pixels);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(picture); }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(model); }
                }
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.layer = 31;
                var material = new Material(Shader.Find("OpenFusion/Toon"));
                try
                {
                    material.EnableKeyword("_OF_RIM"); material.EnableKeyword("_OF_RAMP");
                    material.SetColor("_Color",new Color(.15f,.15f,.15f,1)); material.SetFloat("_RimPower",2); material.SetFloat("_RimIntensity",1);
                    sphere.GetComponent<Renderer>().sharedMaterial=material; Fit(camera,sphere.GetComponent<Renderer>().bounds);
                    Shader.SetGlobalColor("_RimColor",Color.black); var off=Render(camera,target);
                    Shader.SetGlobalColor("_RimColor",SeasonalEvents.HalloweenRim); var on=Render(camera,target);
                    try
                    {
                        var a=off.GetPixels32(); var b=on.GetPixels32(); int green=0;
                        for(int i=0;i<a.Length;i++)if(b[i].a>127 && b[i].g-a[i].g>b[i].r-a[i].r+2)green++;
                        if(green<100)throw new Exception("The GPU did not render the expected green Fresnel rim: "+green);
                        File.WriteAllBytes("Logs/retrobution-303/rim-off.png",off.EncodeToPNG());
                        File.WriteAllBytes("Logs/retrobution-303/rim-green.png",on.EncodeToPNG());
                        Debug.Log("[seasonal visual] GPU green rim changed "+green+" pixels compared with disabled rim.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(off); UnityEngine.Object.DestroyImmediate(on); }
                }
                finally { UnityEngine.Object.DestroyImmediate(sphere); UnityEngine.Object.DestroyImmediate(material); }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        static void Fit(Camera camera, Bounds bounds)
        {
            camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x)*1.15f;
            camera.transform.position=bounds.center+Vector3.forward*Mathf.Max(5,bounds.size.magnitude*2);
            camera.transform.LookAt(bounds.center);
        }
        static Texture2D Render(Camera camera, RenderTexture target)
        {
            PreviewRender.Render(camera,target); var previous=RenderTexture.active;
            try
            {
                RenderTexture.active=target; var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();return image;
            }
            finally { RenderTexture.active=previous; }
        }
    }
}

