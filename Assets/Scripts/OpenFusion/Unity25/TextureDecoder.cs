// Texture2D de Unity 2.5 -> RGBA32 (filas en el orden de Unity: de abajo hacia arriba).
// Formatos vistos en Retrobution: DXT5(12) ~84%, DXT1(10), DXT3(11), RGBA32(4), RGB24(3), ARGB32(5), ARGB4444(2).
using System;

namespace OpenFusion.Unity25
{
    public static class TextureDecoder
    {
        public static byte[] ToRGBA32(int w, int h, int format, byte[] d)
        {
            var o = new byte[w * h * 4];
            switch (format)
            {
                case 10: case 11: case 12: Dxt(w, h, format, d, o); break;
                case 1: for (int i = 0; i < w * h; i++) { o[4*i] = 255; o[4*i+1] = 255; o[4*i+2] = 255; o[4*i+3] = d[i]; } break;   // Alpha8
                case 4: Array.Copy(d, o, o.Length); break;
                case 5: for (int i = 0; i < w * h; i++) { o[4*i] = d[4*i+1]; o[4*i+1] = d[4*i+2]; o[4*i+2] = d[4*i+3]; o[4*i+3] = d[4*i]; } break;
                case 3: for (int i = 0; i < w * h; i++) { o[4*i] = d[3*i]; o[4*i+1] = d[3*i+1]; o[4*i+2] = d[3*i+2]; o[4*i+3] = 255; } break;
                case 2: for (int i = 0; i < w * h; i++) { int v = d[2*i] | d[2*i+1] << 8; o[4*i] = (byte)(((v >> 8) & 15) * 17); o[4*i+1] = (byte)(((v >> 4) & 15) * 17); o[4*i+2] = (byte)((v & 15) * 17); o[4*i+3] = (byte)(((v >> 12) & 15) * 17); } break;
                case 7: for (int i = 0; i < w * h; i++) { int v = d[2*i] | d[2*i+1] << 8; Rgb565(v, o, 4*i); o[4*i+3] = 255; } break;   // RGB565 (TextureFormat 7)
                case 13: for (int i = 0; i < w * h; i++) { int v = d[2*i] | d[2*i+1] << 8; o[4*i] = (byte)(((v >> 12) & 15) * 17); o[4*i+1] = (byte)(((v >> 8) & 15) * 17); o[4*i+2] = (byte)(((v >> 4) & 15) * 17); o[4*i+3] = (byte)((v & 15) * 17); } break;   // RGBA4444
                default: throw new NotSupportedException("formato de textura " + format);
            }
            return o;
        }

        static void Rgb565(int c, byte[] dst, int i) { int r = (c >> 11) & 31, g = (c >> 5) & 63, b = c & 31; dst[i] = (byte)(r << 3 | r >> 2); dst[i+1] = (byte)(g << 2 | g >> 4); dst[i+2] = (byte)(b << 3 | b >> 2); }

        static void Dxt(int w, int h, int fmt, byte[] d, byte[] o)
        {
            int bw = (w + 3) / 4, bh = (h + 3) / 4, p = 0; var pal = new byte[16]; var al = new byte[8]; var px = new byte[4 * 16];
            int bs = fmt == 10 ? 8 : 16;
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++, p += bs)
                {
                    int cp = fmt == 10 ? p : p + 8;
                    int c0 = d[cp] | d[cp + 1] << 8, c1 = d[cp + 2] | d[cp + 3] << 8;
                    Rgb565(c0, pal, 0); Rgb565(c1, pal, 4); pal[3] = pal[7] = pal[11] = pal[15] = 255;
                    bool four = fmt != 10 || c0 > c1;
                    for (int k = 0; k < 3; k++)
                    {
                        if (four) { pal[8 + k] = (byte)((2 * pal[k] + pal[4 + k]) / 3); pal[12 + k] = (byte)((pal[k] + 2 * pal[4 + k]) / 3); }
                        else { pal[8 + k] = (byte)((pal[k] + pal[4 + k]) / 2); pal[12 + k] = 0; }
                    }
                    // DXT1 de Unity es RGB sin alfa: el índice 3 en modo de 3 colores es negro OPACO
                    uint bits = BitConverter.ToUInt32(d, cp + 4);
                    for (int j = 0; j < 16; j++) { int ci = (int)((bits >> (2 * j)) & 3); Array.Copy(pal, ci * 4, px, j * 4, 4); }
                    if (fmt == 11) for (int j = 0; j < 16; j++) px[j * 4 + 3] = (byte)(((d[p + (j >> 1)] >> (4 * (j & 1))) & 15) * 17);
                    else if (fmt == 12)
                    {
                        int a0 = d[p], a1 = d[p + 1];
                        al[0] = (byte)a0; al[1] = (byte)a1;
                        if (a0 > a1) for (int k = 1; k < 7; k++) al[k + 1] = (byte)(((7 - k) * a0 + k * a1) / 7);
                        else { for (int k = 1; k < 5; k++) al[k + 1] = (byte)(((5 - k) * a0 + k * a1) / 5); al[6] = 0; al[7] = 255; }
                        ulong ab = 0; for (int k = 0; k < 6; k++) ab |= (ulong)d[p + 2 + k] << (8 * k);
                        for (int j = 0; j < 16; j++) px[j * 4 + 3] = al[(int)((ab >> (3 * j)) & 7)];
                    }
                    for (int j = 0; j < 16; j++)
                    {
                        int x = bx * 4 + (j & 3), y = by * 4 + (j >> 2); if (x >= w || y >= h) continue;
                        Array.Copy(px, j * 4, o, (y * w + x) * 4, 4);
                    }
                }
        }
    }
}
