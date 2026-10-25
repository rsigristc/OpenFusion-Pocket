using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFusion.Screens
{
    // OpenFusionLauncher 2.3 protocol: ofl://server?description=...&endpoint=...
    public static class LauncherServerLink
    {
        public const string UpstreamVersion = "2.3.2";
        public static string NormalizeEndpoint(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            if (value.Any(char.IsWhiteSpace) || value.Any(char.IsControl) || value.Contains("\\")) return null;
            if (!value.Contains("://")) value = "https://" + value;
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http") || string.IsNullOrEmpty(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return null;
            return uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped).TrimEnd('/');
        }

        public static bool TryParse(string link, out ServerEntry server, out string error)
        {
            server = null; error = "Invalid OpenFusion server link.";
            if (string.IsNullOrEmpty(link) || link.Length > 4096 || link.Any(char.IsControl) ||
                !Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != "ofl" || uri.Host != "server" ||
                (uri.AbsolutePath != "" && uri.AbsolutePath != "/") || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || !uri.IsDefaultPort) return false;
            var args = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                foreach (var pair in uri.Query.TrimStart('?').Split('&'))
                {
                    if (pair.Length == 0) continue;
                    var parts = pair.Split(new[] { '=' }, 2);
                    string key = Uri.UnescapeDataString(parts[0].Replace("+", " "));
                    string val = parts.Length == 2 ? Uri.UnescapeDataString(parts[1].Replace("+", " ")) : "";
                    // Never import credentials, unknown launch arguments or ambiguous duplicate fields.
                    if ((key != "description" && key != "endpoint" && key != "ip" && key != "version") || args.ContainsKey(key) || val.Any(char.IsControl)) return false;
                    args.Add(key, val);
                }
            }
            catch (UriFormatException) { return false; }
            if (!args.TryGetValue("endpoint", out var endpoint) || string.IsNullOrEmpty(endpoint))
            {
                error = "This link uses a Simple Server. Pocket currently requires an API endpoint to load game assets.";
                return false;
            }
            if (args.ContainsKey("ip") || args.ContainsKey("version")) return false;
            endpoint = NormalizeEndpoint(endpoint);
            if (endpoint == null || endpoint.Length > 120) return false;
            args.TryGetValue("description", out var name);
            if (name != null && name.Length > 64) return false;
            server = new ServerEntry { endpoint = endpoint, description = string.IsNullOrWhiteSpace(name) ? "My OpenFusion Server" : name.Trim() };
            error = null; return true;
        }
    }
}
