// Material de Unity 2.5 (diccionario del lector genérico) + ShaderSpec -> especificación para el material moderno.
using System;
using System.Collections.Generic;

namespace OpenFusion.Materials
{
    public sealed class TexSlot { public int FileId; public long PathId; public float ScaleX = 1, ScaleY = 1, OffsetX, OffsetY; }

    public sealed class MaterialSpec
    {
        public string Name, ShaderName; public ShaderSpec Shader;
        public Dictionary<string, float[]> Colors = new Dictionary<string, float[]>();
        public Dictionary<string, float> Floats = new Dictionary<string, float>();
        public Dictionary<string, TexSlot> Textures = new Dictionary<string, TexSlot>();
        public int RenderQueue;          // m_CustomRenderQueue si existe, si no el del shader
        public float Cutoff;             // valor efectivo del alpha test (0.01 / constante / _Cutoff del material)
    }

    public static class MaterialMapper
    {
        static string PName(object pair) => (string)((Dictionary<string, object>)((Dictionary<string, object>)pair)["first"])["name"];
        static object PVal(object pair) => ((Dictionary<string, object>)pair)["second"];

        /// shaderName ya resuelto (PPtr -> Shader -> nombre). Devuelve null si el shader no está en el catálogo.
        public static MaterialSpec Map(Dictionary<string, object> mat, string shaderName)
        {
            var m = new MaterialSpec { Name = (string)mat["m_Name"], ShaderName = shaderName };
            ShaderCatalog.All.TryGetValue(shaderName ?? "", out m.Shader);
            var sv = (Dictionary<string, object>)mat["m_SavedProperties"];
            foreach (var p in (object[])sv["m_TexEnvs"])
            {
                var t = (Dictionary<string, object>)PVal(p); var tex = (Dictionary<string, object>)t["m_Texture"];
                var sc = (Dictionary<string, object>)t["m_Scale"]; var of = (Dictionary<string, object>)t["m_Offset"];
                m.Textures[PName(p)] = new TexSlot { FileId = System.Convert.ToInt32(tex["m_FileID"]), PathId = System.Convert.ToInt64(tex["m_PathID"]),
                    ScaleX = (float)sc["x"], ScaleY = (float)sc["y"], OffsetX = (float)of["x"], OffsetY = (float)of["y"] };
            }
            foreach (var p in (object[])sv["m_Floats"]) m.Floats[PName(p)] = (float)PVal(p);
            foreach (var p in (object[])sv["m_Colors"]) { var c = (Dictionary<string, object>)PVal(p); m.Colors[PName(p)] = new[] { (float)c["r"], (float)c["g"], (float)c["b"], (float)c["a"] }; }
            m.RenderQueue = m.Shader != null ? m.Shader.Queue : 2000;
            if (mat.TryGetValue("m_CustomRenderQueue", out var crq) && System.Convert.ToInt32(crq) >= 0) m.RenderQueue = System.Convert.ToInt32(crq);
            m.Cutoff = 0;
            if (m.Shader?.AlphaTest != null)
            {
                var v = m.Shader.AlphaTestValue;
                if (v.StartsWith("_")) m.Cutoff = m.Floats.TryGetValue(v, out var f) ? f : 0;
                else float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out m.Cutoff);
            }
            return m;
        }
    }
}
