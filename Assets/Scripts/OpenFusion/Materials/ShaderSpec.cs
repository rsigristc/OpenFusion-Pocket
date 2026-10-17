// Estado de render real de cada shader legacy (extraído de su ShaderLab, NO del nombre: 31 de 66 nombres
// normal_*/particle_* no reflejan su estado real). Catálogo generado en Generated/ShaderCatalog.g.cs.
using System.Collections.Generic;

namespace OpenFusion.Materials
{
    public enum ShaderKind { LegacyLit, LegacyGlow, Particle, Toon, Vfx, FusionMatter, FusionEffect, Water, DiffuseFade, Other }

    public sealed class ShaderSpec
    {
        public ShaderKind Kind; public string Src, Dst, Cull, AlphaTest, AlphaTestValue;
        public bool ZWrite, VertexColorAD, Lighting, HasProgram, OutlinePass, AlphaPrimary; public int Queue; public string[] Props;
        /// Nombre del shader moderno que lo implementa (null = sin portar todavía).
        public string TargetShader
        {
            get
            {
                switch (Kind)
                {
                    case ShaderKind.LegacyLit: case ShaderKind.LegacyGlow: case ShaderKind.Particle: case ShaderKind.Other: return "OpenFusion/Legacy";
                    case ShaderKind.Toon: return "OpenFusion/Toon";
                    default: return null;
                }
            }
        }
    }
}
