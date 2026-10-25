// Paquetes que el cliente ENVÍA (login server y game server) y las respuestas del login server. Puro; sin Unity.
// Disposición = Marshal Pack 4 del cliente original (cadenas UTF-16 de longitud fija). Cada Build devuelve tipo (4 B) + cuerpo, listo para FramedStream.SendAsync.
using System;
using System.Collections.Generic;
using System.Text;
using OpenFusion.World;

namespace OpenFusion.Net
{
    public enum LoginError { DbError = 0, UnknownId = 1, WrongPassword = 2, AlreadyLoggedIn = 3, Suspended = 4, Other = 5, OutdatedClient = 6, NotBetaTester = 7, AuthConnection = 8, EulaRequired = 9 }

    public sealed class LoginSuccess { public int CharCount, SlotNum, PaymentFlag; public ulong SvrTime; public string Id; public int OpenBetaFlag; }
    public sealed class CharInfo {
        public int Slot, Level; public PcAppearance Appearance; public int X, Y, Z;
        public bool WorldPositionKnown = true;
        public int TutorialFlag = -1; // sPCStyle2; unknown is never treated as a new character
        public bool NeedsCustomization => Appearance != null && (Appearance.Style.Gender < 1 || Appearance.Style.Gender > 2 || Appearance.Style.FaceStyle <= 0 || Appearance.Style.HairStyle <= 0);
    }   // Appearance: Uid, nombre, estilo, equipo (aEquip)
    public struct ShardTicket { public string Host; public int Port; public long EnterSerialKey; }
    public sealed class NameSaved { public long Uid; public int Slot, Gender; public string FirstName, LastName; }

    public static class ClientPackets
    {
        public const int LoginSize = 468, LoginSuccSize = 84, LoginFailSize = 72, CharInfoSize = 204, ShardSelectSuccSize = 28, PcEnterSize = 80, CharCreateSize = 100, SaveNameSize = 68, CheckNameSize = 64, SaveNameSuccSize = 64;

        // ---- utilidades ----
        public static byte[] New(uint type, int bodySize) { var b = new byte[4 + bodySize]; BitConverter.GetBytes(type).CopyTo(b, 0); return b; }
        public static void PutStr(byte[] b, int o, string s, int chars)
        {
            // Marshal ByValTStr(Unicode): como máximo chars-1 caracteres y terminador
            s = s ?? ""; if (s.Length > chars - 1) s = s.Substring(0, chars - 1); Encoding.Unicode.GetBytes(s).CopyTo(b, o);
        }
        static void PutI(byte[] b, int o, int v) => BitConverter.GetBytes(v).CopyTo(b, o);
        public static void PutInt(byte[] b, int o, int v) { PutI(b, o, v); }
        static void PutL(byte[] b, int o, long v) => BitConverter.GetBytes(v).CopyTo(b, o);
        static void PutS(byte[] b, int o, short v) => BitConverter.GetBytes(v).CopyTo(b, o);
        static string Str(byte[] b, int o, int chars) { var s = Encoding.Unicode.GetString(b, o, chars * 2); int z = s.IndexOf('\0'); return z >= 0 ? s.Substring(0, z) : s; }
        static void Need(byte[] b, int n, string w) { if (b.Length < n) throw new ArgumentException(w + " necesita " + n + " bytes, hay " + b.Length); }

