using System;
using System.IO;
using System.Threading.Tasks;
using OpenFusion.Api;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenFusion.Screens
{
    public sealed class PocketUpdater : MonoBehaviour
    {
        public PocketRelease Latest { get; private set; }
        public bool Checking { get; private set; }
        public bool Downloading { get; private set; }
        public bool Ready { get; private set; }
        public bool Available => Latest != null && Latest.NewerThan(Application.version, InstalledCode);
        public string Status { get; private set; } = "Updates have not been checked yet.";
        public float Progress => active == null ? 0f : Mathf.Clamp01(active.downloadProgress);
        public long InstalledCode { get; private set; } = PocketRelease.BuildCode;
        UnityWebRequest active; bool destroyed, cancelRequested; string apkPath, verifiedHash;

        void Awake()
        {
            apkPath = Path.Combine(Application.temporaryCachePath, "updates", "OpenFusion-Pocket.apk");
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var helper = new AndroidJavaClass("com.openfusion.updater.PocketInstaller")) using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity")) { InstalledCode = helper.CallStatic<long>("installedCode", activity); apkPath = helper.CallStatic<string>("updatePath", activity); } }
            catch (Exception e) { Debug.LogWarning("[updater] build code: " + e.Message); }
#endif
        }
        static Task Wait(AsyncOperation operation)
        {
            if (operation.isDone) return Task.CompletedTask;
            var done = new TaskCompletionSource<bool>(); operation.completed += _ => done.TrySetResult(true); return done.Task;
        }
        async Task<string> Text(string url)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                active = req; req.timeout = 25; req.SetRequestHeader("User-Agent", "OpenFusion-Pocket/" + Application.version);
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                await Wait(req.SendWebRequest());
                if (req.result != UnityWebRequest.Result.Success) throw new IOException("GitHub request failed (" + req.responseCode + "). Please try again later.");
                if (req.downloadedBytes > 2 * 1024 * 1024) throw new IOException("Update metadata is too large.");
                return req.downloadHandler.text;
            }
        }
        public async Task CheckAsync()
        {
            if (destroyed || Checking || Downloading) return;
            Checking = true; Status = "Checking GitHub releases…";
            try
            {
                string json = await Text(PocketRelease.LatestApi); var release = PocketRelease.Read(json);
                string manifest = await Text(release.ManifestUrl); release.ApplyManifest(manifest, json);
                if (destroyed) return;
                if (Latest == null || Latest.Sha256 != release.Sha256) { Ready = false; verifiedHash = null; }
                Latest = release;
                Status = Available ? "Update available: " + release.Title : "You are up to date.";
            }
            catch (Exception e) { if (!destroyed) Status = e.Message; }
            finally { active = null; Checking = false; }
        }
        public async Task DownloadAsync()
        {
            if (destroyed || Checking || Downloading || !Available) return;
            var release = Latest; string part = apkPath + ".part"; Downloading = true; Ready = false; cancelRequested = false;
            Status = "Downloading update…";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(apkPath));
                if (File.Exists(part)) File.Delete(part);
                using (var req = UnityWebRequest.Get(release.ApkUrl))
                {
                    active = req; req.timeout = 600;
                    req.downloadHandler = new DownloadHandlerFile(part) { removeFileOnAbort = true };
                    var operation = req.SendWebRequest();
                    while (!operation.isDone)
                    {
                        if (req.downloadedBytes > (ulong)release.Size) { req.Abort(); throw new IOException("Download exceeds the expected APK size."); }
                        await Task.Delay(100);
                    }
                    if (cancelRequested) throw new OperationCanceledException("Download cancelled.");
                    if (req.result != UnityWebRequest.Result.Success) throw new IOException("Download failed. Check your connection and retry.");
                }
                active = null; Status = "Verifying download…";
                if (!await Task.Run(() => release.Verify(part))) throw new IOException("Update verification failed. The APK will not be installed.");
                if (cancelRequested) throw new OperationCanceledException("Download cancelled.");
                if (destroyed) return;
                if (File.Exists(apkPath)) File.Delete(apkPath); File.Move(part, apkPath);
                verifiedHash = release.Sha256; Ready = true; Status = "Download verified. Tap Install update to continue.";
            }
            catch (Exception e) { if (!destroyed) Status = e.Message; }
            finally { active = null; Downloading = false; if (File.Exists(part)) File.Delete(part); }
        }
        public void CancelDownload() { if (Downloading) { cancelRequested = true; active?.Abort(); } }
        public void Install()
        {
            if (!Ready || Latest == null || verifiedHash != Latest.Sha256 || !File.Exists(apkPath)) return;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var helper = new AndroidJavaClass("com.openfusion.updater.PocketInstaller"))
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    Status = helper.CallStatic<string>("install", activity, apkPath, verifiedHash, Latest.VersionCode);
#else
                Status = "Installation is available on Android. Open GitHub to download for another device.";
#endif
            }
            catch (Exception e) { Status = "Could not open the installer: " + e.Message; }
        }
        void OnDestroy() { destroyed = true; active?.Abort(); }
    }
}
