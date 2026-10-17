using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using OpenFusion.World;
using OpenFusion.Unity25;

namespace OpenFusion.EditorTools
{
    public static class TerrainRegressionChecks
    {
        public static void Run()
        {
            GameObject root = null; Texture2D diffuse = null;
            try
            {
                diffuse = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                diffuse.Apply();
                var t = new Terrain25 { Name = "TerrainRegression", Width = 33, Height = 33,
                    ScaleX = 1f, ScaleY = 8f, ScaleZ = 1f, Heights = new ushort[33 * 33], AlphamapResolution = 16 };
                t.Splats.Add(new SplatLayer25 { Texture = new PPtr25 { PathId = 1 }, TileX = 1, TileY = 1 });
                t.Splats.Add(new SplatLayer25 { Texture = new PPtr25 { PathId = 2 }, TileX = 1, TileY = 1 });
                t.AlphaTextures.Add(new PPtr25 { PathId = 99 });
                // Mascara rectangular: G=255 izquierda, G=0 derecha.
                var alpha = new Texture25 { Name = "AlphaData", Width = 2, Height = 1, Format = 4,
                    Data = new byte[] { 0, 255, 0, 0, 0, 0, 0, 0 } };
                int cpuReads = 0;
                root = TerrainBuilder.Build(t, p =>
                {
                    if (p.PathId == 99) throw new Exception("La mascara no debe pasar por una textura GPU comprimida.");
                    return diffuse;
                }, resolveAlphaData: p => { cpuReads++; return alpha; });
                var terrain = root.GetComponent<Terrain>();
                var weights = terrain.terrainData.GetAlphamaps(0, 0, 16, 16);
                if (cpuReads != 1 || weights[0, 0, 1] < 0.99f || weights[0, 15, 0] < 0.99f)
                    throw new Exception("La mezcla del terreno no coincide con los datos originales.");
                if (terrain.materialTemplate == null || terrain.materialTemplate.shader.name != "Universal Render Pipeline/Terrain/Lit")
                    throw new Exception("Falta el material URP del terreno.");
                if (!terrain.materialTemplate.enableInstancing || !terrain.materialTemplate.IsKeywordEnabled("_TERRAIN_INSTANCED_PERPIXEL_NORMAL"))
                    throw new Exception("Falta la normal por pixel del terreno instanciado (regresion GLES).");
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    if (Mathf.Abs(weights[y, x, 0] + weights[y, x, 1] - 1f) > 0.01f)
                        throw new Exception("Los pesos del terreno no estan normalizados.");
                ProjectSetup.ConfigureShaders();
                var graphics = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset"));
                if (graphics.FindProperty("m_InstancingStripping").intValue != 2)
                    throw new Exception("Se eliminarian variantes de terrenos instanciados.");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/terrain-regression-results.txt", "PASS: mascara CPU, sin leer textura GPU; mascara rectangular; pesos normalizados; material Terrain/Lit; variantes de instancing conservadas.");
                Debug.Log("OpenFusion: regresion de terreno OK.");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (diffuse != null) UnityEngine.Object.DestroyImmediate(diffuse);
            }
        }
    }
}
