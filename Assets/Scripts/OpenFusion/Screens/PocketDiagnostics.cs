using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using OpenFusion.Api;

namespace OpenFusion.Screens
{
    public static class PocketDiagnostics
    {
        static readonly Queue<string> traces = new Queue<string>();
        static bool initialized;
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Application.logMessageReceivedThreaded += Capture;
        }
        static void Capture(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            // Deliberately omit messages, file paths, account files and packet logs.
            // Keep only method names from Unity's managed stack trace.
            var methods = new StringBuilder(type.ToString());
            foreach (string line in (stack ?? "").Split('\n'))
            {
                string method = line.Trim(); int end = method.IndexOf('(');
                if (end <= 0) continue;
                method = method.Substring(0, end).Trim();
                if (method.Length > 200 || !System.Text.RegularExpressions.Regex.IsMatch(method, @"^[\w.<>:` +]+$")) continue;
                methods.Append('\n').Append(method);
                if (methods.Length > 2000) break;
            }
            lock (traces) { traces.Enqueue(methods.ToString()); while (traces.Count > 20) traces.Dequeue(); }
        }
        public static string Report()
        {
            var report = new StringBuilder("OpenFusion:Pocket diagnostics\n");
            report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            report.AppendLine("Pocket: " + Application.version + " / build " + PocketRelease.BuildCode);
            report.AppendLine("Launcher feature reference: OpenFusionLauncher " + LauncherServerLink.UpstreamVersion);
            report.AppendLine("Engine: Unity " + Application.unityVersion + " / " + (IntPtr.Size * 8) + "-bit");
            report.AppendLine("OS: " + SystemInfo.operatingSystem);
            report.AppendLine("GPU: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType);
            report.AppendLine("Graphics: " + SystemInfo.graphicsDeviceVersion);
            report.AppendLine("Memory MB: system=" + SystemInfo.systemMemorySize + " managed=" + GC.GetTotalMemory(false) / 1048576);
            report.AppendLine("Window: " + Screen.width + "x" + Screen.height + " / " + Screen.orientation);
            report.AppendLine("Account data, server URLs, log messages and raw packet logs are excluded.");
            lock (traces) foreach (string trace in traces) report.AppendLine(trace);
            return report.ToString();
        }
    }
}
