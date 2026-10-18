// Arranque de partida: login -> personaje -> shard -> mundo. Se coloca en un GameObject junto a WorldBootstrap (+ DongLoader).
// SIN COMPILAR NI PROBAR EN UNITY NI CONTRA UN SERVIDOR REAL.
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using OpenFusion.Api;
using OpenFusion.Net;
using OpenFusion.Tables;
using OpenFusion.World;
using OpenFusion.Screens;

namespace OpenFusion
{
    public sealed partial class GameClient : MonoBehaviour
    {
        public WorldBootstrap Bootstrap;
        // La app siempre arranca con launcher, login y pantallas de personajes.
        [HideInInspector] public bool UseScreens = true;
        [Header("Cuenta")] public string Username = ""; public string Password = "";
        [Tooltip("Retrobution usa /auth + /cookie (secure_apis_enabled); si el servidor no lo exige se usa usuario/contraseña directos")] public bool PreferSecureApi = true;
        [Header("Servidor de login (vacío = login_address de la API)")] public string LoginHost = ""; public int LoginPort = 0;
        [Header("Depuración")] [Tooltip("Registra en la consola cada paquete de login enviado/recibido (nombre y tamaño)")] public bool LogPackets = false;
        [Header("Protocolo")] public int VersionA = 1, VersionB = 0, VersionC = 44;
        [Tooltip("0 = el shard que elija el servidor")] public int Shard = 0;
        [Tooltip("Posición en la lista de personajes (0 = primero)")] public int CharacterIndex = 0;
        [Header("Envío de movimiento")] public bool SendMovement = true; public float SendInterval = 0.1f; public int RunSpeed = 600;
        [Tooltip("Mover al jugador propio con WASD/flechas (sólo para pruebas)")] public bool DebugWalker = false; public float DebugWalkSpeed = 8f;

        public event Action<string> Status; public LoginSession Login { get; private set; } public EnteredShard Shard_ { get; private set; } public GameTables Tables { get; private set; }

