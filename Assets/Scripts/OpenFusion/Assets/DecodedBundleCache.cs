using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenFusion.Assets
{
    // Derived data only: the original, verified download remains authoritative.
    // Deflate trades a little disk space for much faster decoding than legacy LZMA.
    public sealed class DecodedBundleCache
    {
        const int Magic = 0x3143464f;
        readonly string root;
        readonly long budget;
        readonly object gate = new object();
        public DecodedBundleCache(string root, long budget = 640L * 1024 * 1024)
        { this.root = root; this.budget = budget; }
        string PathFor(string identity)
        {
            using (var sha = SHA256.Create())
                return Path.Combine(root, BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "") + ".ofc");
        }
        public Dictionary<string, byte[]> Load(string identity, Func<Dictionary<string, byte[]>> decode, out bool hit, bool transient = false)
        {
            lock (gate)
            {
                string path = PathFor(identity); hit = false;
                string oldPath = path;
                if (transient) path = Path.Combine(root, "map-" + Path.GetFileName(path));
                try
                {
                    if (transient && !File.Exists(path) && File.Exists(oldPath)) File.Move(oldPath, path);
                    if (File.Exists(path))
                    {
                        var result = Read(path);
                        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
                        hit = true; return result;
                    }
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException)
                { /* Truncated/corrupt derived data is rebuilt from the original. */ }
                var files = decode();
                if (files == null) return null;
                string part = path + ".part";
                try
                {
                    Directory.CreateDirectory(root);
                    Write(part, files);
                    long size = new FileInfo(part).Length;
                    if (size > budget) return files;
                    // Visiting many dongs must not evict the shared startup working set.
                    var existing = new DirectoryInfo(root).GetFiles("*.ofc");
                    if (transient && existing.Where(f => !f.Name.StartsWith("map-", StringComparison.Ordinal)).Sum(f => f.Length) + size > budget) return files;
                    // Never exceed the bound after committing; remove oldest derived entries.
                    long total = size;
                    foreach (var file in existing.OrderBy(f => f.Name.StartsWith("map-", StringComparison.Ordinal) ? 1 : 0).ThenByDescending(f => f.LastWriteTimeUtc))
                    {
                        if (file.FullName == path) { file.Delete(); continue; }
                        total += file.Length;
                        if (total > budget) file.Delete();
                    }
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(part, path);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                finally { try { if (File.Exists(part)) File.Delete(part); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
                return files;
            }
        }
        static Dictionary<string, byte[]> Read(string path)
        {
            using (var file = File.OpenRead(path))
            using (var stream = new DeflateStream(file, CompressionMode.Decompress))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            using (var sha = SHA256.Create())
            {
                if (reader.ReadInt32() != Magic) throw new InvalidDataException("Invalid derived bundle header");
                int count = reader.ReadInt32();
                if (count < 1 || count > 256) throw new InvalidDataException("Invalid derived bundle count");
                var result = new Dictionary<string, byte[]>(); long total = 0;
                for (int i = 0; i < count; i++)
                {
                    int nameSize = reader.ReadInt32();
                    if (nameSize < 1 || nameSize > 4096) throw new InvalidDataException("Invalid derived file name");
                    string name = Encoding.UTF8.GetString(Exact(reader, nameSize));
                    int size = reader.ReadInt32(); total += size;
                    if (size < 0 || size > 256 * 1024 * 1024 || total > 512L * 1024 * 1024) throw new InvalidDataException("Invalid derived file size");
                    var hash = Exact(reader, 32); var bytes = Exact(reader, size);
                    if (!hash.SequenceEqual(sha.ComputeHash(bytes))) throw new InvalidDataException("Derived bundle checksum failed");
                    result.Add(name, bytes);
                }
                if (stream.ReadByte() != -1) throw new InvalidDataException("Unexpected derived bundle data");
                return result;
            }
        }
        static byte[] Exact(BinaryReader reader, int size)
        {
            var data = reader.ReadBytes(size);
            if (data.Length != size) throw new EndOfStreamException();
            return data;
        }
        static void Write(string path, Dictionary<string, byte[]> files)
        {
            using (var file = File.Create(path))
            using (var stream = new DeflateStream(file, CompressionLevel.Fastest))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            using (var sha = SHA256.Create())
            {
                writer.Write(Magic); writer.Write(files.Count);
                foreach (var pair in files)
                {
                    var name = Encoding.UTF8.GetBytes(pair.Key); writer.Write(name.Length); writer.Write(name);
                    writer.Write(pair.Value.Length); writer.Write(sha.ComputeHash(pair.Value)); writer.Write(pair.Value);
                }
            }
        }
    }
}
