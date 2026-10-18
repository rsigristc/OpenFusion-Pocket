using System;
using Newtonsoft.Json.Linq;
using OpenFusion.World;
using OpenFusion.Screens;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class LegacyRespawnRegressionChecks
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static Rect RectOf(JToken t) => new Rect((float)t["x"], (float)t["y"], (float)t["width"], (float)t["height"]);
        public static void Run()
        {
            var clock = new LegacyRespawnClock(); clock.Enter(100);
            Check(clock.Remaining(100) == 60 && !clock.Due(159.9f) && clock.Due(160), "Legacy auto-resurrection must wait sixty seconds");
            clock.Enter(150); Check(clock.Remaining(150) == 10, "A surface change must not restart the death timer");
            clock.MarkRequested(); Check(!clock.Due(500), "An accepted manual/automatic request must not be repeated after timeout");
            clock.Clear(); clock.Enter(600); Check(clock.Remaining(600) == 60 && !clock.Requested, "A new death must get its own countdown");
            var bindings = JArray.Parse(Resources.Load<TextAsset>("OpenFusion/UI/Data/bindings").text);
            JObject legacy = null;
            foreach (JObject b in bindings) if ((string)b["script"] == "ResurrectMode") { legacy = (JObject)b["fields"]; break; }
            Check(legacy != null, "Original ResurrectMode binding missing");
            Check(RectOf(legacy["WindowRect"]) == PlayerLife.LegacyWindow && RectOf(legacy["GrimPos"]) == PlayerLife.LegacyGrim &&
                RectOf(legacy["ResurectButton"]) == PlayerLife.LegacyRevive && RectOf(legacy["PhoenixButton"]) == PlayerLife.LegacyPhoenix &&
                RectOf(legacy["UseItemButton"]) == PlayerLife.LegacyUseItem && RectOf(legacy["TextLabel"]) == PlayerLife.LegacyText &&
                (float)legacy["fResurrectTime"] == LegacyRespawnClock.Duration, "Respawn layout/countdown diverged from the actual Legacy prefab");
            foreach (string name in new[] { "grim", "resurrect", "systemDialogBox", "buff_icon_back", "back", "blue_button_normal" }) Check(UiKit.Tex(name) != null, "Missing original respawn texture: " + name);
            var box = Skin.Get(Skin.Menu, "box");
            Check(box != null && box.N == UiKit.Tex("systemDialogBox") && box.bL == 40 && box.bB == 40, "Original system dialog slice skin was not resolved");
            Check(Skin.Get(Skin.Menu, "button")?.Font?.Atlas != null && Skin.Get(Skin.Menu, "smallfont")?.Font?.Atlas != null && Skin.Get(Skin.Menu, "BigFont16")?.Font?.Atlas != null, "Original respawn bitmap fonts unavailable");
            foreach (var safe in new[] { new Rect(0, 0, 1920, 1080), new Rect(24, 48, 1032, 2268), new Rect(12, 20, 616, 320), new Rect(0, 0, 1848, 2448) }) {
                float s = Mathf.Min(safe.width / 544, safe.height / 359);
                var r = PlayerLife.LegacyPlacement(safe, s);
                Check(r.xMin >= safe.xMin && r.xMax <= safe.xMax && r.yMax <= safe.yMax && r.y + PlayerLife.LegacyGrim.y * s >= safe.yMin,
                    "Legacy death panel/Grim must remain usable in landscape, cover, fold and safe insets");
            }
            Debug.Log("[legacy-respawn-regression] Passed: original prefab geometry, textures, bitmap fonts, safe areas and single-shot 60-second countdown.");
        }
    }
}
