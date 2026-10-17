// Diagnóstico visual: vuelca a Logs/openfusion-materials.txt los materiales de todos los Renderer de la escena
// (shader, blend, ZWrite, cola, texturas, colores) y el estado de iluminación/niebla. Menú: OpenFusion/5 Dump scene materials.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class SceneDiagnostics
    {
        [MenuItem("OpenFusion/5 Dump scene materials")]
        public static void Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Unity {Application.unityVersion}  colorSpace={PlayerSettings.colorSpace}  playing={Application.isPlaying}");
            sb.AppendLine($"RenderSettings: fog={RenderSettings.fog} fogColor={RenderSettings.fogColor} ambientMode={RenderSettings.ambientMode} ambientLight={RenderSettings.ambientLight} skybox={(RenderSettings.skybox ? RenderSettings.skybox.name : "null")}");
            var cam = Camera.main; if (cam) sb.AppendLine($"Camera: clear={cam.clearFlags} bg={cam.backgroundColor} fov={cam.fieldOfView} near={cam.nearClipPlane} far={cam.farClipPlane} hdr={cam.allowHDR} pos={cam.transform.position} rot={cam.transform.eulerAngles}");
            foreach (var l in Object.FindObjectsOfType<Light>()) sb.AppendLine($"Light {l.name}: {l.type} color={l.color} intensity={l.intensity} rot={l.transform.eulerAngles} shadows={l.shadows}");
            foreach (var n in new[] { "_OF_Light0Dir", "_OF_Light0Color", "_OF_Ambient" })
                sb.AppendLine($"global {n} = {(n == "_OF_Light0Dir" ? Shader.GetGlobalVector(n).ToString() : Shader.GetGlobalColor(n).ToString())}");
            sb.AppendLine($"global _OF_FogDepth = {Shader.GetGlobalFloat("_OF_FogDepth")}");

            var rends = Object.FindObjectsOfType<Renderer>(true);
            sb.AppendLine($"\nRenderers: {rends.Length}");
            var byShader = rends.SelectMany(r => r.sharedMaterials.Where(m => m)).GroupBy(m => m.shader ? m.shader.name : "(null)").OrderByDescending(g => g.Count());
            sb.AppendLine("\n== Materiales (instancias) por shader");
            foreach (var g in byShader) sb.AppendLine($"{g.Count(),6}  {g.Key}");

            sb.AppendLine("\n== Materiales únicos (hasta 120)");
            var seen = new HashSet<Material>(); int k = 0;
            foreach (var r in rends) foreach (var m in r.sharedMaterials)
            {
                if (!m || !seen.Add(m) || k++ >= 120) continue;
                var s = m.shader; sb.Append($"{m.name} | {(s ? s.name : "null")} | queue={m.renderQueue}");
                foreach (var p in new[] { "_SrcBlend", "_DstBlend", "_ZWrite", "_Cull", "_AlphaTestMode", "_Cutoff", "_Lit", "_Particle", "_VertexAD", "_Glow", "_AlphaPrimary" }) if (m.HasProperty(p)) sb.Append($" {p}={m.GetFloat(p)}");
                foreach (var p in new[] { "_Color", "_AmbColor", "_Emission", "_TintColor" }) if (m.HasProperty(p)) sb.Append($" {p}={m.GetColor(p)}");
                var mt = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null; sb.Append($" tex={(mt ? mt.name + " " + mt.width + "x" + mt.height : "null")}");
                sb.AppendLine($" | usado en: {r.name} ({r.transform.root.name})");
            }

            sb.AppendLine("\n== Renderers más grandes (por bounds) y qué hay en y < -140 (bajo el suelo)");
            foreach (var r in rends.Where(r => r.enabled && r.gameObject.activeInHierarchy).OrderByDescending(r => r.bounds.size.sqrMagnitude).Take(25))
            {
                var ms = string.Join(";", r.sharedMaterials.Where(m => m).Select(m => m.name + "{" + (m.shader ? m.shader.name : "?") + " q=" + m.renderQueue + (m.HasProperty("_SrcBlend") ? " blend=" + m.GetFloat("_SrcBlend") + "/" + m.GetFloat("_DstBlend") + " zw=" + m.GetFloat("_ZWrite") : "") + "}"));
                sb.AppendLine($"{r.name} [{r.GetType().Name}] centro={r.bounds.center} tam={r.bounds.size} capa={LayerMask.LayerToName(r.gameObject.layer)} raiz={r.transform.root.name} mats={ms}");
            }
            foreach (var r in rends.Where(r => r.enabled && r.bounds.min.y < -140 && r.bounds.max.y > -140).Take(15))
                sb.AppendLine($"cruza y=-140: {r.name} y={r.bounds.min.y:F0}..{r.bounds.max.y:F0} mats={string.Join(";", r.sharedMaterials.Where(m => m).Select(m => m.name + " q=" + m.renderQueue))}");
            sb.AppendLine("\n== Terrenos");
            foreach (var t in Object.FindObjectsOfType<Terrain>())
                sb.AppendLine($"{t.name} pos={t.transform.position} size={t.terrainData.size} layers={t.terrainData.terrainLayers.Length} materialTemplate={(t.materialTemplate ? t.materialTemplate.shader.name : "null (pipeline default)")} q={(t.materialTemplate ? t.materialTemplate.renderQueue : -1)} activo={t.gameObject.activeInHierarchy} bounds={t.terrainData.bounds.center}/{t.terrainData.bounds.size}");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/openfusion-materials.txt", sb.ToString());
            Debug.Log("OpenFusion: informe escrito en Logs/openfusion-materials.txt (" + rends.Length + " renderers)");
        }
    }
}
#endif
