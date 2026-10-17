// Decodificación de PackedBitVector de Unity 2.5 (mallas comprimidas y curvas de rotación comprimidas).
// Port de tools/ff25anim.py (validado: 3933 curvas con ||q|-1|=0, 6/6 mallas con huesos).
using System;

namespace OpenFusion.Convert
{
    public sealed class PackedBits
    {
        public byte[] Data; public int NumItems, BitSize; public float Range, Start;
    }

    public static class PackedVectors
    {
        sealed class Cur { public int Idx, Bit; }

        static uint Bits(byte[] d, int nbits, Cur c)
        {
            uint x = 0; int bits = 0;
            while (bits < nbits)
            {
                x |= (uint)(d[c.Idx] >> c.Bit) << bits;
                int num = Math.Min(nbits - bits, 8 - c.Bit); c.Bit += num; bits += num;
                if (c.Bit == 8) { c.Idx++; c.Bit = 0; }
            }
            return nbits >= 32 ? x : x & ((1u << nbits) - 1);
        }

        public static uint[] UnpackInts(PackedBits v)
        {
            var o = new uint[v.NumItems]; if (v.BitSize == 0) return o;
            var c = new Cur(); for (int i = 0; i < o.Length; i++) o[i] = Bits(v.Data, v.BitSize, c); return o;
        }

        public static float[] UnpackFloats(PackedBits v)
        {
            var o = new float[v.NumItems];
            if (v.BitSize == 0) { for (int i = 0; i < o.Length; i++) o[i] = v.Start; return o; }
            float scale = v.Range != 0 ? 1f / v.Range : 0f; uint mx = v.BitSize >= 32 ? uint.MaxValue : (1u << v.BitSize) - 1;
            var c = new Cur();
            for (int i = 0; i < o.Length; i++)
            {
                uint x = Bits(v.Data, v.BitSize, c);
                o[i] = scale != 0 ? x / (scale * mx) + v.Start : v.Start;
            }
            return o;
        }

        /// Cuaterniones comprimidos: 3 bits de flags (2 = componente omitida, 1 = signo), 9/10 bits por componente.
        public static float[][] UnpackQuats(byte[] data, int n)
        {
            var o = new float[n][]; var c = new Cur();
            for (int i = 0; i < n; i++)
            {
                uint flags = Bits(data, 3, c); var q = new float[4]; float s = 0;
                for (int j = 0; j < 4; j++)
                {
                    if ((flags & 3) != j)
                    {
                        int bs = ((flags & 3) + 1) % 4 == j ? 9 : 10;
                        uint x = Bits(data, bs, c); q[j] = x / (0.5f * ((1 << bs) - 1)) - 1f; s += q[j] * q[j];
                    }
                }
                int last = (int)(flags & 3); q[last] = (float)Math.Sqrt(Math.Max(0f, 1f - s));
                if ((flags & 4) != 0) q[last] = -q[last];
                o[i] = q;
            }
            return o;
        }

        /// Curva de rotación comprimida -> tiempos (s; deltas en 1/100 s acumulados), quats, pendientes.
        public static void DecodeCurve(PackedBits times, byte[] qdata, int qn, PackedBits slopes,
            out float[] t, out float[][] q, out float[] s)
        {
            var dt = UnpackInts(times); t = new float[dt.Length]; long acc = 0;
            for (int i = 0; i < dt.Length; i++) { acc += dt[i]; t[i] = acc * 0.01f; }
            q = UnpackQuats(qdata, qn); s = UnpackFloats(slopes);
        }
    }
}
