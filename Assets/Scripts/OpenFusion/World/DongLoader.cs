// Equivalente del DongLoader del cliente original: streaming de dongs 16x16 de 512 m alrededor del jugador.
// La lógica de decisión está en DongGrid/DongRegistry (probada); esto sólo la conecta con Unity. SIN COMPILAR NI PROBAR.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OpenFusion.Materials;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class DongLoader : MonoBehaviour
    {
        public Transform Player; public Camera Cam;
        public bool UseSky = true; LegacySky sky6;
        public bool CompressTextures = true;
        // Character screens need shared avatar assets, but no map objects or terrain.
        public bool Paused { get; set; }
        int pauseDongX = -1, pauseDongY = -1;
        public void PauseAfterDong(int x, int y) { pauseDongX = x; pauseDongY = y; }
        /// Niebla lineal que oculta el borde del mundo cargado (sin ella se ve el color de fondo como un "plano" sobre el suelo).
        /// Pruebas: transponer el pasto / los mapas de mezcla si salen en sitios equivocados.
        public bool GrassTranspose, AlphaTranspose;
        public bool UseFog = true; public bool FogExp2 = false; public float FogDensityScale = 1f;   // niebla del original: exponencial, densidad = fogDepth*0.005
        public event Action<int, int> DongLoaded, DongUnloaded;
        /// Faltan archivos del dong (aún no descargados): quien orquesta descargas (WorldBootstrap) debe pedirlos; se reintenta a los 2 s.
        public event Action<int, int> DongMissing;
        public AssetStore Store => store; public DongBuilder Builder => builder;
        ModelFactory skyModels;
        public ModelFactory CreateModelFactory(IAssetLocator locator)
        {
            var mf = new ModelFactory(store, locator, builder);
            skyModels = mf;
            // EPElementController (efectos ES[n] de jumppads/launchers): el constructor de dongs los pide por índice
            builder.EffectFactory = id => { var rf = mf.Locate("Prefabs/Particle/EffectScripts/ES[" + id + "].prefab"); if (!rf.Ok) return null; var go = builder.BuildPrefabAt(rf, null); if (go == null) return null; WorldEntities.ActivateAll(go); go.SetActive(true); return go; };
            return mf;
        }

        IBundleSource source; DongRegistry reg; AssetStore store; DongBuilder builder; readonly Dictionary<int, DongBuildResult> built = new Dictionary<int, DongBuildResult>();
        readonly HashSet<string> sharedLoaded = new HashSet<string>(); bool busy, ready; Vector3 preload;
        readonly HashSet<string> pinnedResourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase), pinnedResourceBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public void ImportResourceBundle(string name, Dictionary<string, byte[]> files)
        {
            foreach (var file in files) { if (!store.Has(file.Key)) TryAdd(file.Key, file.Value); pinnedResourceFiles.Add(file.Key); }
            sharedLoaded.Add(name); pinnedResourceBundles.Add(name);
        }

        /// mainUnity3d: bytes de main.unity3d (para el CachingManifest). start: posición inicial del mundo (original: 6912,1792 = dong 13,3).
        public void Init(byte[] mainUnity3d, IBundleSource src, Vector3 start, CachingManifest25 manifest = null)
        {
            pinnedResourceFiles.Clear(); pinnedResourceBundles.Clear(); sharedLoaded.Clear();
            TerrainBuilder.DetailTranspose = !GrassTranspose;   // los datos de detalle 2.5 van por columnas: el orden correcto es el transpuesto (la casilla GrassTranspose ahora lo invierte) TerrainBuilder.AlphaTranspose = AlphaTranspose;
            source = src; store = new AssetStore(); builder = new DongBuilder(store) { CompressTextures = CompressTextures };
            reg = new DongRegistry(manifest ?? CachingManifest25.FromMainUnity3d(mainUnity3d)); reg.SetZoneFor(start.x, start.z); preload = start; ready = true;
        }
        /// true cuando el dong (x,y) ya está construido (implica que los bundles compartidos —modelos, texturas— ya están cargados).
        public bool IsDongLoaded(int x, int y) => ready && reg != null && (reg.At(x, y)?.Loaded ?? false);
        public void SetPosition(Vector3 position)
        {
            preload = position; reg.SetZoneFor(position.x, position.z);
            if (Player != null) Player.position = position;
        }
        public async System.Threading.Tasks.Task PrepareSharedAsync(IEnumerable<string> names)
        {
            foreach (var name in names.Distinct())
            {
                if (sharedLoaded.Contains(name)) continue;
                var files = await System.Threading.Tasks.Task.Run(() => source.Load(name));
                if (files == null) throw new System.IO.IOException("Missing cached bundle: " + name);
                foreach (var kv in files) TryAdd(kv.Key, kv.Value);
                sharedLoaded.Add(name);
                // Initial world loading only; avoid forced collections while playing.
                if (built.Count == 0) OpenFusion.Screens.PocketIosLoadingMemory.SharedBundleLoaded();
            }
        }
        public byte AttributeAt(Vector3 p, out bool known) => reg.AttributeAt(p.x, p.z, out known);
        public bool TryTerrainHeight(Vector3 p, out float height)
        {
            DongGrid.Cell(p.x, p.z, out int x, out int y);
            var terrain = T(x, y); height = 0f;
            if (terrain == null || terrain.terrainData == null) return false;
            height = terrain.SampleHeight(p) + terrain.transform.position.y; return true;
        }

        void Update()
        {
            if (Paused || !ready || busy || reg == null || store == null || builder == null) return;
            var pos = Player != null ? Player.position : preload;
            reg.SetZoneFor(pos.x, pos.z);
            var st = reg.Step(pos.x, pos.z);
            if (st.unload) StartCoroutine(Unload(st.ux, st.uy)); else if (st.load && Time.time >= reg.At(st.x, st.y).RetryAt) StartCoroutine(Load(st.x, st.y));
            ApplyColors(pos);
        }

        bool sharedReady;
        IEnumerator EnsureShared()
        {
            sharedReady = true;
            foreach (var name in reg.SharedFiles(reg.Zone))
            {
                if (sharedLoaded.Contains(name)) continue; Dictionary<string, byte[]> files = null;
                var t = System.Threading.Tasks.Task.Run(() => source.Load(name)); while (!t.IsCompleted) yield return null;
                if (t.IsFaulted) { OpenFusion.Screens.PocketDiagnostics.AssetFailure(name, true); sharedReady = false; Debug.LogWarning("Bundle compartido " + name + ": " + t.Exception.GetBaseException().Message); continue; }
                files = t.Result;
                if (files == null) { OpenFusion.Screens.PocketDiagnostics.AssetFailure(name); sharedReady = false; continue; } // Reintentar cuando la descarga termine.
                foreach (var kv in files) TryAdd(kv.Key, kv.Value);
                sharedLoaded.Add(name);
                if (built.Count == 0) OpenFusion.Screens.PocketIosLoadingMemory.SharedBundleLoaded();
            }
        }
        void TryAdd(string name, byte[] data) { try { store.Add(name, data); } catch (Exception e) { Debug.LogWarning("No se pudo leer " + name + ": " + e.Message); } }

        IEnumerator Load(int x, int y)
        {
            var timing = System.Diagnostics.Stopwatch.StartNew();
            busy = true; var d = reg.At(x, y); d.Loading = true; yield return EnsureShared();
            long sharedMs = timing.ElapsedMilliseconds;
            try
            {
            if (!sharedReady) { d.RetryAt = Time.time + 2f; DongMissing?.Invoke(x, y); yield break; }
            string sceneName = null; var added = new List<string>(); bool missing = false;
            foreach (var f in reg.FilesFor(x, y))
            {
                var t = System.Threading.Tasks.Task.Run(() => source.Load(f)); while (!t.IsCompleted) yield return null;
                if (t.IsFaulted) { OpenFusion.Screens.PocketDiagnostics.AssetFailure(f, true); Debug.LogWarning("Bundle " + f + ": " + t.Exception.GetBaseException().Message); missing = true; continue; }
                if (t.Result == null) { OpenFusion.Screens.PocketDiagnostics.AssetFailure(f); missing = true; continue; }
                foreach (var kv in t.Result) { if (f.StartsWith("Map_") && kv.Key == "BuildPlayer-" + DongGrid.MapName(x, y)) sceneName = kv.Key; if (!kv.Key.EndsWith(".sharedAssets")) { TryAdd(kv.Key, kv.Value); added.Add(kv.Key); } }
            }
            if (sceneName != null && store.Has(sceneName))
            {
                long readMs = timing.ElapsedMilliseconds - sharedMs;
                var buildTime = System.Diagnostics.Stopwatch.StartNew();
                var res = builder.Build(sceneName); if (res.Root != null) res.Root.name = DongGrid.MapName(x, y);
                try { EnvironmentalAudio.Attach(res.Root, store, added, x, y); }
                catch (Exception ex) { Debug.LogWarning("[environment] " + x + "," + y + ": " + ex.Message); }
                d.Colors = res.Colors; d.Attributes = res.Attributes; d.OriginX = x * DongGrid.Size; d.OriginZ = y * DongGrid.Size;
                if (res.Terrain != null) { var td = res.Terrain.terrainData; d.HmW = td.heightmapResolution; d.HmH = td.heightmapResolution; d.SizeX = td.size.x; d.SizeZ = td.size.z; }
                built[y * 16 + x] = res; d.StoreFiles = added; d.Loaded = true;
                if (x == pauseDongX && y == pauseDongY) { pauseDongX = pauseDongY = -1; Paused = true; }
                LinkTerrains(x, y); DongLoaded?.Invoke(x, y);
                Debug.Log("[load] map " + x + "," + y + " shared=" + sharedMs + "ms read=" + readMs + "ms build=" + buildTime.ElapsedMilliseconds + "ms");
                Debug.Log("[dong] cargado " + x + "," + y + " terreno=" + (res.Terrain != null ? res.Terrain.transform.position.ToString() + " size=" + res.Terrain.terrainData.size : "no") + " | cargados=" + built.Count);
            }
            if (missing) { d.RetryAt = Time.time + 2f; DongMissing?.Invoke(x, y); }
            }
            finally { d.Loading = false; busy = false; }
        }

        IEnumerator Unload(int x, int y)
        {
            busy = true; var d = reg.At(x, y);
            if (built.TryGetValue(y * 16 + x, out var res)) { if (res.Root != null) Destroy(res.Root); built.Remove(y * 16 + x); }
            foreach (var f in d.StoreFiles) { if (pinnedResourceFiles.Contains(f)) continue; store.Remove(f); builder.Forget(f); }
            if (source is IReleasableSource rs) foreach (var f in reg.FilesFor(x, y)) if (!pinnedResourceBundles.Contains(f)) rs.Release(f);
            d.Loaded = false; d.Colors = default; d.Attributes = null; d.StoreFiles = new List<string>();
            DongUnloaded?.Invoke(x, y); yield return Resources.UnloadUnusedAssets(); busy = false;
        }

        Terrain T(int x, int y) => built.TryGetValue(y * 16 + x, out var r) ? r.Terrain : null;
        void LinkTerrains(int x, int y)
        {   // como el original: SetNeighbors(left, top, right, bottom) en el dong y sus vecinos
            for (int i = Mathf.Max(y - 1, 0); i < Mathf.Min(y + 2, 16); i++) for (int j = Mathf.Max(x - 1, 0); j < Mathf.Min(x + 2, 16); j++)
            { var t = T(j, i); if (t != null) t.SetNeighbors(T(j - 1, i), T(j, i + 1), T(j + 1, i), T(j, i - 1)); }
        }

        void ApplyColors(Vector3 p)
        {
            reg.PositionColor(p.x, p.z, out var fog, out var sky, out var light, out var fogDepth, out var rim);
            var fogColor = new Color(fog[0], fog[1], fog[2], 1f);
            var skyColor = new Color(sky[0], sky[1], sky[2], 1f);
            var lightColor = new Color(light[0], light[1], light[2], 1f);
            var rimColor = new Color(rim[0], rim[1], rim[2], 1f);
            float density = SeasonalEvents.Ambience(ref fogColor, ref skyColor, ref lightColor, ref rimColor, fogDepth, reg.Zone == ZoneType.Paid);
            Shader.SetGlobalColor("_RimColor", rimColor);
            // cnPlayerCamera.DefaultAmbience: fogDensity = fogDepth * 0.005, fog = fogDepth > 0 (la niebla exponencial de la escena 2.5; el modo no se guarda en el dong).
            RenderSettings.fog = UseFog && fogDepth > 0f; RenderSettings.fogMode = FogExp2 ? FogMode.ExponentialSquared : FogMode.Exponential; RenderSettings.fogDensity = density * FogDensityScale;
            RenderSettings.reflectionIntensity = 0f;   // el original no tiene reflejos de entorno: evita el brillo (Fresnel) que se movía con la vista
            RenderSettings.fogColor = fogColor;
            // niebla = fondo de cámara: el borde se funde con el cielo (el original muestra el cian de la niebla)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.2f, .2f, .2f, 1f); // Legacy scene, independent of the glow filter
            GraphicsFx.SetFilter(lightColor); // Already transformed by ordinary/event ambience, exactly once.
            if (Cam != null)
            {
                Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = fogColor;
                // cielo 6 caras del original, teñido con el color de niebla (cnPlayerCamera: skybox.SetColor("_Tint", fog))
                if (UseSky) { if (sky6 == null) sky6 = LegacySky.Create(Cam); if (sky6 != null) sky6.Apply(Cam.transform.position, fogColor, skyModels); }
            }
            // fogDepth: el original lo usa en sus shaders de niebla (fogslope.tif); aquí queda como global para los shaders OpenFusion
            Shader.SetGlobalFloat("_OF_FogDepth", fogDepth);
        }
    }
}
