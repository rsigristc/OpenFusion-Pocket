using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class IosPreparationChecks
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        public static void Run()
        {
            string native = File.ReadAllText("Assets/Plugins/iOS/PocketIOS.mm");
            string managed = File.ReadAllText("Assets/Scripts/OpenFusion/Screens/PocketIos.cs");
            foreach (Match symbol in Regex.Matches(managed, @"extern\s+(?:int|void|IntPtr)\s+(PocketIOS_\w+)\("))
                Check(native.Contains(symbol.Groups[1].Value + "("), "Missing native iOS entry point: " + symbol.Groups[1].Value);
            Check(native.Contains("kSecAttrAccessibleWhenUnlockedThisDeviceOnly") && native.Contains("PocketCredentialService"), "Credentials must remain scoped to app-specific, device-local Keychain items.");
            Check(!native.Contains("NSLog(") && !managed.Contains("Debug.Log"), "Credential bridge must not log arguments or secrets.");
            Check(!File.ReadAllText("Assets/Scripts/OpenFusion/World/DualScreenController.cs").Contains("AndroidJava"), "DS controller must not directly depend on JNI types in the iOS player.");
            Check(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIcon/FusionSpawnIcon-v1.png") != null, "Shared application icon is missing.");
            Directory.CreateDirectory("Logs/ios");
            File.WriteAllText("Logs/ios/preparation-checks.txt", "PASS: managed/native entry-point agreement, credential scope/logging guards, isolated DS JNI bridge and shared icon. Source checks only; Keychain, share sheet, Metal and signing require macOS/device validation. iOS Build Support installed: " + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS));
            Debug.Log("[ios preparation] source checks passed; native runtime not certified");
        }
    }
}
