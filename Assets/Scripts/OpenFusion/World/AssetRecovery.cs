using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenFusion.Assets;
using UnityEngine;
namespace OpenFusion.World
{
    // Load resource-only bundles without building or entering their map scenes.
    // Routes are generated from real container indexes, never inferred from NPC names.
    public sealed class AssetRecovery : MonoBehaviour
    {
        Dictionary<string,string> routes;
        readonly Queue<string> pending = new Queue<string>();
        readonly HashSet<string> requested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> recovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,int> attempts = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,float> retryAt = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
        BundleCache cache; BundleManager manager; DongLoader loader; ModelFactory models;
        CancellationTokenSource cancellation; int generation; bool running;
        public void Configure(BundleCache cache, BundleCatalog catalog, BundleManager manager, DongLoader loader, ModelFactory models)
        {
            generation++; cancellation?.Cancel(); cancellation?.Dispose(); cancellation = new CancellationTokenSource();
            if (this.models != null) this.models.MissingAsset = null;
            this.cache = cache; this.manager = manager; this.loader = loader; this.models = models;
            pending.Clear(); requested.Clear(); available.Clear(); recovered.Clear(); attempts.Clear(); retryAt.Clear(); running = false;
            foreach (var item in catalog.All) available.Add(item.Info.name);
            if (routes == null)
            {
                var data = Resources.Load<TextAsset>("OpenFusion/NpcAssetRoutes");
                routes = data == null ? new Dictionary<string,string>() : Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,string>>(data.text);
            }
            models.MissingAsset = Request;
        }
        void Request(string path)
        {
            if (path == null || routes == null || !routes.TryGetValue(path.ToLowerInvariant(), out string bundle) || !available.Contains(bundle) || requested.Contains(bundle) || requested.Count >= 16) return;
            if (attempts.TryGetValue(bundle,out int count) && (count >= 3 || retryAt.TryGetValue(bundle,out float after) && Time.unscaledTime < after)) return;
            attempts[bundle] = count + 1;
            OpenFusion.Screens.PocketDiagnostics.AssetFailure(bundle);
            requested.Add(bundle); pending.Enqueue(bundle);
        }
        async void Update()
        {
            if (running || pending.Count == 0 || loader == null || cache == null) return;
            running = true; string bundle = pending.Dequeue(); int stamp = generation; var target = loader; var source = manager; var token = cancellation.Token;
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(30000);
                    await cache.EnsureAsync(new[]{bundle}, ct:timeout.Token);
                    var files = await Task.Run(() => source.Load(bundle), timeout.Token);
                    if (this == null || stamp != generation || target == null || files == null) return;
                    target.ImportResourceBundle(bundle, files); recovered.Add(bundle);
                    Debug.Log("[asset recovery] imported resource bundle " + bundle);
                }
            }
            catch (Exception error) { if (stamp == generation) { requested.Remove(bundle); retryAt[bundle]=Time.unscaledTime+30; OpenFusion.Screens.PocketDiagnostics.AssetFailure(bundle,true); OpenFusion.Screens.PocketDiagnostics.Caught(error); } }
            finally { if (stamp == generation) running = false; }
        }
        void OnDestroy() { generation++; cancellation?.Cancel(); cancellation?.Dispose(); if(models!=null)models.MissingAsset=null; }
    }
}
