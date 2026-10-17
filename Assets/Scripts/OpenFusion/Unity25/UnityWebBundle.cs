// Contenedor "UnityWeb" (Unity 2.x/3.x): cabecera big-endian + bloque LZMA con directorio y archivos.
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFusion.Unity25
{
    public sealed class UnityWebBundle
    {
        public string Signature, PlayerVersion, EngineVersion; public uint Version;
        public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();

        static uint U32(byte[] b, int p) => (uint)(b[p] << 24 | b[p + 1] << 16 | b[p + 2] << 8 | b[p + 3]);
        static string Str(byte[] b, ref int p) { int s = p; while (b[p] != 0) p++; var r = Encoding.UTF8.GetString(b, s, p - s); p++; return r; }

        public static UnityWebBundle Parse(byte[] b)
        {
            var o = new UnityWebBundle(); int p = 0;
            o.Signature = Str(b, ref p); o.Version = U32(b, p); p += 4; o.PlayerVersion = Str(b, ref p); o.EngineVersion = Str(b, ref p);
            if (o.Signature != "UnityWeb" && o.Signature != "UnityRaw") throw new NotSupportedException("firma " + o.Signature);
            if (o.Version >= 4) p += 20;
            p += 4; int headerSize = (int)U32(b, p); p += 4; p += 4; int levels = (int)U32(b, p); p += 4; p += 8 * (levels - 1);
            int compSize = (int)U32(b, p); p += 4; p += 4; /* uncompressed */ if (o.Version >= 2) p += 4; if (o.Version >= 3) p += 4;
            byte[] dir;
            if (o.Signature == "UnityWeb")
            {
                var props = new byte[5]; Array.Copy(b, headerSize, props, 0, 5);
                long usize = BitConverter.ToInt64(b, headerSize + 5);
                dir = Lzma.Decode(b, headerSize + 13, props, usize);
            }
            else { dir = new byte[compSize]; Array.Copy(b, headerSize, dir, 0, compSize); }
            int n = (int)U32(dir, 0); int q = 4;
            for (int i = 0; i < n; i++)
            {
                string name = Str(dir, ref q); int off = (int)U32(dir, q), size = (int)U32(dir, q + 4); q += 8;
                var f = new byte[size]; Array.Copy(dir, off, f, 0, size); o.Files[name] = f;
            }
            return o;
        }
    }
}
