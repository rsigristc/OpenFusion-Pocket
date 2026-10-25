// Raíz de composición: servidor -> versión -> main.unity3d -> descarga por fases -> DongLoader. SIN COMPILAR NI PROBAR (UnityEngine + servidor real).
// Usa async/await del hilo principal de Unity (UnitySynchronizationContext).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using OpenFusion.Api;
using OpenFusion.Assets;
using OpenFusion.Unity25;
using OpenFusion.World;

namespace OpenFusion
{
    public sealed class WorldBootstrap : MonoBehaviour
    {
        [Tooltip("URL de la API del servidor (p. ej. https://api.ffretrobution.net)")] public string ServerUrl = "https://api.ffretrobution.net";
        [Tooltip("Identificador local del servidor (carpeta de caché)")] public string ServerId = "retrobution";
        [Tooltip("uuid de la versión; vacío = la primera de game_versions")] public string VersionUuid = "";
        public Vector3 StartPosition = new Vector3(6912f, 0f, 1792f);   // dong 13,3 (posición inicial del cliente original)
        [Tooltip("GameClient llama a Begin() tras el login")] public bool AutoStart = false;
        public DongLoader Loader; public Transform Player;
        public event Action<string, float> Status;               // texto + progreso 0..1 para la UI de carga
        public BundleManager Bundles { get; private set; }
        public ModelFactory Models { get; private set; }
        public PlayerAvatarFactory Avatars { get; private set; }
        /// Las tablas (xdtdatas) llegan por la API del servidor; en cuanto estén cargadas se habilita la creación de avatares de jugador.
        public void SetTables(OpenFusion.Tables.Gen.XdtTableScript tables) { if (Models != null) Avatars = new PlayerAvatarFactory(tables, Models); else pendingTables = tables; }
        OpenFusion.Tables.Gen.XdtTableScript pendingTables;
        public WorldEntities Entities { get; private set; }
        bool mapLoading; int warpGeneration; string warpError; Vector3 warpTarget;
        public static bool Travelling => GameClient.FindSelf != null && GameClient.FindSelf.Bootstrap != null && GameClient.FindSelf.Bootstrap.mapLoading;
        /// Conecta el despachador de paquetes con el mundo: jugadores y NPC aparecen/desaparecen/se mueven según el servidor, y el jugador propio pasa a ser el objetivo del DongLoader.
        /// Debe llamarse cuando el arranque haya terminado (Models listo) y las tablas (xdtdatas, all_hnpc) estén cargadas.
        public void StartEntities(OpenFusion.Net.PacketDispatcher dispatcher, OpenFusion.Tables.Gen.XdtTableScript tables, OpenFusion.Tables.Gen.HNpcTableScript hnpcs)
        {
            SetTables(tables);
            Entities = gameObject.GetComponent<WorldEntities>(); if (Entities == null) Entities = gameObject.AddComponent<WorldEntities>();
            Entities.NpcTable = tables != null ? tables.m_pNpcTable : null;
            Entities.MissionTable = tables != null ? tables.m_pMissionTable : null; Entities.Instances = tables != null ? tables.m_pInstanceTable : null;
            Entities.Init(dispatcher, ModelCatalog.From(tables), Models, Avatars, hnpcs, transform);
            { var hud = gameObject.GetComponent<PlayerHud>(); if (hud == null) hud = gameObject.AddComponent<PlayerHud>(); hud.Init(Entities, dispatcher); hud.MaxHpProvider = lvl => { var gd = tables != null && tables.m_pAvatarTable != null ? tables.m_pAvatarTable.m_pAvatarGrowData : null; if (gd != null) foreach (var g in gd) if (g != null && g.m_iLevel == lvl) return g.m_iMaxHP; return OpenFusion.World.HudData.DefaultMaxHp(lvl); }; dispatcher.SelfEnterRaw += body => hud.ApplyEnterBody(body); }   // Status.iMaxHP = AvatarGrowElement(nivel).m_iMaxHP (UserContainer.SetData)
            if (gameObject.GetComponent<VisToggles>() == null) gameObject.AddComponent<VisToggles>();
            { var nw = gameObject.GetComponent<NanoWheelHud>(); if (nw == null) nw = gameObject.AddComponent<NanoWheelHud>(); nw.Init(gameObject.GetComponent<PlayerHud>(), dispatcher, tables); if (Models != null) { var nm = gameObject.GetComponent<NanoModelRt>(); if (nm == null) nm = gameObject.AddComponent<NanoModelRt>(); nm.Init(Models, ModelCatalog.From(tables)); nw.NanoModelProvider = nm.Get; } }
            NanoMessages.Ensure(gameObject).InitGuide(dispatcher, tables);
            { var rw = RewardHud.Ensure(gameObject); rw.Init(gameObject.GetComponent<PlayerHud>(), dispatcher, tables); var sh = gameObject.GetComponent<ShinyEggs>(); if (sh == null) sh = gameObject.AddComponent<ShinyEggs>(); sh.Init(Entities, dispatcher, tables, Models, ModelCatalog.From(tables), transform); var wp = WelcomePopup.Ensure(gameObject); wp.UrlProvider = () => MainFileUrl; wp.ServerUrlProvider = () => ServerUrl; wp.BackLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; Entities.SelfSpawned += (go, e) => wp.Show(); } { var cb = gameObject.GetComponent<CombatSystem>(); if (cb == null) cb = gameObject.AddComponent<CombatSystem>(); cb.Init(Entities, dispatcher, tables, Models, Avatars); } { var ns = gameObject.GetComponent<NanoSystem>(); if (ns == null) ns = gameObject.AddComponent<NanoSystem>(); ns.Init(Entities, dispatcher, tables, gameObject.GetComponent<PlayerHud>(), gameObject.GetComponent<NanoWheelHud>(), gameObject.GetComponent<CombatSystem>(), Models, ModelCatalog.From(tables)); } { var nc = gameObject.GetComponent<NanocomHud>(); if (nc == null) nc = gameObject.AddComponent<NanocomHud>(); nc.Init(gameObject.GetComponent<PlayerHud>(), Entities, dispatcher, tables); } { var gm = gameObject.GetComponent<GameMenu>(); if (gm == null) gm = gameObject.AddComponent<GameMenu>(); gm.Init(Entities, dispatcher, tables); } { var bfl = BroomFlight.Ensure(gameObject); bfl.Init(Entities, dispatcher); bfl.GetModels = () => Models; } { var vrd = VehicleRide.Ensure(gameObject); vrd.GetModels = () => Models; vrd.GetAvatars = () => Avatars; vrd.Init(Entities, dispatcher, tables, ModelCatalog.From(tables)); } NameTags.Ensure(gameObject).Init(Entities, dispatcher); FloatingPoints.Ensure(gameObject); GroupState.Ensure(gameObject).Init(Entities, dispatcher); { var nh = gameObject.GetComponent<NanocomHud>(); if (nh != null) ZoneBanner.Ensure(gameObject).Init(nh); } FirstUse.Ensure(gameObject).Init(dispatcher, tables, gameObject.GetComponent<PlayerHud>(), Entities); { var sw = SettingsWindow.Ensure(gameObject); sw.BackLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; } { var jw = JournalWindow.Ensure(gameObject); jw.Init(dispatcher, gameObject.GetComponent<NanocomHud>(), Entities, tables); jw.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; } { var iw = InventoryWindow.Ensure(gameObject); iw.Init(dispatcher, Entities, tables, gameObject.GetComponent<PlayerHud>(), gameObject.GetComponent<JournalWindow>()); iw.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; { var ap = AvatarPreview.Ensure(gameObject); ap.Factory = Avatars; iw.Preview = ap; iw.NanoModel = nid => { var nmr = gameObject.GetComponent<NanoModelRt>(); return nmr != null ? nmr.Get(nid) : null; }; iw.NpcModel = npn => { var nmr2 = gameObject.GetComponent<NanoModelRt>(); return nmr2 != null ? nmr2.GetNpc(npn) : null; }; iw.Wheel = gameObject.GetComponent<NanoWheelHud>(); TransportWindow.NpcPortrait = npp => { var nmr3 = gameObject.GetComponent<NanoModelRt>(); return nmr3 != null ? nmr3.GetNpcPortrait(npp) : null; }; } } { System.Func<string, UnityEngine.Texture> tl = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; var pl = gameObject.GetComponent<PlayerHud>(); var jwn = gameObject.GetComponent<JournalWindow>(); var bw = BankWindow.Ensure(gameObject); bw.Init(dispatcher, Entities, tables, pl, jwn); bw.TexLoader = tl; var vw = VendorWindow.Ensure(gameObject); vw.Init(dispatcher, Entities, tables, pl, jwn); vw.TexLoader = tl; var tw = TradeWindow.Ensure(gameObject); tw.Init(dispatcher, Entities, tables, pl, jwn); tw.TexLoader = tl; } { var pl2 = gameObject.GetComponent<PlayerHud>(); var bwn = BarberWindow.Ensure(gameObject); bwn.Init(dispatcher, Entities, tables, pl2); bwn.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; var trw = TransportWindow.Ensure(gameObject); trw.Init(dispatcher, Entities, tables, pl2); trw.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; var rsy = RaceSystem.Ensure(gameObject); rsy.Init(dispatcher, Entities, tables, pl2); rsy.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; var rlw = RulesWindow.Ensure(gameObject); rlw.Init(tables); rlw.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; var enw = EnchantWindow.Ensure(gameObject); enw.Init(dispatcher, Entities, tables, pl2); enw.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; var cbw = CombineWindow.Ensure(gameObject); cbw.Init(dispatcher, Entities, tables, pl2); cbw.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; var gcw = GuideChangerWindow.Ensure(gameObject); gcw.Init(dispatcher, Entities, tables, pl2); gcw.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; } { var ew = EmailWindow.Ensure(gameObject); ew.Init(dispatcher, Entities, tables, gameObject.GetComponent<NanocomHud>(), gameObject.GetComponent<PlayerHud>()); ew.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; } { var gw = GuideWindow.Ensure(gameObject); gw.Init(tables); gw.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; } { var wm = WorldMapWindow.Ensure(gameObject); wm.Init(dispatcher, gameObject.GetComponent<NanocomHud>(), Entities); wm.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; }
            { var chat = gameObject.GetComponent<ChatHud>(); if (chat == null) chat = gameObject.AddComponent<ChatHud>(); chat.Init(gameObject.GetComponent<PlayerHud>(), Entities, dispatcher); chat.GroupSizeProvider = () => GroupState.Size; chat.ChatBubble += (id, msg, emote, kind) => { if (kind == 2) return; var ents = Entities; var ui = NpcUI.Instance; if (ents == null || ui == null) return; ui.AddPlayerBubble(id, ents.TransformOfPlayer(id), msg); }; }
            { var gcl = gameObject.GetComponent<GameClient>(); if (gcl == null) gcl = FindObjectOfType<GameClient>(); SocialMenu.Ensure(gameObject).Init(dispatcher, Entities, tables); AnimEventSink.FaceTexture = texName => { if (Models == null || string.IsNullOrEmpty(texName)) return null; foreach (var ext in new[] { "", ".png", ".psd", ".dds", ".tga" }) { try { var tx = Models.Texture(("texture/" + texName + ext).ToLowerInvariant()); if (tx != null) return tx; } catch (System.Exception) { } } return null; }; var em = EmoteSystem.Ensure(gameObject); em.Init(Entities, dispatcher, tables, gameObject.GetComponent<NanoSystem>(), gcl); InvenMode.Ensure(gameObject).Init(Entities, dispatcher, gcl); PlayerTarget.Ensure(gameObject).Init(Entities, dispatcher, gcl); var mcp = MenuChatPanel.Ensure(gameObject); mcp.TexLoader = path => { try { return Models != null ? Models.Texture(path.ToLowerInvariant()) : null; } catch (System.Exception) { return null; } }; mcp.Init(gameObject.GetComponent<ChatHud>(), tables, gameObject.GetComponent<NanocomHud>(), em); }   // emotes + frases rapidas del chat (MenuChatScript)
            PlayerLife.Ensure(gameObject).Init(Entities, gameObject.GetComponent<PlayerHud>());
            WarpFlow.Ensure(gameObject).Init(dispatcher, tables, gameObject.GetComponent<PlayerHud>());   // espera/bloqueo de warp, SUCC (taros/item) y FAIL modal
            Entities.WeaponTable = tables.m_pWeaponItemTable;
            ZiplineRide.Ensure(gameObject).Init(Entities, dispatcher);   // tirolina (EpZiplineTrigger): propia y remotos
            { var bus = BusSystem.Ensure(gameObject); bus.GetModels = () => Models; bus.Init(Entities, dispatcher, tables); }   // autobuses del mundo (TRANSPORTATION_*, PC_MOVETRANSPORTATION, TRANSPORT_WARP)
            EpTriggers.Ensure(gameObject).Init(Entities, dispatcher);   // resto de EpTrigger del mapa: plataformas, jumppad, aros, lanzador, tobogan, cuerda, cinta, interruptor
            var areas = gameObject.GetComponent<EnvironmentAreas>(); if (areas == null) areas = gameObject.AddComponent<EnvironmentAreas>(); areas.Init(Loader, Entities);
            Entities.Teleported -= OnTeleport; Entities.Teleported += OnTeleport;
            Entities.SelfSpawned += (go, e) => { Player = go.transform; if (Loader != null) { Loader.Player = Player; Loader.Paused = false; } };
        }
        BundleStreamer streamer; BundleCache cache;

