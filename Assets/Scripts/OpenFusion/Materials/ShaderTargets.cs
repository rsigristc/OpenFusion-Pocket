// Qué shader moderno implementa cada shader legacy (por familia y, para VFX/Other, por nombre). Sin dependencias de Unity.
namespace OpenFusion.Materials
{
    public static class ShaderTargets
    {
        public const string Legacy = "OpenFusion/Legacy", Toon = "OpenFusion/Toon", Vfx = "OpenFusion/VFX", Hologram = "OpenFusion/Hologram",
            FusionMatter = "OpenFusion/FusionMatter", Scroll = "OpenFusion/Scroll", Water = "OpenFusion/Water", MiniMap = "OpenFusion/MiniMap";

        public static string Target(ShaderKind kind, string shaderName)
        {
            switch (kind)
            {
                case ShaderKind.LegacyLit: case ShaderKind.LegacyGlow: case ShaderKind.Particle: return Legacy;
                case ShaderKind.Toon: return Toon;
                case ShaderKind.Vfx: return shaderName != null && shaderName.Contains("HologramSolid") ? Hologram : Vfx;
                case ShaderKind.FusionMatter: return FusionMatter;
                case ShaderKind.FusionEffect: case ShaderKind.DiffuseFade: return Scroll;
                case ShaderKind.Water: return Water;
                case ShaderKind.Other: return shaderName == "MiniMapRender" ? MiniMap : Legacy;   // fusionStar y RetroLit/otros: sin luz o lit simple
                default: return null;
            }
        }

        /// Modo de OpenFusion/VFX: 0 Dual, 1 Polar, 2 Flipbook, 3 Rotate, 4 DistortVignette, 5 DistortGradient; -1 si no es VFX.
        public static int VfxMode(string shaderName)
        {
            if (shaderName == null || !shaderName.StartsWith("Custom/VFX/")) return -1;
            if (shaderName.Contains("DualScrolling")) return 0;
            if (shaderName.Contains("PolarScrolling")) return 1;
            if (shaderName.Contains("RotatingFlipbook")) return 2;
            if (shaderName.Contains("TextureRotate")) return 3;
            if (shaderName.Contains("ScrollDistort_wMask_Vignette")) return 4;
            if (shaderName.Contains("ScrollDistort_wMask_Gradient")) return 5;
            return -1;
        }
    }
}