        // ---- login server: cliente -> servidor ----
        /// iLoginType 1 = usuario/contraseña. iClientVerA/B/C: 1/0/44 por defecto en el cliente original; C además entra en la clave FEKey.
        public static byte[] LoginPassword(string id, string password, int verA = 1, int verB = 0, int verC = 44)
        {
            var p = New(PacketIds.P_CL2LS_REQ_LOGIN, LoginSize); int o = 4;
            PutStr(p, o, id, 33); PutStr(p, o + 66, password, 33); PutI(p, o + 132, verA); PutI(p, o + 136, verB); PutI(p, o + 140, verC); PutI(p, o + 144, 1); return p;
        }
        /// iLoginType 2 = cookie web (tokenLogin del original): szID y szPassword = " ", szCookie_TEGid y szCookie_authid en ANSI.
        public static byte[] LoginCookie(string tegId, string authId, int verA = 1, int verB = 0, int verC = 44)
        {
            var p = New(PacketIds.P_CL2LS_REQ_LOGIN, LoginSize); int o = 4;
            PutStr(p, o, " ", 33); PutStr(p, o + 66, " ", 33); PutI(p, o + 132, verA); PutI(p, o + 136, verB); PutI(p, o + 140, verC); PutI(p, o + 144, 2);
            var t = Encoding.ASCII.GetBytes(tegId ?? ""); Array.Copy(t, 0, p, o + 148, Math.Min(t.Length, 63));
            var a = Encoding.ASCII.GetBytes(authId ?? ""); Array.Copy(a, 0, p, o + 212, Math.Min(a.Length, 254)); return p;
        }
        public static byte[] CharDelete(long uid) { var p = New(PacketIds.P_CL2LS_REQ_CHAR_DELETE, 8); PutL(p, 4, uid); return p; }
        public static byte[] CharSelect(long uid) { var p = New(PacketIds.P_CL2LS_REQ_CHAR_SELECT, 8); PutL(p, 4, uid); return p; }
        public static byte[] ShardSelect(int shard) { var p = New(PacketIds.P_CL2LS_REQ_SHARD_SELECT, 1); p[4] = (byte)shard; return p; }
        public static byte[] ShardListInfo() => New(PacketIds.P_CL2LS_REQ_SHARD_LIST_INFO, 0);
        public static byte[] LiveCheckLs(int v = 0) { var p = New(PacketIds.P_CL2LS_REP_LIVE_CHECK, 4); PutI(p, 4, v); return p; }
        public static byte[] ExitDuplicate(string id, string password) { var p = New(PacketIds.P_CL2LS_REQ_PC_EXIT_DUPLICATE, 132); PutStr(p, 4, id, 33); PutStr(p, 70, password, 33); return p; }
        public static byte[] CheckCharName(int fnCode, int lnCode, int mnCode, string first, string last)
        { var p = New(PacketIds.P_CL2LS_REQ_CHECK_CHAR_NAME, CheckNameSize); PutI(p, 4, fnCode); PutI(p, 8, lnCode); PutI(p, 12, mnCode); PutStr(p, 16, first, 9); PutStr(p, 34, last, 17); return p; }
        public static byte[] SaveCharName(int slot, int gender, int fnCode, int lnCode, int mnCode, string first, string last)
        { var p = New(PacketIds.P_CL2LS_REQ_SAVE_CHAR_NAME, SaveNameSize); p[4] = (byte)slot; p[5] = (byte)gender; PutI(p, 8, fnCode); PutI(p, 12, lnCode); PutI(p, 16, mnCode); PutStr(p, 20, first, 9); PutStr(p, 38, last, 17); return p; }
        public static byte[] SaveCharTutor(long uid, int tutorialFlag) { var p = New(PacketIds.P_CL2LS_REQ_SAVE_CHAR_TUTOR, 12); PutL(p, 4, uid); p[12] = (byte)tutorialFlag; return p; }

        /// Creación del personaje: sPCStyle (con el iPC_UID de SAVE_CHAR_NAME_SUCC) + sOnItem (ids de camisa, pantalón y zapatos) + sOnItem_Index (índices elegidos en la pantalla).
        public static byte[] CharCreate(long uid, PlayerStyle st, int shirtId, int pantsId, int shoesId, int shirtIdx, int pantsIdx, int shoesIdx, int @class = 0, int faceIdx = -1, int hairIdx = -1, NameSaved reserved = null)
        {
            var p = New(PacketIds.P_CL2LS_REQ_CHAR_CREATE, CharCreateSize); int o = 4;
            if (reserved != null) { PutStr(p, o + 10, reserved.FirstName, 9); PutStr(p, o + 28, reserved.LastName, 17); }
            PutL(p, o, uid); p[o + 62] = (byte)st.Gender; p[o + 63] = (byte)st.FaceStyle; p[o + 64] = (byte)st.HairStyle; p[o + 65] = (byte)st.HairColor; p[o + 66] = (byte)st.SkinColor; p[o + 67] = (byte)st.EyeColor; p[o + 68] = (byte)st.Height; p[o + 69] = (byte)st.Body; PutI(p, o + 72, @class);
            int i = o + 76;   // sOnItem (Pack 2): Hand, UB, LB, Foot, Head, Face, Back
            PutS(p, i + 2, (short)shirtId); PutS(p, i + 4, (short)pantsId); PutS(p, i + 6, (short)shoesId);
            int x = i + 14;   // sOnItem_Index: UB, LB, Foot, FaceStyle, HairStyle
            PutS(p, x, (short)shirtIdx); PutS(p, x + 2, (short)pantsIdx); PutS(p, x + 4, (short)shoesIdx); PutS(p, x + 6, (short)(faceIdx >= 0 ? faceIdx : st.FaceStyle)); PutS(p, x + 8, (short)(hairIdx >= 0 ? hairIdx : st.HairStyle)); return p;   // original: iFaceStyleSelected / iHairStyleSelected (índice de fila 2..Max+1)
        }

