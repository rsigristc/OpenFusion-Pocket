using System.Collections;
using OpenFusion.Unity25;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OpenFusion.World
{
    // Opt-in real-GPU comparison with the Legacy terrain equation. No account or
    // live world is needed. Nothing enables this during an ordinary app launch.
    public sealed class LegacyTerrainRenderProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent")) {
                    if (!intent.Call<bool>("getBooleanExtra", "of_lighting_probe", false)) return;
                    intent.Call("removeExtra", "of_lighting_probe");
                    new GameObject("LegacyTerrainRenderProbe").AddComponent<LegacyTerrainRenderProbe>();
                }
            } catch (System.Exception error) { Debug.LogError("[lighting-probe] " + error); }
#endif
        }
        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(8,8,TextureFormat.RGBA32,false);
            var pixels = new Color[64]; for (int i=0;i<pixels.Length;i++) pixels[i]=color;
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
        IEnumerator Start()
        {
            GameObject root=null, cameraGo=null, sunGo=null;
            Texture2D diffuse=null, lightmap=null, sample=null; RenderTexture target=null;
            bool fog=RenderSettings.fog; var previousSun=RenderSettings.sun;
            try {
                RenderSettings.fog=false;
                diffuse=Solid(new Color(.4f,0,0,1)); lightmap=Solid(new Color(.5f,.5f,.5f,1));
                var data=new Terrain25 { Name="LightingEquationProbe", Width=33, Height=33, ScaleX=1, ScaleY=4, ScaleZ=1,
                    Heights=new ushort[1089], Lightmap=new PPtr25 { PathId=99 } };
                for(int i=0;i<6;i++) data.Splats.Add(new SplatLayer25 { Texture=new PPtr25 { PathId=i+1 }, TileX=8, TileY=8 });
                root=TerrainBuilder.Build(data,p=>p.PathId==99?lightmap:diffuse);
                root.layer=30; root.transform.position=new Vector3(2000,0,2000);
                var terrain=root.GetComponent<Terrain>(); terrain.terrainData.alphamapResolution=16;
                var weights=new float[16,16,6]; for(int y=0;y<16;y++) for(int x=0;x<16;x++) for(int l=0;l<6;l++) weights[y,x,l]=1f/6;
                terrain.terrainData.SetAlphamaps(0,0,weights); terrain.drawInstanced=true;
                sunGo=new GameObject("LightingProbeSun"); var sun=sunGo.AddComponent<Light>(); sun.type=LightType.Directional; sun.cullingMask=1<<30; RenderSettings.sun=sun;
                cameraGo=new GameObject("LightingProbeCamera"); var camera=cameraGo.AddComponent<Camera>();
                camera.transform.position=root.transform.position+new Vector3(16,30,16); camera.transform.rotation=Quaternion.Euler(90,0,0);
                camera.orthographic=true; camera.orthographicSize=20; camera.farClipPlane=180; camera.cullingMask=1<<30;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.blue; camera.allowHDR=false;
                cameraGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
                target=new RenderTexture(64,64,24); target.Create(); camera.targetTexture=target;
                sample=new Texture2D(1,1,TextureFormat.RGBA32,false);
                for(int mode=0;mode<3;mode++) {
                    sun.intensity=mode==1?0:2; sun.transform.rotation=Quaternion.Euler(mode==1?0:90,0,0);
                    terrain.basemapDistance=mode==2?0:1000;
                    terrain.terrainData.SetBaseMapDirty(); terrain.Flush();
                    for(int frame=0;frame<6;frame++) yield return new WaitForEndOfFrame();
                    var previous=RenderTexture.active;
                    try { RenderTexture.active=target; sample.ReadPixels(new Rect(32,32,1,1),0,0); sample.Apply(); }
                    finally { RenderTexture.active=previous; }
                    Color pixel=sample.GetPixel(0,0);
                    bool pass=Mathf.Abs(pixel.r-.4f)<.045f && pixel.g<.03f && pixel.b<.03f;
                    Debug.Log("[lighting-probe] mode="+mode+" api="+SystemInfo.graphicsDeviceType+" expected=(0.4,0,0) actual="+pixel+
                        " layers=6 instanced=true basemap="+terrain.basemapDistance+" result="+(pass?"PASS":"FAIL"));
                }
            }
            finally {
                RenderSettings.fog=fog; RenderSettings.sun=previousSun;
                if(cameraGo!=null) Destroy(cameraGo); if(root!=null) Destroy(root); if(sunGo!=null) Destroy(sunGo);
                if(target!=null) { target.Release(); Destroy(target); }
                if(diffuse!=null) Destroy(diffuse); if(lightmap!=null) Destroy(lightmap); if(sample!=null) Destroy(sample);
                Destroy(gameObject);
            }
        }
    }
}