        async void Start()
        {
            if (!AutoStart) return;
            try { await Run(); } catch (Exception e) { Debug.LogError("WorldBootstrap: " + e); Status?.Invoke("Error: " + e.Message, 0f); }
        }

        /// Arranca la descarga/carga del mundo alrededor de StartPosition (si AutoStart es false). Los errores se registran y se relanzan.
        public async Task Begin(bool characterSelectionOnly = false)
        {
            try { await Run(characterSelectionOnly); } catch (Exception e) { Debug.LogError("WorldBootstrap: " + e); Status?.Invoke("Error: " + e.Message, 0f); throw; }
        }

        public string MainFileUrl;   // base de news.php (pop-up de novedades)

        async Task Run(bool characterSelectionOnly = false)
        {
            Status?.Invoke("Conectando al servidor…", 0f);
            var api = new ServerApi(ServerUrl);
            var uuid = VersionUuid; if (string.IsNullOrEmpty(uuid)) uuid = (await api.GetInfo()).GameVersions.First();
            var chain = await api.GetChain(uuid, Path.Combine(Application.persistentDataPath, "version_manifests", ServerId));
            // El propietario del bundle ya identifica su version en BundleCache.
            // No duplicar todos los bundles heredados bajo cada version seleccionada.
            var dir = Path.Combine(Application.persistentDataPath, "bundles", ServerId);
            var catalog = BundleCatalog.FromChain(chain);
            cache = await Task.Run(() => { var result = new BundleCache(dir, catalog); GameCacheInfo.Record(dir, ServerId, chain[0], catalog); return result; });
            cache.LoadLog = message => Debug.Log(message);
            bool mainCached = await Task.Run(() => cache.IsMainCached(chain[0]));
            Status?.Invoke(mainCached ? "Reading cached main.unity3d…" : "Downloading main.unity3d…", 0.02f);
            Debug.Log("[cache] main.unity3d: " + (mainCached ? "disk cache" : "download"));
            MainFileUrl = chain[0].main_file_url;
            var seasonal = GetComponent<SeasonalEvents>() ?? gameObject.AddComponent<SeasonalEvents>();
            _ = seasonal.ConfigureAsync(ServerUrl, MainFileUrl); // Optional metadata never delays character/map loading.
            var main = await cache.EnsureMainFileAsync(chain[0]);
            Status?.Invoke("Reading game data…", .025f);
            var gameData = await Task.Run(() => MainGameData.LoadCached(main, Path.Combine(dir, chain[0].uuid, "main-game-data.json"), chain[0].main_file_info?.hash));
            var manifest = gameData.Manifest;
            Debug.Log("[cache] main game data: " + (gameData.CacheHit ? "disk cache" : "extracted from main.unity3d") + "; projectiles=" + (gameData.Projectiles?.Length ?? 0));
            Bundles = new BundleManager(cache, manifest); streamer = new BundleStreamer(manifest, cache);
            Bundles.SetPosition(StartPosition.x, StartPosition.z);

            DongGrid.Cell(StartPosition.x, StartPosition.z, out int sx, out int sy);
            var zone = manifest.IsTutorial(sx, sy) ? ZoneType.Tutorial : manifest.IsFree(sx, sy) ? ZoneType.Free : ZoneType.Paid;
            var progress = new Progress<DownloadProgress>(p => Status?.Invoke($"Descargando recursos… {p.FilesDone}/{p.FilesTotal} ({p.BytesDone / 1048576} MB)", 0.05f + 0.85f * (p.BytesTotal == 0 ? 1f : (float)p.BytesDone / p.BytesTotal)));
            await streamer.EnsurePhaseAsync(characterSelectionOnly ? Phase.CharacterSelection : Phases.For(zone), progress);
            if (!characterSelectionOnly) await streamer.PrefetchAroundAsync(sx, sy, progress);

            Status?.Invoke("Cargando el mundo…", 0.95f);
            Loader.Player = Player;
            Loader.DongMissing += async (x, y) =>
            {
                try { var m = Models.GameData.Manifest; await streamer.EnsurePhaseAsync(Phases.For(m.IsTutorial(x, y) ? ZoneType.Tutorial : m.IsFree(x, y) ? ZoneType.Free : ZoneType.Paid)); await streamer.EnsureDongAsync(x, y); }
                catch (Exception e) { Debug.LogWarning("Descarga de mapa: " + e.Message); }
            };
            Loader.DongLoaded += async (x, y) =>
            {
                try { await streamer.PrefetchAroundAsync(x, y); }
                catch (Exception e) { Debug.LogWarning("Precarga de mapa: " + e.Message); }
            };
            Loader.Paused = characterSelectionOnly;
            Loader.Init(main, Bundles, StartPosition, manifest);
            if (characterSelectionOnly) await Loader.PrepareSharedAsync(streamer.FilesUpTo(Phase.CharacterSelection));
            Models = Loader.CreateModelFactory(Bundles);
            Models.GameData = gameData;
            if (pendingTables != null) Avatars = new PlayerAvatarFactory(pendingTables, Models);
            Status?.Invoke("Listo", 1f);
        }

