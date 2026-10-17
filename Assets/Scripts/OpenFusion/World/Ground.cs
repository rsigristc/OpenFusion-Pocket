// Suelo bajo un punto: terrenos (SampleHeight) y colisionadores. El servidor sólo manda una altura aproximada; el cliente original deja caer al avatar con física.
using UnityEngine;

namespace OpenFusion.World
{
    public static class Ground
    {
        /// Altura del suelo más cercana a p.y bajo/alrededor de p (terreno o colisionador). Devuelve p.y si no hay nada.
        public static float Y(Vector3 p, float search = 60f)
        {
            float best = p.y; float bestD = float.MaxValue; bool found = false; float topTerrain = float.NegativeInfinity;
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                var o = t.transform.position; var sz = t.terrainData.size;
                if (p.x < o.x || p.z < o.z || p.x > o.x + sz.x || p.z > o.z + sz.z) continue;
                float y = t.SampleHeight(p) + o.y; if (y > topTerrain) topTerrain = y; float d = Mathf.Abs(y - p.y);
                if (d < bestD) { bestD = d; best = y; found = true; }
            }
            var hits = Physics.RaycastAll(new Vector3(p.x, p.y + search, p.z), Vector3.down, search * 2f);
            foreach (var h in hits) { float d = Mathf.Abs(h.point.y - p.y); if (d < bestD) { bestD = d; best = h.point.y; found = true; } }
            if (topTerrain > best) best = topTerrain;   // el terreno es el suelo visible: nunca por debajo de él (evita mobs hundidos)
            return found ? best : p.y;
        }
        public static string Describe(Vector3 p)
        {
            var sb = new System.Text.StringBuilder("suelo en " + p + ": ");
            foreach (var t in Terrain.activeTerrains) { if (t == null || t.terrainData == null) continue; var o = t.transform.position; var sz = t.terrainData.size;
                if (p.x >= o.x && p.z >= o.z && p.x <= o.x + sz.x && p.z <= o.z + sz.z) sb.Append("terreno '" + t.name + "' y=" + (t.SampleHeight(p) + o.y) + " (origen y=" + o.y + ", alto=" + sz.y + ", collider=" + (t.GetComponent<Collider>() != null) + ") "); }
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 100f, p.z), Vector3.down, 200f)) sb.Append("| rayo '" + h.collider.name + "' y=" + h.point.y + " ");
            sb.Append("| terrenos activos=" + Terrain.activeTerrains.Length);
            return sb.ToString();
        }
    }
}
