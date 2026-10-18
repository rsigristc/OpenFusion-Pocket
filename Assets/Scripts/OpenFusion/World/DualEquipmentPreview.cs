using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFusion.World
{
    // Streams the existing AvatarPreview camera at a device-bounded frame rate. No disk frames or extra world camera.
    [DefaultExecutionOrder(600)]
    public sealed class DualEquipmentPreview : MonoBehaviour
    {
        public const int Tile = 320, Count = 1, Width = 256;
        public static float FrameInterval { get; private set; } = 1f / 18f;
        readonly int[] ids = new int[Count];
        readonly RenderTexture[] sources = new RenderTexture[Count];
        int[] pixels;
        RenderTexture atlas;
        Texture2D readback;
        AsyncGPUReadbackRequest request;
        bool prepared, pending, destroying, failed, configured, asyncRead;
        float next, lastActive;
        int revision, frames;
        bool Visible => !destroying && !failed && DualScreenController.Instance != null &&
            DualScreenController.Instance.Active && DualScreenController.Instance.SecondaryTab == "Equipment" &&
            Application.isFocused && !WorldBootstrap.Travelling;

        void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Visible) { if (!pending && Time.unscaledTime - lastActive > 2f) ReleaseBuffers(); return; }
            lastActive = Time.unscaledTime;
            if (pending || Time.unscaledTime < next) return;
            try
            {
                if (!configured) { configured = true; asyncRead = SystemInfo.supportsAsyncGPUReadback; }
                FrameInterval = 1f / (asyncRead ? SystemInfo.systemMemorySize >= 6000 ? 24 : SystemInfo.systemMemorySize >= 3000 ? 18 : 12 : 6);
                next = Time.unscaledTime + FrameInterval;
                sources[0] = InventoryWindow.Instance?.DualAvatarTexture();
                ids[0] = 1;
                bool any = sources[0] != null;
                prepared = any; revision = DualScreenController.Instance.Capabilities.revision;
            }
            catch (Exception e) { Stop(e.Message); }
#endif
        }
        void LateUpdate()
        {
            if (!prepared || pending) return; prepared = false;
            if (!Visible || revision != DualScreenController.Instance.Capabilities.revision) return;
            try
            {
                if (atlas == null) { atlas = new RenderTexture(Width, Tile, 0, RenderTextureFormat.ARGB32) { name = "DS Equipment Preview", antiAliasing = 1 }; atlas.Create(); }
                var previous = RenderTexture.active;
                try { RenderTexture.active = atlas; GL.Clear(false, true, Color.clear); }
                finally { RenderTexture.active = previous; }
                if (sources[0] != null) Graphics.Blit(sources[0], atlas);
                pending = true;
                if (asyncRead) request = AsyncGPUReadback.Request(atlas, 0, TextureFormat.RGBA32, Complete);
                else
                {
                    if (readback == null) readback = new Texture2D(Width, Tile, TextureFormat.RGBA32, false);
                    try { RenderTexture.active = atlas; readback.ReadPixels(new Rect(0, 0, Width, Tile), 0, 0, false); Send(readback.GetRawTextureData<byte>()); }
                    finally { RenderTexture.active = previous; pending = false; }
                }
            }
            catch (Exception e) { pending = false; Stop(e.Message); }
        }
        void Complete(AsyncGPUReadbackRequest result)
        {
            try
            {
                if (destroying || this == null) return;
                if (result.hasError) { asyncRead = false; Debug.LogWarning("[DS EquipmentPreview] Async readback unavailable; using capped synchronous previews."); return; }
                Send(result.GetData<byte>());
            }
            catch (Exception e) { Stop(e.Message); }
            finally { pending = false; }
        }
        void Send(NativeArray<byte> rgba)
        {
            var controller = DualScreenController.Instance;
            if (!Visible || controller == null || controller.Capabilities.revision != revision) return;
            if (pixels == null) pixels = new int[Width * Tile];
            if (!ConvertFrame(rgba, pixels, Width, Tile)) return;
            if (controller.SendEquipmentPreview(revision, pixels))
            { frames++; if (frames == 1 || frames % 120 == 0) Debug.Log("[DS EquipmentPreview] frames=" + frames + " async=" + asyncRead + " tiles=" + string.Join(",", ids)); }
        }
        // Android's int pixels are ARGB; Unity readback rows start at the bottom.
        public static bool ConvertFrame(NativeArray<byte> rgba, int[] argb, int width, int height)
        {
            if (!rgba.IsCreated || width <= 0 || height <= 0 || argb == null || argb.Length != width * height || rgba.Length != width * height * 4) return false;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            { int src = ((height - 1 - y) * width + x) * 4; argb[y * width + x] = (rgba[src + 3] << 24) | (rgba[src] << 16) | (rgba[src + 1] << 8) | rgba[src + 2]; }
            return true;
        }
        void Stop(string reason) { failed = true; prepared = false; Debug.LogWarning("[DS EquipmentPreview] Avatar preview unavailable: " + reason); }
        void ReleaseBuffers()
        {
            if (atlas != null) { atlas.Release(); Destroy(atlas); atlas = null; }
            if (readback != null) { Destroy(readback); readback = null; }
            pixels = null;
        }
        void OnDestroy() { destroying = true; if (pending) request.WaitForCompletion(); ReleaseBuffers(); }
    }
}
