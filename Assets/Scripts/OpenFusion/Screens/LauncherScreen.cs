// Launcher estilo OpenFusionLauncher (Tauri/Bootstrap dark) en IMGUI para Android. Sustituye al formulario World/MobileLogin.cs.
// Home (logo, tagline, tabla de servidores, Add/Edit/Delete/Connect), modales Add/Edit, Delete, Log In/Register, Forgot, About y Settings.
// Todo se dibuja en unidades de referencia (altura 681, UiKit.BeginTopLeft) con la fuente normal de IMGUI. Scroll tactil propio (arrastrar), sin GUI.ScrollView.
// La contrasena NUNCA se guarda (ni en disco ni en logs); "Remember Me" solo guarda el nombre de usuario por servidor en PlayerPrefs.
// SIN COMPILAR EN UNITY (solo comprobacion sintactica Roslyn).
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using OpenFusion.Api;

namespace OpenFusion.Screens
{
    public sealed class LauncherScreen : MonoBehaviour
    {
        /// <summary>true mientras el launcher esta en pantalla.</summary>
        public bool Visible { get; private set; }

        // ---------------------------------------------------------------- constantes
        const string ResPath = "OpenFusion/Launcher/";                       // Resources/OpenFusion/Launcher/<nombre>
        const string RetroUuid = "7d1a4c52-3b8e-4f6a-9c21-5e0b8a47d3f1";      // uuid fijo propio de Retrobution
        const string PrefSel = "of_launcher_sel";
        const string PrefUserPrefix = "of_launcher_user_";                    // + uuid del servidor -> nombre de usuario recordado
        const int NetTimeout = 10;                                            // s, como el launcher original
        const float RowH = 45f;

        // paleta (spec 1.12)
        static readonly Color CPrimary = Hex("#0d6efd"), CHeader = Hex("#34476e"), COdd = Hex("#24314d"), CEven = Hex("#34476e");
        static readonly Color CGlow = Hex("#0097fc"), CBorder = Hex("#90d1e4"), CInput = Hex("#0099ff"), CText = Hex("#dee2e6"), CModalText = Hex("#d5edf4");
        static readonly Color CHdrRow = Hex("#304165"), CRowA = Hex("#2b3750"), CRowB = Hex("#304164"), CRowSel = Hex("#2272e5"), CChip = Hex("#18794d"), CPlayers = Hex("#8fd203"), CRowBld = Hex("#34405a"), CRowBld2 = Hex("#34476e");
        static readonly Color COk = Hex("#a2ec00"), CTag = Hex("#4a76b7"), CTagShadow = Hex("#4349c4"), CGray = Hex("#9aa5b1"), CLink = Hex("#6ea8fe"), CRed = new Color(1f, 0f, 0f, 1f);

        enum Modal { None, AddEdit, Delete, Login, Forgot, About, Settings }
        enum Kind { Primary, Success, Danger, Warning, Dark }
        enum Page { Home, Settings }

        // ---------------------------------------------------------------- tipos internos
        sealed class ServersFile { public List<ServerEntry> servers = new List<ServerEntry>(); public List<string> favorites = new List<string>(); }
        sealed class VerBadge { public string uuid, name; }                  // name == null -> "unknown"
        sealed class Stat
        {
            public bool simple, loading = true, statusOk, infoOk, versionsDone; public int players; public string err;
            public string serverName, loginAddress; public bool secure, emailRequired;
            public List<VerBadge> versions = new List<VerBadge>();
        }
        sealed class Ctx { public bool secure, emailRequired; public string loginAddress, serverName; }
        sealed class Resp { public long Code; public string Text, Error; public bool Ok { get { return Error == null && Code >= 200 && Code < 300; } } }
        sealed class Scroller : ScrollMotion { public Vector2? tap, releaseTap; public int finger = -1, touchFrame = -1, releaseFrame = -1; public bool releaseDragged; }
        sealed class ToastItem { public string kind, text; public float expire; }
        sealed class MdBlock { public string rich, url; public int kind; public float indent, y, h; }
        sealed class MdDoc { public readonly List<MdBlock> blocks = new List<MdBlock>(); public float laidW = -1f, height; public int baseSize = 14; public Color color = Color.white; }

        // ---------------------------------------------------------------- estado
        TaskCompletionSource<LauncherResult> tcs;
        List<ServerEntry> servers = new List<ServerEntry>(); List<string> favorites = new List<string>();
        readonly Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
        static readonly Dictionary<string, string> verNames = new Dictionary<string, string>();   // uuid de version -> nombre
        int sel = -1; float lastTapT; int lastTapIdx = -1; bool thanks;
        Modal modal = Modal.None; int modalToken; bool busy, inputOn = true, confirmOpen, escBroken;
        readonly List<ToastItem> toasts = new List<ToastItem>();
        readonly Scroller scrTable = new Scroller(), scrAnn = new Scroller(), scrPage = new Scroller();
        Page page; int stab; bool clickSuppress; Prefs prefsApplied, prefsEdit;

        // Add/Edit
        string adUuid, adName = "", adEndpoint = "", adHost = ""; int adTab;
        // Login / Register
        int loginTab; string loginUser = "", loginPass = "", regUser = "", regPass = "", regConfirm = "", regEmail = "", formErr, forgotEmail = "";
        bool remember, emailReq; bool forgotBusy;
        MdDoc annDoc, confirmDoc; bool annLoading;

        // recursos / estilos (se crean en OnGUI)
        GUIStyle stLbl, stField, stMd; Texture2D tLogo, tBtnP, tBtnS, tBtnD, tBtnW, tModalBg, tModalFoot;

        // ---------------------------------------------------------------- API publica
        /// <summary>Muestra el launcher; completa cuando la persona pulsa Connect y el login esta validado.</summary>
        public Task<LauncherResult> Run()
        {
            if (tcs != null && !tcs.Task.IsCompleted) { Visible = true; return tcs.Task; }
            thanks = tcs != null && tcs.Task.IsCompleted;   // al volver del juego
            tcs = new TaskCompletionSource<LauncherResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            LoadServers(); modal = Modal.None; confirmOpen = false; busy = false; modalToken++; page = Page.Home; ApplyPrefs(LoadPrefs(), false);
            Visible = true; RefreshAll();
            return tcs.Task;
        }

        void OnDestroy()
        {
            foreach (var t in new[] { tBtnP, tBtnS, tBtnD, tBtnW, tModalBg, tModalFoot }) if (t != null) Destroy(t);
            if (tcs != null) tcs.TrySetCanceled();
        }

        // ---------------------------------------------------------------- Update: atras, inercia, toasts
        void Update()
        {
            if (!Visible) return;
            if (!escBroken)
            {
                try
                {
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                    {
                        if (confirmOpen) confirmOpen = false;
                        else if (modal != Modal.None) CloseModal();
                        else if (page == Page.Settings) page = Page.Home;
                        else sel = -1;
                    }
                }
                catch (Exception) { escBroken = true; }   // sin Input antiguo
            }
            Inertia(scrTable); Inertia(scrAnn); Inertia(scrPage);
            if (page == Page.Settings && stab == 2 && Time.unscaledTime >= nextCacheScan) RefreshBuildCache();
            { var cur = Sel(); foreach (var sv in servers) { float al; bgAlpha.TryGetValue(sv.uuid, out al); float tgt = page == Page.Home && sv == cur && Art(artBg, sv, false) != null ? 0.25f : 0f; bgAlpha[sv.uuid] = Mathf.MoveTowards(al, tgt, Time.unscaledDeltaTime * 0.5f); } }
            toasts.RemoveAll(t => Time.unscaledTime > t.expire);
        }
        static void Inertia(Scroller s) { s.Tick(Time.unscaledDeltaTime); }

        // ---------------------------------------------------------------- persistencia (servers.json)
        static string ServersPath { get { return Path.Combine(Application.persistentDataPath, "servers.json"); } }

