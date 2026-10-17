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
            File.WriteAllText("Logs/connection-regression-results.txt", "PASS: non-cooperative operation bounded and transport aborted; successful operation retained; cancellation closes transport; stalled login send times out and closes session; responsive TCP login connects; connection/failure UI remains dismissible.\n");
        }
    }
}
