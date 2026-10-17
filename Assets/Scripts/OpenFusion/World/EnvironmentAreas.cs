using System;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    // cnOwnAvatarStatus: report area transitions; HP is exclusively authoritative.
    public sealed class EnvironmentAreas : MonoBehaviour
    {
        DongLoader loader; WorldEntities entities;
        bool poison, heal; float poisonAt = -1f, healAt = -1f, sampleAt;
        public void Init(DongLoader l, WorldEntities e) { loader = l; entities = e; }
        public static bool InArea(byte attributes, int mask, float y, float terrainY, bool swimming, int vehicle)
            => attributes < 64 && (attributes & mask) != 0 && vehicle == 0 && (swimming || Mathf.Abs(y - terrainY) < .5f);
        void Update()
        {
            if (Time.unscaledTime < sampleAt || loader == null || loader.Paused || WorldBootstrap.Travelling || entities == null || entities.Self == null) return;
            sampleAt = Time.unscaledTime + .2f;
            var gc = GameClient.FindSelf; if (gc == null || NpcUI.Sender == null) return;
            var p = entities.Self.transform.position;
            byte attributes = loader.AttributeAt(p, out bool known);
            // Unloaded terrain must never be interpreted as a clean zone.
            if (!known || !loader.TryTerrainHeight(p, out float height)) return;
            Transition(InArea(attributes, 8, p.y, height, gc.IsSwimming, gc.Vehicle), ref poison, ref poisonAt, PacketIds.P_CL2FE_DOT_DAMAGE_ONOFF);
            Transition(InArea(attributes, 16, p.y, height, gc.IsSwimming, gc.Vehicle), ref heal, ref healAt, PacketIds.P_CL2FE_DOT_HEAL_ONOFF);
        }
        static void Transition(bool value, ref bool previous, ref float changedAt, uint type)
        {
            if (value == previous || Time.unscaledTime - changedAt < 1f) return;
            var packet = ClientPackets.New(type, 4); ClientPackets.PutInt(packet, 4, value ? 1 : 0);
            NpcUI.Sender(packet); previous = value; changedAt = Time.unscaledTime;
            Debug.Log("[environment] " + type + " active=" + value);
        }
    }
}