        void LoadServers()
        {
            List<ServerEntry> list = null; favorites = new List<string>();
            try
            {
                string p = ServersPath;
                if (File.Exists(p))
                {
                    var f = JsonConvert.DeserializeObject<ServersFile>(File.ReadAllText(p));
                    if (f != null && f.servers != null) { list = f.servers.FindAll(x => x != null); if (f.favorites != null) favorites = f.favorites; }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[launcher] servers.json: " + ex.Message); }
            bool fresh = list == null;
            if (fresh) list = DefaultServers();
            foreach (var x in list) if (string.IsNullOrEmpty(x.uuid)) x.uuid = Guid.NewGuid().ToString();
            servers = list;
            bool mig = false;   // listas guardadas por versiones anteriores: mismo nombre/orden que el launcher original
            foreach (var x in servers) if (x.uuid == RetroUuid && x.description == "Retrobution") { x.description = "FusionFall Retrobution"; mig = true; }
            if (PlayerPrefs.GetInt("of_launcher_order", 0) == 0)
            {
                PlayerPrefs.SetInt("of_launcher_order", 1); mig = true;
                if (servers.Count > 1 && servers[0].uuid == RetroUuid) { var r0 = servers[0]; servers.RemoveAt(0); servers.Add(r0); }
            }
            if (fresh || mig) SaveServers();
            string want = PlayerPrefs.GetString(PrefSel, "");
            sel = servers.FindIndex(x => x.uuid == want);
            if (sel < 0 && servers.Count > 0) { sel = servers.FindIndex(x => x.uuid == RetroUuid); if (sel < 0) sel = 0; }   // por defecto el primero (Retrobution en un arranque limpio)
        }

        static List<ServerEntry> DefaultServers()
        {
            return new List<ServerEntry>
            {
                new ServerEntry { uuid = "d9773558-6ddf-4d00-8803-d830a22a842b", description = "OpenFusion Public - Original", endpoint = "api.dexlabs.systems" },
                new ServerEntry { uuid = "b84f6859-f500-41f2-bdfa-8e3746639ee9", description = "OpenFusion Public - Academy", endpoint = "api.dexlabs.systems/academy" },
                new ServerEntry { uuid = RetroUuid, description = "FusionFall Retrobution", endpoint = "api.ffretrobution.net" },
            };
        }

        void SaveServers()
        {
            try
            {
                var f = new ServersFile { servers = servers, favorites = favorites };
                File.WriteAllText(ServersPath, JsonConvert.SerializeObject(f, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            }
            catch (Exception ex) { Debug.LogWarning("[launcher] no se pudo guardar servers.json: " + ex.Message); }
        }

        void SaveSel() { var s = Sel(); PlayerPrefs.SetString(PrefSel, s != null ? s.uuid : ""); PlayerPrefs.Save(); }
        ServerEntry Sel() { return sel >= 0 && sel < servers.Count ? servers[sel] : null; }
        static string UserKey(ServerEntry s) { return PrefUserPrefix + s.uuid; }
        static string ApiUrl(ServerEntry s)
        {
            var e = (s.endpoint ?? "").Trim().TrimEnd('/');
            return e.StartsWith("http://") || e.StartsWith("https://") ? e : "https://" + e;
        }
        static bool IsEndpoint(ServerEntry s) { return s != null && !string.IsNullOrEmpty(s.endpoint); }

        // ---------------------------------------------------------------- red (UnityWebRequest)
        /// <summary>Peticion HTTP con timeout; nunca lanza. Completa en el hilo principal.</summary>
        static Task<Resp> Send(string method, string url, string jsonBody, int timeoutSec)
        {
            var src = new TaskCompletionSource<Resp>();
            UnityWebRequest req;
            try
            {
                req = new UnityWebRequest(url, method);
                req.downloadHandler = new DownloadHandlerBuffer();
                if (jsonBody != null) { req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)); req.SetRequestHeader("Content-Type", "application/json"); }
                req.timeout = timeoutSec;
            }
            catch (Exception e) { src.TrySetResult(new Resp { Code = 0, Error = e.Message }); return src.Task; }
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                var r = new Resp { Code = req.responseCode };
                try { r.Text = req.downloadHandler != null ? req.downloadHandler.text : null; } catch (Exception) { }
                if (req.result != UnityWebRequest.Result.Success) r.Error = string.IsNullOrEmpty(req.error) ? "request failed" : req.error;
                req.Dispose();
                src.TrySetResult(r);
            };
            return src.Task;
        }

        /// <summary>GET de texto (lanza Exception si falla o no es 2xx).</summary>
        static async Task<string> GetText(string url, int timeoutSec)
        {
            var r = await Send("GET", url, null, timeoutSec);
            if (!r.Ok) throw new Exception(r.Error ?? ("HTTP " + r.Code));
            return r.Text ?? "";
        }

        static Ctx ParseInfo(string json)
        {
            var j = JObject.Parse(json);
            var c = new Ctx { serverName = (string)j["server_name"], loginAddress = (string)j["login_address"] };
            JToken t = j["secure_apis_enabled"]; c.secure = t != null && t.Type == JTokenType.Boolean && (bool)t;
            t = j["email_required"]; c.emailRequired = t != null && t.Type == JTokenType.Boolean && (bool)t;
            return c;
        }
        static List<string> ParseVersionIds(string json)
        {
            var l = new List<string>(); var j = JObject.Parse(json);
            var arr = j["game_versions"] as JArray;
            if (arr != null) { foreach (var v in arr) { var s = (string)v; if (!string.IsNullOrEmpty(s)) l.Add(s); } }
            else { var one = (string)j["game_version"]; if (!string.IsNullOrEmpty(one)) l.Add(one); }
            return l;
        }

        // ---------------------------------------------------------------- estado de cada servidor
        void RefreshAll() { foreach (var s in servers.ToArray()) { var _t = RefreshServer(s); } }

        async Task RefreshServer(ServerEntry s)
        {
            var st = new Stat { simple = !IsEndpoint(s) }; stats[s.uuid] = st;
            if (st.simple) { st.loading = false; st.versionsDone = true; return; }
            string b = ApiUrl(s);
            var tInfo = GetText(b + "/", NetTimeout); var tStat = GetText(b + "/status", NetTimeout);   // en paralelo
            try { var j = JObject.Parse(await tStat); st.players = (int?)j["player_count"] ?? 0; st.statusOk = true; }
            catch (Exception e) { st.statusOk = false; st.err = e.Message; }
            string info = null;
            try { info = await tInfo; var c = ParseInfo(info); st.serverName = c.serverName; st.loginAddress = c.loginAddress; st.secure = c.secure; st.emailRequired = c.emailRequired; st.infoOk = true; }
            catch (Exception e) { st.infoOk = false; if (st.err == null) st.err = e.Message; }
            st.loading = false;
            if (st.infoOk)
            {
                try
                {
                    var ids = ParseVersionIds(info); int n = 0;
                    foreach (var id in ids)
                    {
                        if (n++ >= 6) break;
                        string name = await VersionName(b, id);
                        st.versions.Add(new VerBadge { uuid = id, name = name });
                    }
                }
                catch (Exception e) { Debug.LogWarning("[launcher] versiones: " + e.Message); }
            }
            st.versionsDone = true;
        }

        /// <summary>Nombre de una version (GET /versions/&lt;uuid&gt;.json y, si falla, /versions/&lt;uuid&gt;). null si no se pudo obtener.</summary>
        static async Task<string> VersionName(string baseUrl, string uuid)
        {
            string n; if (verNames.TryGetValue(uuid, out n)) return n;
            n = OpenFusion.Api.KnownGameVersions.NameFor(uuid); if (n != null) return n;
            foreach (var suffix in new[] { ".json", "" })
            {
                try
                {
                    var j = JObject.Parse(await GetText(baseUrl + "/versions/" + uuid + suffix, NetTimeout));
                    n = (string)j["name"]; if (string.IsNullOrEmpty(n)) n = uuid;
                    verNames[uuid] = n; return n;
                }
                catch (Exception) { }
            }
            return null;
        }

        /// <summary>Informacion de la API de un servidor (reutiliza la ya cargada si existe).</summary>
        async Task<Ctx> FetchCtx(ServerEntry s)
        {
            Stat st;
            if (stats.TryGetValue(s.uuid, out st) && st.infoOk) return new Ctx { secure = st.secure, emailRequired = st.emailRequired, loginAddress = st.loginAddress, serverName = st.serverName };
            return ParseInfo(await GetText(ApiUrl(s) + "/", NetTimeout));
        }

        // ---------------------------------------------------------------- acciones
        void Toast(string kind, string text) { toasts.Add(new ToastItem { kind = kind, text = text, expire = kind == "success" ? Time.unscaledTime + 5f : float.MaxValue }); if (toasts.Count > 5) toasts.RemoveAt(0); }

        void CloseModal() { modal = Modal.None; confirmOpen = false; busy = false; forgotBusy = false; modalToken++; GUI.FocusControl(null); regPass = ""; regConfirm = ""; loginPass = ""; }

        void OnConnect() { var s = Sel(); if (s != null) OpenLogin(s); }

        void OpenLogin(ServerEntry s)
        {
            modal = Modal.Login; modalToken++; loginTab = 0; confirmOpen = false; formErr = null; busy = false;
            loginUser = PlayerPrefs.GetString(UserKey(s), ""); remember = PlayerPrefs.HasKey(UserKey(s)); loginPass = "";
            regUser = ""; regPass = ""; regConfirm = ""; regEmail = ""; annDoc = null; scrAnn.y = 0f; emailReq = false;
            if (IsEndpoint(s)) { var _a = LoadAnn(s, modalToken); var _c = LoadCtx(s, modalToken); }
        }

        async Task LoadAnn(ServerEntry s, int token)
        {
            annLoading = true; string md = null;
            try { md = await GetText(ApiUrl(s) + "/announcements.md", NetTimeout); } catch (Exception) { }
            if (this == null || token != modalToken) return;
            annLoading = false;
            annDoc = ParseMd(string.IsNullOrWhiteSpace(md) ? "This server has no announcements." : md); annDoc.baseSize = 13;
        }
        async Task LoadCtx(ServerEntry s, int token)
        {
            try { var c = await FetchCtx(s); if (this != null && token == modalToken) emailReq = c.emailRequired; } catch (Exception) { }
        }

        static string LoginError(Exception e)
        {
            var m = e is AggregateException && e.InnerException != null ? e.InnerException.Message : e.Message;
            if (m.IndexOf("incorrect", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("401", StringComparison.Ordinal) >= 0) return "Incorrect username or password";
            return "Failed to login (" + m + ")";
        }

        /// <summary>Valida las credenciales (LoginSecure si el servidor usa APIs seguras) y entrega el resultado al GameClient.</summary>
        async Task DoLogin(ServerEntry s, string user, string pass)
        {
            int token = modalToken; busy = true; formErr = null;
            try
            {
                string url = "", addr = s.ip;
                if (IsEndpoint(s))
                {
                    url = ApiUrl(s); Ctx c;
                    try { c = await FetchCtx(s); } catch (Exception) { if (token == modalToken) formErr = "Failed to contact API server"; return; }
                    if (token != modalToken) return;
                    addr = c.loginAddress;
                    if (c.secure)
                    {
                        try
                        {
                            var task = new ServerApi(url).LoginSecure(user, pass);
                            var done = await Task.WhenAny(task, Task.Delay(20000));
                            if (done != task) throw new TimeoutException("Failed to contact API server (timeout)");
                            var ck = await task;
                            long exp;
                            if (ck != null && long.TryParse(ck.expires, out exp) && exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) throw new Exception("Cookie expired; try syncing your system clock");
                        }
                        catch (Exception e) { if (token == modalToken) formErr = LoginError(e); return; }
                    }
                }
                if (this == null || token != modalToken) return;
                // solo se recuerda el NOMBRE de usuario; la contrasena no se guarda en ningun sitio
                if (remember) PlayerPrefs.SetString(UserKey(s), user); else PlayerPrefs.DeleteKey(UserKey(s));
                PlayerPrefs.Save();
                var res = new LauncherResult { ServerUrl = url, ServerId = s.uuid, ServerName = s.description, Username = user, Password = pass, LoginAddress = addr, VersionUuid = s.version };
                loginPass = ""; regPass = ""; regConfirm = "";
                modal = Modal.None; modalToken++; Visible = false; thanks = false;
                if (tcs != null) tcs.TrySetResult(res);
            }
            finally { if (token == modalToken || modal == Modal.None) busy = false; }
        }

        async Task DoRegister(ServerEntry s)
        {
            int token = modalToken; busy = true; formErr = null; bool relogin = false; string u = regUser, p = regPass;
            try
            {
                string url = ApiUrl(s); string em = string.IsNullOrWhiteSpace(regEmail) ? null : regEmail.Trim();
                string body = JsonConvert.SerializeObject(new { username = u, password = p, email = em });
                var r = await Send("POST", url + "/account/register", body, NetTimeout);
                if (this == null || token != modalToken) return;
                if (!r.Ok)
                {
                    string msg = r.Error != null && r.Code == 0 ? r.Error : "API error " + r.Code + ": " + (r.Text ?? "") + " [" + url + "/account/register]";
                    formErr = "Failed to register (" + msg + ")"; return;
                }
                string text = string.IsNullOrWhiteSpace(r.Text) ? "Account created" : r.Text;
                if (r.Code == 201) { Toast("success", text); relogin = true; }
                else { Toast("info", text); loginUser = u; loginTab = 0; }
                regUser = ""; regPass = ""; regConfirm = ""; regEmail = "";
            }
            finally { busy = false; }
            if (relogin && this != null && token == modalToken) { loginUser = u; loginPass = p; await DoLogin(s, u, p); }
        }

        async Task DoForgot(ServerEntry s)
        {
            int token = modalToken; forgotBusy = true;
            try
            {
                var r = await Send("POST", ApiUrl(s) + "/account/otp", JsonConvert.SerializeObject(new { email = forgotEmail.Trim() }), NetTimeout);
                if (this == null) return;
                if (r.Ok) { Toast("success", "One-time password sent"); if (token == modalToken) CloseModal(); }
                else Toast("error", "Failed to send one-time password (" + (r.Error ?? ("API error " + r.Code + ": " + r.Text)) + ")");
            }
            finally { forgotBusy = false; }
        }

        // ---------------------------------------------------------------- validacion (spec 1.3 / 1.5)
        static string NormEndpoint(string s)
        {
            s = (s ?? "").Trim();
            if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) s = s.Substring(8); else if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            return s;
        }
        static bool ValidEndpoint(string raw)
        {
            string s = NormEndpoint(raw); if (s.Length == 0) return false;
            int i = s.IndexOf('/'); string host = i < 0 ? s : s.Substring(0, i), path = i < 0 ? null : s.Substring(i);
            if (!Regex.IsMatch(host, "^[a-z0-9.-]+$", RegexOptions.IgnoreCase)) return false;
            if (path != null && (path.Length < 2 || path.EndsWith("/") || path.IndexOf(' ') >= 0)) return false;
            return true;
        }
        /// <summary>host[:puerto]; vacio = localhost. Devuelve null si es invalido; si no, "host:puerto" (puerto por defecto 23000).</summary>
        static string NormHost(string raw)
        {
            string s = (raw ?? "").Trim(); if (s.Length == 0) s = "localhost";
            var m = Regex.Match(s, @"^([a-z0-9.-]+)(?::(\d{1,5}))?$", RegexOptions.IgnoreCase); if (!m.Success) return null;
            int port = 23000; if (m.Groups[2].Success && (!int.TryParse(m.Groups[2].Value, out port) || port < 1 || port > 65535)) return null;
            return m.Groups[1].Value + ":" + port;
        }
        static bool ValidUser(string s) { return Regex.IsMatch(s ?? "", "^[a-zA-Z0-9_-]{4,32}$"); }
        static bool ValidPass(string s) { return s != null && s.Length >= 8 && s.Length <= 32; }
        static bool ValidEmail(string s) { return Regex.IsMatch(s ?? "", @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"); }

        void OpenAddEdit(ServerEntry edit)
        {
            modal = Modal.AddEdit; modalToken++; adUuid = edit != null ? edit.uuid : null;
            adName = edit != null ? (edit.description ?? "") : ""; adEndpoint = edit != null ? (edit.endpoint ?? "") : ""; adHost = edit != null ? (edit.ip ?? "") : "";
            adTab = edit != null && !IsEndpoint(edit) ? 0 : 1;
        }

        void SaveAddEdit()
        {
            string name = string.IsNullOrWhiteSpace(adName) ? "My OpenFusion Server" : adName.Trim();
            var e = new ServerEntry { uuid = adUuid ?? Guid.NewGuid().ToString(), description = name };
            if (adTab == 1) e.endpoint = NormEndpoint(adEndpoint).TrimEnd('/'); else e.ip = NormHost(adHost);   // al guardar se descarta lo otro
            if (adUuid == null) { servers.Add(e); sel = servers.Count - 1; Toast("success", "Server added"); }
            else { int i = servers.FindIndex(x => x.uuid == adUuid); if (i >= 0) { servers[i] = e; Toast("success", "Server updated"); } }
            SaveServers(); SaveSel(); var _t = RefreshServer(e); CloseModal();
        }

        void DeleteSelected()
        {
            var s = Sel(); if (s == null) return;
            servers.Remove(s); favorites.Remove(s.uuid); stats.Remove(s.uuid);
            sel = servers.Count == 0 ? -1 : Mathf.Min(sel, servers.Count - 1);
            SaveServers(); SaveSel(); Toast("success", "Server deleted"); CloseModal();
        }

        // ================================================================ DIBUJO
        static Color Hex(string h) { Color c; return ColorUtility.TryParseHtmlString(h, out c) ? c : Color.magenta; }
        static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

        void InitGui()
        {
            if (stLbl != null) return;
            stLbl = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            stMd = new GUIStyle(stLbl) { wordWrap = true, clipping = TextClipping.Overflow, alignment = TextAnchor.UpperLeft };
            stField = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false, padding = new RectOffset(8, 8, 4, 4) };
            foreach (var st in new[] { stField.normal, stField.hover, stField.focused, stField.active, stField.onNormal, stField.onHover, stField.onFocused, stField.onActive }) { st.background = null; st.textColor = Color.white; }
            tLogo = UiKit.TexPath_(ResPath + "of-dark");
            // tiles generados en codigo con los colores del spec (los PNG NPOT se reescalarian al importar)
            tBtnP = Tile(Hex("#1c4970"), Hex("#0f283e")); tBtnS = Tile(Hex("#5f8e00"), Hex("#385400")); tBtnD = Tile(Hex("#700000"), Hex("#510000")); tBtnW = Tile(Hex("#eda413"), Hex("#ab7d20"));
            tModalBg = Tile(Hex("#00264c"), Hex("#003366"), Hex("#003366")); tModalFoot = Tile(Hex("#042647"), Hex("#001b24"), Hex("#001b24"));
        }

        /// <summary>Tile de 1xN (filas de arriba a abajo) con wrap Repeat y filtro Point.</summary>
        static Texture2D Tile(params Color[] rowsTopToBottom)
        {
            int h = rowsTopToBottom.Length; var t = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < h; i++) t.SetPixel(0, h - 1 - i, rowsTopToBottom[i]);
            t.Apply(false, false); return t;
        }
        static void DrawTile(Rect r, Texture2D t) { if (t != null && Event.current.type == EventType.Repaint) GUI.DrawTextureWithTexCoords(r, t, new Rect(0f, 0f, 1f, r.height / t.height)); }

        // ---- texto
        void Lbl(Rect r, string t, int size, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false, bool wrap = false)
        {
            if (string.IsNullOrEmpty(t)) return;
            var st = stLbl; st.fontSize = size; st.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; st.alignment = a; st.wordWrap = wrap; st.clipping = wrap ? TextClipping.Overflow : TextClipping.Clip; st.normal.textColor = c;
            GUI.Label(r, t, st);
        }
        float TextW(string t, int size, bool bold)
        {
            var st = stLbl; st.fontSize = size; st.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; st.wordWrap = false;
            return st.CalcSize(new GUIContent(t)).x;
        }
        float WrapH(string t, int size, float w)
        {
            var st = stLbl; st.fontSize = size; st.fontStyle = FontStyle.Normal; st.wordWrap = true;
            return st.CalcHeight(new GUIContent(t), w);
        }

        // ---- botones / toques (respetan inputOn: los controles de debajo de un modal no reciben eventos)
        bool Tap(Rect r, bool enabled = true)
        {
            var e = Event.current; int id = GUIUtility.GetControlID(FocusType.Passive, r); bool on = enabled && inputOn, hov = r.Contains(e.mousePosition), res = false;
            if (!on) return false;
            if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; if (hov && !clickSuppress) res = true; e.Use(); }
            return res;
        }

