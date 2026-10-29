using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace OpenFusion.Screens
{
    // Native symbols are linked only into the iOS player, never Android or Editor.
    public static class PocketIos
    {
        public static bool Available => Application.platform == RuntimePlatform.IPhonePlayer;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int PocketIOS_Save(string identity, string password);
        [DllImport("__Internal")] static extern IntPtr PocketIOS_Load(string identity);
        [DllImport("__Internal")] static extern int PocketIOS_Forget(string identity);
        [DllImport("__Internal")] static extern int PocketIOS_ForgetAll();
        [DllImport("__Internal")] static extern void PocketIOS_Free(IntPtr text);
        [DllImport("__Internal")] static extern void PocketIOS_Share(string report);
        [DllImport("__Internal")] static extern void PocketIOS_ShareFile(string path);
        [DllImport("__Internal")] static extern int PocketIOS_BuildNumber();
#endif
        public static int InstalledBuild
        {
            get {
#if UNITY_IOS && !UNITY_EDITOR
                return PocketIOS_BuildNumber();
#else
                return OpenFusion.Api.PocketRelease.IosBuildCode;
#endif
            }
        }
        public static bool SupportsOs(string minimum)
        {
#if UNITY_IOS && !UNITY_EDITOR
            var installed = OpenFusion.Api.PocketRelease.ParseVersion(UnityEngine.iOS.Device.systemVersion);
            var required = OpenFusion.Api.PocketRelease.ParseVersion(minimum);
            return installed != null && required != null && installed >= required;
#else
            return true;
#endif
        }
        public static void ShareFile(string path)
        {
#if UNITY_IOS && !UNITY_EDITOR
            PocketIOS_ShareFile(path);
#endif
        }
        public static bool Save(string identity, string password)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return !string.IsNullOrEmpty(identity) && password != null && PocketIOS_Save(identity, password) == 0;
#else
            return false;
#endif
        }
        public static string Load(string identity)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (string.IsNullOrEmpty(identity)) return "";
            IntPtr text = PocketIOS_Load(identity);
            try { return text == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(text) ?? ""; }
            finally { if (text != IntPtr.Zero) PocketIOS_Free(text); }
#else
            return "";
#endif
        }
        public static void Forget(string identity)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(identity)) PocketIOS_Forget(identity);
#endif
        }
        public static void ForgetAll()
        {
#if UNITY_IOS && !UNITY_EDITOR
            PocketIOS_ForgetAll();
#endif
        }
        public static void Share(string report)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(report)) PocketIOS_Share(report);
#endif
        }
    }
}
