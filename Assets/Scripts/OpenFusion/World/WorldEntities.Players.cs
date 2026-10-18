using System;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    public sealed partial class WorldEntities
    {
        int playerNewPackets, playerAroundPackets, playerMovePackets, playerMalformedPackets;
        int playerSpawns, playerExits, playerRecoveries;
        float nextPlayerAudit, nextPlayerRepair;
        readonly System.Collections.Generic.Dictionary<int, float> missingPlayers = new System.Collections.Generic.Dictionary<int, float>();

        void PlayerPacket(uint type, byte[] body)
        {
            if (type == PacketIds.P_FE2CL_PC_NEW) System.Threading.Interlocked.Increment(ref playerNewPackets);
            else if (type == PacketIds.P_FE2CL_PC_AROUND) System.Threading.Interlocked.Increment(ref playerAroundPackets);
            else if (type == PacketIds.P_FE2CL_PC_MOVE || type == PacketIds.P_FE2CL_PC_STOP || type == PacketIds.P_FE2CL_PC_JUMP)
                System.Threading.Interlocked.Increment(ref playerMovePackets);
        }
        void PlayerMalformed(uint type, Exception error)
        {
            if (type == PacketIds.P_FE2CL_PC_NEW || type == PacketIds.P_FE2CL_PC_AROUND)
            {
                System.Threading.Interlocked.Increment(ref playerMalformedPackets);
                queue.Enqueue(() => UnityEngine.Debug.LogWarning("[players] malformed " + PacketIds.Name(type) + ": " + error.Message));
            }
        }
        // A shadow, marker or empty skeleton is not proof that an avatar was built.
        public static bool HasPlayerModel(GameObject holder)
        {
            if (holder == null) return false;
            foreach (var renderer in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null && renderer.sharedMesh.vertexCount > 0) return true;
            return false;
        }
        void PrepareRemotePlayer(GameObject go, PcAppearance appearance)
        {
            var animation = go.GetComponentInChildren<Animation>(true);
            if (animation != null)
            {
                var animator = go.GetComponent<NpcAnimator>(); if (animator == null) animator = go.AddComponent<NpcAnimator>();
                animator.Player = true; animator.AniStyle = StyleOf(appearance); animator.Init(animation.gameObject, null, null);
                if (animation.GetComponent<AvatarEmoteGuard>() == null) animation.gameObject.AddComponent<AvatarEmoteGuard>();
                if (appearance.Hp <= 0) animator.PlayerDie();
            }
            if (!HasPlayerModel(go))
            {
                if (go.transform.Find("RemoteAvatarPlaceholder") == null)
                {
                    var placeholder = new GameObject("RemoteAvatarPlaceholder"); placeholder.transform.SetParent(go.transform, false); Marker(placeholder, Color.cyan);
                }
                missingPlayers[appearance.Id] = Time.unscaledTime + 2;
                UnityEngine.Debug.LogWarning("[players] model unavailable id=" + appearance.Id + " gender=" + appearance.Style.Gender + " position=" + go.transform.position + "; visible placeholder; retry enabled");
            }
        }
        void UpdatePlayers()
        {
            // At most one rebuild per two seconds, without replacing the network/movement holder.
            if (Time.unscaledTime >= nextPlayerRepair && avatars != null)
            {
                nextPlayerRepair = Time.unscaledTime + 2;
                int candidate = 0;
                foreach (var entry in missingPlayers) if (Time.unscaledTime >= entry.Value) { candidate = entry.Key; break; }
                if (candidate != 0)
                {
                    if (!players.TryGetValue(candidate, out var go) || go == null || !apps.TryGetValue(candidate, out var app)) missingPlayers.Remove(candidate);
                    else if (avatars.RepairRemoteModel(go, app))
                    {
                        missingPlayers.Remove(candidate); PrepareRemotePlayer(go, app); playerRecoveries++;
                        UnityEngine.Debug.Log("[players] model recovered id=" + candidate);
                    }
                    else missingPlayers[candidate] = Time.unscaledTime + 15;
                }
            }
            if (Self == null || Time.unscaledTime < nextPlayerAudit) return;
            nextPlayerAudit = Time.unscaledTime + 15;
            int models = 0, drawable = 0, nearby = 0, inView = 0;
            var cam = Camera.main; var planes = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;
            foreach (var entry in players)
            {
                var go = entry.Value; if (go == null) continue;
                if (Vector3.Distance(Self.transform.position, go.transform.position) <= 150) nearby++;
                if (!HasPlayerModel(go)) continue; models++;
                bool shown = false, framed = false;
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || r.sharedMesh == null || cam == null || (cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                    shown = true; if (planes != null && GeometryUtility.TestPlanesAABB(planes, r.bounds)) framed = true;
                }
                if (shown) drawable++; if (framed) inView++;
            }
            UnityEngine.Debug.Log("[players] newPackets=" + playerNewPackets + " aroundPackets=" + playerAroundPackets + " moves=" + playerMovePackets +
                " malformed=" + playerMalformedPackets + " spawned=" + playerSpawns + " exits=" + playerExits + " tracked=" + players.Count +
                " models=" + models + " drawable=" + drawable + " within150m=" + nearby + " inFrustum=" + inView + " recovered=" + playerRecoveries + " queued=" + queue.Count);
        }
    }
}
