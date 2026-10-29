using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenFusion.Screens
{
    // Collect only at loading boundaries, never from the gameplay update loop.
    // No asset cache is evicted: serialized data and converted models stay valid.
    public static class PocketIosLoadingMemory
    {
        const long CollectionStep = 32L * 1024 * 1024;
        static long lastCollection;
        static bool pressure;
        static int warnings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (!PocketIos.Available) return;
            Application.lowMemory -= LowMemory;
            Application.lowMemory += LowMemory;
            lastCollection = 0; warnings = 0; pressure = false;
        }

        static void LowMemory() { pressure = true; warnings++; Record("warning", 0); }

        public static async Task BeforeWorldAsync()
        {
            if (!PocketIos.Available) return;
            // Finish deferred destruction of character preview objects first.
            await Task.Yield();
            var unload = Resources.UnloadUnusedAssets();
            while (!unload.isDone) await Task.Yield();
            Collect("world-start", true);
        }

        public static void SharedBundleLoaded()
        {
            if (PocketIos.Available) Collect("shared-bundle", false);
        }

        public static void WorldReady()
        {
            if (PocketIos.Available) Collect("world-ready", true);
        }

        static void Collect(string stage, bool force)
        {
            long before = GC.GetTotalMemory(false);
            if (!force && !pressure && before - lastCollection < CollectionStep) return;
            GC.Collect();
            lastCollection = GC.GetTotalMemory(false); pressure = false;
            Record(stage, Math.Max(0, before - lastCollection));
        }

        static void Record(string stage, long reclaimed)
        {
            // Fixed stage names and numeric counters only; no account or world data.
            string line = DateTime.UtcNow.ToString("O") + " " + stage +
                " managedMB=" + GC.GetTotalMemory(false) / 1048576 +
                " unityMB=" + UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576 +
                " reclaimedMB=" + reclaimed / 1048576 + " warnings=" + warnings;
            Debug.Log("[ios loading memory] " + line);
            try
            {
                var path = Path.Combine(Application.persistentDataPath, "ios-loading-memory.log");
                if (File.Exists(path) && new FileInfo(path).Length > 32768) File.WriteAllText(path, "");
                File.AppendAllText(path, line + "\n");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
