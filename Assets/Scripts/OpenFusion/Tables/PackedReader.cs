// Lector de MonoBehaviours serializados por Unity 2.5 (sin alineación a 4 B, LE).
// Cabecera 33 B (PPtrs + enabled + m_Script), luego nombre (len+bytes) y campos públicos
// en orden de declaración. Las clases Gen/*.g.cs llaman a estos métodos (sin reflexión: seguro para IL2CPP).
using System;
using System.Text;
using UnityEngine;

namespace OpenFusion.Tables
{
    public sealed class PackedReader
    {
        readonly byte[] _d; public int Pos;
        public PackedReader(byte[] d, int pos = 0) { _d = d; Pos = pos; }
        public int Remaining => _d.Length - Pos;
        public static int ScriptId(byte[] raw) => BitConverter.ToInt32(raw, 29);

        /// Salta la cabecera y devuelve el nombre del objeto; el cuerpo queda listo para T.Read(r).
        public string Header() { Pos = 33; return String(); }

        public int Count() { int n = I32(); if (n < 0 || n > 5000000) throw new FormatException("longitud inválida " + n + " @" + Pos); return n; }
        public string String()
        {
            int n = Count(); var s = Encoding.UTF8.GetString(_d, Pos, n); Pos += n; return s;
        }
        public int I32() { var v = BitConverter.ToInt32(_d, Pos); Pos += 4; return v; }
        public uint U32() { var v = BitConverter.ToUInt32(_d, Pos); Pos += 4; return v; }
        public short I16() { var v = BitConverter.ToInt16(_d, Pos); Pos += 2; return v; }
        public ushort U16() { var v = BitConverter.ToUInt16(_d, Pos); Pos += 2; return v; }
        public byte U8() => _d[Pos++];
        public bool Bool() => _d[Pos++] != 0;
        public long I64() { var v = BitConverter.ToInt64(_d, Pos); Pos += 8; return v; }
        public ulong U64() { var v = BitConverter.ToUInt64(_d, Pos); Pos += 8; return v; }
        public float F32() { var v = BitConverter.ToSingle(_d, Pos); Pos += 4; return v; }
        public double F64() { var v = BitConverter.ToDouble(_d, Pos); Pos += 8; return v; }
        public Vector2 V2() => new Vector2(F32(), F32());
        public Vector3 V3() => new Vector3(F32(), F32(), F32());
        public Vector4 V4() => new Vector4(F32(), F32(), F32(), F32());
        public Quaternion Q() => new Quaternion(F32(), F32(), F32(), F32());
        public Color Col() => new Color(F32(), F32(), F32(), F32());
        public Rect RectV() => new Rect(F32(), F32(), F32(), F32()); // x,y,w,h
        public int PPtr() { Pos += 8; return 0; }
    }
}
