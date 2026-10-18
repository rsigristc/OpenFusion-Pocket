using System;
using OpenFusion.Net;

namespace OpenFusion.World
{
    // Packed protocol results. Never treat BatteryDrain's iBatteryW as iHP.
    public static class SkillResults
    {
        static int I(byte[] b, int o) => BitConverter.ToInt32(b, o);
        static short S(byte[] b, int o) => BitConverter.ToInt16(b, o);
        public static bool Damage(int type) => type == 1 || type == 3 || type == 4 || type == 5 || type == 8 || type == 24 || type == 30;
        public static int Stride(int type)
        {
            if (type == 1 || type == 30) return 20;
            if (type == 3 || type == 4 || type == 5 || type == 8) return 32;
            if (type == 21 || type == 22) return 40;
            if (type == 24) return 36;
            if (type == 26) return 12;
            if (type == 7 || type == 27 || type == 28 || type == 29 || type == 40) return 24;
            if (type == 2 || type == 6 || type == 9 || type == 10 || type == 11 || type == 12 || type == 13 || type == 14 || type == 15 || type == 16 || type == 17 || type == 18 || type == 19 || type == 20 || type == 23 || type == 25 || type == 31 || type == 32 || type == 33 || type == 34 || type == 35) return 16;
            return 0;
        }
        public static bool IsPacket(uint t) => t == PacketIds.P_FE2CL_NPC_SKILL_HIT || t == PacketIds.P_FE2CL_NPC_SKILL_CORRUPTION_HIT ||
            t == PacketIds.P_FE2CL_NANO_SKILL_USE || t == PacketIds.P_FE2CL_NANO_SKILL_USE_SUCC ||
            t == PacketIds.P_FE2CL_PC_ROCKET_STYLE_HIT || t == PacketIds.P_FE2CL_PC_GRENADE_STYLE_HIT || t == PacketIds.P_FE2CL_NPC_ATTACK_CHARs;

        public static void SetBankStamina(HudData d, int id, short stamina)
        {
            if (id <= 0) return;
            for (int i = 0; i < 3; i++) if (d.NanoSlots[i] == id) d.NanoStamina[i] = stamina;
            if (d.NanoBankStamina != null && id < d.NanoBankStamina.Length) d.NanoBankStamina[id] = stamina;
        }
        static void ActiveStamina(HudData d, short stamina, bool inactive)
        {
            int slot = d.ActiveNanoSlot;
            if (slot >= 0 && slot < 3) SetBankStamina(d, d.NanoSlots[slot], stamina);
            if (inactive) d.ActiveNanoSlot = -1;
        }
        public static bool ApplyPacket(uint type, byte[] b, int self, HudData d, out int flags)
        {
            flags = -1;
            if (!IsPacket(type) || self <= 0 || d == null || b == null) return false;
            int countOffset, first, stride, est;
            bool attack = type == PacketIds.P_FE2CL_PC_ROCKET_STYLE_HIT || type == PacketIds.P_FE2CL_PC_GRENADE_STYLE_HIT || type == PacketIds.P_FE2CL_NPC_ATTACK_CHARs;
            if (attack) { countOffset = type == PacketIds.P_FE2CL_NPC_ATTACK_CHARs ? 4 : 20; first = countOffset + 4; stride = 24; est = 1; }
            else if (type == PacketIds.P_FE2CL_NPC_SKILL_CORRUPTION_HIT) { countOffset = 20; first = 24; stride = 40; est = 22; }
            else {
                bool npc = type == PacketIds.P_FE2CL_NPC_SKILL_HIT;
                countOffset = npc ? 24 : 32; first = countOffset + 4;
                if (b.Length < first) return false;
                est = I(b, countOffset - 4); stride = Stride(est);
                if (est == 30) first += 16; // Legacy bloodsucking: heal result precedes damage results.
            }
            if (stride == 0 || b.Length < first) return false;
            int count = I(b, countOffset);
            if (count < 0 || count > (b.Length - first) / stride) return false;
            bool changed = false;
            if (est == 30 && I(b, first - 16) == 1 && I(b, first - 12) == self) { d.Hp = I(b, first - 4); changed = true; }
            for (int i = 0; i < count; i++) {
                int o = first + stride * i;
                if (I(b, o) != 1 || I(b, o + 4) != self) continue;
                changed = true;
                if (Damage(est) || est == 22) d.Hp = I(b, o + 16);
                else if (est == 2 || est == 34 || est == 7) d.Hp = I(b, o + 12);
                if (est == 3 || est == 4 || est == 5 || est == 8) { ActiveStamina(d, S(b, o + 20), I(b, o + 24) != 0); flags = I(b, o + 28); }
                else if (est == 21) { d.BatteryWpn = I(b, o + 16); d.BatteryNano = I(b, o + 24); ActiveStamina(d, S(b, o + 28), I(b, o + 32) != 0); flags = I(b, o + 36); }
                else if (est == 22) { SetBankStamina(d, S(b, o + 28), S(b, o + 30)); d.ActiveNanoSlot = I(b, o + 24) != 0 ? -1 : S(b, o + 22); flags = I(b, o + 32); }
                else if (est == 6) SetBankStamina(d, S(b, o + 10), S(b, o + 14));
                else if (est == 7) SetBankStamina(d, S(b, o + 18), S(b, o + 22));
                else if (stride == 16 && est != 2 && est != 6 && est != 34) flags = I(b, o + 12);
            }
            return changed;
        }
    }
}
