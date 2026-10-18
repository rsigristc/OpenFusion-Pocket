// Lectura de las estructuras de red del avatar del jugador (sPCStyle, sPCAppearanceData, y el trozo de sPCLoadData2CL que lo describe).
// Disposición = Marshal Pack 4 del cliente original (cadenas UTF-16 de longitud fija). Puro; sin Unity.
using System;
using System.Collections.Generic;
using System.Text;
using OpenFusion.World;

namespace OpenFusion.Net
{
    public struct ItemBase { public short Type, Id; public int Opt, TimeLimit; }

    public enum PcState : sbyte { Unknown = -1, Idle = 0, Moving = 1, Combat = 2, Sitting = 3, StreetStore = 4, Vehicle = 8 }

    public sealed class PcAppearance
    {
        public int Id; public long Uid; public string FirstName, LastName; public bool NameChecked;
        public PlayerStyle Style; public int Class;
        public int Level, Hp, MapNum, X, Y, Z, Angle; public ItemBase[] Equip = new ItemBase[9];
        public short NanoId = -1;          // sólo en sPCAppearanceData
        public int FusionMatter;           // sólo en sPCLoadData2CL (entra en la clave de sesión del game server)
        public PcState State; public int ConditionBitFlag;   // sólo en sPCAppearanceData

        public const int StyleSize = 76, AppearanceSize = 232, LoadDataSize = 2552;

        static string Str(byte[] b, int o, int chars) { var s = Encoding.Unicode.GetString(b, o, chars * 2); int z = s.IndexOf('\0'); return z >= 0 ? s.Substring(0, z) : s; }

        /// sPCStyle (76 bytes) en la posición o.
        public static void ReadStyle(byte[] b, int o, PcAppearance a)
        {
            a.Uid = BitConverter.ToInt64(b, o); a.NameChecked = (sbyte)b[o + 8] == 1; a.FirstName = Str(b, o + 10, 9); a.LastName = Str(b, o + 28, 17);
            a.Style = new PlayerStyle { Gender = (sbyte)b[o + 62], FaceStyle = (sbyte)b[o + 63], HairStyle = (sbyte)b[o + 64], HairColor = (sbyte)b[o + 65], SkinColor = (sbyte)b[o + 66], EyeColor = (sbyte)b[o + 67], Height = (sbyte)b[o + 68], Body = (sbyte)b[o + 69] };
            a.Class = BitConverter.ToInt32(b, o + 72);
        }
        static void ReadEquip(byte[] b, int o, PcAppearance a) { for (int i = 0; i < 9; i++) { int p = o + i * 12; a.Equip[i] = new ItemBase { Type = BitConverter.ToInt16(b, p), Id = BitConverter.ToInt16(b, p + 2), Opt = BitConverter.ToInt32(b, p + 4), TimeLimit = BitConverter.ToInt32(b, p + 8) }; } }

        /// sPCAppearanceData (232 bytes): jugadores que entran en el campo de visión (PC_NEW, PC_NEW_LIST...).
        public static PcAppearance ParseAppearance(byte[] b, int o = 0)
        {
            if (b.Length - o < AppearanceSize) throw new ArgumentException("sPCAppearanceData necesita 232 bytes");
            var a = new PcAppearance { Id = BitConverter.ToInt32(b, o) }; a.ConditionBitFlag = BitConverter.ToInt32(b, o + 80); a.State = (PcState)(sbyte)b[o + 84];
            ReadStyle(b, o + 4, a);
            a.Level = BitConverter.ToInt16(b, o + 86); a.Hp = BitConverter.ToInt32(b, o + 88); a.MapNum = BitConverter.ToInt32(b, o + 92);
            a.X = BitConverter.ToInt32(b, o + 96); a.Y = BitConverter.ToInt32(b, o + 100); a.Z = BitConverter.ToInt32(b, o + 104); a.Angle = BitConverter.ToInt32(b, o + 108);
            ReadEquip(b, o + 112, a); a.NanoId = BitConverter.ToInt16(b, o + 220);
            return a;
        }

        /// sPCLoadData2CL (2552 bytes): el propio jugador al entrar (REP_PC_ENTER_SUCC). Sólo se leen los campos de aspecto y posición.
        public static PcAppearance ParseLoadData(byte[] b, int o = 0)
        {
            if (b.Length - o < LoadDataSize) throw new ArgumentException("sPCLoadData2CL necesita 2552 bytes");
            var a = new PcAppearance(); ReadStyle(b, o + 4, a);
            a.Level = BitConverter.ToInt16(b, o + 84); a.FusionMatter = BitConverter.ToInt32(b, o + 108); a.Hp = BitConverter.ToInt32(b, o + 92); a.MapNum = BitConverter.ToInt32(b, o + 116);
            a.X = BitConverter.ToInt32(b, o + 120); a.Y = BitConverter.ToInt32(b, o + 124); a.Z = BitConverter.ToInt32(b, o + 128); a.Angle = BitConverter.ToInt32(b, o + 132);
            ReadEquip(b, o + 136, a); return a;
        }

        /// Ids visibles de los objetos equipados. Con "combine" (localización que lo active) un objeto puede mostrar otro aspecto en (iOpt >> 16) & 0xFFFF.
        public PlayerEquip VisibleEquip(bool combine = true)
        {
            var ids = new int[9]; for (int i = 0; i < 9; i++) { int cosmetic = (Equip[i].Opt >> 16) & 0xFFFF; ids[i] = combine && i <= 3 && cosmetic > 0 ? cosmetic : Equip[i].Id; }
            return PlayerEquip.FromItemEquip(ids);
        }
    }
}
