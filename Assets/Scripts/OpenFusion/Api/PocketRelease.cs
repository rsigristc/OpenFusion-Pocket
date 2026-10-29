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
        public const int BuildCode = 7;
        public const string AppVersion = "3.1";
        public const int IosBuildCode = 8;
        public const string IosAppVersion = "0.5";
        public const string IosBundle = "com.openfusion.pocket.ios";
        public const string IosApi = "https://api.github.com/repos/rsigristc/OpenFusion-Pocket/releases?per_page=100";
        public const string IosInstallHelp = Repository + "/blob/main/docs/IOS_INSTALL.md";
        public const string Package = "com.openfusion.android";
        public string Tag, Title, Notes, Page, ManifestUrl, ApkUrl, ApkName, Sha256, Version;
        public long Size, VersionCode;
        public bool IsIos;
        public string MinimumOs;

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
        public static PocketRelease ReadIosFeed(string json)
        {
            var candidates = JArray.Parse(json).OfType<JObject>().Where(j =>
                (bool?)j["draft"] != true && (bool?)j["prerelease"] != true &&
                ((string)j["tag_name"] ?? "").StartsWith("ios-v", StringComparison.Ordinal) &&
                ParseVersion(((string)j["tag_name"]).Substring(4)) != null)
                .OrderByDescending(j => ParseVersion(((string)j["tag_name"]).Substring(4)));
            var latest = candidates.FirstOrDefault();
            if (latest == null) throw new InvalidDataException("No published iOS release is available yet.");
            return Read(latest.ToString(), true);
        }
        public static PocketRelease Read(string json, bool ios = false)
        {
            var j = JObject.Parse(json);
            if ((bool?)j["draft"] == true || (bool?)j["prerelease"] == true) throw new InvalidDataException("Only published stable releases are supported.");
            string tag = (string)j["tag_name"], page = (string)j["html_url"];
            string versionTag = ios && tag != null && tag.StartsWith("ios-v", StringComparison.Ordinal) ? tag.Substring(4) : ios ? null : tag;
            if (ParseVersion(versionTag) == null || !RepositoryUrl(page, false) || page != Repository + "/releases/tag/" + tag) throw new InvalidDataException("Invalid release identity.");
            var assets = j["assets"] as JArray;
            string manifestName = ios ? "update-ios.json" : "update.json";
            var manifest = assets?.OfType<JObject>().SingleOrDefault(a => (string)a["name"] == manifestName && (string)a["state"] == "uploaded");
            string url = (string)manifest?["browser_download_url"];
            if (!RepositoryUrl(url, true) || url != Repository + "/releases/download/" + tag + "/" + manifestName) throw new InvalidDataException("This release does not contain a valid update manifest. Open GitHub to download it manually.");
            return new PocketRelease { IsIos = ios, Tag = tag, Title = (string)j["name"] ?? tag, Notes = (string)j["body"] ?? "No release notes provided.", Page = page, ManifestUrl = url };
        }
        public void ApplyManifest(string manifestJson, string releaseJson)
        {
            var m = JObject.Parse(manifestJson);
            string version = (string)m["version"], name = (string)m[IsIos ? "ipa_asset" : "apk_asset"], hash = (string)m["sha256"];
            long code = (long?)m["version_code"] ?? 0, size = (long?)m["size"] ?? 0;
            string versionTag = IsIos ? Tag.Substring(4) : Tag;
            if ((int?)m["schema"] != 1 || (string)m[IsIos ? "bundle" : "package"] != (IsIos ? IosBundle : Package) || (IsIos && (string)m["platform"] != "ios") || ParseVersion(version) == null || ParseVersion(version) != ParseVersion(versionTag) || code <= 0 || size <= 0 || size > 1024L * 1024 * 1024 || string.IsNullOrEmpty(name) || !Regex.IsMatch(name, IsIos ? @"^OpenFusion-Pocket-[A-Za-z0-9._-]+\.ipa$" : @"^OpenFusion-Pocket-[A-Za-z0-9._-]+\.apk$") || hash == null || !Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Invalid update manifest.");
            if (IsIos && ParseVersion((string)m["minimum_os"]) == null) throw new InvalidDataException("Invalid iOS minimum OS version.");
            var document = JToken.Parse(releaseJson);
            var release = document as JObject;
            if (release == null && IsIos && document is JArray feed) release = feed.OfType<JObject>().SingleOrDefault(j => (string)j["tag_name"] == Tag);
            var assets = release?["assets"] as JArray;
            var apk = assets?.OfType<JObject>().SingleOrDefault(a => (string)a["name"] == name && (string)a["state"] == "uploaded");
            string url = (string)apk?["browser_download_url"], digest = (string)apk?["digest"];
            if (!RepositoryUrl(url, true) || url != Repository + "/releases/download/" + Tag + "/" + name || (long?)apk?["size"] != size || (!string.IsNullOrEmpty(digest) && !string.Equals(digest, "sha256:" + hash, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Application asset metadata does not match the release.");
            Version = version; VersionCode = code; ApkName = name; ApkUrl = url; Size = size; Sha256 = hash.ToLowerInvariant();
            MinimumOs = IsIos ? (string)m["minimum_os"] : null;
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
