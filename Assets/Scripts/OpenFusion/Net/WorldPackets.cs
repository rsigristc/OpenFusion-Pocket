// Paquetes FE2CL que describen el mundo visible (jugadores y NPC que entran/salen/se mueven). Puro; sin Unity.
// Disposición = Marshal Pack 4 del cliente original; el cuerpo empieza tras los 4 bytes del tipo. Las listas son [cabecera de 4 B con el contador] + elementos.
using System;
using System.Collections.Generic;

namespace OpenFusion.Net
{
    public struct NpcAppearance { public int Id, Type, Hp, ConditionBitFlag, X, Y, Z, Angle, BarkerType; public const int Size = 36; }
    public struct PcMove { public ulong CliTime, SvrTime; public int X, Y, Z, Angle, Speed, Id; public float VX, VY, VZ; public byte Key; }
    public struct PcStop { public ulong CliTime, SvrTime; public int X, Y, Z, Id; }
    public struct PcJump { public ulong CliTime, SvrTime; public int X, Y, Z, VX, VY, VZ, Angle, Speed, Id; public byte Key; }
    public struct NpcMove { public int Id, ToX, ToY, ToZ, Speed; public short Style; }
    public struct EnterSucc { public int Id; public PcAppearance Self; public ulong SvrTime; public QuestSave Quest; }
    /// Datos de misiones de sPCLoadData2CL (desplazamientos calculados desde el final de la estructura: aQuestFlag en 1712, aRepeatQuestFlag 1968, aRunningQuest 2032, aQInven 844).
    public sealed class QuestSave
    {
        public long[] Flags = new long[32], Repeat = new long[8]; public int[] Running = new int[9], QItemId = new int[50], QItemOpt = new int[50]; public int Mentor, Level; public long FirstUse1, FirstUse2; public ItemBase[] Inven = new ItemBase[50]; public int Warp; public long[] Wyvern = new long[2];   // iWarpLocationFlag / aWyvernLocationFlag
        public static QuestSave Parse(byte[] b, int o)
        {
            int d = o + 4; var q = new QuestSave(); q.Level = BitConverter.ToInt16(b, d + 84); q.Mentor = BitConverter.ToInt16(b, d + 86);
            for (int i = 0; i < 32; i++) q.Flags[i] = BitConverter.ToInt64(b, d + 1712 + 8 * i);
            for (int i = 0; i < 8; i++) q.Repeat[i] = BitConverter.ToInt64(b, d + 1968 + 8 * i);
            for (int k = 0; k < 9; k++) q.Running[k] = BitConverter.ToInt32(b, d + 2032 + 52 * k);
            for (int i = 0; i < 50; i++) { int oo = d + 244 + 12 * i; q.Inven[i] = new ItemBase { Type = BitConverter.ToInt16(b, oo), Id = BitConverter.ToInt16(b, oo + 2), Opt = BitConverter.ToInt32(b, oo + 4), TimeLimit = BitConverter.ToInt32(b, oo + 8) }; }
            for (int i = 0; i < 50; i++) { q.QItemId[i] = BitConverter.ToInt16(b, d + 844 + 12 * i + 2); q.QItemOpt[i] = BitConverter.ToInt32(b, d + 844 + 12 * i + 4); }
            if (b.Length >= d + 2552) { q.Warp = BitConverter.ToInt32(b, d + 2504); q.Wyvern[0] = BitConverter.ToInt64(b, d + 2508); q.Wyvern[1] = BitConverter.ToInt64(b, d + 2516); q.FirstUse1 = BitConverter.ToInt64(b, d + 2536); q.FirstUse2 = BitConverter.ToInt64(b, d + 2544); }   // sPCLoadData2CL.iFirstUseFlag1/2
            return q;
        }
    }

    public static class WorldPackets
    {
        public const int PcMoveSize = 56, PcStopSize = 32, PcJumpSize = 56, NpcMoveSize = 24, EnterSuccSize = 2564;
        static int I(byte[] b, int o) => BitConverter.ToInt32(b, o);
        static void Need(byte[] b, int o, int n, string what) { if (b.Length - o < n) throw new ArgumentException(what + " necesita " + n + " bytes, hay " + (b.Length - o)); }

