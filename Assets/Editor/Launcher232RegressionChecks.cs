using System;
using System.Reflection;
using System.IO;
using System.Xml;
using OpenFusion.Screens;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class Launcher232RegressionChecks
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static void Run()
        {
            Check(LauncherServerLink.TryParse("ofl://server/?endpoint=api.dexlabs.systems%2Facademy&description=Academy+Server", out var server, out _) &&
                server.endpoint == "https://api.dexlabs.systems/academy" && server.description == "Academy Server", "Upstream form-encoded link/subpath parsing failed.");
            Check(LauncherServerLink.TryParse("ofl://server?endpoint=http%3A%2F%2Flocalhost%3A8080%2Fapi%2F", out server, out _) &&
                server.endpoint == "http://localhost:8080/api", "Explicit HTTP and API port must be preserved.");
            foreach (string link in new[] {
                "https://server?endpoint=api.example.test", "ofl://unknown?endpoint=api.example.test",
                "ofl://server/path?endpoint=api.example.test", "ofl://server?endpoint=file%3A%2F%2F%2Flocal",
                "ofl://server?endpoint=https%3A%2F%2Fuser%3Apassword%40example.test",
                "ofl://server?endpoint=api.example.test&endpoint=other.test",
                "ofl://server?endpoint=api.example.test&password=secret",
                "ofl://server?endpoint=api.example.test&description=bad%0Aname" })
                Check(!LauncherServerLink.TryParse(link, out _, out _), "Unsafe/ambiguous link accepted.");
            Check(!LauncherServerLink.TryParse("ofl://server?ip=localhost%3A23000&version=beta-20100104", out _, out var error) && error.Contains("API endpoint"), "Unsupported Simple link needs an actionable explanation.");
            Check(LauncherServerLink.NormalizeEndpoint("HTTPS://API.EXAMPLE.TEST/academy/") == "https://api.example.test/academy", "Endpoint identity must normalize scheme, host and trailing slash.");
            foreach (string endpoint in new[] { "https://example.test?token=secret", "https://example.test#frag", "https://exa mple.test", "https://user@example.test" })
                Check(LauncherServerLink.NormalizeEndpoint(endpoint) == null, "Invalid API endpoint accepted.");

            var type = typeof(LauncherScreen);
            var pending = type.GetField("pendingServerLink", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = pending.GetValue(null);
            var host = new GameObject("Launcher232Regression");
            try
            {
                var launcher = host.AddComponent<LauncherScreen>();
                var process = type.GetMethod("ProcessServerLink", Instance);
                string link = "ofl://server?endpoint=api.example.test&description=Test";
                pending.SetValue(null, link);
                process.Invoke(launcher, null);
                Check((string)pending.GetValue(null) == link, "Active game session must defer server links.");
                type.GetField("<Visible>k__BackingField", Instance).SetValue(launcher, true);
                var modal = type.GetField("modal", Instance);
                modal.SetValue(launcher, Enum.Parse(modal.FieldType, "About"));
                process.Invoke(launcher, null);
                Check((string)pending.GetValue(null) == link, "Server links must not overwrite an open modal.");
                modal.SetValue(launcher, Enum.Parse(modal.FieldType, "None"));
                process.Invoke(launcher, null);
                Check(pending.GetValue(null) == null && modal.GetValue(launcher).ToString() == "AddEdit" &&
                    (string)type.GetField("adEndpoint", Instance).GetValue(launcher) == "https://api.example.test", "New endpoint must open a reviewable Add Server form.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); pending.SetValue(null, previous); }
            typeof(PocketDiagnostics).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { "password=secret-test-token", "OpenFusion.Test.Run () (at C:/private/user/file.cs:10)", LogType.Error });
            string report = PocketDiagnostics.Report();
            Check(report.Contains("OpenFusion.Test.Run") && !report.Contains("secret-test-token") && !report.Contains("C:/private"), "Diagnostics must omit messages and filesystem paths.");
            // The Unity Gradle export may retain our renamed activity on incremental builds.
            string fixture = Path.GetFullPath(Path.Combine("Temp", "launcher232-" + Guid.NewGuid().ToString("N"), "unityLibrary"));
            Directory.CreateDirectory(Path.Combine(fixture, "PocketDualScreen.androidlib"));
            Directory.CreateDirectory(Path.Combine(fixture, "src", "main"));
            File.WriteAllText(Path.Combine(fixture, "PocketDualScreen.androidlib", "build.gradle"), "");
            string manifest = Path.Combine(fixture, "src", "main", "AndroidManifest.xml");
            File.WriteAllText(manifest, "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application><activity android:name=\"com.unity3d.player.UnityPlayerActivity\" /></application></manifest>");
            var callback = new DualScreenAndroidBuild();
            callback.OnPostGenerateGradleAndroidProject(fixture);
            callback.OnPostGenerateGradleAndroidProject(fixture);
            var doc = new XmlDocument(); doc.Load(manifest);
            Check(doc.GetElementsByTagName("data").Count == 1 && File.ReadAllText(manifest).Contains("BROWSABLE"), "Incremental Gradle export must retain exactly one deep-link registration.");
            Debug.Log("[launcher-2.3.2] PASS: upstream links, validation, deferred sessions/modals, confirmation form and diagnostics privacy.");
        }
    }
}
