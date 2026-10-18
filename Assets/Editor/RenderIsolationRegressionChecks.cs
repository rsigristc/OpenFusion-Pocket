using System;
using OpenFusion.World;
using OpenFusion.Unity25;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class RenderIsolationRegressionChecks
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        public static void Run()
        {
            var host = new GameObject("Preview isolation fixture");
            var oldActive = RenderTexture.active; var fade = Shader.GetGlobalVector("_OF_ObjFade");
            RenderTexture previous = null, target = null; Texture2D icon = null;
            try
            {
                var camera = host.AddComponent<Camera>(); camera.enabled = false;
                previous = new RenderTexture(12, 12, 24); target = new RenderTexture(24, 12, 24);
                camera.targetTexture = previous; camera.aspect = 1.25f; camera.clearFlags = CameraClearFlags.Depth; camera.backgroundColor = Color.red;
                var expectedFade = new Vector4(1, 300, 400, 0); Shader.SetGlobalVector("_OF_ObjFade", expectedFade);
                bool callback = false, rejected = false;
                try {
                    PreviewRender.Scoped(camera, target, () => {
                        callback = true;
                        Check(camera.targetTexture == target && Mathf.Approximately(camera.aspect, 2), "A portrait must render to its own target and aspect.");
                        Check(Shader.GetGlobalVector("_OF_ObjFade").x == 0 && camera.clearFlags == CameraClearFlags.SolidColor && camera.backgroundColor.a == 0, "World fading must not dissolve UI models; their target must clear transparently.");
                        throw new ApplicationException("Synthetic GPU failure");
                    });
                } catch (ApplicationException) { rejected = true; }
                Check(callback && rejected && camera.targetTexture == previous && Mathf.Approximately(camera.aspect, 1.25f) && camera.clearFlags == CameraClearFlags.Depth && camera.backgroundColor == Color.red, "A failing preview must restore its camera state.");
                Check(RenderTexture.active == oldActive && Shader.GetGlobalVector("_OF_ObjFade") == expectedFade, "A failing preview must restore render-target and world fading globals.");
                byte[] dxt = new byte[16]; dxt[0] = 0; dxt[1] = 255;
                ulong indices = 0; for (int i = 1; i < 16; i++) indices |= 1UL << (3 * i);
                for (int i = 0; i < 6; i++) dxt[2 + i] = (byte)(indices >> (8 * i));
                dxt[8] = 0; dxt[9] = 248; // red RGB565, including the fully transparent pixel
                var source = new Texture25 { Name = "cosicon_fixture", Width = 4, Height = 4, Format = 12, Data = dxt };
                icon = UnityConverters.ToTexture(source, true);
                var pixels = icon.GetPixels32();
                Check(icon.format == TextureFormat.RGBA32 && pixels[0].a == 0 && pixels[1].a == 255 && pixels[0].r > 200, "Icon conversion must preserve DXT5 alpha/RGB without mobile recompression or black color-key guessing.");
                Check(UnityConverters.IsInterfaceIcon("WPNICON_01") && !UnityConverters.IsInterfaceIcon("terrain_grass"), "Keep icon protection scoped; retain world-texture compression.");
                Debug.Log("[render isolation regression] transparent targets, camera/global restoration after failure and alpha-preserving icon conversion passed.");
            }
            finally
            {
                RenderTexture.active = oldActive; Shader.SetGlobalVector("_OF_ObjFade", fade);
                if (icon != null) UnityEngine.Object.DestroyImmediate(icon);
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
