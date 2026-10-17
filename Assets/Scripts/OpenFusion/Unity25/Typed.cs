// Adaptadores tipados sobre los diccionarios del lector genérico (SerializedFile.ReadObject).
using System;
using System.Collections.Generic;
using OpenFusion.Convert;

namespace OpenFusion.Unity25
{
    using Dict = Dictionary<string, object>;

    static class D
    {
        public static Dict O(object o) => (Dict)o;
        public static object[] A(object o) => (object[])o;
        public static float F(object o) => (float)o;
        public static int I(object o) => o is int i ? i : o is uint u ? (int)u : o is byte b ? b : o is bool bb ? (bb ? 1 : 0) : Convert2(o);
        static int Convert2(object o) => System.Convert.ToInt32(o);
        public static float[] V3(object o) { var d = O(o); return new[] { F(d["x"]), F(d["y"]), F(d["z"]) }; }
        public static float[] V4(object o) { var d = O(o); return new[] { F(d["x"]), F(d["y"]), F(d["z"]), F(d["w"]) }; }
        public static float[] Flat3(object arr) { var a = A(arr); var r = new float[a.Length * 3]; for (int i = 0; i < a.Length; i++) { var v = V3(a[i]); r[3*i] = v[0]; r[3*i+1] = v[1]; r[3*i+2] = v[2]; } return r; }
        public static float[] Flat2(object arr) { var a = A(arr); var r = new float[a.Length * 2]; for (int i = 0; i < a.Length; i++) { var v = O(a[i]); r[2*i] = F(v["x"]); r[2*i+1] = F(v["y"]); } return r; }
        public static PackedBits PB(object o)
        {
            var d = O(o); return new PackedBits {
                Data = (byte[])d["m_Data"], NumItems = I(d["m_NumItems"]), BitSize = I(d["m_BitSize"]),
                Range = d.ContainsKey("m_Range") ? F(d["m_Range"]) : 0, Start = d.ContainsKey("m_Start") ? F(d["m_Start"]) : 0 };
        }
    }

    public sealed class Texture25
    {
        public string Name; public int Width, Height, Format; public bool MipMap; public byte[] Data;
        public int FilterMode, WrapMode;
        public static Texture25 From(Dictionary<string, object> v)
        {
            var ts = D.O(v["m_TextureSettings"]);
            return new Texture25 { Name = (string)v["m_Name"], Width = D.I(v["m_Width"]), Height = D.I(v["m_Height"]), Format = D.I(v["m_TextureFormat"]),
                MipMap = (bool)v["m_MipMap"], Data = (byte[])v["image data"], FilterMode = D.I(ts["m_FilterMode"]), WrapMode = D.I(ts["m_WrapMode"]) };
        }
        public byte[] ToRGBA32() => TextureDecoder.ToRGBA32(Width, Height, Format, Data);
    }

    public sealed class SubMesh25 { public int FirstByte, IndexCount, TriangleCount; public bool IsTriStrip; }

    public sealed class Mesh25
    {
        public string Name; public float[] Positions, Normals, UV, UV1, Tangents; public int[] BoneIdx; public float[] BoneW;
        public float[][] BindPose;   // matrices 4x4 (16 floats, orden e00..e33)
        public int[][] SubmeshTriangles; public float[] AabbCenter, AabbExtent;

