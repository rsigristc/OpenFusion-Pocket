using System;
using System.Reflection;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class CameraGroundingRegressionChecks
    {
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        public static void Run()
        {
            var sender = NpcUI.Sender; var previousClient = GameClient.FindSelf;
            bool waterCollision = Physics.GetIgnoreLayerCollision(0, 2), previewCollision = Physics.GetIgnoreLayerCollision(0, 30);
            var host = new GameObject("Grounding regression"); var player = new GameObject("Idle player");
            TerrainData data = null; GameObject terrain = null;
            try
            {
                // A visible terrain whose physical collider is unavailable must still
                // supply a stable floor. This reproduced the old 1 cm sink/reset loop.
                data = new TerrainData { heightmapResolution = 33, size = new Vector3(16, 10, 16) };
                terrain = Terrain.CreateTerrainGameObject(data); terrain.transform.position = new Vector3(10000, 7, 10000);
                UnityEngine.Object.DestroyImmediate(terrain.GetComponent<TerrainCollider>());
                player.transform.position = new Vector3(10008, 7, 10008);
                var client = host.AddComponent<GameClient>(); client.enabled = false; client.SendMovement = false;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var walk = typeof(GameClient).GetMethod("WalkCC", flags);
                var grounded = typeof(GameClient).GetField("ccGrounded", flags);
                grounded.SetValue(client, true);
                for (int frame = 0; frame < 180; frame++)
                {
                    walk.Invoke(client, new object[] { player.transform, Vector3.zero, Vector3.zero });
                    Check(Mathf.Abs(player.transform.position.y - 7) < 0.0001f, "Idle grounding must not repeatedly sink below visible terrain and move the follow-camera pivot.");
                }
                player.transform.position += Vector3.up * 2;
                grounded.SetValue(client, false);
                walk.Invoke(client, new object[] { player.transform, Vector3.zero, Vector3.zero });
                Check(player.transform.position.y > 8.5f, "Terrain correction must not snap an airborne player down to the floor.");
                Debug.Log("[camera grounding regression] 180 idle grounding probes remain stable without a terrain collider; airborne height remains free.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player); UnityEngine.Object.DestroyImmediate(host);
                if (terrain != null) UnityEngine.Object.DestroyImmediate(terrain);
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
                NpcUI.Sender = sender; GameClient.FindSelf = previousClient;
                Physics.IgnoreLayerCollision(0, 2, waterCollision); Physics.IgnoreLayerCollision(0, 30, previewCollision);
            }
        }
    }
}
