using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    // DongLoader.LoadEvSound in Legacy. Sources belong to a dong and are released with it.
    public sealed class EnvironmentalAudio : MonoBehaviour
    {
        string path; AudioSource source; bool loading; float next;
        static int playing;
        public static void Attach(GameObject dong, AssetStore store, IEnumerable<string> files, int x, int y)
        {
            if (dong == null) return;
            foreach (var file in files)
            {
                var sf = store.Get(file); if (sf == null) continue;
                var index = new AssetIndex(); index.Add(file, sf);
                foreach (var key in index.Keys)
                {
                    if (!key.StartsWith("evsound/", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!key.EndsWith($"sound_{x:00}_{y:00}.asset", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!index.TryGet(key, out var asset)) continue;
                    var reference = store.Resolve(file, asset.Target); if (!reference.Ok) continue;
                    var data = store.Read(reference);
                    if (!data.TryGetValue("EvSoundArray", out var rows)) continue;
                    foreach (var row in D.A(rows))
                    {
                        var entry = D.O(row); var position = D.V3(entry["Position"]); var name = entry["FileName"] as string;
                        if (string.IsNullOrEmpty(name)) continue;
                        var go = new GameObject("Environment " + name); go.transform.SetParent(dong.transform, false);
                        go.transform.position = new Vector3(position[0], position[1], position[2]);
                        go.AddComponent<EnvironmentalAudio>().path = "enviro sound/" + name;
                    }
                }
            }
        }
        void Update()
        {
            if (Time.unscaledTime < next || Sfx.Instance == null) return; next = Time.unscaledTime + 2f;
            var listener = Camera.main != null ? Camera.main.transform : Sfx.Instance.Listener; if (listener == null) return;
            float distance = Vector3.Distance(listener.position, transform.position);
            if (source != null)
            {
                source.volume = Sfx.AmbientVolume;
                if (distance > 40f && source.isPlaying) { source.Stop(); playing = Mathf.Max(0, playing - 1); }
                else if (distance < 30f && !source.isPlaying && playing < 18) { source.Play(); playing++; }
            }
            else if (!loading && distance < 30f)
            {
                loading = true;
                Sfx.Instance.Load(path, clip =>
                {
                    if (this == null) return; loading = false; if (clip == null) return;
                    source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = true;
                    source.clip = clip; source.spatialBlend = 1; source.dopplerLevel = 0; source.maxDistance = 40;
                    var keys = new List<Keyframe>(); for (int d = 0; d <= 40; d += 2) keys.Add(new Keyframe(d, 1f / (1f + .4f * Mathf.Max(0, d - 1))));
                    source.rolloffMode = AudioRolloffMode.Custom; source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(keys.ToArray()));
                });
            }
        }
        void OnDisable() { if (source != null && source.isPlaying) { source.Stop(); playing = Mathf.Max(0, playing - 1); } }
    }
}
