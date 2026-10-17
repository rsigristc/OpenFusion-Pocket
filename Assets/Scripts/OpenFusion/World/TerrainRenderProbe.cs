using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    // Opt-in GPU regression check, launched with an Android intent extra. No login
    // or world assets are needed, so terrain rendering can be checked on the device.
    public sealed class TerrainRenderProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    if (!intent.Call<bool>("getBooleanExtra", "of_render_probe", false)) return;
                    intent.Call("removeExtra", "of_render_probe");
                    new GameObject("TerrainRenderProbe").AddComponent<TerrainRenderProbe>();
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[render-probe] " + e.Message); }
        }
        IEnumerator Start()
        {
            GameObject root = null, cameraGo = null, sunGo = null; Texture2D texture = null, readback = null; RenderTexture target = null;
            bool oldFog = RenderSettings.fog;
            var oldSun = RenderSettings.sun;
            try
            {
                RenderSettings.fog = false;
                texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var pixels = new Color[64]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.red;
                texture.SetPixels(pixels); texture.Apply();
                var data = new Terrain25 { Name = "RenderProbe", Width = 33, Height = 33, ScaleX = 1, ScaleY = 4, ScaleZ = 1, Heights = new ushort[1089] };
                data.Splats.Add(new SplatLayer25 { Texture = new PPtr25 { PathId = 1 }, TileX = 8, TileY = 8 });
                root = TerrainBuilder.Build(data, _ => texture); root.layer = 30; root.transform.position = new Vector3(100000, 0, 100000);
                var terrain = root.GetComponent<Terrain>(); terrain.terrainData.alphamapResolution = 16;
                var alpha = new float[16, 16, 1]; for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) alpha[y, x, 0] = 1;
                terrain.terrainData.SetAlphamaps(0, 0, alpha);
                sunGo = new GameObject("ProbeSun"); var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 2; sun.cullingMask = 1 << 30; sun.transform.rotation = Quaternion.Euler(90, 0, 0);
                RenderSettings.sun = sun;
                cameraGo = new GameObject("ProbeCamera"); var cam = cameraGo.AddComponent<Camera>();
                cam.transform.position = root.transform.position + new Vector3(16, 30, 16); cam.transform.rotation = Quaternion.Euler(90, 0, 0);
                cam.orthographic = true; cam.orthographicSize = 20; cam.farClipPlane = 180; cam.cullingMask = 1 << 30;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.blue; cam.depth = 100;
                cameraGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                target = new RenderTexture(64, 64, 24); target.Create(); cam.targetTexture = target;
                readback = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                for (int mode = 0; mode < 6; mode++)
                {
                    terrain.basemapDistance = mode == 1 ? 0 : 1000;
                    if (mode == 2) GraphicsFx.ApplyTerrain(terrain, new OptionData { Visibility = 0 });
                    if (mode >= 3) terrain.drawInstanced = false;
                    if (mode == 4) { root.transform.position = new Vector3(2000, 0, 2000); cam.transform.position = root.transform.position + new Vector3(16, 30, 16); }
                    if (mode == 5) { terrain.drawInstanced = true; terrain.materialTemplate.shaderKeywords = new string[0]; terrain.materialTemplate.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL"); }
                    terrain.terrainData.SetBaseMapDirty(); terrain.Flush();
                    for (int f = 0; f < 4; f++) yield return new WaitForEndOfFrame();
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    readback.ReadPixels(new Rect(32, 32, 1, 1), 0, 0); readback.Apply(); RenderTexture.active = previous;
                    var color = readback.GetPixel(0, 0);
                    bool red = color.r > .15f && color.r > color.g + .1f && color.r > color.b + .1f;
                    Debug.Log("[render-probe] mode=" + mode + " basemap=" + terrain.basemapDistance + " shader=" + terrain.materialTemplate.shader.name + " instanced=" + terrain.drawInstanced + " supported=" + terrain.materialTemplate.shader.isSupported + " pixel=" + color + " result=" + (red ? "PASS" : "FAIL"));
                }
            }
            finally
            {
                RenderSettings.fog = oldFog;
                RenderSettings.sun = oldSun;
                if (cameraGo != null) Destroy(cameraGo); if (root != null) Destroy(root); if (sunGo != null) Destroy(sunGo);
                if (target != null) { target.Release(); Destroy(target); } if (texture != null) Destroy(texture); if (readback != null) Destroy(readback);
                Destroy(gameObject);
            }
        }
    }
}
