using System;
using UnityEngine;

namespace OpenFusion.World
{
    // Keeps JNI types out of non-Android player assemblies.
    sealed class PocketDualScreenBridge : IDisposable
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass native;
#endif
        public static PocketDualScreenBridge Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var bridge = new PocketDualScreenBridge { native = new AndroidJavaClass("com.openfusion.dualscreen.PocketDualScreen") };
            try {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity")) bridge.native.CallStatic("start", activity);
                return bridge;
            } catch { bridge.Dispose(); throw; }
#else
            return null;
#endif
        }
        public void CallStatic(string method, params object[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            native.CallStatic(method, args);
#endif
        }
        public T CallStatic<T>(string method, params object[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return native.CallStatic<T>(method, args);
#else
            return default;
#endif
        }
        public void Dispose()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            native?.Dispose(); native = null;
#endif
        }
    }
}
