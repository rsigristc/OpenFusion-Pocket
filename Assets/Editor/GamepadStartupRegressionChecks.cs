using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using OpenFusion.World;

namespace OpenFusion.EditorTools
{
    public static class GamepadStartupRegressionChecks
    {
        const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
        static void Load(bool android) => typeof(ControlMap).GetMethod("LoadForPlatform", Flags).Invoke(null, new object[] { android });
        static void Persist() => typeof(ControlMap).GetMethod("SavePadPreferences", Flags).Invoke(null, null);
        static ControlMap.Bind Pad(ControlMap.K key) => ((ControlMap.Bind[])typeof(ControlMap).GetField("pad", Flags).GetValue(null))[(int)key];
        public static void Run()
        {
            CheckSenderLifetime();
            // Reproduce closing a welcome popup over the chat HUD with a touch,
            // then pressing R1 while Android still reports that pointer position.
            Check(CombatSystem.AttackRequested(false, true, false, true), "HUD hover blocked controller/keyboard/touch attack");
            Check(!CombatSystem.AttackRequested(false, true, true, false), "Clicking HUD fired weapon");
            Check(CombatSystem.AttackRequested(false, false, true, false), "World mouse click did not attack");
            Check(!CombatSystem.AttackRequested(false, true, false, false), "Hover generated an attack without input");
            Check(!CombatSystem.AttackRequested(true, false, true, true), "Modal/chat typing must block all attack input");
            Check(ControlMap.IsPointerKey(KeyCode.Mouse0) && ControlMap.IsPointerKey(KeyCode.Mouse1) &&
                !ControlMap.IsPointerKey(KeyCode.JoystickButton5) && !ControlMap.IsPointerKey(KeyCode.Z), "Input source classification");

            var saved = new Dictionary<string, int?>();
            Action<string> backup = k => saved[k] = PlayerPrefs.HasKey(k) ? (int?)PlayerPrefs.GetInt(k) : null;
            foreach (var k in new[] { "InputVer", "PadType", "Fire1Mapping" }) backup(k);
            for (int i = 0; i < 40; i++) { string k = ((ControlMap.K)i).ToString(); backup(k + "PadMapping"); backup(k + "PadAxisMapping"); }
            ControlMap.Push();
            try
            {
                PlayerPrefs.SetInt("InputVer", 0); PlayerPrefs.DeleteKey("PadType"); Load(true);
                Check(ControlMap.JoyType == 3 && Pad(ControlMap.K.Fire1).key == KeyCode.JoystickButton5 && Pad(ControlMap.K.Fire1).axis == -1, "Fresh Android profile did not bind R1");
                Persist(); Load(true); Load(true);
                Check(ControlMap.JoyType == 3 && Pad(ControlMap.K.Fire1).key == KeyCode.JoystickButton5, "Attack lost on subsequent startup");

                PlayerPrefs.SetInt("Fire1Mapping", (int)KeyCode.U);
                Pad(ControlMap.K.Fire1).key = KeyCode.JoystickButton12; Pad(ControlMap.K.Fire1).axis = -1;
                Pad(ControlMap.K.Jump).key = KeyCode.None; Pad(ControlMap.K.Jump).axis = -1; Persist(); Load(true);
                Check(Pad(ControlMap.K.Fire1).key == KeyCode.JoystickButton12 && Pad(ControlMap.K.Jump).key == KeyCode.None && Pad(ControlMap.K.Jump).axis == -1, "Custom/cleared controller bindings lost on startup");
                Check(ControlMap.Name(ControlMap.K.Fire1, 0) == "U", "Keyboard mapping lost on startup");
                Pad(ControlMap.K.Fire1).key = KeyCode.None; Persist(); Load(true);
                Check(Pad(ControlMap.K.Fire1).key == KeyCode.None && Pad(ControlMap.K.Fire1).axis == -1, "Intentional attack clear was overwritten");
                PlayerPrefs.SetInt("InputVer", 0); PlayerPrefs.SetInt("PadType", 0); Load(true);
                Check(ControlMap.JoyType == 0 && Pad(ControlMap.K.Fire1).axis == 5, "Explicit controller profile overwritten");

                ControlMap.Capturing = true;
                var activeAxes = (bool[])typeof(ControlMap).GetField("axCur", Flags).GetValue(null); activeAxes[(int)ControlMap.K.Fire1] = true;
                Load(true); Check(!ControlMap.Capturing && !activeAxes[(int)ControlMap.K.Fire1], "Reload retained stale remap/axis state");
                File.WriteAllText("Logs/gamepad-startup-regression-results.txt", "PASS: controller attack with stale touch pointer over HUD; mouse HUD click suppressed; real modal blocks all sources; fresh Android R1; repeated startup; saved custom/cleared bindings and explicit profile preserved; stale capture/axis state reset.\n");
            }
            finally
            {
                foreach (var entry in saved) { if (entry.Value.HasValue) PlayerPrefs.SetInt(entry.Key, entry.Value.Value); else PlayerPrefs.DeleteKey(entry.Key); }
                PlayerPrefs.Save(); ControlMap.Pop();
            }
        }
        static void CheckSenderLifetime()
        {
            var savedSender = NpcUI.Sender; var savedClient = GameClient.FindSelf;
            var first = new GameObject("OldSessionSender"); var second = new GameObject("NewSessionSender");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var oldClient = first.AddComponent<GameClient>(); var newClient = second.AddComponent<GameClient>();
                var bind = typeof(GameClient).GetMethod("BindSender", flags);
                var destroy = typeof(GameClient).GetMethod("OnDestroy", flags);
                bind.Invoke(oldClient, null); var oldSender = NpcUI.Sender;
                bind.Invoke(newClient, null); var newSender = NpcUI.Sender;
                Check(oldSender != newSender && newSender.Target == newClient && GameClient.FindSelf == newClient, "Scene reload retained previous session sender");
                destroy.Invoke(oldClient, null);
                Check(NpcUI.Sender == newSender && GameClient.FindSelf == newClient, "Old scene teardown cleared new session sender");
                destroy.Invoke(newClient, null);
                Check(NpcUI.Sender == null && GameClient.FindSelf == null, "Destroyed session retained static sender/client references");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second);
                NpcUI.Sender = savedSender; GameClient.FindSelf = savedClient;
            }
        }
    }
}
