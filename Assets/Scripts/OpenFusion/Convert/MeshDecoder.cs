// Mallas de Unity 2.5: descompresión de CompressedMesh y conversión de índices/tri-strips a listas de triángulos.
using System;
using System.Collections.Generic;

namespace OpenFusion.Convert
{
    public sealed class CompressedMeshData
    {
        public PackedBits Vertices, Normals, NormalSigns, UV, Triangles, Weights, BoneIndices;
    }
    public sealed class DecodedMesh
    {
        public float[] Positions;          // xyz
        public float[] Normals;            // xyz (puede ser null)
        public float[] UVs;                // uv (puede ser null)
        public uint[] Indices;
        public int[] BoneIdx;              // 4 por vértice (null si no hay skin)
        public float[] BoneW;              // 4 por vértice
        public int BoneIndicesConsumed, BoneIndicesTotal;
    }

    public static class MeshDecoder
    {
        static bool Has(PackedBits p) => p != null && p.NumItems > 0;

        public static DecodedMesh Decompress(CompressedMeshData cm)
        {
            var r = new DecodedMesh();
            r.Positions = PackedVectors.UnpackFloats(cm.Vertices);
            int nv = r.Positions.Length / 3;
            if (Has(cm.Normals))
            {
                var nd = PackedVectors.UnpackFloats(cm.Normals); var sg = PackedVectors.UnpackInts(cm.NormalSigns);
                r.Normals = new float[sg.Length * 3];
                for (int i = 0; i < sg.Length; i++)
                {
                    float x = nd[2 * i], y = nd[2 * i + 1], zs = 1 - x * x - y * y;
                    float z = zs >= 0 ? (float)Math.Sqrt(zs) : 0f; if (sg[i] == 0) z = -z;
                    r.Normals[3 * i] = x; r.Normals[3 * i + 1] = y; r.Normals[3 * i + 2] = z;
                }
            }
            if (Has(cm.UV))
            {
                var uv = PackedVectors.UnpackFloats(cm.UV); int n = Math.Min(nv, uv.Length / 2);
                r.UVs = new float[n * 2]; Array.Copy(uv, r.UVs, n * 2);
            }
            r.Indices = PackedVectors.UnpackInts(cm.Triangles);
            if (Has(cm.Weights))
            {
                var wt = PackedVectors.UnpackInts(cm.Weights); var bi = PackedVectors.UnpackInts(cm.BoneIndices);
                var idx = new List<int>(); var w = new List<float>();
                var ci = new int[4]; var cw = new float[4]; int j = 0, bp = 0; uint s = 0;
                foreach (var x in wt)
                {
                    cw[j] = x / 31f; ci[j] = (int)bi[bp++]; j++; s += x;
                    if (s >= 31)
                    {
                        for (int k = j; k < 4; k++) { cw[k] = 0; ci[k] = 0; }
                        idx.AddRange(ci); w.AddRange(cw); Array.Clear(ci, 0, 4); Array.Clear(cw, 0, 4); j = 0; s = 0;
                    }
                    else if (j == 3)
                    {
                        cw[3] = (31 - s) / 31f; ci[3] = (int)bi[bp++];
                        idx.AddRange(ci); w.AddRange(cw); Array.Clear(ci, 0, 4); Array.Clear(cw, 0, 4); j = 0; s = 0;
                    }
                }
                r.BoneIdx = idx.ToArray(); r.BoneW = w.ToArray(); r.BoneIndicesConsumed = bp; r.BoneIndicesTotal = bi.Length;
            }
            return r;
        }

        /// Convierte un submesh (índices crudos de 16/32 bits) en lista de triángulos.
        public static int[] Triangles(byte[] indexBuffer, bool use16, int firstByte, int indexCount, bool isTriStrip)
        {
            int sz = use16 ? 2 : 4, first = firstByte / sz; var ii = new int[indexCount];
            for (int i = 0; i < indexCount; i++)
                ii[i] = use16 ? BitConverter.ToUInt16(indexBuffer, (first + i) * 2) : (int)BitConverter.ToUInt32(indexBuffer, (first + i) * 4);
            return ToTriangleList(ii, isTriStrip);
        }

        /// Índices (strip o lista) -> lista de triángulos; siempre devuelve un múltiplo de 3 (descarta restos y degenerados de strip).
        public static int[] ToTriangleList(int[] ii, bool isTriStrip)
        {
            var tris = new List<int>();
            if (isTriStrip)
            {
                for (int i = 0; i + 2 < ii.Length; i++)
                {
                    int a = ii[i], b = ii[i + 1], c = ii[i + 2];
                    if (a != b && b != c && a != c) { tris.Add(a); if (i % 2 == 0) { tris.Add(b); tris.Add(c); } else { tris.Add(c); tris.Add(b); } }
                }
            }
            else for (int i = 0; i + 2 < ii.Length; i += 3) { tris.Add(ii[i]); tris.Add(ii[i + 1]); tris.Add(ii[i + 2]); }
            return tris.ToArray();
        }
    }
}
