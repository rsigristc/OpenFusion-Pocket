using System;
using System.IO;
using System.Reflection;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class DualScreenRegressionChecks
    {
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static DualScreenCapabilities Fold() => new DualScreenCapabilities { reliable = true, available = true, fold = true, flat = true, kind = 1, density = 2, width = 1800, height = 1600, primaryWidth = 1800, primaryHeight = 1000, secondaryWidth = 1800, secondaryHeight = 600 };
        public static void Run()
        {
            var c = Fold();
            Require(DualScreenPolicy.ShouldUse(DualScreenPreference.Automatic, c, true, false), "Verified flat fold must support Auto.");
            Require(!DualScreenPolicy.ShouldUse(DualScreenPreference.SingleScreen, c, true, false), "Single Screen must always override fold evidence.");
            Require(!DualScreenPolicy.ShouldUse(DualScreenPreference.DualScreen, c, false, false), "Launcher/login must retain the full surface.");
            Require(!DualScreenPolicy.ShouldUse(DualScreenPreference.Automatic, c, true, true), "Modal panels must restore the complete legacy interface.");
            c.fold = false;
            Require(!DualScreenPolicy.CanUse(c), "A large conventional screen must NEVER enable DS, even with large panes.");
            c = Fold(); c.reliable = false;
            Require(!DualScreenPolicy.CanUse(c), "Unknown compatibility must fail closed.");
            c = Fold(); c.available = false;
            Require(!DualScreenPolicy.CanUse(c), "Folded / cover display without a current feature must restore Single.");
            c = Fold(); c.flat = false; c.separating = true;
            Require(DualScreenPolicy.CanUse(c), "A useful separating posture may use DS.");
            c.secondaryHeight = 50;
            Require(!DualScreenPolicy.CanUse(c), "A hinge/keyboard leaving insufficient panel space must reject DS.");
            c = Fold(); c.kind = 2; c.fold = false; c.physical = true;
            Require(!DualScreenPolicy.CanUse(c), "An external display without independently verified touch must be rejected.");
            c.touchVerified = true;
            Require(DualScreenPolicy.CanUse(c), "A verified adequately sized physical display must be accepted.");
            c.available = false;
            Require(!DualScreenPolicy.CanUse(c), "Unplugging a display must restore Single immediately.");
            c = Fold(); c.density = float.NaN;
            Require(!DualScreenPolicy.CanUse(c), "Invalid platform metrics must be rejected.");
            for (int i = 0; i < 20; i++)
            {
                c = Fold(); c.available = i % 2 == 0;
                Require(DualScreenPolicy.ShouldUse(DualScreenPreference.Automatic, c, true, false) == (i % 2 == 0), "Repeated capability transitions must not latch DS.");
            }
            GameObject host = null;
            bool hadLow = PlayerPrefs.HasKey("MapFiltersLow"), hadHigh = PlayerPrefs.HasKey("MapFiltersHigh");
            int oldLow = PlayerPrefs.GetInt("MapFiltersLow"), oldHigh = PlayerPrefs.GetInt("MapFiltersHigh");
            var oldSender = NpcUI.Sender; bool mapWasImplemented = GameMenu.Implemented.Contains(GameMenu.Win.Map);
            try
            {
                NpcUI.Sender = null; host = new GameObject("DS regression");
                var hud = host.AddComponent<PlayerHud>(); hud.Data.BatteryNano = 1234; hud.Data.BatteryWpn = 5678; hud.Data.Taros = 24680;
                hud.Data.NanoSlots[0] = 7; hud.Data.NanoStamina[0] = 81;
                var map = WorldMapWindow.Ensure(host);
                // Edit-mode AddComponent does not run Awake for ordinary behaviours.
                typeof(WorldMapWindow).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(map, null);
                string first = map.DualSnapshot();
                Require(first.Contains("\"nanoBoost\":1234") && first.Contains("\"attackBoost\":5678") && first.Contains("\"energy\":81") && first.Contains("\"taros\":24680"), "Secondary values must come from the SAME live HUD state.");
                var mapFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                var zoneField = typeof(WorldMapWindow).GetField("zone", mapFlags);
                var zoomField = typeof(WorldMapWindow).GetField("zoom", mapFlags);
                var rectField = typeof(WorldMapWindow).GetField("targetRect", mapFlags);
                for (int region = 0; region < 4; region++)
                {
                    zoneField.SetValue(map, region);
                    int minimum = region == 3 ? 0 : 2;
                    zoomField.SetValue(map, Enum.ToObject(zoomField.FieldType, minimum));
                    typeof(WorldMapWindow).GetMethod("SetZoomRect", mapFlags).Invoke(map, new object[] { Vector2.one * .5f });
                    var wide = (Rect)rectField.GetValue(map);
                    Require(map.DualSnapshot().Contains("\"zoomIn\":true"), "Every regional world view must advertise zoom in.");
                    for (int step = 0; step < 5; step++) map.DualCommand("zoom", "in", 0, 0);
                    var close = (Rect)rectField.GetValue(map);
                    Require(close.width < wide.width && System.Convert.ToInt32(zoomField.GetValue(map)) == 3, "Regional zoom must reach local view and clamp at its nearest level.");
                    Require(map.DualSnapshot().Contains("\"zoomOut\":true"), "Local view must retain zoom out in Companion.");
                    for (int step = 0; step < 5; step++) map.DualCommand("zoom", "out", 0, 0);
                    Require(System.Convert.ToInt32(zoomField.GetValue(map)) == minimum && Mathf.Approximately(((Rect)rectField.GetValue(map)).width, wide.width), "Zoom out must restore each region's world extent without using another region's texture.");
                }
                map.DualCommand("zoom", "in", 0, 0); map.DualCommand("pan", "", .2f, -.1f);
                var field = typeof(WorldMapWindow).GetField("targetRect", BindingFlags.Instance | BindingFlags.NonPublic);
                var before = (Rect)field.GetValue(map);
                typeof(WorldMapWindow).GetMethod("OnOpen", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(map, new object[] { false });
                Require((Rect)field.GetValue(map) == before, "Switching to the existing full-screen map must preserve zoom and pan.");
                map.DualCommand("world", "", 0, 0);
                var icons = (System.Collections.Generic.HashSet<int>)typeof(WorldMapWindow).GetField("iconOn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(map);
                bool service = icons.Contains(13), other = icons.Contains(12), mission = icons.Contains(15);
                map.DualCommand("filter", "13", 0, 0);
                Require(icons.Contains(13) != service && icons.Contains(12) == other, "An individual DS filter must not toggle other categories.");
                map.DualCommand("filter", "15", 0, 0);
                Require(icons.Contains(15) != mission && icons.Contains(16) == icons.Contains(15), "Mission filters must retain the original grouped semantics.");
                int count = icons.Count; map.DualCommand("filter", "999", 0, 0);
                Require(icons.Count == count, "Unknown filters must be ignored.");
                map.DualCommand("local", "", 0, 0); service = icons.Contains(13);
                map.DualCommand("filter", "13", 0, 0);
                Require(icons.Contains(13) == service, "Local map must preserve the original all-category display.");
                map.DualCommand("world", "", 0, 0);
                hud.Data.Taros = 97531;
                Require(map.DualSnapshot().Contains("\"taros\":97531"), "Taros must reflect updates from the shared server-driven HUD.");
                hud.Data.BatteryWpn = 90;
                Require(map.DualSnapshot().Contains("\"attackBoost\":90"), "A server-updated counter must be reflected without a duplicated data model.");
                map.DualCommand("zoom", "in", 0, 0);
                var controller = host.AddComponent<DualScreenController>();
                var caps = Fold(); caps.active = true; caps.revision = 9;
                typeof(DualScreenController).GetProperty("Capabilities").GetSetMethod(true).Invoke(controller, new object[] { caps });
                var handle = typeof(DualScreenController).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                before = (Rect)field.GetValue(map);
                handle.Invoke(controller, new object[] { "{\"action\":\"pan\",\"x\":0.1,\"revision\":8}" });
                Require((Rect)field.GetValue(map) == before, "A command from a detached surface must not reach the current panel.");
                handle.Invoke(controller, new object[] { "{\"action\":\"pan\",\"x\":0.1,\"revision\":9}" });
                Require((Rect)field.GetValue(map) != before, "A current-surface command must still reach the shared map.");
                var touch = host.AddComponent<TouchControls>(); touch.CancelForSurfaceChange();
                Require(TouchControls.Move == Vector2.zero && TouchControls.Look == Vector2.zero && TouchControls.Pinch == 0, "Transition must clear touch motion.");
            }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                NpcUI.Sender = oldSender;
                if (!mapWasImplemented) GameMenu.Implemented.Remove(GameMenu.Win.Map);
                if (hadLow) PlayerPrefs.SetInt("MapFiltersLow", oldLow); else PlayerPrefs.DeleteKey("MapFiltersLow");
                if (hadHigh) PlayerPrefs.SetInt("MapFiltersHigh", oldHigh); else PlayerPrefs.DeleteKey("MapFiltersHigh");
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/dual-screen-regression.txt", "PASS: conventional/unknown/folded/external-unverified rejection; verified fold/physical selection; Single override; modals; insufficient space; repeated disconnects; shared live counters and map state; touch reset.\nHardware posture, native touch routing and visual validation still require a compatible device.\n");
            Debug.Log("[DS] Policy and shared-state regression checks passed.");
        }
    }
}
