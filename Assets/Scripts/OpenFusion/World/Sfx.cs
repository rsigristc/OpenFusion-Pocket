// Sonido del juego (equivalente a SoundUtil + carga de AudioClip del original). Los AudioClip de los bundles 2.5 llevan los bytes OGG/MP3/WAV tal cual:
// se localizan con ModelFactory.Locate(ruta), se extraen del objeto crudo, se vuelcan a un archivo temporal y se decodifican con UnityWebRequestMultimedia.
// Reglas de SoundUtil: Playsound(nombre) = "ui sound/"+nombre+".wav" x volumen SFX x 0,7; PlaySoundAtPoint con IsPlayable (jugador < 16 m o cámara < 40/30 m,
// máx. 20 a la vez), pitch aleatorio 1±offset y rolloffFactor 0,1.
// SIN COMPILAR EN UNITY.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class Sfx : MonoBehaviour
    {
        public static Sfx Instance;
        public static float SfxVolume = 0.5f, MusicVolume = 0.5f, AmbientVolume = 0.5f, VoiceVolume = 0.5f;   // cnSoundOption por defecto (maestro 0,5 en AudioListener)
        public Transform Listener;   // ListenPlayer: el avatar propio

        ModelFactory models;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, List<Action<AudioClip>>> waiting = new Dictionary<string, List<Action<AudioClip>>>();
        readonly Dictionary<string, float> missingUntil = new Dictionary<string, float>();
        int active, logMiss, logOk; string dir; AnimationCurve rolloff;

        public static void Boot(GameObject host, ModelFactory m, Transform listener)
        {
            if (Instance == null) { var ex = host.GetComponent<Sfx>(); Instance = ex != null ? ex : host.AddComponent<Sfx>(); }
            Instance.models = m; if (listener != null) Instance.Listener = listener;
            { var go0 = GameOptions.Cur; AudioListener.volume = go0.MasterOn ? go0.Master : 0f; }   // respeta las opciones guardadas
            Debug.Log("[sonido] Sfx.Boot: ModelFactory " + (m != null ? "ok" : "NULL") + ", listener volumen " + AudioListener.volume.ToString("0.00") + ", dispositivo de audio: " + AudioSettings.outputSampleRate + " Hz, " + AudioSettings.driverCapabilities);
            try { Instance.dir = Path.Combine(Application.temporaryCachePath, "sfx"); Directory.CreateDirectory(Instance.dir); } catch { }
            var k = new List<Keyframe>(); for (int d = 0; d <= 40; d += 2) k.Add(new Keyframe(d, 1f / (1f + 0.1f * Mathf.Max(0, d - 1))));   // rolloffFactor 0,1 del original
            Instance.rolloff = new AnimationCurve(k.ToArray());
        }

        // ---------------------------------------------------------------- carga de clips

        /// Extrae los bytes de audio de un AudioClip 2.5 (name, format, length, frequency, size, is3D, dataLen, data) y su tipo.
        public static byte[] AudioBytes(byte[] file, uint off, uint size, bool big, out AudioType type)
        {
            type = AudioType.UNKNOWN;
            if (file == null || size < 4 || (ulong)off + size > (ulong)file.Length) return null;
            int o = (int)off, end = (int)(off + size);
            int Rd(int p) { var b = new byte[4]; Array.Copy(file, p, b, 0, 4); if (big) Array.Reverse(b); return BitConverter.ToInt32(b, 0); }
            int len = Rd(o); if (len < 0 || len > end - o - 4) return null;
            int p0 = o + 4 + len; p0 = (p0 + 3) & ~3;
            int start = -1;
            if (p0 + 24 <= end) { int dl = Rd(p0 + 20); if (dl > 4 && p0 + 24 + dl <= end + 3) start = p0 + 24; }
            if (start < 0 || !Known(file, start)) { start = -1; for (int i = p0; i < Math.Min(end - 4, p0 + 80); i++) if (Known(file, i)) { start = i; break; } }
            if (start < 0) return null;
            type = Kind(file, start); var res = new byte[end - start]; Array.Copy(file, start, res, 0, res.Length);
            return res;
        }
        static bool Known(byte[] f, int i) { return Kind(f, i) != AudioType.UNKNOWN; }
        static AudioType Kind(byte[] f, int i)
        {
            if (i + 4 > f.Length) return AudioType.UNKNOWN;
            if (f[i] == 'O' && f[i + 1] == 'g' && f[i + 2] == 'g' && f[i + 3] == 'S') return AudioType.OGGVORBIS;
            if (f[i] == 'R' && f[i + 1] == 'I' && f[i + 2] == 'F' && f[i + 3] == 'F') return AudioType.WAV;
            if (f[i] == 'I' && f[i + 1] == 'D' && f[i + 2] == '3') return AudioType.MPEG;
            if (f[i] == 0xFF && (f[i + 1] & 0xE0) == 0xE0) return AudioType.MPEG;
            return AudioType.UNKNOWN;
        }

        public static string Norm(string path)
        {
            if (string.IsNullOrEmpty(path)) return null; path = path.Replace('\\', '/');
            return path.ToLowerInvariant();
        }
        public bool HasClip(string path) { var r = models != null ? models.Locate(path) : default; return r.Ok && models.Store.TypeOf(r) == "AudioClip"; }

        /// Pide un clip por ruta del contenedor ("fu sound/x.wav", "bgm/y.ogg", "sound/z.wav"...). cb recibe null si no existe. `stream` = música larga.
        public void Load(string path, Action<AudioClip> cb, bool stream = false)
        {
            var key = Norm(path); if (key == null) { if (cb != null) cb(null); return; }
            AudioClip c; if (cache.TryGetValue(key, out c) && c != null) { if (cb != null) cb(c); return; }
            float until; if (missingUntil.TryGetValue(key, out until) && Time.time < until) { if (cb != null) cb(null); return; }
            List<Action<AudioClip>> l; if (waiting.TryGetValue(key, out l)) { if (cb != null) l.Add(cb); return; }
            l = new List<Action<AudioClip>>(); if (cb != null) l.Add(cb); waiting[key] = l;
            StartCoroutine(LoadRoutine(key, stream));
        }

        IEnumerator LoadRoutine(string key, bool stream)
        {
            AudioClip clip = null; string why = null;
            if (models == null) why = "sin ModelFactory";
            else
            {
                Ref r = models.Locate(key); if (!r.Ok) why = "no está en los bundles cargados";
                else if (models.Store.TypeOf(r) != "AudioClip") why = "no es AudioClip (" + models.Store.TypeOf(r) + ")";
                else
                {
                    AudioType t; byte[] data = null;
                    try { data = AudioBytes(r.File.Data, r.Info.Offset, r.Info.Size, r.File.BigEndian, out t); } catch (Exception e) { why = e.Message; t = AudioType.UNKNOWN; }
                    if (data == null) why = why ?? "sin firma de audio";
                    else
                    {
                        string ext = t == AudioType.OGGVORBIS ? ".ogg" : t == AudioType.WAV ? ".wav" : ".mp3";
                        string hash;
                        using (var sha = System.Security.Cryptography.SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                        string file = Path.Combine(dir ?? Application.temporaryCachePath, hash + ext);
                        try { if (!File.Exists(file)) File.WriteAllBytes(file, data); } catch (Exception e) { why = e.Message; }
                        if (why == null)
                        {
                            using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri, t))
                            {
                                req.timeout = 15;
                                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = stream;
                                yield return req.SendWebRequest();
                                if (req.result == UnityWebRequest.Result.Success) { clip = DownloadHandlerAudioClip.GetContent(req); if (clip != null) clip.name = key; }
                                else why = req.error;
                            }
                        }
                    }
                }
            }
            if (clip != null) { cache[key] = clip; if (logOk++ < 6) Debug.Log("[sonido] cargado " + key + " (" + clip.length.ToString("F1") + " s)"); }
            else { missingUntil[key] = Time.time + 20f; if (logMiss++ < 40) Debug.LogWarning("[sonido] " + key + ": " + why); }
            List<Action<AudioClip>> l; if (waiting.TryGetValue(key, out l)) { waiting.Remove(key); foreach (var cb in l) { try { cb(clip); } catch (Exception e) { Debug.LogWarning("[sonido] " + e.Message); } } }
        }

        // ---------------------------------------------------------------- reproducción

        /// SoundUtil.Playsound(nombre): "ui sound/"+nombre+".wav", 2D, volumen SFX x 0,7.
        public static void Ui(string name, float vol = 1f) { if (Instance != null) Instance.Play2D("ui sound/" + name + ".wav", vol * 0.7f * SfxVolume); }
        /// Botón de la interfaz: mouse_click01..05.
        public static void Button() { Ui("mouse_click0" + UnityEngine.Random.Range(1, 6)); }

        public void Play2D(string path, float vol = 1f, bool loop = false, Action<AudioSource> started = null)
        {
            Load(path, c =>
            {
                if (c == null || this == null) return;
                var go = new GameObject("sfx2d " + c.name); go.transform.SetParent(transform, false);
                var a = go.AddComponent<AudioSource>(); a.clip = c; a.volume = Mathf.Clamp01(vol); a.spatialBlend = 0f; a.loop = loop; a.Play(); if (play2dLog++ < 5) Debug.Log("[sonido] play2D " + c.name + " vol " + a.volume.ToString("0.00") + " reproduciendo=" + a.isPlaying + " listener=" + AudioListener.volume.ToString("0.00") + " pausa=" + AudioListener.pause);
                if (!loop) { active++; StartCoroutine(Kill(go, c.length + 0.2f)); }
                if (started != null) started(a);
            });
        }

        int play2dLog; IEnumerator Kill(GameObject go, float t) { yield return new WaitForSeconds(t); active = Mathf.Max(0, active - 1); if (go != null) Destroy(go); }

        /// SoundUtil.IsPlayable(pos).
        public bool IsPlayable(Vector3 pos)
        {
            if (active >= 20) return false;
            if (Listener != null && (Listener.position - pos).sqrMagnitude < 16f * 16f) return true;
            var cam = Camera.main; if (cam == null) return false; float lim = active < 10 ? 40f : 30f;
            return (cam.transform.position - pos).sqrMagnitude < lim * lim;
        }

        /// SoundUtil.PlaySoundAtPoint(ruta, pos, "SFXSound", loop, vol, pitchOffset). `parent` opcional para que siga a un objeto.
        public AudioSource At(string path, Vector3 pos, float vol = 1f, bool loop = false, float pitchOffset = 0f, Transform parent = null, bool voice = false)
        {
            if (string.IsNullOrEmpty(path) || !IsPlayable(pos)) return null;
            AudioSource res = null; var holder = new GameObject("sfx"); holder.transform.position = pos; if (parent != null) holder.transform.SetParent(parent, true);
            Load(path, c =>
            {
                if (c == null || holder == null) { if (holder != null) Destroy(holder); return; }
                holder.name = "sfx " + c.name;
                var a = holder.AddComponent<AudioSource>(); a.clip = c; a.volume = Mathf.Clamp01(vol * (voice ? VoiceVolume : SfxVolume)); a.loop = loop; a.spatialBlend = 1f;
                a.rolloffMode = AudioRolloffMode.Custom; a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff); a.maxDistance = 40f; a.dopplerLevel = 0f;
                a.pitch = pitchOffset > 0f ? UnityEngine.Random.Range(1f - pitchOffset, 1f + pitchOffset) : 1f; a.Play(); res = a;
                if (!loop) { active++; StartCoroutine(Kill(holder, c.length / Mathf.Max(0.1f, a.pitch) + 0.2f)); }
            });
            return res;
        }

        public static void PlayAt(string path, Vector3 pos, float vol = 1f, bool loop = false, float pitchOffset = 0f, Transform parent = null, bool voice = false)
        { if (Instance != null) Instance.At(path, pos, vol, loop, pitchOffset, parent, voice); }
    }
}
