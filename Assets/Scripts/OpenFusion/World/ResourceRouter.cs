// Port fiel de ResourceLocator.LocateWWWResource (cliente original): dada la ruta de un asset decide en qué bundle está.
// Puro (sin Unity). La decisión depende de qué bundles están cargados y de su contenido (AssetBundle.Contains = AssetIndex.Has).
using System;
using System.Collections.Generic;

namespace OpenFusion.World
{
    public interface IBundleContents
    {
        bool IsLoaded(string bundle);                 // nombre del manifiesto, p. ej. "Retro_shared_part2.resourceFile"
        bool Contains(string bundle, string path);    // AssetBundle.Contains (ruta en minúsculas)
    }

    public sealed class ResourceRouter
    {
        public const string Invalid = "invalid.resourceFile";
        readonly IBundleContents c;
        public int CurrentDongX, CurrentDongY;        // ResourceLocator.m_CurrentPosX/Y
        public string FontSuffix;                     // localización: null/"" = inglés (sin fuente aparte)
        public ResourceRouter(IBundleContents contents) { c = contents; }

        public void SetPosition(float x, float z) { CurrentDongX = (int)Math.Floor(x / DongGrid.Size); CurrentDongY = (int)Math.Floor(z / DongGrid.Size); }

        static string Dong(int x, int y) => $"DongResources_{x:00}_{y:00}.resourceFile";
        bool Has(string b, string path) => c.IsLoaded(b) && c.Contains(b, path);

        bool SurroundingArea(string path, out string www)
        {
            var cur = Dong(CurrentDongX, CurrentDongY);
            if (Has(cur, path)) { www = cur; return true; }
            for (int i = 0; i < 16; i++) for (int j = 0; j < 16; j++) { var n = Dong(i, j); if (Has(n, path)) { www = n; return true; } }
            www = null; return false;
        }

        /// Bundles de zona de pago (m_PaidZone), en orden de manifiesto. Se pasan al construir el router.
        public IList<string> PaidZoneFiles = new List<string>();
        bool WorldShared(string path, out string www)
        {
            foreach (var f in PaidZoneFiles) if (Has(f, path)) { www = f; return true; }
            www = null; return false;
        }

        /// Devuelve el nombre del bundle (del manifiesto) o null si no se encuentra (el original devuelve false + "invalid.resourceFile").
        public string Locate(string input)
        {
            string res = input;                        // el original usa la ruta tal cual; Contains es insensible a mayúsculas (claves en minúsculas)
            string low = input.ToLowerInvariant();
            bool Starts(string p) => input.StartsWith(p, StringComparison.OrdinalIgnoreCase);   // los loaders pasan la ruta en minusculas
            if (Starts("bgm/"))
            {
                foreach (var m in new[] { "LobbyMusic.resourceFile", "FutureMusic.resourceFile", "PastMusic.resourceFile", "RetroMusic.resourceFile" }) if (Has(m, low)) return m;
                return null;
            }
            if (Starts("FU Sound/")) return "FUSound.resourceFile";
            if (Starts("CharacterCreationAssets/Font/")) return string.IsNullOrEmpty(FontSuffix) ? "" : "Font" + FontSuffix + ".resourceFile";
            if (Starts("CC Sound/") || Starts("ui sound") || Starts("CharacterCreationAssets") || Has("CharacterCreation.resourceFile", low)) return "CharacterCreation.resourceFile";
            if (Starts("Prefabs/Particle/EffectScripts") || input.IndexOf("bullettable.asset", StringComparison.Ordinal) != -1 || Has("Effects.resourceFile", low)) return "Effects.resourceFile";
            if (Starts("TutorialAssets/Shaders/") || Has("CharacterSelection.resourceFile", low)) return "CharacterSelection.resourceFile";
            if (Starts("TutorialAssets/") || Starts("Tut Sound/") || Has("Tutorial.resourceFile", low)) return "Tutorial.resourceFile";
            if (Starts("Texture") || Starts("texture") || input.IndexOf(".dds", StringComparison.Ordinal) != -1)
            {
                if (Has("CharTexture.resourceFile", low)) return "CharTexture.resourceFile";
                if (Has("NpcTexture.resourceFile", low)) return "NpcTexture.resourceFile";
                if (Has("Retro_shared.resourceFile", low)) return "Retro_shared.resourceFile";
                return "Retro_shared_part2.resourceFile";
            }
            if (Starts("Nano") || Starts("nano") || Has("FutureNano.resourceFile", low))
                return Has("FutureNano.resourceFile", low) ? "FutureNano.resourceFile" : "Nano.resourceFile";
            if (Has("Retro_shared.resourceFile", low)) return "Retro_shared.resourceFile";
            if (Has("Retro_shared_part2.resourceFile", low)) return "Retro_shared_part2.resourceFile";
            if (Starts("Icons") || Starts("Help")) return "Icons.resourceFile";
            if (Starts("xdtdatas") || Starts("all_hnpc") || Starts("clientnpc") || Starts("worldname")) return "TableData.resourceFile";
            if (Has("Freearea_shared.resourceFile", low)) return "Freearea_shared.resourceFile";
            if (WorldShared(low, out var w)) return w;
            if (SurroundingArea(low, out var s)) return s;
            return null;
        }
    }
}
