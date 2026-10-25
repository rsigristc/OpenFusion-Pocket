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
        static bool batch, inputPreview;
        static string output = "Builds/OpenFusion-release.apk";
        public static void Batch() { batch = true; Run(); }
        public static void BatchInputPreview() { batch = inputPreview = true; output = "Builds/OpenFusion-Pocket-controls-preview.apk"; Run(); }
        public static void BatchDS() { batch = true; output = "Builds/OpenFusion-Pocket-DS-preview.apk"; Run(); }
        static async void Run()
        {
            Directory.CreateDirectory("Logs");
            try
            {
                InputRegressionChecks.Run();
                Launcher232RegressionChecks.Run();
                PlatformInputRegressionChecks.Run();
                DualScreenRegressionChecks.Run();
                DualNanoRegressionChecks.Run();
                DualEquipmentRegressionChecks.Run();
                DualGumballRegressionChecks.Run();
                DualMissionRegressionChecks.Run();
                DualEmailRegressionChecks.Run();
                GamepadStartupRegressionChecks.Run();
                CombatRegressionChecks.Run();
                CombatFeedbackRegressionChecks.Run();
                LegacyRespawnRegressionChecks.Run();
                DeathTutorialRegressionChecks.Run();
                TutorialIntroRegressionChecks.Run();
                TutorialTrainingRegressionChecks.Run();
                TutorialAssetAudit.Run();
                WorldRegressionChecks.Run();
                RemotePlayerRegressionChecks.Run();
                RenderIsolationRegressionChecks.Run();
                CameraGroundingRegressionChecks.Run();
                UpdaterRegressionChecks.Run();
                WeaponAudioRegressionChecks.Run();
                await Retrobution303RegressionChecks.Run();
                await ConnectionRegressionChecks.Run();
                await CharacterCreationRegressionChecks.Run();
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
                bool ok = BuildScript.Build(false, output, inputPreview ? new[] { "POCKET_INPUT_PREVIEW" } : null);
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
