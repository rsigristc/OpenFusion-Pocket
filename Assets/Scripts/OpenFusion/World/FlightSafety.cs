using UnityEngine;
namespace OpenFusion.World
{
    public static class FlightSafety
    {
        public static Vector3 LaunchPosition(Vector3 origin, Vector3 direction, float offset, Transform actor, Transform launcher, CharacterController controller)
        {
            float terrain = GameClient.TerrainTop(origin);
            if (origin.y < terrain + .03f) origin.y = terrain + .03f;
            if (Ground.TrySupport(origin, actor, .5f, .1f, 60, out float surface) && origin.y < surface + .03f) origin.y = surface + .03f;
            float radius = controller != null ? controller.radius : .3f, height = controller != null ? controller.height : 1.6f;
            float clear = offset; direction.Normalize();
            var bottom = origin + Vector3.up * radius; var top = origin + Vector3.up * Mathf.Max(radius, height - radius);
            foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, direction, offset, ~((1 << 2) | (1 << 30)), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null || actor != null && hit.transform.IsChildOf(actor) || launcher != null && hit.transform.IsChildOf(launcher)) continue;
                clear = Mathf.Min(clear, Mathf.Max(0, hit.distance - .05f));
            }
            var result = origin + direction * clear;
            terrain = GameClient.TerrainTop(result); if (result.y < terrain + .03f) result.y = terrain + .03f;
            return result;
        }
        public static void Relocate(CharacterController controller, Transform actor, Vector3 position)
        {
            bool on = controller != null && controller.enabled;
            if (on) controller.enabled = false;
            actor.position = position;
            if (on) controller.enabled = true;
            Physics.SyncTransforms();
        }
        // Legacy applies terrain correction in its movement pipeline as well as CC
        // collisions. EP flight bypasses GameClient.WalkCC, so it must do this too.
        public static bool CorrectTerrain(CharacterController controller, Transform actor)
        {
            float terrain = GameClient.TerrainTop(actor.position);
            if (actor.position.y >= terrain) return false;
            var position = actor.position; position.y = terrain;
            Relocate(controller, actor, position); return true;
        }
    }
}
