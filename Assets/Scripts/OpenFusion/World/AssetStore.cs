// Almacén de archivos serializados cargados + resolución de PPtr entre archivos (FileID -> Externals). Sin dependencias de Unity.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenFusion.Materials;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public struct Ref { public SerializedFile File; public string FileName; public ObjectInfo Info; public bool Ok => Info != null; }

    public sealed class AssetStore
    {
        sealed class Entry { public string Name; public SerializedFile File; public Dictionary<long, ObjectInfo> ById; }
        readonly Dictionary<string, Entry> files = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<(string, long), Mesh25> meshes = new Dictionary<(string, long), Mesh25>();
        readonly Dictionary<(string, long), MaterialSpec> materials = new Dictionary<(string, long), MaterialSpec>();
        readonly Dictionary<(string, long), string> shaderNames = new Dictionary<(string, long), string>();
        readonly Dictionary<(string, long), Texture25> textures = new Dictionary<(string, long), Texture25>();

        /// name: nombre interno del archivo (p.ej. "CustomAssetBundle-b4f5..." o "BuildPlayer-Map_01_00"); se compara sin mayúsculas.
        public SerializedFile Add(string name, byte[] data)
        {
            var f = SerializedFile.Parse(data);
            files[name] = new Entry { Name = name, File = f, ById = f.Objects.ToDictionary(o => o.PathId) };
            return f;
        }
        public bool Has(string name) => files.ContainsKey(name);
        public SerializedFile Get(string name) => files.TryGetValue(name, out var e) ? e.File : null;
        public void Remove(string name)
        {
            files.Remove(name); string n = name.ToLowerInvariant();
            foreach (var k in meshes.Keys.Where(k => k.Item1 == n).ToList()) meshes.Remove(k);
            foreach (var k in materials.Keys.Where(k => k.Item1 == n).ToList()) materials.Remove(k);
            foreach (var k in shaderNames.Keys.Where(k => k.Item1 == n).ToList()) shaderNames.Remove(k);
            foreach (var k in textures.Keys.Where(k => k.Item1 == n).ToList()) textures.Remove(k);
        }

        /// nombre del archivo externo n (FileID n>=1) tal como lo declara el archivo origen; "" si no es un bundle propio
        public static string ExternalName(SerializedFile from, int fileId) => fileId >= 1 && fileId - 1 < from.Externals.Count ? System.IO.Path.GetFileName(from.Externals[fileId - 1]) : null;

        public Ref Resolve(string fromName, int fileId, long pathId)
        {
            if (pathId == 0 || !files.TryGetValue(fromName, out var from)) return default;
            string target = fromName;
            if (fileId != 0) { target = ExternalName(from.File, fileId); if (target == null || !files.ContainsKey(target)) return default; }
            var e = files[target];
            return e.ById.TryGetValue(pathId, out var info) ? new Ref { File = e.File, FileName = e.Name, Info = info } : default;
        }
        public Ref Resolve(string fromName, PPtr25 p) => Resolve(fromName, p.FileId, p.PathId);
        public string TypeOf(Ref r) => r.Ok ? r.File.TypeOf(r.Info)?.Type : null;
        public Dictionary<string, object> Read(Ref r) => r.File.ReadObject(r.Info, out _);

        public Mesh25 Mesh(Ref r) { if (!r.Ok || TypeOf(r) != "Mesh") return null; var k = (r.FileName.ToLowerInvariant(), r.Info.PathId); if (!meshes.TryGetValue(k, out var m)) meshes[k] = m = Mesh25.From(Read(r)); return m; }
        public Texture25 Texture(Ref r) { if (!r.Ok || TypeOf(r) != "Texture2D") return null; var k = (r.FileName.ToLowerInvariant(), r.Info.PathId); if (!textures.TryGetValue(k, out var t)) textures[k] = t = Texture25.From(Read(r)); return t; }

        static readonly Regex ShaderRx = new Regex("Shader\\s+\"([^\"]+)\"", RegexOptions.Compiled);
        public string ShaderName(Ref r)
        {
            if (!r.Ok || r.File.TypeOf(r.Info)?.Type != "Shader") return null; var k = (r.FileName.ToLowerInvariant(), r.Info.PathId);
            if (shaderNames.TryGetValue(k, out var n)) return n;
            var raw = Encoding.UTF8.GetString(r.File.Data, (int)r.Info.Offset, (int)r.Info.Size); var m = ShaderRx.Match(raw);
            return shaderNames[k] = m.Success ? m.Groups[1].Value : null;
        }
        /// Material -> MaterialSpec (con su shader resuelto); las texturas de la spec llevan FileId relativo al archivo del material
        public MaterialSpec Material(Ref r, out string ownerFile)
        {
            ownerFile = r.FileName; if (!r.Ok || TypeOf(r) != "Material") return null; var k = (r.FileName.ToLowerInvariant(), r.Info.PathId);
            if (materials.TryGetValue(k, out var s)) return s;
            var v = Read(r); var sh = Resolve(r.FileName, PPtr25.From(v["m_Shader"]));
            return materials[k] = MaterialMapper.Map(v, ShaderName(sh) ?? "");
        }
    }
}
