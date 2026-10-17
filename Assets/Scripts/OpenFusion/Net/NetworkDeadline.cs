using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFusion.Net
{
    public static class NetworkDeadline
    {
        // Some Unity/Android socket operations do not honour CancellationToken.
        // Close the transport on expiry and stop waiting even if its task hangs.
        public static async Task Wait(Task operation, TimeSpan timeout, Action abort, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            using (var timer = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var delay = Task.Delay(timeout, timer.Token);
                if (await Task.WhenAny(operation, delay).ConfigureAwait(false) == operation)
                { timer.Cancel(); await operation.ConfigureAwait(false); return; }
                try { abort?.Invoke(); } catch { }
                _ = operation.ContinueWith(t => { var observed = t.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                ct.ThrowIfCancellationRequested();
                throw new TimeoutException("The server did not respond within " + timeout.TotalSeconds.ToString("0") + " seconds.");
            }
        }
        public static async Task<T> Wait<T>(Task<T> operation, TimeSpan timeout, Action abort, CancellationToken ct = default)
        { await Wait((Task)operation, timeout, abort, ct).ConfigureAwait(false); return await operation.ConfigureAwait(false); }
    }
}
