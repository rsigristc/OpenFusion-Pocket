// Suelo bajo un punto: terrenos (SampleHeight) y colisionadores. El servidor sólo manda una altura aproximada; el cliente original deja caer al avatar con física.
using UnityEngine;

namespace OpenFusion.World
{
    public static class Ground
    {
        static readonly RaycastHit[] cameraHits = new RaycastHit[64], supportHits = new RaycastHit[64];
        // Legacy EnvironmentCollision.HandleSurfaceSliding replaces horizontal
        // input with the surface's downhill direction at 2 m/s, doubling gravity.
        public static bool SlideVelocity(Vector3 feet, Transform actor, float slopeLimit, ref Vector3 velocity)
        {
            Vector3 normal = Vector3.up; float surface = float.NegativeInfinity;
            if (CameraObstacle(feet + Vector3.up * .15f, Vector3.down, .4f, actor, ~((1 << 2) | (1 << 30)), out var hit))
            { normal = hit.normal; surface = hit.point.y; }
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                var origin = terrain.transform.position; var size = terrain.terrainData.size;
                float x = (feet.x - origin.x) / size.x, z = (feet.z - origin.z) / size.z;
                if (x < 0 || x > 1 || z < 0 || z > 1) continue;
                float height = terrain.SampleHeight(feet) + origin.y;
                if (height > surface && height <= feet.y + .15f && height >= feet.y - .25f)
                { surface = height; normal = terrain.terrainData.GetInterpolatedNormal(x, z); }
            }
            if (surface == float.NegativeInfinity || normal.y <= .001f || Vector3.Angle(normal, Vector3.up) <= slopeLimit) return false;
            var downhill = new Vector3(normal.x, 0, normal.z).normalized * 2f;
            velocity = new Vector3(downhill.x, Mathf.Min(velocity.y, 0) * 2f, downhill.z);
            return true;
        }
        public static bool CameraObstacle(Vector3 origin, Vector3 direction, float distance, Transform actor, int mask, out RaycastHit nearest)
        {
            nearest = default; bool found = false; float closest = distance;
            int count = Physics.RaycastNonAlloc(origin, direction, cameraHits, distance, mask, QueryTriggerInteraction.Ignore);
            var hits = count == cameraHits.Length ? Physics.RaycastAll(origin, direction, distance, mask, QueryTriggerInteraction.Ignore) : cameraHits;
            if (hits != cameraHits) count = hits.Length;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider is CharacterController || hit.collider.GetComponentInParent<NpcInfo>() != null || hit.collider.GetComponentInParent<RemotePc>() != null ||
                    actor != null && hit.transform.IsChildOf(actor) || hit.distance >= closest) continue;
                closest = hit.distance; nearest = hit; found = true;
            }
            return found;
        }
        /// A bounded, upward-facing physical support near the avatar's feet.
        /// Unlike Y(), exclude actors/triggers and choose the closest surface, not
        /// the highest roof. Useful when warp coordinates begin inside a platform.
        public static bool TrySupport(Vector3 feet, Transform actor, float rise, float drop, float slope, out float height)
        {
            height=feet.y;float nearest=float.PositiveInfinity;bool found=false;
            var origin=feet+Vector3.up*(rise+.02f); float length=rise+drop+.04f; int mask=~((1<<2)|(1<<30));
            int count=Physics.RaycastNonAlloc(origin,Vector3.down,supportHits,length,mask,QueryTriggerInteraction.Ignore);
            var hits=count==supportHits.Length?Physics.RaycastAll(origin,Vector3.down,length,mask,QueryTriggerInteraction.Ignore):supportHits;
            if(hits!=supportHits)count=hits.Length;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(hit.collider==null || hit.collider is CharacterController || hit.collider.GetComponentInParent<NpcInfo>()!=null || hit.collider.GetComponentInParent<RemotePc>()!=null || actor!=null && (hit.transform==actor || hit.transform.IsChildOf(actor)))continue;
                if(hit.normal.y<Mathf.Cos(slope*Mathf.Deg2Rad))continue;
                float delta=hit.point.y-feet.y;if(delta>rise+.001f || delta < -drop-.001f)continue;
                float distance=Mathf.Abs(delta);if(distance>=nearest)continue;
                nearest=distance;height=hit.point.y;found=true;
            }
            return found;
        }
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
