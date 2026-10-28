using System;
using System.Reflection;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class VariableJumpRegressionChecks
    {
        static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .0001f, message);
        public static void Run()
        {
            bool option = GameOptions.Cur.VariableJump;
            var previous = GameClient.FindSelf;
            var host = new GameObject("Legacy variable jump checks");
            try
            {
                var client = host.AddComponent<GameClient>(); client.enabled = false; client.SendMovement = client.SendJump = false;
                var velocity = typeof(GameClient).GetField("vy", Flags);
                var start = typeof(GameClient).GetMethod("StartManualJump", Flags);
                var step = typeof(GameClient).GetMethod("StepJumpVelocity", Flags);
                var reset = typeof(GameClient).GetMethod("ResetJumpControl", Flags);
                void Begin() { reset.Invoke(client, null); start.Invoke(client, new object[] { 0f }); }
                void Step(float dt, bool held, float now) => step.Invoke(client, new object[] { dt, held, now });
                float V() => (float)velocity.GetValue(client);
                client.JumpVelocity = 5.68f; client.Gravity = 10f; GameOptions.Cur.VariableJump = true;
                Begin(); Near(V(), 8.236f, "Legacy variable jump takeoff must use the 1.45 multiplier.");
                Step(.01f, true, 0); Near(V(), 8.136f, "Takeoff frame must retain normal gravity.");
                Step(.01f, true, .01f); Near(V(), 7.936f, "Legacy air control uses gravity 20 during the initial half-second cooldown.");
                Step(.01f, false, .02f); Near(V(), 1.784f, "Releasing above velocity 3 must quarter upward speed before gravity.");
                velocity.SetValue(client, .5f); Step(.01f, true, .6f); Near(V(), .47f, "Held jump apex uses Legacy's 0.75 threshold and 0.3 gravity scale.");
                velocity.SetValue(client, -100f); Step(.01f, true, .7f); Near(V(), -10f, "Held variable jump retains terminal velocity -10.");
                velocity.SetValue(client, -100f); Step(.01f, false, .7f); Near(V(), -20f, "Released variable jump retains terminal velocity -20.");

                foreach (int fps in new[] { 30, 60, 120 })
                {
                    float Peak(float holdSeconds)
                    {
                        Begin(); float y = 0, peak = 0, dt = 1f / fps;
                        for (int frame = 0; frame < fps * 5; frame++) {
                            float now = frame * dt; Step(dt, now < holdSeconds, now);
                            y += V() * dt; peak = Mathf.Max(peak, y); if (y < 0) break;
                        }
                        return peak;
                    }
                    float tap = Peak(.05f), held = Peak(2);
                    Check(tap > .4f && tap < .7f && held > 1.65f && held < 1.85f && held > tap * 2,
                        "A held press must reach the Legacy high-jump trajectory at " + fps + " FPS, while a tap cuts it short.");
                }
                GameOptions.Cur.VariableJump = false;
                Begin(); Near(V(), 5.68f, "Disabling variable jump must restore the original fixed takeoff.");
                Step(.01f, true, 0); Step(.01f, false, .01f); Near(V(), 5.48f, "Fixed jump must ignore early release.");
                GameOptions.Cur.VariableJump = true; client.Vehicle = 1;
                Begin(); Near(V(), 5.68f, "Vehicle jumps must not receive the variable-jump multiplier.");
                Step(.01f, true, 0); Step(.01f, false, .01f); Near(V(), 5.48f, "Vehicle jumps must ignore release cutting and extra gravity.");
                client.Vehicle = 0;
                reset.Invoke(client, null);
                client.SkillJump(0);
                Step(.01f, true, Time.time); Step(.01f, false, Time.time + .01f);
                Check(V() < 2, "Direct rope/zipline jumps must not inherit Nano impulse protection.");
                client.SkillJump(4, 1.5f); float skillVelocity = V();
                Step(.01f, false, Time.time); Step(.01f, false, Time.time + .01f);
                Near(V(), skillVelocity - .2f, "Nano jump impulses must survive releasing A during Legacy's 1.5-second protection.");
                client.SkillJump(12, 2.5f); skillVelocity = V();
                Step(.01f, false, Time.time); Step(.01f, false, Time.time + 2);
                Near(V(), skillVelocity - .2f, "Jump pads must keep their separate 2.5-second impulse protection.");
                reset.Invoke(client, null);
                Near((float)typeof(GameClient).GetField("jumpExtraGravityUntil", Flags).GetValue(client), 0,
                    "Relocation/death reset must clear a previous jump's temporary gravity protection.");
                Debug.Log("[variable jump regression] Legacy takeoff/release/gravity/apex/cooldown; tap vs hold at 30/60/120 FPS; option, vehicle, Nano and jump-pad exceptions passed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); GameClient.FindSelf = previous; GameOptions.Cur.VariableJump = option; }
        }
    }
}
