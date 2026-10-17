// Loc: traducciones de la interfaz del cliente original (TextManager.GetStr + guixdtdatas, 12 idiomas; aqui los 6 que ofrece cnOptionMode:
// English, Portuguese, Spanish, Italian, Turkish, Candy Pirate). Datos: Resources/OpenFusion/UI/Data/guilang.json = { ids:[0,1,2,4,8,5], rows:[[en,pt,es,it,tr,pirate],...] }
// (ids = eLang). El original llama GetStr en cada sitio; aqui se traduce al dibujar (BitmapFont.Draw) por coincidencia exacta de texto.
// PlayerPrefs "SelectedLanguage" (mismo nombre que el original). El cambio es inmediato (el original pedia reiniciar).
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OpenFusion
{
    public static class Loc
    {
        static bool loaded; static int lang = -1, col = 0;
        static int[] ids = { 0 };
        static readonly Dictionary<string, string[]> exact = new Dictionary<string, string[]>();
        static readonly Dictionary<string, string[]> lower = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        public static int Lang { get { Ensure(); return lang; } }
        public static void SetLang(int id) { Ensure(); lang = id; PlayerPrefs.SetInt("SelectedLanguage", id); PlayerPrefs.Save(); col = IndexOf(id); cache.Clear(); }
        static int IndexOf(int id) { for (int i = 0; i < ids.Length; i++) if (ids[i] == id) return i; return 0; }

        static void Ensure()
        {
            if (loaded) return; loaded = true;
            lang = PlayerPrefs.GetInt("SelectedLanguage", 0);
            try
            {
                var ta = Resources.Load<TextAsset>("OpenFusion/UI/Data/guilang"); if (ta == null) { Debug.LogWarning("[loc] falta guilang.json"); return; }
                var j = JObject.Parse(ta.text); var ia = (JArray)j["ids"]; ids = new int[ia.Count]; for (int i = 0; i < ids.Length; i++) ids[i] = (int)ia[i];
                foreach (JArray r in (JArray)j["rows"])
                {
                    var v = new string[r.Count]; for (int i = 0; i < v.Length; i++) v[i] = (string)r[i];
                    if (string.IsNullOrEmpty(v[0])) continue; if (!exact.ContainsKey(v[0])) exact[v[0]] = v; if (!lower.ContainsKey(v[0])) lower[v[0]] = v;
                }
                col = IndexOf(lang);
            }
            catch (Exception e) { Debug.LogWarning("[loc] " + e.Message); }
        }

        /// <summary>TextManager.GetStr: devuelve la traduccion o el mismo texto.</summary>
        public static string T(string s)
        {
            if (string.IsNullOrEmpty(s)) return s; if (!loaded) Ensure(); if (col == 0) return s;
            string r; if (cache.TryGetValue(s, out r)) return r;
            r = Translate(s); if (cache.Count > 4000) cache.Clear(); cache[s] = r; return r;
        }
        static string Translate(string s)
        {
            string[] v;
            if (exact.TryGetValue(s, out v)) return Pick(v);
            string suffix = ""; string core = s;
            if (core.EndsWith(":")) { suffix = ":"; core = core.Substring(0, core.Length - 1); }
            if (core.Length != s.Length && exact.TryGetValue(core, out v)) return Pick(v) + suffix;
            if (lower.TryGetValue(core, out v))
            {
                string t = Pick(v); bool allUp = core == core.ToUpperInvariant(); return (allUp ? t.ToUpperInvariant() : t) + suffix;
            }
            return s;
        }
        static string Pick(string[] v) { return col < v.Length && !string.IsNullOrEmpty(v[col]) ? v[col] : v[0]; }
    }
}
