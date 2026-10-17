using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OpenFusion.Api;

namespace OpenFusion.Assets
{
    public sealed class GameCacheInfo
    {
        public string serverId, uuid, name;
        public long expectedBytes, downloadedBytes;
        const string Metadata = "cache-info.json";
        public static void Record(string serverRoot, string serverId, VersionManifest version, BundleCatalog catalog)
        {
            Directory.CreateDirectory(serverRoot);
            var info = new GameCacheInfo { serverId = serverId, uuid = version.uuid, name = version.name,
                expectedBytes = catalog.All.Sum(i => i.Info.compressed_info?.size ?? 0) + (version.main_file_info?.size ?? 0) };
            File.WriteAllText(Path.Combine(serverRoot, Metadata), JsonConvert.SerializeObject(info));
        }
        public static List<GameCacheInfo> Scan(string bundlesRoot, Dictionary<string, string> names)
        {
            var rows = new List<GameCacheInfo>(); if (!Directory.Exists(bundlesRoot)) return rows;
            foreach (var serverRoot in Directory.GetDirectories(bundlesRoot))
            {
                GameCacheInfo info = null;
                try
                {
                    var metadata = Path.Combine(serverRoot, Metadata);
                    if (File.Exists(metadata)) info = JsonConvert.DeserializeObject<GameCacheInfo>(File.ReadAllText(metadata));
                    if (info == null)
                    {
                        var main = Directory.EnumerateFiles(serverRoot, "main.unity3d", SearchOption.AllDirectories)
                            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                        if (main == null) continue;
                        info = new GameCacheInfo { serverId = Path.GetFileName(serverRoot), uuid = Path.GetFileName(Path.GetDirectoryName(main)) };
                    }
                    if (string.IsNullOrEmpty(info.name))
                        info.name = names.TryGetValue(info.uuid ?? "", out var label) ? label : KnownGameVersions.NameFor(info.uuid) ?? info.uuid;
                    info.downloadedBytes = Directory.EnumerateFiles(serverRoot, "*", SearchOption.AllDirectories)
                        .Where(f => Path.GetFileName(f) != Metadata && !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                        .Sum(f => new FileInfo(f).Length);
                    rows.Add(info);
                }
                catch (IOException) { } // A download or cache clear can finish while the UI scans.
            }
            return rows.OrderBy(r => r.name).ToList();
        }
    }
}
