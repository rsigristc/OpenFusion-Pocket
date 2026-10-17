// Decodificador LZMA (alone/raw) compacto, basado en el algoritmo del LZMA SDK (dominio público).
using System;

namespace OpenFusion.Unity25
{
    public static class Lzma
    {
        const int kNumStates = 12, kNumPosBitsMax = 4, kNumLenToPosStates = 4, kEndPosModelIndex = 14;
        const int kMatchMinLen = 2;

        sealed class RC
        {
            readonly byte[] b; int p; uint range = 0xFFFFFFFF, code;
            public RC(byte[] buf, int pos) { b = buf; p = pos; for (int i = 0; i < 5; i++) code = (code << 8) | b[p++]; }
            public uint Bit(ushort[] probs, int i)
            {
                uint prob = probs[i], bound = (range >> 11) * prob;
                if (code < bound) { range = bound; probs[i] = (ushort)(prob + ((2048 - prob) >> 5)); if (range < (1u << 24)) { range <<= 8; code = (code << 8) | b[p++]; } return 0; }
                range -= bound; code -= bound; probs[i] = (ushort)(prob - (prob >> 5));
                if (range < (1u << 24)) { range <<= 8; code = (code << 8) | b[p++]; }
                return 1;
            }
            public uint Direct(int n)
            {
                uint r = 0;
                for (; n > 0; n--)
                {
                    range >>= 1; code -= range; uint t = 0 - (code >> 31); code += range & t; r = (r << 1) + (t + 1);
                    if (range < (1u << 24)) { range <<= 8; code = (code << 8) | b[p++]; }
                }
                return r;
            }
            public uint Tree(ushort[] pr, int off, int bits) { uint m = 1; for (int i = 0; i < bits; i++) m = (m << 1) + Bit(pr, off + (int)m); return m - (1u << bits); }
            public uint RevTree(ushort[] pr, int off, int bits)
            {
                uint m = 1, sym = 0;
                for (int i = 0; i < bits; i++) { uint bit = Bit(pr, off + (int)m); m = (m << 1) + bit; sym |= bit << i; }
                return sym;
            }
        }

        static ushort[] Init(int n) { var a = new ushort[n]; for (int i = 0; i < n; i++) a[i] = 1024; return a; }

        sealed class Len
        {
            ushort[] choice = Init(2), low = Init(16 << 3), mid = Init(16 << 3), high = Init(256);
            public uint Decode(RC rc, int posState)
            {
                if (rc.Bit(choice, 0) == 0) return rc.Tree(low, posState << 3, 3);
                if (rc.Bit(choice, 1) == 0) return 8 + rc.Tree(mid, posState << 3, 3);
                return 16 + rc.Tree(high, 0, 8);
            }
        }

        /// props: 5 bytes (lc/lp/pb + dict). data: bytes tras la cabecera. outSize conocido.
        public static byte[] Decode(byte[] data, int dataOffset, byte[] props, long outSize)
        {
            int d = props[0]; int lc = d % 9; d /= 9; int lp = d % 5; int pb = d / 5;
            var outb = new byte[outSize]; var rc = new RC(data, dataOffset);
            var lit = Init(0x300 << (lc + lp)); var isMatch = Init(kNumStates << kNumPosBitsMax); var isRep = Init(kNumStates);
            var isRepG0 = Init(kNumStates); var isRepG1 = Init(kNumStates); var isRepG2 = Init(kNumStates); var isRep0Long = Init(kNumStates << kNumPosBitsMax);
            var posSlot = Init(kNumLenToPosStates << 6); var posSpec = Init(115); var align = Init(16);
            var lenD = new Len(); var repLenD = new Len();
            int state = 0; uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0; long pos = 0;
            uint pbMask = (1u << pb) - 1, lpMask = (1u << lp) - 1;
            while (pos < outSize)
            {
                int posState = (int)((uint)pos & pbMask);
                if (rc.Bit(isMatch, (state << kNumPosBitsMax) + posState) == 0)
                {
                    uint prev = pos > 0 ? outb[pos - 1] : 0u;
                    int off = 0x300 * (int)(((((uint)pos & lpMask) << lc) + (prev >> (8 - lc))));
                    uint sym = 1;
                    if (state >= 7)
                    {
                        uint mb = outb[pos - rep0 - 1];
                        do
                        {
                            uint mbit = (mb >> 7) & 1; mb <<= 1; uint bit = rc.Bit(lit, off + (int)((1 + mbit) << 8) + (int)sym);
                            sym = (sym << 1) | bit; if (mbit != bit) break;
                        } while (sym < 0x100);
                    }
                    while (sym < 0x100) sym = (sym << 1) | rc.Bit(lit, off + (int)sym);
                    outb[pos++] = (byte)sym;
                    state = state < 4 ? 0 : state < 10 ? state - 3 : state - 6;
                    continue;
                }
                uint len;
                if (rc.Bit(isRep, state) != 0)
                {
                    if (pos == 0) throw new InvalidOperationException("LZMA: rep sin datos");
                    if (rc.Bit(isRepG0, state) == 0)
                    {
                        if (rc.Bit(isRep0Long, (state << kNumPosBitsMax) + posState) == 0)
                        { state = state < 7 ? 9 : 11; outb[pos] = outb[pos - rep0 - 1]; pos++; continue; }
                    }
                    else
                    {
                        uint dist;
                        if (rc.Bit(isRepG1, state) == 0) dist = rep1;
                        else { if (rc.Bit(isRepG2, state) == 0) dist = rep2; else { dist = rep3; rep3 = rep2; } rep2 = rep1; }
                        rep1 = rep0; rep0 = dist;
                    }
                    len = repLenD.Decode(rc, posState); state = state < 7 ? 8 : 11;
                }
                else
                {
                    rep3 = rep2; rep2 = rep1; rep1 = rep0;
                    len = lenD.Decode(rc, posState); state = state < 7 ? 7 : 10;
                    int lenState = (int)Math.Min(len, kNumLenToPosStates - 1);
                    uint slot = rc.Tree(posSlot, lenState << 6, 6);
                    if (slot < 4) rep0 = slot;
                    else
                    {
                        int nd = (int)(slot >> 1) - 1; rep0 = (2 | (slot & 1)) << nd;
                        if (slot < kEndPosModelIndex) rep0 += rc.RevTree(posSpec, (int)rep0 - (int)slot, nd);
                        else { rep0 += rc.Direct(nd - 4) << 4; rep0 += rc.RevTree(align, 0, 4); }
                    }
                    if (rep0 == 0xFFFFFFFF) break; // marcador de fin
                }
                len += kMatchMinLen;
                if (rep0 >= pos) throw new InvalidOperationException("LZMA: distancia inválida");
                for (uint i = 0; i < len && pos < outSize; i++) { outb[pos] = outb[pos - rep0 - 1]; pos++; }
            }
            return outb;
        }
    }
}
