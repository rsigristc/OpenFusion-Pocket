// LegacyEffectKeys.cs -- réplica de EffectEmitterController.Update (clase propia en su archivo para que Unity pueda serializarla al recargar el dominio).
using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    /// <summary>Réplica de EffectEmitterController.Update: recorre las claves (tiempo normalizado, posición local, emitir sí/no) de cada sistema de partículas del efecto.</summary>
    public sealed class LegacyEffectKeys : MonoBehaviour
    {
        public sealed class Key { public float Time; public Vector3 Translate; public int GenType; }
        public sealed class Entry { public ParticleSystem Ps; public Transform T; public Key[] Keys; public bool LastEmit = true; }
        public readonly List<Entry> Entries = new List<Entry>();
        public float MaxTimer = -1f;
        float timer;

        static void SetEmit(Entry e, bool on) { if (e.LastEmit == on) return; e.LastEmit = on; var em = e.Ps.emission; em.enabled = on; }

        void Update()
        {
            timer += Time.deltaTime;
            if (MaxTimer > 0f && timer > MaxTimer) { foreach (var e in Entries) if (e.Ps != null) SetEmit(e, false); enabled = false; return; }
            float num = MaxTimer < 0f ? -MaxTimer : MaxTimer; if (num <= 0.0001f) return;
            if (timer > num) timer -= num;
            foreach (var e in Entries)
            {
                if (e.Ps == null || e.Keys == null || e.Keys.Length == 0) continue;
                int idx = 0; var cur = e.Keys[0];
                for (int k = 0; k < e.Keys.Length - 1; k++) if (e.Keys[k].Time * num < timer) { cur = e.Keys[k + 1]; idx = k + 1; }
                if (idx != 0)
                {
                    var prev = e.Keys[idx - 1]; float span = cur.Time - prev.Time; float f = span > 1e-5f ? (timer / num - prev.Time) / span : 0f;
                    SetEmit(e, prev.GenType != 0); e.T.localPosition = prev.Translate + (cur.Translate - prev.Translate) * f;
                }
                else { e.T.localPosition = cur.Translate; SetEmit(e, cur.GenType != 0); }
            }
        }
    }
}
