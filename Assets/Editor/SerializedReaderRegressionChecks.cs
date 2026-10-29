using System;
using System.IO;
using OpenFusion.Unity25;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class SerializedReaderRegressionChecks
    {
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        public static void Run()
        {
            var random = new System.Random(7341);
            var types = new[] { "SInt16", "UInt16", "SInt32", "UInt32", "SInt64", "UInt64", "float", "double" };
            int compared = 0;
            foreach (bool be in new[] { false, true })
            foreach (string type in types)
            for (int i = 0; i < 256; i++)
            {
                int size = type.EndsWith("16") ? 2 : type.EndsWith("64") || type == "double" ? 8 : 4;
                var bytes = new byte[size + 1]; random.NextBytes(bytes);
                var reference = new byte[size]; Array.Copy(bytes, 1, reference, 0, size);
                if (be != !BitConverter.IsLittleEndian) Array.Reverse(reference);
                var reader = new SerializedFile.Reader(bytes, 1, be) { Packed = true };
                object result = reader.Read(new TypeNode { Type = type });
                byte[] roundtrip;
                switch (type)
                {
                    case "SInt16": roundtrip = BitConverter.GetBytes((short)result); break;
                    case "UInt16": roundtrip = BitConverter.GetBytes((ushort)result); break;
                    case "SInt32": roundtrip = BitConverter.GetBytes((int)result); break;
                    case "UInt32": roundtrip = BitConverter.GetBytes((uint)result); break;
                    case "SInt64": roundtrip = BitConverter.GetBytes((long)result); break;
                    case "UInt64": roundtrip = BitConverter.GetBytes((ulong)result); break;
                    case "float": roundtrip = BitConverter.GetBytes((float)result); break;
                    default: roundtrip = BitConverter.GetBytes((double)result); break;
                }
                Require(reader.Pos == size + 1, "Packed scalar offset changed: " + type);
                for (int b = 0; b < size; b++) Require(roundtrip[b] == reference[b], "Scalar bit pattern changed: " + type);
                compared++;
            }
            var aligned = new SerializedFile.Reader(new byte[8], 1, false);
            aligned.Read(new TypeNode { Type = "UInt16", MetaFlag = 0x4000 });
            Require(aligned.Pos == 4, "Aligned field changed.");
            var packed = new SerializedFile.Reader(new byte[8], 1, false) { Packed = true };
            packed.Read(new TypeNode { Type = "UInt16", MetaFlag = 0x4000 });
            Require(packed.Pos == 3, "Packed field aligned unexpectedly.");
            bool truncated = false;
            try { new SerializedFile.Reader(new byte[3], 0, false).I32(); } catch (FormatException) { truncated = true; }
            Require(truncated, "Truncated scalar must fail.");
            var repeated = new SerializedFile.Reader(new byte[4], 0, false);
            repeated.I32(); // JIT/warm-up before measuring.
            long start = GC.GetAllocatedBytesForCurrentThread(); int total = 0;
            for (int i = 0; i < 100000; i++) { repeated.Pos = 0; total ^= repeated.I32(); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            Require(total == 0 && allocated < 4096, "Integer scalar loop still allocates temporary buffers: " + allocated);
            Directory.CreateDirectory("Logs/ios");
            string report = "PASS: " + compared + " bit-exact LE/BE scalar fixtures (including packed offsets), alignment and truncation; 100000 I32 reads allocated " + allocated + " bytes. Read(TypeNode) still boxes scalar results by design.";
            File.WriteAllText("Logs/ios/serialized-reader-checks.txt", report);
            Debug.Log(report);
        }
    }
}
