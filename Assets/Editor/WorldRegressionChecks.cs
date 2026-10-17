using System;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class WorldRegressionChecks
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Put(byte[] b, int offset, int value) => BitConverter.GetBytes(value).CopyTo(b, offset);
        public static void Run()
        {
            Check(EnvironmentAreas.InArea(8, 8, 12, 12, false, 0), "Infection terrain not detected");
            Check(!EnvironmentAreas.InArea(8, 8, 13, 12, false, 0), "Jump incorrectly entered infection area");
            Check(EnvironmentAreas.InArea(16, 16, 13, 12, true, 0), "Healing water not detected");
            Check(!EnvironmentAreas.InArea(72, 8, 12, 12, false, 0), "High attributes incorrectly treated as infection");
            Check(!EnvironmentAreas.InArea(8, 8, 12, 12, true, 1), "Vehicle incorrectly entered infection area");
            var data = new HudData { Hp = 100, ActiveNanoSlot = 0, NanoSlots = new short[] { 3, 0, 0 }, NanoBankStamina = new short[40] };
            var dot = new byte[44]; Put(dot, 0, 1); Put(dot, 4, 77); Put(dot, 8, 17); Put(dot, 12, 1); Put(dot, 16, 77);
            Put(dot, 20, 1); Put(dot, 24, 0); Put(dot, 28, 81); Put(dot, 32, 12); Put(dot, 36, 1); Put(dot, 40, 8);
            Check(HudPackets.ApplyTimeBuff(dot, 77, data, out int kind, out int amount, out int flags) && kind == 17 && amount == -2 && flags == 8 && data.Hp == 81 && data.ActiveNanoSlot == -1 && data.NanoBankStamina[3] == 12, "Authoritative protected infection/nano depletion");
            data.Hp = 100;
            var truncated = new byte[43]; Array.Copy(dot, truncated, truncated.Length);
            Check(!HudPackets.ApplyTimeBuff(truncated, 77, data, out _, out _, out _) && data.Hp == 100, "Truncated tick changed HP");
            Put(dot, 16, 78);
            Check(!HudPackets.ApplyTimeBuff(dot, 77, data, out _, out _, out _) && data.Hp == 100, "Another player's tick changed local HP");
            var heal = new byte[28]; Put(heal, 0, 1); Put(heal, 4, 77); Put(heal, 8, 24); Put(heal, 12, 1); Put(heal, 16, 77); Put(heal, 20, 21); Put(heal, 24, 121);
            Check(HudPackets.ApplyTimeBuff(heal, 77, data, out kind, out amount, out _) && data.Hp == 121 && amount == 21, "Healing pool HP result");
            var poison = new byte[32]; Array.Copy(dot, poison, 32); Put(poison, 8, 19); Put(poison, 16, 77); Put(poison, 20, 0); Put(poison, 24, 40); Put(poison, 28, 0);
            Check(HudPackets.ApplyTimeBuff(poison, 77, data, out _, out amount, out _) && data.Hp == 0 && amount == 40, "Lethal poison tick ignored");

            var host = new GameObject("WorldRegression");
            try
            {
                var race = RaceSystem.Ensure(host); var dispatcher = new PacketDispatcher(); race.Init(dispatcher, null, null, null);
                var ep = EpTriggers.Ensure(host); ep.Init(null, null);
                var switchGo = new GameObject("Switch"); switchGo.transform.SetParent(host.transform);
                var node = EpTriggers.Register(new EpInfo { Go = switchGo, Type = EpT.Switch });
                node.ServerId = 1; node.SwitchOn();
                var pkt = ClientPackets.New(PacketIds.P_FE2CL_INSTANCE_MAP_INFO, 64);
                Put(pkt, 4, 9); Put(pkt, 20, 51200); Put(pkt, 28, 51200); Put(pkt, 40, 11); Put(pkt, 44, 123); Put(pkt, 60, 1); Put(pkt, 64, 1);
                dispatcher.Dispatch(pkt);
                typeof(RaceSystem).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(race, null);
                Check(RaceSystem.InsMap && RaceSystem.SwitchOn.Length == 1 && RaceSystem.TopScore == 123, "Valid instance packet ignored");
                var bad = new byte[60]; Put(bad, 56, int.MaxValue);
                Check(!RaceSystem.ValidInstanceInfo(bad) && !RaceSystem.ValidInstanceInfo(new byte[56]), "Malformed instance packet accepted");
                RaceSystem.ResetState();
                Check(!RaceSystem.InsMap && RaceSystem.SwitchOn.Length == 0 && RaceSystem.TopScore == 0 && RaceSystem.InsArea == default(Rect) && node.ServerId == -1 && !node.SwitchedOn && switchGo.transform.position.y == 0, "Instance reset retained switches or metadata");
                dispatcher.Dispatch(pkt);
                typeof(RaceSystem).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(race, null);
                // Edit-mode objects never enter Play/Awake; invoke the lifecycle callback explicitly.
                typeof(RaceSystem).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(race, null);
                UnityEngine.Object.DestroyImmediate(race);
                Check(!RaceSystem.InsMap && RaceSystem.SwitchOn.Length == 0, "Session teardown leaked instance state");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); RaceSystem.ResetState(); }
            Debug.Log("[world-regression] Passed: infection/healing, authoritative ticks, instance validation and lifecycle");
        }
    }
}