        bool Btn(Rect r, string text, Kind k, bool enabled = true, int size = 17, string icon = null, bool iconLeft = false)
        {
            var e = Event.current; int id = GUIUtility.GetControlID(FocusType.Passive, r); bool on = enabled && inputOn, hov = r.Contains(e.mousePosition), res = false;
            if (on)
            {
                if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; if (hov && !clickSuppress) res = true; e.Use(); }
            }
            if (e.type == EventType.Repaint)
            {
                var oc = GUI.color; if (!enabled) GUI.color = new Color(oc.r, oc.g, oc.b, oc.a * 0.65f);
                Color glow;
                if (k == Kind.Dark) { UiKit.Fill(r, Color.black); glow = CInput; UiKit.Outline(r, glow, 1.5f); }
                else
                {
                    Texture2D tile;
                    switch (k) { case Kind.Success: tile = tBtnS; glow = COk; break; case Kind.Danger: tile = tBtnD; glow = CRed; break; case Kind.Warning: tile = tBtnW; glow = Color.yellow; break; default: tile = tBtnP; glow = Hex("#2a9fd6"); break; }
                    DrawTile(r, tile);
                    UiKit.Outline(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), A(glow, 0.5f), 2f);
                    UiKit.Outline(r, A(glow, 0.9f), 1f);
                }
                if (on && GUIUtility.hotControl == id && hov) UiKit.Fill(r, new Color(1f, 1f, 1f, 0.15f));
                BtnContent(r, text, size, icon, iconLeft, Hex("#f8f9fa"));
                GUI.color = oc;
            }
            return res;
        }

        void BtnContent(Rect r, string text, int size, string icon, bool iconLeft, Color col)
        {
            if (string.IsNullOrEmpty(icon)) { Lbl(r, text, size, col, TextAnchor.MiddleCenter, false); return; }
            float isz = size * 0.95f;
            if (string.IsNullOrEmpty(text)) { DrawIco(new Rect(r.center.x - isz * 0.5f, r.center.y - isz * 0.5f, isz, isz), icon, col); return; }
            float tw = TextW(text, size, false), gap = 6f, x = r.center.x - (tw + gap + isz) * 0.5f;
            if (iconLeft) { DrawIco(new Rect(x, r.center.y - isz * 0.5f, isz, isz), icon, col); Lbl(new Rect(x + isz + gap, r.y, tw + 4f, r.height), text, size, col, TextAnchor.MiddleLeft, false); }
            else { Lbl(new Rect(x, r.y, tw + 4f, r.height), text, size, col, TextAnchor.MiddleLeft, false); DrawIco(new Rect(x + tw + gap, r.center.y - isz * 0.5f, isz, isz), icon, col); }
        }

        // ---- campos
        string Input(Rect r, string val, int max, string name, bool pass = false, bool invalid = false, string placeholder = null, int size = 17)
        {
            UiKit.Fill(r, Color.black); UiKit.Outline(r, invalid ? CRed : CInput, 1.5f);
            stField.fontSize = size; GUI.SetNextControlName(name);
            string v = pass ? GUI.PasswordField(r, val ?? "", '*', max, stField) : GUI.TextField(r, val ?? "", max, stField);
            if (placeholder != null && string.IsNullOrEmpty(v) && GUI.GetNameOfFocusedControl() != name) Lbl(new Rect(r.x + 8, r.y, r.width - 16, r.height), placeholder, size, A(CGray, 0.7f));
            return v;
        }
        string FieldRow(Rect r, string label, string val, int max, string name, bool pass, bool invalid, string ph = null)
        {
            Lbl(new Rect(r.x, r.y, r.width, 18), label, 13, CModalText);
            return Input(new Rect(r.x, r.y + 18, r.width, 40), val, max, name, pass, invalid, ph);
        }

        // ---- scroll tactil (arrastrar). Devuelve el rect de contenido (origen 0,0); cerrar con ScrollEnd()
        Rect ScrollBegin(Scroller s, Rect view, float contentH)
        {
            var e = Event.current; s.tap = null; s.Bounds(contentH, view.height);
            bool touch = UnityEngine.Input.touchCount > 0 || s.finger >= 0 || s.releaseFrame == Time.frameCount;
            if (touch && inputOn)
            {
                if (s.touchFrame != Time.frameCount)
                {
                    s.touchFrame = Time.frameCount; bool found = false;
                    for (int i = 0; i < UnityEngine.Input.touchCount; i++)
                    {
                        var finger = UnityEngine.Input.GetTouch(i);
                        var position = new Vector2(finger.position.x / UiKit.S, (Screen.height - finger.position.y) / UiKit.S);
                        if (finger.phase == TouchPhase.Began && s.finger < 0 && view.Contains(position))
                        { s.finger = finger.fingerId; s.Begin(position, Time.unscaledTime); }
                        if (s.finger != finger.fingerId) continue;
                        found = true; s.Drag(position, Time.unscaledTime);
                        if (s.dragging) GUIUtility.hotControl = 0;
                        if (finger.phase == TouchPhase.Ended || finger.phase == TouchPhase.Canceled)
                        {
                            s.releaseFrame = Time.frameCount; s.releaseDragged = s.dragging || finger.phase == TouchPhase.Canceled;
                            s.releaseTap = new Vector2(position.x - view.x, position.y - view.y + s.y);
                            s.End(Time.unscaledTime); s.dragging = false; s.finger = -1;
                        }
                    }
                    if (s.finger >= 0 && !found) { s.End(Time.unscaledTime); s.dragging = false; s.finger = -1; }
                }
                bool suppress = s.dragging || s.releaseFrame == Time.frameCount && s.releaseDragged;
                if (suppress && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp || e.type == EventType.MouseDown)) { clickSuppress = true; e.Use(); }
                if (!suppress && s.releaseFrame == Time.frameCount && e.type == EventType.MouseUp) s.tap = s.releaseTap;
            }
            if (!touch && inputOn)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && view.Contains(e.mousePosition)) { s.Begin(e.mousePosition, Time.unscaledTime); }
                else if (e.type == EventType.MouseDrag && s.down)
                {
                    s.Drag(e.mousePosition, Time.unscaledTime);
                    if (s.dragging) { GUIUtility.hotControl = 0; e.Use(); }
                }
                else if (e.type == EventType.MouseUp && s.down)
                {
                    clickSuppress = s.dragging; s.down = false;
                    if (!s.dragging && view.Contains(e.mousePosition)) s.tap = new Vector2(e.mousePosition.x - view.x, e.mousePosition.y - view.y + s.y);
                    if (Time.unscaledTime - s.lastMove > 0.08f) s.vel = 0f;
                    s.dragging = false;
                }
                else if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) { s.y += e.delta.y * 24f; s.vel = 0f; e.Use(); }
            }
            else if (!inputOn) { s.down = false; s.finger = -1; }
            s.y = Mathf.Clamp(s.y, 0f, s.maxY);
            GUI.BeginGroup(view); GUI.BeginGroup(new Rect(0f, -s.y, view.width, Mathf.Max(contentH, view.height)));
            return new Rect(0f, 0f, view.width, contentH);
        }
        static void ScrollEnd() { GUI.EndGroup(); GUI.EndGroup(); }

        // ---- area segura en unidades de referencia
        static Rect SafeArea()
        {
            var sa = Screen.safeArea; float s = UiKit.S;
            var r = Rect.MinMaxRect(sa.xMin / s, (Screen.height - sa.yMax) / s, sa.xMax / s, (Screen.height - sa.yMin) / s);
            return new Rect(r.x + 6f, r.y + 4f, r.width - 12f, r.height - 8f);
        }

        // ================================================================ OnGUI
        void OnGUI()
        {
            if (!Visible) return;
            InitGui();
            UiKit.BeginTopLeft();
            try
            {
                var sz = UiKit.RefSize; UiKit.Fill(new Rect(0, 0, sz.x, sz.y), Color.black);
                var a = SafeArea();
                if (Event.current.type == EventType.Layout) clickSuppress = false;
                inputOn = modal == Modal.None; if (page == Page.Settings) DrawSettingsPage(a); else DrawHome(a);
                inputOn = true;
                switch (modal)
                {
                    case Modal.AddEdit: DrawAddEdit(a); break;
                    case Modal.Delete: DrawDelete(a); break;
                    case Modal.Login: DrawLogin(a); break;
                    case Modal.Forgot: DrawForgot(a); break;
                    case Modal.About: DrawAbout(a); break;
                }
                inputOn = true; DrawToasts(a);
            }
            finally { UiKit.End(); }
        }

        // ---------------------------------------------------------------- arte por servidor (fondo / logo) e iconos
        // Fondo y logo: /launcher/background.png y /launcher/logo.png del endpoint (como el launcher original). Se incluyen copias de los tres servidores por defecto
        // en Resources/OpenFusion/Launcher y se refrescan desde la red (cache en persistentDataPath/launcher_art).
        readonly Dictionary<string, Texture2D> artBg = new Dictionary<string, Texture2D>(), artLogo = new Dictionary<string, Texture2D>(), icons = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, float> bgAlpha = new Dictionary<string, float>();
        static Font monoFont; static bool monoTried;

        static string BundledArt(ServerEntry s, bool logo)
        {
            string e = (s != null ? (s.endpoint ?? "") : "").ToLowerInvariant();
            if (e.Contains("ffretrobution")) return logo ? "logo_retrobution" : "bg_retrobution";
            if (e.Contains("dexlabs.systems/academy")) return logo ? null : "bg_academy";
            if (e.Contains("dexlabs.systems")) return logo ? null : "bg_original";
            return null;
        }
        static string ArtPath(ServerEntry s, bool logo) { return Path.Combine(Path.Combine(Application.persistentDataPath, "launcher_art"), s.uuid + (logo ? "_logo.png" : "_bg.png")); }

        Texture2D Art(Dictionary<string, Texture2D> cache, ServerEntry s, bool logo)
        {
            if (s == null || string.IsNullOrEmpty(s.uuid)) return null;
            Texture2D t; if (cache.TryGetValue(s.uuid, out t)) return t;
            cache[s.uuid] = null;
            string bk = BundledArt(s, logo); if (bk != null) t = LoadBundled(bk);
            try
            {
                string p = ArtPath(s, logo);
                if (File.Exists(p)) { var d = new Texture2D(2, 2, TextureFormat.RGBA32, false); if (d.LoadImage(File.ReadAllBytes(p), false)) t = d; else Destroy(d); }
            }
            catch (Exception) { }
            cache[s.uuid] = t;
            if (IsEndpoint(s)) { var _f = FetchArt(s, logo); }
            return t;
        }

        /// <summary>PNG incluido como TextAsset (.bytes): se decodifica sin el reescalado a potencia de 2 del importador de texturas.</summary>
        static Texture2D LoadBundled(string name)
        {
            var ta = Resources.Load<TextAsset>(ResPath + name); if (ta == null) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false); bool ok = t.LoadImage(ta.bytes, false);
            Resources.UnloadAsset(ta); if (!ok) { Destroy(t); return null; }
            return t;
        }

        async Task FetchArt(ServerEntry s, bool logo)
        {
            try
            {
                var bytes = await GetBytes(ApiUrl(s) + "/launcher/" + (logo ? "logo" : "background") + ".png", NetTimeout);
                if (this == null || bytes == null || bytes.Length < 100) return;
                var d = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!d.LoadImage(bytes, false)) { Destroy(d); return; }
                (logo ? artLogo : artBg)[s.uuid] = d;
                try { var p = ArtPath(s, logo); Directory.CreateDirectory(Path.GetDirectoryName(p)); File.WriteAllBytes(p, bytes); } catch (Exception) { }
            }
            catch (Exception) { }
        }

        static Task<byte[]> GetBytes(string url, int timeoutSec)
        {
            var src = new TaskCompletionSource<byte[]>();
            UnityWebRequest req;
            try { req = UnityWebRequest.Get(url); req.timeout = timeoutSec; }
            catch (Exception) { src.TrySetResult(null); return src.Task; }
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                byte[] b = null;
                try { if (req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300) b = req.downloadHandler.data; } catch (Exception) { }
                req.Dispose(); src.TrySetResult(b);
            };
            return src.Task;
        }

        Texture2D Ico(string n)
        {
            Texture2D t; if (icons.TryGetValue(n, out t)) return t;
            t = Resources.Load<Texture2D>(ResPath + "Icons/ic_" + n); icons[n] = t; return t;
        }
        void DrawIco(Rect r, string n, Color c)
        {
            var t = Ico(n); if (t == null || Event.current.type != EventType.Repaint) return;
            var o = GUI.color; GUI.color = c * o; GUI.DrawTexture(r, t, ScaleMode.ScaleToFit, true); GUI.color = o;
        }

        static Font Mono
        {
            get
            {
                if (!monoTried) { monoTried = true; try { monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Roboto Mono", "Droid Sans Mono", "Courier New", "monospace" }, 16); } catch (Exception) { monoFont = null; } }
                return monoFont;
            }
        }
        void LblM(Rect r, string t, int size, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false)
        { var f = stLbl.font; stLbl.font = Mono; Lbl(r, t, size, c, a, bold); stLbl.font = f; }
        float TextWM(string t, int size, bool bold) { var f = stLbl.font; stLbl.font = Mono; float w = TextW(t, size, bold); stLbl.font = f; return w; }

        void DrawBackgrounds(Vector2 sz)
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (var s in servers)
            {
                float al; if (!bgAlpha.TryGetValue(s.uuid, out al) || al <= 0.002f) continue;
                var t = Art(artBg, s, false); if (t == null) continue;
                UiKit.Img(new Rect(0f, 0f, sz.x, sz.y), t, new Color(1f, 1f, 1f, al), ScaleMode.ScaleAndCrop);
            }
        }

        // ---------------------------------------------------------------- Home
        void DrawHome(Rect a)
        {
            var sz = UiKit.RefSize; DrawBackgrounds(sz);
            float tw = Mathf.Min(a.width - 12f, Mathf.Clamp(a.width * 0.58f, 600f, 860f)), cx = a.center.x, x0 = cx - tw * 0.5f;
            var cur = Sel(); var logo = Art(artLogo, cur, true);
            float y = a.y + 20f, ls = Mathf.Clamp(a.height * 0.32f, 120f, 256f);
            if (logo != null) { float lw = ls * logo.width / (float)logo.height; UiKit.Img(new Rect(cx - lw * 0.5f, y, lw, ls), logo, ScaleMode.ScaleToFit); }
            else UiKit.Img(new Rect(cx - ls * 0.5f, y, ls, ls), tLogo, ScaleMode.ScaleToFit);
            y += ls + 4f;
            string l1 = thanks ? "Thanks for playing!" : "Welcome to OpenFusion.", l2 = thanks ? "" : "Select a server from the list below to get started.";
            TagLine(new Rect(x0, y, tw, 27f), l1); TagLine(new Rect(x0, y + 27f, tw, 27f), l2); y += 64f;

            int n = servers.Count; float hh = 44f, th = hh + Mathf.Max(n, 1) * RowH, maxTh = a.yMax - 50f - 48f - y;
            th = Mathf.Clamp(th, hh + 60f, Mathf.Max(hh + 60f, maxTh));
            DrawTable(new Rect(x0, y, tw, th)); float rowY = y + th + 10f;

            bool has = Sel() != null;
            if (Btn(new Rect(x0, rowY, 48f, 38f), null, Kind.Success, true, 16, "plus")) OpenAddEdit(null);
            if (Btn(new Rect(x0 + 52f, rowY, 50f, 38f), null, Kind.Primary, has, 16, "edit")) OpenAddEdit(Sel());
            if (Btn(new Rect(x0 + 106f, rowY, 48f, 38f), null, Kind.Danger, has, 16, "trash")) { modal = Modal.Delete; modalToken++; }
            if (Btn(new Rect(x0 + tw - 112f, rowY, 112f, 38f), "Connect", Kind.Primary, has, 16, "angles_right")) OnConnect();

            if (Btn(new Rect(a.x + 4f, a.yMax - 42f, 50f, 38f), null, Kind.Primary, true, 16, "cog")) OpenSettings();
            if (Btn(new Rect(a.xMax - 54f, a.yMax - 42f, 50f, 38f), null, Kind.Primary, true, 16, "info")) { modal = Modal.About; modalToken++; }
        }

        void OpenSettings() { RefreshBuildCache(); page = Page.Settings; stab = 0; scrPage.y = 0f; prefsApplied = LoadPrefs(); prefsEdit = prefsApplied.Clone(); }

        void TagLine(Rect r, string t)
        {
            if (string.IsNullOrEmpty(t)) return;
            Lbl(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), t, 18, A(CTagShadow, 0.9f), TextAnchor.MiddleCenter, true);
            Lbl(r, t, 18, CTag, TextAnchor.MiddleCenter, true);
        }

        void DrawTable(Rect r)
        {
            for (int i = 3; i >= 1; i--) UiKit.Outline(new Rect(r.x - i * 2f, r.y - i * 2f, r.width + i * 4f, r.height + i * 4f), A(CGlow, 0.16f / i), 2f);
            UiKit.Fill(r, Color.black);
            float hh = 44f, w = r.width, stW = w < 560f ? 112f : 150f, verW = (w - stW) * (w < 560f ? 0.42f : 0.5f), nmW = w - stW - verW;
            UiKit.Fill(new Rect(r.x, r.y, r.width, hh), CHdrRow);
            Lbl(new Rect(r.x + 15f, r.y, nmW - 15f, hh), "Server Name", 17, Color.white, TextAnchor.MiddleLeft, true);
            { string t = "Game Versions"; float gw = TextW(t, 17, true), gx = r.x + nmW + (verW - (gw + 8f + 16f)) * 0.5f;
              Lbl(new Rect(gx, r.y, gw, hh), t, 17, Color.white, TextAnchor.MiddleLeft, true);
              var ir = new Rect(gx + gw + 8f, r.y + hh * 0.5f - 8f, 16f, 16f); DrawIco(ir, "rotate_right", Color.white); if (Tap(new Rect(ir.x - 10f, r.y, 36f, hh))) RefreshAll(); }
            { string t = "Status"; float gw = TextW(t, 17, true), right = r.xMax - 15f, ix = right - 16f;
              Lbl(new Rect(ix - 8f - gw, r.y, gw, hh), t, 17, Color.white, TextAnchor.MiddleLeft, true);
              var ir = new Rect(ix, r.y + hh * 0.5f - 8f, 16f, 16f); DrawIco(ir, "rotate_right", Color.white); if (Tap(new Rect(ir.x - 10f, r.y, 36f, hh))) RefreshAll(); }

            var view = new Rect(r.x, r.y + hh, w, r.height - hh);
            int n = servers.Count;
            ScrollBegin(scrTable, view, n * RowH);
            if (n == 0) Lbl(new Rect(0, 0, w, 60f), "No servers available.", 16, CGray, TextAnchor.MiddleCenter);
            for (int i = 0; i < n; i++)
            {
                var rr = new Rect(0f, i * RowH, w, RowH);
                if (rr.yMax >= scrTable.y && rr.y <= scrTable.y + view.height)
                {
                    var s = servers[i]; Stat st; stats.TryGetValue(s.uuid, out st);
                    UiKit.Fill(rr, i == sel ? CRowSel : (i % 2 == 0 ? CRowA : CRowB));
                    Lbl(new Rect(15f, rr.y, nmW - 20f, RowH), s.description ?? "", 18, Color.white);
                    DrawVersions(new Rect(nmW, rr.y, verW, RowH), s, st);
                    DrawStatus(new Rect(nmW + verW, rr.y, stW, RowH), s, st);
                }
                if (scrTable.tap.HasValue && rr.Contains(scrTable.tap.Value)) TapRow(i);
            }
            ScrollEnd();
            UiKit.Outline(r, A(CPrimary, 0.9f), 1f);
        }

        void TapRow(int i)
        {
            bool dbl = i == lastTapIdx && Time.unscaledTime - lastTapT < 0.45f;
            sel = i; lastTapIdx = i; lastTapT = Time.unscaledTime; SaveSel();
            if (dbl) { lastTapIdx = -1; OnConnect(); }
        }

        void Badge(ref float x, float y, string text, Color bg, float maxX)
        {
            float bw = TextWM(text, 13, true) + 18f; if (x + bw > maxX) return;
            UiKit.Fill(new Rect(x, y, bw, 24f), bg); LblM(new Rect(x, y, bw, 24f), text, 13, Color.white, TextAnchor.MiddleCenter, true); x += bw + 4f;
        }

        void DrawVersions(Rect c, ServerEntry s, Stat st)
        {
            var items = new List<KeyValuePair<string, Color>>(); var ok = CChip; var bad = Hex("#dc3545"); var gray = Hex("#6c757d");
            if (!IsEndpoint(s)) items.Add(new KeyValuePair<string, Color>(string.IsNullOrEmpty(s.version) ? "unknown" : s.version, string.IsNullOrEmpty(s.version) ? bad : gray));
            else if (st == null || st.loading || (st.infoOk && !st.versionsDone)) { Lbl(c, Dots(), 18, CGray, TextAnchor.MiddleCenter); return; }
            else if (!st.infoOk) { Lbl(c, "-", 18, CGray, TextAnchor.MiddleCenter); return; }
            else if (st.versions.Count == 0) items.Add(new KeyValuePair<string, Color>("no versions", bad));
            else
            {
                VerBadge pick = null; foreach (var v in st.versions) if (v.uuid == s.version) pick = v;
                if (pick != null) items.Add(new KeyValuePair<string, Color>(pick.name ?? "unknown", pick.name == null ? bad : ok));
                else foreach (var v in st.versions) items.Add(new KeyValuePair<string, Color>(v.name ?? "unknown", v.name == null ? bad : ok));
            }
            float total = 0f; foreach (var it in items) total += TextWM(it.Key, 13, true) + 22f;
            float x = c.x + Mathf.Max(4f, (c.width - total) * 0.5f), y = c.y + (c.height - 24f) * 0.5f;
            foreach (var it in items) Badge(ref x, y, it.Key, it.Value, c.xMax);
        }

        string Dots() { int n = (int)(Time.unscaledTime * 3f) % 4; return new string('.', n + 1); }

        void DrawStatus(Rect c, ServerEntry s, Stat st)
        {
            float pr = c.xMax - 15f;
            if (!IsEndpoint(s)) { DrawIco(new Rect(pr - 18f, c.center.y - 9f, 18f, 18f), "question", CGray); return; }
            if (st == null || st.loading) { Lbl(new Rect(c.x, c.y, c.width - 15f, c.height), Dots(), 18, CGray, TextAnchor.MiddleRight); return; }
            if (!st.statusOk) { DrawIco(new Rect(pr - 20f, c.center.y - 10f, 20f, 20f), "plug_x", CRed); return; }
            string num = st.players.ToString(); float w = TextW(num, 18, true);
            Lbl(new Rect(pr - w, c.y, w + 2f, c.height), num, 18, CPlayers, TextAnchor.MiddleLeft, true);
            DrawIco(new Rect(pr - w - 8f - 14f, c.center.y - 7f, 14f, 14f), "user", CPlayers);
        }

        // ---------------------------------------------------------------- marco de modal
        /// <summary>Dibuja fondo oscuro + marco del modal y devuelve el rect del cuerpo (bajo la cabecera de 44).</summary>
        Rect ModalFrame(Rect a, float w, float h, string title)
        {
            var sz = UiKit.RefSize; UiKit.Fill(new Rect(0, 0, sz.x, sz.y), new Color(0f, 0f, 0f, 0.6f));
            h = Mathf.Min(h, a.height - 8f); w = Mathf.Min(w, a.width - 8f);
            bool kbd = false; try { kbd = TouchScreenKeyboard.visible; } catch (Exception) { }
            float x = a.center.x - w * 0.5f, y = kbd ? a.y + 4f : a.center.y - h * 0.5f;
            var r = new Rect(x, y, w, h);
            for (int i = 3; i >= 1; i--) UiKit.Outline(new Rect(r.x - i * 2f, r.y - i * 2f, r.width + i * 4f, r.height + i * 4f), A(CGlow, 0.18f / i), 2f);
            DrawTile(new Rect(r.x, r.y, r.width, 44f), tModalBg); DrawTile(new Rect(r.x, r.y + 44f, r.width, r.height - 44f), tModalFoot);
            UiKit.Fill(new Rect(r.x, r.y + 44f, r.width, 1f), CBorder); UiKit.Outline(r, CBorder, 1f);
            Lbl(new Rect(r.x + 14f, r.y, r.width - 28f, 44f), title, 20, CModalText, TextAnchor.MiddleLeft, true);
            return new Rect(r.x, r.y + 45f, r.width, r.height - 45f);
        }

        // ---------------------------------------------------------------- Add / Edit
        void DrawAddEdit(Rect a)
        {
            float w = Mathf.Min(a.width - 16f, 640f);
            var body = ModalFrame(a, w, 345f, adUuid == null ? "Add Server" : "Edit Server");
            float x0 = body.x + 14f, iw = body.width - 28f, y = body.y + 10f;
            adName = FieldRow(new Rect(x0, y, iw, 62f), "Server Name", adName, 64, "ad_name", false, false, "My OpenFusion Server"); y += 68f;
            // pestanas
            for (int i = 0; i < 2; i++)
            {
                var tr = new Rect(body.x + 14f + i * (iw * 0.5f), y, iw * 0.5f, 40f); bool act = adTab == i;
                DrawTile(tr, act ? tModalFoot : tModalBg); UiKit.Outline(tr, CBorder, 1f);
                Lbl(tr, i == 0 ? "Simple Server" : "Endpoint Server", 16, CModalText, TextAnchor.MiddleCenter, act);
                if (Tap(tr) && !act) { adTab = i; GUI.FocusControl(null); }
            }
            y += 50f; bool valid;
            if (adTab == 0)
            {
                bool bad = !string.IsNullOrEmpty(adHost) && NormHost(adHost) == null;
                adHost = FieldRow(new Rect(x0, y, iw, 62f), "Server Host (ip:port)", adHost, 80, "ad_host", false, bad, "localhost:23000"); valid = NormHost(adHost) != null;
            }
            else
            {
                bool bad = !string.IsNullOrEmpty(adEndpoint) && !ValidEndpoint(adEndpoint);
                adEndpoint = FieldRow(new Rect(x0, y, iw, 62f), "API Host", adEndpoint, 120, "ad_ep", false, bad, "api.myserver.xyz"); valid = ValidEndpoint(adEndpoint);
            }
            float by = body.yMax - 54f;
            if (Btn(new Rect(body.xMax - 14f - 130f, by, 130f, 44f), adUuid == null ? "Add" : "Save", Kind.Success, valid)) SaveAddEdit();
            if (Btn(new Rect(body.xMax - 14f - 130f - 8f - 120f, by, 120f, 44f), "Cancel", Kind.Primary)) CloseModal();
        }

        void DrawDelete(Rect a)
        {
            var s = Sel(); if (s == null) { CloseModal(); return; }
            var body = ModalFrame(a, Mathf.Min(a.width - 16f, 520f), 220f, "Are you sure?");
            Lbl(new Rect(body.x + 14f, body.y + 12f, body.width - 28f, 80f), "Do you really want to delete " + s.description + "?\nYou could always re-add it later.", 17, CModalText, TextAnchor.UpperLeft, false, true);
            float by = body.yMax - 54f;
            if (Btn(new Rect(body.xMax - 14f - 150f, by, 150f, 44f), "Yes, Delete", Kind.Danger)) DeleteSelected();
            if (Btn(new Rect(body.xMax - 14f - 150f - 8f - 120f, by, 120f, 44f), "Cancel", Kind.Primary)) CloseModal();
        }

        // ---------------------------------------------------------------- Login / Register
        void DrawLogin(Rect a)
        {
            var s = Sel(); if (s == null) { CloseModal(); return; }
            bool ep = IsEndpoint(s);
            float w = Mathf.Min(a.width - 16f, 720f), h = Mathf.Min(a.height - 8f, 520f);
            var body = ModalFrame(a, w, h, s.description ?? "");
            bool prevOn = inputOn; inputOn = prevOn && !confirmOpen;
            bool ge = GUI.enabled; GUI.enabled = ge && inputOn && !busy;
            float pad = 14f, x0 = body.x + pad, iw = body.width - 2f * pad, colW = (iw - 12f) * 0.5f, y = body.y;

            if (ep) { float annH = Mathf.Clamp(body.height - 292f, 50f, 200f); DrawAnnouncements(new Rect(body.x, y, body.width, annH), s); y += annH; }

            int tabs = ep ? 2 : 1;
            for (int i = 0; i < tabs; i++)
            {
                var tr = new Rect(body.x + i * body.width / tabs, y, body.width / tabs, 40f); bool act = loginTab == i;
                DrawTile(tr, act ? tModalFoot : tModalBg); UiKit.Outline(tr, CBorder, 1f);
                Lbl(tr, i == 0 ? "Log In" : "Register", 17, CModalText, TextAnchor.MiddleCenter, act);
                if (Tap(tr) && !act) { loginTab = i; formErr = null; GUI.FocusControl(null); }
            }
            y += 50f; bool can; string focus = GUI.GetNameOfFocusedControl();
            if (loginTab == 0)
            {
                loginUser = FieldRow(new Rect(x0, y, colW, 62f), "Username", loginUser, 64, "lg_user", false, false);
                loginPass = FieldRow(new Rect(x0 + colW + 12f, y, colW, 62f), "Password", loginPass, 128, "lg_pass", true, false);
                y += 66f;
                var cb = new Rect(x0, y + 6f, 28f, 28f);
                UiKit.Fill(cb, Color.black); UiKit.Outline(cb, CInput, 1.5f); if (remember) Lbl(cb, "X", 18, COk, TextAnchor.MiddleCenter, true);
                Lbl(new Rect(x0 + 38f, y, 170f, 40f), "Remember Me", 16, CModalText);
                if (Tap(new Rect(x0, y, 210f, 40f))) remember = !remember;
                if (ep)
                {
                    string ft = "Forgot your password?"; float fw = TextW(ft, 15, false); var fr = new Rect(x0 + iw - fw - 4f, y, fw + 4f, 40f);
                    Lbl(fr, ft, 15, CLink, TextAnchor.MiddleRight); UiKit.Fill(new Rect(fr.x + 2f, fr.y + 28f, fw, 1f), CLink);
                    if (Tap(new Rect(fr.x - 10f, y, fr.width + 20f, 40f))) { modal = Modal.Forgot; modalToken++; forgotEmail = ""; formErr = null; inputOn = prevOn; GUI.enabled = ge; return; }
                }
                can = loginUser.Trim().Length > 0 && loginPass.Length > 0;
            }
            else
            {
                string em = (regEmail ?? "").Trim();
                bool uBad = regUser.Length > 0 && !ValidUser(regUser), pBad = regPass.Length > 0 && !ValidPass(regPass), cBad = regConfirm.Length > 0 && regConfirm != regPass;
                bool eBad = em.Length > 0 && !ValidEmail(em);
                regUser = FieldRow(new Rect(x0, y, colW, 62f), "Username", regUser, 32, "rg_user", false, uBad);
                regPass = FieldRow(new Rect(x0 + colW + 12f, y, colW, 62f), "Password", regPass, 32, "rg_pass", true, pBad); y += 64f;
                regConfirm = FieldRow(new Rect(x0, y, colW, 62f), "Confirm Password", regConfirm, 32, "rg_conf", true, cBad);
                regEmail = FieldRow(new Rect(x0 + colW + 12f, y, colW, 62f), emailReq ? "Email" : "Email (optional)", regEmail, 100, "rg_mail", false, eBad); y += 64f;
                string hint = focus == "rg_user" ? "• 4 - 32 characters long  • No special characters besides - and _" : focus == "rg_pass" ? "• 8 - 32 characters long" : cBad ? "Passwords do not match" : "";
                Lbl(new Rect(x0, y, iw, 16f), hint, 12, CGray);
                float pre = TextW("View this server's", 13, false) + 4f, lw = TextW("privacy policy", 13, false), pw = pre + lw;
                var pr = new Rect(x0 + (iw - pw) * 0.5f, y + 16f, pw, 22f); var lr = new Rect(pr.x + pre, pr.y, lw, 22f);
                Lbl(new Rect(pr.x, pr.y, pre, 22f), "View this server's", 13, CGray); Lbl(lr, "privacy policy", 13, CLink); UiKit.Fill(new Rect(lr.x, lr.y + 18f, lw, 1f), CLink);
                if (Tap(new Rect(lr.x - 8f, lr.y - 6f, lw + 16f, 34f))) Application.OpenURL(ApiUrl(s) + "/privacy");
                can = ValidUser(regUser) && ValidPass(regPass) && regConfirm == regPass && (em.Length == 0 ? !emailReq : ValidEmail(em));
            }
            GUI.enabled = ge;

            // Enter = enviar
            if (inputOn && !busy && can && UiKit.EnterPressed() && focus != null && (focus.StartsWith("lg_") || focus.StartsWith("rg_"))) SubmitLogin(s);

            string msg = busy ? (loginTab == 0 ? "Logging in" : "Registering") + Dots() : formErr; Color mc = busy ? CGray : CRed;
            Lbl(new Rect(x0, body.yMax - 54f - 30f, iw, 28f), msg, 14, mc, TextAnchor.MiddleLeft, false, true);
            float by = body.yMax - 54f;
            if (Btn(new Rect(body.xMax - pad - 150f, by, 150f, 44f), loginTab == 0 ? "Log In" : "Register", Kind.Success, can && !busy)) SubmitLogin(s);
            if (Btn(new Rect(body.xMax - pad - 150f - 8f - 120f, by, 120f, 44f), "Cancel", Kind.Primary)) { CloseModal(); inputOn = prevOn; return; }
            inputOn = prevOn;

            if (confirmOpen) DrawConfirm(a, s);
        }

        void SubmitLogin(ServerEntry s)
        {
            if (loginTab == 0) { var _t = DoLogin(s, loginUser.Trim(), loginPass); return; }
            bool hasMail = !string.IsNullOrWhiteSpace(regEmail);
            confirmDoc = ParseMd(hasMail
                ? "A verification email will be sent to the address you provided. Please click on the enclosed link in order to complete the registration process as soon as possible.\n\nIf you do not click the verification link, the email will not be linked to your account. You will still be able to log in, but **you will not be able to recover your password.**"
                : "Continue without entering an email address? If there is no email linked to your account, **there will be no way to recover your password if you forget it**.\n\nIf you're logged in, you can always add an email address later by going to Settings -> Authentication -> Manage Account");
            confirmDoc.baseSize = 15; confirmDoc.color = CModalText; confirmOpen = true;
        }

        void DrawConfirm(Rect a, ServerEntry s)
        {
            float w = Mathf.Min(a.width - 16f, 560f), tw = w - 28f;
            MdLayout(confirmDoc, tw);
            var body = ModalFrame(a, w, 45f + confirmDoc.height + 24f + 60f, "Note!");
            GUI.BeginGroup(new Rect(body.x + 14f, body.y + 12f, tw, confirmDoc.height + 4f)); MdDraw(confirmDoc, tw); GUI.EndGroup();
            float by = body.yMax - 54f;
            if (Btn(new Rect(body.xMax - 14f - 150f, by, 150f, 44f), "I understand", Kind.Danger)) { confirmOpen = false; var _t = DoRegister(s); }
            if (Btn(new Rect(body.xMax - 14f - 150f - 8f - 120f, by, 120f, 44f), "Cancel", Kind.Primary)) confirmOpen = false;
        }

        void DrawAnnouncements(Rect r, ServerEntry s)
        {
            UiKit.Fill(r, Color.black); UiKit.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), CBorder);
            var view = new Rect(r.x + 8f, r.y + 5f, r.width - 16f, r.height - 11f);
            if (annDoc == null) { Lbl(view, annLoading ? "Loading announcements" + Dots() : "This server has no announcements.", 13, CGray); return; }
            MdLayout(annDoc, view.width);
            ScrollBegin(scrAnn, view, annDoc.height); MdDraw(annDoc, view.width); ScrollEnd();
            if (scrAnn.tap.HasValue) { string u = MdTap(annDoc, scrAnn.tap.Value); if (u != null) Application.OpenURL(u); }
        }

        // ---------------------------------------------------------------- Forgot / About / Settings
        void DrawForgot(Rect a)
        {
            var s = Sel(); if (s == null) { CloseModal(); return; }
            var body = ModalFrame(a, Mathf.Min(a.width - 16f, 600f), 400f, "Forgot Password");
            float x0 = body.x + 14f, iw = body.width - 28f, y = body.y + 10f;
            string t1 = "Enter the email address associated with your account below to receive a one-time password that can be used to log in.";
            float h1 = WrapH(t1, 15, iw); Lbl(new Rect(x0, y, iw, h1), t1, 15, CModalText, TextAnchor.UpperLeft, false, true); y += h1 + 10f;
            bool ge = GUI.enabled; GUI.enabled = ge && !forgotBusy;
            string em = forgotEmail.Trim(); bool bad = em.Length > 0 && !ValidEmail(em);
            forgotEmail = FieldRow(new Rect(x0, y, iw, 62f), "Email", forgotEmail, 100, "fg_mail", false, bad); y += 70f;
            GUI.enabled = ge;
            string t2 = "Once you have logged in, you can change your password in Settings -> Authentication -> Manage Account -> Change Password.";
            Lbl(new Rect(x0, y, iw, WrapH(t2, 14, iw)), t2, 14, CGray, TextAnchor.UpperLeft, false, true);
            float by = body.yMax - 54f; bool ok = ValidEmail(em) && !forgotBusy;
            if (Btn(new Rect(body.xMax - 14f - 260f, by, 260f, 44f), forgotBusy ? "Sending" + Dots() : "Send Temporary Password", Kind.Success, ok, 16)) { var _t = DoForgot(s); }
            if (Btn(new Rect(body.xMax - 14f - 260f - 8f - 110f, by, 110f, 44f), "Cancel", Kind.Primary)) CloseModal();
        }

        void DrawAbout(Rect a)
        {
            var body = ModalFrame(a, Mathf.Min(a.width - 16f, 500f), 214f, "About OpenFusionLauncher");
            float x0 = body.x + 16f, iw = body.width - 32f, y = body.y + 14f;
            LblM(new Rect(x0, y, iw, 22f), "Version " + Application.version, 15, CModalText); y += 36f;
            LblM(new Rect(x0, y, iw, 22f), "©2020-2025 OpenFusion Contributors", 15, CModalText); y += 22f;
            LblM(new Rect(x0, y, iw, 22f), "OpenFusion is licensed under MIT.", 15, CModalText);
            float by = body.yMax - 48f;
            if (Btn(new Rect(x0, by, 56f, 38f), null, Kind.Primary, true, 18, "github")) Application.OpenURL("https://github.com/OpenFusionProject/");
            if (Btn(new Rect(x0 + 62f, by, 56f, 38f), null, Kind.Primary, true, 18, "discord")) Application.OpenURL("https://discord.gg/DYavckB");
            if (Btn(new Rect(body.xMax - 16f - 78f, by, 78f, 38f), "Close", Kind.Primary, true, 16)) CloseModal();
        }

        // ================================================================ Pagina de ajustes (4 pestanas, como el launcher original)
        sealed class Prefs
        {
            public int keepAwake = 1, fps = Application.isMobilePlatform ? 0 : 1, quality = 1, look = 1;
            public Prefs Clone() { return (Prefs)MemberwiseClone(); }
            public bool Same(Prefs o) { return keepAwake == o.keepAwake && fps == o.fps && quality == o.quality && look == o.look; }
        }
        static readonly int[] FpsVals = { 30, 60, -1 };
        static readonly float[] LookVals = { 0.2f, 0.3f, 0.45f };
        static Prefs LoadPrefs()
        {
            return new Prefs { keepAwake = PlayerPrefs.GetInt("of_set_keepawake", 1), fps = PlayerPrefs.GetInt("of_set_fps", Application.isMobilePlatform ? 0 : 1), quality = PlayerPrefs.GetInt("of_set_quality", 1), look = PlayerPrefs.GetInt("of_set_look", 1) };
        }
        static void SavePrefs(Prefs p)
        {
            PlayerPrefs.SetInt("of_set_keepawake", p.keepAwake); PlayerPrefs.SetInt("of_set_fps", p.fps); PlayerPrefs.SetInt("of_set_quality", p.quality); PlayerPrefs.SetInt("of_set_look", p.look); PlayerPrefs.Save();
        }
        /// <summary>all=false: solo aplica lo que la persona ya cambio alguna vez (no toca los valores por defecto del proyecto).</summary>
        static void ApplyPrefs(Prefs p, bool all)
        {
            try
            {
                if (all || PlayerPrefs.HasKey("of_set_keepawake")) Screen.sleepTimeout = p.keepAwake == 1 ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
                Application.targetFrameRate = FpsVals[Mathf.Clamp(p.fps, 0, 2)];
                int n = QualitySettings.names.Length;
                if (n > 0 && (all || PlayerPrefs.HasKey("of_set_quality"))) QualitySettings.SetQualityLevel(p.quality == 0 ? 0 : p.quality == 1 ? n / 2 : n - 1, true);
                var tc = OpenFusion.World.TouchControls.Instance;
                if (tc != null && (all || PlayerPrefs.HasKey("of_set_look"))) tc.LookDegPerDp = LookVals[Mathf.Clamp(p.look, 0, 2)];
            }
            catch (Exception ex) { Debug.LogWarning("[launcher] ajustes: " + ex.Message); }
        }

        sealed class BuildInfo { public string uuid, name; public long full, offline, cached; public bool managed; }
        List<BuildInfo> cacheRows = new List<BuildInfo>(); bool scanningCache; float nextCacheScan;
        static List<BuildInfo> builds;
        List<BuildInfo> Builds()
        {
            if (builds != null) return cacheRows.Concat(builds.Where(b => !cacheRows.Any(c => c.uuid == b.uuid))).ToList();
            builds = new List<BuildInfo>();
            try
            {
                var ta = Resources.Load<TextAsset>(ResPath + "builds");
                if (ta != null)
                {
                    var arr = JObject.Parse(ta.text)["a"] as JArray;
                    if (arr != null) foreach (var o in arr) builds.Add(new BuildInfo { uuid = (string)o["uuid"], name = (string)o["name"], full = (long)o["full"], offline = (long)o["offline"] });
                }
            }
            catch (Exception ex) { Debug.LogWarning("[launcher] builds.json: " + ex.Message); }
            return cacheRows.Concat(builds.Where(b => !cacheRows.Any(c => c.uuid == b.uuid))).ToList();
        }
        async void RefreshBuildCache()
        {
            if (scanningCache) return; scanningCache = true; nextCacheScan = Time.unscaledTime + 5f;
            string root = Path.Combine(Application.persistentDataPath, "bundles"); var labels = new Dictionary<string, string>(verNames);
            try
            {
                var rows = await Task.Run(() => OpenFusion.Assets.GameCacheInfo.Scan(root, labels));
                cacheRows = rows.Select(r => new BuildInfo { uuid = r.uuid, name = r.name, full = r.expectedBytes, cached = r.downloadedBytes, managed = true }).ToList();
            }
            catch (Exception e) { Debug.LogWarning("[cache] scan: " + e.Message); }
            finally { scanningCache = false; }
        }
        static string Gb(long bytes) { return (bytes / 1073741824.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " GB"; }

        static readonly string[] TabNames = { "Launcher Settings", "Game Settings", "Game Builds", "Authentication" };
        static readonly string[] TabIcons = { "rocket", "gamepad", "download", "key" };

        void DrawSettingsPage(Rect a)
        {
            var sz = UiKit.RefSize; var tabCol = Hex("#3d9be9");
            DrawTile(new Rect(0f, 0f, sz.x, sz.y), tModalFoot);
            // cabecera (NavBar): Back + titulo
            float hh = a.y + 52f; DrawTile(new Rect(0f, 0f, sz.x, hh), tModalBg);
            if (Btn(new Rect(a.x + 6f, a.y + 8f, 92f, 38f), "Back", Kind.Primary, true, 16, "angles_left", true)) { page = Page.Home; scrPage.y = 0f; return; }
            Lbl(new Rect(a.x, a.y + 4f, a.width, 46f), "Settings", 24, CModalText, TextAnchor.MiddleCenter, true);
            // pestanas
            float ty = hh, th = 42f, tw = sz.x / 4f;
            for (int i = 0; i < 4; i++)
            {
                var tr = new Rect(i * tw, ty, tw, th); bool act = stab == i;
                DrawTile(tr, act ? tModalFoot : tModalBg);
                if (act) { UiKit.Fill(new Rect(tr.x, tr.y, tr.width, 1f), CBorder); UiKit.Fill(new Rect(tr.x, tr.y, 1f, tr.height), CBorder); UiKit.Fill(new Rect(tr.xMax - 1f, tr.y, 1f, tr.height), CBorder); }
                else UiKit.Fill(new Rect(tr.x, tr.yMax - 1f, tr.width, 1f), CBorder);
                string name = TabNames[i]; float nw = TextW(name, 16, false), isz = 16f, gx = tr.center.x - (isz + 6f + nw) * 0.5f; Color c = act ? CModalText : tabCol;
                DrawIco(new Rect(gx, tr.center.y - isz * 0.5f, isz, isz), TabIcons[i], c);
                Lbl(new Rect(gx + isz + 6f, tr.y, nw + 4f, tr.height), name, 16, c, TextAnchor.MiddleLeft, false);
                if (Tap(tr) && !act) { stab = i; scrPage.y = 0f; }
            }
            float cy = ty + th; var view = new Rect(0f, cy, sz.x, sz.y - cy);
            float contentH;
            switch (stab)
            {
                case 2: contentH = 94f + Builds().Count * 140f + 8f; break;
                case 3: contentH = 54f + servers.Count * 120f + 8f; break;
                default: contentH = 420f; break;
            }
            ScrollBegin(scrPage, view, contentH);
            float vw = view.width;
            if (stab == 0 || stab == 1) DrawPrefsTab(vw);
            else if (stab == 2) DrawBuildsTab(vw);
            else DrawAuthTab(vw);
            ScrollEnd();
        }

        int Choice(Rect r, string label, string[] opts, int cur)
        {
            Lbl(new Rect(r.x, r.y, r.width, 20f), label, 15, CModalText);
            float gap = 6f, bw = (r.width - gap * (opts.Length - 1)) / opts.Length;
            for (int i = 0; i < opts.Length; i++) if (Btn(new Rect(r.x + i * (bw + gap), r.y + 24f, bw, 38f), opts[i], i == cur ? Kind.Primary : Kind.Dark, true, 16)) cur = i;
            return cur;
        }

        void DrawPrefsTab(float vw)
        {
            if (prefsEdit == null) { prefsApplied = LoadPrefs(); prefsEdit = prefsApplied.Clone(); }
            bool launcher = stab == 0;
            float pw = Mathf.Min(vw - 24f, Mathf.Clamp(vw * 0.66f, 520f, 780f)), px = (vw - pw) * 0.5f, py = 28f;
            float bodyH = launcher ? 2 * 74f + 62f : 2 * 74f; float ph = 16f + 38f + 12f + 1f + 14f + bodyH + 12f;
            var pr = new Rect(px, py, pw, ph);
            for (int i = 3; i >= 1; i--) UiKit.Outline(new Rect(pr.x - i * 2f, pr.y - i * 2f, pr.width + i * 4f, pr.height + i * 4f), A(CGlow, 0.16f / i), 2f);
            DrawTile(pr, tModalBg); UiKit.Outline(pr, CPrimary, 1f);
            float x0 = pr.x + 16f, iw = pr.width - 32f, y = pr.y + 16f; bool changed = !prefsEdit.Same(prefsApplied);
            Lbl(new Rect(x0, y, iw - 170f, 38f), TabNames[stab], 20, CModalText, TextAnchor.MiddleLeft, true);
            if (Btn(new Rect(pr.xMax - 16f - 50f, y, 50f, 38f), null, Kind.Success, changed, 16, "check")) { prefsApplied = prefsEdit.Clone(); SavePrefs(prefsApplied); ApplyPrefs(prefsApplied, true); Toast("success", "Settings saved"); }
            if (Btn(new Rect(pr.xMax - 16f - 104f, y, 50f, 38f), null, Kind.Primary, changed, 16, "rotate_left")) prefsEdit = prefsApplied.Clone();
            if (Btn(new Rect(pr.xMax - 16f - 158f, y, 50f, 38f), null, Kind.Danger, true, 16, "trash")) { prefsEdit = new Prefs(); prefsApplied = prefsEdit.Clone(); SavePrefs(prefsApplied); ApplyPrefs(prefsApplied, true); Toast("success", "Settings reset to defaults"); }
            y += 38f + 12f; UiKit.Fill(new Rect(x0, y, iw, 1f), A(CPrimary, 0.8f)); y += 1f + 14f;
            if (launcher)
            {
                prefsEdit.keepAwake = Choice(new Rect(x0, y, iw, 62f), "Keep the screen on while playing", new[] { "No", "Yes" }, prefsEdit.keepAwake); y += 74f;
                prefsEdit.fps = Choice(new Rect(x0, y, iw, 62f), "Frame rate limit", new[] { "30 FPS", "60 FPS", "Unlimited" }, prefsEdit.fps); y += 74f;
                if (Btn(new Rect(x0, y, iw, 48f), "Forget remembered usernames", Kind.Danger, true, 16)) { foreach (var s in servers) PlayerPrefs.DeleteKey(UserKey(s)); PlayerPrefs.Save(); Toast("success", "Remembered usernames cleared"); }
            }
            else
            {
                prefsEdit.quality = Choice(new Rect(x0, y, iw, 62f), "Graphics quality", new[] { "Low", "Medium", "High" }, prefsEdit.quality); y += 74f;
                prefsEdit.look = Choice(new Rect(x0, y, iw, 62f), "Touch camera sensitivity", new[] { "Low", "Normal", "High" }, prefsEdit.look);
            }
        }

        bool cacheClearing;
        async void ClearGameCache()
        {
            if (cacheClearing) return;
            cacheClearing = true;
            try
            {
                // Solo datos descargados de esta app; las cuentas y preferencias se conservan.
                string root = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "bundles"));
                await Task.Run(() => { if (Directory.Exists(root)) Directory.Delete(root, true); });
                Toast("success", "Game cache cleared. Resources will download on the next login.");
            }
            catch (Exception e) { Toast("error", "Could not clear game cache: " + e.Message); }
            finally { cacheClearing = false; RefreshBuildCache(); }
        }

        void DrawBuildsTab(float vw)
        {
            string msg = "Game data is downloaded and managed automatically by the app on Android.";
            if (Btn(new Rect(8f, 8f, 120f, 38f), "Add Build", Kind.Success, true, 16, "plus")) Toast("info", msg);
            if (Btn(new Rect(vw - 8f - 130f, 8f, 130f, 38f), "Clear All Game", Kind.Danger, true, 16, "trash")) ClearGameCache();
            if (Btn(new Rect(vw - 8f - 130f - 8f - 160f, 8f, 160f, 38f), "Delete All Offline", Kind.Danger, true, 16, "trash")) ClearGameCache();
            float y = 54f, xX = vw - 66f, c1 = Mathf.Max(220f, vw * 0.34f), c2 = (xX - c1) * 0.5f, c3 = xX - c1 - c2;
            UiKit.Fill(new Rect(0f, y, vw, 40f), CHdrRow);
            Lbl(new Rect(8f, y, c1, 40f), "Version", 16, Color.white, TextAnchor.MiddleLeft, true);
            Lbl(new Rect(c1, y, c2, 40f), "Game Cache", 16, Color.white, TextAnchor.MiddleCenter, true);
            Lbl(new Rect(c1 + c2, y, c3, 40f), "Offline Cache", 16, Color.white, TextAnchor.MiddleCenter, true);
            y += 40f; var list = Builds();
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i]; var rr = new Rect(0f, y + i * 140f, vw, 140f);
                if (rr.yMax < scrPage.y - 4f || rr.y > scrPage.y + 2000f) continue;
                UiKit.Fill(rr, i % 2 == 0 ? CRowBld : CRowBld2);
                LblM(new Rect(8f, rr.y + 34f, c1 - 12f, 36f), b.name, 26, Color.white); LblM(new Rect(8f, rr.y + 72f, c1 - 12f, 20f), b.uuid, 13, A(Color.white, 0.6f));
                CacheCell(new Rect(c1, rr.y, c2, 140f), (b.managed ? Gb(b.cached) : "--") + " / " + (b.full > 0 ? Gb(b.full) : "shared cache"), new[] { "folder", "trash" }, new[] { Kind.Primary, Kind.Danger }, msg);
                CacheCell(new Rect(c1 + c2, rr.y, c3, 140f), "-- / " + Gb(b.offline), new[] { "folder", "download", "wrench", "trash" }, new[] { Kind.Primary, Kind.Success, Kind.Warning, Kind.Danger }, msg);
                if (Btn(new Rect(xX + 10f, rr.y + 6f, 46f, 128f), null, Kind.Danger, true, 18, "xmark")) Toast("info", msg);
            }
        }

        void CacheCell(Rect c, string text, string[] icons_, Kind[] kinds, string msg)
        {
            Lbl(new Rect(c.x, c.y + 10f, c.width, 22f), text, 15, CModalText, TextAnchor.MiddleCenter);
            var bar = new Rect(c.x + 14f, c.y + 42f, c.width - 28f, 17f); UiKit.Fill(bar, Color.black); UiKit.Outline(bar, CGlow, 1f);
            float bw = 50f, gap = 4f, total = icons_.Length * bw + (icons_.Length - 1) * gap, x = c.center.x - total * 0.5f;
            for (int i = 0; i < icons_.Length; i++) if (Btn(new Rect(x + i * (bw + gap), c.y + 82f, bw, 38f), null, kinds[i], true, 16, icons_[i])) Toast("info", msg);
        }

        void DrawAuthTab(float vw)
        {
            if (Btn(new Rect(vw - 8f - 106f, 8f, 106f, 38f), "Refresh", Kind.Primary, true, 16, "rotate_right")) RefreshAll();
            if (Btn(new Rect(vw - 8f - 106f - 8f - 134f, 8f, 134f, 38f), "Log Out All", Kind.Danger, true, 16, "sign_out"))
            { foreach (var s in servers) PlayerPrefs.DeleteKey(UserKey(s)); PlayerPrefs.Save(); Toast("success", "Remembered usernames cleared"); }
            float y = 54f;
            for (int i = 0; i < servers.Count; i++)
            {
                var s = servers[i]; Stat st; stats.TryGetValue(s.uuid, out st); var rr = new Rect(0f, y + i * 120f, vw, 120f);
                var bg = Art(artBg, s, false);
                if (bg != null) UiKit.Img(rr, bg, Color.white, ScaleMode.ScaleAndCrop); else UiKit.Fill(rr, CRowBld2);
                UiKit.Fill(rr, new Color(0f, 0f, 0f, 0.38f)); UiKit.Fill(new Rect(rr.x, rr.yMax - 1f, rr.width, 1f), A(CBorder, 0.35f));
                var logo = Art(artLogo, s, true); float tx = 32f, ny;
                if (logo != null) { float lw = 60f * logo.width / (float)logo.height; UiKit.Img(new Rect(tx, rr.y + 18f, lw, 60f), logo, ScaleMode.ScaleToFit); ny = rr.y + 88f; }
                else { LblM(new Rect(tx, rr.y + 30f, vw * 0.55f, 36f), s.description ?? "", 26, Color.white); ny = rr.y + 66f; }
                string ep = IsEndpoint(s) ? s.endpoint : s.ip; float ew = TextWM(ep ?? "", 13, false);
                LblM(new Rect(tx, ny, ew + 4f, 20f), ep ?? "", 13, CGray);
                bool known = st != null && !st.loading, online = known && (st.statusOk || st.infoOk);
                if (known && IsEndpoint(s)) LblM(new Rect(tx + ew + 8f, ny, 80f, 20f), online ? "online" : "offline", 13, online ? COk : CRed);
                if (IsEndpoint(s) && known && !online) continue;   // el original oculta el estado de sesion si el servidor esta fuera de linea
                string saved = PlayerPrefs.GetString(UserKey(s), "");
                Lbl(new Rect(vw - 16f - 260f, rr.y + 28f, 260f, 20f), saved.Length > 0 ? "saved username: " + saved : "not logged in", 13, A(Color.white, 0.65f), TextAnchor.MiddleRight);
                if (Btn(new Rect(vw - 16f - 100f, rr.y + 53f, 100f, 38f), "Log In", Kind.Success, true, 16, "sign_in")) { sel = i; SaveSel(); OnConnect(); }
            }
        }

        // ---------------------------------------------------------------- toasts
        void DrawToasts(Rect a)
        {
            float w = Mathf.Min(300f, a.width - 16f), y = a.y + 8f, x = a.xMax - w - 4f;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i]; float bh = WrapH(t.text, 13, w - 20f) + 14f, h = 24f + bh; var r = new Rect(x, y, w, h);
                Color hc; string title;
                switch (t.kind) { case "success": hc = Hex("#198754"); title = "Success"; break; case "error": hc = Hex("#dc3545"); title = "Error"; break; case "warning": hc = Hex("#ffc107"); title = "Warning"; break; default: hc = CPrimary; title = "Info"; break; }
                UiKit.Fill(r, Hex("#212529")); UiKit.Fill(new Rect(r.x, r.y, r.width, 24f), hc); UiKit.Outline(r, A(Color.white, 0.25f), 1f);
                Lbl(new Rect(r.x + 10f, r.y, r.width - 20f, 24f), title, 14, t.kind == "warning" ? Color.black : Color.white, TextAnchor.MiddleLeft, true);
                Lbl(new Rect(r.x + 10f, r.y + 28f, r.width - 20f, bh - 8f), t.text, 13, Color.white, TextAnchor.UpperLeft, false, true);
                if (Tap(r)) { toasts.RemoveAt(i); break; }
                y += h + 6f;
            }
        }

        // ================================================================ mini Markdown (titulos, negritas/cursivas, listas, enlaces como texto coloreado)
        static MdDoc ParseMd(string md)
        {
            var d = new MdDoc(); if (md == null) return d;
            foreach (var raw in md.Replace("\r", "").Split('\n'))
            {
                string t = raw.Trim();
                if (t.StartsWith("```")) continue;
                if (t.Length == 0) { if (d.blocks.Count > 0) d.blocks.Add(new MdBlock { kind = 5 }); continue; }
                if (Regex.IsMatch(t, @"^(-{3,}|\*{3,}|_{3,})$")) { d.blocks.Add(new MdBlock { kind = 5 }); continue; }
                int kind = 0; string pre = "";
                var m = Regex.Match(t, @"^(#{1,6})\s+(.*)$");
                if (m.Success) { kind = Math.Min(3, m.Groups[1].Length); t = m.Groups[2].Value; }
                else
                {
                    m = Regex.Match(t, @"^([-*+]|\d+\.)\s+(.*)$");
                    if (m.Success) { kind = 4; pre = char.IsDigit(m.Groups[1].Value[0]) ? m.Groups[1].Value + " " : "• "; t = m.Groups[2].Value; }
                    else if (t.StartsWith("> ")) t = t.Substring(2);
                }
                string url = null; var lm = Regex.Match(t, @"\[[^\]]+\]\((https?://[^)\s]+)");
                if (lm.Success) url = lm.Groups[1].Value;
                d.blocks.Add(new MdBlock { kind = kind, rich = pre + MdInline(t), url = url });
            }
            return d;
        }

        static string MdInline(string t)
        {
            t = t.Replace("<", "‹").Replace(">", "›");   // neutraliza etiquetas rich text que venga del servidor
            t = Regex.Replace(t, @"!\[[^\]]*\]\([^)]*\)", "");
            t = Regex.Replace(t, @"\[([^\]]+)\]\(([^)\s]+)(?:\s+[^)]*)?\)", "<color=#6ea8fe>$1</color>");
            t = Regex.Replace(t, @"\*\*(.+?)\*\*", "<b>$1</b>");
            t = Regex.Replace(t, @"__(.+?)__", "<b>$1</b>");
            t = Regex.Replace(t, @"(?<![\*\w])\*(?!\s)([^*]+?)(?<!\s)\*(?![\*\w])", "<i>$1</i>");
            t = Regex.Replace(t, @"`([^`]+)`", "<color=#9ecbff>$1</color>");
            return t;
        }

        GUIStyle MdStyle(MdDoc d, int kind)
        {
            var st = stMd; bool head = kind >= 1 && kind <= 3;
            st.fontSize = kind == 1 ? d.baseSize + 8 : kind == 2 ? d.baseSize + 5 : kind == 3 ? d.baseSize + 3 : d.baseSize;
            st.fontStyle = head ? FontStyle.Bold : FontStyle.Normal; st.normal.textColor = d.color; return st;
        }

        /// <summary>Calcula alturas de cada bloque para un ancho dado (con cache).</summary>
        void MdLayout(MdDoc d, float w)
        {
            if (Mathf.Abs(d.laidW - w) < 0.5f) return;
            d.laidW = w; float y = 0f;
            foreach (var b in d.blocks)
            {
                if (b.kind == 5) b.h = 6f;
                else { b.indent = b.kind == 4 ? 6f : 0f; b.h = MdStyle(d, b.kind).CalcHeight(new GUIContent(b.rich), w - b.indent) + (b.kind >= 1 && b.kind <= 3 ? 5f : 3f); }
                b.y = y; y += b.h;
            }
            d.height = y;
        }

        void MdDraw(MdDoc d, float w)
        {
            foreach (var b in d.blocks)
            {
                if (b.kind == 5) continue;
                GUI.Label(new Rect(b.indent, b.y, w - b.indent, b.h), b.rich, MdStyle(d, b.kind));
            }
        }

        /// <summary>URL del bloque tocado (coordenadas de contenido) o null.</summary>
        static string MdTap(MdDoc d, Vector2 p)
        {
            foreach (var b in d.blocks) if (b.kind != 5 && p.y >= b.y && p.y < b.y + b.h) return b.url;
            return null;
        }
    }
}