        public async Task PrepareIntroductionAsync()
        {
            // The ship uses the selection/creation models and NPC textures; avoid
            // downloading the nine offline training maps just to show the cinematic.
            var names = new[] { "CharacterCreation.resourceFile", "NpcTexture.resourceFile", "Effects.resourceFile" };
            await cache.EnsureAsync(names);
            await Loader.PrepareSharedAsync(names);
        }
        public async Task PrepareTutorialAsync(OpenFusion.Net.PcAppearance appearance, OpenFusion.Tables.Gen.XdtTableScript tables)
        {
            await streamer.EnsurePhaseAsync(Phase.Tutorial);
            await Loader.PrepareSharedAsync(streamer.FilesUpTo(Phase.Tutorial));
            TutorialAssets.Validate(Models, tables, appearance, Avatars);
            Debug.Log("[training assets] original illustrations, models, weapon, Nano and core effects verified");
        }
        public async Task LoadWorldAsync(Vector3 position)
        {
            StartPosition = position;
            DongGrid.Cell(position.x, position.z, out int x, out int y);
            var manifest = Models.GameData.Manifest;
            var zone = manifest.IsTutorial(x, y) ? ZoneType.Tutorial : manifest.IsFree(x, y) ? ZoneType.Free : ZoneType.Paid;
            await streamer.EnsurePhaseAsync(Phases.For(zone));
            await streamer.EnsureDongAsync(x, y);
            Loader.SetPosition(position); Loader.PauseAfterDong(x, y); Loader.Paused = false;
            for (int n = 0; !Loader.IsDongLoaded(x, y); n++)
            {
                if (n >= 480) throw new TimeoutException("The initial map could not be loaded. Please try again.");
                await Task.Delay(250);
            }
        }

