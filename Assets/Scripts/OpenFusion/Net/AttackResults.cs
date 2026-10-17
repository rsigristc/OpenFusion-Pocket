using System;
using System.Collections.Generic;

namespace OpenFusion.Net
{
    public struct AttackResult
    {
        public int CharacterType, Id, Protected, Damage, Hp;
        public byte HitFlag;
        public bool IsNpc => CharacterType == 2 || CharacterType == 4;
        // The dedicated ATTACK_NPCs packet already identifies the target category.
        // OpenFusion also leaves eCT zero in its NPC-only grenade compatibility
        // response (used for rockets). Genuine mixed-target packets remain strict.
        public bool TargetsNpc(bool npcOnly = false) => IsNpc || npcOnly && CharacterType == 0;
    }
    public static class AttackResults
    {
        // Offsets are relative to the packet body, after the four-byte packet ID.
        public static List<AttackResult> Read(byte[] body, int countOffset, int first, int stride = 24)
        {
            if (body == null || countOffset < 0 || first < countOffset + 4 || stride < 20 || body.Length < first)
                throw new ArgumentException("Invalid combat result header");
            int count = BitConverter.ToInt32(body, countOffset);
            if (count < 0 || count > (body.Length - first) / stride)
                throw new ArgumentException("Combat result count exceeds packet length");
            var result = new List<AttackResult>(count);
            for (int i = 0; i < count; i++)
            {
                int o = first + i * stride;
                result.Add(new AttackResult { CharacterType = BitConverter.ToInt32(body, o), Id = BitConverter.ToInt32(body, o + 4),
                    Protected = BitConverter.ToInt32(body, o + 8), Damage = BitConverter.ToInt32(body, o + 12), Hp = BitConverter.ToInt32(body, o + 16),
                    HitFlag = stride == 24 ? body[o + 20] : (byte)0 });
            }
            return result;
        }
    }
}
