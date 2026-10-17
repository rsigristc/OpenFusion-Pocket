// Orígenes de bundles: devuelven los archivos internos (nombre -> bytes) de un archivo del manifiesto. Sin dependencias de Unity.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public interface IBundleSource
    {
        /// fileName: tal como aparece en el manifiesto (p.ej. "Map_01_00.unity3d"). null si no está disponible.
        Dictionary<string, byte[]> Load(string fileName);
    }

    /// Fuente que quiere saber cuándo un bundle ya no se usa (descargado de memoria).
    public interface IReleasableSource : IBundleSource { void Release(string fileName); }

    /// Lee la caché del launcher: ffcache/<guid>/<nombre en minúsculas, '_'->"_5f", '.'->"_2e">/<archivo interno> (ya descomprimidos).
    public sealed class FfCacheBundleSource : IBundleSource
    {
        readonly string root;
        public FfCacheBundleSource(string ffcacheOrGuidDir) { root = ffcacheOrGuidDir; }
        /// nombre en minúsculas con "_"->"_5f" y "."->"_2e", pero la extensión conserva mayúsculas (p.ej. "map_5f01_5f00_2eunity3d", "tutorial_2eresourceFile")
        public static string Escape(string fileName) { int i = fileName.LastIndexOf('.'); string b = i < 0 ? fileName : fileName.Substring(0, i), e = i < 0 ? "" : fileName.Substring(i + 1); return b.ToLowerInvariant().Replace("_", "_5f") + (i < 0 ? "" : "_2e" + e); }
        IEnumerable<string> Dirs() { yield return root; foreach (var d in Directory.GetDirectories(root)) yield return d; }
        public Dictionary<string, byte[]> Load(string fileName)
        {
            string esc = Escape(fileName);
            foreach (var d in Dirs()) { var p = Path.Combine(d, esc); if (Directory.Exists(p)) return Directory.GetFiles(p).ToDictionary(Path.GetFileName, File.ReadAllBytes); }
            return null;
        }
    }

    /// Descarga CDN: el archivo es un UnityWeb (LZMA) como main.unity3d. downloader devuelve los bytes crudos (síncrono o desde caché en disco).
    public sealed class UnityWebBundleSource : IBundleSource
    {
        readonly Func<string, byte[]> downloader;
        public UnityWebBundleSource(Func<string, byte[]> d) { downloader = d; }
        public Dictionary<string, byte[]> Load(string fileName) { var raw = downloader(fileName); return raw == null ? null : new Dictionary<string, byte[]>(UnityWebBundle.Parse(raw).Files); }
    }
}
