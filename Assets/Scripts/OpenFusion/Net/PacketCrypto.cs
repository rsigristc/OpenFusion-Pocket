// Port fiel de csSocketManager (Encrypt/Decrypt) y cnNetworkManager.ReceiveDataSum
// del cliente Unity 2.5 de FusionFall. SIN VERIFICAR contra un servidor en vivo.
using System;

namespace OpenFusion.Net
{
    public sealed class PacketCrypto
    {
        const string DefaultKeyString = "!$a%b&~#b>$rT~!Qm@rQn~W#";
        const int DefaultERSize = 8;
        const uint GameServerTypeMin = 318767104;

        // iV estatico de cnNetworkManager
        const int IV = 268403508; // 0x0FFF8334

        public static readonly ulong DefaultKey = ComputeDefaultKey();
        public ulong EKey = DefaultKey;   // clave del login server
        public ulong FEKey;               // clave del game server (tras REP_LOGIN_SUCC)
        public bool GameServer;

        static ulong ComputeDefaultKey()
        {
            ulong k = 0;
            for (int i = 0; i < 8; i++) k |= (ulong)(byte)DefaultKeyString[8 + i] << (8 * i);
            return k;
        }

        public static ulong Create64BitKey(ulong time, int iv1, int iv2)
            => DefaultKey * (time * (ulong)(uint)(iv1 + 1) * (ulong)(uint)(iv2 + 1));

        public void ResetToLogin() { EKey = DefaultKey; GameServer = false; }

        // Checksum (cnNetworkManager.ReceiveDataSum). data[0..4) = tipo, no entra en la suma.
        public static uint DataSum(byte[] data, int size)
        {
            int num = 0, num2 = 0, num3 = IV >> 20;
            for (int i = IV & 0xF; i < size; i++)
            {
                num += data[i];
                num -= num3 * (num / num3);
                num2 += num;
                num2 -= num3 * (num2 / num3);
            }
            return (uint)(((num2 << ((IV >> 12) & 0xF)) | num) & (IV >> 16));
        }

        // type = ((type & 0xFF000FFF) | (sum << 12)) ^ (type & 0xFFF000)   (ver csSocketData/hook de envio)
        public static uint StampType(uint type, uint sum)
            => ((type & 0xFF000FFF) | (sum << 12)) ^ (type & 0xFFF000);

        public int Encrypt(byte[] data, int size)
        {
            GameServer = BitConverter.ToUInt32(data, 0) >= GameServerTypeMin;
            int er = size % (DefaultERSize / 2 + 1) * 2 + DefaultERSize;
            int s2 = Xor64(EKey, data, size);
            return ByteChange(er, data, s2);
        }

        public int Decrypt(byte[] data, int size)
        {
            int er = size % (DefaultERSize / 2 + 1) * 2 + DefaultERSize;
            int s2 = ByteChange(er, data, size);
            return Xor64(GameServer ? FEKey : EKey, data, s2);
        }

        static int Xor64(ulong key, byte[] data, int size)
        {
            int i = 0;
            for (; i + 8 <= size; i += 8)
            {
                ulong v = BitConverter.ToUInt64(data, i) ^ key;
                for (int b = 0; b < 8; b++) data[i + b] = (byte)(v >> (8 * b));
            }
            for (int b = 0; i + b < size; b++) data[i + b] ^= (byte)(key >> (8 * b));
            return size;
        }

        static int ByteChange(int er, byte[] data, int size)
        {
            int num = 0, num2 = 0;
            while (num + er <= size)
            {
                int a = num + num2, b = num + (er - 1 - num2);
                byte t = data[a]; data[a] = data[b]; data[b] = t;
                num += er; num2++;
                if (num2 > er / 2) num2 = 0;
            }
            return size;
        }
    }
}