        // ---- login server: servidor -> cliente ----
        public static LoginSuccess ParseLoginSucc(byte[] b)
        {
            Need(b, LoginSuccSize, "sP_LS2CL_REP_LOGIN_SUCC");
            return new LoginSuccess { CharCount = (sbyte)b[0], SlotNum = (sbyte)b[1], PaymentFlag = (sbyte)b[2], SvrTime = BitConverter.ToUInt64(b, 4), Id = Str(b, 12, 33), OpenBetaFlag = BitConverter.ToInt32(b, 80) };
        }
        public static int ParseErrorCode(byte[] b, string what) { Need(b, 4, what); return BitConverter.ToInt32(b, 0); }
        public static CharInfo ParseCharInfo(byte[] b)
        {
            Need(b, CharInfoSize, "sP_LS2CL_REP_CHAR_INFO");
            var a = new PcAppearance(); PcAppearance.ReadStyle(b, 4, a);
            for (int i = 0; i < 9; i++) { int p = 96 + i * 12; a.Equip[i] = new ItemBase { Type = BitConverter.ToInt16(b, p), Id = BitConverter.ToInt16(b, p + 2), Opt = BitConverter.ToInt32(b, p + 4), TimeLimit = BitConverter.ToInt32(b, p + 8) }; }
            a.Level = BitConverter.ToInt16(b, 2); a.X = BitConverter.ToInt32(b, 84); a.Y = BitConverter.ToInt32(b, 88); a.Z = BitConverter.ToInt32(b, 92);
            return new CharInfo { Slot = (sbyte)b[0], Level = a.Level, Appearance = a, X = a.X, Y = a.Y, Z = a.Z, TutorialFlag = (sbyte)b[81] };
        }
        public static ShardTicket ParseShardSelectSucc(byte[] b)
        {
            Need(b, ShardSelectSuccSize, "sP_LS2CL_REP_SHARD_SELECT_SUCC");
            int n = Array.IndexOf(b, (byte)0, 0, 16); if (n < 0) n = 16;   // Encoding.Default.GetString del original: ASCII hasta el primer 0
            // El servidor rellena el campo con basura tras la IP (p.ej. "20.121.31.107 # "): nos quedamos con el prefijo válido de host.
            int h = 0; while (h < n && (char.IsLetterOrDigit((char)b[h]) || b[h] == (byte)'.' || b[h] == (byte)'-')) h++;
            return new ShardTicket { Host = Encoding.ASCII.GetString(b, 0, h), Port = BitConverter.ToInt32(b, 16), EnterSerialKey = BitConverter.ToInt64(b, 20) };
        }
        public static string[] ParseCheckNameSucc(byte[] b) { Need(b, 52, "sP_LS2CL_REP_CHECK_CHAR_NAME_SUCC"); return new[] { Str(b, 0, 9), Str(b, 18, 17) }; }
        public static NameSaved ParseSaveNameSucc(byte[] b)
        { Need(b, SaveNameSuccSize, "sP_LS2CL_REP_SAVE_CHAR_NAME_SUCC"); return new NameSaved { Uid = BitConverter.ToInt64(b, 0), Slot = (sbyte)b[8], Gender = (sbyte)b[9], FirstName = Str(b, 10, 9), LastName = Str(b, 28, 17) }; }
        public static CharInfo ParseCharCreateSucc(byte[] b)
        {
            Need(b, 100, "sP_LS2CL_REP_CHAR_CREATE_SUCC"); var a = new PcAppearance(); PcAppearance.ReadStyle(b, 4, a); a.Level = BitConverter.ToInt16(b, 0);
            // sOnItem (Pack 2) en 84: Hand, UB, LB, Foot, Head, Face, Back
            a.Equip[0].Id = BitConverter.ToInt16(b, 84); a.Equip[1].Id = BitConverter.ToInt16(b, 86); a.Equip[2].Id = BitConverter.ToInt16(b, 88); a.Equip[3].Id = BitConverter.ToInt16(b, 90); a.Equip[4].Id = BitConverter.ToInt16(b, 92); a.Equip[5].Id = BitConverter.ToInt16(b, 94); a.Equip[6].Id = BitConverter.ToInt16(b, 96);
            // sOnItem contains IDs only; restore the equipment table types before previews use them.
            for (int i = 1; i <= 6; i++) { var item = a.Equip[i]; item.Type = (short)i; a.Equip[i] = item; }
            return new CharInfo { Slot = -1, Level = a.Level, Appearance = a, WorldPositionKnown = false, TutorialFlag = (sbyte)b[81] };
        }

