// EpParse: MonoBehaviour EpTrigger del mapa (Dictionary del typetree) -> EpInfo -> EpTriggers.Register. SIN COMPILAR NI PROBAR EN UNITY.
// Lo llama DongBuilder.ApplyScript con los scripts que tienen triggerType y objectId (salvo la tirolina, triggerType 2, que sigue en ZiplineRide).
// Campos del script segun el volcado de los mapas: triggerType, objectId, serverId, cneId, radius, startPosition y por tipo:
//   Platform: from, to, velocity, spinvelocity, moveType, targetElementId, waypointCount, head, tail | Jumppad: addPower | Launcher: minPower, maxPower, initRotate, maxRotate
//   Switch: targetElementId | Ring: fceType | Slope / Belt: speed, waypointCount, head, tail | Rope: speed, moveType, waypointCount, head, tail
// Waypoint (MonoBehaviour en un hijo del nodo): point (local al nodo), prev, next. La lista Points va de head a tail siguiendo next.
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    internal static class EpParse
    {
        static readonly Dictionary<(string, long), Dictionary<long, int>> orderCache = new Dictionary<(string, long), Dictionary<long, int>>();
        static int logged;

        static int GI(Dictionary<string, object> v, string k, int d) { object o; return v.TryGetValue(k, out o) && o != null ? D.I(o) : d; }
        static float GF(Dictionary<string, object> v, string k, float d) { object o; if (!v.TryGetValue(k, out o) || o == null) return d; return o is float f ? f : System.Convert.ToSingle(o); }
        static Vector3 GV(Dictionary<string, object> v, string k)
        {
            object o; if (v.TryGetValue(k, out o) && o is Dictionary<string, object>) { var a = D.V3(o); return new Vector3(a[0], a[1], a[2]); }
            return Vector3.zero;
        }

        public static void Handle(AssetStore store, string file, SceneNode25 n, Dictionary<string, object> v, GameObject go)
        {
            try
            {
                int ty = D.I(v["triggerType"]);
                if (ty != EpT.Slope && ty != EpT.Launcher && ty != EpT.Platform && ty != EpT.Jumppad && ty != EpT.Switch && ty != EpT.Ring && ty != EpT.Rope && ty != EpT.Belt) return;
                var info = new EpInfo { Type = ty, Go = go, ObjectId = GI(v, "objectId", -1), CneId = GI(v, "cneId", -1), Radius = GF(v, "radius", 1f) };
                // colisionador trigger del propio nodo (DongBuilder no construye los triggers)
                foreach (var pc in n.Components)
                {
                    if (pc.Key != "BoxCollider" && pc.Key != "SphereCollider" && pc.Key != "CapsuleCollider") continue;
                    var r = store.Resolve(file, 0, pc.Value); if (!r.Ok) continue;
                    var cv = store.Read(r); if (!(cv.ContainsKey("m_IsTrigger") && D.I(cv["m_IsTrigger"]) != 0)) continue;
                    info.Shape = pc.Key; info.Center = GV(cv, "m_Center");
                    if (pc.Key == "BoxCollider") info.Size = GV(cv, "m_Size"); else { info.R = GF(cv, "m_Radius", 0.5f); info.H = GF(cv, "m_Height", 0f); }
                    break;
                }
                switch (ty)
                {
                    case EpT.Platform:
                        info.From = GV(v, "from"); info.To = GV(v, "to"); info.Velocity = GF(v, "velocity", 0f); info.SpinVelocity = GF(v, "spinvelocity", 0f);
                        info.MoveType = GI(v, "moveType", 0); info.TargetElementId = GI(v, "targetElementId", 0); info.WaypointCount = GI(v, "waypointCount", 0);
                        if (info.MoveType >= 8 && info.MoveType <= 10) info.Points = Chain(store, file, v);
                        break;
                    case EpT.Jumppad: info.AddPower = GF(v, "addPower", 0f); break;
                    case EpT.Launcher:
                        info.MinPower = GF(v, "minPower", 0f); info.MaxPower = GF(v, "maxPower", 0f); info.InitRotate = GV(v, "initRotate"); info.MaxRotate = GV(v, "maxRotate");
                        break;
                    case EpT.Switch: info.TargetElementId = GI(v, "targetElementId", 0); break;
                    case EpT.Ring:
                        info.FceType = GI(v, "fceType", 0);
                        // Retrobution 3.0.2 changes this at runtime; map bundles still
                        // contain the legacy 0.38 collider. Preserve larger authored pods.
                        if (info.Shape == "SphereCollider") info.R = Mathf.Max(info.R, 0.48f);
                        break;
                    case EpT.Slope:
                    case EpT.Belt:
                        info.Speed = GF(v, "speed", 0f); info.WaypointCount = GI(v, "waypointCount", 0); info.Points = Chain(store, file, v);
                        break;
                    case EpT.Rope:
                        info.Speed = GF(v, "speed", 0f); info.RopeMoveType = GI(v, "moveType", 0); info.WaypointCount = GI(v, "waypointCount", 0); info.Points = Chain(store, file, v);
                        break;
                }
                info.Order = OrderOf(store, file, n);
                EpTriggers.Register(info);
            }
            catch (Exception e) { if (logged++ < 20) Debug.LogWarning("[ep] registro: " + e.Message); }
        }

        /// <summary>Waypoint.point de head a tail (siguiendo next).</summary>
        static Vector3[] Chain(AssetStore store, string file, Dictionary<string, object> v)
        {
            object ho; if (!v.TryGetValue("head", out ho) || ho == null) return null;
            var list = new List<Vector3>(); var seen = new HashSet<long>(); var p = PPtr25.From(ho);
            while (p.PathId != 0 && list.Count < 256)
            {
                if (!seen.Add(p.PathId)) break;
                var r = store.Resolve(file, p); if (!r.Ok) break;
                var w = store.Read(r); list.Add(GV(w, "point"));
                object nx; if (!w.TryGetValue("next", out nx) || nx == null) break;
                p = PPtr25.From(nx);
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        /// <summary>Posicion del nodo entre los hijos de su padre (orden de m_Children del Transform padre = orden de GetComponentsInChildren / hijos del original).</summary>
        static int OrderOf(AssetStore store, string file, SceneNode25 n)
        {
            var par = n.Parent; if (par == null) return 0;
            var key = (file, par.TransformId); Dictionary<long, int> map;
            if (!orderCache.TryGetValue(key, out map))
            {
                if (orderCache.Count > 4000) orderCache.Clear();
                map = new Dictionary<long, int>();
                try
                {
                    var r = store.Resolve(file, 0, par.TransformId);
                    if (r.Ok)
                    {
                        var tv = store.Read(r); object ch;
                        if (tv.TryGetValue("m_Children", out ch) && ch is object[] arr) for (int i = 0; i < arr.Length; i++) map[PPtr25.From(arr[i]).PathId] = i;
                    }
                }
                catch (Exception) { }
                if (map.Count == 0) for (int i = 0; i < par.Children.Count; i++) map[par.Children[i].TransformId] = i;
                orderCache[key] = map;
            }
            int o; return map.TryGetValue(n.TransformId, out o) ? o : par.Children.IndexOf(n);
        }
    }
}
