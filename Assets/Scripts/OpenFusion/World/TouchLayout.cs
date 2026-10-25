using UnityEngine;

namespace OpenFusion.World
{
    // Independent preferences, shared with the settings Apply/Cancel transaction.
    public static partial class TouchLayout
    {
        public static readonly ControlMap.K[] Actions = { ControlMap.K.Fire1, ControlMap.K.Jump,
            ControlMap.K.WpnChange, ControlMap.K.VehicleToggle, ControlMap.K.WpnCharge,
            ControlMap.K.NanoCharge, ControlMap.K.Fire2, ControlMap.K.Nano1, ControlMap.K.Nano2,
            ControlMap.K.Nano3, ControlMap.K.CameraShoulderToggle, ControlMap.K.AutoRun,
            ControlMap.K.Esc, ControlMap.K.WorldMap, ControlMap.K.Inventory };
        public static readonly string[] Labels = { "ATK", "JUMP", "WEAPON", "VEHICLE", "WEAPON BOOST",
            "NANO BOOST", "POWER", "NANO 1", "NANO 2", "NANO 3", "AIM", "AUTORUN", "MENU", "MAP", "INVENTORY" };
        static bool loaded;
        static int mask, backupMask;
        static bool hideWithPad, backupHide;
        static float scale, backupScale; static string backupLayout;
        const int DefaultMask = (1 << 7) - 1 | (1 << 12);
        public static bool HideWithPad { get { Load(); return hideWithPad; } set { Load(); hideWithPad = value; } }
        public static float Scale { get { Load(); return scale; } set { Load(); scale = Mathf.Clamp(value, .65f, 1.3f); } }
        public static void Load()
        {
            if (loaded) return; loaded = true;
            mask = PlayerPrefs.GetInt("Touch.Buttons", DefaultMask);
            hideWithPad = PlayerPrefs.GetInt("Touch.HideWithPad", 1) != 0;
            scale = Mathf.Clamp(PlayerPrefs.GetFloat("Touch.Scale", 1f), .65f, 1.3f); ReadGeometry(PlayerPrefs.GetString("Touch.Geometry", ""));
        }
        public static bool Enabled(int i) { Load(); return (mask & (1 << i)) != 0; }
        public static void Enable(int i, bool value) { Load(); if (value) mask |= 1 << i; else mask &= ~(1 << i); }
        public static void Reset() { loaded = true; mask = DefaultMask; hideWithPad = true; scale = 1f; ResetGeometry(); }
        public static void Save() { Load(); PlayerPrefs.SetInt("Touch.Buttons", mask); PlayerPrefs.SetInt("Touch.HideWithPad", hideWithPad ? 1 : 0); PlayerPrefs.SetFloat("Touch.Scale", scale); PlayerPrefs.SetString("Touch.Geometry", GeometryJson()); }
        public static void Push() { Load(); backupMask = mask; backupHide = hideWithPad; backupScale = scale; backupLayout = GeometryJson(); }
        public static void Pop() { mask = backupMask; hideWithPad = backupHide; scale = backupScale; ReadGeometry(backupLayout); }
    }
}