        public static NpcAppearance ParseNpc(byte[] b, int o = 0)
        {
            Need(b, o, NpcAppearance.Size, "sNPCAppearanceData");
            return new NpcAppearance { Id = I(b, o), Type = I(b, o + 4), Hp = I(b, o + 8), ConditionBitFlag = I(b, o + 12), X = I(b, o + 16), Y = I(b, o + 20), Z = I(b, o + 24), Angle = I(b, o + 28), BarkerType = I(b, o + 32) };
        }
        public static PcMove ParsePcMove(byte[] b, int o = 0)
        {
            Need(b, o, PcMoveSize, "sP_FE2CL_PC_MOVE");
            return new PcMove { CliTime = BitConverter.ToUInt64(b, o), X = I(b, o + 8), Y = I(b, o + 12), Z = I(b, o + 16), VX = BitConverter.ToSingle(b, o + 20), VY = BitConverter.ToSingle(b, o + 24), VZ = BitConverter.ToSingle(b, o + 28), Angle = I(b, o + 32), Key = b[o + 36], Speed = I(b, o + 40), Id = I(b, o + 44), SvrTime = BitConverter.ToUInt64(b, o + 48) };
        }
        public static PcStop ParsePcStop(byte[] b, int o = 0)
        {
            Need(b, o, PcStopSize, "sP_FE2CL_PC_STOP");
            return new PcStop { CliTime = BitConverter.ToUInt64(b, o), X = I(b, o + 8), Y = I(b, o + 12), Z = I(b, o + 16), Id = I(b, o + 20), SvrTime = BitConverter.ToUInt64(b, o + 24) };
        }
        public static PcJump ParsePcJump(byte[] b, int o = 0)
        {
            Need(b, o, PcJumpSize, "sP_FE2CL_PC_JUMP");
            return new PcJump { CliTime = BitConverter.ToUInt64(b, o), X = I(b, o + 8), Y = I(b, o + 12), Z = I(b, o + 16), VX = I(b, o + 20), VY = I(b, o + 24), VZ = I(b, o + 28), Angle = I(b, o + 32), Key = b[o + 36], Speed = I(b, o + 40), Id = I(b, o + 44), SvrTime = BitConverter.ToUInt64(b, o + 48) };
        }
        public static NpcMove ParseNpcMove(byte[] b, int o = 0)
        {
            Need(b, o, NpcMoveSize, "sP_FE2CL_NPC_MOVE");
            return new NpcMove { Id = I(b, o), ToX = I(b, o + 4), ToY = I(b, o + 8), ToZ = I(b, o + 12), Speed = I(b, o + 16), Style = BitConverter.ToInt16(b, o + 20) };
        }
        public static EnterSucc ParseEnterSucc(byte[] b, int o = 0)
        {
            Need(b, o, EnterSuccSize, "sP_FE2CL_REP_PC_ENTER_SUCC");
            return new EnterSucc { Id = I(b, o), Self = PcAppearance.ParseLoadData(b, o + 4), SvrTime = BitConverter.ToUInt64(b, o + 2556), Quest = QuestSave.Parse(b, o) };
        }

        /// Lista = contador (int) + n elementos de tamaño fijo. El contador se valida contra el tamaño del paquete para no leer fuera.
        public static int CheckedCount(byte[] body, int elemSize, string what)
        {
            Need(body, 0, 4, what); int n = I(body, 0);
            if (n < 0 || (long)n * elemSize > body.Length - 4) throw new ArgumentException(what + ": contador " + n + " no cabe en el paquete (" + body.Length + " B)");
            return n;
        }
        public static List<PcAppearance> ParsePcAround(byte[] body) { int n = CheckedCount(body, PcAppearance.AppearanceSize, "PC_AROUND"); var l = new List<PcAppearance>(n); for (int i = 0; i < n; i++) l.Add(PcAppearance.ParseAppearance(body, 4 + i * PcAppearance.AppearanceSize)); return l; }
        public static List<NpcAppearance> ParseNpcAround(byte[] body) { int n = CheckedCount(body, NpcAppearance.Size, "NPC_AROUND"); var l = new List<NpcAppearance>(n); for (int i = 0; i < n; i++) l.Add(ParseNpc(body, 4 + i * NpcAppearance.Size)); return l; }
        public static List<int> ParseIdList(byte[] body, string what) { int n = CheckedCount(body, 4, what); var l = new List<int>(n); for (int i = 0; i < n; i++) l.Add(I(body, 4 + 4 * i)); return l; }
    }
}
