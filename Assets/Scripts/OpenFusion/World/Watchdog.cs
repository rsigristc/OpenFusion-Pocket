// Vigilante de congelamientos: un hilo comprueba que el hilo principal siga dibujando fotogramas. Si pasa >3 s sin fotograma escribe en Logs/freeze.log
// la última "miga" (Watchdog.At) ejecutada, para saber dónde se quedó Unity. Falsos positivos posibles si el editor está en pausa o sin foco.
using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace OpenFusion.World
{
    public static class Watchdog
    {
        static volatile string crumb = ""; static volatile bool active; static long crumbAt, beat; static string path; static bool started; static readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        public static void At(string c) { crumb = c; Interlocked.Exchange(ref crumbAt, sw.ElapsedMilliseconds); }
        sealed class Beat : MonoBehaviour { void OnEnable() { active = true; } void OnDestroy() { active = false; } void Update() { Interlocked.Exchange(ref beat, sw.ElapsedMilliseconds); } void LateUpdate() { Interlocked.Exchange(ref beat, sw.ElapsedMilliseconds); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("Watchdog"); go.hideFlags = HideFlags.HideAndDontSave; UnityEngine.Object.DontDestroyOnLoad(go); go.AddComponent<Beat>(); Interlocked.Exchange(ref beat, sw.ElapsedMilliseconds);   // se recrea en cada Play (sin recarga de dominio los estáticos persisten)
            if (started) return; started = true;
            try { path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "freeze.log")); Directory.CreateDirectory(Path.GetDirectoryName(path)); } catch { return; }
            var t = new Thread(Loop) { IsBackground = true, Name = "OpenFusionWatchdog" }; t.Start();
            Write("watchdog iniciado");
        }
        static void Loop()
        {
            bool stalled = false; long lastReport = 0;
            while (true)
            {
                Thread.Sleep(500); if (!active) { stalled = false; continue; } long now = sw.ElapsedMilliseconds, gap = now - Interlocked.Read(ref beat);
                if (gap > 3000 && (!stalled || now - lastReport > 10000)) { stalled = true; lastReport = now; Write("SIN FOTOGRAMA hace " + gap / 1000 + " s; última miga: '" + crumb + "' (hace " + (now - Interlocked.Read(ref crumbAt)) / 1000 + " s)"); }
                else if (stalled && gap < 1000) { stalled = false; Write("recuperado"); }
            }
        }
        static void Write(string m) { try { File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + " [freeze] " + m + "\n"); } catch { } }
    }
}
