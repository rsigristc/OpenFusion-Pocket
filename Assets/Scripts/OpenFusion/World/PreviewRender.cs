using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenFusion.World
{
    // All portrait cameras use one isolated camera request, never the full game camera stack.
    public static class PreviewRender
    {
        static readonly int ObjectFade = Shader.PropertyToID("_OF_ObjFade");
        public static void Scoped(Camera camera, RenderTexture destination, Action render)
        {
            var target = camera.targetTexture; var active = RenderTexture.active;
            float aspect = camera.aspect; var clear = camera.clearFlags; var color = camera.backgroundColor;
            var fade = Shader.GetGlobalVector(ObjectFade);
            try
            {
                camera.targetTexture = destination; camera.aspect = destination.width / (float)destination.height;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                Shader.SetGlobalVector(ObjectFade, new Vector4(0, 1e9f, 1e9f + 1, 0));
                render();
            }
            finally
            {
                Shader.SetGlobalVector(ObjectFade, fade);
                camera.targetTexture = target; camera.aspect = aspect; camera.clearFlags = clear; camera.backgroundColor = color;
                RenderTexture.active = active;
            }
        }
        public static void Render(Camera camera, RenderTexture destination)
        {
            if (camera == null || destination == null) return;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false; data.renderShadows = false; data.dithering = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
            Scoped(camera, destination, () => {
                if (GraphicsSettings.currentRenderPipeline == null) { camera.Render(); return; }
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = destination };
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new NotSupportedException("Single-camera previews are unsupported by this render pipeline.");
                RenderPipeline.SubmitRenderRequest(camera, request);
            });
        }
    }
}
