// Aplica las opciones graficas/sonido al mundo (efectos de cnGraphicOption.Apply + extras del port movil):
//   visibilidad  -> distancia de la camara, mapa base / error de malla / detalle del terreno (tablas cameraCulls, baseMapDistances, heightMapPixelErrors, detailObjectCulls del original)
//   particulas   -> ParticleDensity (tope y tasa de emision)         toon outline -> global _OF_ToonOff (OpenFusionToon.shader)
//   glow         -> global _OF_GlowOff (OpenFusionLegacy) + bloom URP   anisotropico -> QualitySettings.anisotropicFiltering
//   object fading-> global _OF_ObjFade (disolucion por ruido en OpenFusionLegacy)     sombras -> BlobShadow (solo propio / todos)
//   texturas     -> QualitySettings.globalTextureMipmapLimit             soft vegetation -> QualitySettings.softVegetation (URP lo ignora; se guarda por fidelidad)
//   extras       -> renderScale, MSAA/FXAA, saturacion, FPS
// SIN COMPILAR EN UNITY.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenFusion.World
{
    /// Se pone a cada ParticleSystem creado por LegacyParticles.Build: escala el tope de particulas y la emision (cnGraphicOption: limites 2000/4000/6000/8000).
    public sealed class ParticleDensity : MonoBehaviour
    {
        public static readonly List<ParticleDensity> All = new List<ParticleDensity>();
        public static float Factor = 1f;
        ParticleSystem ps; int baseMax; float baseRate; ParticleSystem.Burst[] baseBursts;
        public static void Tag(ParticleSystem p)
        {
            if (p == null || p.GetComponent<ParticleDensity>() != null) return;
            var d = p.gameObject.AddComponent<ParticleDensity>(); d.ps = p; var m = p.main; d.baseMax = m.maxParticles; var e = p.emission; d.baseRate = e.rateOverTimeMultiplier;
            d.baseBursts = new ParticleSystem.Burst[e.burstCount]; if (e.burstCount > 0) e.GetBursts(d.baseBursts);
            All.Add(d); d.Rescale();
        }
        void OnDestroy() { All.Remove(this); }
        public void Rescale()
        {
            if (ps == null) return; float f = Factor, rf = Mathf.Min(f, 1f);
            var m = ps.main; m.maxParticles = Mathf.Max(4, Mathf.RoundToInt(baseMax * f));
            var e = ps.emission; e.rateOverTimeMultiplier = baseRate * rf;
            if (baseBursts != null && baseBursts.Length > 0)
            {
                var nb = new ParticleSystem.Burst[baseBursts.Length];
                for (int i = 0; i < nb.Length; i++) { nb[i] = baseBursts[i]; nb[i].count = new ParticleSystem.MinMaxCurve(Mathf.Max(1f, baseBursts[i].count.constant * rf)); }
                e.SetBursts(nb);
            }
        }
    }

    public static class GraphicsFx
    {
        // tablas del original (cnOption), indice = RoundToInt(visibilidad * 4)
        public static readonly float[] CameraCulls = { 100f, 200f, 300f, 450f, 600f };
        static readonly float[] BaseMap = { 40f, 75f, 150f, 225f, 300f };
        static readonly float[] HeightErr = { 40f, 30f, 20f, 10f, 5f };
        static readonly float[] DetailCull = { 0f, 40f, 80f, 120f, 160f };
        static readonly float[] ParticleFactor = { 1f / 3f, 2f / 3f, 1f, 4f / 3f };
        static readonly int[] FpsVals = { 30, 60, -1 };

        public static OptionData Last;
        static GraphicsRunner runner;
        static Volume vol; static Bloom bloom; static ColorAdjustments color; static VolumeProfile prof;
        static float origScale = -1f; static int origMsaa = -1; static bool quitHooked;
        public static float CameraFar { get { return Last == null ? 1000f : CameraCulls[Idx(Last)] + 80f; } }
        static int Idx(OptionData o) { return Mathf.Clamp(Mathf.RoundToInt(o.Visibility * 4f), 0, 4); }

        public static void Ensure(GameObject host)
        {
            if (runner != null || host == null) return;
            runner = host.GetComponent<GraphicsRunner>(); if (runner == null) runner = host.AddComponent<GraphicsRunner>();
        }

        public static void ApplyAll(OptionData o)
        {
            if (o == null) return; Last = o.Clone(); int i = Idx(o); float far = CameraCulls[i] + 80f;
            try
            {
                Shader.SetGlobalFloat("_OF_ToonOff", o.Toon ? 0f : 1f);
                Shader.SetGlobalFloat("_OF_GlowOff", o.Glow ? 0f : 1f);
                Shader.SetGlobalVector("_OF_ObjFade", o.Fade ? new Vector4(1f, far * 0.85f, far, 0f) : new Vector4(0f, 1e9f, 1e9f + 1f, 0f));
                QualitySettings.anisotropicFiltering = o.Aniso ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Disable;
                QualitySettings.softVegetation = o.SoftVegetation; Shader.SetGlobalFloat("_OF_SoftVeg", o.SoftVegetation ? 1f : 0f);
                QualitySettings.globalTextureMipmapLimit = (int)o.Texture;   // 0 completa, 1 mitad, 2 cuarto
                ParticleDensity.Factor = ParticleFactor[Mathf.Clamp(o.ParticleEffect, 0, 3)]; foreach (var d in ParticleDensity.All.ToArray()) if (d != null) d.Rescale();
                BlobShadow.AllShadows = o.Shadow == ShadowDetail.AllCharacters; BlobShadow.Refresh();
                int fps = FpsVals[Mathf.Clamp(o.Fps, 0, 2)]; if (Application.targetFrameRate != fps) Application.targetFrameRate = fps;
            }
            catch (System.Exception ex) { Debug.LogWarning("[opciones] graficos: " + ex.Message); }
            try { ApplyUrp(o); } catch (System.Exception ex) { Debug.LogWarning("[opciones] URP: " + ex.Message); }
            try { ApplySound(o); } catch (System.Exception ex) { Debug.LogWarning("[opciones] sonido: " + ex.Message); }
            if (runner != null) runner.Invalidate();
            try
            {   // diagnostico: cuantos materiales hay con cada shader afectado por las opciones
                int toon = 0, legacy = 0; foreach (var r in Object.FindObjectsOfType<Renderer>()) foreach (var m in r.sharedMaterials) { if (m == null || m.shader == null) continue; string n = m.shader.name; if (n == "OpenFusion/Toon") toon++; else if (n == "OpenFusion/Legacy") legacy++; }
                Debug.Log("[opciones] aplicado: visibilidad " + o.Visibility.ToString("0.00") + " (camara " + far + " m), toon " + o.Toon + ", glow " + o.Glow + ", fade " + o.Fade + ", particulas " + o.ParticleEffect + " | materiales Toon: " + toon + ", Legacy: " + legacy);
            }
            catch (System.Exception) { }
        }

        public static void ApplySound(OptionData o)
        {
            AudioListener.pause = false; AudioListener.volume = o.MasterOn ? o.Master : 0f;
            Sfx.SfxVolume = o.SfxOn ? o.Sfx : 0f; Sfx.VoiceVolume = o.VoiceOn ? o.Voice : 0f; Sfx.AmbientVolume = o.AmbientOn ? o.Ambient : 0f; Sfx.MusicVolume = o.MusicOn ? o.Music : 0f;
            Debug.Log("[opciones] audio: listener " + AudioListener.volume.ToString("0.00") + " (maestro " + (o.MasterOn ? "on" : "off") + " " + o.Master.ToString("0.00") + "), efectos " + Sfx.SfxVolume.ToString("0.00") + ", voz " + Sfx.VoiceVolume.ToString("0.00") + ", ambiente " + Sfx.AmbientVolume.ToString("0.00") + ", musica " + Sfx.MusicVolume.ToString("0.00") + ", AudioListener en escena: " + Object.FindObjectsOfType<AudioListener>().Length);
        }

        static void ApplyUrp(OptionData o)
        {
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; if (rp == null) return;
            if (origScale < 0f) { origScale = rp.renderScale; origMsaa = rp.msaaSampleCount; }
            if (!quitHooked) { quitHooked = true; Application.quitting += RestoreUrp; }
            rp.renderScale = Mathf.Clamp(o.RenderScale, 0.5f, 1f); rp.msaaSampleCount = o.AA == 2 ? 2 : o.AA == 3 ? 4 : 1;
            EnsureVolume(); bool post = NeedPost(o);
            if (vol != null) { bloom.active = o.Glow && o.GlowStrength > 0.01f; bloom.intensity.Override(1.4f * o.GlowStrength); ApplyGrade(o); }
            ApplyCamera(Camera.main, o, post);
        }
        /// <summary>Opcion GLOW del original (GlowEffect en la camara): con ella activa la imagen se multiplica por filterColor (= luz del dong * 0.6 + blanco * 0.4, cnPlayerCamera.DefaultAmbience) y se suma el brillo difuminado; apagada, la escena sale sin tinte ni brillo.</summary>
        public static Color FilterColor = Color.white;
        public static bool NeedPost(OptionData o) { return o.Glow || Mathf.Abs(o.Saturation - 0.5f) > 0.01f || o.AA == 1; }
        public static void SetFilter(Color c) { c.a = 1f; if (c == FilterColor) return; FilterColor = c; if (Last != null) ApplyGrade(Last); }
        static void ApplyGrade(OptionData o)
        {
            if (vol == null || color == null || o == null) return; bool sat = Mathf.Abs(o.Saturation - 0.5f) > 0.01f;
            color.active = sat || o.Glow;
            color.saturation.Override(sat ? (o.Saturation - 0.5f) * 80f : 0f); color.contrast.Override(sat ? (o.Saturation - 0.5f) * 16f : 0f);
            color.colorFilter.Override(o.Glow ? FilterColor : Color.white);
        }
        public static void ApplyCamera(Camera cam, OptionData o, bool post)
        {
            if (cam == null || o == null) return; var d = cam.GetUniversalAdditionalCameraData(); if (d == null) return;
            d.renderPostProcessing = post; d.antialiasing = o.AA == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None; d.antialiasingQuality = AntialiasingQuality.High;
            cam.allowMSAA = o.AA >= 2; cam.farClipPlane = CameraCulls[Idx(o)] + 80f;
        }
        static void EnsureVolume()
        {
            if (vol != null) return;
            var go = new GameObject("OF_GlobalVolume"); Object.DontDestroyOnLoad(go); vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 5f; vol.weight = 1f;
            prof = ScriptableObject.CreateInstance<VolumeProfile>(); prof.name = "OF_Profile";
            bloom = prof.Add<Bloom>(true); bloom.threshold.Override(0.95f); bloom.scatter.Override(0.5f); bloom.intensity.Override(0.5f); bloom.highQualityFiltering.Override(false);
            color = prof.Add<ColorAdjustments>(true); color.saturation.Override(0f); color.contrast.Override(0f);
            vol.sharedProfile = prof;
        }
        static void RestoreUrp()
        {
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rp != null && origScale > 0f) { rp.renderScale = origScale; rp.msaaSampleCount = origMsaa; }
        }

        /// Terreno segun la visibilidad (cnGraphicOption.Apply -> terrain.basemapDistance / heightmapPixelError / detailObjectDistance)
        public static void ApplyTerrain(Terrain t, OptionData o)
        {
            if (t == null || o == null) return; int i = Idx(o);
            t.basemapDistance = Application.isMobilePlatform ? CameraCulls[i] + 100f : BaseMap[i]; t.heightmapPixelError = HeightErr[i];
            GrassField.Distance = DetailCull[i]; Shader.SetGlobalFloat("_OF_GrassDist", GrassField.Enabled ? DetailCull[i] : 0f);
            if (!GrassField.Custom || t.GetComponentInChildren<GrassField>() == null) t.detailObjectDistance = DetailCull[i];
        }
    }

    /// Mantiene aplicadas las opciones: camara nueva (cambio de escena), terrenos recien cargados y carga inicial.
    public sealed class GraphicsRunner : MonoBehaviour
    {
        readonly HashSet<int> doneTerrains = new HashSet<int>(); float next; Camera lastCam;
        public void Invalidate() { doneTerrains.Clear(); next = 0f; }
        void Start() { GameOptions.EnsureLoaded(); GameOptions.Apply(); }
        void LateUpdate()
        {
            var o = GraphicsFx.Last; if (o == null) return;
            var cam = Camera.main;
            if (cam != null)
            {
                float far = GraphicsFx.CameraFar; if (!Mathf.Approximately(cam.farClipPlane, far)) cam.farClipPlane = far;
                if (cam != lastCam) { lastCam = cam; GraphicsFx.ApplyCamera(cam, o, GraphicsFx.NeedPost(o)); }
            }
            if (Time.unscaledTime < next) return; next = Time.unscaledTime + 2f;
            var ts = Terrain.activeTerrains; for (int i = 0; i < ts.Length; i++) { var t = ts[i]; if (t == null) continue; if (doneTerrains.Add(t.GetInstanceID())) GraphicsFx.ApplyTerrain(t, o); }
        }
    }
}
