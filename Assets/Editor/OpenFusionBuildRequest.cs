using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    // Permite solicitar una compilacion al editor abierto sin iniciar otra instancia.
    [InitializeOnLoad]
    public static class OpenFusionBuildRequest
    {
        const string Request = "Temp/openfusion-release.request";
        static OpenFusionBuildRequest() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            File.Delete(Request);
            EditorApplication.delayCall += Run;
        }
        static bool batch;
        public static void Batch() { batch = true; Run(); }
        static async void Run()
        {
            Directory.CreateDirectory("Logs");
            try
            {
                InputRegressionChecks.Run();
                GamepadStartupRegressionChecks.Run();
                CombatRegressionChecks.Run();
                WorldRegressionChecks.Run();
                WeaponAudioRegressionChecks.Run();
                await ConnectionRegressionChecks.Run();
                await AndroidRegressionChecks.Run();
                await StartupRegressionChecks.Run();
                TerrainRegressionChecks.Run();
                // BuildPlayer/Refresh deben ejecutarse fuera de una continuacion
                // async del SynchronizationContext del editor.
                EditorApplication.delayCall += Build;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                File.WriteAllText("Logs/release-result.txt", e.ToString());
                if (batch) EditorApplication.Exit(1);
            }
        }
        static void Build()
        {
            try
            {
                bool ok = BuildScript.Build(false, "Builds/OpenFusion-release.apk");
                File.WriteAllText("Logs/release-result.txt", ok ? "Succeeded" : "Failed");
                if (batch) EditorApplication.Exit(ok ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                File.WriteAllText("Logs/release-result.txt", e.ToString());
                if (batch) EditorApplication.Exit(1);
            }
        }
    }
}
