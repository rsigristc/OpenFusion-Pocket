using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    public sealed partial class WorldMapWindow
    {
        [Serializable] sealed class DualMarker
        { public float x, y, yaw; public string name, image; public int kind; }
        [Serializable] sealed class DualNano
        { public int id, energy, max; public string name; public bool active; public float cooldown; }
        [Serializable] sealed class DualFilter
        { public int id; public string image, label; public bool selected; }
        [Serializable] sealed class DualMap
        {
            public string image, location, notice;
            public float x, y, width, height;
            public int nanoBoost, attackBoost, taros, equipmentScroll;
            public string tarosImage;
            public bool local, filtersEnabled;
            public List<DualFilter> filters = new List<DualFilter>();
            public bool missions, shops, services, zoomIn, zoomOut;
            public List<DualMarker> markers = new List<DualMarker>();
            public List<DualNano> nanos = new List<DualNano>();
            public string tab;
            public bool station, dead, canSummon, canSummonCards, connected;
            public InventoryWindow.DualNanoCollection collection;
            public InventoryWindow.DualEquipmentState equipment;
            public JournalWindow.DualMissionState missionsPanel;
            public EmailWindow.DualMailState mailPanel;
            public int unreadMail;
        }
        bool dualInitialized;
        int dualPlayerZone = -1;
        readonly Dictionary<int, string> dualTextures = new Dictionary<int, string>();

        // Readback only once per texture identity, capped at 1024 px. No per-frame screenshots
        // or second 3D render. Android keeps only a bounded bitmap cache of these local files.
        string DualTexture(Texture texture, int limit = 1024)
        {
            if (texture == null) return "";
            int id = texture.GetInstanceID(); if (dualTextures.TryGetValue(id, out var path)) return path;
            var dir = Path.Combine(Application.temporaryCachePath, "dual-screen"); Directory.CreateDirectory(dir);
            path = Path.Combine(dir, id + ".png");
            float scale = Mathf.Min(1, (float)limit / Mathf.Max(texture.width, texture.height));
            int w = Mathf.Max(1, Mathf.RoundToInt(texture.width * scale)), h = Mathf.Max(1, Mathf.RoundToInt(texture.height * scale));
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Texture2D copy = null;
            try
            {
                Graphics.Blit(texture, rt); RenderTexture.active = rt;
                copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0); copy.Apply();
                File.WriteAllBytes(path, copy.EncodeToPNG()); dualTextures[id] = path; return path;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); if (copy != null) Destroy(copy); }
        }

        void DualReady()
        {
            if (!dualInitialized)
            {
                // Same map state as the modal window; don't reset it when a surface disappears.
                if (!open) OnOpen(false);
                dualInitialized = true;
            }
            if (Me != null)
            {
                int z = ZoneOf(new Vector2(Me.position.x, Me.position.z));
                if (dualPlayerZone >= 0 && dualPlayerZone != z) DualCommand("local", "", 0, 0);
                dualPlayerZone = z;
            }
        }
        public string DualSnapshot()
        {
            DualReady();
            var data = new DualMap { location = nh != null ? nh.CurrentDongName : "Location unavailable", x = targetRect.x, y = targetRect.y, width = targetRect.width, height = targetRect.height,
                missions = iconOn.Contains(MissionIcons[0]), shops = iconOn.Contains(ShopIcons[0]), services = iconOn.Contains(IndividualIcons[0]),
                zoomIn = zone == 3 && (zoom == Zoom.Type1 || zoom == Zoom.Type2), zoomOut = zone == 3 && (zoom == Zoom.Type2 || zoom == Zoom.Type3) };
            data.local = zoom == Zoom.Type4; data.filtersEnabled = !data.local;
            AddDualFilter(data, 15, "Missions"); AddDualFilter(data, 10, "Shops");
            foreach (int icon in IndividualIcons) AddDualFilter(data, icon, "Map category " + icon);
            data.tarosImage = DualTexture(UiKit.Tex("tarosicon") ?? UiKit.Tex("taros_count"), 64);
            bool unavailable = RaceSystem.InsMap && RaceSystem.EpId <= 0;
            if (unavailable) data.notice = "No map is available for this instance. Use the main screen to continue.";
            else
            {
                Texture map = zone == 0 || zone == 2 ? FreeZoneTex(Mathf.Clamp((int)zoom - 2, 0, 1)) : zone == 1 ? DarkZoneTex(Mathf.Clamp((int)zoom - 2, 0, 1)) : PayZone((int)zoom);
                data.image = DualTexture(map);
                if (map == null) data.notice = "Map texture unavailable in the downloaded client assets.";
                if (nh != null)
                {
                    foreach (var pr in nh.AllClientNpcs())
                    {
                        var pos = new Vector2(pr.Value.x, pr.Value.z); var n = pos / World;
                        if (!targetRect.Contains(n) || ZoneOf(pos) != zone && zone != 3 || zone == 3 && ZoneOf(pos) != 3) continue;
                        var row = nh.NpcRowOf(pr.Key); if (row == null || presentKnown && !present.Contains(pr.Key)) continue;
                        int sym = nh.MissionSymbolOf(pr.Key), icon = sym == 2 ? 16 : sym == 1 ? 15 : row.m_iMapIcon;
                        if (icon <= 0 || !iconOn.Contains(icon) && zoom != Zoom.Type4) continue;
                        if (!DongAt(pos, out _, out _)) continue;
                        data.markers.Add(new DualMarker { x = n.x, y = n.y, name = nh.NpcNameOf(pr.Key), image = DualTexture(nh.MapIconTexture(icon), 64) });
                    }
                    if (nh.ViewWayPoint && nh.WayPointOption)
                        data.markers.Add(new DualMarker { x = nh.WayPoint.x / World, y = nh.WayPoint.z / World, name = "Mission objective", kind = 1, image = DualTexture(nh.MapIconTexture(2), 64) });
                }
                foreach (var point in userWaypoints) data.markers.Add(new DualMarker { x = point.X / World, y = point.Z / World, name = "Custom waypoint", kind = 1 });
                if (Me != null) data.markers.Add(new DualMarker { x = Me.position.x / World, y = Me.position.z / World, yaw = Me.eulerAngles.y + (nh != null ? nh.PlayerYawOffset : 0), name = "You", kind = 2, image = DualTexture(nh != null ? nh.MapIconTexture(0) : null, 64) });
            }
            var hud = GetComponent<PlayerHud>();
            if (hud != null)
            {
                var d = hud.Data; data.nanoBoost = d.BatteryNano; data.attackBoost = d.BatteryWpn; data.taros = d.Taros;
                for (int i = 0; i < 3; i++)
                {
                    int id = d.NanoSlots[i];
                    var table = nh != null && nh.Tables != null ? nh.Tables.m_pNanoTable?.m_pNanoData : null;
                    int max = table != null && id > 0 && id < table.Length && table[id] != null ? table[id].m_iNanoBattery1 : HudData.NanoStaminaMax;
                    data.nanos.Add(new DualNano { id = id, name = id > 0 ? hud.NanoNameProvider?.Invoke(id) ?? ("Nano " + id) : "Empty", energy = d.NanoStamina[i], max = max, active = d.ActiveNanoSlot == i,
                        cooldown = GetComponent<NanoWheelHud>()?.CooldownRemaining(i) ?? 0 });
                }
            }
            var controller = DualScreenController.Instance;
            data.tab = controller != null ? controller.SecondaryTab : "Map";
            data.equipmentScroll = controller != null ? controller.EquipmentScroll : 0;
            var inventory = GetComponent<InventoryWindow>(); data.station = inventory != null && inventory.DualStation;
            data.connected = GameClient.FindSelf != null && GameClient.FindSelf.WorldConnected;
            data.dead = PlayerLife.Active;
            data.canSummon = data.connected && (GetComponent<NanoWheelHud>()?.CanSummonFromUi ?? false);
            data.canSummonCards = data.connected && (GetComponent<NanoWheelHud>()?.CanSummonFromCard ?? false);
            if (inventory != null && data.tab == "Nanos") data.collection = inventory.DualNanos(controller != null ? controller.SelectedNano : 0, controller != null ? controller.NanoScroll : 0, texture => DualTexture(texture, 128));
            if (inventory != null && data.tab == "Equipment") data.equipment = inventory.DualEquipment(texture => DualTexture(texture, 128));
            if (data.tab == "Journal" && JournalWindow.Instance != null) data.missionsPanel = JournalWindow.Instance.DualMissions(texture => DualTexture(texture, 256));
            var email=EmailWindow.Instance;data.unreadMail=email!=null?email.UnreadCount:0;
            if(data.tab=="EMail"&&email!=null)data.mailPanel=email.DualMail(texture=>DualTexture(texture,128));
            return JsonUtility.ToJson(data);
        }
        void AddDualFilter(DualMap data, int id, string label)
        {
            data.filters.Add(new DualFilter { id = id, label = label, selected = iconOn.Contains(id),
                image = DualTexture(nh != null ? nh.MapIconTexture(id) : null, 64) });
        }
        public void DualCommand(string action, string value, float x, float y)
        {
            if (!dualInitialized) return;
            if (action == "pan") { targetRect.x -= x * targetRect.width; targetRect.y += y * targetRect.height; Clamp(); }
            else if (action == "zoom") { if (value == "in") ZoomIn(CenterPos()); else ZoomOut(CenterPos()); }
            else if (action == "world") { zone = dualPlayerZone >= 0 ? dualPlayerZone : 3; zoom = zone == 3 ? Zoom.Type1 : Zoom.Type3; SetZoomRect(Me != null ? new Vector2(Me.position.x / World, Me.position.z / World) : Vector2.one * .5f); }
            else if (action == "local") { zone = Me != null ? ZoneOf(new Vector2(Me.position.x, Me.position.z)) : 3; zoom = Zoom.Type4; SetZoomRect(Me != null ? new Vector2(Me.position.x / World, Me.position.z / World) : Vector2.one * .5f); }
            else if (action == "filter")
            {
                int[] set;
                if (value == "missions" || value == "15") set = MissionIcons;
                else if (value == "shops" || value == "10") set = ShopIcons;
                else if (value == "services") set = IndividualIcons;
                else if (int.TryParse(value, out int id) && Array.IndexOf(IndividualIcons, id) >= 0) set = new[] { id };
                else return; // Only original filter categories may change the shared preference mask.
                if (zoom == Zoom.Type4) return; // Legacy local view always shows every category.
                SetIcons(set, !iconOn.Contains(set[0])); SaveFilters();
            }
            worldRect = targetRect;
        }
    }
}
