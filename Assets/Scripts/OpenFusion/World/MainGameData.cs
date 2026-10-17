using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    // These objects are assigned directly to managers in main.unity3d. They can be
    // newer than the similarly named assets in streamed bundles (Retrobution).
    public sealed class MainGameData
    {
        public sealed class Projectile
        {
            public int Part, Succ, Fire; public string Sound, FireLink, SuccLink;
            public float BulletScale, SuccScale, FireScale, MaxTimer, Hide;
        }
        public sealed class Region { public int Index, Delay; public string File; public bool Ins; public float[] Pts; }
        public int Schema = 1; public string Hash;
        public CachingManifest25 Manifest;
        public Projectile[] Projectiles;
        public Region[] Music, Ambient;
        [JsonIgnore] public bool CacheHit;

        public static MainGameData LoadCached(byte[] main, string path, string hash = null)
        {
            if (string.IsNullOrEmpty(hash)) using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(main)).Replace("-", "").ToLowerInvariant();
            try
            {
                var cached = File.Exists(path) ? JsonConvert.DeserializeObject<MainGameData>(File.ReadAllText(path)) : null;
                if (cached != null && cached.Schema == 1 && cached.Hash == hash && cached.Manifest?.Lists?.Count > 0)
                { cached.CacheHit = true; return cached; }
            }
            catch (Exception) { /* A broken derived cache is rebuilt from the verified main. */ }
            var data = FromMain(main); data.Hash = hash;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path + ".part", JsonConvert.SerializeObject(data));
                if (File.Exists(path)) File.Delete(path); File.Move(path + ".part", path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return data;
        }
        public static MainGameData FromMain(byte[] main)
        {
            var result = new MainGameData(); var bundle = UnityWebBundle.Parse(main);
            foreach (var file in bundle.Files)
            {
                if (file.Key != "mainData" && !file.Key.EndsWith(".assets", StringComparison.OrdinalIgnoreCase)) continue;
                var sf = SerializedFile.Parse(file.Value);
                foreach (var obj in sf.Objects)
                {
                    if (obj.ClassId != 114) continue;
                    var d = sf.ReadObject(obj, out _);
                    if (d.ContainsKey("m_PaidZoneCoordinates")) result.Manifest = CachingManifest25.From(d);
                    if (d.TryGetValue("m_pBulletData", out var bullets))
                        result.Projectiles = D.A(bullets).Select(value =>
                        {
                            var row = D.O(value);
                            return new Projectile { Part = D.I(row["m_iParticleScript"]), Succ = D.I(row["m_iSuccScript"]), Fire = D.I(row["m_iFireScript"]), Sound = row["m_strSuccSound"] as string,
                                BulletScale = D.F(row["m_fBulletModelScale"]), SuccScale = D.F(row["m_fSuccModelScale"]), FireScale = D.F(row["m_fFireModelScale"]), MaxTimer = D.F(row["m_fMaxTimer"]), Hide = D.F(row["m_fHideTime"]), FireLink = row["m_strFireLink"] as string, SuccLink = row["m_strSuccLink"] as string };
                        }).ToArray();
                    if (d.ContainsKey("musicDatas") && d.ContainsKey("ambientDatas"))
                    { result.Music = Regions(d["musicDatas"]); result.Ambient = Regions(d["ambientDatas"]); }
                }
            }
            if (result.Manifest == null) throw new InvalidDataException("main.unity3d contains no caching manifest.");
            return result;
        }
        static Region[] Regions(object values) => D.A(values).Select(value =>
        {
            var d = D.O(value); return new Region { Index = D.I(d["index"]), Delay = D.I(d["delay"]), File = d["fileName"] as string, Ins = D.I(d["bInstanceFlag"]) != 0, Pts = D.Flat2(d["points"]) };
        }).ToArray();
    }
}
