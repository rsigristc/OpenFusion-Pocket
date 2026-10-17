// Cliente de la API de servidor (formato Retrobution/OpenFusion). Requiere Newtonsoft.Json
// (paquete com.unity.nuget.newtonsoft-json). SIN COMPILAR NI PROBAR contra el servidor real.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OpenFusion.Api
{
    public class ServerInfo
    {
        [JsonProperty("server_name")] public string ServerName;
        [JsonProperty("api_version")] public string ApiVersion;
        [JsonProperty("secure_apis_enabled")] public bool SecureApis;
        [JsonProperty("game_versions")] public List<string> GameVersions;
        [JsonProperty("login_address")] public string LoginAddress; // "ip:puerto"
        [JsonProperty("email_required")] public bool EmailRequired;
    }
    class RefreshResp { public string refresh_token; public string token; }
    class SessionResp { public string username; public string session_token; }
    public class CookieResp { public string username; public string cookie; public string expires; }

    public sealed class ServerApi
    {
        readonly HttpClient _h;
        readonly string _base;
        public ServerApi(string baseUrl, HttpMessageHandler handler = null) { _base = baseUrl.TrimEnd('/'); _h = handler == null ? new HttpClient() : new HttpClient(handler, false); _h.Timeout = TimeSpan.FromSeconds(20); }

        async Task<T> Get<T>(string path)
        {
            var url = _base + path;
            try { return JsonConvert.DeserializeObject<T>(await _h.GetStringAsync(url)); }
            catch (HttpRequestException e) { throw new HttpRequestException($"{e.Message} -> GET {url}", e); }
        }

        // Como el launcher: /versions/<uuid> y, si falla, /versions/<uuid>.json
        async Task<VersionManifest> FetchVersion(string uuid)
        {
            try { return await Get<VersionManifest>($"/versions/{uuid}.json"); }
            catch (HttpRequestException) { return await Get<VersionManifest>($"/versions/{uuid}"); }
        }

        public Task<ServerInfo> GetInfo() => Get<ServerInfo>("/");
        public Task<VersionManifest> GetVersion(string uuid) => FetchVersion(uuid);

        /// Cadena de versiones (la más nueva primero) siguiendo parent_uuid.
        sealed class SavedChain { public string source; public List<VersionManifest> chain; }
        public async Task<List<VersionManifest>> GetChain(string uuid, string cacheDirectory = null)
        {
            string cacheFile = cacheDirectory != null && Guid.TryParse(uuid, out _) ? Path.Combine(cacheDirectory, uuid + ".json") : null;
            if (cacheFile != null)
            {
                try
                {
                    var saved = await Task.Run(() => File.Exists(cacheFile) ? JsonConvert.DeserializeObject<SavedChain>(File.ReadAllText(cacheFile)) : null);
                    if (saved != null && saved.source == _base && saved.chain != null && saved.chain.Count > 0 && saved.chain[0].uuid == uuid && saved.chain.All(v => v != null && v.bundles != null)) return saved.chain;
                }
                catch (Exception) { } // Corrupt metadata is replaceable; download it again.
            }
            var l = new List<VersionManifest>();
            var seen = new HashSet<string>();
            while (!string.IsNullOrEmpty(uuid))
            {
                if (!seen.Add(uuid)) throw new InvalidDataException("Version manifest cycle: " + uuid);
                VersionManifest v;
                // la versión pedida es obligatoria; los padres son opcionales (el launcher solo los usa para copiar caché)
                try { v = await GetVersion(uuid); }
                catch (HttpRequestException) when (l.Count > 0) { break; }
                l.Add(v); uuid = v.parent_uuid;
            }
            if (cacheFile != null && l.Count > 0)
                try { await Task.Run(() => { Directory.CreateDirectory(cacheDirectory); File.WriteAllText(cacheFile, JsonConvert.SerializeObject(new SavedChain { source = _base, chain = l })); }); } catch (IOException) { }
            return l;
        }

        sealed class ApiException : Exception { public int Status; public ApiException(int s, string m) : base(m) { Status = s; } }

        async Task<string> Post(string path, object body, string bearer = null)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, _base + path)
            { Content = new StringContent(JsonConvert.SerializeObject(body ?? new { }), Encoding.UTF8, "application/json") };
            if (bearer != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var r = await _h.SendAsync(req); var text = await r.Content.ReadAsStringAsync();
            if (!r.IsSuccessStatusCode)
                throw new ApiException((int)r.StatusCode, r.StatusCode == System.Net.HttpStatusCode.Unauthorized && path == "/auth"
                    ? "Incorrect username or password (401)" : $"API {(int)r.StatusCode} en POST {path}: {text}");
            return text;
        }

        /// /auth -> /auth/session -> /cookie (como endpoint.rs del launcher). Devuelve (usuario, cookie) para REQ_LOGIN con iLoginType=2.
        /// /auth devuelve el token de refresco como TEXTO PLANO (no JSON); /auth/session devuelve {username, session_token}; /cookie, {username, cookie, expires}.
        public async Task<CookieResp> LoginSecure(string user, string pass)
        {
            var refresh = (await Post("/auth", new { username = user, password = pass })).Trim().Trim('"');
            var s = JsonConvert.DeserializeObject<SessionResp>(await Post("/auth/session", null, refresh));
            return JsonConvert.DeserializeObject<CookieResp>(await Post("/cookie", null, s.session_token));
        }
    }
}
