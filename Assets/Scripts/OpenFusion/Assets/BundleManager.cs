// Gestor de bundles: fases de descarga del cliente original + registro de índices por bundle + enrutado de rutas.
// Sin dependencias de Unity. Es una IBundleSource "decoradora": DongLoader pide Load(nombre) y el gestor registra el índice del bundle.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenFusion.Unity25;
using OpenFusion.World;

namespace OpenFusion.Assets
{
    /// Fases del cliente original (AssetLoader.Mode), en el orden en que se necesitan.
    public enum Phase { CharacterCreation, CharacterSelection, Tutorial, FreeZone, PaidZone }

    public static class Phases
    {
        public static readonly Phase[] Order = { Phase.CharacterCreation, Phase.CharacterSelection, Phase.Tutorial, Phase.FreeZone, Phase.PaidZone };
        static string List(Phase p) => p == Phase.CharacterCreation ? "m_CharacterCreation" : p == Phase.CharacterSelection ? "m_CharacterSelection" : p == Phase.Tutorial ? "m_Tutorial" : p == Phase.FreeZone ? "m_FreeZone" : "m_PaidZone";
        /// Archivos compartidos de la fase (sin los Map_/DongResources_ de cada dong; esos se piden por posición).
        public static IEnumerable<string> Files(CachingManifest25 m, Phase p)
            => m.Lists.TryGetValue(List(p), out var l) ? l.Select(e => e.FileName).Distinct().Where(n => !IsDongFile(n)) : Enumerable.Empty<string>();
        /// Los 9 mapas del tutorial van dentro de m_Tutorial (Map_XX_YY.unity3d): también cuentan como fichero de dong.
        public static IEnumerable<string> TutorialMaps(CachingManifest25 m) => m.Lists.TryGetValue("m_Tutorial", out var l) ? l.Select(e => e.FileName).Where(IsDongFile) : Enumerable.Empty<string>();
        public static bool IsDongFile(string n) => n.StartsWith("Map_", StringComparison.Ordinal) || n.StartsWith("DongResources_", StringComparison.Ordinal);
        /// Fase mínima necesaria para jugar en una zona (según la posición del jugador).
        public static Phase For(ZoneType z) => z == ZoneType.Tutorial ? Phase.Tutorial : z == ZoneType.Free ? Phase.FreeZone : Phase.PaidZone;
    }

    public sealed class BundleManager : IReleasableSource, IBundleContents, IAssetLocator
    {
        readonly IBundleSource inner; readonly CachingManifest25 manifest; readonly object gate = new object();
        readonly Dictionary<string, AssetIndex> indexes = new Dictionary<string, AssetIndex>(StringComparer.OrdinalIgnoreCase);   // por nombre de manifiesto
        readonly HashSet<string> loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Dictionary<string, WeakReference<byte[]>>> resident = new Dictionary<string, Dictionary<string, WeakReference<byte[]>>>(StringComparer.OrdinalIgnoreCase);
        public readonly ResourceRouter Router;
        public BundleManager(IBundleSource inner, CachingManifest25 manifest)
        {
            this.inner = inner; this.manifest = manifest; Router = new ResourceRouter(this);
            Router.PaidZoneFiles = manifest.Lists.TryGetValue("m_PaidZone", out var l) ? l.Select(e => e.FileName).ToList() : new List<string>();
        }

        // --- IBundleSource (puede llamarse desde un hilo de fondo) ---
        public Dictionary<string, byte[]> Load(string fileName)
        {
            lock (gate)
            {
                if (resident.TryGetValue(fileName, out var refs))
                {
                    var reused = new Dictionary<string, byte[]>();
                    foreach (var entry in refs) { if (!entry.Value.TryGetTarget(out var bytes)) break; reused.Add(entry.Key, bytes); }
                    if (reused.Count == refs.Count) return reused;
                }
            }
            var files = inner.Load(fileName); if (files == null) return null;
            AssetIndex ai = null;
            if (!fileName.StartsWith("Map_", StringComparison.Ordinal))   // las escenas no tienen AssetBundle/m_Container útil
            {
                ai = new AssetIndex();
                foreach (var kv in files) { try { ai.Add(kv.Key, SerializedFile.Parse(kv.Value)); } catch (Exception) { /* .resS u otros recursos binarios */ } }
            }
            lock (gate) { loaded.Add(fileName); if (ai != null) indexes[fileName] = ai; resident[fileName] = files.ToDictionary(kv => kv.Key, kv => new WeakReference<byte[]>(kv.Value)); }
            return files;
        }
        public void Release(string fileName) { lock (gate) { loaded.Remove(fileName); indexes.Remove(fileName); resident.Remove(fileName); } }

