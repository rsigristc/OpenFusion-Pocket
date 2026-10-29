using System;
using UnityEngine;

namespace OpenFusion.World
{
    // No scene reloads, network handlers, duplicated gameplay state or extra world cameras.
    [DefaultExecutionOrder(-300)]
    public sealed class DualScreenController : MonoBehaviour
    {
        public static DualScreenController Instance { get; private set; }
        public DualScreenCapabilities Capabilities { get; private set; } = new DualScreenCapabilities();
        public bool Active => Capabilities.active;
        // Station dialogs/windows stay on the Unity surface; DS remains available for inspection.
        public static bool SecondaryBlocked => (GameMenu.Blocking && !GameMenu.NanoStationOnly && !GameMenu.DeathOnly && !GameMenu.EquipmentInfoOnly) ||
            (NpcUI.Active && !NpcUI.NanoStationActive) || WelcomePopup.Active || ChatHud.Typing;
        public static bool SecondaryCommandAllowed(string action) => !TutorialTraining.Active && !SecondaryBlocked &&
            (!(GameMenu.Blocking || NpcUI.Active) || action == "tab" || action == "nanoSelect" || action == "nanoScroll" || action == "missionSelect" || action == "missionMode" || action == "missionScroll" ||
                action == "pan" || action == "zoom" || action == "world" || action == "local" || action == "filter" ||
                (GameMenu.NanoStationOnly && !NpcUI.Active &&
                    (action == "nanoEquip" || action == "nanoUnequip" || action == "nanoTune" || action == "closeStation" || action == "nanoCard")));
        public static bool ShowSecondaryNanos => Instance != null && Instance.Active;
        public string SecondaryTab { get; private set; } = "Map";
        public int SelectedNano { get; private set; }
        public int NanoScroll { get; private set; }
        public int EquipmentScroll { get; private set; }
        public void StationOpened() { nextPanel = 0; }
        const string PreferenceKey = "pocket.dualScreen.preference";
        public DualScreenPreference Preference { get; private set; }
        PocketDualScreenBridge bridge;
        float nextPoll, nextPanel;
        bool requested, failed;
        bool originalPortrait, originalUpsideDown, foldRotation;
        int revision = -1, viewWidth, viewHeight;

        public static void Ensure(GameObject host)
        {
            if (host.GetComponent<DualScreenController>() == null) host.AddComponent<DualScreenController>();
            if (host.GetComponent<DualNanoPreview>() == null) host.AddComponent<DualNanoPreview>();
            if (host.GetComponent<DualEquipmentPreview>() == null) host.AddComponent<DualEquipmentPreview>();
        }
        void Awake()
        {
            Instance = this;
            originalPortrait = Screen.autorotateToPortrait; originalUpsideDown = Screen.autorotateToPortraitUpsideDown;
            Preference = (DualScreenPreference)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, 0), 0, 2);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var cache = System.IO.Path.Combine(Application.temporaryCachePath, "dual-screen");
                if (System.IO.Directory.Exists(cache)) foreach (var file in System.IO.Directory.GetFiles(cache, "*.png"))
                    try { System.IO.File.Delete(file); } catch (System.IO.IOException) { }
                bridge = PocketDualScreenBridge.Start();
            }
            catch (Exception e) { Fail(e); }
