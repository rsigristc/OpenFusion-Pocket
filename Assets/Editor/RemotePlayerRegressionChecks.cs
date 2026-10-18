using System;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class RemotePlayerRegressionChecks
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static void Put(byte[] data, int offset, int value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, offset, 4);
        static byte[] Appearance(int id, int x, int y, int z)
        {
            var body = new byte[PcAppearance.AppearanceSize]; Put(body, 0, id);
            body[66] = 1; body[86] = 9; Put(body, 88, 100); Put(body, 96, x); Put(body, 100, y); Put(body, 104, z);
            return body;
        }
        static void Send(PacketDispatcher dispatcher, uint type, byte[] body)
        { var packet = new byte[body.Length + 4]; Put(packet, 0, (int)type); Buffer.BlockCopy(body, 0, packet, 4, body.Length); dispatcher.Dispatch(packet); }
        public static void Run()
        {
            var host = new GameObject("Remote player fixture");
            try
            {
                var dispatcher = new PacketDispatcher(); var entities = host.AddComponent<WorldEntities>();
                entities.Init(dispatcher, null, null, null, null);
                var update = typeof(WorldEntities).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
                Action drain = () => update.Invoke(entities, null);
                Send(dispatcher, PacketIds.P_FE2CL_PC_NEW, Appearance(42, 370540, 444137, -5300)); drain();
                var remote = entities.TransformOfPlayer(42);
                Check(entities.PlayerCount == 1 && remote != null && entities.AppOf(42).Level == 9, "PC_NEW must create a tracked remote independently of the local player.");
                Check(Vector3.Distance(remote.position, new Vector3(3705.4f, -53, 4441.37f)) < .02f, "Fallback players must use server coordinates instead of spawning at world origin.");
                Check(!WorldEntities.HasPlayerModel(remote.gameObject) && remote.Find("RemoteAvatarPlaceholder") != null, "An empty model/shadow must retain a visible explicitly provisional marker.");
                var around = new byte[4 + 2 * PcAppearance.AppearanceSize]; Put(around, 0, 2);
                Buffer.BlockCopy(Appearance(42, 1000, 2000, 3000), 0, around, 4, PcAppearance.AppearanceSize);
                Buffer.BlockCopy(Appearance(43, 4000, 5000, 6000), 0, around, 4 + PcAppearance.AppearanceSize, PcAppearance.AppearanceSize);
                Send(dispatcher, PacketIds.P_FE2CL_PC_AROUND, around); drain(); drain();
                Check(entities.PlayerCount == 2 && entities.AppOf(43) != null, "PC_AROUND must create every player and replace duplicate IDs without accumulating entries.");
                var move = new byte[WorldPackets.PcMoveSize]; Put(move, 8, 12300); Put(move, 12, 45600); Put(move, 16, 78900); Put(move, 44, 43);
                Send(dispatcher, PacketIds.P_FE2CL_PC_MOVE, move); drain();
                var mover = entities.TransformOfPlayer(43).GetComponent<RemoteMover>();
                var target = (Vector3)typeof(RemoteMover).GetField("target", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mover);
                Check(target == new Vector3(123, 789, 456), "Remote movement must preserve server axis and centimetre conversion.");
                var bad = new byte[4]; Put(bad, 0, 2); Send(dispatcher, PacketIds.P_FE2CL_PC_AROUND, bad); drain();
                Check(dispatcher.MalformedCount == 1 && entities.PlayerCount == 2, "Truncated player lists must be diagnosed without deleting existing players.");
                var exit = BitConverter.GetBytes(43); Send(dispatcher, PacketIds.P_FE2CL_PC_EXIT, exit); drain();
                Check(entities.PlayerCount == 1 && entities.AppOf(43) == null && entities.TransformOfPlayer(43) == null, "PC_EXIT must remove the player, appearance and recovery entry.");
                var oldModel = new GameObject("old avatar"); oldModel.transform.SetParent(remote); oldModel.AddComponent<AvatarModelRoot>();
                var effect = new GameObject("unrelated effect"); effect.transform.SetParent(remote);
                var replacement = new GameObject("replacement avatar"); replacement.transform.SetParent(remote);
                PlayerAvatarFactory.RetireRemoteModel(remote.gameObject, replacement);
                Check(!oldModel.activeSelf && effect.activeSelf && replacement.activeSelf, "Model recovery must retire only the avatar/placeholder, preserving other effects and the replacement.");
                UnityEngine.Debug.Log("[players regression] new/around/move/exit, malformed list, duplicate ID and missing-model coordinates passed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