        // --- IBundleContents (para el enrutador) ---
        public bool IsLoaded(string bundle) { lock (gate) return loaded.Contains(bundle); }
        public bool Contains(string bundle, string path) { lock (gate) return indexes.TryGetValue(bundle, out var i) && i.Has(path); }

        // --- IAssetLocator: ruta -> bundle (enrutador) -> objeto ---
        public bool TryLocate(string path, out AssetEntry e)
        {
            e = default;
            lock (gate)
            {
                var bundle = Router.Locate(path);
                if (bundle == null || !indexes.TryGetValue(bundle, out var i)) return false;
                return i.TryGet(path, out e);
            }
        }
        public string BundleFor(string path) { lock (gate) return Router.Locate(path); }
        public void SetPosition(float x, float z) { lock (gate) Router.SetPosition(x, z); }
        public IReadOnlyCollection<string> LoadedFiles { get { lock (gate) return loaded.ToList(); } }
    }

    /// Orquesta las descargas por fase/dong sobre cualquier descargador (BundleCache). Sin Unity.
    public sealed class BundleStreamer
    {
        readonly CachingManifest25 manifest; readonly IBundleDownloader dl; readonly DongRegistry reg;
        public BundleStreamer(CachingManifest25 m, IBundleDownloader d) { manifest = m; dl = d; reg = new DongRegistry(m); }

        /// Todo lo compartido hasta la fase indicada incluida (p. ej. Tutorial = CharacterCreation + CharacterSelection + Tutorial).
        public List<string> FilesUpTo(Phase last)
        {
            var l = new List<string>(); foreach (var p in Phases.Order) { l.AddRange(Phases.Files(manifest, p)); if (p == last) break; }
            return l.Distinct().ToList();
        }
        public List<string> FilesFor(Phase p) => Phases.Files(manifest, p).ToList();

        /// Archivos de un dong; los 9 mapas del tutorial ya están en su fase, pero se piden igual si hace falta.
        public List<string> DongFiles(int x, int y) => reg.Loadable(x, y) || manifest.IsTutorial(x, y) || manifest.IsFree(x, y) || manifest.IsPaid(x, y) ? reg.FilesFor(x, y).Where(f => manifest.Version(f) != 0).ToList() : new List<string>();

        public Task EnsurePhaseAsync(Phase p, IProgress<DownloadProgress> progress = null, System.Threading.CancellationToken ct = default) => dl.EnsureAsync(FilesUpTo(p), progress, 3, ct);
        public Task EnsureDongAsync(int x, int y, IProgress<DownloadProgress> progress = null, System.Threading.CancellationToken ct = default) => dl.EnsureAsync(DongFiles(x, y), progress, 3, ct);
        /// Descarga por adelantado el 3x3 alrededor de (x,y), empezando por el propio dong y luego por cercanía (el cliente carga a <280 m).
        public Task PrefetchAroundAsync(int x, int y, IProgress<DownloadProgress> progress = null, System.Threading.CancellationToken ct = default)
        {
            var cells = new List<(int x, int y)>(); for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) cells.Add((x + dx, y + dy));
            var files = cells.OrderBy(c => Math.Abs(c.x - x) + Math.Abs(c.y - y)).SelectMany(c => DongFiles(c.x, c.y)).Distinct().ToList();
            return dl.EnsureAsync(files, progress, 3, ct);
        }
    }
}
