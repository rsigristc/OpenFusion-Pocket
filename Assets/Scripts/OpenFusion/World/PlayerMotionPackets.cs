using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    // Server axes are X, horizontal Y, vertical Z, in centimetres.
    // Always use the current position, including slopes, falls and swimming.
    public static class PlayerMotionPackets
    {
        static int S(float v) => Mathf.RoundToInt(v * 100f);
        public static byte[] Move(ulong time, Vector3 position, Vector3 velocity, int angle, byte key, int speed) =>
            ClientPackets.Move(time, S(position.x), S(position.z), S(position.y), velocity.x, velocity.z, velocity.y, angle, key, speed);
        public static byte[] Stop(ulong time, Vector3 position) =>
            ClientPackets.Stop(time, S(position.x), S(position.z), S(position.y));
        public static byte[] Jump(ulong time, Vector3 position, Vector3 velocity, int angle, byte key, int speed) =>
            ClientPackets.Jump(time, S(position.x), S(position.z), S(position.y), S(velocity.x), S(velocity.z), S(velocity.y), angle, key, speed);
    }
}