        // El mundo se carga DESPUÉS del login, centrado en la posición guardada del personaje (el original también carga antes de pedir el shard).
        static readonly object logLock = new object();
        // W24: cambio de shard (OTHER_SHARD_SUCC): se recarga la escena y se reentra sin menu de login con las credenciales y el personaje de la sesion anterior (CnCharSelectionMode.Play con el shard nuevo).
        static LauncherResult lastLaunch; static long lastCharUid; static bool resumePending; static long resumeUid;
        void OnShardSwitch(int shard, int channel)
        {
            if (this == null) return;
            resumePending = UseScreens && lastLaunch != null && lastCharUid != 0; resumeUid = lastCharUid; shardAlive = false;
            Report("Cambio de shard " + shard + " (canal " + channel + "): cerrando la sesion del shard actual y volviendo a entrar…");
            try { if (Shard_ != null && Shard_.Session != null) Shard_.Session.Dispose(); } catch { } try { if (Login != null) Login.Dispose(); } catch { }
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
        void Awake()
        { BindSender();
            OpenFusion.World.DualScreenController.Ensure(gameObject);
            ChannelWarp.ReloadOnShardChange = false; ChannelWarp.ShardSwitch -= OnShardSwitch; ChannelWarp.ShardSwitch += OnShardSwitch;
            if (GetComponent<OpenFusion.World.TouchControls>() == null) gameObject.AddComponent<OpenFusion.World.TouchControls>();
            UseScreens = true;
            GraphicsFx.Ensure(gameObject);
            if (enabled && Bootstrap != null) Bootstrap.AutoStart = false; DebugWalker = true; SendInterval = 0.4f; SendMovement = true;   // el original manda PC_MOVE como mucho cada 0,3 s (fPacketMinInterval); más rápido = el servidor corta por exceso de paquetes. F2 activa/desactiva el envío
            try   // Diagnostico opt-in: evitar escritura continua en disco en release.
            {
                if (!Debug.isDebugBuild && !LogPackets) return;
                var path = (Application.isMobilePlatform ? System.IO.Path.Combine(Application.persistentDataPath, "openfusion-runtime.txt") : System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "openfusion-runtime.txt"))); System.IO.File.WriteAllText(path, "");
                Application.logMessageReceivedThreaded += (msg, stack, type) => { try { lock (logLock) System.IO.File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " [" + type + "] " + msg + (type == LogType.Log ? "" : "\n" + stack) + "\n"); } catch { } };
            } catch { }
        }
        Action<byte[]> ownedSender;
        void BindSender()
        {
            FindSelf = this;
            ownedSender = pkt => { _ = Send(pkt); };
            NpcUI.Sender = ownedSender;
        }

        async void Start()
        {
            try { await Run(); }
            catch (LoginException e) { StartupFailed(e.Message); }
            catch (Exception e) { Debug.LogError("GameClient: " + e); StartupFailed(e is TaskCanceledException ? "The server request timed out. Check your connection and try again." : e.Message); }
        }
        void StartupFailed(string message)
        {
            if (this == null) return;
            RecordStartup("FAILED: " + message);
            if (Bootstrap != null && Bootstrap.Loader != null) Bootstrap.Loader.Paused = true;
            Report("Startup failed: " + message);
            try { Login?.Dispose(); Shard_?.Session.Dispose(); } catch { }
            shardAlive = false;
            LoadingScreen.Ensure().ShowFailure(message, () =>
            {
                resumePending = false;
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            });
        }
        void StartupStage(LoadingScreen screen, string title)
        { RecordStartup(title); Report("[startup] " + title); if (screen != null) screen.ShowStage(title); }
        void RecordStartup(string stage)
        {
            try
            {
                var path = System.IO.Path.Combine(Application.persistentDataPath, "startup-diagnostics.log");
                if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 32768) System.IO.File.Delete(path);
                System.IO.File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + stage +
                    " managedMB=" + (GC.GetTotalMemory(false) / 1048576) +
                    " unityMB=" + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576) + "\n");
            }
            catch (System.IO.IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        void Report(string s) { Debug.Log("GameClient: " + s); Status?.Invoke(s); }

        async Task Run()
        {
            // 2. servidor de login
            LoadingScreen loading = null;
            bool resuming = false;
            if (UseScreens)
            {   // 1. launcher: servidor + usuario/contraseña escritos por la persona (en memoria)
                loading = LoadingScreen.Ensure(); loading.Bind(Bootstrap);
                var launcher = GetComponent<LauncherScreen>() ?? gameObject.AddComponent<LauncherScreen>();
                resuming = resumePending && lastLaunch != null; resumePending = false;
                var lr = resuming ? lastLaunch : await launcher.Run(); lastLaunch = lr;
                if (!string.IsNullOrEmpty(lr.ServerUrl)) Bootstrap.ServerUrl = lr.ServerUrl;
                else { Report("Los servidores 'Simple' (sin API) aún no están soportados: el cliente necesita la API para descargar los recursos."); return; }
                if (!string.IsNullOrEmpty(lr.ServerId)) Bootstrap.ServerId = lr.ServerId;
                Bootstrap.VersionUuid = lr.VersionUuid ?? "";
                Username = lr.Username; Password = lr.Password; StartupStage(loading, "CONTACTING SERVER");
            }
            var api = new ServerApi(Bootstrap.ServerUrl); var info = await api.GetInfo();
            if (string.IsNullOrEmpty(Bootstrap.VersionUuid) && info.GameVersions != null && info.GameVersions.Count > 0) Bootstrap.VersionUuid = info.GameVersions[0];
            // usuario/contraseña: se pueden escribir en el Inspector DURANTE el Play (esos cambios no se guardan en la escena)
            for (bool warned = false; string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password); warned = true)
            { if (!warned) Report("Escribe Username y Password en el Inspector de GameClient (Play mode)…"); await Task.Delay(500); }
            string host = LoginHost, port = LoginPort > 0 ? LoginPort.ToString() : null;
            if (string.IsNullOrEmpty(host)) { var p = (info.LoginAddress ?? "").Split(':'); host = p[0]; if (port == null && p.Length > 1) port = p[1]; }
            int portNum = port != null ? int.Parse(port) : 23000;   // puerto por defecto del cliente original
            lastHost = host; lastPort = portNum;
            if (resuming) await Task.Delay(1500);   // da tiempo al servidor a registrar la desconexion del shard anterior (si no, AlreadyLoggedIn)
            Report("Conectando al login server " + host + ":" + portNum + "…");
            StartupStage(loading, "CONNECTING TO LOGIN SERVER");
            var opts = new LoginOptions { VerA = VersionA, VerB = VersionB, VerC = VersionC };
            if (LogPackets) opts.Log = m => Debug.Log("net " + m);
            Login = await LoginSession.ConnectAsync(host, portNum, opts);

            // 3. login
            LoginSuccess ok;
            StartupStage(loading, "AUTHENTICATING ACCOUNT");
            if (PreferSecureApi && info.SecureApis)
            {
                var ck = await api.LoginSecure(Username, Password);
                ok = await Login.LoginWithCookieAsync(ck.username ?? Username, ck.cookie);   // TEGid = usuario, authid = cookie: suposición
            }
            else ok = await Login.LoginAsync(Username, Password);
            Report("Sesión iniciada: " + ok.Id + ", " + ok.CharCount + " personajes");
            StartupStage(loading, "LOADING CHARACTER SELECTION ASSETS");
            GuideChangerWindow.PaymentFlag = ok.PaymentFlag;   // CnCharSelectionMode.iPaymentFlag (boton time machine, icono de guia)
            if (Login.Characters.Count == 0 && !UseScreens) { Report("La cuenta no tiene personajes: créalos con LoginSession.CheckNameAsync/SaveNameAsync/CreateCharacterAsync"); return; }
            CharInfo ch = null;
            if (Login.Characters.Count > 0)
            {   // personaje para situar el mundo inicial: el último jugado (pantallas) o CharacterIndex (Inspector)
                int ix = Math.Min(CharacterIndex, Login.Characters.Count - 1);
                if (UseScreens && Login.Info != null) { int last = Login.Info.SlotNum; for (int i = 0; i < Login.Characters.Count; i++) if (Login.Characters[i].Slot == last) ix = i; }
                ch = Login.Characters[ix];
            }

            // 3b. mundo alrededor del personaje: servidor (x, y, z) = (cliente x, cliente z, cliente y) * 100
            if (ch != null) Bootstrap.StartPosition = new Vector3(ch.X / 100f, ch.Z / 100f, ch.Y / 100f);
            // el cargador de dongs sigue a Bootstrap.Player: colócalo (y la cámara) en el personaje antes de cargar
            if (Bootstrap.Player != null) Bootstrap.Player.position = Bootstrap.StartPosition;
            var cam = Camera.main; if (cam != null) { cam.transform.position = Bootstrap.StartPosition + new Vector3(0f, 40f, -40f); cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f); }
            Report((ch != null ? "Personaje " + ch.Appearance.FirstName : "Cuenta sin personajes") + " en " + Bootstrap.StartPosition + ": cargando el mundo…");
            await Bootstrap.Begin(UseScreens);
            Sfx.Boot(gameObject, Bootstrap.Models, null);
            MusicController.Boot(gameObject, null, Bootstrap.Models.GameData).Lobby();
            Report("Leyendo tablas…");
            Tables = await Task.Run(() => TableLoader.Load(Bootstrap.Bundles.Load("TableData.resourceFile") ?? throw new InvalidOperationException("TableData.resourceFile no está descargado")));

            if (UseScreens)
            {   // 3c. selección / creación de personaje (el original las hace con los recursos de esas pantallas ya cargados)
                Bootstrap.SetTables(Tables.Xdt);
                if (loading != null) loading.Hide();
                CharInfo resumeCh = null;
                if (resuming && resumeUid != 0) foreach (var rc in Login.Characters) if (rc.Appearance != null && rc.Appearance.Uid == resumeUid) resumeCh = rc;
                var sel = GetComponent<CharSelectScreen>() ?? gameObject.AddComponent<CharSelectScreen>();
                var nameScr = GetComponent<NameCreateScreen>() ?? gameObject.AddComponent<NameCreateScreen>();
                var createScr = GetComponent<CharCreateScreen>() ?? gameObject.AddComponent<CharCreateScreen>();
                int slotForCreate = Login.Characters.Count == 0 ? 1 : -1;   // sin personajes: directo a crear, como el original
                ch = resumeCh;
                while (ch == null)
                {
                    int createSlot = slotForCreate; slotForCreate = -1;
                    if (createSlot < 0)
                    {
                        var r = await sel.Run(Login, Bootstrap, Tables);
                        if (r.Action == SelectAction.Quit) { Report("Sesión cerrada."); Login.Dispose(); return; }
                        if (r.Action == SelectAction.Play) { ch = r.Char; break; }
                        createSlot = r.Slot;
                    }
                    var nm = await nameScr.Run(Login, createSlot, Tables); if (nm == null) { if (Login.Characters.Count == 0) slotForCreate = 1; continue; }
                    var created = await createScr.Run(Login, nm, Bootstrap, Tables); if (created == null) { slotForCreate = Login.Characters.Count == 0 ? 1 : -1; continue; }
                    // vuelve a la selección con el personaje nuevo (el original sigue con el tutorial)
                }
                var np = new Vector3(ch.X / 100f, ch.Z / 100f, ch.Y / 100f);
                StartupStage(loading, "LOADING WORLD" );
                await Bootstrap.LoadWorldAsync(np);
                if (loading != null) { loading.Show("FREE ZONE"); loading.SetProgress("Entering the game…", 1f, 1f); }
            }

            // 4. personaje -> shard -> entrada. Los manejadores del mundo se enganchan ANTES de leer, para no perder PC_AROUND/NPC_AROUND.
            lastCharUid = ch.Appearance.Uid; int useShard = ChannelWarp.PendingShard > 0 ? ChannelWarp.PendingShard : Shard;   // tras OTHER_SHARD_SUCC: shard del buddy
            var ticket = await Login.SelectCharacterAsync(ch.Appearance.Uid, useShard); ChannelWarp.PendingShard = 0;
            Report("Entrando al shard " + ticket.Host + ":" + ticket.Port + "…");
            try   // el servidor valida la velocidad: la del original es AvatarElement[1].m_iRunSpeed (cnAvatarStatus), no un valor inventado
            {
                var av = Tables.Xdt.m_pAvatarTable != null ? Tables.Xdt.m_pAvatarTable.m_pAvatarData : null;
                if (av != null && av.Length > 1 && av[1] != null && av[1].m_iRunSpeed > 0) { RunSpeed = av[1].m_iRunSpeed; DebugWalkSpeed = RunSpeed / 100f; if (av[1].m_iJumpHeight > 0) JumpVelocity = av[1].m_iJumpHeight * 0.01f; Report("Velocidad de carrera del servidor: " + RunSpeed + " (" + DebugWalkSpeed + " m/s)"); }
            } catch (Exception e) { Report("No se pudo leer la velocidad de carrera: " + e.Message); }
            Shard_ = await Login.EnterShardAsync(ticket, string.IsNullOrEmpty(ok.Id) ? Username : ok.Id, g => Bootstrap.StartEntities(g.Dispatcher, Tables.Xdt, Tables.HNpc));
 if (loading != null) loading.Hide();
            Report("Dentro del mundo como " + Shard_.Enter.Self.FirstName + " (id " + Shard_.Enter.Id + ")"); shotsAt = Time.time + 12f;
            var worldSession = Shard_.Session;
            worldSession.Disconnected += e => { shardAlive = false; pendingDisconnect = "Game server connection closed: " + worldSession.EndReason +
                "\nRecent gameplay packets: " + worldSession.RecentTraffic; };
            await Shard_.Session.SendAsync(ClientPackets.LoadingComplete(Shard_.Enter.Id));
        }

        // ---- movimiento: servidor (x, y, z) = (cliente x, cliente z, cliente y) * 100 ----
        float nextSend; Vector3 lastPos; bool moving; bool hooked;
        static int ToServer(float v) => Mathf.RoundToInt(v * 100f);
        static ulong Now() => (ulong)(Time.realtimeSinceStartup * 1000f);   // como GetTimestampMs() del original
        // AvatarUtil.GetDirNum: tabla de teclas -> dirección (0 = parado)
        static readonly int[,] KeyToDir = { { 6, 5, 4 }, { 7, 0, 3 }, { 8, 1, 2 } };
        static byte DirNum(float h, float v) => (byte)KeyToDir[Mathf.Clamp(Mathf.RoundToInt(v), -1, 1) + 1, Mathf.Clamp(Mathf.RoundToInt(h), -1, 1) + 1];
        bool shardAlive = true; int moveLogs; float lastSendT, firstMoveT; int moveSent, stopSent; bool shotsDone; float shotsAt = 1e9f; [Tooltip("Prueba: fracción de la velocidad de carrera del servidor (descarta un límite de velocidad en el servidor)")] public float WalkScale = 1f;

        [Header("Cámara")] public bool FollowCamera = true; public Vector3 CameraOffset = new Vector3(0f, 6f, -9f); public float CameraSmooth = 12f, CameraTurn = 3f; bool camLogged, camInit; float camYaw, camPitch, camDist;
        // Modo de apuntado (cnPlayerCamera.SetOffsetEnabled/ToggleOffsetSide): con arma de modo 5/6 la cámara pasa al hombro (offsetXMax 1,15, lerp 7,35) y se dispara hacia la mira (centro de pantalla)
        public bool AimValid, AimActive; bool aimOn = true, aimLoaded; int aimSide = 1; float aimOffX, aimKeyT = float.MaxValue; public float CamYaw { get { return camYaw; } }
        const float AimOffsetMax = 1.15f, AimHold = 1f;
        // Valores de cnPlayerCamera (cliente original): fMinDistance 1, fMaxDistance 15, minRotationX -60, maxRotationX 75 (Awake), DefaultDistance 7, DefautlRotX 11.5, fHeight 2;
        // rueda: Distance += Mouse ScrollWheel (±0.1 por muesca) * fMouseSensivity (6) = 0.6 por muesca, sin suavizado; colisión: Physics.Raycast(pivote -> posición deseada, máscara 112641)
        [Header("Cámara (límites del original)")] public float CamMinDist = 1f, CamMaxDist = 15f, CamMinPitch = -60f, CamMaxPitch = 75f, CamDefaultDist = 7f, CamDefaultPitch = 11.5f, CamPivotHeight = 2f, ZoomPerNotch = 6f;
        [Tooltip("Capas contra las que choca la cámara: 112641 = capas 0, 11, 12, 13, 15, 16 (máscara del original). Las capas de DongBuilder son las del original. La capa 2 (agua) queda fuera.")] public int CameraCollisionMask = 112641; public bool CameraCollision = true;
        float nextDiag; int diagCount;
        void Diag()
        {
            if (Time.time < nextDiag || diagCount >= 4) return; if (Bootstrap != null && Bootstrap.Player != null && diagCount == 0) Debug.Log("[diag] " + Ground.Describe(Bootstrap.Player.position)); nextDiag = Time.time + 3f; diagCount++;
            var sb = new System.Text.StringBuilder("[diag] frame " + Time.frameCount + " t=" + Time.time.ToString("F1") + " | cámaras: ");
            foreach (var c in FindObjectsOfType<Camera>()) sb.Append("'" + c.name + "' pos=" + c.transform.position + " rot=" + c.transform.eulerAngles + " prof=" + c.depth + " on=" + c.enabled + " cull=" + c.cullingMask + " near/far=" + c.nearClipPlane + "/" + c.farClipPlane + " | ");
            var mp = Bootstrap != null ? Bootstrap.Player : null;
            sb.Append("main=" + (Camera.main != null ? Camera.main.name : "null") + " | jugador=" + (mp != null ? mp.name + " " + mp.position : "null") + " | fly=" + (FindObjectOfType<OpenFusion.World.OpenFusionCamera>() != null));
            Debug.Log(sb.ToString());
        }

        void LateUpdate()
        {
            Watchdog.At("GameClient.LateUpdate");
            Diag();
            var p = Bootstrap != null ? Bootstrap.Player : null; var cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>(); if (!FollowCamera || p == null || Shard_ == null || cam == null) return;
            if (!camLogged)
            {   // la cámara de vuelo libre (OpenFusionCamera) pelea con esta: se apaga
                camLogged = true; var fly = cam.GetComponent<OpenFusion.World.OpenFusionCamera>(); if (fly != null) fly.enabled = false;
                Report("Cámara '" + cam.name + "' en " + cam.transform.position + " -> sigo a " + p.name + " en " + p.position + (fly != null ? " (OpenFusionCamera desactivada)" : ""));
            }
            if (OpenFusion.World.NpcUI.Active && OpenFusion.World.NpcUI.Instance.TryGetCamera(out var npcPos, out var npcRot)) { float k = 1f - Mathf.Exp(-8f * Time.deltaTime); cam.transform.position = Vector3.Lerp(cam.transform.position, npcPos, k); cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, npcRot, k); return; }
            // cámara orbital: botón derecho = girar/inclinar (también hacia abajo para ver el cielo), rueda = zoom
            if (!camInit) { camInit = true; camDist = CamDefaultDist; camPitch = CamDefaultPitch; camYaw = 0f; }
            if (OpenFusion.World.TouchControls.Active)
            {   // táctil: un dedo gira la cámara, pellizco = zoom
                var lk = OpenFusion.World.TouchControls.Look; camYaw += lk.x; camPitch = Mathf.Clamp(camPitch - lk.y, CamMinPitch, CamMaxPitch);
                float pz = OpenFusion.World.TouchControls.Pinch; if (Mathf.Abs(pz) > 0.0001f) camDist = Mathf.Clamp(camDist - pz * 0.04f, CamMinDist, CamMaxDist);
            }
            if (!aimLoaded) { aimLoaded = true; aimOn = PlayerPrefs.GetInt("cameraActive", 1) != 0; aimSide = PlayerPrefs.GetInt("cameraSide", 0) == 1 ? -1 : 1; }
            if (AimValid && !OpenFusion.World.ChatHud.Typing && !OpenFusion.World.GameMenu.Blocking)
            {   // tecla "Toggle Aim" (F, confirmada por el usuario): pulsar = activar/desactivar, mantener 1 s = cambiar de hombro
                if (OpenFusion.World.ControlMap.Down(OpenFusion.World.ControlMap.K.CameraShoulderToggle, false)) aimKeyT = Time.time;
                else if (OpenFusion.World.ControlMap.Held(OpenFusion.World.ControlMap.K.CameraShoulderToggle, false) && Time.time - aimKeyT > AimHold && aimKeyT != float.MaxValue) { if (!aimOn) aimOn = true; else aimSide = -aimSide; aimKeyT = float.MaxValue; PlayerPrefs.SetInt("cameraActive", aimOn ? 1 : 0); PlayerPrefs.SetInt("cameraSide", aimSide < 0 ? 1 : 2); }
                else if (OpenFusion.World.ControlMap.Up(OpenFusion.World.ControlMap.K.CameraShoulderToggle, false) && aimKeyT != float.MaxValue) { aimOn = !aimOn; aimKeyT = float.MaxValue; PlayerPrefs.SetInt("cameraActive", aimOn ? 1 : 0); }
            }
            AimActive = AimValid && aimOn;
            aimOffX = Mathf.Lerp(aimOffX, AimActive ? aimSide * AimOffsetMax : 0f, Time.deltaTime * 7.35f);
            float camSens = OpenFusion.World.ControlMap.CameraSensitivity / 5f;
            if (Input.touchCount == 0 && Input.GetMouseButton(1)) { camYaw += Input.GetAxis("Mouse X") * CameraTurn * camSens; camPitch = Mathf.Clamp(camPitch - Input.GetAxis("Mouse Y") * CameraTurn * camSens * (OpenFusion.World.ControlMap.FlipCameraY ? -1f : 1f), CamMinPitch, CamMaxPitch); }
            if (!OpenFusion.World.ChatHud.Typing && !OpenFusion.World.GameMenu.Blocking && !OpenFusion.World.NpcUI.Active)
            {   // teclas/stick de camara, giro y zoom (ConfigurableInput: CameraX/CameraY/HorizontalTurn/CameraZoom)
                float ck = Time.deltaTime; camYaw += (OpenFusion.World.ControlMap.CameraX * PadLookSpeed * camSens + OpenFusion.World.ControlMap.Turn * TurnSpeed) * ck;
                camPitch = Mathf.Clamp(camPitch - OpenFusion.World.ControlMap.CameraY * PadLookSpeed * camSens * ck, CamMinPitch, CamMaxPitch);
                float zk = OpenFusion.World.ControlMap.Zoom; if (Mathf.Abs(zk) > 0.01f) camDist = Mathf.Clamp(camDist - zk * ZoomKeySpeed * ck, CamMinDist, CamMaxDist);
            }
            float wheel = Input.GetAxis("Mouse ScrollWheel"); if (Mathf.Abs(wheel) > 0.0001f) camDist = Mathf.Clamp(camDist - wheel * ZoomPerNotch, CamMinDist, CamMaxDist);   // arriba = acercar (el signo del original depende del InputManager, no verificable)
            var rotC = Quaternion.Euler(camPitch, camYaw, 0f); var pivot0 = p.position + Vector3.up * CamPivotHeight; var pivot = pivot0 + rotC * new Vector3(aimOffX, 0f, 0f);
            if (Mathf.Abs(aimOffX) > 0.01f) { RaycastHit ph; var po = pivot - pivot0; if (Physics.Raycast(pivot0, po, out ph, po.magnitude, CameraCollisionMask, QueryTriggerInteraction.Ignore)) pivot = pivot0 + po.normalized * Mathf.Max(0f, ph.distance - 0.1f); }   // el hombro no atraviesa paredes
            var want = pivot + rotC * new Vector3(0f, 0f, -camDist); float camLimit = camDist;
            if (CameraCollision)
            {   // cnPlayerCamera.PositionUpdate: rayo pivote -> cámara; si choca, la cámara va al punto de impacto + (pivote-impacto).normalizado * clamp(1 - dot(normal, dir), 0, dist - 0.1)
                var dirBack = (want - pivot).normalized; RaycastHit ch;
                if (camDist > 0.01f && Physics.Raycast(pivot, dirBack, out ch, camDist, CameraCollisionMask, QueryTriggerInteraction.Ignore))
                {
                    var toPivot = (pivot - ch.point).normalized; float push = Mathf.Clamp(1f - Vector3.Dot(ch.normal, toPivot), 0f, Mathf.Max(0f, ch.distance - 0.1f));
                    want = ch.point + toPivot * push; camLimit = (want - pivot).magnitude;
                }
            }
            float gy = Ground.Y(want, 40f) + 0.5f; if (want.y < gy) want.y = gy;   // la cámara no entra en el suelo
            var camNew = (cam.transform.position - want).sqrMagnitude < 0.000004f ? want : Vector3.Lerp(cam.transform.position, want, 1f - Mathf.Exp(-CameraSmooth * Time.deltaTime));
            var camOff = camNew - pivot; if (camOff.magnitude > camLimit + 0.01f) camNew = pivot + camOff.normalized * camLimit;   // el suavizado no deja la cámara detrás de una pared
            cam.transform.position = camNew;
            var aim = pivot + Vector3.up * Mathf.Max(0f, -camPitch) * 0.25f;   // al bajar la cámara se apunta más arriba: se ve el cielo
            cam.transform.rotation = Quaternion.LookRotation(aim - cam.transform.position, Vector3.up);
            AuditCamera(cam, p);
            if (AimActive && !PlayerLife.Active) p.rotation = Quaternion.Euler(0f, camYaw, 0f);   // SetAngle(RotationY): el avatar mira hacia donde apunta la cámara
        }

        public float PadLookSpeed = 140f, TurnSpeed = 100f, ZoomKeySpeed = 8f;   // grados/s con sensibilidad 5; giro por teclas Q/E; zoom por teclas en m/s
        public float StepHeight = 0.5f; public bool BlockOnObjects = true;
        void Update()
        {
            var disconnected = System.Threading.Interlocked.Exchange(ref pendingDisconnect, null);
            if (disconnected != null) { Debug.LogWarning(disconnected); RecordStartup(disconnected); }
            EquipBonusTick();
            Watchdog.At("GameClient.Update");
            if (Input.GetKeyDown(KeyCode.F2)) { SendMovement = !SendMovement; Report("Envío de movimiento al servidor: " + (SendMovement ? "ACTIVADO" : "desactivado")); }
            if (OpenFusion.World.NpcUI.Sender == null && FindSelf == this) OpenFusion.World.NpcUI.Sender = ownedSender;
            if (!tpHooked && Bootstrap != null && Bootstrap.Entities != null) { tpHooked = true; Bootstrap.Entities.Teleported += QueueServerRelocation; }
            var p = Bootstrap != null ? Bootstrap.Player : null; if (p == null || Shard_ == null) return;
            TestPackets(p);
            if (!shotsDone && Time.time > shotsAt) { shotsDone = true; StartCoroutine(OpenFusion.World.Shots.Run(p)); }
            if (Input.GetKeyDown(KeyCode.F9)) StartCoroutine(OpenFusion.World.Shots.Run(p));
            if (!hooked) { hooked = true; lastPos = p.position; }
            bool relocated = ApplyServerRelocation(p);
            if (TickLifeAnimation(p)) {
                if (moving && SendMovement && WorldConnected) _ = Send(PlayerMotionPackets.Stop(Now(), p.position));
                moving = false; lastPos = p.position; return;
            }
            if (relocated) return; // Never send stale coordinates or treat a server warp as walking velocity.
            if (WorldBootstrap.Travelling || OpenFusion.World.BroomFlight.Active || OpenFusion.World.TransportWindow.Sending) { lastPos = p.position; moving = false; return; }
            if (DebugWalker && !OpenFusion.World.NpcUI.Active) Walk(p);
            if (!SendMovement || Time.time < nextSend) return;
            nextSend = Time.time + SendInterval; float dt = Mathf.Max(0.05f, Time.time - lastSendT); lastSendT = Time.time;
            var pos = p.position; var d = pos - lastPos; bool now = d.sqrMagnitude > 1e-6f;
            int angle = (int)p.eulerAngles.y - 180;   // igual que el original: (int)fCurAngle - 180
            if (OpenFusion.World.BusSystem.OnBus && (now || moving))
            {   // sobre un autobus el original manda REQ_PC_MOVETRANSPORTATION (se comprueba antes que plataforma y cinta)
                byte bkey = now ? DirNum(OpenFusion.World.TouchControls.AxisRaw("Horizontal"), OpenFusion.World.TouchControls.AxisRaw("Vertical")) : (byte)0;
                if (OpenFusion.World.BusSystem.TrySend(p, d.x / dt, d.y / dt, d.z / dt, angle, bkey, RunSpeed)) { moving = now; lastPos = pos; return; }
            }
            if (OpenFusion.World.EpTriggers.OnSurface)
            {   // sobre plataforma / cinta el original manda REQ_PC_MOVEPLATFORM / REQ_PC_BELT en lugar de PC_MOVE / PC_STOP (cnAvatarThirdPersonMove.MovePacket)
                if (now || moving)
                {
                    byte skey = now ? DirNum(OpenFusion.World.TouchControls.AxisRaw("Horizontal"), OpenFusion.World.TouchControls.AxisRaw("Vertical")) : (byte)0;
                    if (OpenFusion.World.EpTriggers.TrySendSurface(p, d.x / dt, d.y / dt, d.z / dt, angle, skey, RunSpeed)) { moving = now; lastPos = pos; return; }
                }
            }
            if (now)
            {
                byte key = DirNum(OpenFusion.World.TouchControls.AxisRaw("Horizontal"), OpenFusion.World.TouchControls.AxisRaw("Vertical")); int sx = ToServer(pos.x), sy = ToServer(pos.z), sz = ToServer(pos.y);
                if (moveLogs++ < 8) Report("[mov] MOVE srv=(" + sx + "," + sy + "," + sz + ") alta-inicial=(" + Shard_.Enter.Self.X + "," + Shard_.Enter.Self.Y + "," + Shard_.Enter.Self.Z + ") v=(" + (d.x / dt).ToString("F2") + "," + (d.z / dt).ToString("F2") + "," + (d.y / dt).ToString("F2") + ") ang=" + angle + " tecla=" + key + " vel=" + RunSpeed + " t=" + Now());
                moveSent++; if (firstMoveT <= 0) firstMoveT = Time.realtimeSinceStartup; _ = Send(PlayerMotionPackets.Move(Now(), pos, d / dt, angle, key, RunSpeed));
            }
            else if (moving) { stopSent++; _ = Send(PlayerMotionPackets.Stop(Now(), pos)); }
            moving = now; lastPos = pos;
        }


        string lastHost; int lastPort; bool variantsRunning;
        /// F8: cierra la sesión y prueba varias formas de enviar MOVE quietos en conexiones nuevas, para ver cuál no corta el servidor. Usa el usuario/contraseña ya escritos en el Inspector.
        async Task Variants()
        {
            if (variantsRunning || Bootstrap == null) return; variantsRunning = true;
            try
            {
                Report("[var] cerrando la sesión actual y probando variantes (≈2 min)…"); try { Shard_?.Session.Dispose(); } catch { } try { Login?.Dispose(); } catch { } shardAlive = false; await Task.Delay(4000);
                var api = new ServerApi(Bootstrap.ServerUrl);
                string[] names = { "A base (MOVE quieto, cliTime=reloj)", "B cliTime = svrTime del servidor + transcurrido", "C cliTime = 0", "D un MOVE cada 1,2 s", "E paquete de 56 bytes (relleno)", "F solo PC_STOP" };
                for (int v = 0; v < names.Length; v++)
                {
                    LoginSession ls = null; EnteredShard sh = null; bool alive = true; int sent = 0; float t0 = 0f;
                    try
                    {
                        ls = await LoginSession.ConnectAsync(lastHost, lastPort, new LoginOptions { VerA = VersionA, VerB = VersionB, VerC = VersionC });
                        LoginSuccess ok; if (PreferSecureApi) { var ck = await api.LoginSecure(Username, Password); ok = await ls.LoginWithCookieAsync(ck.username ?? Username, ck.cookie); } else ok = await ls.LoginAsync(Username, Password);
                        var ch = ls.Characters[Math.Min(CharacterIndex, ls.Characters.Count - 1)];
                        var ticket = await ls.SelectCharacterAsync(ch.Appearance.Uid, Shard);
                        sh = await ls.EnterShardAsync(ticket, string.IsNullOrEmpty(ok.Id) ? Username : ok.Id, null);
                        sh.Session.Disconnected += e => alive = false;
                        await sh.Session.SendAsync(ClientPackets.LoadingComplete(sh.Enter.Id)); await Task.Delay(1500);
                        var se = sh.Enter.Self; t0 = Time.realtimeSinceStartup; int n = v == 3 ? 10 : 16;
                        for (int i = 0; i < n && alive; i++)
                        {
                            ulong cli = Now(); byte[] pkt;
                            if (v == 1) cli = sh.Enter.SvrTime + (ulong)((Time.realtimeSinceStartup - t0) * 1000f); else if (v == 2) cli = 0;
                            if (v == 5) pkt = ClientPackets.Stop(cli, se.X, se.Y, se.Z); else pkt = ClientPackets.Move(cli, se.X, se.Y, se.Z, 0f, 0f, 0f, se.Angle, 0, 0);
                            if (v == 4) Array.Resize(ref pkt, pkt.Length + 8);
                            await sh.Session.SendAsync(pkt); sent++; await Task.Delay(v == 3 ? 1200 : 400);
                        }
                        await Task.Delay(1200);
                        Report("[var] " + names[v] + ": " + (alive ? "VIVA tras " + sent + " paquetes" : "CAÍDA tras " + sent + " paquetes (" + (Time.realtimeSinceStartup - t0).ToString("F1") + " s)"));
                    }
                    catch (Exception e) { Report("[var] " + names[v] + " error: " + e.Message); }
                    finally { try { sh?.Session.Dispose(); } catch { } try { ls?.Dispose(); } catch { } }
                    await Task.Delay(4000);
                }
                Report("[var] terminado");
            }
            finally { variantsRunning = false; }
        }

        /// F7: 15 paquetes MOVE idénticos (quietos, en la posición de alta) cada 0,4 s. Si el servidor corta al 8.º, el límite es por número de paquetes y no por la posición.
        System.Collections.IEnumerator StillMoves(int n)
        {
            var se = Shard_.Enter.Self; Report("[test] F7: " + n + " MOVE quietos en (" + se.X + "," + se.Y + "," + se.Z + ") cada 0,4 s");
            for (int i = 1; i <= n && shardAlive; i++) { Report("[test] F7 MOVE " + i + "/" + n); _ = Send(ClientPackets.Move(Now(), se.X, se.Y, se.Z, 0f, 0f, 0f, se.Angle, 0, 0)); yield return new WaitForSeconds(0.4f); }
            Report("[test] F7 terminado, conexión " + (shardAlive ? "VIVA" : "caída"));
        }

        /// Pruebas de un solo paquete para averiguar cuál hace que el servidor corte: F3 STOP, F4 MOVE quieto, F5 MOVE con velocidad (sin desplazarse), F6 MOVE 1 m más allá.
        void TestPackets(Transform p)
        {
            if (Input.GetKeyDown(KeyCode.F8)) OpenFusion.World.FxDebug.Dump(p.position);
            if (Input.GetKeyDown(KeyCode.F7)) StartCoroutine(StillMoves(15));
            if (Input.GetKeyDown(KeyCode.F8)) _ = Variants();
            var pos = p.position; int x = ToServer(pos.x), y = ToServer(pos.z), z = ToServer(pos.y); int ang = (int)p.eulerAngles.y - 180;
            if (Input.GetKeyDown(KeyCode.F3)) { Report("[test] F3 STOP en " + x + "," + y + "," + z); _ = Send(ClientPackets.Stop(Now(), x, y, z)); }
            if (Input.GetKeyDown(KeyCode.F4)) { Report("[test] F4 MOVE quieto en " + x + "," + y + "," + z + " ang=" + ang); _ = Send(ClientPackets.Move(Now(), x, y, z, 0f, 0f, 0f, ang, 0, 0)); }
            if (Input.GetKeyDown(KeyCode.F5)) { Report("[test] F5 MOVE vel=6 en " + x + "," + y + "," + z + " ang=" + ang); _ = Send(ClientPackets.Move(Now(), x, y, z, 6f, 0f, 0f, ang, 3, 600)); }
            if (Input.GetKeyDown(KeyCode.F6)) { Report("[test] F6 MOVE +1 m en " + (x + 100) + "," + y + "," + z + " ang=" + ang); _ = Send(ClientPackets.Move(Now(), x + 100, y, z, 6f, 0f, 0f, ang, 3, 600)); }
        }

        Animation pAnim; string pClip; float pAnimTry;
        // ---- vehiculo (cnAvatarAnimation.iVehicle / StrModifyForVehicle / Status.UpdateVehicleSpd / cnAvatarThirdPersonMove.UpdateVehicleMovement). Lo gestiona World/VehicleRide ----
        /// <summary>iVehicle: 0 a pie, 1 tabla ("board_"), 2/3 scooter ("scooter_").</summary>
        public int Vehicle;
        /// <summary>vehicleSpeed (m/s) = ItemElement.m_iUp_runSpeed * 0,01.</summary>
        public float VehicleSpeed;
        [Tooltip("Multiplicador de la velocidad del vehiculo (1 = original: hasta 1,35 x vehicleSpeed hacia delante). Bajarlo si el servidor rechaza el movimiento.")] public float VehicleSpeedMul = 1f;
        int baseRunSpeed; Vector3 vehMom, lastWalkVel;
        public bool IsAirborne { get { return airborne; } }
        /// <summary>Velocidad horizontal actual del vehiculo (m/s) para el tono del motor.</summary>
        public float VehicleMomentumMag { get { return new Vector2(vehMom.x, vehMom.z).magnitude; } }
        /// <summary>VEHICLE_ON/OFF_SUCC: iVehicle = m_iEquipType, BaseAttrib.iRunSpeed = m_iUp_runSpeed (es lo que se envia en MOVE/JUMP), AvatarMoveForce(0). type 0 = a pie.</summary>
        public void SetVehicle(int type, int vehicleRunSpeed)
        {
            if (type > 0 && Vehicle == 0) { baseRunSpeed = RunSpeed; vehMom = lastWalkVel; vehMom.y = 0f; }   // SetMomentumToTempKM: conserva la velocidad al montar
            if (type <= 0 && Vehicle > 0) { if (baseRunSpeed > 0) RunSpeed = baseRunSpeed; vehMom = Vector3.zero; }
            Vehicle = Mathf.Max(0, type);
            if (Vehicle > 0) { if (vehicleRunSpeed > 0) RunSpeed = vehicleRunSpeed; VehicleSpeed = RunSpeed * 0.01f; if (swimming) swimming = false; EndEmote(); }
            else VehicleSpeed = 0f;
            pClip = null; fidgetAt = 0f; fidgetEnd = 0f;
        }
        /// <summary>StrModifyForVehicle + GetAnimationName: run/runback -> board_run..., stand* -> board_stand1, jump* e inven con prefijo; null = sin cambio (o el clip no existe).</summary>
        string VehicleClip(string c)
        {
            if (pAnim == null) return null; string pre = Vehicle == 1 ? "board_" : (Vehicle == 2 || Vehicle == 3) ? "scooter_" : null; if (pre == null) return null;
            string n;
            if (c == "run" || c == "runback" || c == "runpose" || c == "runbackpose") n = pre + c;
            else if (c.StartsWith("stand")) n = pre + "stand1";
            else if (c.StartsWith("jump") || c == "inven") n = pre + c;
            else return null;
            return pAnim[n] != null ? n : null;
        }
        /// <summary>UpdateVehicleAcceleration / UpdateVehicleCoasting: movimiento con inercia. Devuelve el 'dir' equivalente (velocidad / (DebugWalkSpeed*WalkScale)) para el codigo de movimiento existente.</summary>
        Vector3 VehicleDir(Vector3 dir, Vector3 raw, Transform p)
        {
            float dt = Time.deltaTime; bool ground = ccGrounded && !airborne; float vs = VehicleSpeed * VehicleSpeedMul;
            if (dir.sqrMagnitude > 0f)
            {
                float rate, top;
                if (raw.z < 0f) { rate = ground ? 0.75f : 0.375f; top = vs * 0.54f; }
                else if (Mathf.Abs(raw.x) > 0.9f && raw.z > 0.9f) { rate = ground ? 0.8f : 0.45f; top = vs * 1.215f; }
                else if (Mathf.Abs(raw.x) > 0.9f && raw.z < 0.1f) { rate = vehMom.magnitude > 10f ? (ground ? 2.5f : 1.25f) : (ground ? 0.9f : 0.45f); top = vs * 0.9f; }
                else { rate = ground ? 0.85f : 0.5f; top = vs * 1.35f; }
                var want = dir.normalized * top;
                vehMom = Vector3.Lerp(vehMom, want, rate * dt);
                float dot = Vector3.Dot(dir.normalized, vehMom.normalized);
                vehMom = Vector3.Lerp(vehMom, want, dot < 0.8f ? 0.0001f : 3f * dt);
                if (vehMom.sqrMagnitude > 0.01f && Physics.Raycast(p.position + Vector3.up, vehMom.normalized, 0.5f, ColMask, QueryTriggerInteraction.Ignore))
                {   // choque con una pared: rebote a 7,5 m/s y sonido vehicle_jumplnd
                    vehMom = -vehMom.normalized * 7.5f;
                    if (OpenFusion.World.Sfx.Instance != null) OpenFusion.World.Sfx.Instance.At("sound/vehicle_jumplnd.wav", p.position, 1f, false, 0.15f);
                }
                float mag = new Vector2(vehMom.x, vehMom.z).magnitude;
                if (mag > top && mag > 0f) { float k = top / mag; vehMom.x *= k; vehMom.z *= k; }
            }
            else
            {
                float mag = new Vector2(vehMom.x, vehMom.z).magnitude;
                if (mag > 0.1f) { float f = Mathf.Pow(ground ? 0.98f : 0.997f, dt * 60f); vehMom.x *= f; vehMom.z *= f; }   // el original multiplica por frame (60 fps)
                else vehMom = Vector3.zero;
            }
            vehMom.y = 0f;
            return vehMom / Mathf.Max(0.0001f, DebugWalkSpeed * WalkScale);
        }
        // Estilo de animación según el arma (cnAvatarStatus.iAniStyle: 1 stick, 2 pistol, 3 rifle, 4 rocket, 6 bomb): run/stand/jump/attack/wound llevan el prefijo del estilo.
        public int AniStyle; public float CombatUntil; public void EnterCombat() { CombatUntil = Time.time + 5f; }   // GameFrame.SendCombatMode: fMaxCombatTime = 5
        // ---- emotes (cnAvatarAnimation.AvatarEmote / EndEmote): los dispara EmoteSystem al recibir REP_PC_AVATAR_EMOTES_CHAT; movimiento, salto o ataque los cortan ----
        /// <summary>Modo inventario propio (cnAvatarAnimation.bInvenMode): lo fija World/InvenMode; en reposo reproduce el clip inven.</summary>
        public bool InvenMode;
        public bool EmoteActive; float emoteUntil; int emoteCode; string emoteClip; int emoteHolds;
        /// <summary>Lo llama al terminar el clip del emote propio (code, clip); devuelve true si se reenvia otro (bucle de baile / sentado): se mantiene el estado 0,7 s a la espera del eco del servidor.</summary>
        public System.Func<int, string, bool> EmoteEnded;
        public bool PlayEmote(string clip, int code)
        {
            if (pAnim == null || swimming || airborne || Dashing) return false; var st = pAnim[clip]; if (st == null) return false;
            st.wrapMode = WrapMode.Once; st.time = 0f; st.speed = 1f; pAnim.CrossFade(clip, 0.15f); pClip = clip;
            EmoteActive = true; emoteUntil = Time.time + st.length; emoteCode = code; emoteClip = clip; emoteHolds = 0; fidgetAt = 0f; fidgetEnd = 0f; atkUntil = 0f; return true;
        }
        public void EndEmote() { if (!EmoteActive) return; EmoteActive = false; pClip = null; var fs = GetComponentInChildren<OpenFusion.World.AnimEventSink>(true); if (fs != null) fs.emote_face_off(""); if (OpenFusion.World.EmoteSystem.Instance != null) OpenFusion.World.EmoteSystem.Instance.ResetLoop(); }
         public void ResetAnimClip() { pClip = null; } static readonly string[] StylePrefix = { "", "stick", "pistol", "rifle", "rocket", "", "bomb" };
        string Styled(string c)
        {
            if (Vehicle > 0) { var vc = VehicleClip(c); if (vc != null) return vc; }
            if (AniStyle <= 0 || AniStyle >= StylePrefix.Length || pAnim == null || StylePrefix[AniStyle].Length == 0) return c;
            if (!(c == "run" || c == "runback" || c.StartsWith("stand") || c.StartsWith("jump") || c.StartsWith("attack") || c.StartsWith("wound") || c == "ready")) return c;
            var n = StylePrefix[AniStyle] + c; return pAnim[n] != null ? n : c;
        }
        int atkNum = 1; float atkUntil, lastMoveT; bool atkLogged; Transform walkT;
        /// cnAvatarAnimation.AvatarAttack: parado -> attackN completo; en movimiento -> attackNupper (capa superior). N rota 1..3 si el clip existe.
        public void PlayAttack(float faceYaw, float cool = 0.65f, bool continued = false)
        {
            if (pAnim == null || swimming) return; EndEmote();
            bool moving = Time.time - lastMoveT < 0.15f; /* el modo combate ya no arranca al atacar: GameFrame.SendCombatMode se llama al recibir resultados (ver EnterCombat) */
            if (!atkLogged) { atkLogged = true; var sb = new System.Text.StringBuilder("[atk] estilo=" + AniStyle + " cooltime=" + cool.ToString("F2") + " clips de combate: "); foreach (AnimationState st0 in pAnim) { var nm = st0.name; if (nm.Contains("attack") || nm.Contains("ready") || nm.Contains("wound") || nm.Contains("die") || nm.Contains("dead")) sb.Append(nm + "(" + st0.length.ToString("F2") + ") "); } Debug.Log(sb.ToString()); }
            if (!continued) atkNum = 1;   // AvatarAttack(bUpdate): sin ataque continuado vuelve a attack1
            string full = Styled("attack" + atkNum), up = Styled("attack" + atkNum + "upper");
            if (pAnim[full] == null) { atkNum = 1; full = Styled("attack1"); up = Styled("attack1upper"); }
            // parado ("stand"/"ready"): attackN completo (CrossFade 0,15) + attackNupper encima (PlayNow); en movimiento sólo el de la capa superior.
            // SetAniTypeSpeed(Attack, cooltime): si el cooltime es menor que el clip, el clip se acelera para durar exactamente el cooltime (ataques encadenados sin pausa)
            if (!moving && pAnim[full] != null)
            {
                var st = pAnim[full]; st.wrapMode = WrapMode.Once; st.time = 0f; st.speed = cool < st.length ? cool / st.length : 1f; pAnim.CrossFade(full, 0.15f); pClip = full; atkUntil = Time.time + st.length / st.speed * 0.9f;
            }
            if (pAnim[up] != null) { var su = pAnim[up]; su.layer = 103; su.wrapMode = WrapMode.Once; su.time = 0f; su.speed = cool < su.length ? cool / su.length : 1f; pAnim.CrossFade(up, 0.15f); }
            else if (moving && pAnim[full] != null) { var st = pAnim[full]; st.wrapMode = WrapMode.Once; st.time = 0f; st.speed = cool < st.length ? cool / st.length : 1f; pAnim.CrossFade(full, 0.1f); pClip = full; }
            if (continued) atkNum = pAnim[Styled("attack" + (atkNum + 1))] != null ? atkNum + 1 : 1; else atkNum = 1;
            if (walkT != null && !float.IsNaN(faceYaw)) walkT.rotation = Quaternion.Euler(0f, faceYaw, 0f);
        }
        void SetAnim(Transform p, string clip)
        {
            if (pAnim == null) { if (Time.time < pAnimTry) return; pAnimTry = Time.time + 1f; pAnim = p.GetComponentInChildren<Animation>(true); if (pAnim == null) return; pAnim.cullingType = AnimationCullingType.AlwaysAnimate; }
            clip = Styled(clip);
            if (pClip == clip) return; var st = pAnim[clip]; if (st == null) return;
            bool fromGest = pClip != null && (pClip.EndsWith("stand2") || pClip.EndsWith("stand3") || pClip.EndsWith("stand4")); st.wrapMode = WrapMode.Loop; pAnim.CrossFade(clip, fromGest ? 0.3f : 0.15f); pClip = clip; Debug.Log("[anim] bucle " + clip + " combate=" + (Time.time < CombatUntil));
        }
        [Header("Salto")] public float JumpVelocity = 5.68f, Gravity = 10f; // original: v0 = AvatarElement[1].m_iJumpHeight*0.01 (568), fGravity = 10, velocidad terminal = -fGravity
        float nextJumpPkt; public bool SendJump = true;
        bool tpHooked; volatile bool tpPending; Vector3 tpPos; string pendingDisconnect;
        public void QueueServerRelocation(int x, int y, int z) { tpPos = PlayerAvatarFactory.ToClient(x, y, z); tpPending = true; }
        bool ApplyServerRelocation(Transform player)
        {
            if (!tpPending) return false;
            tpPending = false; player.position = tpPos; lastPos = tpPos; moving = airborne = false; vy = 0; dashT = 0;
            lastSendT = Time.time; nextSend = Time.time + SendInterval;
            GetComponent<TouchControls>()?.CancelForSurfaceChange();
            Report("[warp] applied authoritative position before movement: " + tpPos); return true;
        }
        bool airborne; float vy, jumpT, landUntil, fidgetAt, fidgetEnd;
        // ---- habilidades de nano de movimiento (cnAvatarAttack.NanoSkillUse -> cnAvatarThirdPersonMove.Jump / RocketDash) ----
        float dashT, dashSpeed; public bool Dashing { get { return dashT > 0f; } }
        /// Skill 29/40: Jump(iDefaultJumpHeight*0.01 + valueA*0.01). En el cliente 2.5 el 'extra de gravedad' sólo existe con el salto variable (opción), que aquí no hay, así que TempDisableExtraG no hace nada.
        // ---- buffs de velocidad / salto (Status.UpdateBuff: eCSTB 3 = salto, 1 = velocidad positiva, 8 = negativa; eTBU 1/3 alta o cambio, 2 baja) ----
        float baseJump, buffJump; int baseRun, buffPos, buffNeg; bool buffBase;
        /// <summary>sP_FE2CL_PC_BUFF_UPDATE propio. iValue en unidades del servidor (cm / cm/s), igual que BaseAttrib.iJumpHeight / iRunSpeed.</summary>
        public static void ApplyBuff(int cstb, int tbu, int value) { var g = FindSelf; if (g != null) g.OnBuff(cstb, tbu, value); }
        public static GameClient FindSelf;
        public bool WorldConnected => shardAlive && Shard_ != null && Shard_.Session != null;
        void OnBuff(int cstb, int tbu, int value)
        {
            if (!buffBase) { baseJump = JumpVelocity; baseRun = RunSpeed; buffBase = true; }
            bool on = tbu == 1 || tbu == 3; if (!on && tbu != 2) return; int v = on ? value : 0;
            if (cstb == 3) buffJump = v * 0.01f; else if (cstb == 1) buffPos = v; else if (cstb == 8) buffNeg = v; else return;
            RecalcMove();
            Debug.Log("[buff] cstb=" + cstb + " tbu=" + tbu + " valor=" + value + " -> salto=" + JumpVelocity.ToString("0.00") + " vel=" + RunSpeed);
        }
        int equipJump, equipRun; float nextEqTick;
        /// <summary>CurAttrib = Base + sum(m_iUp_jumpHeight / m_iUp_runSpeed de los objetos equipados 0..6) + buffs (cnAvatarStatus.UpdateAttrib). A pie; con vehiculo no se aplica.</summary>
        void RecalcMove()
        {
            if (!buffBase) { baseJump = JumpVelocity; baseRun = RunSpeed; buffBase = true; }
            if (Vehicle != 0) return;
            JumpVelocity = baseJump + (equipJump + buffJump * 100f) * 0.01f; RunSpeed = Mathf.Max(0, baseRun + equipRun + buffPos - buffNeg); DebugWalkSpeed = RunSpeed / 100f;
        }
        void EquipBonusTick()
        {
            if (Time.time < nextEqTick) return; nextEqTick = Time.time + 1f;
            try
            {
                var ents = Bootstrap != null ? Bootstrap.Entities : null; if (ents == null || ents.SelfId == 0 || Tables == null || Tables.Xdt == null) return;
                var app = ents.AppOf(ents.SelfId); if (app == null || app.Equip == null) return;
                int j = 0, rn = 0;
                for (int sl = 0; sl < 7 && sl < app.Equip.Length; sl++)
                {
                    var it = app.Equip[sl]; if (it.Id <= 0) continue;
                    var tb = OpenFusion.World.ItemTbl.Eq(Tables.Xdt, sl); if (tb == null || tb.m_pItemData == null || it.Id >= tb.m_pItemData.Length || tb.m_pItemData[it.Id] == null) continue;
                    j += tb.m_pItemData[it.Id].m_iUp_jumpHeight; rn += tb.m_pItemData[it.Id].m_iUp_runSpeed;
                }
                if (j != equipJump || rn != equipRun) { equipJump = j; equipRun = rn; RecalcMove(); Debug.Log("[equip] bonus salto=" + j + " velocidad=" + rn + " -> salto=" + JumpVelocity.ToString("0.00") + " vel=" + RunSpeed); }
            }
            catch (Exception) { }
        }
        public void SkillJump(float extra) { if (swimming || Dashing) return; airborne = true; vy = JumpVelocity + extra; jumpT = Time.time; nextJumpPkt = Time.time + 0.3f; PlayOnce("jumpstart"); }
        /// RocketDash(35): 0,5556 s hacia delante de la cámara, velocidad 35 m/s menos 60 m/s², sin gravedad (fVelocityZ = 0).
        public void SkillDash(float speed) { dashSpeed = speed; dashT = 0.5555555f; airborne = false; vy = 0f; landUntil = 0f; pClip = null; }
        void DashStep(Transform p)
        {   // Do3DDashMovement
            float dt = Time.deltaTime; var f = Quaternion.Euler(0f, camYaw, 0f) * Vector3.forward;
            var np = p.position + f * Mathf.Max(0f, dashSpeed) * dt; if (BlockOnObjects) np = ResolveHorizontal(p, p.position, np);
            float g = GroundOrWater(np, 20f, out bool wet); if (g > np.y) np.y = g;
            p.position = np; Physics.SyncTransforms(); p.rotation = Quaternion.Euler(0f, camYaw, 0f); lastMoveT = Time.time;
            dashSpeed -= 60f * dt; dashT -= dt;
            if (pAnim != null && pAnim["dash"] != null) SetAnim(p, "dash"); else SetAnim(p, "run");
            if (dashT <= 0f) { dashT = 0f; if (p.position.y > GroundOrWater(p.position, 40f, out bool w2) + 0.15f) { airborne = true; vy = 0f; jumpT = Time.time; } pClip = null; }
        }
        void PlayOnce(string clip, float fade = 0.08f)
        {
            if (pAnim == null) return; EndEmote(); clip = Styled(clip); var st = pAnim[clip]; if (st == null) return;
            st.wrapMode = WrapMode.Once; st.time = 0f; pAnim.CrossFade(clip, fade); pClip = clip; Debug.Log("[anim] una vez " + clip + " (" + st.length.ToString("0.0") + " s)");
        }
        // ---- nado (cliente original: cnAvatarThirdPersonMove / cnAvatarAnimation) ----
        // El agua es un colisionador con tag "Water" en el que se apoya el avatar (misma velocidad y gravedad; sin salto: ProcessJumpInput exige !ani.bWater).
        // bWater -> clips swim/swimidle (swimback/swimleft/swimright según la tecla), sin skills (CacuSkillReady) y sonidos SFX_Landing_Water / SFX_EmergeWater (x0.5).
        // El paquete MOVE no lleva ninguna marca de nado: misma tecla (DirNum) y la altura real (la de la superficie al nadar).
        [Header("Agua")] public bool EnableSwim = true;
        [Tooltip("Profundidad (superficie - suelo) a partir de la cual se nada. El original no tiene umbral: el CharacterController se apoya en el colisionador de agua.")] public float SwimEnterDepth = 0.9f;
        [Tooltip("Histéresis: se deja de nadar por debajo de esta profundidad")] public float SwimExitDepth = 0.5f;
        [Tooltip("Altura del origen del avatar sobre la superficie (el original lo apoya en el colisionador: 0)")] public float SwimYOffset = 0f;
        [Tooltip("Opcionales: Sound/SFX_Landing_Water.wav y Sound/SFX_EmergeWater.wav (volumen 0.5 x SFX)")] public AudioClip SplashIn, SplashOut; public float SfxVolume = 1f;
        bool swimming; float nextSwimLog;
        public bool IsSwimming => swimming;
        /// Suelo bajo q, o la superficie del agua si el agua es suficientemente profunda (equivale a que el avatar se apoye en el colisionador "Water").
        float GroundOrWater(Vector3 q, float search, out bool water)
        {
            float g = Ground.Y(q, search); water = false;
            if (EnableSwim && Vehicle <= 0 && WaterSurface.Count > 0 && WaterSurface.TryGetSurface(q, out float wy))
            {
                if (Time.time > nextSwimLog) { nextSwimLog = Time.time + 2f; Debug.Log("[swim] agua en " + q + ": superficie y=" + wy.ToString("F2") + " suelo y=" + g.ToString("F2") + " profundidad=" + (wy - g).ToString("F2") + " nadando=" + swimming); }
                if (wy - g >= (swimming ? SwimExitDepth : SwimEnterDepth)) { water = true; return wy + SwimYOffset; }
            }
            return g;
        }
        // cnAvatarAnimation.AvatarMove con bWater: iMoveDir 1 -> swim, 2/3 -> swimright, 7/8 -> swimleft, resto (4..6) -> swimback; quieto -> swimidle. En el agua el avatar mira hacia donde mira la cámara.
        void SwimAnim(Transform p, bool moving, Vector3 raw)
        {
            string c = "swimidle";
            if (moving) { int d = DirNum(raw.x, raw.z); c = d == 1 ? "swim" : (d == 2 || d == 3) ? "swimright" : (d == 7 || d == 8) ? "swimleft" : "swimback"; }
            if (pAnim != null && pAnim[c] == null) c = moving ? (pAnim["swim"] != null ? "swim" : "run") : "stand1";   // si el avatar no trae el clip
            SetAnim(p, c);
        }
        void SetSwimming(bool on, Vector3 at)
        {
            if (on == swimming) return; swimming = on; Debug.Log("[swim] nado=" + on + " en " + at); fidgetAt = 0f; fidgetEnd = 0f; pClip = null;   // fuerza el cambio de clip (AvatarWater -> AvatarStand)
            var clip = on ? SplashIn : SplashOut; if (clip != null) AudioSource.PlayClipAtPoint(clip, at, 0.5f * SfxVolume);
            else if (Sfx.Instance != null) Sfx.Instance.At(on ? "sound/sfx_landing_water.wav" : "sound/sfx_emergewater.wav", at, 0.5f);   // SoundUtil.PlaySoundAtPoint("Sound/SFX_...", SFX x 0,5)
        }

        // ---- colisión horizontal con los colisionadores del mapa (CharacterController del original: radio ~0.35, altura ~1.7, stepOffset) ----
        public float BodyRadius = 0.35f, BodyHeight = 1.7f;
        static readonly Collider[] colBuf = new Collider[16]; int colLogs;
        const int ColMask = ~((1 << 2) | (1 << 30));   // sin el colisionador del agua (capa 2) ni la cámara del retrato (capa 30)
        bool BodyHits(Transform self, Vector3 pos, float baseY, out Collider hit)
        {
            hit = null;
            var a = new Vector3(pos.x, baseY + StepHeight + BodyRadius, pos.z); var b = new Vector3(pos.x, baseY + BodyHeight - BodyRadius, pos.z);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, BodyRadius, colBuf, ColMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) { var c = colBuf[i]; if (c == null || c is TerrainCollider || c.transform.IsChildOf(self)) continue; hit = c; return true; }
            return false;
        }
        Vector3 ResolveHorizontal(Transform self, Vector3 from, Vector3 to)
        {
            if (!BodyHits(self, to, from.y, out var h)) return to;
            if (BodyHits(self, from, from.y, out var _)) return to;   // ya está dentro de algo (spawn, teletransporte): se deja salir
            if (colLogs < 6) { colLogs++; Debug.Log("[col] bloqueado por '" + h.name + "' (" + h.GetType().Name + ", capa " + h.gameObject.layer + ") en " + to); }
            var tx = new Vector3(to.x, to.y, from.z); if (!BodyHits(self, tx, from.y, out var _)) return tx;
            var tz = new Vector3(from.x, to.y, to.z); if (!BodyHits(self, tz, from.y, out var _)) return tz;
            return new Vector3(from.x, to.y, from.z);
        }
        void Walk(Transform p)
        {
            walkT = p;
            ReapplyIdleOnce(p);
            var raw = (OpenFusion.World.ChatHud.Typing || OpenFusion.World.GameMenu.Blocking) ? Vector3.zero : new Vector3(OpenFusion.World.TouchControls.AxisRaw("Horizontal"), 0f, OpenFusion.World.TouchControls.AxisRaw("Vertical"));
            var dir = raw.sqrMagnitude > 0.01f ? Quaternion.Euler(0f, camYaw, 0f) * raw.normalized : Vector3.zero;   // relativo a la cámara
            if (pAnim == null) SetAnim(p, "stand1");   // inicializa pAnim
            if (dashT > 0f) { DashStep(p); return; }
            Vector3 mdir = dir; if (Vehicle > 0) mdir = VehicleDir(dir, raw, p); else lastWalkVel = dir * DebugWalkSpeed * WalkScale;
            if (!airborne && !swimming && !OpenFusion.World.ChatHud.Typing && !OpenFusion.World.GameMenu.Blocking && (OpenFusion.World.ControlMap.Down(OpenFusion.World.ControlMap.K.Jump, false) || OpenFusion.World.TouchControls.ConsumeJump()))
            {
                airborne = true; vy = (Vehicle > 0 && buffBase) ? baseJump : JumpVelocity; jumpT = Time.time; nextJumpPkt = Time.time + 0.3f; PlayOnce("jumpstart");
                if (SendJump && SendMovement)
                {
                    int ang = (int)p.eulerAngles.y - 180; byte key = DirNum(raw.x, raw.z);
                    _ = Send(PlayerMotionPackets.Jump(Now(), p.position, new Vector3(mdir.x * DebugWalkSpeed, vy, mdir.z * DebugWalkSpeed), ang, key, RunSpeed));
                }
            }
            bool ccDone = UseCharacterController && WalkCC(p, mdir, raw);
            if (!ccDone) {
            var np = p.position;
            if (mdir.sqrMagnitude > 0f) np += mdir * DebugWalkSpeed * WalkScale * Time.deltaTime;
            if (BlockOnObjects && mdir.sqrMagnitude > 0f) np = ResolveHorizontal(p, p.position, np);   // paredes, troncos, estructuras: colisionadores del mapa
            if (airborne)
            {
                vy -= Gravity * Time.deltaTime; if (vy < -Gravity) vy = -Gravity; np.y += vy * Time.deltaTime;
                if (SendJump && SendMovement && Time.time >= nextJumpPkt) { nextJumpPkt = Time.time + 0.3f; int ang2 = (int)p.eulerAngles.y - 180; _ = Send(PlayerMotionPackets.Jump(Now(), p.position, new Vector3(dir.x * DebugWalkSpeed, vy, dir.z * DebugWalkSpeed), ang2, DirNum(raw.x, raw.z), RunSpeed)); } float g = GroundOrWater(new Vector3(np.x, np.y, np.z), 40f, out bool wetLand);
                if (np.y <= g && vy < 0f)
                {
                    np.y = g; airborne = false;
                    if (wetLand) { SetSwimming(true, np); landUntil = 0f; }   // caer al agua: se apoya en la superficie y nada (sin animación de aterrizaje)
                    else { landUntil = Time.time + 0.2f; PlayOnce("jumpend"); }
                }
                else if (Time.time - jumpT > 0.25f) SetAnim(p, "jump");
            }
            else
            {   // colisión con objetos: se sube un escalón de hasta StepHeight (silla, cajas); más alto = pared, no avanza (deslizamiento por ejes)
                float gy2 = GroundOrWater(np, 20f, out bool wetHere);
                if (BlockOnObjects && mdir.sqrMagnitude > 0f && gy2 - p.position.y > StepHeight)
                {
                    var tx = new Vector3(np.x, np.y, p.position.z); var tz = new Vector3(p.position.x, np.y, np.z);
                    float gx = GroundOrWater(tx, 20f, out bool wetX), gz = GroundOrWater(tz, 20f, out bool wetZ);
                    if (gx - p.position.y <= StepHeight) { np = tx; gy2 = gx; wetHere = wetX; } else if (gz - p.position.y <= StepHeight) { np = tz; gy2 = gz; wetHere = wetZ; } else { np.x = p.position.x; np.z = p.position.z; gy2 = p.position.y; wetHere = swimming; }
                }
                np.y = gy2; if (wetHere != swimming) SetSwimming(wetHere, np);
            }
            p.position = np;
            }
            if (swimming) p.rotation = Quaternion.Euler(0f, camYaw, 0f); else if (dir.sqrMagnitude > 0f) p.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);   // el original (zipline) pone yaw = rumbo del movimiento
            if (dir.sqrMagnitude > 0f) lastMoveT = Time.time;
            if (airborne || Time.time < landUntil) return;
            if (EmoteActive)
            {   // cnAvatarAnimation.EndEmote: cualquier movimiento lo corta; al acabar el clip, EmoteEnded decide si se reenvia (baile / sentado)
                if (dir.sqrMagnitude > 0f) EndEmote();
                else if (Time.time < emoteUntil) return;
                else
                {
                    var cb = EmoteEnded; if (cb != null && emoteHolds < 3 && cb(emoteCode, emoteClip)) { emoteHolds++; emoteUntil = Time.time + 0.7f; return; }
                    EmoteActive = false; pClip = null;
                }
            }
            if (Time.time < atkUntil && dir.sqrMagnitude <= 0f) return;
            if (InvenMode && dir.sqrMagnitude <= 0f && !swimming) { fidgetAt = 0f; fidgetEnd = 0f; SetAnim(p, "inven"); return; }   // SetInvenMotion: clip inven (board_inven / scooter_inven via Styled)
            if (Vehicle > 0)
            {   // AvatarMoveForce / GetStandName con iVehicle: board_run (board_runback hacia atras) o board_stand1; sin gestos de espera ni 'ready' de combate
                fidgetAt = 0f; fidgetEnd = 0f; byte vk = DirNum(raw.x, raw.z);
                SetAnim(p, dir.sqrMagnitude <= 0f ? "stand1" : (vk >= 4 && vk <= 6) ? "runback" : "run"); return;
            }
            if (!swimming && dir.sqrMagnitude <= 0f && Time.time < CombatUntil && pAnim != null && pAnim[Styled("ready")] != null) { fidgetAt = 0f; fidgetEnd = 0f; SetAnim(p, "ready"); return; }   // GetStandName: en combate 'ready' (con prefijo del arma); pasados 5 s vuelve a stand1
            if (swimming) { SwimAnim(p, dir.sqrMagnitude > 0f, raw); return; }   // cnAvatarAnimation.AvatarMove/AvatarStand con bWater: swim / swimidle (sin gestos de espera)
            if (dir.sqrMagnitude > 0f) { fidgetAt = 0f; fidgetEnd = 0f; SetAnim(p, "run"); return; }
            // reposo: stand1 en bucle y, de vez en cuando, un gesto de espera (stand2/stand3/stand4) como en el original
            if (Time.time < fidgetEnd) return;
            if (fidgetAt <= 0f) fidgetAt = Time.time + UnityEngine.Random.Range(6f, 12f);
            if (Time.time >= fidgetAt && pAnim != null)
            {
                var pick = new[] { "stand2", "stand3", "stand4" }[UnityEngine.Random.Range(0, 3)]; var st = pAnim[pick];
                if (st != null) { PlayOnce(pick, 0.3f); fidgetEnd = Time.time + Mathf.Max(0.5f, st.length); }
                fidgetAt = 0f; if (st != null) return;
            }
            SetAnim(p, "stand1");
        }

        // ---- movimiento con CharacterController (como cnAvatarThirdPersonMove): altura 1.6, radio 0.3, centro (0,0.8,0), slopeLimit 50, skin 0.03 (prefab OwnUser) y stepOffset 0.4 (código) ----
        [Header("Colisión (original)")] public bool UseCharacterController = true;
        public float CcHeight = 1.6f, CcRadius = 0.3f, CcSlope = 50f, CcStep = 0.4f, CcSkin = 0.03f;
        CharacterController cc; bool ccGrounded; int ccLogs;
        CharacterController EnsureCC(Transform p)
        {
            if (cc != null && cc.gameObject == p.gameObject) return cc;
            cc = p.GetComponent<CharacterController>(); if (cc == null) cc = p.gameObject.AddComponent<CharacterController>();
            cc.height = CcHeight; cc.radius = CcRadius; cc.center = new Vector3(0f, CcHeight * 0.5f, 0f); cc.slopeLimit = CcSlope; cc.stepOffset = CcStep; cc.skinWidth = CcSkin; cc.minMoveDistance = 0f;
            Physics.IgnoreLayerCollision(p.gameObject.layer, 2, false);   // como el original: el CharacterController se apoya en el colisionador de agua (capa 2) y al tocarlo bWater = true
            Physics.IgnoreLayerCollision(p.gameObject.layer, 30, true);
            return cc;
        }
        static float TerrainTop(Vector3 q)
        {
            float best = float.NegativeInfinity;
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue; var o = t.transform.position; var sz = t.terrainData.size;
                if (q.x < o.x || q.z < o.z || q.x > o.x + sz.x || q.z > o.z + sz.z) continue;
                float y = t.SampleHeight(q) + o.y; if (y > best) best = y;
            }
            return best;
        }
        /// true si movió al avatar (modo CharacterController). Gravedad 10 con velocidad terminal -10; en el suelo vy = 0; el salto ya viene con vy = JumpVelocity.
        bool WalkCC(Transform p, Vector3 dir, Vector3 raw)
        {
            var c = EnsureCC(p); if (!c.enabled) c.enabled = true;
            Physics.SyncTransforms();   // teletransportes / ajustes de posición hechos por otros sistemas
            float dt = Time.deltaTime;
            if (airborne) { vy -= Gravity * dt; if (vy < -Gravity) vy = -Gravity; }
            else if (!ccGrounded) { vy -= Gravity * dt; if (vy < -Gravity) vy = -Gravity; }
            else vy = 0f;
            var mv = dir * DebugWalkSpeed * WalkScale * dt; mv.y = ccGrounded && !airborne ? -0.01f : vy * dt;
            var before = p.position;
            c.Move(mv);
            ccGrounded = (c.collisionFlags & CollisionFlags.Below) != 0;
            // GetTerrainCorrection del original: nunca por debajo del terreno visible
            float ty = TerrainTop(p.position);
            // The grounding probe moves down by 1 cm each frame. A 2 cm dead band
            // here made a visible terrain without matching collider height produce
            // a repeating sink/reset cycle, which the follow camera also inherited.
            if (ty > float.NegativeInfinity && p.position.y < ty) { var q = p.position; q.y = ty; p.position = q; Physics.SyncTransforms(); ccGrounded = true; if (vy < 0f) vy = 0f; }
            if (ccLogs < 4 && dir.sqrMagnitude > 0f && (p.position - before).magnitude < DebugWalkSpeed * dt * 0.2f) { ccLogs++; Debug.Log("[cc] bloqueado en " + p.position + " flags=" + c.collisionFlags); }
            // agua (original, cnAvatarThirdPersonMove): el avatar se apoya en el colisionador "Water" y al tocarlo bWater = true (misma velocidad y gravedad, sin salto);
            // si no toca suelo y venía del agua, un rayo desde +2 m (alcance 4) que dé en agua a < 2.5 m mantiene el nado ("Force set water").
            bool wetNow = false;
            if (EnableSwim && Vehicle <= 0 && WaterSurface.Count > 0)   // AvatarWater(bWater & iVehicle == 0): con vehiculo no se nada
            {
                int wm = 1 << WaterSurface.WaterLayer; RaycastHit wh;
                if (ccGrounded) wetNow = Physics.Raycast(p.position + Vector3.up * 0.5f, Vector3.down, out wh, 0.56f, wm, QueryTriggerInteraction.Collide);
                else if (swimming) wetNow = Physics.Raycast(p.position + Vector3.up * 2f, Vector3.down, out wh, 4f, wm, QueryTriggerInteraction.Collide) && wh.distance < 2.5f;
            }
            if (wetNow != swimming) SetSwimming(wetNow, p.position);
            if (wetNow) { airborne = false; if (vy < 0f) vy = 0f; return true; }
            if (airborne)
            {
                if (SendJump && SendMovement && Time.time >= nextJumpPkt) { nextJumpPkt = Time.time + 0.3f; int ang2 = (int)p.eulerAngles.y - 180; _ = Send(PlayerMotionPackets.Jump(Now(), p.position, new Vector3(dir.x * DebugWalkSpeed, vy, dir.z * DebugWalkSpeed), ang2, DirNum(raw.x, raw.z), RunSpeed)); }
                if (ccGrounded && vy <= 0f) { airborne = false; vy = 0f; landUntil = Time.time + 0.2f; PlayOnce("jumpend"); }
                else if (Time.time - jumpT > 0.25f) SetAnim(p, "jump");
            }
            else if (!ccGrounded && vy < -4f) { airborne = true; jumpT = Time.time - 1f; }   // cayendo de un borde: animación de caída y aterrizaje
            return true;
        }
        void ReapplyIdleOnce(Transform p) { }

        async Task Send(byte[] pkt) { if (!shardAlive || Shard_ == null || Shard_.Session == null) return; try { CombatAudit.Sent(pkt); await Shard_.Session.SendAsync(pkt); } catch (Exception e) { Debug.LogWarning("envío: " + e); } }

        void OnDestroy()
        {
            if (NpcUI.Sender == ownedSender) NpcUI.Sender = null;
            if (FindSelf == this) FindSelf = null;
            ChannelWarp.ShardSwitch -= OnShardSwitch;
            try { Shard_?.Session.Dispose(); Login?.Dispose(); } catch { }
        }
    }
}
