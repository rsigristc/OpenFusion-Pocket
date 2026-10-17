// Estado de los dongs (cargado / colores / atributos), sin dependencias de Unity.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class DongState
    {
        public int X, Y; public bool Loaded, Loading; public float RetryAt; public DongColors Colors; public byte[] Attributes; public int HmW = 129, HmH = 129; public float SizeX = 512, SizeZ = 512;
        public float OriginX, OriginZ, OriginY; public List<string> StoreFiles = new List<string>();
    }

    public sealed class DongRegistry
    {
        public readonly DongState[] Dongs = new DongState[DongGrid.Count * DongGrid.Count];
        public ZoneType Zone = ZoneType.Tutorial; public CachingManifest25 Manifest;
        public DongRegistry(CachingManifest25 m) { Manifest = m; for (int i = 0; i < Dongs.Length; i++) Dongs[i] = new DongState { X = i % 16, Y = i / 16 }; }
        public DongState At(int x, int y) => x >= 0 && y >= 0 && x < 16 && y < 16 ? Dongs[y * 16 + x] : null;
        public bool Loaded(int x, int y) => At(x, y)?.Loaded ?? false;
        public bool Loadable(int x, int y) => Manifest.Has(x, y, Zone);
        public (bool load, int x, int y, bool unload, int ux, int uy) Step(float px, float pz) => DongGrid.Step(px, pz, Loaded, Loadable);
        /// Zona del jugador al entrar al mundo (DongLoader.LoadPosition)
        public void SetZoneFor(float px, float pz) { DongGrid.Cell(px, pz, out int x, out int y); Zone = Manifest.IsTutorial(x, y) ? ZoneType.Tutorial : Manifest.IsFree(x, y) ? ZoneType.Free : ZoneType.Paid; }
        public DongColors ColorsAt(int x, int y) => At(x, y)?.Colors ?? default;
        public void PositionColor(float px, float pz, out float[] fog, out float[] sky, out float[] light, out float fogDepth, out float[] rim)
            => DongGrid.PositionColor(px, pz, ColorsAt, out fog, out sky, out light, out fogDepth, out rim);
        public byte AttributeAt(float wx, float wz, out bool known)
        {
            DongGrid.Cell(wx, wz, out int x, out int y); var d = At(x, y); known = d != null && d.Loaded && d.Attributes != null;
            return known ? DongGrid.Attribute(d.Attributes, d.HmW, d.HmH, d.SizeX, d.SizeZ, d.OriginX, d.OriginZ, wx, wz, out _) : (byte)0;
        }
        /// Archivos del manifiesto que necesita un dong: Map_XX_YY.unity3d y, fuera del tutorial, DongResources_XX_YY.resourceFile
        public IEnumerable<string> FilesFor(int x, int y)
        {
            yield return DongGrid.MapName(x, y) + ".unity3d";
            if (!Manifest.IsTutorial(x, y)) yield return DongGrid.Prefix("DongResources_", x, y) + ".resourceFile";
        }
        /// Archivos compartidos (no por dong) de una zona, en el orden en que deben estar cargados
        public IEnumerable<string> SharedFiles(ZoneType z) => Manifest.ForZone(z).Select(e => e.FileName).Where(n => !n.StartsWith("Map_") && !n.StartsWith("DongResources_"));
    }
}
