// MaterialSpec -> UnityEngine.Material (shader OpenFusion/Legacy o OpenFusion/Toon). SIN COMPILAR NI PROBAR en Unity.
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFusion.Materials
{
    public static class MaterialBinder
    {
        static BlendMode B(string n)
        {
            switch (n) {
                case "One": return BlendMode.One; case "Zero": return BlendMode.Zero; case "SrcAlpha": return BlendMode.SrcAlpha;
                case "OneMinusSrcAlpha": return BlendMode.OneMinusSrcAlpha; case "SrcColor": return BlendMode.SrcColor;
                case "OneMinusSrcColor": return BlendMode.OneMinusSrcColor; case "DstAlpha": return BlendMode.DstAlpha;
                case "OneMinusDstAlpha": return BlendMode.OneMinusDstAlpha; case "DstColor": return BlendMode.DstColor;
                case "OneMinusDstColor": return BlendMode.OneMinusDstColor; default: return BlendMode.One; }
        }
        static CullMode C(string n) => n == "Off" ? CullMode.Off : n == "Front" ? CullMode.Front : CullMode.Back;

        /// textureLookup: resuelve (fileId, pathId) -> Texture2D ya convertida (o null).
        public static Material Create(MaterialSpec s, Func<TexSlot, Texture> textureLookup)
        {
            var sh = s.Shader; if (sh == null) return null;
            if (StationFx) { var fx = CreateStationFx(s, textureLookup); if (fx != null) return fx; }
            string target = ShaderTargets.Target(sh.Kind, s.ShaderName); var shader = target == null ? null : Shader.Find(target);
            if (shader == null) return null;
            var m = new Material(shader) { name = s.Name, renderQueue = s.RenderQueue };
            m.SetFloat("_SrcBlend", (float)B(sh.Src)); m.SetFloat("_DstBlend", (float)B(sh.Dst));
            m.SetFloat("_ZWrite", sh.ZWrite ? 1 : 0); m.SetFloat("_Cull", (float)C(sh.Cull));
            m.SetFloat("_AlphaTestMode", sh.AlphaTest == null ? 0 : sh.AlphaTest == "GEqual" ? 2 : 1); m.SetFloat("_Cutoff", s.Cutoff);
            // Los shaders originales 'normal_blendSrcalphaInvsrcalphaTest*' / 'normal_transparentcutoutinfrontofWater' tienen 2 pasadas: (1) corte alfa >=0.9 con ZWrite ON y (2) mezcla suave sin ZWrite.
            // El catálogo las colapsaba a la 2a (mezcla + ZWrite off + corte 0.9): sin profundidad, orden dependiente de la vista (submarino "lego") y follaje ralo. Se emula la 1a pasada como corte opaco (umbral 0.5 para no perder hojas con los mipmaps).
            if (sh.Kind == ShaderKind.LegacyLit && sh.Src == "SrcAlpha" && sh.Dst == "OneMinusSrcAlpha" && !sh.ZWrite && sh.AlphaTest == "GEqual")
            { m.SetFloat("_SrcBlend", (float)B("One")); m.SetFloat("_DstBlend", (float)B("Zero")); m.SetFloat("_ZWrite", 1); m.SetFloat("_AlphaTestMode", 1); m.SetFloat("_Cutoff", 0.5f); m.renderQueue = 2450; }
            foreach (var kv in s.Colors) if (HasType(shader, kv.Key, false)) m.SetColor(kv.Key, new Color(kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[3]));
            foreach (var kv in s.Floats) if (HasType(shader, kv.Key, true) && kv.Key != "_Cutoff") m.SetFloat(kv.Key, kv.Value);
            foreach (var kv in s.Textures)
            {
                if (!m.HasProperty(kv.Key)) continue; var t = textureLookup(kv.Value); if (t == null) continue;
                m.SetTexture(kv.Key, t); m.SetTextureScale(kv.Key, new Vector2(kv.Value.ScaleX, kv.Value.ScaleY)); m.SetTextureOffset(kv.Key, new Vector2(kv.Value.OffsetX, kv.Value.OffsetY));
            }
            if (target == ShaderTargets.Vfx) m.SetFloat("_VFXMode", ShaderTargets.VfxMode(s.ShaderName));
            if (target != ShaderTargets.Legacy && target != ShaderTargets.Toon) { /* los demás shaders no usan palabras clave */ }
            else if (sh.Kind == ShaderKind.Toon)
            {
                bool ramp = sh.Props != null && Array.IndexOf(sh.Props, "_ShaderMap") >= 0; SetKw(m, "_OF_RAMP", "_Ramp", ramp); SetKw(m, "_OF_OUTLINE", "_OutlineOn", sh.OutlinePass);
            }
            else
            {
                SetKw(m, "_OF_PARTICLE", "_Particle", sh.Kind == ShaderKind.Particle);
                SetKw(m, "_OF_LIT", "_Lit", sh.Kind == ShaderKind.LegacyLit || sh.Kind == ShaderKind.LegacyGlow);
                SetKw(m, "_OF_VERTEX_AD", "_VertexAD", sh.VertexColorAD); SetKw(m, "_OF_ALPHA_PRIMARY", "_AlphaPrimary", sh.AlphaPrimary); SetKw(m, "_OF_GLOW", "_Glow", sh.Kind == ShaderKind.LegacyGlow);
            }
            return m;
        }
        /// Estacion de nanos (mob/nanomachine.kfm): el clip 'nif-default' trae curvas de material (_Color/_AmbColor/_Emission, _MainTex.offset/rotation) pero con valores de relleno
        /// (todas las curvas valen (-1.9987, ~0, ~0) o ~1e-38; no son datos reales), asi que los anillos no giran, los rayos (_Color negro) quedan invisibles y la esfera no se anima.
        /// Mientras no existan los datos reales, se emulan con OpenFusion/StationFx (aditivo, UV con giro/desplazamiento y parpadeo en el shader). false = materiales Legacy estaticos.
        public static bool StationFx = true;
        static Color Col(MaterialSpec s, string k) { float[] v; return s.Colors != null && s.Colors.TryGetValue(k, out v) && v != null && v.Length >= 3 ? new Color(v[0], v[1], v[2], 1f) : Color.black; }
        static Color Sat2(Color c) { return new Color(Mathf.Clamp01(c.r * 2f), Mathf.Clamp01(c.g * 2f), Mathf.Clamp01(c.b * 2f), 1f); }
        static Material CreateStationFx(MaterialSpec s, Func<TexSlot, Texture> textureLookup)
        {
            string n = s.Name; if (string.IsNullOrEmpty(n) || !n.StartsWith("nanomachine-", StringComparison.Ordinal)) return null;
            bool light = n.IndexOf("lighting_", StringComparison.Ordinal) >= 0, ring = n.IndexOf("gm_06_eff", StringComparison.Ordinal) >= 0, circ = n.IndexOf("gm_05_eff", StringComparison.Ordinal) >= 0;
            if (!light && !ring && !circ) return null;
            var shader = Shader.Find("OpenFusion/StationFx"); if (shader == null) return null;
            TexSlot slot; if (s.Textures == null || !s.Textures.TryGetValue("_MainTex", out slot) || slot == null) return null;
            var tex = textureLookup(slot); if (tex == null) return null;
            var m = new Material(shader) { name = s.Name, renderQueue = s.RenderQueue > 0 ? s.RenderQueue : 3011 };
            m.SetTexture("_MainTex", tex);
            Color c = Col(s, "_Color"), a = Col(s, "_AmbColor"); float seed = 0f; foreach (char ch in n) seed = (seed + ch) % 97f;
            if (light) { m.SetColor("_Tint", Color.white); m.SetFloat("_Flicker", 1f); m.SetFloat("_Seed", seed); }
            else if (ring)
            {
                var t = Sat2(c + a); if (t.r + t.g + t.b < 0.05f) t = Color.white;
                m.SetColor("_Tint", t); m.SetFloat("_Spin", n.IndexOf("dasdfsdf", StringComparison.Ordinal) >= 0 ? -45f : 30f); m.SetFloat("_Pulse", 0.2f); m.SetFloat("_PulseSpeed", 2.5f); m.SetFloat("_Seed", seed);
            }
            else
            {
                var t = Sat2(c); if (t.r + t.g + t.b < 0.05f) t = Color.white;
                m.SetColor("_Tint", t); m.SetVector("_Scroll", new Vector4(0.03f, 0.05f, 0f, 0f)); m.SetFloat("_Pulse", 0.15f); m.SetFloat("_PulseSpeed", 1.5f); m.SetFloat("_Seed", seed);
            }
            return m;
        }

        /// true si el shader tiene la propiedad y su tipo es escalar (Float/Range) o de vector/color. Evita SetFloat sobre un Color (_Emission) y viceversa.
        static bool HasType(Shader sh, string name, bool scalar)
        {
            int i = sh.FindPropertyIndex(name); if (i < 0) return false;
            var t = sh.GetPropertyType(i); bool isScalar = t == UnityEngine.Rendering.ShaderPropertyType.Float || t == UnityEngine.Rendering.ShaderPropertyType.Range;
            return scalar ? isScalar : (t == UnityEngine.Rendering.ShaderPropertyType.Color || t == UnityEngine.Rendering.ShaderPropertyType.Vector);
        }
        static void SetKw(Material m, string kw, string prop, bool on) { if (on) m.EnableKeyword(kw); else m.DisableKeyword(kw); if (m.HasProperty(prop)) m.SetFloat(prop, on ? 1 : 0); }
    }
}
