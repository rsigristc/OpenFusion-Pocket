// Bucle de recepción del game server: FramedStream -> PacketDispatcher, más la clave de sesión que fija REP_PC_ENTER_SUCC. Puro; sin Unity.
// NO incluye el login ni el handshake de shard (REQ_LOGIN, REQ_PC_ENTER...): asume una conexión ya establecida y con las claves iniciales puestas.
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFusion.Net
{
    public sealed class GameSession : IDisposable
    {
        public readonly PacketCrypto Crypto; public readonly PacketDispatcher Dispatcher = new PacketDispatcher();
        readonly FramedStream framed; readonly Stream stream; readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        public event Action<Exception> Disconnected;        // null = cierre limpio del servidor
        public long PacketsReceived;
        /// Registro opcional de paquetes (primeras apariciones de cada tipo, enviados y recibidos).
        public Action<string> Log;
        public string EndReason { get; private set; } = "Session active";
        readonly System.Collections.Generic.Queue<string> traffic = new System.Collections.Generic.Queue<string>();
        public string RecentTraffic { get { lock (traffic) return string.Join(" | ", traffic); } }
        readonly System.Collections.Generic.Dictionary<string, int> seen = new System.Collections.Generic.Dictionary<string, int>();
        void Trace(string dir, byte[] pkt, int skip)
        {
            TraceGameplay(dir, pkt);
            var l = Log; if (l == null || pkt == null || pkt.Length < 4) return;
            string name; try { name = PacketIds.Name(PacketIds.Unstamp(BitConverter.ToUInt32(pkt, 0))); } catch { name = "?"; }
            lock (seen) { seen.TryGetValue(dir + name, out int n); seen[dir + name] = ++n; if (n > 3 && n % 200 != 0 && (name.EndsWith("_NPC_MOVE") || name.EndsWith("_PC_MOVE") || name.EndsWith("_LIVE_CHECK"))) return; l("FE " + dir + " " + name + " (" + pkt.Length + " B) #" + n); }
        }
        void TraceGameplay(string direction, byte[] packet)
        {
            if (packet == null || packet.Length < 4) return;
            uint type = PacketIds.Unstamp(BitConverter.ToUInt32(packet, 0));
            bool motion = type == PacketIds.P_CL2FE_REQ_PC_MOVE || type == PacketIds.P_CL2FE_REQ_PC_STOP || type == PacketIds.P_CL2FE_REQ_PC_JUMP;
            bool regen = type == PacketIds.P_FE2CL_REP_PC_REGEN_SUCC;
            if (!motion && !regen && type != PacketIds.P_CL2FE_REQ_PC_REGEN && type != PacketIds.P_CL2FE_REQ_PC_FIRST_USE_FLAG_SET &&
                type != PacketIds.P_CL2FE_REQ_PC_LOADING_COMPLETE && type != PacketIds.P_FE2CL_REQ_LIVE_CHECK && type != PacketIds.P_CL2FE_REP_LIVE_CHECK) return;
            string item = Environment.TickCount + " " + direction + PacketIds.Name(type);
            if ((motion && packet.Length >= 24) || (regen && packet.Length >= 24))
                item += " xyz=" + BitConverter.ToInt32(packet, 12) + "," + BitConverter.ToInt32(packet, 16) + "," + BitConverter.ToInt32(packet, 20);
            if (regen && packet.Length >= 45) item += " hp=" + BitConverter.ToInt32(packet, 4) + " relocate=" + packet[44];
            lock (traffic) { traffic.Enqueue(item); while (traffic.Count > 24) traffic.Dequeue(); }
        }

        public GameSession(Stream stream, PacketCrypto crypto)
        {
            // el servidor comprueba que seguimos vivos: si no se contesta, expulsa la sesión
            // la respuesta a LIVE_CHECK se da directamente en el bucle de lectura (RunAsync), sin depender del hilo principal
            this.stream = stream; Crypto = crypto; framed = new FramedStream(stream, crypto);
            // GameFrame.cs: tras REP_PC_ENTER_SUCC la clave de envío (EKey) deja de ser la de por defecto: Creat_64Bit_Key(uiSvrTime, iID + 1, iFusionMatter + 1)
            Dispatcher.SelfEnter += e => { if (Crypto.EKey == PacketCrypto.DefaultKey) Crypto.EKey = PacketCrypto.Create64BitKey(e.SvrTime, e.Id + 1, e.Self.FusionMatter + 1); };
        }

        /// Si no es null, los paquetes se despachan por este contexto (en Unity: el hilo principal, donde se pueden crear GameObjects).
        public System.Threading.SynchronizationContext Context;

        public static async Task<GameSession> ConnectAsync(string host, int port, PacketCrypto crypto, CancellationToken ct = default, TimeSpan? timeout = null)
        {
            var c = new TcpClient { NoDelay = true };
            try { await NetworkDeadline.Wait(c.ConnectAsync(host, port), timeout ?? TimeSpan.FromSeconds(15), c.Dispose, ct).ConfigureAwait(false); return new GameSession(c.GetStream(), crypto); }
            catch { c.Dispose(); throw; }
        }

        /// Lee y despacha paquetes hasta que se cierre la conexión o se cancele. Nunca lanza: informa por Disconnected.
        public async Task RunAsync(CancellationToken ct = default)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var p = await framed.ReceiveAsync(ct).ConfigureAwait(false);
                    PacketsReceived++; Trace("←", p, 0);
                    if (p.Length >= 8 && PacketIds.Unstamp(BitConverter.ToUInt32(p, 0)) == PacketIds.P_FE2CL_REQ_LIVE_CHECK) { int lv = BitConverter.ToInt32(p, 4); _ = SendAsync(ClientPackets.LiveCheckFe(lv)).ContinueWith(t => { if (t.IsFaulted) Log?.Invoke("FE live-check fallo: " + t.Exception.GetBaseException().Message); }); }
                    var ctx = Context; if (ctx != null) ctx.Post(_ => Dispatcher.Dispatch(p), null); else Dispatcher.Dispatch(p);
                }
                EndReason = "Receive loop cancelled"; Disconnected?.Invoke(null);
            }
            catch (EndOfStreamException) { EndReason = "Server closed the stream after " + PacketsReceived + " packets"; Log?.Invoke("FE ✕ " + EndReason); Disconnected?.Invoke(null); }
            catch (OperationCanceledException) { EndReason = "Receive cancelled"; Disconnected?.Invoke(null); }
            catch (Exception e) { EndReason = e.GetType().Name + ": " + e.Message; Log?.Invoke("FE ✕ " + EndReason); Disconnected?.Invoke(e); }
        }

        /// payload = tipo (4 B) + cuerpo. Serializa los envíos (el cifrado y la escritura no son reentrantes).
        public async Task SendAsync(byte[] payload, CancellationToken ct = default)
        {
            await sendLock.WaitAsync(ct).ConfigureAwait(false);
            Trace("→", payload, 0); try { await framed.SendAsync(payload, ct).ConfigureAwait(false); } finally { sendLock.Release(); }
        }

        public void Dispose() { try { stream.Dispose(); } catch { } }
    }
}
