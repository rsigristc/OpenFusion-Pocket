using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace OpenFusion.Api
{
    public sealed class PocketRelease
    {
        public const string Repository = "https://github.com/rsigristc/OpenFusion-Pocket";
        public const string LatestApi = "https://api.github.com/repos/rsigristc/OpenFusion-Pocket/releases/latest";
        public const int BuildCode = 5;
        public const string AppVersion = "2.1";
        public const string Package = "com.openfusion.android";
        public string Tag, Title, Notes, Page, ManifestUrl, ApkUrl, ApkName, Sha256, Version;
        public long Size, VersionCode;

        public static bool RepositoryUrl(string value, bool asset)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var u) || u.Scheme != "https" || !u.IsDefaultPort || !string.IsNullOrEmpty(u.UserInfo) || !string.IsNullOrEmpty(u.Query) || !string.IsNullOrEmpty(u.Fragment)) return false;
            string path = u.AbsolutePath;
            return u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
                path.StartsWith("/rsigristc/OpenFusion-Pocket/releases/" + (asset ? "download/" : "tag/"), StringComparison.Ordinal);
        }
        public static System.Version ParseVersion(string v)
        {
            if (v != null && Regex.IsMatch(v, @"^[vV]?\d+\.\d+(\.\d+)?(\.\d+)?$"))
                if (System.Version.TryParse(v.TrimStart('v', 'V'), out var result))
                    return new System.Version(result.Major, result.Minor, Math.Max(0, result.Build), Math.Max(0, result.Revision));
            return null;
        }
        public static PocketRelease Read(string json)
        {
            var j = JObject.Parse(json);
            if ((bool?)j["draft"] == true || (bool?)j["prerelease"] == true) throw new InvalidDataException("Only published stable releases are supported.");
            string tag = (string)j["tag_name"], page = (string)j["html_url"];
            if (ParseVersion(tag) == null || !RepositoryUrl(page, false) || page != Repository + "/releases/tag/" + tag) throw new InvalidDataException("Invalid release identity.");
            var assets = j["assets"] as JArray;
            var manifest = assets?.OfType<JObject>().SingleOrDefault(a => (string)a["name"] == "update.json" && (string)a["state"] == "uploaded");
            string url = (string)manifest?["browser_download_url"];
            if (!RepositoryUrl(url, true) || url != Repository + "/releases/download/" + tag + "/update.json") throw new InvalidDataException("This release does not contain a valid update manifest. Open GitHub to download it manually.");
            return new PocketRelease { Tag = tag, Title = (string)j["name"] ?? tag, Notes = (string)j["body"] ?? "No release notes provided.", Page = page, ManifestUrl = url };
        }
        public void ApplyManifest(string manifestJson, string releaseJson)
        {
            var m = JObject.Parse(manifestJson);
            string version = (string)m["version"], name = (string)m["apk_asset"], hash = (string)m["sha256"];
            long code = (long?)m["version_code"] ?? 0, size = (long?)m["size"] ?? 0;
            if ((int?)m["schema"] != 1 || (string)m["package"] != Package || ParseVersion(version) == null || ParseVersion(version) != ParseVersion(Tag) || code <= 0 || size <= 0 || size > 1024L * 1024 * 1024 || string.IsNullOrEmpty(name) || !Regex.IsMatch(name, @"^OpenFusion-Pocket-[A-Za-z0-9._-]+\.apk$") || hash == null || !Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Invalid update manifest.");
            var assets = JObject.Parse(releaseJson)["assets"] as JArray;
            var apk = assets?.OfType<JObject>().SingleOrDefault(a => (string)a["name"] == name && (string)a["state"] == "uploaded");
            string url = (string)apk?["browser_download_url"], digest = (string)apk?["digest"];
            if (!RepositoryUrl(url, true) || url != Repository + "/releases/download/" + Tag + "/" + name || (long?)apk?["size"] != size || (!string.IsNullOrEmpty(digest) && !string.Equals(digest, "sha256:" + hash, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("APK metadata does not match the release.");
            Version = version; VersionCode = code; ApkName = name; ApkUrl = url; Size = size; Sha256 = hash.ToLowerInvariant();
        }
        public bool NewerThan(string installed, long code)
        {
            var current = ParseVersion(installed); var target = ParseVersion(Version);
            return current != null && target != null && target >= current && VersionCode > code;
        }
        public bool Verify(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != Size || string.IsNullOrEmpty(Sha256)) return false;
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), Sha256, StringComparison.OrdinalIgnoreCase);
        }
    }
}
