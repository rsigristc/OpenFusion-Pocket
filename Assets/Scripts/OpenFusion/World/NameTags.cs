// Nombres flotantes sobre la cabeza (PrintName.DrawAll del original): propio, otros jugadores, NPC y monstruos, segun las opciones de "FLOATING DISPLAY".
// Colores del original (namecolors): jugador 0.23/0.57/0.78, grupo 0.25/0.63/0.08, NPC 1/0.78/0, monstruo 0.89/0.17/0.17. Altura: 0.9*alto (jugador) / 0.8*alto (NPC) sobre los pies.
// Visibilidad (PrintName.PositionUpdate, cada 0,3 s): en pantalla, dentro de la distancia de corte +5 (playerCulls/npcCulls) y sin pared delante (rayo con mascara ~1792).
// El grupo no se distingue aun (todos los jugadores salen con el color de "otros"). SIN COMPILAR EN UNITY.
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    public sealed class NameTags : MonoBehaviour
    {
        WorldEntities ents; PacketDispatcher disp;
        readonly ConcurrentDictionary<int, string> names = new ConcurrentDictionary<int, string>();
        string selfName = "";
        BitmapFont font; bool fontTried;
        sealed class Cache { public float H, HAt = -10f; public bool Vis; public float VisAt = -10f; public NpcInfo Info; public bool InfoTried; }
        readonly Dictionary<int, Cache> cache = new Dictionary<int, Cache>();
        static readonly float[] PlayerCull = { 30f, 65f, 125f, 185f, 250f }, NpcCull = { 30f, 60f, 120f, 180f, 240f };
        static readonly Color CMine = new Color(0.23f, 0.57f, 0.78f), COther = new Color(0.23f, 0.57f, 0.78f), CGroup = new Color(0.25f, 0.63f, 0.08f), CNpc = new Color(1f, 0.78f, 0f), CMob = new Color(0.89f, 0.17f, 0.17f);
        const int HitMask = ~1792;
        float lastClean;

        public static NameTags Ensure(GameObject host) { var n = host.GetComponent<NameTags>(); if (n == null) n = host.AddComponent<NameTags>(); return n; }
        public void Init(WorldEntities e, PacketDispatcher d)
        {
            if (disp != null) { disp.PcEnter -= OnPcEnter; disp.SelfEnter -= OnSelfEnter; }
            ents = e; disp = d; if (disp != null) { disp.PcEnter += OnPcEnter; disp.SelfEnter += OnSelfEnter; }
        }
        void OnDestroy() { if (disp != null) { disp.PcEnter -= OnPcEnter; disp.SelfEnter -= OnSelfEnter; } }
        void OnPcEnter(PcAppearance a) { if (a == null) return; names[a.Id] = ((a.FirstName ?? "") + " " + (a.LastName ?? "")).Trim(); }
        void OnSelfEnter(EnterSucc e) { if (e.Self == null) return; selfName = ((e.Self.FirstName ?? "") + " " + (e.Self.LastName ?? "")).Trim(); }

        static float BoundsHeight(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return 0f; Bounds b = default; bool has = false;
            foreach (var r in rs) { if (r is ParticleSystemRenderer || r.gameObject.name == "blobshadow" || !r.enabled || !r.gameObject.activeInHierarchy) continue; if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            return has ? Mathf.Clamp(b.max.y - go.transform.position.y, 0.8f, 8f) : 0f;   // 0 = sin modelo visible: sin etiqueta (NPC marcador/invisible)
        }

        void OnGUI()
        {
            GUI.depth = 20;   // sin restaurar al salir: el depth de un script es el que tiene al terminar OnGUI (antes se reponia a 0 y las etiquetas quedaban sobre los menus)
            if (GameMenu.HudHidden || TradeOverlay.Active || ModalWindow.Overlayed) return; var e = Event.current; if (e == null || e.type != EventType.Repaint || ents == null) return;
            var o = GameOptions.Cur; if (!(o.MyName || o.OtherName || o.GroupName || o.NpcName || o.MonsterName)) return;
            if (GameMenu.HudHidden || NpcUI.Active || ents.Self == null) return;
            var cam = Camera.main; if (cam == null) return;
            if (!fontTried) { fontTried = true; font = BitmapFontLoader.Load("JEFFE___14__903"); }
            if (font == null) return;
            int vi = Mathf.Clamp(Mathf.RoundToInt(o.Visibility * 4f), 0, 4); float sc = GameOptions.UiScale; var om = GUI.matrix; GUI.depth = 20;
            try
            {
                if (o.MyName) Tag(cam, ents.Self, selfName, CMine, true, 1000f, sc, null, VehicleRide.NameDx(ents.SelfId));
                if (o.OtherName || o.GroupName)
                    foreach (var kv in ents.OtherPlayers) { if (kv.Value == null) continue; string n; if (!names.TryGetValue(kv.Key, out n)) continue; bool grp = GroupState.IsMember(kv.Key); if (grp ? !o.GroupName : !o.OtherName) continue; Tag(cam, kv.Value, n, grp ? CGroup : COther, true, PlayerCull[vi] + 5f, sc, null, VehicleRide.NameDx(kv.Key)); }   // PrintName: colorindex 2 (grupo) se dibuja con bGroupName, 1 con bOtherName
                if (o.NpcName || o.MonsterName)
                    foreach (var go in ents.NpcObjects)
                    {
                        if (go == null) continue; var c = GetCache(go); if (!c.InfoTried) { c.InfoTried = true; c.Info = go.GetComponent<NpcInfo>(); }
                        var info = c.Info; if (info == null || string.IsNullOrEmpty(info.Name)) continue;
                        int ty = info.Type; if (ty >= 100 || ty == 17 || ty == 7 || ty == 8 || ty == 9 || ty == 25) continue;   // NpcContainer.Add: bDrawName = false para estos tipos
                        bool mob = info.Team != 1; if (mob ? !o.MonsterName : !o.NpcName) continue;                              // SetName(.., team != 1 ? 4 (monstruo) : 3 (NPC))
                        Tag(cam, go, info.Name, mob ? CMob : CNpc, false, NpcCull[vi] + 5f, sc, info);
                    }
                if (Time.unscaledTime - lastClean > 30f) { lastClean = Time.unscaledTime; var dead = new List<int>(); foreach (var k in cache.Keys) { } cache.Clear(); }
            }
            finally { GUI.matrix = om; GUI.color = Color.white; }
        }

        Cache GetCache(GameObject go) { Cache c; int id = go.GetInstanceID(); if (!cache.TryGetValue(id, out c)) { c = new Cache(); cache[id] = c; } return c; }

        void Tag(Camera cam, GameObject go, string name, Color col, bool user, float maxDist, float sc, NpcInfo ni = null, float dx = 0f)
        {
            if (go == null || string.IsNullOrEmpty(name) || !go.activeInHierarchy) return;
            var c = GetCache(go); if (Time.unscaledTime - c.HAt > 2f) { c.HAt = Time.unscaledTime; c.H = BoundsHeight(go); }
            if (c.H <= 0f) return;
            float hh = !user && ni != null && ni.TableHeight > 0.1f ? ni.TableHeight : c.H;   // NPC: fHeight = NpcTable.m_iHeight * 0.01 (no la caja del modelo)
            Vector3 p = go.transform.position + Vector3.up * (hh * (user ? 0.9f : 0.8f) + dx);   // PrintName.dx: +0.35 con tabla, +0.3 con scooter
            var sp = cam.WorldToScreenPoint(p); if (sp.z <= 0.1f || sp.x < 0f || sp.y < 0f || sp.x > Screen.width || sp.y > Screen.height) return;
            if (go != ents.Self)
            {
                if (Time.unscaledTime - c.VisAt > 0.3f)
                {
                    c.VisAt = Time.unscaledTime; var dir = p - cam.transform.position; float mag = dir.magnitude;
                    c.Vis = mag <= maxDist && !Physics.Raycast(cam.transform.position, dir / Mathf.Max(mag, 0.01f), mag, HitMask, QueryTriggerInteraction.Ignore);
                }
                if (!c.Vis) return;
            }
            GUI.matrix = Matrix4x4.TRS(new Vector3(sp.x, Screen.height - sp.y, 0f), Quaternion.identity, new Vector3(sc, sc, 1f));
            var r = new Rect(-100f, -15f, 200f, 30f);
            font.Draw(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), name, TextAnchor.MiddleCenter, false, new Color(0f, 0f, 0f, 0.8f), null);
            font.Draw(r, name, TextAnchor.MiddleCenter, false, col, null);
        }
    }
}
