// SerializedFile de Unity 2.5 (versión 6): metadatos al final del archivo, big/little endian según flag,
// con TypeTree embebido. Lector genérico guiado por el typetree (equivalente al de UnityPy/AssetStudio).
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFusion.Unity25
{
    public sealed class TypeNode
    {
        public string Type, Name; public int ByteSize, Index, Flags, Version, MetaFlag;
        public List<TypeNode> Children = new List<TypeNode>();
        public bool Aligned => (MetaFlag & 0x4000) != 0;
    }

    public sealed class ObjectInfo
    {
        public long PathId; public uint Offset, Size; public int TypeId; public int ClassId;
    }

    public sealed class SerializedFile
    {
        public byte[] Data; public uint Version; public bool BigEndian;
        public Dictionary<int, TypeNode> Types = new Dictionary<int, TypeNode>();   // classId -> árbol
        public List<ObjectInfo> Objects = new List<ObjectInfo>();
        public List<string> Externals = new List<string>();

        int p;
        uint U32() { uint v = BigEndian ? (uint)(Data[p] << 24 | Data[p + 1] << 16 | Data[p + 2] << 8 | Data[p + 3]) : BitConverter.ToUInt32(Data, p); p += 4; return v; }
        int I32() => (int)U32();
        string CStr() { int s = p; while (Data[p] != 0) p++; var r = Encoding.UTF8.GetString(Data, s, p - s); p++; return r; }

        public static SerializedFile Parse(byte[] data)
        {
            var f = new SerializedFile { Data = data };
            f.BigEndian = true;
            uint metaSize = f.U32(), fileSize = f.U32(); f.Version = f.U32(); uint dataOffset = f.U32();
            if (f.Version != 6) throw new NotSupportedException("SerializedFile v" + f.Version + " (solo v6 verificada)");
            f.p = (int)(fileSize - metaSize); f.BigEndian = data[f.p++] != 0;
            int nt = f.I32();
            for (int i = 0; i < nt; i++)
            {
                int classId = f.I32(); var root = f.ReadNode(); if (!f.Types.ContainsKey(classId)) f.Types[classId] = root;
            }
            int no = f.I32();
            for (int i = 0; i < no; i++)
            {
                var o = new ObjectInfo { PathId = f.I32() }; o.Offset = f.U32() + dataOffset; o.Size = f.U32(); o.TypeId = f.I32();
                o.ClassId = f.p < data.Length ? (BigEndian2(data, f.p, f.BigEndian)) : 0; f.p += 2; f.p += 2; // class_id u16 + is_destroyed u16
                f.Objects.Add(o);
            }
            int ne = f.I32();
            for (int i = 0; i < ne; i++) { f.CStr(); f.p += 16; f.p += 4; f.Externals.Add(f.CStr()); }
            return f;
        }
        static int BigEndian2(byte[] d, int p, bool be) => be ? (d[p] << 8 | d[p + 1]) : (d[p + 1] << 8 | d[p]);

        TypeNode ReadNode()
        {
            var root = new TypeNode(); var stack = new Stack<(TypeNode node, int remaining)>();
            Fill(root); stack.Push((root, I32()));
            // lectura iterativa: cada nodo declara su nº de hijos al final
            var cur = new Stack<TypeNode>(); var pend = new Stack<int>();
            cur.Push(root); pend.Push(stack.Pop().remaining);
            while (cur.Count > 0)
            {
                if (pend.Peek() == 0) { cur.Pop(); pend.Pop(); continue; }
                pend.Push(pend.Pop() - 1);
                var n = new TypeNode(); Fill(n); cur.Peek().Children.Add(n);
                int cc = I32(); cur.Push(n); pend.Push(cc);
            }
            return root;
        }
        void Fill(TypeNode n) { n.Type = CStr(); n.Name = CStr(); n.ByteSize = I32(); n.Index = I32(); n.Flags = I32(); n.Version = I32(); n.MetaFlag = I32(); }

        // ---- lectura genérica de un objeto ----
        public sealed class Reader
        {
            readonly byte[] d; readonly bool be; public int Pos;
            public Reader(byte[] data, int pos, bool bigEndian) { d = data; Pos = pos; be = bigEndian; }
            // Scalar reads must not allocate a temporary byte[] for every vertex,
            // animation key and terrain sample. Preserve unaligned/packed reads.
            ulong Bits(int size)
            {
                if (Pos < 0 || Pos > d.Length - size) throw new FormatException("Truncated scalar");
                ulong value = 0;
                if (be) for (int i = 0; i < size; i++) value = (value << 8) | d[Pos + i];
                else for (int i = 0; i < size; i++) value |= (ulong)d[Pos + i] << (8 * i);
                Pos += size; return value;
            }
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
            struct NumberBits
            {
                [System.Runtime.InteropServices.FieldOffset(0)] public uint U32;
                [System.Runtime.InteropServices.FieldOffset(0)] public float Float;
                [System.Runtime.InteropServices.FieldOffset(0)] public ulong U64;
                [System.Runtime.InteropServices.FieldOffset(0)] public double Double;
            }
            public int I32() => unchecked((int)Bits(4));
            public bool Packed; public void Align() { if (!Packed) Pos = (Pos + 3) & ~3; }

            public object Read(TypeNode n)
            {
                object v;
                switch (n.Type)
                {
                    case "SInt8": v = (sbyte)d[Pos++]; break;
                    case "UInt8": case "char": v = d[Pos++]; break;
                    case "bool": v = d[Pos++] != 0; break;
                    case "short": case "SInt16": v = unchecked((short)Bits(2)); break;
                    case "unsigned short": case "UInt16": v = (ushort)Bits(2); break;
                    case "int": case "SInt32": v = I32(); break;
                    case "unsigned int": case "UInt32": v = (uint)Bits(4); break;
                    case "long long": case "SInt64": v = unchecked((long)Bits(8)); break;
                    case "unsigned long long": case "UInt64": v = Bits(8); break;
                    case "float": v = new NumberBits { U32 = (uint)Bits(4) }.Float; break;
                    case "double": v = new NumberBits { U64 = Bits(8) }.Double; break;
                    case "string": { int len = I32(); if (len < 0 || len > d.Length - Pos) throw new FormatException("string " + len); v = Encoding.UTF8.GetString(d, Pos, len); Pos += len; Align(); break; }
                    case "TypelessData": { int len = I32(); if (len < 0 || len > d.Length - Pos) throw new FormatException("data " + len); var b = new byte[len]; Array.Copy(d, Pos, b, 0, len); Pos += len; v = b; break; }
                    default:
                        if (n.Children.Count > 0 && n.Children[0].Type == "Array")
                        {
                            var arr = n.Children[0]; int size = I32(); if (size < 0 || size > d.Length - Pos) throw new FormatException("array " + size);
                            var sub = arr.Children[1];
                            if (sub.Type == "UInt8" || sub.Type == "char" || sub.Type == "SInt8") { var b = new byte[size]; Array.Copy(d, Pos, b, 0, size); Pos += size; v = b; }
                            else { var l = new object[size]; for (int i = 0; i < size; i++) l[i] = Read(sub); v = l; }
                            if (arr.Aligned) Align();
                        }
                        else
                        {
                            var o = new Dictionary<string, object>();
                            foreach (var c in n.Children) o[c.Name] = Read(c);
                            v = o;
                        }
                        break;
                }
                if (n.Aligned) Align();
                return v;
            }
        }

        public TypeNode TypeOf(ObjectInfo o) => Types.TryGetValue(o.TypeId, out var t) ? t : (Types.TryGetValue(o.ClassId, out t) ? t : null);

        /// Lee un objeto; devuelve su valor y la posición final (para detectar datos extra, p.ej. MonoBehaviour).
        public Dictionary<string, object> ReadObject(ObjectInfo o, out int consumed)
        {
            var t = TypeOf(o); var r = new Reader(Data, (int)o.Offset, BigEndian);
            Dictionary<string, object> v;
            if (t.Type == "MonoBehaviour")
            {
                // Unity 2.5: tras m_Script los datos del script van empaquetados (sin alineación a 4 B)
                v = new Dictionary<string, object>(); bool packed = false;
                foreach (var c in t.Children)
                {
                    v[c.Name] = r.Read(c);
                    
                }
                if (!packed) r.Packed = false;
            }
            else v = (Dictionary<string, object>)r.Read(t);
            consumed = r.Pos - (int)o.Offset; return v;
        }
    }
}
