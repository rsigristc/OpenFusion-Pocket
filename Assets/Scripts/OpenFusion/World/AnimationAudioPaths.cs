using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenFusion.World
{
    public static class AnimationAudioPaths
    {
        static readonly Regex Range = new Regex(@"\(RAND:(\d+)-(\d+)\)");
        public static IEnumerable<string> Variants(string path)
        {
            var match = Range.Match(path);
            if (!match.Success) { yield return path; yield break; }
            int min = int.Parse(match.Groups[1].Value), max = Math.Min(min + 32, int.Parse(match.Groups[2].Value));
            for (int i = min; i <= max; i++)
                foreach (var value in Variants(path.Substring(0, match.Index) + i + path.Substring(match.Index + match.Length))) yield return value;
        }
        public static string Choose(string data, Func<string, bool> available, int random)
        {
            string path = "sound/" + data;
            if (!path.Contains("(RAND:")) return path;
            var valid = new List<string>(); string first = null; int guard = 0;
            foreach (var candidate in Variants(path))
            {
                if (++guard > 256) break;
                if (first == null) first = candidate;
                if (available(candidate)) valid.Add(candidate);
            }
            return valid.Count > 0 ? valid[(random & int.MaxValue) % valid.Count] : first;
        }
    }
}
