using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenFusion.World
{
    // Ported from GameEventManager/HalloweenEvent in Retrobution 3.0.3 main.unity3d.
    // The endpoint, never the device calendar, determines whether Halloween is active.
    public sealed class SeasonalEvents : MonoBehaviour
    {
        public static SeasonalEvents Instance { get; private set; }
        public bool Halloween { get; private set; }
        public bool EventSound { get; private set; }
        readonly HashSet<SeasonalNpcCosmetic> npcs = new HashSet<SeasonalNpcCosmetic>();
        string[] endpoints = Array.Empty<string>(); int generation; bool fetching; float nextPoll;
        public static readonly Color HalloweenRim = new Color(104f / 255f * .45f, 189f / 255f * .45f, 62f / 255f * .45f, 1f);

        public static string[] Endpoints(string api, string main)
        {
            var urls = new List<string>();
            foreach (string source in new[] { (api ?? "").TrimEnd('/') + "/", main })
                if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http"))
                { string url = new Uri(uri, "themedEvent.php").AbsoluteUri; if (!urls.Contains(url)) urls.Add(url); }
            return urls.ToArray();
        }
        public static bool TryParse(string value, out bool halloween, out bool sound)
        {
            halloween = sound = false;
            if (value == null || value.Length > 128) return false;
            var parts = value.Trim().TrimStart('\uFEFF').Trim().ToLowerInvariant().Split(',').Select(p => p.Trim()).ToArray();
            if (parts[0] != "halloween" && parts[0] != "none" && parts[0] != "" && parts[0] != "knishmas") return false;
            halloween = parts[0] == "halloween"; sound = halloween && parts.Contains("eventsound"); return true;
        }
        public async Task ConfigureAsync(string api, string main)
        {
            Instance = this; ++generation; fetching = false; Apply(false, false);
            endpoints = Endpoints(api, main); await RefreshAsync();
        }
        async Task RefreshAsync()
        {
            if (fetching) return;
            int token = generation; fetching = true; nextPoll = Time.unscaledTime + 60f;
            try
            {
                foreach (string endpoint in endpoints)
                {
                    using (var request = UnityWebRequest.Get(endpoint))
                    {
                        request.timeout = 4;
                        var done = new TaskCompletionSource<bool>(); var op = request.SendWebRequest();
                        if (!op.isDone) { op.completed += _ => done.TrySetResult(true); await done.Task; }
                        if (this == null || token != generation) return;
                        if (request.result != UnityWebRequest.Result.Success || !TryParse(request.downloadHandler.text, out bool active, out bool sound)) continue;
                        Apply(active, sound); return;
                    }
                }
                // A temporary outage preserves the last confirmed event for this session.
            }
            catch (Exception) { /* Optional seasonal metadata must never block game entry. */ }
            finally { if (this != null && token == generation) fetching = false; }
        }
        public void Apply(bool active, bool sound)
        {
            sound &= active;
            if (Halloween == active && EventSound == sound) return;
            Halloween = active; EventSound = sound;
            foreach (var npc in npcs.ToArray()) if (npc != null) npc.Refresh(); else npcs.Remove(npc);
            MusicController.Instance?.ResetAmbient();
            Debug.Log("[seasonal] Halloween=" + active + ", event sound=" + sound);
        }
        public void TrackNpc(GameObject model, ModelRef data, ModelFactory factory, int id)
        {
            if (model == null || data == null || !SeasonalNpcCosmetic.Supports(data.Model)) return;
            var cosmetic = model.GetComponent<SeasonalNpcCosmetic>() ?? model.AddComponent<SeasonalNpcCosmetic>();
            npcs.Add(cosmetic); cosmetic.Init(this, factory, data.Model, id);
        }
        internal void Forget(SeasonalNpcCosmetic cosmetic) { npcs.Remove(cosmetic); }
        void Update()
        {
            if (!fetching && Time.unscaledTime >= nextPoll && GameClient.FindSelf != null && GameClient.FindSelf.WorldConnected) _ = RefreshAsync();
        }
        void OnDestroy()
        {
            ++generation;
            foreach (var npc in npcs.ToArray()) if (npc != null) npc.Clear();
            npcs.Clear(); if (Instance == this) Instance = null;
        }
        public static float Ambience(ref Color fog, ref Color sky, ref Color light, ref Color rim, float depth, bool paid)
        {
            if (Instance == null || !Instance.Halloween) return depth * .005f;
            fog = new Color((.01f + 120f * .00035f) * .8f, .01f * .8f, (.01f + 255f * .00035f) * .8f, .95f);
            sky.a = 0f; light = new Color(.5f, .5f, .6f, 1f); rim = HalloweenRim;
            return depth * (paid ? .007f : .0165f);
        }
        public static string Sound(string original, bool instance, bool music)
        {
            if (Instance == null || !Instance.EventSound || instance || string.IsNullOrEmpty(original) || original == "none.ogg") return original;
            string lower = original.ToLowerInvariant();
            if ((music && lower.Contains("main theme")) || lower.Contains(music ? "icespire.ogg" : "ambient icespire.ogg")) return original;
            return music ? "Halloween.ogg" : "Ambient Halloween.ogg";
        }
        public static int Delay(int original) => Instance != null && Instance.EventSound ? Math.Min(120, original) : original;
    }
}
