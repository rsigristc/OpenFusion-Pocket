using System;
using UnityEngine;

namespace OpenFusion.World
{
    public enum DualScreenPreference { Automatic, SingleScreen, DualScreen }

    // Platform evidence, never a device-name or screen-size heuristic.
    [Serializable]
    public sealed class DualScreenCapabilities
    {
        public bool reliable, fold, flat, separating, physical, touchVerified, available, active;
        public int kind, revision, width, height, primaryWidth, primaryHeight, secondaryWidth, secondaryHeight;
        public float density = 1;
        public string reason = "Android has not confirmed a usable dual-screen configuration.";
    }

    public static class DualScreenPolicy
    {
        public static bool CanUse(DualScreenCapabilities c)
        {
            if (c == null || !c.reliable || !c.available || c.density <= 0 || float.IsNaN(c.density)) return false;
            bool evidence = c.kind == 1 ? c.fold && (c.flat || c.separating) :
                c.kind == 2 && c.physical && c.touchVerified;
            return evidence && c.primaryWidth / c.density >= 480 && c.primaryHeight / c.density >= 270 &&
                c.secondaryWidth / c.density >= 320 && c.secondaryHeight / c.density >= 300 &&
                c.primaryWidth >= c.primaryHeight * 1.2f;
        }

        public static bool ShouldUse(DualScreenPreference preference, DualScreenCapabilities c, bool worldReady, bool modal)
            => preference != DualScreenPreference.SingleScreen && worldReady && !modal && CanUse(c);
    }
}
