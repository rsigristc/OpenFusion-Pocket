using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using OpenFusion.Api;

namespace OpenFusion.Screens
{
    // Independent allowlisted journal: never export Player.log/logcat, messages,
    // preferences, accounts or packets. Threaded capture never calls Unity APIs.
    public static class PocketDiagnostics
    {
        const int MaxEvents = 192, MaxBytes = 256 * 1024;
        static readonly object gate = new object();
        static readonly Queue<string> traces = new Queue<string>();
        static readonly Dictionary<string, DateTime> recent = new Dictionary<string, DateTime>();
        static readonly Regex methodName = new Regex(@"^(OpenFusion|UnityEngine|Unity|System|TMPro)\.[A-Za-z0-9_.+<>`:/-]{1,180}$");
        static readonly Regex eventLine = new Regex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ\|(Error|Exception|Assert|Warning|Asset|Session)\|[A-Za-z0-9_.:,<>`+/-]{1,1500}$");
        static readonly string[] exceptionTypes = { "NullReferenceException", "OutOfMemoryException", "IndexOutOfRangeException", "ArgumentException", "ArgumentNullException", "InvalidOperationException", "IOException", "FileNotFoundException", "NotSupportedException", "TaskCanceledException", "OperationCanceledException", "HttpRequestException", "AndroidJavaException" };
        static bool initialized;
        static string journal, previous = "";
        static int suppressed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            StartJournal(Path.Combine(Application.persistentDataPath, "safe-diagnostics"));
            Application.logMessageReceivedThreaded += Capture;
            Record("Session", "Started");
            Application.quitting += () => Record("Session", "CleanExit");
        }
        static void StartJournal(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                journal = Path.Combine(dir, "current.txt");
                string old = Path.Combine(dir, "previous.txt");
                previous = ReadJournal(journal);
                if (previous.Length == 0) previous = ReadJournal(old);
                File.WriteAllText(old, previous, new UTF8Encoding(false));
                File.WriteAllText(journal, "", new UTF8Encoding(false));
            }
            catch { journal = null; }
        }
        static string ReadJournal(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > MaxBytes) return "";
                var rows = new Queue<string>();
                foreach (string line in File.ReadLines(path))
                    if (ValidEvent(line)) { rows.Enqueue(line); if (rows.Count > MaxEvents) rows.Dequeue(); }
                return string.Join("\n", rows);
            }
            catch { return ""; }
        }
        static bool ValidEvent(string line)
        {
            if (line == null || line.Length > 1600 || !eventLine.IsMatch(line)) return false;
            string[] fields = line.Split('|');
            if (fields[1] == "Session") return fields[2] == "Started" || fields[2] == "CleanExit";
            if (fields[1] == "Asset")
            {
                string[] parts = fields[2].Split(':');
                return parts.Length == 2 && (parts[0] == "Missing" || parts[0] == "ReadFailed") && SafeAsset(parts[1]) == parts[1];
            }
            string[] details = fields[2].Split(',');
            bool known = details[0] == "Managed" || details[0] == "MissingResource" || details[0] == "Shader" || details[0] == "LoadFailure";
            foreach (string exception in exceptionTypes) known |= details[0] == "Managed." + exception;
            if (!known) return false;
            for (int i = 1; i < details.Length; i++) if (!methodName.IsMatch(details[i])) return false;
            return details.Length <= 7;
        }
        static void Record(string type, string detail)
        {
            string key = type + "|" + detail;
            DateTime now = DateTime.UtcNow;
            lock (gate)
            {
                if (recent.TryGetValue(key, out var last) && (now - last).TotalSeconds < 10) { suppressed++; return; }
                if (recent.Count >= MaxEvents) recent.Clear();
                recent[key] = now;
                string line = now.ToString("yyyy-MM-ddTHH:mm:ssZ") + "|" + key;
                if (!ValidEvent(line)) return;
                traces.Enqueue(line); while (traces.Count > MaxEvents) traces.Dequeue();
                if (journal == null) return;
                try
                {
                    if (new FileInfo(journal).Length + line.Length + 1 > MaxBytes)
                        File.WriteAllText(journal, string.Join("\n", traces) + "\n", new UTF8Encoding(false));
                    else File.AppendAllText(journal, line + "\n", new UTF8Encoding(false));
                }
                catch { } // No recursive logging from the threaded callback.
            }
        }
        static void Capture(string message, string stack, LogType type)
        {
            string category = "Managed";
            if (type == LogType.Warning)
            {
                // Classify fixed prefixes, never retain message content.
                string m = message ?? "";
                if (m.StartsWith("[intro] missing animation", StringComparison.Ordinal) || m.StartsWith("[bitmapfont]", StringComparison.Ordinal) ||
                    m.StartsWith("[bala]", StringComparison.Ordinal) || m.StartsWith("[map] no se encontro", StringComparison.Ordinal)) category = "MissingResource";
                else if (m.StartsWith("Shader", StringComparison.Ordinal)) category = "Shader";
                else if (m.StartsWith("Bundle ", StringComparison.Ordinal) || m.StartsWith("Bundle compartido ", StringComparison.Ordinal) ||
                    m.StartsWith("[DS] Single-screen fallback:", StringComparison.Ordinal)) category = "LoadFailure";
                else return;
            }
            else if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (category == "Managed") foreach (string exception in exceptionTypes)
                if ((message ?? "").StartsWith(exception + ":", StringComparison.Ordinal) || (message ?? "").StartsWith("System." + exception + ":", StringComparison.Ordinal))
                { category += "." + exception; break; }
            var methods = new StringBuilder(category);
            int count = 0;
            string bounded = stack ?? ""; if (bounded.Length > 12000) bounded = bounded.Substring(0, 12000);
            foreach (string line in bounded.Split('\n'))
            {
                int end = line.IndexOf('('); if (end <= 0) continue;
                string method = line.Substring(0, end).Trim();
                if (method.StartsWith("at ", StringComparison.Ordinal)) method = method.Substring(3);
                if (!methodName.IsMatch(method)) continue;
                methods.Append(',').Append(method); if (++count == 6) break;
            }
            Record(type.ToString(), methods.ToString());
        }
        // Export only known built-in names / numeric map cells. Arbitrary resource
        // paths might contain account data, so they become the fixed Other category.
        static string SafeAsset(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 100) return "Other";
            if (Regex.IsMatch(value, @"^(Map_\d{2}_\d{2}\.unity3d|DongResources_\d{2}_\d{2}\.resourceFile)$")) return value;
            if (Regex.IsMatch(value, @"^Effect-\d{1,5}$")) return value;
            switch (value)
            {
                case "Other": case "TutorialAsset": case "NanoAsset": case "IconAsset": case "SoundAsset": case "ModelAsset":
                case "main.unity3d": case "Tutorial.resourceFile": case "CharacterCreation.resourceFile":
                case "CharacterSelection.resourceFile": case "Effects.resourceFile": case "Nano.resourceFile": case "FutureNano.resourceFile":
                case "Icons.resourceFile": case "TableData.resourceFile": case "CharTexture.resourceFile": case "NpcTexture.resourceFile":
                case "Retro_shared.resourceFile": case "Retro_shared_part2.resourceFile": case "Freearea_shared.resourceFile":
                case "World_shared.resourceFile": case "World_shared_part2.resourceFile":
                case "World_shared_part3.resourceFile": case "World_shared_part4.resourceFile": case "World_shared_part5.resourceFile":
                case "FUSound.resourceFile": case "LobbyMusic.resourceFile": case "FutureMusic.resourceFile": case "PastMusic.resourceFile": case "RetroMusic.resourceFile": return value;
                default: return "Other";
            }
        }
        public static void AssetFailure(string bundle, bool readFailed = false) => Record("Asset", (readFailed ? "ReadFailed:" : "Missing:") + SafeAsset(bundle));
        public static void Caught(Exception exception)
        {
            if (exception == null) return;
            exception = exception.GetBaseException();
            // Only the allowlisted type name and real managed frames enter Capture.
            Capture(exception.GetType().Name + ":", exception.StackTrace, LogType.Exception);
        }
        public static void MissingResource(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            // These are probes as well as actual failures: report categories, not filenames.
            string category = "Other";
            var effect = Regex.Match(path.Length <= 100 ? path : "", @"^Prefabs/Particle/EffectScripts/ES\[(\d{1,5})\]\.prefab$", RegexOptions.IgnoreCase);
            if (effect.Success) category = "Effect-" + effect.Groups[1].Value;
            else if (path.StartsWith("TutorialAssets/", StringComparison.OrdinalIgnoreCase)) category = "TutorialAsset";
            else if (path.StartsWith("Nano", StringComparison.OrdinalIgnoreCase)) category = "NanoAsset";
            else if (path.StartsWith("Icons", StringComparison.OrdinalIgnoreCase)) category = "IconAsset";
            else if (path.StartsWith("bgm/", StringComparison.OrdinalIgnoreCase) || path.IndexOf(" Sound/", StringComparison.OrdinalIgnoreCase) >= 0) category = "SoundAsset";
            else if (path.StartsWith("mob/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("wear/", StringComparison.OrdinalIgnoreCase)) category = "ModelAsset";
            AssetFailure(category);
        }
        static string Hardware(string value)
        {
            string clean = Regex.Replace(value ?? "unknown", @"[^A-Za-z0-9 ._()+:/-]", "?");
            return clean.Substring(0, Math.Min(180, clean.Length));
        }
        public static string Report()
        {
            var report = new StringBuilder("OpenFusion:Pocket - Export Logs / schema 1\n");
            report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            report.AppendLine("Pocket: " + Application.version + " / build " + PocketRelease.BuildCode + " / build GUID " + Application.buildGUID);
            report.AppendLine("Launcher feature reference: OpenFusionLauncher " + LauncherServerLink.UpstreamVersion);
            report.AppendLine("Engine: Unity " + Application.unityVersion + " / " + IntPtr.Size * 8 + "-bit / " + (Debug.isDebugBuild ? "development" : "release"));
            report.AppendLine("OS: " + Hardware(SystemInfo.operatingSystem));
            report.AppendLine("Device model: " + Hardware(SystemInfo.deviceModel));
            report.AppendLine("GPU: " + Hardware(SystemInfo.graphicsDeviceName) + " / " + SystemInfo.graphicsDeviceType);
            report.AppendLine("Graphics: " + Hardware(SystemInfo.graphicsDeviceVersion));
            report.AppendLine("Memory MB: system=" + SystemInfo.systemMemorySize + " graphics=" + SystemInfo.graphicsMemorySize + " managed=" + GC.GetTotalMemory(false) / 1048576);
            report.AppendLine("Window: " + Screen.width + "x" + Screen.height + " / " + Screen.orientation);
            report.AppendLine("DS active: " + (OpenFusion.World.DualScreenController.Instance?.Active == true));
            report.AppendLine(PocketPlatform.DiagnosticsExitInfo());
            report.AppendLine("Privacy: no accounts, credentials, tokens, player IDs/names, chat, mail, URLs, local paths, screenshots or packet payloads. No raw log files are attached.");
            report.AppendLine("Events are capped; repeats suppressed for 10 seconds. Missing entries may include normal fallback probes. No full native crash dump is collected.");
            lock (gate)
            {
                report.AppendLine("\nPREVIOUS SESSION (may have ended normally; no CleanExit is not proof of a crash):");
                report.AppendLine(previous.Length > 0 ? previous : "No retained events.");
                report.AppendLine("\nCURRENT SESSION:");
                foreach (string trace in traces) report.AppendLine(trace);
                report.AppendLine("Suppressed repeats: " + suppressed);
            }
            return report.ToString();
        }
    }
}
