using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using OpenFusion.World;
using OpenFusion.Net;

namespace OpenFusion.EditorTools
{
    public static class InputRegressionChecks
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static Touch Finger(int id, Vector2 position, TouchPhase phase) { return new Touch { fingerId = id, position = position, phase = phase }; }
        public static void Run()
        {
            var results = new List<string>(); GameObject host = null;
            ControlMap.Push();
            try
            {
                var scroll = new OpenFusion.Screens.ScrollMotion(); scroll.Bounds(2000, 500);
                scroll.Begin(new Vector2(100, 400), 0); scroll.Drag(new Vector2(100, 200), .1f); scroll.End(.12f);
                Require(scroll.y == 200, "Touch drag should advance the scroll offset.");
                for (int i = 0; i < 3; i++) { scroll.Bounds(2000, 500); scroll.Tick(.016f); }
                Require(scroll.y >= 200 && scroll.y <= scroll.maxY, "Layout/repaint and inertia must not reset scroll to the top.");
                results.Add("PASS: touch scroll offset survives repeated layout bounds and release inertia.");
                ControlMap.ResetMappings();
                var oldJump = ControlMap.Name(ControlMap.K.Jump, 0);
                Require(!ControlMap.Assign(ControlMap.K.Jump, 0, KeyCode.A), "Duplicate must require confirmation.");
                Require(ControlMap.SameKey && ControlMap.Name(ControlMap.K.Jump, 0) == oldJump && ControlMap.Name(ControlMap.K.Left, 0) == "A", "Conflict changed mappings prematurely.");
                ControlMap.CancelRemap();
                Require(ControlMap.Name(ControlMap.K.Jump, 0) == oldJump, "Cancel must preserve mapping.");
                ControlMap.Assign(ControlMap.K.Jump, 0, KeyCode.A);
                Require(ControlMap.ReplacePending(ControlMap.K.Jump, 0), "Replace failed.");
                Require(ControlMap.Name(ControlMap.K.Jump, 0) == "A" && ControlMap.Name(ControlMap.K.Left, 0) == "", "Replace must clear the old owner.");
                Require(ControlMap.Assign(ControlMap.K.Jump, 0, KeyCode.A), "Assigning the same cell must be idempotent.");
                ControlMap.Assign(ControlMap.K.Jump, 1, KeyCode.U, replace: true);
                ControlMap.Clear(ControlMap.K.Jump, 0);
                Require(ControlMap.Name(ControlMap.K.Jump, 0) == "" && ControlMap.Name(ControlMap.K.Jump, 1) == "U", "Clear must only clear the selected cell.");
                results.Add("PASS: conflict confirmation, cancel without changes, replacement, same binding, clearing one slot.");
                Require(!ControlMap.Assign(ControlMap.K.Jump, 2, KeyCode.None, 3), "Shared axis conflict must require confirmation.");
                ControlMap.ReplacePending(ControlMap.K.Jump, 2);
                Require(ControlMap.Name(ControlMap.K.Left, 2) == "" && ControlMap.Name(ControlMap.K.Right, 2) == "", "Replacing axis must clear both previous directions.");
                ControlMap.Clear(ControlMap.K.Jump, 2);
                Require(ControlMap.Name(ControlMap.K.Jump, 2) == "", "Clear must remove axes too.");
                results.Add("PASS: shared controller axis conflicts and clearing axes.");
                Require(!ControlMap.HasGamepad(new[] { "", "  ", null }) && ControlMap.HasGamepad(new[] { "", "Android Gamepad" }), "Disconnected slots must not count as controllers.");
                results.Add("PASS: empty joystick slots ignored; named gamepads detected.");

                TouchLayout.Reset(); host = new GameObject("InputRegression"); var touch = host.AddComponent<TouchControls>();
                typeof(TouchControls).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(touch, null);
                var step = typeof(TouchControls).GetMethod("ProcessTouches", BindingFlags.NonPublic | BindingFlags.Instance);
                var actionRect = typeof(TouchControls).GetMethod("ActionRect", BindingFlags.NonPublic | BindingFlags.Instance);
                Action<Touch[], bool, bool> frame = (f, active, blocked) => step.Invoke(touch, new object[] { f, active, blocked });
                Func<int, Vector2> button = i => { var r = (Rect)actionRect.Invoke(touch, new object[] { i }); return new Vector2(r.center.x, (Screen.height > 0 ? Screen.height : 720f) - r.center.y); };
                var attack = button(0); var jump = button(1);
                var joy = new Vector2((Screen.width > 0 ? Screen.width : 1280f) * .15f, (Screen.height > 0 ? Screen.height : 720f) * .3f);
                var look = new Vector2((Screen.width > 0 ? Screen.width : 1280f) * .5f, (Screen.height > 0 ? Screen.height : 720f) * .7f);
                frame(new[] { Finger(3, attack, TouchPhase.Began), Finger(7, joy, TouchPhase.Began), Finger(11, look, TouchPhase.Began) }, true, false);
                Require(TouchControls.Down(ControlMap.K.Fire1), "Attack must begin on its own finger. Screen=" + Screen.width + "x" + Screen.height + "; rect=" + actionRect.Invoke(touch, new object[] { 0 }));
                frame(new[] { Finger(11, look + Vector2.right * 80, TouchPhase.Moved), Finger(3, attack, TouchPhase.Stationary), Finger(7, joy + Vector2.up * 80, TouchPhase.Moved) }, true, false);
                Require(TouchControls.Held(ControlMap.K.Fire1) && !TouchControls.Down(ControlMap.K.Fire1) && TouchControls.Move.y > 0 && TouchControls.Look.x > 0, "Attack, movement and look must coexist regardless of touch order.");
                frame(new[] { Finger(3, attack, TouchPhase.Stationary), Finger(15, jump, TouchPhase.Began) }, true, false);
                Require(TouchControls.Held(ControlMap.K.Fire1) && TouchControls.Down(ControlMap.K.Jump), "Jump must not release attack.");
                frame(new[] { Finger(3, attack, TouchPhase.Canceled), Finger(15, jump, TouchPhase.Stationary) }, true, false);
                Require(!TouchControls.Held(ControlMap.K.Fire1) && TouchControls.Up(ControlMap.K.Fire1) && TouchControls.Held(ControlMap.K.Jump), "Cancel must only release its own finger.");
                frame(Array.Empty<Touch>(), false, false);
                Require(!TouchControls.Active && !TouchControls.Held(ControlMap.K.Jump) && TouchControls.Move == Vector2.zero, "Connecting controller must reset touch state.");
                frame(new[] { Finger(3, attack, TouchPhase.Began) }, true, false);
                Require(TouchControls.Down(ControlMap.K.Fire1), "Touch must resume after controller disconnect.");
                frame(Array.Empty<Touch>(), true, true);
                Require(!TouchControls.Held(ControlMap.K.Fire1), "Opening a modal must reset held actions.");
                results.Add("PASS: attack + movement + camera; independent jump; cancellation; gamepad connect/disconnect; modal reset.");
                var nano = ClientPackets.ChargeNano(123); var weapon = ClientPackets.ToggleWeaponBoost(123);
                Require(nano.Length == 8 && BitConverter.ToInt32(nano, 4) == 123 && weapon.Length == 16 && BitConverter.ToInt32(weapon, 8) == 8, "Boost packet layout changed.");
                results.Add("PASS: nano charge and weapon boost packet layouts match original client.");
            }
            finally
            {
                if (host != null) { typeof(TouchControls).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(host.GetComponent<TouchControls>(), null); UnityEngine.Object.DestroyImmediate(host); }
                ControlMap.Pop(); ControlMap.CancelRemap();
                Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/input-regression-results.txt", results);
            }
            Debug.Log("OpenFusion: regresiones de controles OK\n" + string.Join("\n", results));
        }
    }
}
