// Modelos del manifiesto de versión (/versions/<uuid>.json). Sin dependencias (Newtonsoft los rellena por nombre de campo).
using System.Collections.Generic;

namespace OpenFusion.Api
{
    public class HashInfo { public string hash; public long size; }
    public class BundleInfo
    {
        public string name; public HashInfo compressed_info;
        public Dictionary<string, HashInfo> uncompressed_info;   // por archivo interno (ej. "BuildPlayer-Map_11_01")
    }
    public class VersionManifest
    {
        public string uuid, asset_url, name, parent_uuid, main_file_url;
        public HashInfo main_file_info;
        public Dictionary<string, BundleInfo> bundles;   // clave = nombre del bundle (ej. "Map_11_01.unity3d")
    }
}
