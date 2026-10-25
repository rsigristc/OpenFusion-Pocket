// Cliente del login server: login -> personajes -> selección -> shard -> entrada al game server. Puro; sin Unity.
// Sigue el flujo de CnLoginMode / CnCharSelectionMode / CnCharCreationMode del cliente original. SIN VERIFICAR contra un servidor en vivo.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFusion.Net
{
    public sealed class LoginException : Exception
    {
        public readonly string Stage; public readonly int Code;
        public LoginException(string stage, int code, string msg) : base(msg) { Stage = stage; Code = code; }
        public LoginError? LoginCode => Stage == "login" && Enum.IsDefined(typeof(LoginError), Code) ? (LoginError)Code : (LoginError?)null;
    }

    public sealed class LoginOptions
    {
        public int VerA = 1, VerB = 0, VerC = 44;                 // iClientVerA/B/C del original (C entra en FEKey: debe coincidir con el servidor)
        public TimeSpan Timeout = TimeSpan.FromSeconds(15);
        public TimeSpan DuplicateWait = TimeSpan.FromMilliseconds(1500);   // pausa tras REQ_PC_EXIT_DUPLICATE (el original duerme 1500 ms)
        public int PortOffset = 0;                                // iServerPortSelected: se suma al puerto del shard
        public Action<string> CreationLog; // stages, packet names/sizes and codes only; never names or credentials
        public Action<string> Log;                                // registro de paquetes enviados/recibidos (null = silencio)
    }

    /// Una conexión al login server. Un único bucle de lectura contesta los live-check, cambia las claves al recibir LOGIN_SUCC
    /// (antes de leer el siguiente paquete, que ya viene con la clave nueva) y entrega el resto a quien lo espera.
    public sealed class LoginSession : IDisposable
    {
        public readonly PacketCrypto Crypto = new PacketCrypto();
        public readonly LoginOptions Options;
        public LoginSuccess Info { get; private set; }
        public ulong FEKey => Crypto.FEKey;
        public event Action<Exception> Disconnected;

        readonly Stream stream; readonly FramedStream framed; readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly object gate = new object();
        readonly Dictionary<long, NameSaved> reservedNames = new Dictionary<long, NameSaved>();
        readonly List<Waiter> waiters = new List<Waiter>(); readonly List<CharInfo> chars = new List<CharInfo>();
        readonly SemaphoreSlim charSignal = new SemaphoreSlim(0);
        int verC; Exception dead;

        sealed class Waiter { public uint[] Types; public TaskCompletionSource<KeyValuePair<uint, byte[]>> Tcs = new TaskCompletionSource<KeyValuePair<uint, byte[]>>(TaskCreationOptions.RunContinuationsAsynchronously); }

        public IReadOnlyList<CharInfo> Characters { get { lock (gate) return chars.ToArray(); } }

        public LoginSession(Stream s, LoginOptions options = null)
        {
            Options = options ?? new LoginOptions(); stream = s; framed = new FramedStream(s, Crypto); verC = Options.VerC;
            var _ = Task.Run(ReadLoop);
        }

        public static async Task<LoginSession> ConnectAsync(string host, int port, LoginOptions options = null, CancellationToken ct = default)
        {
            var c = new TcpClient { NoDelay = true };
            options = options ?? new LoginOptions();
            try { await NetworkDeadline.Wait(c.ConnectAsync(host, port), options.Timeout, c.Dispose, ct).ConfigureAwait(false); return new LoginSession(c.GetStream(), options); }
            catch (OperationCanceledException) { c.Dispose(); throw; }
            catch (Exception e) { c.Dispose(); throw new LoginException("connect", e is TimeoutException ? -2 : -1, "Could not connect to the login server. " + e.Message); }
        }

        // ---------- bucle de lectura ----------
        async Task ReadLoop()
        {
            Exception err = null;
            try { while (!cts.IsCancellationRequested) Handle(await framed.ReceiveAsync(cts.Token).ConfigureAwait(false)); }
            catch (EndOfStreamException) { err = new IOException("el login server cerró la conexión"); }
            catch (OperationCanceledException) { err = new IOException("sesión cerrada"); }
            catch (Exception e) { err = e; }
            Options.Log?.Invoke("✕ LS lectura terminada: " + (err?.Message ?? "?"));
            List<Waiter> w; lock (gate) { dead = err; w = new List<Waiter>(waiters); waiters.Clear(); }
            foreach (var x in w) x.Tcs.TrySetException(err);
            try { Disconnected?.Invoke(err); } catch { }
        }

        void Handle(byte[] packet)
        {
            if (packet.Length < 4) return;
            uint type = PacketIds.Unstamp(BitConverter.ToUInt32(packet, 0));
            Options.Log?.Invoke("← LS " + PacketIds.Name(type) + " (" + packet.Length + " B)");
            var body = new byte[packet.Length - 4]; Buffer.BlockCopy(packet, 4, body, 0, body.Length);
            switch (type)
            {
                case PacketIds.P_LS2CL_REQ_LIVE_CHECK:
                    var _ = SendAsync(ClientPackets.LiveCheckLs(0)).ContinueWith(t => { var e = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); return;
                case PacketIds.P_LS2CL_REP_LOGIN_SUCC:
                    try
                    {
                        var ok = ClientPackets.ParseLoginSucc(body);
                        // CnLoginMode: EKey = Creat_64Bit_Key(svrTime, iCharCount+1, iSlotNum+1); FEKey = Creat_64Bit_Key(DefaultKey, logVer, 1)
                        Crypto.EKey = PacketCrypto.Create64BitKey(ok.SvrTime, ok.CharCount + 1, ok.SlotNum + 1);
                        Crypto.FEKey = PacketCrypto.Create64BitKey(PacketCrypto.DefaultKey, verC, 1);
                        lock (gate) { Info = ok; chars.Clear(); }
                        while (charSignal.Wait(0)) { }
                    }
                    catch (ArgumentException) { }   // cuerpo corto: el waiter lo notará al parsear
                    break;
                case PacketIds.P_LS2CL_REP_CHAR_INFO:
                    try { var ci = ClientPackets.ParseCharInfo(body); lock (gate) chars.Add(ci); charSignal.Release(); } catch (ArgumentException) { }
                    return;
            }
            Waiter hit = null;
            lock (gate) { foreach (var w in waiters) if (Array.IndexOf(w.Types, type) >= 0) { hit = w; break; } if (hit != null) waiters.Remove(hit); }
            hit?.Tcs.TrySetResult(new KeyValuePair<uint, byte[]>(type, body));
        }

        // ---------- envío / petición ----------
        public async Task SendAsync(byte[] payload, CancellationToken ct = default)
        {
            lock (gate) if (dead != null) throw new IOException("login server desconectado", dead);
            if (Options.Log != null && payload != null && payload.Length >= 4) Options.Log("→ LS " + PacketIds.Name(PacketIds.Unstamp(BitConverter.ToUInt32(payload, 0))) + " (" + payload.Length + " B)");
            await sendLock.WaitAsync(ct).ConfigureAwait(false);
            try { await framed.SendAsync(payload, ct).ConfigureAwait(false); } finally { sendLock.Release(); }
        }

        /// Registra la espera ANTES de enviar (la respuesta puede llegar antes de que SendAsync vuelva).
        async Task<KeyValuePair<uint, byte[]>> Request(string stage, byte[] payload, uint[] types, CancellationToken ct)
        {
            var w = new Waiter { Types = types };
            lock (gate) { if (dead != null) throw new IOException("login server desconectado", dead); waiters.Add(w); }
            try
            {
                async Task<KeyValuePair<uint, byte[]>> SendAndWait()
                {
                    bool creation = stage == "check_name" || stage == "save_name" || stage == "char_create";
                    if (creation) Options.CreationLog?.Invoke("stage=" + stage + " sent");
                    await SendAsync(payload, ct).ConfigureAwait(false);
                    var reply = await w.Tcs.Task.ConfigureAwait(false);
                    if (creation) Options.CreationLog?.Invoke("stage=" + stage + " reply=" + PacketIds.Name(reply.Key) + " bytes=" + reply.Value.Length +
                        (reply.Key == PacketIds.P_LS2CL_REP_CHECK_CHAR_NAME_FAIL || reply.Key == PacketIds.P_LS2CL_REP_SAVE_CHAR_NAME_FAIL || reply.Key == PacketIds.P_LS2CL_REP_CHAR_CREATE_FAIL || reply.Key == PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL ? " code=" + ClientPackets.ParseErrorCode(reply.Value, stage) : ""));
                    return reply;
                }
                try { return await NetworkDeadline.Wait(SendAndWait(), Options.Timeout, Dispose, ct).ConfigureAwait(false); }
                catch (TimeoutException) { throw new LoginException(stage, -2, "The login server did not respond in time (" + stage + ")."); }
            }
            finally { lock (gate) waiters.Remove(w); w.Tcs.TrySetCanceled(); }
        }

        static LoginException Fail(string stage, byte[] body, string what) => new LoginException(stage, ClientPackets.ParseErrorCode(body, what), what + " código " + ClientPackets.ParseErrorCode(body, what));

        // ---------- flujo ----------
        public Task<LoginSuccess> LoginAsync(string id, string password, CancellationToken ct = default) { verC = Options.VerC; return DoLogin(ClientPackets.LoginPassword(id, password, Options.VerA, Options.VerB, Options.VerC), ct); }
        /// Login con cookie web (tokenLogin del original). El mapeo TEGid = usuario / authid = cookie del servidor es una suposición.
        public Task<LoginSuccess> LoginWithCookieAsync(string tegId, string authId, CancellationToken ct = default) { verC = Options.VerC; return DoLogin(ClientPackets.LoginCookie(tegId, authId, Options.VerA, Options.VerB, Options.VerC), ct); }

        async Task<LoginSuccess> DoLogin(byte[] req, CancellationToken ct)
        {
            var r = await Request("login", req, new[] { PacketIds.P_LS2CL_REP_LOGIN_SUCC, PacketIds.P_LS2CL_REP_LOGIN_FAIL }, ct).ConfigureAwait(false);
            if (r.Key == PacketIds.P_LS2CL_REP_LOGIN_FAIL) throw Fail("login", r.Value, "login rechazado");
            var ok = ClientPackets.ParseLoginSucc(r.Value);
            // el servidor envía iCharCount x REP_CHAR_INFO a continuación, sin que se pidan
            using (var tc = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                tc.CancelAfter(Options.Timeout);
                try { for (int i = 0; i < ok.CharCount; i++) await charSignal.WaitAsync(tc.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { ct.ThrowIfCancellationRequested(); throw new LoginException("login", -2, "faltan personajes tras el login (" + Characters.Count + "/" + ok.CharCount + ")"); }
            }
            return ok;
        }

        /// Código 3 (AlreadyLoggedIn): el original manda REQ_PC_EXIT_DUPLICATE sólo con szID, espera 1,5 s y vuelve a iniciar sesión.
        public async Task ExitDuplicateAsync(string id, CancellationToken ct = default)
        { await SendAsync(ClientPackets.ExitDuplicate(id, ""), ct).ConfigureAwait(false); await Task.Delay(Options.DuplicateWait, ct).ConfigureAwait(false); }

        /// CHAR_SELECT y luego SHARD_SELECT (el original lo encadena solo al recibir CHAR_SELECT_SUCC). shard 0 = el que elija el servidor.
        public async Task<ShardTicket> SelectCharacterAsync(long uid, int shard = 0, CancellationToken ct = default)
        {
            // Servidores OpenFusion/Retrobution (verificado en vivo): REQ_CHAR_SELECT se contesta DIRECTAMENTE con REP_SHARD_SELECT_SUCC
            // (sin CHAR_SELECT_SUCC). El cliente original, en cambio, recibe CHAR_SELECT_SUCC y luego pide el shard: se aceptan los dos flujos.
            var r = await Request("char_select", ClientPackets.CharSelect(uid),
                new[] { PacketIds.P_LS2CL_REP_CHAR_SELECT_SUCC, PacketIds.P_LS2CL_REP_CHAR_SELECT_FAIL, PacketIds.P_LS2CL_REP_SHARD_SELECT_SUCC, PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL }, ct).ConfigureAwait(false);
            if (r.Key == PacketIds.P_LS2CL_REP_CHAR_SELECT_FAIL) throw Fail("char_select", r.Value, "selección de personaje rechazada");
            if (r.Key == PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL) throw Fail("shard_select", r.Value, "shard rechazado");
            if (r.Key == PacketIds.P_LS2CL_REP_CHAR_SELECT_SUCC)
            {
                r = await Request("shard_select", ClientPackets.ShardSelect(shard), new[] { PacketIds.P_LS2CL_REP_SHARD_SELECT_SUCC, PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL }, ct).ConfigureAwait(false);
                if (r.Key == PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL) throw Fail("shard_select", r.Value, "shard rechazado");
            }
            var t = ClientPackets.ParseShardSelectSucc(r.Value); t.Port += Options.PortOffset; return t;
        }

        // ---------- creación de personaje ----------
        /// Comprueba un nombre elegido con la "rueda de nombres" (códigos fn/ln/mn) o en texto libre (códigos 0 + cadenas). Devuelve [nombre, apellido] aceptados.
        public async Task<string[]> CheckNameAsync(int fnCode, int lnCode, int mnCode, string first, string last, CancellationToken ct = default)
        {
            var r = await Request("check_name", ClientPackets.CheckCharName(fnCode, lnCode, mnCode, first, last), new[] { PacketIds.P_LS2CL_REP_CHECK_CHAR_NAME_SUCC, PacketIds.P_LS2CL_REP_CHECK_CHAR_NAME_FAIL }, ct).ConfigureAwait(false);
            if (r.Key == PacketIds.P_LS2CL_REP_CHECK_CHAR_NAME_FAIL) throw Fail("check_name", r.Value, "nombre rechazado");
            return ClientPackets.ParseCheckNameSucc(r.Value);
        }
        public async Task<NameSaved> SaveNameAsync(int slot, int gender, int fnCode, int lnCode, int mnCode, string first, string last, CancellationToken ct = default)
        {
            var r = await Request("save_name", ClientPackets.SaveCharName(slot, gender, fnCode, lnCode, mnCode, first, last), new[] { PacketIds.P_LS2CL_REP_SAVE_CHAR_NAME_SUCC, PacketIds.P_LS2CL_REP_SAVE_CHAR_NAME_FAIL, PacketIds.P_LS2CL_REP_CHECK_CHAR_NAME_FAIL, PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL }, ct).ConfigureAwait(false);
            if (r.Key != PacketIds.P_LS2CL_REP_SAVE_CHAR_NAME_SUCC) throw Fail(r.Key == PacketIds.P_LS2CL_REP_CHECK_CHAR_NAME_FAIL ? "check_name" : r.Key == PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL ? "save_slot" : "save_name", r.Value, "no se pudo reservar el nombre");
            var saved = ClientPackets.ParseSaveNameSucc(r.Value);
            lock (gate) reservedNames[saved.Uid] = saved;
            return saved;
        }
        public NameSaved ReservedName(int slot)
        {
            lock (gate) {
                foreach (var entry in reservedNames.Values) if (entry.Slot == slot) return entry;
                var pending = chars.Find(c => c.Slot == slot && c.NeedsCustomization);
                if (pending == null) return null;
                var a = pending.Appearance;
                var name = new NameSaved { Uid = a.Uid, Slot = pending.Slot, Gender = a.Style.Gender, FirstName = a.FirstName, LastName = a.LastName };
                reservedNames[name.Uid] = name;
                return name;
            }
        }
        public async Task<CharInfo> CreateCharacterAsync(long uid, World.PlayerStyle st, int shirtId, int pantsId, int shoesId, int shirtIdx, int pantsIdx, int shoesIdx, int faceIdx = -1, int hairIdx = -1, CancellationToken ct = default)
        {
            NameSaved reserved;
            lock (gate) reservedNames.TryGetValue(uid, out reserved);
            var r = await Request("char_create", ClientPackets.CharCreate(uid, st, shirtId, pantsId, shoesId, shirtIdx, pantsIdx, shoesIdx, 0, faceIdx, hairIdx, reserved), new[] { PacketIds.P_LS2CL_REP_CHAR_CREATE_SUCC, PacketIds.P_LS2CL_REP_CHAR_CREATE_FAIL, PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL }, ct).ConfigureAwait(false);
            if (r.Key != PacketIds.P_LS2CL_REP_CHAR_CREATE_SUCC) throw Fail("char_create", r.Value, "creación rechazada");
            var ci = ClientPackets.ParseCharCreateSucc(r.Value);
            if (ci.Appearance == null || ci.Appearance.Uid != uid) throw new LoginException("char_create", -1, "The server returned a different character. Reconnect before trying again.");
            lock (gate) {
                if (reservedNames.TryGetValue(uid, out var saved)) { ci.Slot = saved.Slot; reservedNames.Remove(uid); }
                int existing = chars.FindIndex(c => c.Appearance != null && c.Appearance.Uid == uid);
                if (existing >= 0) { if (ci.Slot < 1) ci.Slot = chars[existing].Slot; chars[existing] = ci; }
                else chars.Add(ci);
            }
            return ci;
        }
        /// Borra un personaje (REQ_CHAR_DELETE{iPC_UID} -> CHAR_DELETE_SUCC{iSlotNum}). Devuelve el slot vaciado.
        public async Task<int> DeleteCharacterAsync(long uid, CancellationToken ct = default)
        {
            var r = await Request("char_delete", ClientPackets.CharDelete(uid), new[] { PacketIds.P_LS2CL_REP_CHAR_DELETE_SUCC, PacketIds.P_LS2CL_REP_CHAR_DELETE_FAIL }, ct).ConfigureAwait(false);
            if (r.Key == PacketIds.P_LS2CL_REP_CHAR_DELETE_FAIL) throw Fail("char_delete", r.Value, "borrado rechazado");
            int slot = r.Value != null && r.Value.Length > 0 ? (sbyte)r.Value[0] : -1;
            lock (gate) chars.RemoveAll(c => c.Appearance != null && c.Appearance.Uid == uid);
            return slot;
        }
        /// Guarda la bandera del tutorial (SAVE_CHAR_TUTOR); el original la envía al saltarse el tutorial.
        public Task SaveTutorFlagAsync(long uid, int flag, CancellationToken ct = default) { return NetworkDeadline.Wait(SendAsync(ClientPackets.SaveCharTutor(uid, flag), ct), Options.Timeout, Dispose, ct); }

        // ---------- game server ----------
        /// Abre la conexión al shard con la clave FEKey del login, envía REQ_PC_ENTER y espera ENTER_SUCC/ENTER_FAIL. La sesión queda leyendo en segundo plano.
        /// attach se llama ANTES de empezar a leer: ahí se enganchan los manejadores (PC_AROUND/NPC_AROUND llegan pegados a ENTER_SUCC y no deben perderse).
        public async Task<EnteredShard> EnterShardAsync(ShardTicket ticket, string username, Action<GameSession> attach = null, CancellationToken ct = default)
        {
            var crypto = new PacketCrypto { FEKey = Crypto.FEKey };
            var sc = SynchronizationContext.Current;   // hilo del llamador (Unity: principal); los manejadores del mundo deben ejecutarse ahí
            if (sc != null && attach != null)
            {
                var inner = attach;
                attach = g => { var done = new ManualResetEventSlim(); Exception err = null; sc.Post(_ => { try { inner(g); } catch (Exception e) { err = e; } finally { done.Set(); } }, null); done.Wait(); if (err != null) throw err; };
            }
            GameSession gs;
            try { gs = await GameSession.ConnectAsync(ticket.Host, ticket.Port, crypto, ct, Options.Timeout).ConfigureAwait(false); }
            catch (Exception e) when (!(e is OperationCanceledException)) { throw new LoginException("shard_connect", -1, "no se pudo conectar al shard " + ticket.Host + ":" + ticket.Port + " (" + e.Message + ")"); }
            gs.Context = sc; gs.Log = Options.Log;
            return await EnterOn(gs, ticket, username, attach, ct).ConfigureAwait(false);
        }

        /// Igual que EnterShardAsync pero sobre una sesión ya creada (pruebas / transportes propios).
        public async Task<EnteredShard> EnterOn(GameSession gs, ShardTicket ticket, string username, Action<GameSession> attach = null, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<EnterSucc>(TaskCreationOptions.RunContinuationsAsynchronously);
            gs.Dispatcher.SelfEnter += e => tcs.TrySetResult(e);
            gs.Dispatcher.EnterFail += c => tcs.TrySetException(new LoginException("enter", c, "el shard rechazó la entrada, código " + c));
            gs.Disconnected += e => tcs.TrySetException(new LoginException("enter", -3, "el shard cerró la conexión antes de entrar" + (e != null ? " (" + e.Message + ")" : "")));
            try { attach?.Invoke(gs); } catch { gs.Dispose(); throw; }
            var run = gs.RunAsync(ct);
            try
            {
                await gs.SendAsync(ClientPackets.PcEnter(username, ticket.EnterSerialKey), ct).ConfigureAwait(false);
                using (var tc = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    tc.CancelAfter(Options.Timeout);
                    using (tc.Token.Register(() => tcs.TrySetCanceled()))
                    {
                        try { var e = await tcs.Task.ConfigureAwait(false); return new EnteredShard { Session = gs, Enter = e, Run = run }; }
                        catch (OperationCanceledException) { ct.ThrowIfCancellationRequested(); throw new LoginException("enter", -2, "tiempo de espera agotado esperando REP_PC_ENTER_SUCC"); }
                    }
                }
            }
            catch { gs.Dispose(); throw; }
        }

        public void Dispose() { cts.Cancel(); try { stream.Dispose(); } catch { } }
    }

    public sealed class EnteredShard { public GameSession Session; public EnterSucc Enter; public Task Run; }
}
