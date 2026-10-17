// Activa el post-proceso de URP (glow/bloom, FXAA, color) asignando PostProcessData a los renderers: sin eso el opcion GLOW y el filtro de color de Settings no se ven.
// Se ejecuta sola al cargar el editor (comprobacion barata) y desde el menu OpenFusion. SIN COMPILAR EN UNITY.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OpenFusion.EditorTools
{
    [InitializeOnLoad]
    public static class PostProcessSetup
    {
        const string PackagePath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        static PostProcessSetup() { EditorApplication.delayCall += () => Ensure(false); }

        [MenuItem("OpenFusion/7 Activar post-proceso (glow, FXAA, color)")]
        public static void Menu() { Ensure(true); }

        public static void Ensure(bool verbose)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(PackagePath);
            if (data == null) { foreach (var g in AssetDatabase.FindAssets("t:PostProcessData")) { data = AssetDatabase.LoadAssetAtPath<PostProcessData>(AssetDatabase.GUIDToAssetPath(g)); if (data != null) break; } }
            if (data == null) { if (verbose) Debug.LogWarning("OpenFusion: no se encontro PostProcessData en el paquete de URP."); return; }
            bool changed = false;
            foreach (var g in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(g));
                if (rd != null && rd.postProcessData == null) { rd.postProcessData = data; EditorUtility.SetDirty(rd); changed = true; }
            }
            if (changed) { AssetDatabase.SaveAssets(); Debug.Log("OpenFusion: post-proceso de URP activado (PostProcessData asignado a los renderers)."); }
            else if (verbose) Debug.Log("OpenFusion: el post-proceso ya estaba activado.");
        }
    }
}
