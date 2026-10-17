// Registro estático de las superficies de agua (materiales ffWater / shader OpenFusion/Water) para detectar el nado.
// Original: el agua es un MeshCollider con tag "Water" sobre el que se apoya el CharacterController (cnAvatarThirdPersonMove.OnControllerColliderHit
// -> bWater = true). Aquí el colisionador de agua va en un hijo de la capa "Ignore Raycast" (2): no lo ven Ground.Y, ni Physics.RaycastAll por defecto, ni la
// cámara; sólo se consulta con Collider.Raycast. Hook: DongBuilder.BuildNodes llama a WaterSurface.TryRegister(go) al final de cada nodo. SIN COMPILAR EN UNITY.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    public static class WaterSurface
    {
        public const int WaterLayer = 2;   // Physics.IgnoreRaycastLayer = 1 << 2
        sealed class Entry { public Collider Col; public Renderer R; }
        static readonly List<Entry> entries = new List<Entry>();
        public static int Count { get { return entries.Count; } }

        public static bool IsWater(Material m)
        {
            if (m == null) return false;
            if (m.name.StartsWith("ffWater", StringComparison.OrdinalIgnoreCase)) return true;
            var sh = m.shader; return sh != null && sh.name == "OpenFusion/Water";
        }

        /// Si el nodo tiene un MeshRenderer con material de agua, le añade un MeshCollider (no convexo) en un hijo de la capa 2 y lo registra.
        /// El MeshCollider propio del nodo (ColliderMesh del original, tag Water) se sustituye: si no, Ground.Y trataría el agua como suelo.
        public static bool TryRegister(GameObject go)
        {
            var mr = go.GetComponent<MeshRenderer>(); var mf = go.GetComponent<MeshFilter>(); if (mr == null || mf == null || mf.sharedMesh == null) return false;
            bool water = false; foreach (var m in mr.sharedMaterials) if (IsWater(m)) { water = true; break; }
            if (!water) return false;
            var src = mf.sharedMesh;
            foreach (var old in go.GetComponents<MeshCollider>()) { if (old.sharedMesh != null) src = old.sharedMesh; UnityEngine.Object.Destroy(old); }
            if (!HasTriangles(src)) return false;
            var child = new GameObject("WaterCollider"); child.layer = WaterLayer; child.transform.SetParent(go.transform, false);
            var mc = child.AddComponent<MeshCollider>(); mc.sharedMesh = src;   // convex = false (por defecto)
            entries.Add(new Entry { Col = mc, R = mr });   // los límites se piden en cada consulta: la raíz del dong se coloca DESPUÉS de construirlo y los de ahora serían obsoletos
            Debug.Log("[water] superficie registrada: " + go.name + " (total " + entries.Count + ")");
            return true;
        }

        static bool HasTriangles(Mesh m) { for (int i = 0; i < m.subMeshCount; i++) if (m.GetIndexCount(i) >= 3) return true; return false; }

        /// Altura de la superficie de agua bajo/sobre (x,z) de `p` más cercana a p.y. false si no hay agua en ese punto.
        /// Se lanza un rayo hacia abajo desde arriba del volumen y, si falla (caras hacia abajo), uno hacia arriba desde debajo.
        public static bool TryGetSurface(Vector3 p, out float y)
        {
            y = 0f; bool found = false; float bestD = float.MaxValue;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.Col == null) { entries.RemoveAt(i); continue; }   // dong descargado
                var b = e.R != null ? e.R.bounds : new Bounds(); b.Expand(new Vector3(0f, 2f, 0f)); if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                RaycastHit h; float len = b.size.y + 2f;
                bool hit = e.Col.Raycast(new Ray(new Vector3(p.x, b.max.y + 1f, p.z), Vector3.down), out h, len);
                if (!hit) hit = e.Col.Raycast(new Ray(new Vector3(p.x, b.min.y - 1f, p.z), Vector3.up), out h, len);
                if (!hit) continue;
                float d = Mathf.Abs(h.point.y - p.y); if (d < bestD) { bestD = d; y = h.point.y; found = true; }
            }
            return found;
        }

        public static void Clear() { entries.Clear(); }
    }
}
