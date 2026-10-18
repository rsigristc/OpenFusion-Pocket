using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;

namespace OpenFusion.EditorTools
{
    public static class ConnectionRegressionChecks
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        sealed class PausedMainContext : SynchronizationContext
        {
            public int Queued;
            public override void Post(SendOrPostCallback callback, object state) { Interlocked.Increment(ref Queued); }
        }
        sealed class StalledStream : Stream
        {
            readonly TaskCompletionSource<int> read = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            readonly TaskCompletionSource<bool> write = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool Closed;
            public override Task<int> ReadAsync(byte[] b, int o, int n, CancellationToken ct) => read.Task;
            public override Task WriteAsync(byte[] b, int o, int n, CancellationToken ct) => write.Task;
            protected override void Dispose(bool disposing) { Closed = true; read.TrySetResult(0); write.TrySetException(new ObjectDisposedException("test stream")); base.Dispose(disposing); }
            public override bool CanRead => true; public override bool CanWrite => true; public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException(); public override long Position { get => 0; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] b, int o, int n) => throw new NotSupportedException();
            public override void Write(byte[] b, int o, int n) => throw new NotSupportedException();
            public override long Seek(long n, SeekOrigin s) => throw new NotSupportedException(); public override void SetLength(long n) => throw new NotSupportedException();
        }
        public static async Task Run()
        {
            bool aborted = false; var pending = new TaskCompletionSource<bool>(); var clock = Stopwatch.StartNew();
            try { await NetworkDeadline.Wait(pending.Task, TimeSpan.FromMilliseconds(80), () => aborted = true); throw new Exception("Stalled operation did not time out"); }
            catch (TimeoutException) { Check(aborted && clock.Elapsed < TimeSpan.FromSeconds(3), "Timeout did not abort transport promptly"); }
            pending.TrySetResult(true);
            aborted = false;
            Check(await NetworkDeadline.Wait(Task.FromResult(7), TimeSpan.FromSeconds(1), () => aborted = true) == 7 && !aborted, "Successful operation was aborted");
            using (var cancellation = new CancellationTokenSource())
            {
                pending = new TaskCompletionSource<bool>(); cancellation.CancelAfter(50);
                try { await NetworkDeadline.Wait(pending.Task, TimeSpan.FromSeconds(5), () => aborted = true, cancellation.Token); throw new Exception("Cancellation ignored"); }
                catch (OperationCanceledException) { Check(aborted, "Cancellation leaked transport"); }
                pending.TrySetResult(true);
            }
            using (var stream = new StalledStream()) using (var login = new LoginSession(stream, new LoginOptions { Timeout = TimeSpan.FromMilliseconds(80) }))
            {
                try { await login.LoginAsync("regression", "test"); throw new Exception("Stalled send escaped login timeout"); }
                catch (LoginException e) { Check(e.Code == -2 && stream.Closed, "Login send timeout did not close session"); }
            }
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            try
            {
                var accepted = listener.AcceptTcpClientAsync();
                using (var login = await LoginSession.ConnectAsync("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port))
                using (var peer = await accepted) { Check(peer.Connected, "Responsive local login connection failed"); }
            }
            finally { listener.Stop(); }
            // Death/menu/fold rendering must never be required to answer server liveness probes.
            listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            try {
                var accepted = listener.AcceptTcpClientAsync();
                using (var client = await GameSession.ConnectAsync("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, new PacketCrypto { FEKey = PacketCrypto.DefaultKey }))
                using (var peer = await accepted)
                using (var limit = new CancellationTokenSource(TimeSpan.FromSeconds(3))) {
                    var context = new PausedMainContext(); client.Context = context;
                    var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    client.Disconnected += _ => finished.TrySetResult(true);
                    var run = client.RunAsync(limit.Token);
                    var server = new FramedStream(peer.GetStream(), new PacketCrypto { FEKey = PacketCrypto.DefaultKey });
                    for (int i = 1; i <= 3; i++) {
                        var check = ClientPackets.New(PacketIds.P_FE2CL_REQ_LIVE_CHECK, 4); ClientPackets.PutInt(check, 4, i);
                        await server.SendAsync(check, limit.Token);
                        var reply = await NetworkDeadline.Wait(server.ReceiveAsync(limit.Token), TimeSpan.FromSeconds(2), client.Dispose, limit.Token);
                        Check(PacketIds.Unstamp(BitConverter.ToUInt32(reply, 0)) == PacketIds.P_CL2FE_REP_LIVE_CHECK && BitConverter.ToInt32(reply, 4) == i,
                            "Main-thread pause prevented a matching liveness reply");
                    }
                    Check(context.Queued > 0 && !finished.Task.IsCompleted, "A paused UI context must not end the server session");
                    peer.Close(); await NetworkDeadline.Wait(finished.Task, TimeSpan.FromSeconds(2), client.Dispose);
                    await run;
                    Check(client.EndReason.Contains("Server closed") && client.RecentTraffic.Contains("LIVE_CHECK"), "Real EOF must retain a useful transport reason and bounded gameplay history");
                }
            } finally { listener.Stop(); }
            var go = new GameObject("ConnectionFailureRegression");
            try
            {
                var screen = go.AddComponent<LoadingScreen>(); screen.ShowStage("CONNECTING TO LOGIN SERVER");
                Check(screen.Visible, "Connection stage invisible");
                screen.ShowFailure("The login server did not respond.", () => { });
                Check(screen.Visible && !screen.Mini, "Failure screen invisible"); screen.Hide();
                Check(!screen.Visible, "Failure screen cannot be dismissed");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            File.WriteAllText("Logs/connection-regression-results.txt", "PASS: non-cooperative operation bounded and transport aborted; successful operation retained; cancellation closes transport; stalled login send times out and closes session; responsive TCP login connects; heartbeat replies continue while main-thread callbacks are paused; actual EOF records its reason and packet context; connection/failure UI remains dismissible.\n");
        }
    }
}
