using System;
using UnityEngine;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    public sealed class LegacyRespawnClock
    {
        public const float Duration = 60;
        public bool Running { get; private set; }
        public bool Requested { get; private set; }
        float started;
        public void Enter(float now) { if (Running) return; started = now; Running = true; Requested = false; }
        public void Clear() { Running = Requested = false; }
        public void MarkRequested() { Requested = true; }
        public float Remaining(float now) => Running ? Math.Max(0, Duration - Math.Max(0, now - started)) : Duration;
        public bool Due(float now) => Running && !Requested && Remaining(now) <= 0;
    }

    public sealed partial class PlayerLife
    {
        // ResurrectMode path_id 1438 and FusionFallSysMessageSkin, extracted from Legacy.
        public static readonly Rect LegacyWindow = new Rect(0, 0, 520, 164);
        public static readonly Rect LegacyGrim = new Rect(132, -171, 255, 196);
        public static readonly Rect LegacyRevive = new Rect(374, 124, 110, 25);
        public static readonly Rect LegacyPhoenix = new Rect(44, 124, 150, 25);
        public static readonly Rect LegacyUseItem = new Rect(209, 124, 150, 25);
        public static readonly Rect LegacyText = new Rect(140, 20, 380, 104);

        public static Rect LegacyPlacement(Rect safe, float scale)
        {
            float w = LegacyWindow.width * scale, h = LegacyWindow.height * scale;
            float y = Mathf.Clamp(safe.center.y - h / 2, safe.yMin + 183 * scale, safe.yMax - h - 12 * scale);
            return new Rect(safe.center.x - w / 2, y, w, h);
        }

        void DrawLegacyRespawn()
        {
            if (!Active || GameMenu.Open || GameMenu.Current != GameMenu.Win.None || Screen.width <= 0 || Screen.height <= 0) return;
            var e = Event.current; if (e == null) return;
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth; bool enabled = GUI.enabled;
            try {
                GUI.matrix = Matrix4x4.identity; GUI.depth = -200; GUI.color = Color.white;
                if (e.type == EventType.Repaint) {
                    var black = UiKit.Tex("back");
                    GUI.color = new Color(1, 1, 1, .55f);
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black != null ? black : Texture2D.blackTexture);
                    GUI.color = Color.white;
                }
                var area = Screen.safeArea;
                var safe = area.width > 0 && area.height > 0 ? new Rect(area.x, Screen.height - area.yMax, area.width, area.height) : new Rect(0, 0, Screen.width, Screen.height);
                float scale = Mathf.Min(GameOptions.UiScale, safe.width / 544, safe.height / 359);
                if (scale <= 0) return;
                var panel = LegacyPlacement(safe, scale);
                GUI.matrix = Matrix4x4.TRS(new Vector3(panel.x, panel.y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
                var grim = UiKit.Tex("grim"); var icon = UiKit.Tex("resurrect");
                if (e.type == EventType.Repaint && grim != null) GUI.DrawTexture(LegacyGrim, grim, ScaleMode.StretchToFill, true);
                Skin.Box(LegacyWindow, Skin.Get(Skin.Menu, "box"));
                if (e.type == EventType.Repaint && icon != null) GUI.DrawTexture(new Rect(60, 25, 46, 71), icon, ScaleMode.ScaleToFit, true);
                Skin.Label(new Rect(LegacyText.x, LegacyText.y, LegacyText.width - 20, 25), Loc.T("You have been defeated!"), Skin.Get(Skin.Menu, "BigFont16"), Color.cyan);
                Skin.Label(new Rect(LegacyText.x, LegacyText.y + 28, LegacyText.width - 20, 43), Loc.T("Would you like to proceed to the nearest Resurrect 'Em?"), Skin.Get(Skin.Menu, "smallfont2"), Color.white);
                var client = GameClient.FindSelf; bool connected = client != null && client.WorldConnected;
                string status = !connected ? "Disconnected." : pending ? "Waiting for the server…" : respawnClock.Requested ? "Please select a revival option." :
                    Loc.T("You will go automatically in") + " " + Mathf.CeilToInt(respawnClock.Remaining(Time.unscaledTime)).ToString();
                Skin.Label(new Rect(LegacyText.x, LegacyText.y + 73, LegacyText.width - 20, 22), status, Skin.Get(Skin.Menu, "smallfont"), Color.cyan);

                bool group = Time.unscaledTime < groupReviveUntil;
                bool phoenix = GetComponent<NanocomHud>()?.HasPhoenix ?? false;
                var button = Skin.Get(Skin.Menu, "button");
                bool canRequest = enabled && connected && !pending;
                // Group/self Phoenix still use server-authorized request types 4/3.
                if (Skin.Button(LegacyPhoenix, "", button, canRequest && (group || phoenix))) RequestRespawn(group ? 4 : 3);
                Skin.Label(new Rect(79, 124, 110, 25), Loc.T("Nano Power"), Skin.Get(Skin.Menu, "smallfont"), canRequest && (group || phoenix) ? Color.white : Color.gray);
                if (e.type == EventType.Repaint) {
                    var previous = GUI.color; GUI.color = canRequest && (group || phoenix) ? Color.white : new Color(1, 1, 1, .35f);
                    var back = UiKit.Tex("buff_icon_back"); if (back != null) GUI.DrawTexture(new Rect(44, 120, 32, 32), back);
                    int slot = hud.Data.ActiveNanoSlot;
                    var skill = slot >= 0 && slot < 3 ? hud.NanoSkillIconProvider?.Invoke(hud.Data.NanoSlots[slot]) : null;
                    if (skill != null) GUI.DrawTexture(new Rect(50, 127, 20, 20), skill, ScaleMode.ScaleToFit, true);
                    GUI.color = previous;
                }
                // The reference server does not verify/consume Phoenix items in REGEN.
                // Preserve the original control but never pretend an unsupported item was used.
                Skin.Button(LegacyUseItem, Loc.T("USE ITEM"), button, false);
                if (Skin.Button(LegacyRevive, Loc.T("REVIVE"), button, canRequest)) RequestRespawn();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            } finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; GUI.enabled = enabled; }
        }
    }
}
