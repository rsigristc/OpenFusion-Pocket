using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace OpenFusion.EditorTools
{
    public static class PocketIosBuild
    {
        public const string Identifier = "com.openfusion.pocket.ios";
        public const string MinimumOS = "16.0";
        public const string DefaultOutput = "Builds/iOS/Xcode";
        [MenuItem("OpenFusion/iOS/Configure single-screen preview")]
        public static void Configure()
        {
            PlayerSettings.companyName = "OpenFusion"; PlayerSettings.productName = "OpenFusion:Pocket";
            PlayerSettings.bundleVersion = OpenFusion.Api.PocketRelease.IosAppVersion;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            var target = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(target, Identifier);
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Low);
            PlayerSettings.SetArchitecture(target, 1); // ARM64, device build.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetOSVersionString = MinimumOS;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            string previewNumber = Environment.GetEnvironmentVariable("POCKET_IOS_BUILD_NUMBER");
            if (!string.IsNullOrEmpty(previewNumber) && (!int.TryParse(previewNumber, out int build) || build < 1))
                throw new BuildFailedException("POCKET_IOS_BUILD_NUMBER must be a positive integer.");
            PlayerSettings.iOS.buildNumber = string.IsNullOrEmpty(previewNumber) ? OpenFusion.Api.PocketRelease.IosBuildCode.ToString() : previewNumber;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIcon/FusionSpawnIcon-v1.png");
            if (icon != null) {
                var sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.iOS, IconKind.Application);
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.iOS, sizes.Select(_ => icon).ToArray(), IconKind.Application);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("OpenFusion/iOS/Export Xcode preview")]
        public static void ExportMenu() { Export(DefaultOutput); }
        public static void BatchExport()
        {
            try {
                var output = Environment.GetEnvironmentVariable("POCKET_IOS_XCODE_OUTPUT");
                bool ok = Export(string.IsNullOrWhiteSpace(output) ? DefaultOutput : output);
                EditorApplication.Exit(ok ? 0 : 1);
            } catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }
        public static bool Export(string output)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new BuildFailedException("Install iOS Build Support for this Unity Editor before exporting. Xcode on macOS is required to compile, sign and export the IPA.");
            IosPreparationChecks.Run();
            SerializedReaderRegressionChecks.Run();
            UpdaterRegressionChecks.Run();
            Configure(); ProjectSetup.ConfigureRenderPipeline(); ProjectSetup.ConfigureShaders(); ProjectSetup.CreateScenes();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new BuildFailedException("No enabled game scene.");
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = output, target = BuildTarget.iOS, targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.CompressWithLz4
            });
            Directory.CreateDirectory("Logs/ios");
            string result = "Xcode export: " + report.summary.result + "; errors=" + report.summary.totalErrors + "; warnings=" + report.summary.totalWarnings + "; path=" + Path.GetFullPath(output) + ". This is not a signed IPA or a device test.";
            File.WriteAllText("Logs/ios/export-result.txt", result); Debug.Log(result);
            return report.summary.result == BuildResult.Succeeded;
        }
    }
#if UNITY_IOS
    public sealed class PocketIosPostprocess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 30;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            string root = report.summary.outputPath;
            string projectPath = PBXProject.GetPBXProjectPath(root);
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            string framework = project.GetUnityFrameworkTargetGuid();
            project.AddFrameworkToProject(framework, "Security.framework", false);
            project.AddFrameworkToProject(framework, "UIKit.framework", false);
            string plugin = project.FindFileGuidByProjectPath("Libraries/Plugins/iOS/PocketIOS.mm");
            if (string.IsNullOrEmpty(plugin)) throw new BuildFailedException("PocketIOS.mm was not added to the exported Xcode project. Verify the iOS plugin import settings.");
            project.SetCompileFlagsForFile(framework, plugin, new[] { "-fobjc-arc" }.ToList());
            project.WriteToFile(projectPath);
            string plistPath = Path.Combine(root, "Info.plist");
            var plist = new PlistDocument(); plist.ReadFromFile(plistPath);
            plist.root.SetString("NSLocalNetworkUsageDescription", "Connect to a FusionFall server hosted on your local network.");
            plist.root.SetBoolean("UIRequiresFullScreen", true);
            var types = plist.root.values.TryGetValue("CFBundleURLTypes", out var existing) ? existing.AsArray() : plist.root.CreateArray("CFBundleURLTypes");
            var entry = types.AddDict(); entry.SetString("CFBundleURLName", "OpenFusion launcher server link"); entry.CreateArray("CFBundleURLSchemes").AddString("ofl");
            plist.WriteToFile(plistPath);
            // Signing is deliberately left to the owner's Xcode team/profile.
        }
    }
#endif
}
