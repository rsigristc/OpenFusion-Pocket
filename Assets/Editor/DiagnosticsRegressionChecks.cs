using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenFusion.Screens;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class DiagnosticsRegressionChecks
    {
        const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
        static object Invoke(string name, params object[] args) => typeof(PocketDiagnostics).GetMethod(name, Flags).Invoke(null, args);
        static FieldInfo Field(string name) => typeof(PocketDiagnostics).GetField(name, Flags);
        public static void Run()
        {
            string dir = Path.GetFullPath(Path.Combine("Temp", "diagnostics-" + Guid.NewGuid().ToString("N")));
            var rows = (Queue<string>)Field("traces").GetValue(null); var savedRows = rows.ToArray();
            var recent = (Dictionary<string, DateTime>)Field("recent").GetValue(null); var savedRecent = new Dictionary<string, DateTime>(recent);
            object oldPath = Field("journal").GetValue(null), oldPrevious = Field("previous").GetValue(null), oldSuppressed = Field("suppressed").GetValue(null);
            try
            {
                rows.Clear(); recent.Clear(); Invoke("StartJournal", dir);
                string secret = "SECRET_ACCOUNT_TOKEN";
                Invoke("Capture", "NullReferenceException: password=" + secret + " https://account.invalid/?token=" + secret,
                    "OpenFusion.Test.Safe () (at C:/private/" + secret + ".cs:10)\nSystem.Net.Http.Send (Authorization: " + secret + ")\nC:/private/" + secret + "()\nOpenFusion.Test/<Load>d__1:MoveNext ()\nat OpenFusion.Test.Managed () in C:/private/" + secret, LogType.Exception);
                Invoke("Capture", "[intro] missing animation " + secret, "OpenFusion.Test.Animation ()", LogType.Warning);
                PocketDiagnostics.AssetFailure("https://host/" + secret + ".unity3d");
                PocketDiagnostics.AssetFailure("Map_12_02.unity3d");
                PocketDiagnostics.MissingResource("Prefabs/Particle/EffectScripts/ES[740].prefab");
                PocketDiagnostics.MissingResource("TutorialAssets/" + secret);
                string report = PocketDiagnostics.Report();
                Check(report.Contains("Managed.NullReferenceException") && report.Contains("OpenFusion.Test.Safe") && report.Contains("MissingResource") && report.Contains("Effect-740") && report.Contains("Map_12_02"), "Useful safe error types, methods, effect IDs and bundle failures must survive export.");
                Check(!report.Contains(secret) && !report.Contains("C:/private") && !report.Contains("account.invalid") && !report.Contains("Authorization:"), "Export leaked account/message/path data.");
                Check(report.Contains("OpenFusion.Test/<Load>d__1:MoveNext") && report.Contains("OpenFusion.Test.Managed"), "Unity coroutine frames and CLR at-prefixed frames must retain methods only.");
                string disk = File.ReadAllText(Path.Combine(dir, "current.txt"));
                Check(!disk.Contains(secret), "Unsafe data reached persistent journal before export.");
                for (int i = 0; i < 1000; i++) PocketDiagnostics.AssetFailure("Map_12_02.unity3d");
                Check(rows.Count <= 8 && (int)Field("suppressed").GetValue(null) >= 1000, "Repeated probes must not flood diagnostics.");
                File.AppendAllText(Path.Combine(dir, "current.txt"), "\npassword=" + secret + "\n2026-10-27T00:00:00Z|Asset|Missing:" + secret + "\n");
                Invoke("StartJournal", dir);
                string previous = (string)Field("previous").GetValue(null);
                Check(previous.Contains("Effect-740") && !previous.Contains(secret), "Previous-session rotation must retain safe errors and reject arbitrary file contents.");
                rows.Clear(); recent.Clear();
                for (int i = 0; i < 5000; i++) Invoke("Capture", secret, "OpenFusion.Test.Method" + i + " ()", LogType.Error);
                Check(rows.Count == 192 && new FileInfo(Path.Combine(dir, "current.txt")).Length <= 256 * 1024, "Journals must remain bounded under distinct errors.");
                string oversized = Path.Combine(dir, "oversized.txt"); File.WriteAllText(oversized, new string('x', 262145));
                Check((string)Invoke("ReadJournal", oversized) == "", "Oversized files must not enter reports.");
            }
            finally
            {
                rows.Clear(); foreach (string row in savedRows) rows.Enqueue(row);
                recent.Clear(); foreach (var item in savedRecent) recent.Add(item.Key, item.Value);
                Field("journal").SetValue(null, oldPath); Field("previous").SetValue(null, oldPrevious); Field("suppressed").SetValue(null, oldSuppressed);
                // Only delete the generated fixture after resolving it inside Temp.
                if (dir.StartsWith(Path.GetFullPath("Temp") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Directory.Delete(dir, true);
            }
            Debug.Log("[diagnostics] PASS: privacy before persistence/export, restart rotation, malformed files, repeated probes and bounded journals.");
        }
    }
}
