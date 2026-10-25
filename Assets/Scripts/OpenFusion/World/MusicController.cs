// Música y ambiente de fondo (MusicController + MusicDataStorage del original). Datos: Resources/OpenFusion/Data/music.json (MusicDataStorage.asset de main.unity3d):
// {"m":[[index,archivo,retardo,instancia,[x,y,x,y...]]...],"a":[...]}. Cada 1 s se busca (WorldDataContainer) el primer polígono de música/ambiente que contenga (x,z) del jugador
// con la misma marca de instancia, respetando el retardo de repetición; "none.ogg" = silencio. Música: no se repite sola (loop=false), ambiente en bucle. Fundidos como FadeInNewAudio.
// SIN COMPILAR EN UNITY.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class MusicController : MonoBehaviour
    {
        sealed class Entry { public int Index, Delay; public string File; public bool Ins; public float[] Pts; }
        Entry[] music = new Entry[0], ambient = new Entry[0];
        float[] musicTimes, ambientTimes;
        AudioSource musicSrc, ambSrc; string musicName = "none", ambName = "none"; bool musicRunning, ambRunning; float next; int logN;
        public Transform Player; public Func<bool> InstanceMap;   // x,z del avatar propio y bInsMap
        public static string TrueOverride = "";
        public static bool TrueOverrideLoop;
        public static bool LobbyMuted;
        bool lobby;
        public static MusicController Instance;

        public static MusicController Boot(GameObject host, Transform player, MainGameData data = null)
        {
            var mc = host.GetComponent<MusicController>(); if (mc == null) mc = host.AddComponent<MusicController>();
            if (data?.Music != null && data.Ambient != null) mc.UseMainData(data);
            mc.Player = player; if (player != null) { mc.lobby = false; LobbyMuted = false; } Instance = mc; return mc;
        }
        void UseMainData(MainGameData data)
        {
            Entry Convert(MainGameData.Region r) => new Entry { Index = r.Index, Delay = r.Delay, File = r.File, Ins = r.Ins, Pts = r.Pts };
            music = Array.ConvertAll(data.Music, Convert); ambient = Array.ConvertAll(data.Ambient, Convert);
            musicTimes = new float[Mathf.Max(1, MaxIdx(music) + 1)]; ambientTimes = new float[Mathf.Max(1, MaxIdx(ambient) + 1)];
            ResetAmbient(); Debug.Log("[musica] main.unity3d regions: " + music.Length + " music, " + ambient.Length + " ambient");
        }

        void Awake()
        {
            Load();
            var g = new GameObject("MusicSource"); g.transform.SetParent(transform, false); musicSrc = g.AddComponent<AudioSource>(); musicSrc.loop = false; musicSrc.spatialBlend = 0f;
            var g2 = new GameObject("AmbientSource"); g2.transform.SetParent(transform, false); ambSrc = g2.AddComponent<AudioSource>(); ambSrc.loop = true; ambSrc.spatialBlend = 0f;
        }

        void Load()
        {
            try
            {
                var ta = Resources.Load<TextAsset>("OpenFusion/Data/music"); if (ta == null) { Debug.LogWarning("[musica] falta Resources/OpenFusion/Data/music.json"); return; }
                var root = MiniJson.Parse(ta.text) as Dictionary<string, object>;
                music = Conv(root["m"] as List<object>); ambient = Conv(root["a"] as List<object>);
                musicTimes = new float[Mathf.Max(1, MaxIdx(music) + 1)]; ambientTimes = new float[Mathf.Max(1, MaxIdx(ambient) + 1)];
                for (int i = 0; i < musicTimes.Length; i++) musicTimes[i] = float.NegativeInfinity;
                for (int i = 0; i < ambientTimes.Length; i++) ambientTimes[i] = float.NegativeInfinity;
                Debug.Log("[musica] " + music.Length + " pistas, " + ambient.Length + " ambientes");
            }
            catch (Exception e) { Debug.LogWarning("[musica] " + e.Message); }
        }
        static int MaxIdx(Entry[] a) { int m = 0; foreach (var e in a) m = Mathf.Max(m, e.Index); return m; }
        static Entry[] Conv(List<object> l)
        {
            var r = new Entry[l.Count];
            for (int i = 0; i < r.Length; i++)
            {
                var a = (List<object>)l[i]; var pts = (List<object>)a[4]; var f = new float[pts.Count];
                for (int k = 0; k < f.Length; k++) f[k] = (float)System.Convert.ToDouble(pts[k]);
                r[i] = new Entry { Index = System.Convert.ToInt32(a[0]), File = (string)a[1], Delay = System.Convert.ToInt32(a[2]), Ins = System.Convert.ToInt32(a[3]) != 0, Pts = f };
            }
            return r;
        }

        /// MusicDataStorage.SoundData.Contains: se lanza una recta horizontal por el punto y se cuentan los cruces con las aristas a cada lado.
        static bool Contains(Entry e, float px, float py, bool ins)
        {
            if (ins != e.Ins) return false; int left = 0, right = 0, n = e.Pts.Length / 2;
            for (int i = 0; i < n; i++)
            {
                float x = e.Pts[2 * i], y = e.Pts[2 * i + 1]; int j = i + 1 != n ? i + 1 : 0; float x2 = e.Pts[2 * j], y2 = e.Pts[2 * j + 1];
                if (y2 == y) continue;   // arista paralela a la recta horizontal
                float t = (py - y) / (y2 - y); if (t < 0f || t > 1f) continue;
                float ix = x + t * (x2 - x); if (ix < px) left++; else right++;
            }
            return left % 2 == 1 && right % 2 == 1;
        }

        public void ResetAmbient()
        {
            StopAllCoroutines(); musicRunning = ambRunning = false; next = 0f;
            if (musicTimes != null) for (int i = 0; i < musicTimes.Length; i++) musicTimes[i] = float.NegativeInfinity;
            if (ambientTimes != null) for (int i = 0; i < ambientTimes.Length; i++) ambientTimes[i] = float.NegativeInfinity;
            if (musicSrc.isPlaying) musicSrc.Stop(); if (ambSrc.isPlaying) ambSrc.Stop(); musicSrc.clip = null; ambSrc.clip = null; ambName = "none"; musicName = "none";
        }

        /// Música del lobby: punto (1,1).
        public void Lobby() { lobby = true; Search(1f, 1f, false); }
        void OnDestroy() { if (Instance == this) { Instance = null; TrueOverride = ""; TrueOverrideLoop = false; LobbyMuted = false; } }

        void Update()
        {
            if (!musicRunning && musicSrc != null) musicSrc.volume = lobby && LobbyMuted ? 0f : Sfx.MusicVolume;
            if (!ambRunning && ambSrc != null) ambSrc.volume = Sfx.AmbientVolume;
            if (Time.time < next) return; next = Time.time + 1f;
            if (lobby) { Search(1f, 1f, false); return; }
            if (Player == null) return;
            Search(Player.position.x, Player.position.z, RaceSystem.InsMap || (InstanceMap != null && InstanceMap()));
        }

        void Search(float px, float py, bool ins)
        {
            if (music.Length == 0 || Sfx.Instance == null) return;
            string text = ""; int num = -1;
            for (int i = 0; i < music.Length; i++)
            {
                var m = music[i];
                if (Contains(m, px, py, ins) && musicTimes[m.Index] + SeasonalEvents.Delay(m.Delay) < Time.time) { num = m.Index; text = lobby ? m.File : SeasonalEvents.Sound(m.File, m.Ins, true); break; }
                if (Contains(m, px, py, ins)) { text = ""; break; }
            }
            if (TrueOverride != "")
            {
                if (TrueOverride == "stop") { ResetAmbient(); TrueOverride = ""; }
                else { text = TrueOverride; num = -1; if (musicName != text) ResetAmbient(); else if (!TrueOverrideLoop) TrueOverride = ""; }
            }
            if (text.Length > 0 && (musicSrc.clip == null || !text.Contains(musicName) || !musicSrc.isPlaying) && !musicRunning)
            {
                musicName = text; StartCoroutine(Fade(musicSrc, text, num, true));
            }
            text = "";
            for (int j = 0; j < ambient.Length; j++)
            {
                var m = ambient[j];
                if (Contains(m, px, py, ins) && ambientTimes[m.Index] + SeasonalEvents.Delay(m.Delay) < Time.time) { num = m.Index; text = lobby ? m.File : SeasonalEvents.Sound(m.File, m.Ins, false); break; }
                if (Contains(m, px, py, ins)) { text = ""; break; }
            }
            if (text.Length > 0 && (ambSrc.clip == null || !text.Contains(ambName)) && !ambRunning)
            {
                if (text == musicName && text != "none.ogg") return;
                ambName = text; StartCoroutine(Fade(ambSrc, text, num, false));
            }
        }

        IEnumerator Fade(AudioSource src, string song, int index, bool isMusic)
        {
            float volMod = 1f; // Music and ambient fades run concurrently.
            if (isMusic) musicRunning = true; else ambRunning = true;
            float vol = isMusic ? (lobby && LobbyMuted ? 0f : Sfx.MusicVolume) : Sfx.AmbientVolume;
            if (song.Contains("none"))
            {
                if (src.clip != null && src.isPlaying) { float f = 1f; for (int i = 0; i < 10; i++) { f -= 0.1f; src.volume = vol * f; yield return new WaitForSeconds(0.05f); } src.Stop(); src.clip = null; }
            }
            else
            {
                AudioClip clip = null; bool done = false; Sfx.Instance.Load("bgm/" + song, c => { clip = c; done = true; }, true);
                float deadline = Time.realtimeSinceStartup + 20f;
                while (!done && Time.realtimeSinceStartup < deadline) yield return null;
                if (clip == null) { if (logN++ < 5) Debug.LogWarning("[musica] sin clip " + song); }
                else
                {
                    bool flag = true;   // el original sólo se salta el fundido cuando es el mismo ambiente
                    if (!isMusic && src.clip != null && song.Contains(src.clip.name)) flag = false;
                    if (flag) for (int j = 0; j < 10; j++) { volMod -= 0.1f; src.volume = vol * Mathf.Max(0f, volMod); yield return new WaitForSeconds(0.05f); }
                    src.volume = 0f; src.Stop(); src.clip = clip; src.loop = !isMusic || (song == TrueOverride && TrueOverrideLoop); src.Play();
                    var times = isMusic ? musicTimes : ambientTimes; var list = isMusic ? music : ambient;
                    if (index >= 0)
                    {
                        for (int k = 0; k < times.Length; k++) times[k] = float.NegativeInfinity;
                        times[index] = Time.time + clip.length;
                    }
                    if (flag) for (int n = 0; n < 10; n++) { volMod += 0.1f; src.volume = vol * Mathf.Min(1f, volMod); yield return new WaitForSeconds(0.1f); }
                    if (logN++ < 5) Debug.Log("[musica] " + (isMusic ? "música " : "ambiente ") + song);
                }
            }
            if (isMusic) musicRunning = false; else ambRunning = false;
            src.volume = isMusic ? (lobby && LobbyMuted ? 0f : Sfx.MusicVolume) : Sfx.AmbientVolume;
        }
    }
}
