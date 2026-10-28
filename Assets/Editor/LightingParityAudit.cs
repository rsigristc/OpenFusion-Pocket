using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenFusion.Unity25;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class LightingParityAudit
    {
        public static void Batch()
        {
            try { Run(); UnityEditor.EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
        }
        public static void Run()
        {
            var source = new FfCacheBundleSource(Path.GetFullPath("../ffcache"));
            var output = new List<string>();
            foreach (string name in new[] { "DongResources_12_02.resourceFile", "DongResources_13_03.resourceFile", "DongResources_09_07.resourceFile" })
            {
                var loaded = source.Load(name); if (loaded == null) continue;
                var files = loaded.Where(f => !f.Key.StartsWith("__")).ToDictionary(f => f.Key, f => f.Value);
                var store = new AssetStore();
                foreach (var file in files) store.Add(file.Key, file.Value);
                foreach (var file in files)
                {
                    var sf = store.Get(file.Key);
                    foreach (var item in sf.Objects)
                    {
                        string type = sf.TypeOf(item)?.Type;
                        if (type == "RenderSettings" || type == "Light")
                            output.Add(name + " " + type + " " + Newtonsoft.Json.JsonConvert.SerializeObject(sf.ReadObject(item, out _)));
                        if (type != "TerrainData") continue;
                        var terrain = Terrain25.From(sf.ReadObject(item, out _));
                        var lightmap = store.Texture(store.Resolve(file.Key, terrain.Lightmap));
                        output.Add(name + " terrain=" + terrain.Name + " lm=" + (lightmap == null ? "unresolved" : lightmap.Name + " format=" + lightmap.Format));
                        if (lightmap == null) continue;
                        byte[] rgba = lightmap.ToRGBA32();
                        var rgb = new List<byte>(); var alpha = new HashSet<byte>();
                        for (int i = 0; i < rgba.Length; i += 256) { rgb.Add(rgba[i]); rgb.Add(rgba[i+1]); rgb.Add(rgba[i+2]); alpha.Add(rgba[i+3]); }
                        rgb.Sort();
                        output.Add("RGB q10/50/90/max=" + rgb[rgb.Count/10] + "/" + rgb[rgb.Count/2] + "/" + rgb[rgb.Count*9/10] + "/" + rgb.Last() + " alpha=" + string.Join(",", alpha.OrderBy(v=>v)));
                    }
                }
            }
            Directory.CreateDirectory("Logs/lighting"); File.WriteAllLines("Logs/lighting/legacy-assets.txt", output);
            Debug.Log("[lighting audit] " + string.Join("\n", output));
        }
    }
}