#endif
            if (OpenFusion.Screens.PocketIos.Available) {
                Preference = DualScreenPreference.SingleScreen;
                Capabilities = new DualScreenCapabilities { reason = "This iOS preview uses the full screen. Dual Screen is not available in this build." };
            }
        }
        void Fail(Exception e)
        {
            failed = true; requested = false;
            if (foldRotation) { foldRotation = false; Screen.autorotateToPortrait = originalPortrait; Screen.autorotateToPortraitUpsideDown = originalUpsideDown; }
            Capabilities = new DualScreenCapabilities { reason = "Dual Screen is unavailable on this Android configuration." };
            Debug.LogWarning("[DS] Single-screen fallback: " + e.Message);
            try { bridge?.CallStatic("setActive", false); } catch (Exception) { }
        }
        void Update()
        {
            if (viewWidth != Screen.width || viewHeight != Screen.height)
            { viewWidth = Screen.width; viewHeight = Screen.height; TouchControls.Instance?.CancelForSurfaceChange(); }
            if (bridge == null || failed) return;
            try {
                string command;
                for (int i = 0; i < 32 && !string.IsNullOrEmpty(command = bridge.CallStatic<string>("command")); i++) {
                    if (i == 0) Capabilities = JsonUtility.FromJson<DualScreenCapabilities>(bridge.CallStatic<string>("status")) ?? new DualScreenCapabilities();
                    Handle(command);
                }
            } catch (Exception e) { Fail(e); return; }
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .15f;
            try
            {
                Capabilities = JsonUtility.FromJson<DualScreenCapabilities>(bridge.CallStatic<string>("status")) ?? new DualScreenCapabilities();
                var client = GameClient.FindSelf;
                bool ready = !TutorialTraining.Active && client != null && client.Bootstrap != null && client.Bootstrap.Entities != null && client.Bootstrap.Entities.Self != null && !WorldBootstrap.Travelling;
                var inventory = InventoryWindow.Instance;
                bool station = inventory != null && inventory.DualStation;
                if (!Active && station && inventory.DualLegacySelection > 0) SelectedNano = inventory.DualLegacySelection;
                bool modal = SecondaryBlocked;
                bool desired = DualScreenPolicy.ShouldUse(Preference, Capabilities, ready, modal);
                bool rotation = Preference != DualScreenPreference.SingleScreen && Capabilities.reliable && Capabilities.fold && (foldRotation || DualScreenPolicy.CanUse(Capabilities));
                if (rotation != foldRotation) { foldRotation = rotation; Screen.autorotateToPortrait = rotation || originalPortrait; Screen.autorotateToPortraitUpsideDown = rotation || originalUpsideDown; }
                if (desired != requested) { if (!desired && station) inventory.DualRestoreSelection(SelectedNano); requested = desired; TouchControls.Instance?.CancelForSurfaceChange(); }
                bridge.CallStatic("setActive", desired);
                if (revision != Capabilities.revision)
                {
                    revision = Capabilities.revision; viewWidth = Screen.width; viewHeight = Screen.height;
                    TouchControls.Instance?.CancelForSurfaceChange();
                    Debug.Log("[DS] revision=" + revision + " active=" + Capabilities.active + " kind=" + Capabilities.kind + " Unity=" + viewWidth + "x" + viewHeight);
                }
                if (Capabilities.active && Time.unscaledTime >= nextPanel && WorldMapWindow.Instance != null)
                {
                    nextPanel = Time.unscaledTime + .25f;
                    bridge.CallStatic("panel", WorldMapWindow.Instance.DualSnapshot());
                }
            }
            catch (Exception e) { Fail(e); }
        }
        [Serializable] sealed class Command { public string action, value; public float x, y; public int revision; }
        void Handle(string json)
        {
            var c = JsonUtility.FromJson<Command>(json); if (c == null) return;
            if (c.action == "preference")
            {
                if (int.TryParse(c.value, out int p) && p >= 0 && p <= 2)
                { Preference = (DualScreenPreference)p; PlayerPrefs.SetInt(PreferenceKey, p); PlayerPrefs.Save(); }
                return;
            }
            // Commands queued before a detach/modal transition must never act on gameplay.
            if (!Active || c.revision != Capabilities.revision || !SecondaryCommandAllowed(c.action))
            {
                if (c.action != "nanoScroll") Debug.Log("[DS input] rejected=" + c.action + " active=" + Active + " revision=" + c.revision + "/" + Capabilities.revision + " window=" + GameMenu.Current + " station=" + InventoryWindow.Machine + " npc=" + NpcUI.Active);
                return;
            }
            if (c.action == "boost") { if (c.value == "nano" || c.value == "attack") GameClient.FindSelf?.GetComponent<CombatSystem>()?.ActivateBoost(c.value == "nano"); nextPanel = 0; return; }
            if (c.action == "cameraZoom") { if (c.value == "in" || c.value == "out") GameClient.FindSelf?.ZoomWorldCamera(c.value == "in"); return; }
            var inventory = InventoryWindow.Instance;
            if ((c.action == "nano" || c.action == "nanoCard" || c.action == "nanoEquip" || c.action == "nanoUnequip" || c.action == "nanoTune") &&
                (GameClient.FindSelf == null || !GameClient.FindSelf.WorldConnected))
            { Debug.Log("[DS input] rejected=" + c.action + " reason=disconnected"); return; }
            if (c.action == "tab") { if (c.value == "Map" || c.value == "Nanos" || c.value == "Equipment" || c.value == "Journal" || c.value=="EMail") { SecondaryTab = c.value; nextPanel = 0; Debug.Log("[DS] secondary=" + SecondaryTab); } return; }
            if(c.action.StartsWith("mail",StringComparison.Ordinal)){EmailWindow.Instance?.DualMailCommand(c.action,c.value);nextPanel=0;return;}
            if (c.action == "missionMode") { if (c.value == "0" || c.value == "1") JournalWindow.Instance?.DualMissionMode(c.value == "1"); nextPanel = 0; return; }
            if (c.action == "missionScroll") { if ((c.value == "0" || c.value == "1") && c.x == Mathf.RoundToInt(c.x) && c.y == Mathf.RoundToInt(c.y)) JournalWindow.Instance?.DualMissionScroll(c.value == "1", (int)c.x, (int)c.y); return; }
            if (c.action == "missionSelect" || c.action == "missionFollow" || c.action == "missionAbandon") {
                if (int.TryParse(c.value, out int mission) && c.x == Mathf.RoundToInt(c.x)) {
                    var journal = JournalWindow.Instance;
                    if (c.action == "missionSelect") journal?.DualSelectMission(mission, (int)c.x);
                    else if (GameClient.FindSelf != null && GameClient.FindSelf.WorldConnected) {
                        if (c.action == "missionFollow") journal?.DualFollowMission(mission, (int)c.x);
                        else journal?.DualAbandonMission(mission, (int)c.x);
                    }
                }
                nextPanel = 0; return;
            }
            if (c.action == "nanoGumball") { if (GameClient.FindSelf != null && GameClient.FindSelf.WorldConnected && int.TryParse(c.value, out int nanoId) && c.x == Mathf.RoundToInt(c.x)) inventory?.DualUseGumball(nanoId, (int)c.x); nextPanel = 0; return; }
            if (c.action == "nanoSelect") { if (int.TryParse(c.value, out int id) && inventory != null && inventory.DualNanoExists(id)) { SelectedNano = id; nextPanel = 0; Debug.Log("[DS Nanos] selected=" + id); } return; }
            if (c.action == "nanoScroll") { NanoScroll = Mathf.Clamp(Mathf.RoundToInt(c.y), 0, 100000); return; }
            if (c.action == "closeStation") { if (GameMenu.NanoStationOnly && inventory != null && !inventory.DualNanoBusy) GameMenu.CloseWindow(); return; }
            if (c.action == "nanoCard")
            {
                var wheel = GameClient.FindSelf?.Bootstrap?.GetComponent<NanoWheelHud>();
                bool sent = int.TryParse(c.value, out int id) && c.x == Mathf.RoundToInt(c.x) && wheel != null && wheel.RequestSummonFromCard((int)c.x, id);
                Debug.Log("[DS Nanos] card summon id=" + c.value + " slot=" + c.x + " sent=" + sent); nextPanel = 0; return;
            }
            if (c.action == "nanoEquip" || c.action == "nanoUnequip" || c.action == "nanoTune")
            {
                if (inventory != null && int.TryParse(c.value, out int id) && c.x == Mathf.RoundToInt(c.x))
                {
                    bool sent;
                    if (c.action == "nanoEquip") sent = inventory.DualEquipNano(id, (int)c.x);
                    else if (c.action == "nanoUnequip") sent = inventory.DualUnequipNano(id);
                    else sent = inventory.DualTuneNano(id, (int)c.x);
                    Debug.Log("[DS Nanos] " + c.action + " id=" + id + " target=" + c.x + " sent=" + sent);
                    nextPanel = 0;
                }
                return;
            }
            if (c.action == "equipmentScroll") { EquipmentScroll = Mathf.Clamp(Mathf.RoundToInt(c.y), 0, 100000); return; }
            if (c.action == "itemInspect") { if (c.x == Mathf.RoundToInt(c.x) && (c.y == 0 || c.y == 1)) inventory?.DualInspectItem((int)c.x, c.y == 1, c.value); nextPanel = 0; return; }
            if (c.action == "equipmentRotate" && !GameMenu.Blocking && !NpcUI.Active) { if (inventory?.Preview != null) inventory.Preview.Yaw += Mathf.Clamp(c.x, -90, 90); return; }
            // A station still blocks world input, summon shortcuts and unrelated windows.
            if (GameMenu.Blocking || NpcUI.Active)
            { WorldMapWindow.Instance?.DualCommand(c.action, c.value, c.x, c.y); return; }
            if (c.action == "window")
            {
                if (c.value == "Chat") ChatHud.Instance?.Open();
                else if (c.value == "Nanos" || c.value == "Equipment") InventoryWindow.Instance?.OpenTab(c.value == "Nanos");
                else if (Enum.TryParse(c.value, out GameMenu.Win win)) GameMenu.OpenWindow(win);
            }
            else if (c.action == "nano")
            {
                var wheel = GameClient.FindSelf?.Bootstrap?.GetComponent<NanoWheelHud>();
                bool sent = wheel != null && int.TryParse(c.value, out int slot) && wheel.RequestSlotFromUi(slot);
                Debug.Log("[DS Nanos] summon slot=" + c.value + " wheel=" + (wheel != null) + " allowed=" + (wheel != null && wheel.CanSummonFromUi) + " sent=" + sent);
            }
            else WorldMapWindow.Instance?.DualCommand(c.action, c.value, c.x, c.y);
        }
        public bool SendNanoPreview(int frameRevision, int[] ids, int[] pixels)
        {
            if (bridge == null || failed || !Active || SecondaryTab != "Nanos" || Capabilities.revision != frameRevision) return false;
            bridge.CallStatic("nanoFrame", frameRevision, ids, pixels); return true;
        }
        public bool SendEquipmentPreview(int frameRevision, int[] pixels)
        {
            if (bridge == null || failed || !Active || SecondaryTab != "Equipment" || Capabilities.revision != frameRevision) return false;
            bridge.CallStatic("avatarFrame", frameRevision, pixels); return true;
        }
        public static void ShowSettings()
        {
            var i = Instance; if (i == null) return;
            if (i.bridge != null && !i.failed) { try { i.bridge.CallStatic("settings", (int)i.Preference); return; } catch (Exception e) { i.Fail(e); } }
            i.fallbackSettings = true;
        }
        bool fallbackSettings;
        void OnGUI()
        {
            if (!fallbackSettings) return;
            var old = GUI.depth; GUI.depth = -20000;
            var r = new Rect(Mathf.Max(8, (Screen.width - 480) / 2), Mathf.Max(8, (Screen.height - 200) / 2), Mathf.Min(Screen.width - 16, 480), 200);
            GUI.Box(r, "Dual Screen — Single Screen active");
            GUI.Label(new Rect(r.x + 16, r.y + 40, r.width - 32, 90), Capabilities.reason);
            if (GUI.Button(new Rect(r.x + 16, r.yMax - 50, r.width - 32, 36), "Close")) fallbackSettings = false;
            GUI.depth = old;
        }
        void OnApplicationPause(bool paused)
        {
            if (paused) { requested = false; try { bridge?.CallStatic("setActive", false); } catch (Exception e) { Fail(e); } TouchControls.Instance?.CancelForSurfaceChange(); }
            else nextPoll = 0;
        }
        void OnApplicationFocus(bool focus) { if (focus) { requested = false; nextPoll = 0; } }
        void OnDestroy()
        {
            try { bridge?.CallStatic("stop"); } catch (Exception) { }
            bridge?.Dispose(); bridge = null; if (Instance == this) Instance = null;
            if (foldRotation) { Screen.autorotateToPortrait = originalPortrait; Screen.autorotateToPortraitUpsideDown = originalUpsideDown; }
        }
    }
}
