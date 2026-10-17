using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenFusion.Api;
using OpenFusion.Assets;
using UnityEngine;

namespace OpenFusion.World
{
    // Opt-in ADB diagnostic: measures cached reads without logging in, building
    // the world, retaining bundles, or changing account/graphics settings.
    public static class CacheReadProbe
    {
        sealed class SavedChain { public List<VersionManifest> chain; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CheckRequest()
        {
            string root = Application.persistentDataPath;
            string request = Path.Combine(root, "cache-read-probe.request");
            if (!File.Exists(request)) return;
            string server = File.ReadAllText(request).Trim(); File.Delete(request);
            if (string.IsNullOrEmpty(server) || server != Path.GetFileName(server)) return;
            _ = Task.Run(() => Run(root, server));
        }
        static void Run(string root, string server)
        {
            string output = Path.Combine(root, "cache-read-probe.log");
            try
            {
                File.WriteAllText(output, DateTime.UtcNow.ToString("O") + " START\n");
                string manifests = Path.Combine(root, "version_manifests", server);
                var chainFile = Directory.GetFiles(manifests, "*.json")[0];
                var chain = JsonConvert.DeserializeObject<SavedChain>(File.ReadAllText(chainFile)).chain;
                string bundles = Path.Combine(root, "bundles", server);
                var data = JsonConvert.DeserializeObject<MainGameData>(File.ReadAllText(Path.Combine(bundles, chain[0].uuid, "main-game-data.json")));
                var cache = new BundleCache(bundles, BundleCatalog.FromChain(chain));
                cache.LoadLog = message => File.AppendAllText(output, message + "\n");
                var names = new BundleStreamer(data.Manifest, cache).FilesUpTo(Phase.PaidZone);
                for (int pass = 1; pass <= 2; pass++)
                {
                    var timer = Stopwatch.StartNew(); int count = 0;
                    File.AppendAllText(output, "PASS " + pass + " BEGIN\n");
                    foreach (var name in names)
                    {
                        var files = cache.Load(name);
                        if (files == null) File.AppendAllText(output, "NOT DOWNLOADED " + name + "\n");
                        else count++;
                        files = null;
                        // Avoid retaining a complete world's byte arrays on a 3 GB device.
                        GC.Collect();
                    }
                    File.AppendAllText(output, "PASS " + pass + " END files=" + count + " elapsedMs=" + timer.ElapsedMilliseconds + "\n");
                }
                File.AppendAllText(output, "DONE\n");
            }
            catch (Exception e) { File.AppendAllText(output, "FAILED " + e + "\n"); }
        }
    }
}
