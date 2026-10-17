using System.Collections;
using System.IO;
using UnityEngine;

namespace OpenFusion.World
{
    /// Diagnóstico visual: renderiza con una cámara temporal primeros planos del jugador y de un NPC y los guarda como PNG en Logs/.
    public static class Shots
    {
        public static IEnumerator Run(Transform player)
        {
            yield return new WaitForSeconds(1f);
            Shot("principal", Camera.main, null);
            if (player != null) Shot("jugador", null, player.gameObject);
            var npc = GameObject.Find("NPC#1032 t3111"); if (npc != null) Shot("npc3111", null, npc);
            var n2 = GameObject.Find("NPC#1035 t786"); if (n2 != null) Shot("npc786", null, n2);
            try { Probe(player); } catch (System.Exception e) { Debug.Log("[probe] falló: " + e.Message); }
            Debug.Log("[shots] guardadas en Logs/shot_*.png");
        }

        /// Diagnóstico de materiales cercanos: árboles (wd_etc_tree), plantas, agua. Escribe [probe] en el log con malla, submallas, shader y textura de cada material.
        static void Probe(Transform player)
        {
            if (player == null) return; var keys = new[] { "tree", "plant", "water", "poison", "grass" }; var count = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var r in Object.FindObjectsOfType<Renderer>())
            {
                if (r.bounds.SqrDistance(player.position) > 200f * 200f) continue;
                var mats = r.sharedMaterials; string hit = null;
                foreach (var m in mats) { if (m == null) continue; var ln = m.name.ToLowerInvariant(); foreach (var k in keys) if (ln.Contains(k)) { hit = k; break; } if (hit != null) break; }
                if (hit == null) continue; count.TryGetValue(hit, out var c); if (c >= 6) continue; count[hit] = c + 1;
                var sb = new System.Text.StringBuilder("[probe] " + hit + " '" + r.name + "' " + r.GetType().Name + " en " + r.bounds.center + " tam=" + r.bounds.size + " on=" + r.enabled + " visible=" + r.isVisible + " capa=" + r.gameObject.layer);
                Mesh mesh = null; var smr = r as SkinnedMeshRenderer; if (smr != null) mesh = smr.sharedMesh; else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                if (mesh != null) { sb.Append(" malla=" + mesh.name + " v=" + mesh.vertexCount + " sub=" + mesh.subMeshCount); for (int i = 0; i < mesh.subMeshCount; i++) sb.Append(" [s" + i + " idx=" + mesh.GetIndexCount(i) + "]"); } else sb.Append(" SIN MALLA");
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (m == null) { sb.Append("\n     mat" + i + " NULL"); continue; }
                    var t = (m.HasProperty("_MainTex") ? m.mainTexture : null) as Texture2D; string ts = t == null ? "SIN TEX" : t.name + " " + t.width + "x" + t.height + " " + t.format + " wrap=" + t.wrapMode;
                    sb.Append("\n     mat" + i + " " + m.name + " | " + (m.shader != null ? m.shader.name : "-") + " | q=" + m.renderQueue + " | " + ts);
                    foreach (var p in new[] { "_AlphaTestMode", "_Cutoff", "_ZWrite", "_Cull", "_SrcBlend", "_DstBlend", "_WaveScale" }) if (m.HasProperty(p)) sb.Append(" " + p + "=" + m.GetFloat(p));
                    foreach (var p in new[] { "_Color", "_HorizonColor", "_RefrColor" }) if (m.HasProperty(p)) sb.Append(" " + p + "=" + m.GetColor(p));
                    foreach (var p in new[] { "_BumpMap", "_ReflectiveColor" }) if (m.HasProperty(p)) { var tx = m.GetTexture(p) as Texture2D; sb.Append(" " + p + "=" + (tx == null ? "NULL" : tx.width + "x" + tx.height + " " + tx.format + " wrap=" + tx.wrapMode + " " + tx.name)); }
                }
                Debug.Log(sb.ToString());
            }
        }

        static string Kids(Transform t) { var sb = new System.Text.StringBuilder(" ["); for (int i = 0; i < t.childCount && i < 6; i++) { var c = t.GetChild(i); sb.Append(c.name + (c.gameObject.activeSelf ? "" : "(inactivo)") + " esc=" + c.localScale + " kids=" + c.childCount + "; "); } return sb.Append("]").ToString(); }

        static void Shot(string name, Camera main, GameObject target)
        {
            try
            {
                var go = new GameObject("ShotCam"); var cam = go.AddComponent<Camera>();
                var src = Camera.main; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.7f, 0.9f); cam.nearClipPlane = 0.05f; cam.farClipPlane = 800f; cam.fieldOfView = 40f; cam.cullingMask = -1;
                string info = "";
                if (main != null) { go.transform.position = main.transform.position; go.transform.rotation = main.transform.rotation; cam.fieldOfView = main.fieldOfView; info = " cámara principal en " + main.transform.position; }
                else
                {
                    var rs = target.GetComponentsInChildren<Renderer>(true); int act = 0, ena = 0; foreach (var r0 in rs) { if (r0.gameObject.activeInHierarchy) act++; if (r0.enabled) ena++; } Bounds b = new Bounds(target.transform.position + Vector3.up, Vector3.one * 0.5f); bool first = true;
                    foreach (var r in rs) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
                    float d = Mathf.Max(3f, b.size.magnitude * 1.6f); var c = b.center; go.transform.position = c + new Vector3(0.3f, 0.2f, -1f).normalized * d; go.transform.LookAt(c);
                    info = " objetivo " + target.name + " centro=" + c + " tamaño=" + b.size + " renderers=" + rs.Length + " activos=" + act + " enabled=" + ena + " raíz.activa=" + target.activeInHierarchy + " hijos=" + target.transform.childCount + Kids(target.transform);
                }
                var rt = new RenderTexture(1024, 640, 24); cam.targetTexture = rt; cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(1024, 640, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1024, 640), 0, 0); tex.Apply(); RenderTexture.active = prev;
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/shot_" + name + ".png", tex.EncodeToPNG());
                Object.Destroy(tex); cam.targetTexture = null; rt.Release(); Object.Destroy(go);
                Debug.Log("[shots] " + name + info);
            }
            catch (System.Exception e) { Debug.Log("[shots] " + name + " falló: " + e.Message); }
        }
    }
}