        public static Mesh25 From(Dictionary<string, object> v)
        {
            var m = new Mesh25 { Name = (string)v["m_Name"] };
            bool use16 = D.I(v["m_Use16BitIndices"]) != 0;
            var subs = D.A(v["m_SubMeshes"]); var ib = (byte[])v["m_IndexBuffer"];
            int comp = D.I(v["m_MeshCompression"]);
            if (comp != 0)
            {
                var c = D.O(v["m_CompressedMesh"]);
                var cm = new CompressedMeshData { Vertices = D.PB(c["m_Vertices"]), Normals = D.PB(c["m_Normals"]), NormalSigns = D.PB(c["m_NormalSigns"]),
                    UV = D.PB(c["m_UV"]), Triangles = D.PB(c["m_Triangles"]), Weights = D.PB(c["m_Weights"]), BoneIndices = D.PB(c["m_BoneIndices"]) };
                var r = MeshDecoder.Decompress(cm);
                m.Positions = r.Positions; m.Normals = r.Normals; m.UV = r.UVs; m.BoneIdx = r.BoneIdx; m.BoneW = r.BoneW;
                // índices comprimidos: lista de triángulos única; los submeshes indexan por (firstByte/2, indexCount)
                var all = new int[r.Indices.Length]; for (int i = 0; i < all.Length; i++) all[i] = (int)r.Indices[i];
                m.SubmeshTriangles = new int[subs.Length][];
                for (int s = 0; s < subs.Length; s++)
                {
                    var sd = D.O(subs[s]); int first = D.I(sd["firstByte"]) / 2, cnt = D.I(sd["indexCount"]);
                    first = Math.Min(first, all.Length); cnt = Math.Min(cnt, all.Length - first);
                    var t = new int[cnt]; Array.Copy(all, first, t, 0, cnt); m.SubmeshTriangles[s] = MeshDecoder.ToTriangleList(t, D.I(sd["isTriStrip"]) != 0);
                }
                var bp = D.A(v["m_BindPose"]);
                m.BindPose = Mat(bp);
            }
            else
            {
                m.Positions = D.Flat3(v["m_Vertices"]); m.Normals = D.Flat3(v["m_Normals"]); m.UV = D.Flat2(v["m_UV"]); m.UV1 = D.Flat2(v["m_UV1"]);
                var tg = D.A(v["m_Tangents"]); m.Tangents = new float[tg.Length * 4]; for (int i = 0; i < tg.Length; i++) { var t = D.V4(tg[i]); Array.Copy(t, 0, m.Tangents, 4 * i, 4); }
                var sk = D.A(v["m_Skin"]);
                if (sk.Length > 0)
                {
                    m.BoneIdx = new int[sk.Length * 4]; m.BoneW = new float[sk.Length * 4];
                    for (int i = 0; i < sk.Length; i++) { var d = D.O(sk[i]); for (int j = 0; j < 4; j++) { m.BoneIdx[4*i+j] = D.I(d["boneIndex[" + j + "]"]); m.BoneW[4*i+j] = D.F(d["weight[" + j + "]"]); } }
                }
                m.BindPose = Mat(D.A(v["m_BindPose"]));
                m.SubmeshTriangles = new int[subs.Length][];
                for (int s = 0; s < subs.Length; s++)
                {
                    var sd = D.O(subs[s]);
                    m.SubmeshTriangles[s] = MeshDecoder.Triangles(ib, use16, D.I(sd["firstByte"]), D.I(sd["indexCount"]), D.I(sd["isTriStrip"]) != 0);
                }
            }
            var ab = D.O(v["m_LocalAABB"]); m.AabbCenter = D.V3(ab["m_Center"]); m.AabbExtent = D.V3(ab["m_Extent"]);
            return m;
        }
        static float[][] Mat(object[] a)
        {
            var r = new float[a.Length][];
            for (int i = 0; i < a.Length; i++) { var d = D.O(a[i]); var f = new float[16]; for (int k = 0; k < 16; k++) f[k] = D.F(d["e" + (k / 4) + (k % 4)]); r[i] = f; }
            return r;
        }
    }

    public sealed class Key25 { public float Time; public float[] Value, InSlope, OutSlope; }
    public sealed class Curve25 { public string Path; public Key25[] Keys; public int Dim; public int Pre = 2, Post = 2; }   // Pre/Post = m_PreInfinity/m_PostInfinity: 1 = repetir (loop), 2 = mantener (clamp)
    public sealed class RotCurve25 { public string Path; public float[] Times; public float[][] Quats; public int Pre = 2, Post = 2; public float[] SlopesIn, SlopesOut; }   // pendientes: 4 por clave (x,y,z,w), null si el archivo no las trae

    public sealed class Clip25
    {
        public string Name; public float SampleRate; public int WrapMode;
        public List<Curve25> Position = new List<Curve25>(), Scale = new List<Curve25>();
        public sealed class Ev25 { public float Time; public string Func, Data; }
        public List<Ev25> Events = new List<Ev25>();   // AnimationEvent: sound/attacksound/particle/tag_p/...
        public List<RotCurve25> Rotation = new List<RotCurve25>();   // rotaciones normales + comprimidas ya decodificadas
        public sealed class FloatCurve25 { public string Path, Attribute; public int ClassId; public float[] T, V, In, Out; public int Pre = 2, Post = 2; }
        public List<FloatCurve25> Floats = new List<FloatCurve25>();   // m_FloatCurves: propiedades de material animadas (_MainTex.offset.x, _Color.r, _Emission.g...)

