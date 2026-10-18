// Mesh25/Texture25 -> UnityEngine.Mesh/Texture2D. Depende de UnityEngine: SIN COMPILAR NI PROBAR aquí.
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public static class UnityConverters
    {
        public static Mesh ToMesh(Mesh25 m)
        {
            var mesh = new Mesh { name = m.Name };
            int n = m.Positions.Length / 3; if (n > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var p = new Vector3[n]; for (int i = 0; i < n; i++) p[i] = new Vector3(m.Positions[3*i], m.Positions[3*i+1], m.Positions[3*i+2]);
            mesh.vertices = p;
            if (m.Normals != null && m.Normals.Length == n * 3) { var a = new Vector3[n]; for (int i = 0; i < n; i++) a[i] = new Vector3(m.Normals[3*i], m.Normals[3*i+1], m.Normals[3*i+2]); mesh.normals = a; }
            if (m.UV != null && m.UV.Length == n * 2) { var a = new Vector2[n]; for (int i = 0; i < n; i++) a[i] = new Vector2(m.UV[2*i], m.UV[2*i+1]); mesh.uv = a; }
            if (m.UV1 != null && m.UV1.Length == n * 2) { var a = new Vector2[n]; for (int i = 0; i < n; i++) a[i] = new Vector2(m.UV1[2*i], m.UV1[2*i+1]); mesh.uv2 = a; }
            if (m.Tangents != null && m.Tangents.Length == n * 4) { var a = new Vector4[n]; for (int i = 0; i < n; i++) a[i] = new Vector4(m.Tangents[4*i], m.Tangents[4*i+1], m.Tangents[4*i+2], m.Tangents[4*i+3]); mesh.tangents = a; }
            if (m.BoneIdx != null && m.BoneW != null && m.BoneIdx.Length == n * 4)
            {
                var bw = new BoneWeight[n];
                for (int i = 0; i < n; i++) bw[i] = new BoneWeight { boneIndex0 = m.BoneIdx[4*i], boneIndex1 = m.BoneIdx[4*i+1], boneIndex2 = m.BoneIdx[4*i+2], boneIndex3 = m.BoneIdx[4*i+3],
                    weight0 = m.BoneW[4*i], weight1 = m.BoneW[4*i+1], weight2 = m.BoneW[4*i+2], weight3 = m.BoneW[4*i+3] };
                mesh.boneWeights = bw;
            }
            if (m.BindPose != null && m.BindPose.Length > 0)
            {
                var bp = new Matrix4x4[m.BindPose.Length];
                for (int i = 0; i < bp.Length; i++) for (int k = 0; k < 16; k++) bp[i][k / 4, k % 4] = m.BindPose[i][k];
                mesh.bindposes = bp;
            }
            mesh.subMeshCount = Math.Max(1, m.SubmeshTriangles.Length);
            for (int s = 0; s < m.SubmeshTriangles.Length; s++)
            {
                var t = m.SubmeshTriangles[s]; int len = t.Length - t.Length % 3; bool ok = true;
                for (int i = 0; i < len; i++) if ((uint)t[i] >= (uint)n) { ok = false; break; }
                if (!ok) { var f = new List<int>(len); for (int i = 0; i < len; i += 3) if ((uint)t[i] < (uint)n && (uint)t[i + 1] < (uint)n && (uint)t[i + 2] < (uint)n) { f.Add(t[i]); f.Add(t[i + 1]); f.Add(t[i + 2]); } t = f.ToArray(); len = t.Length; }
                else if (len != t.Length) { var c = new int[len]; Array.Copy(t, c, len); t = c; }
                mesh.SetTriangles(t, s, false);
            }
            if (m.Normals == null || m.Normals.Length != n * 3) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// RGBA32 decodificado -> Texture2D. El orden de filas del archivo de Unity 2.5 (origen abajo-izquierda) coincide con el de Unity moderno.
        /// Compresión a ETC2/ASTC: pasar compress=true (Texture2D.Compress sólo es rápido en editor; en dispositivo usar un transcodificador propio).
        public static bool IsInterfaceIcon(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var prefix in new[] { "cosicon_", "wpnicon_", "nanoicon_", "nanoready_", "skillicon_", "npcicon_", "hnpcicon_", "mobicon_", "generalitemicon_", "questitemicon_", "vehicle_", "transport_" })
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        public static Texture2D ToTexture(Texture25 t, bool compress = false)
        {
            var rgba = t.ToRGBA32(); var tex = new Texture2D(t.Width, t.Height, TextureFormat.RGBA32, t.MipMap) { name = t.Name };
            tex.SetPixelData(rgba, 0); tex.wrapMode = t.WrapMode == 1 ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            tex.filterMode = t.FilterMode == 0 ? FilterMode.Point : t.FilterMode == 1 ? FilterMode.Bilinear : FilterMode.Trilinear;
            tex.Apply(t.MipMap, false);   // los mipmaps se regeneran (no se leen los del archivo)
            bool powerOfTwo = Mathf.IsPowerOfTwo(t.Width) && Mathf.IsPowerOfTwo(t.Height);
            if (compress && !IsInterfaceIcon(t.Name) && t.Width % 4 == 0 && t.Height % 4 == 0 && (!t.MipMap || powerOfTwo))
                tex.Compress(!Application.isMobilePlatform);
            return tex;
        }
    }
}