        // ---- game server: cliente -> servidor ----
        public static byte[] PcEnter(string id, long enterSerialKey) { var p = New(PacketIds.P_CL2FE_REQ_PC_ENTER, PcEnterSize); PutStr(p, 4, id, 33); PutL(p, 4 + 72, enterSerialKey); return p; }
        public static byte[] LoadingComplete(int pcId = 0) { var p = New(PacketIds.P_CL2FE_REQ_PC_LOADING_COMPLETE, 4); PutI(p, 4, pcId); return p; }
        /// sP_CL2FE_REQ_PC_ATTACK_NPCs: iNPCCnt + int[] ids (cnAvatarAttack.AttackTarget).
        public static byte[] AttackNpcs(int[] ids) { var p = New(PacketIds.P_CL2FE_REQ_PC_ATTACK_NPCs, 4 + 4 * ids.Length); PutI(p, 4, ids.Length); for (int i = 0; i < ids.Length; i++) PutI(p, 8 + 4 * i, ids[i]); return p; }
        /// sP_CL2FE_REQ_PC_ROCKET_STYLE_FIRE (28 B): iSkillID, iX, iY, iZ (origen), iToX, iToY, iToZ (cnAvatarAttack.AttackTarget, modo 5). Coordenadas de servidor.
        public static byte[] RocketFire(int skillId, int x, int y, int z, int tx, int ty, int tz) { var p = New(PacketIds.P_CL2FE_REQ_PC_ROCKET_STYLE_FIRE, 28); PutI(p, 4, skillId); PutI(p, 8, x); PutI(p, 12, y); PutI(p, 16, z); PutI(p, 20, tx); PutI(p, 24, ty); PutI(p, 28, tz); return p; }
        /// sP_CL2FE_REQ_PC_ROCKET_STYLE_HIT (20 B: sbyte iBulletID, iX, iY, iZ, iTargetCnt) + por objetivo {iID, eCT (4 = NPC)} (cnWarHead.CrashEffect).
        /// sP_CL2FE_REQ_PC_GRENADE_STYLE_FIRE: iSkillID, iToX, iToY, iToZ (cnAvatarAttack.FireGrenade: destino = posición del jugador + dirección x alcance).
        public static byte[] GrenadeFire(int skillId, int tx, int ty, int tz) { var p = New(PacketIds.P_CL2FE_REQ_PC_GRENADE_STYLE_FIRE, 16); PutI(p, 4, skillId); PutI(p, 8, tx); PutI(p, 12, ty); PutI(p, 16, tz); return p; }
        public static byte[] RocketHit(sbyte bulletId, int x, int y, int z, int[] ids) { var p = New(PacketIds.P_CL2FE_REQ_PC_ROCKET_STYLE_HIT, 20 + 8 * ids.Length); p[4] = (byte)bulletId; PutI(p, 8, x); PutI(p, 12, y); PutI(p, 16, z); PutI(p, 20, ids.Length); for (int i = 0; i < ids.Length; i++) { PutI(p, 24 + 8 * i, ids[i]); PutI(p, 28 + 8 * i, 4); } return p; }
        /// sP_CL2FE_REQ_NANO_SKILL_USE (20 B: sbyte iBulletID, iArg1..3, iTargetCnt) + int por objetivo (cnAvatarAttack.NanoSkillUse).
        public static byte[] NanoSkillUse(sbyte bulletId, int arg1, int arg2, int arg3, int[] ids) { var p = New(PacketIds.P_CL2FE_REQ_NANO_SKILL_USE, 20 + 4 * ids.Length); p[4] = (byte)bulletId; PutI(p, 8, arg1); PutI(p, 12, arg2); PutI(p, 16, arg3); PutI(p, 20, ids.Length); for (int i = 0; i < ids.Length; i++) PutI(p, 24 + 4 * i, ids[i]); return p; }
        /// sP_CL2FE_REQ_SHINY_PICKUP: iShinyID (cnAvatarAttack.CheckShiny).
        public static byte[] ShinyPickup(int shinyId) { var p = New(PacketIds.P_CL2FE_REQ_SHINY_PICKUP, 4); PutI(p, 4, shinyId); return p; }
        // Original client: NanoCharge sends the PC ID; weapon boost uses SET_VALUE type 8 as a toggle.
        public static byte[] ChargeNano(int pcId) { var p = New(PacketIds.P_CL2FE_REQ_CHARGE_NANO_STAMINA, 4); PutI(p, 4, pcId); return p; }
        public static byte[] ToggleWeaponBoost(int pcId) { var p = New(PacketIds.P_CL2FE_GM_REQ_PC_SET_VALUE, 12); PutI(p, 4, pcId); PutI(p, 8, 8); return p; }
        public static byte[] CombatBegin(int pcId) { var p = New(PacketIds.P_CL2FE_REQ_PC_COMBAT_BEGIN, 4); PutI(p, 4, pcId); return p; }
        /// sP_CL2FE_REQ_PC_FIRST_USE_FLAG_SET { int iFlagCode } (cnFirstUseSysManager.CheckCondition)
        public static byte[] FirstUseFlagSet(int code) { var p = New(PacketIds.P_CL2FE_REQ_PC_FIRST_USE_FLAG_SET, 4); PutI(p, 4, code); return p; }
        public static byte[] CombatEnd(int pcId) { var p = New(PacketIds.P_CL2FE_REQ_PC_COMBAT_END, 4); PutI(p, 4, pcId); return p; }
        /// sP_CL2FE_REQ_ITEM_MOVE: eFrom, iFromSlotNum, eTo, iToSlotNum (eFrom/eTo 0 = equipo). TAB cambia el arma: ranura 0 <-> 7.
        public static int LastMoveEf, LastMoveFs, LastMoveEt, LastMoveTs;   // ultima peticion ITEM_MOVE (MoveAck corrige la respuesta de apilado entre inventario y banco)
        public static byte[] ItemMove(int eFrom, int fromSlot, int eTo, int toSlot) { LastMoveEf = eFrom; LastMoveFs = fromSlot; LastMoveEt = eTo; LastMoveTs = toSlot; var p = New(PacketIds.P_CL2FE_REQ_ITEM_MOVE, 16); PutI(p, 4, eFrom); PutI(p, 8, fromSlot); PutI(p, 12, eTo); PutI(p, 16, toSlot); return p; }
        public static byte[] WarpUseNpc(int npcId, int warpId, int itemSlot1 = 0, int itemSlot2 = 0) { var p = New(PacketIds.P_CL2FE_REQ_PC_WARP_USE_NPC, 24); PutI(p, 4, npcId); PutI(p, 8, warpId); PutI(p, 12, 4); PutI(p, 16, itemSlot1); PutI(p, 20, 4); PutI(p, 24, itemSlot2); return p; }
        public static byte[] LiveCheckFe(int v) { var p = New(PacketIds.P_CL2FE_REP_LIVE_CHECK, 4); PutI(p, 4, v); return p; }
        public static byte[] Move(ulong cliTime, int x, int y, int z, float vx, float vy, float vz, int angle, byte key, int speed)
        {
            var p = New(PacketIds.P_CL2FE_REQ_PC_MOVE, 44); BitConverter.GetBytes(cliTime).CopyTo(p, 4); PutI(p, 12, x); PutI(p, 16, y); PutI(p, 20, z);
            BitConverter.GetBytes(vx).CopyTo(p, 24); BitConverter.GetBytes(vy).CopyTo(p, 28); BitConverter.GetBytes(vz).CopyTo(p, 32); PutI(p, 36, angle); p[40] = key; PutI(p, 44, speed); return p;
        }
        public static byte[] Stop(ulong cliTime, int x, int y, int z) { var p = New(PacketIds.P_CL2FE_REQ_PC_STOP, 20); BitConverter.GetBytes(cliTime).CopyTo(p, 4); PutI(p, 12, x); PutI(p, 16, y); PutI(p, 20, z); return p; }
        public static byte[] Jump(ulong cliTime, int x, int y, int z, int vx, int vy, int vz, int angle, byte key, int speed)
        {
            var p = New(PacketIds.P_CL2FE_REQ_PC_JUMP, 44); BitConverter.GetBytes(cliTime).CopyTo(p, 4); PutI(p, 12, x); PutI(p, 16, y); PutI(p, 20, z);
            PutI(p, 24, vx); PutI(p, 28, vy); PutI(p, 32, vz); PutI(p, 36, angle); p[40] = key; PutI(p, 44, speed); return p;
        }
    }
}
