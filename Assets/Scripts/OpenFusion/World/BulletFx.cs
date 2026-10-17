// Efectos de disparo/impacto del original (BulletContainer.AddBullet + BulletMoveScript). Tabla: Resources/OpenFusion/Data/bullets.json (bullettable.asset del bundle Effects, 170 filas):
// [particleScript, succScript, fireScript, succSound, bulletScale, succScale, maxTimer, hideTime, fireLink, succLink, fireScale]. Efectos = prefabs "Prefabs/Particle/EffectScripts/ES[n].prefab".
// Ciclo (BulletMoveScript): espera hideTime -> Start (posición del hueso fireLink del origen, efecto ES[particle] hijo de la bala + ES[fire] en el origen) -> Move (cada frame pos += (destino-pos) * dt/(maxTimer-t)) ->
// Crash a maxTimer: ES[succ] en el destino (hueso succLink o centro) con escala succScale*1.3.
// SIN COMPILAR EN UNITY.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class BulletFx : MonoBehaviour
    {
        public static BulletFx Instance;
        public sealed class Row { public int Part, Succ, Fire; public string Sound, FireLink, SuccLink; public float BulletScale, SuccScale, MaxTimer, Hide, FireScale; }
        Row[] rows; ModelFactory models; readonly HashSet<int> missing = new HashSet<int>(); int logs;
        public bool Enabled = true;

        public void Init(ModelFactory m)
        {
            Instance = this; models = m;
            if (m?.GameData?.Projectiles != null && m.GameData.Projectiles.Length > 0)
            {
                rows = Array.ConvertAll(m.GameData.Projectiles, p => new Row { Part = p.Part, Succ = p.Succ, Fire = p.Fire, Sound = p.Sound, FireLink = p.FireLink, SuccLink = p.SuccLink,
                    BulletScale = p.BulletScale, SuccScale = p.SuccScale, FireScale = p.FireScale, MaxTimer = p.MaxTimer, Hide = p.Hide });
                Debug.Log("[bala] main.unity3d projectile table: " + rows.Length + " rows"); return;
            }
            try
            {
                var reference = m != null ? m.Locate("bullettable.asset") : default;
                if (reference.Ok)
                {
                    rows = ReadRows(m.Store.Read(reference));
                    Debug.Log("[bala] downloaded bullettable.asset: " + rows.Length + " rows"); return;
                }
            }
            catch (Exception e) { Debug.LogWarning("[bala] downloaded table: " + e.Message); }
            try
            {
                var ta = Resources.Load<TextAsset>("OpenFusion/Data/bullets"); var root = ta != null ? MiniJson.Parse(ta.text) as Dictionary<string, object> : null;
                var arr = root != null && root.ContainsKey("b") ? root["b"] as List<object> : null; if (arr == null) { Debug.LogWarning("[bala] falta Resources/OpenFusion/Data/bullets.json"); return; }
                rows = new Row[arr.Count];
                for (int i = 0; i < arr.Count; i++)
                {
                    var a = arr[i] as List<object>; if (a == null || a.Count < 11) continue;
                    rows[i] = new Row { Part = I(a[0]), Succ = I(a[1]), Fire = I(a[2]), Sound = a[3] as string, BulletScale = F(a[4]), SuccScale = F(a[5]), MaxTimer = F(a[6]), Hide = F(a[7]), FireLink = a[8] as string, SuccLink = a[9] as string, FireScale = F(a[10]) };
                }
                Debug.Log("[bala] tabla cargada: " + rows.Length + " filas");
            }
            catch (Exception e) { Debug.LogWarning("[bala] " + e.Message); }
        }
        static int I(object o) { return Mathf.RoundToInt((float)System.Convert.ToDouble(o)); }
        static float F(object o) { return (float)System.Convert.ToDouble(o); }
        public static Row[] ReadRows(Dictionary<string, object> table)
        {
            var data = (object[])table["m_pBulletData"]; var result = new Row[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var d = (Dictionary<string, object>)data[i];
                result[i] = new Row { Part = I(d["m_iParticleScript"]), Succ = I(d["m_iSuccScript"]), Fire = I(d["m_iFireScript"]), Sound = d["m_strSuccSound"] as string,
                    BulletScale = F(d["m_fBulletModelScale"]), SuccScale = F(d["m_fSuccModelScale"]), FireScale = F(d["m_fFireModelScale"]), MaxTimer = F(d["m_fMaxTimer"]), Hide = F(d["m_fHideTime"]),
                    FireLink = d["m_strFireLink"] as string, SuccLink = d["m_strSuccLink"] as string };
            }
            return result;
        }
        readonly HashSet<int> unknownRows = new HashSet<int>();
        public Row RowOf(int type)
        {
            if (rows != null && type >= 0 && type < rows.Length) return rows[type];
            if (unknownRows.Add(type)) Debug.LogWarning("[bala] missing projectile row " + type + " in game data (rows=" + (rows != null ? rows.Length : 0) + ")");
            return null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++) { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; }
            return null;
        }

        /// MakeBullet: `src` origen (jugador), `target` destino (NPC) o, si es null, un punto fijo `fixedPos`.
        public void Fire(int type, Transform src, Transform target, float targetHeight, Vector3 fixedPos)
        {
            if (!Enabled || models == null) return; var r = RowOf(type); if (r == null || src == null) return;
            StartCoroutine(Run(r, src, target, targetHeight, fixedPos));
        }

        IEnumerator Run(Row r, Transform src, Transform dst, float h, Vector3 fixedPos)
        {
            float t0 = Time.time; while (Time.time - t0 < r.Hide) yield return null;
            if (src == null) yield break;
            var fb = FindDeep(src, r.FireLink); Vector3 pos = fb != null ? fb.position : src.position + Vector3.up * 0.8f;
            Transform tb = dst != null ? FindDeep(dst, r.SuccLink) : null;
            Func<Vector3> TP = () => tb != null ? tb.position : (dst != null ? dst.position + Vector3.up * h * 0.5f : fixedPos);
            var carrier = new GameObject("bala"); carrier.transform.position = pos;
            var d0 = TP() - pos; if (d0.sqrMagnitude > 1e-6f) carrier.transform.rotation = Quaternion.LookRotation(d0.normalized, Vector3.up);
            if (r.Part > 0 && r.BulletScale > 0f) Spawn(r.Part, pos, carrier.transform.rotation, r.BulletScale, carrier.transform, 6f);
            if (r.Fire >= 0 && r.FireScale > 0f) Spawn(r.Fire, fb != null ? fb.position : pos, fb != null ? fb.rotation : carrier.transform.rotation, r.FireScale, null, 3f);
            float st = Time.time;
            while (true)
            {
                float el = Time.time - st; if (el >= r.MaxTimer) break;
                float f = Mathf.Min(1f, Time.deltaTime / Mathf.Max(0.0001f, r.MaxTimer - el)); var tp = TP(); var dv = tp - carrier.transform.position;
                if (dv.magnitude > 0.001f) { carrier.transform.rotation = Quaternion.LookRotation(dv.normalized, Vector3.up); carrier.transform.position += dv * f; }
                yield return null;
            }
            var endPos = TP(); var endRot = carrier.transform.rotation; Destroy(carrier);
            if (r.SuccScale > 0f) Spawn(r.Succ, endPos, endRot, r.SuccScale * 1.3f, null, 3f);
            CrashSound(r, endPos);
        }

        /// BulletMoveScript/cnWarHead.CrashEffect: "sound/"+succSound+".wav" en el punto de impacto (se ignoran cadenas con "..." o comillas).
        public static void CrashSound(Row r, Vector3 pos)
        {
            if (r == null || string.IsNullOrEmpty(r.Sound) || r.Sound.IndexOf("...") != -1 || r.Sound.IndexOf('"') != -1) return;
            Sfx.PlayAt("sound/" + r.Sound + ".wav", pos);
        }

        /// EffectController.InstantiateEffect + SetParticleScale: ES[n] del bundle Effects, escala uniforme (partículas en modo jerarquía).
        public GameObject Spawn(int id, Vector3 pos, Quaternion rot, float scale, Transform parent, float life)
        {
            try
            {
                var rf = models.Locate("Prefabs/Particle/EffectScripts/ES[" + id + "].prefab");
                if (!rf.Ok) { if (missing.Add(id)) Debug.LogWarning("[bala] efecto ES[" + id + "] no disponible (bundle Effects sin cargar o id inexistente)"); return null; }
                var go = models.Builder.BuildPrefabAt(rf, null); if (go == null) { if (missing.Add(id)) Debug.LogWarning("[bala] ES[" + id + "] no se pudo construir"); return null; }
                WorldEntities.ActivateAll(go); go.SetActive(true); foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;   // los prefabs 2.5 llegan con TODOS los nodos inactivos (ver ShinyEggs)
                go.transform.position = pos; go.transform.rotation = rot; go.transform.localScale = Vector3.one * scale;
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { var m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy; }
                if (parent != null) go.transform.SetParent(parent, true);
                Destroy(go, life);
                if (logs++ < 14)
                {
                    var rds = go.GetComponentsInChildren<Renderer>(true); var sb = new System.Text.StringBuilder(); int act = 0; Bounds bb = default(Bounds); bool hb = false;
                    foreach (var rd in rds) { if (rd.enabled && rd.gameObject.activeInHierarchy) act++; if (!hb) { bb = rd.bounds; hb = true; } else bb.Encapsulate(rd.bounds); if (sb.Length < 200) sb.Append(rd.GetType().Name + ":" + (rd.sharedMaterial != null ? (rd.sharedMaterial.shader != null ? rd.sharedMaterial.shader.name : "sin shader") : "sin material") + " "); }
                    Debug.Log("[bala] ES[" + id + "] en " + pos + " escala=" + scale.ToString("F2") + " ps=" + go.GetComponentsInChildren<ParticleSystem>(true).Length + " renderers=" + rds.Length + " activos=" + act + " tamaño=" + (hb ? bb.size.ToString("F2") : "-") + " hijos=" + go.GetComponentsInChildren<Transform>(true).Length + " | " + sb);
                }
                return go;
            }
            catch (Exception e) { if (missing.Add(id)) Debug.LogWarning("[bala] ES[" + id + "]: " + e.Message); return null; }
        }
    }
}
