// Índice "ruta de asset -> objeto" a partir de la tabla m_Container del objeto AssetBundle de cada bundle.
// El cliente pide assets por ruta ("mob/npc_arturo.kfm", "wear/hat_x.nif", "texture/x.dds"); la clave del contenedor está en minúsculas.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public struct AssetEntry { public string File; public PPtr25 Target; public string Path; }

    /// Quien sabe a qué objeto apunta una ruta de asset (un índice suelto, o el BundleManager con el enrutador).
    public interface IAssetLocator { bool TryLocate(string path, out AssetEntry entry); }

    public sealed class AssetIndex : IAssetLocator
    {
        readonly Dictionary<string, AssetEntry> map = new Dictionary<string, AssetEntry>();
        public int Count => map.Count;
        public IEnumerable<string> Keys => map.Keys;

        /// Añade todas las entradas del AssetBundle de `file` (nombre interno del archivo, p. ej. "CustomAssetBundle-Retro_shared_part2").
        /// Devuelve cuántas añadió. Si una ruta ya existe se conserva la primera (el cliente busca por orden de bundle).
        public int Add(string fileName, SerializedFile file)
        {
            var ab = file.Objects.FirstOrDefault(o => file.TypeOf(o)?.Type == "AssetBundle"); if (ab == null) return 0;
            var v = file.ReadObject(ab, out _); int n = 0;
            foreach (var c in D.A(v["m_Container"]))
            {
                var d = D.O(c); var key = ((string)d["first"]).ToLowerInvariant(); var target = PPtr25.From(D.O(d["second"])["asset"]);
                if (target.PathId == 0) continue; // A null container entry must not shadow a real prefab in another bundle.
                if (map.ContainsKey(key)) continue; map[key] = new AssetEntry { File = fileName, Target = target, Path = key }; n++;
            }
            return n;
        }

        public void RemoveFile(string fileName) { foreach (var k in map.Where(kv => kv.Value.File == fileName).Select(kv => kv.Key).ToList()) map.Remove(k); }
        public bool TryGet(string path, out AssetEntry e) => map.TryGetValue(path.ToLowerInvariant(), out e);
        public bool TryLocate(string path, out AssetEntry e) => TryGet(path, out e);
        public bool Has(string path) => map.ContainsKey(path.ToLowerInvariant());
    }
}
