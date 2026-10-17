using System;
using System.IO;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenFusion.Api;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class UpdaterRegressionChecks
    {
        public static void VerifyApk(string path)
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                bool installer = false, provider = false;
                foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("classes") && e.FullName.EndsWith(".dex")))
                using (var stream = entry.Open()) using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory); string dex = Encoding.ASCII.GetString(memory.ToArray());
                    installer |= dex.Contains("Lcom/openfusion/updater/PocketInstaller;"); provider |= dex.Contains("Lcom/openfusion/updater/PocketApkProvider;");
                }
                Check(installer && provider, "Updater Android classes were not included in the APK");
                var manifest = zip.GetEntry("AndroidManifest.xml"); Check(manifest != null, "APK manifest missing");
                using (var stream = manifest.Open()) using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory); byte[] bytes = memory.ToArray(); string strings = Encoding.UTF8.GetString(bytes) + Encoding.Unicode.GetString(bytes);
                    Check(strings.Contains("android.permission.REQUEST_INSTALL_PACKAGES") && strings.Contains("com.openfusion.updater.PocketApkProvider") && strings.Contains("com.openfusion.android.updates"), "Updater permission/provider missing from merged Android manifest");
                }
            }
            Debug.Log("[updater-regression] APK installer classes, provider and permission verified");
        }
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Reject(Action parse, string message)
        { bool rejected = false; try { parse(); } catch (InvalidDataException) { rejected = true; } Check(rejected, message); }
        public static void Run()
        {
            const string root = PocketRelease.Repository + "/releases/download/v1.1/";
            byte[] bytes = { 1, 2, 3, 4, 5 }; string sha;
            using (var h = SHA256.Create()) sha = BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            var apk = new JObject { ["name"] = "OpenFusion-Pocket-v1.1.apk", ["size"] = bytes.Length, ["state"] = "uploaded", ["digest"] = "sha256:" + sha, ["browser_download_url"] = root + "OpenFusion-Pocket-v1.1.apk" };
            var release = new JObject { ["tag_name"] = "v1.1", ["name"] = "Pocket 1.1", ["html_url"] = PocketRelease.Repository + "/releases/tag/v1.1", ["body"] = "## Changes\n- Camera fix", ["draft"] = false, ["prerelease"] = false, ["assets"] = new JArray(new JObject { ["name"] = "update.json", ["state"] = "uploaded", ["browser_download_url"] = root + "update.json" }, apk) };
            var manifest = new JObject { ["schema"] = 1, ["version"] = "1.1", ["version_code"] = 3, ["package"] = PocketRelease.Package, ["apk_asset"] = "OpenFusion-Pocket-v1.1.apk", ["size"] = bytes.Length, ["sha256"] = sha };
            Func<PocketRelease> read = () => { var r = PocketRelease.Read(release.ToString()); r.ApplyManifest(manifest.ToString(), release.ToString()); return r; };
            var info = read();
            Check(info.Notes == "## Changes\n- Camera fix" && info.NewerThan("1.0", 2), "GitHub changelog or newer-version detection");
            Check(!info.NewerThan("1.2", 2) && !info.NewerThan("1.1", 3) && !info.NewerThan("1.1", 4), "Updater offered a downgrade or reinstall");
            Check(PocketRelease.ParseVersion("v1.0") == PocketRelease.ParseVersion("1.0.0"), "Equivalent semantic versions differ");
            info.Version = "1.0"; info.VersionCode = 3;
            Check(info.NewerThan("1.0", 2), "Same-release build revision not detected");
            foreach (string bad in new[] { "http://github.com/rsigristc/OpenFusion-Pocket/releases/download/v1.1/update.json", "https://github.com.evil.example/rsigristc/OpenFusion-Pocket/releases/download/v1.1/update.json", "https://github.com/other/other/releases/download/v1.1/update.json", root + "update.json?token=secret", "https://github.com:444/rsigristc/OpenFusion-Pocket/releases/download/v1.1/update.json" })
                Check(!PocketRelease.RepositoryUrl(bad, true), "Untrusted update URL accepted");
            manifest["package"] = "com.other.app"; Reject(() => read(), "Wrong package accepted"); manifest["package"] = PocketRelease.Package;
            manifest["sha256"] = new string('0', 64); Reject(() => read(), "Release/manifest hash mismatch accepted"); manifest["sha256"] = sha;
            manifest["version"] = "1.2"; Reject(() => read(), "Manifest/tag version mismatch accepted"); manifest["version"] = "1.1";
            manifest["apk_asset"] = "../app.apk"; Reject(() => read(), "Unsafe APK filename accepted"); manifest["apk_asset"] = "OpenFusion-Pocket-v1.1.apk";
            manifest["size"] = 0; Reject(() => read(), "Empty APK accepted"); manifest["size"] = bytes.Length;
            release["draft"] = true; Reject(() => read(), "Draft release accepted"); release["draft"] = false;
            release["prerelease"] = true; Reject(() => read(), "Prerelease accepted"); release["prerelease"] = false;
            apk["browser_download_url"] = "https://github.com/other/other/releases/download/v1.1/app.apk"; Reject(() => read(), "Cross-repository APK accepted"); apk["browser_download_url"] = root + "OpenFusion-Pocket-v1.1.apk";
            var path = Path.Combine(Path.GetTempPath(), "pocket-update-regression-" + Guid.NewGuid().ToString("N"));
            try
            {
                info = read(); File.WriteAllBytes(path, bytes); Check(info.Verify(path), "Correct download rejected");
                File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 6 }); Check(!info.Verify(path), "Tampered download accepted");
                File.WriteAllBytes(path, new byte[] { 1, 2 }); Check(!info.Verify(path), "Partial download accepted");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
            File.WriteAllText("Logs/updater-regression-results.txt", "PASS: GitHub changelog; semantic versions and same-release build revisions; downgrade/reinstall rejection; trusted origin; package/version/hash/size validation; stable-only channel; partial/tampered download rejection.\n");
            Debug.Log("[updater-regression] Passed");
        }
    }
}