        public static Clip25 From(Dictionary<string, object> v)
        {
            var c = new Clip25 { Name = (string)v["m_Name"], SampleRate = v.ContainsKey("m_SampleRate") ? D.F(v["m_SampleRate"]) : 0, WrapMode = v.ContainsKey("m_WrapMode") ? D.I(v["m_WrapMode"]) : 0 };
            foreach (var x in D.A(v["m_PositionCurves"])) c.Position.Add(Vec3((Dict)x));
            foreach (var x in D.A(v["m_ScaleCurves"])) c.Scale.Add(Vec3((Dict)x));
            foreach (var x in D.A(v["m_RotationCurves"]))
            {
                var d = (Dict)x; var keys = D.A(D.O(d["curve"])["m_Curve"]);
                var rc = new RotCurve25 { Path = (string)d["path"], Times = new float[keys.Length], Quats = new float[keys.Length][] };
                var cvd = D.O(d["curve"]); rc.Pre = cvd.ContainsKey("m_PreInfinity") ? D.I(cvd["m_PreInfinity"]) : 2; rc.Post = cvd.ContainsKey("m_PostInfinity") ? D.I(cvd["m_PostInfinity"]) : 2;
                bool hasSl = keys.Length > 0 && D.O(keys[0]).ContainsKey("inSlope") && D.O(keys[0]).ContainsKey("outSlope");
                if (hasSl) { rc.SlopesIn = new float[keys.Length * 4]; rc.SlopesOut = new float[keys.Length * 4]; }
                for (int i = 0; i < keys.Length; i++)
                {
                    var k = D.O(keys[i]); rc.Times[i] = D.F(k["time"]); rc.Quats[i] = D.V4(k["value"]);
                    if (hasSl) { var si = D.V4(k["inSlope"]); var so = D.V4(k["outSlope"]); for (int j = 0; j < 4; j++) { rc.SlopesIn[4 * i + j] = si[j]; rc.SlopesOut[4 * i + j] = so[j]; } }
                }
                c.Rotation.Add(rc);
            }
            foreach (var x in D.A(v["m_CompressedRotationCurves"]))
            {
                var d = (Dict)x; var vals = D.O(d["m_Values"]);
                PackedVectors.DecodeCurve(D.PB(d["m_Times"]), (byte[])vals["m_Data"], D.I(vals["m_NumItems"]), D.PB(d["m_Slopes"]), out var t, out var q, out var sl);
                var crc = new RotCurve25 { Path = (string)d["m_Path"], Times = t, Quats = q, Pre = d.ContainsKey("m_PreInfinity") ? D.I(d["m_PreInfinity"]) : 2, Post = d.ContainsKey("m_PostInfinity") ? D.I(d["m_PostInfinity"]) : 2 };
                if (sl != null && sl.Length == t.Length * 4) { crc.SlopesIn = sl; crc.SlopesOut = sl; }   // m_Slopes: pendiente de entrada = de salida, 4 por clave (x,y,z,w)
                c.Rotation.Add(crc);
            }
            if (v.ContainsKey("m_FloatCurves") && v["m_FloatCurves"] is object[] fcs)
                foreach (var x in fcs)
                {
                    var d = D.O(x); if (!d.ContainsKey("attribute") || !d.ContainsKey("curve")) continue; var ks = D.A(D.O(d["curve"])["m_Curve"]); if (ks.Length == 0) continue;
                    var fcv = D.O(d["curve"]); var fc = new FloatCurve25 { Path = d.ContainsKey("path") ? (string)d["path"] : "", Attribute = (string)d["attribute"], ClassId = d.ContainsKey("classID") ? D.I(d["classID"]) : 0, T = new float[ks.Length], V = new float[ks.Length], In = new float[ks.Length], Out = new float[ks.Length],
                        Pre = fcv.ContainsKey("m_PreInfinity") ? D.I(fcv["m_PreInfinity"]) : 2, Post = fcv.ContainsKey("m_PostInfinity") ? D.I(fcv["m_PostInfinity"]) : 2 };
                    for (int i = 0; i < ks.Length; i++) { var k = D.O(ks[i]); fc.T[i] = D.F(k["time"]); fc.V[i] = D.F(k["value"]); fc.In[i] = k.ContainsKey("inSlope") ? D.F(k["inSlope"]) : 0f; fc.Out[i] = k.ContainsKey("outSlope") ? D.F(k["outSlope"]) : 0f; }
                    c.Floats.Add(fc);
                }
            if (v.ContainsKey("m_Events"))
                foreach (var x in D.A(v["m_Events"]))
                {
                    var d = D.O(x); if (!d.ContainsKey("functionName")) continue;
                    c.Events.Add(new Ev25 { Time = d.ContainsKey("time") ? D.F(d["time"]) : 0f, Func = d["functionName"] as string, Data = d.ContainsKey("data") ? d["data"] as string : "" });
                }
            return c;
        }
        static Curve25 Vec3(Dict d)
        {
            var cvo = D.O(d["curve"]); var keys = D.A(cvo["m_Curve"]); var c = new Curve25 { Path = (string)d["path"], Dim = 3, Keys = new Key25[keys.Length],
                Pre = cvo.ContainsKey("m_PreInfinity") ? D.I(cvo["m_PreInfinity"]) : 2, Post = cvo.ContainsKey("m_PostInfinity") ? D.I(cvo["m_PostInfinity"]) : 2 };
            for (int i = 0; i < keys.Length; i++) { var k = D.O(keys[i]); c.Keys[i] = new Key25 { Time = D.F(k["time"]), Value = D.V3(k["value"]), InSlope = D.V3(k["inSlope"]), OutSlope = D.V3(k["outSlope"]) }; }
            return c;
        }
    }
}
