using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using OpenFusion.Api;
using OpenFusion.World;
using OpenFusion.Screens;

namespace OpenFusion.EditorTools
{
    public static class StartupRegressionChecks
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static Task Wait(AsyncOperation operation)
        {
            if (operation.isDone) return Task.CompletedTask;
            var completion = new TaskCompletionSource<bool>(); operation.completed += _ => completion.TrySetResult(true); return completion.Task;
        }
        static async Task<string> Text(string url)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 20; await Wait(req.SendWebRequest());
                Require(req.result == UnityWebRequest.Result.Success, url + ": " + req.error);
                return req.downloadHandler.text;
            }
        }
        public static async Task Run()
        {
            var results = new List<string>(); GameObject host = null;
            try
            {
                host = new GameObject("MusicResetRegression");
                var controller = MusicController.Boot(host, null);
                // Edit-mode AddComponent does not invoke this MonoBehaviour's Awake.
                typeof(MusicController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                foreach (string field in new[] { "musicRunning", "ambRunning" })
                    typeof(MusicController).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, true);
                controller.ResetAmbient();
                foreach (string field in new[] { "musicRunning", "ambRunning" })
                    Require(!(bool)typeof(MusicController).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), "Reset must release pending music/ambient transitions.");
                UnityEngine.Object.DestroyImmediate(host); host = null;
                results.Add("PASS: resetting audio releases both pending transitions and destroys static controller state.");
                var urls = WelcomeNews.Endpoints("https://api.example.test/academy", "http://cdn.example.test/version/main.unity3d");
                Require(urls[0] == "https://api.example.test/academy/news.php" && urls[1] == "http://cdn.example.test/version/news.php", "API must precede CDN; subpaths must be preserved.");
                var images = WelcomeNews.Images(urls[0], "\uFEFF/upsell/one.png\r\n\n# comment\nsecond.png\nfile:///local.png\n<html>error</html>\n/upsell/one.png");
                Require(images.SequenceEqual(new[] { "https://api.example.test/upsell/one.png", "https://api.example.test/academy/second.png" }), "News image resolution, duplicate/scheme/HTML filtering failed.");
                results.Add("PASS: API-first news URLs; relative images resolved against news URL; invalid schemes and HTML ignored.");

                var names = new[] { "beta-20100104", "beta-20111013" };
                var endpoints = new[] { "https://api.dexlabs.systems/", "https://api.dexlabs.systems/academy/" };
                var resolveName = typeof(LauncherScreen).GetMethod("VersionName", BindingFlags.Static | BindingFlags.NonPublic);
                for (int i = 0; i < endpoints.Length; i++)
                {
                    var info = JsonConvert.DeserializeObject<ServerInfo>(await Text(endpoints[i]));
                    Require(info.GameVersions.Count > 0, "Public API must advertise a version.");
                    // Exercise the actual launcher resolver without a usable /versions endpoint.
                    var name = await (Task<string>)resolveName.Invoke(null, new object[] { "https://unused.invalid", info.GameVersions[0] });
                    Require(name == names[i], "Incorrect public server version: " + name);
                    results.Add("PASS: " + info.ServerName + " -> " + name + " (live API UUID, bundled catalog).");
                }

                host = new GameObject("WelcomeRegression"); var popup = host.AddComponent<WelcomePopup>();
                popup.ServerUrl = "https://api.ffretrobution.net";
                popup.MainFileUrl = "http://cdn.ffretrobution.net/retrobution-20261014-2/main.unity3d";
                var expectedPages = new List<string>();
                foreach (var newsUrl in WelcomeNews.Endpoints(popup.ServerUrl, popup.MainFileUrl))
                {
                    try { expectedPages = WelcomeNews.Images(newsUrl, await Text(newsUrl)); }
                    catch (Exception) { continue; }
                    if (expectedPages.Count > 0) break;
                }
                Require(expectedPages.Count > 0, "Live Retrobution news must advertise at least one image.");
                var routine = (IEnumerator)typeof(WelcomePopup).GetMethod("Fetch", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(popup, null);
                try { while (routine.MoveNext()) { if (routine.Current is AsyncOperation op) await Wait(op); else throw new Exception("Unexpected welcome coroutine yield."); } }
                finally { (routine as IDisposable)?.Dispose(); }
                var pages = (List<Texture2D>)typeof(WelcomePopup).GetField("pages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(popup);
                Require(WelcomePopup.Active && pages.Count == expectedPages.Count && pages.All(p => p.width > 0 && p.height > 0), "Every currently advertised Retrobution news page must be downloaded and decoded.");
                Require(GameMenu.HudHidden, "Welcome must hide all game HUD layers.");
                results.Add("PASS: live welcome coroutine fetched and decoded all " + expectedPages.Count + " currently advertised Retrobution pages, popup opened.");
                typeof(WelcomePopup).GetMethod("Exit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(popup, null);
                Require(!WelcomePopup.Active && pages.Count == 0, "Closing welcome must release textures and modal state.");
                results.Add("PASS: closing welcome releases page textures and gameplay lock.");
            }
            finally
            {
                if (host != null)
                {
                    var popup = host.GetComponent<WelcomePopup>();
                    if (popup != null) typeof(WelcomePopup).GetMethod("Exit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(popup, null);
                    UnityEngine.Object.DestroyImmediate(host);
                }
                Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/startup-regression-results.txt", results);
            }
            Debug.Log("OpenFusion: regresiones de inicio OK\n" + string.Join("\n", results));
        }
    }
}
