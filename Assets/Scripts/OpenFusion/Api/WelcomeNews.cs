using System;
using System.Collections.Generic;

namespace OpenFusion.Api
{
    public static class WelcomeNews
    {
        static bool Http(Uri uri) { return uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp; }
        public static List<string> Endpoints(string serverUrl, string mainFileUrl)
        {
            var urls = new List<string>();
            // ffrunner resolves relative PHP requests against the API endpoint,
            // independently of the CDN used by assetInfo.php / main.unity3d.
            if (Uri.TryCreate((serverUrl ?? "").TrimEnd('/') + "/", UriKind.Absolute, out var api) && Http(api))
                urls.Add(new Uri(api, "news.php").AbsoluteUri);
            if (Uri.TryCreate(mainFileUrl, UriKind.Absolute, out var main) && Http(main))
            {
                foreach (var path in new[] { "news.php", "/news.php" })
                { var url = new Uri(main, path).AbsoluteUri; if (!urls.Contains(url)) urls.Add(url); }
            }
            return urls;
        }
        public static List<string> Images(string newsUrl, string text)
        {
            var images = new List<string>();
            if (!Uri.TryCreate(newsUrl, UriKind.Absolute, out var basis) || !Http(basis)) return images;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith("#") || line.Contains("<") || line.Contains(">")) continue;
                if (Uri.TryCreate(basis, line, out var image) && Http(image) && !images.Contains(image.AbsoluteUri))
                    images.Add(image.AbsoluteUri);
                if (images.Count >= 16) break;
            }
            return images;
        }
    }
}
