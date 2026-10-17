// Diagnóstico de "rayos de luz"/brillos que no existen en el cliente original.
//  F9  : lista (en consola y en Logs/rayprobe.log) los renderers cuyo volumen atraviesa el rayo bajo el cursor: nombre, shader, blend, cola, tamaño.
//  F10 : alterna ocultar todos los renderers con mezcla aditiva (Dst=One) que no sean partículas del jugador: si el brillo desaparece, ese es el origen.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class RayProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (FindObjectOfType<RayProbe>() != null) return; var go = new GameObject("RayProbe"); go.hideFlags = HideFlags.HideAndDontSave; DontDestroyOnLoad(go); go.AddComponent<RayProbe>(); }

        readonly List<Renderer> hidden = new List<Renderer>();
        static string PathOf(Transform t) { var sb = new StringBuilder(t.name); for (int i = 0; i < 4 && t.parent != null; i++) { t = t.parent; sb.Insert(0, t.name + "/"); } return sb.ToString(); }
        static bool Additive(Material m) { return m != null && m.HasProperty("_DstBlend") && Mathf.Approximately(m.GetFloat("_DstBlend"), 1f); }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) Probe();
            if (Input.GetKeyDown(KeyCode.F10)) ToggleAdditive();
        }

        void Probe()
        {
            var cam = Camera.main; if (cam == null) return; var ray = cam.ScreenPointToRay(Input.mousePosition); var hits = new List<KeyValuePair<float, string>>();
            foreach (var r in FindObjectsOfType<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue; var b = r.bounds; if (!b.IntersectRay(ray, out float d)) continue;
                var sb = new StringBuilder(); sb.Append(PathOf(r.transform)).Append(" [").Append(r.GetType().Name).Append("] tam=").Append(b.size.ToString("F1")).Append(" d=").Append(d.ToString("F1"));
                foreach (var m in r.sharedMaterials) { if (m == null) continue; sb.Append(" | ").Append(m.name).Append(" ").Append(m.shader != null ? m.shader.name : "?").Append(" q=").Append(m.renderQueue);
                    if (m.HasProperty("_SrcBlend")) sb.Append(" blend=").Append(m.GetFloat("_SrcBlend")).Append("/").Append(m.GetFloat("_DstBlend")); if (m.HasProperty("_ZWrite")) sb.Append(" zw=").Append(m.GetFloat("_ZWrite")); }
                hits.Add(new KeyValuePair<float, string>(d, sb.ToString()));
            }
            hits.Sort((a, c) => a.Key.CompareTo(c.Key)); var o = new StringBuilder("[rayprobe] " + hits.Count + " renderers bajo el cursor (más cercanos primero):\n");
            for (int i = 0; i < hits.Count && i < 30; i++) o.Append("  ").Append(hits[i].Value).Append('\n');
            Debug.Log(o.ToString());
            try { File.AppendAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "rayprobe.log")), System.DateTime.Now.ToString("HH:mm:ss") + " " + o + "\n"); } catch { }
        }

        void ToggleAdditive()
        {
            if (hidden.Count > 0) { foreach (var r in hidden) if (r != null) r.enabled = true; Debug.Log("[rayprobe] aditivos visibles de nuevo (" + hidden.Count + ")"); hidden.Clear(); return; }
            foreach (var r in FindObjectsOfType<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue; bool add = false; foreach (var m in r.sharedMaterials) if (Additive(m)) { add = true; break; }
                if (add && (r.transform.root.GetComponentInChildren<NpcInfo>() == null)) { r.enabled = false; hidden.Add(r); }
            }
            Debug.Log("[rayprobe] renderers aditivos ocultos: " + hidden.Count + " (F10 otra vez para restaurar)");
        }
    }
}