        void OnTeleport(int x, int y, int z) { PrepareWarp(PlayerAvatarFactory.ToClient(x, y, z)); }
        async void PrepareWarp(Vector3 position)
        {
            int generation = ++warpGeneration; warpTarget = position; mapLoading = true; warpError = null; Loader.Paused = true;
            try
            {
                DongGrid.Cell(position.x, position.z, out int x, out int y);
                var m = Models.GameData.Manifest;
                await streamer.EnsurePhaseAsync(Phases.For(m.IsTutorial(x, y) ? ZoneType.Tutorial : m.IsFree(x, y) ? ZoneType.Free : ZoneType.Paid));
                await streamer.EnsureDongAsync(x, y);
                if (this == null || generation != warpGeneration) return;
                Loader.SetPosition(position); Loader.Paused = false;
                for (int n = 0; !Loader.IsDongLoaded(x, y); n++)
                {
                    if (n >= 480) throw new TimeoutException("The destination map could not be loaded.");
                    await Task.Delay(250);
                    if (this == null || generation != warpGeneration) return;
                }
                mapLoading = false; Debug.Log("[world] warp ready " + x + "," + y);
            }
            catch (Exception e) { if (this != null && generation == warpGeneration) { warpError = e.Message; Debug.LogWarning("[world] warp: " + e.Message); } }
        }
        void OnDestroy() { warpGeneration++; if (Entities != null) Entities.Teleported -= OnTeleport; }
        void OnGUI()
        {
            if (!mapLoading) return;
            int depth = GUI.depth; GUI.depth = -200;
            var r = new Rect(Screen.width / 2f - 220, Screen.height / 2f - 55, 440, 110);
            GUI.Box(r, warpError == null ? "Loading destination map…" : warpError);
            if (warpError != null && GUI.Button(new Rect(r.x + 160, r.y + 65, 120, 32), "Retry")) PrepareWarp(warpTarget);
            GUI.depth = depth;
        }
        void Update() { if (Bundles != null && Player != null) Bundles.SetPosition(Player.position.x, Player.position.z); }
    }
}
