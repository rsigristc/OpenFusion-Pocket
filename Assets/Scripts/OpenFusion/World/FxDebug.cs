// FxDebug.cs -- F8: lista los ParticleSystem activos a menos de 25 m del jugador (nombre, modo, material, textura, partículas, tamaño) para identificar efectos raros.
using System.Text;
using UnityEngine;

namespace OpenFusion.World
{
    public static class FxDebug
    {
        public static void Dump(Vector3 pos)
        {
            var sb = new StringBuilder("[fxdebug] cerca de " + pos + "\n"); int n = 0;
            foreach (var ps in Object.FindObjectsOfType<ParticleSystem>())
            {
                if (!ps.gameObject.activeInHierarchy) continue; float d = Vector3.Distance(ps.transform.position, pos); if (d > 25f) continue;
                var r = ps.GetComponent<ParticleSystemRenderer>(); var m = r != null ? r.sharedMaterial : null; var tex = m != null && m.HasProperty("_MainTex") && m.mainTexture != null ? m.mainTexture.name : "-";
                var path = ps.transform.name; var t = ps.transform.parent; for (int i = 0; i < 3 && t != null; i++) { path = t.name + "/" + path; t = t.parent; }
                Debug.Log(string.Format("[fxdebug] " + "{0}  d={1:0.0}  modo={2}  mat={3} tex={4}  n={5} emite={6} play={7}  startSize={8} maxSize={9:0.0}  pos={10}", path, d, r != null ? r.renderMode.ToString() : "?", m != null ? m.name : "NULL", tex, ps.particleCount, ps.emission.enabled, ps.isPlaying, ps.main.startSize.constant, r != null ? r.maxParticleSize : 0f, ps.transform.position));
                if (++n >= 40) break;
            }
            Debug.Log(sb.ToString());
            // modelos de efecto (nifObject) cercanos: x_com_* = respawn
            int k = 0; var cam = Camera.main;
            foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
            {
                var m = r.sharedMaterial; if (m == null || !m.name.StartsWith("x_com_", System.StringComparison.OrdinalIgnoreCase)) continue;
                float d = Vector3.Distance(r.bounds.center, pos); if (d > 40f) continue;
                var mf = r.GetComponent<MeshFilter>(); string mi = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + " v=" + mf.sharedMesh.vertexCount + " b=" + mf.sharedMesh.bounds.size : "sin malla";
                Debug.Log(string.Format("[fxdebug] MR {0}/{1} d={2:0.0} en={3} act={4} vis={5} capa={6} cola={7} src={8} dst={9} cull={10} zw={11} col={12} centro={13} tam={14} esc={15} malla={16}",
                    r.transform.parent != null ? r.transform.parent.name : "", r.name, d, r.enabled, r.gameObject.activeInHierarchy, r.isVisible, r.gameObject.layer, m.renderQueue,
                    m.HasProperty("_SrcBlend") ? m.GetFloat("_SrcBlend") : -1f, m.HasProperty("_DstBlend") ? m.GetFloat("_DstBlend") : -1f, m.HasProperty("_Cull") ? m.GetFloat("_Cull") : -1f, m.HasProperty("_ZWrite") ? m.GetFloat("_ZWrite") : -1f,
                    m.HasProperty("_Color") ? m.GetColor("_Color").ToString() : "-", r.bounds.center, r.bounds.size, r.transform.lossyScale, mi));
                if (++k >= 20) break;
            }
            if (cam != null) Debug.Log("[fxdebug] cámara cullingMask=" + cam.cullingMask + " pos=" + cam.transform.position);
        }
    }
}
