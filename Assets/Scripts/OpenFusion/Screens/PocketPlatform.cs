using System;
using UnityEngine;
namespace OpenFusion.Screens
{
    public static class PocketPlatform
    {
        public static bool Available => Application.platform == RuntimePlatform.Android;
        public static string Identity(string server, string endpoint, string username)
        {
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) endpoint = uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped).TrimEnd('/');
            return (server ?? "") + "\n" + (endpoint ?? "") + "\n" + (username ?? "");
        }
        static T Call<T>(string method, params object[] args)
        {
            if (!Available) return default;
            try { using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer")) using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity")) using(var bridge=new AndroidJavaClass("com.openfusion.dualscreen.PocketPlatform")) {
                var all=new object[args.Length+1];all[0]=activity;Array.Copy(args,0,all,1,args.Length);
                if(typeof(T)==typeof(bool))return (T)(object)bridge.CallStatic<bool>(method,all);
                if(typeof(T)==typeof(string))return (T)(object)bridge.CallStatic<string>(method,all);
                bridge.CallStatic(method,all);return default;
            }} catch(Exception) { return default; } // Never include arguments or account data in diagnostics.
        }
        public static bool Save(string identity,string password)=>Call<bool>("save",identity,password);
        public static string Load(string identity)=>Call<string>("load",identity) ?? "";
        public static void Forget(string identity)=>Call<int>("forget",identity);
        public static void ForgetAll()=>Call<int>("forgetAll");
        public static void RendererSettings()=>Call<int>("rendererSettings");
        public static bool SelfTest()=>Call<bool>("selfTest");
        public static void Ready()=>Call<int>("ready");
        public static void Pointer(bool secondary,int action,Vector2 point,float scroll=0)=>Call<int>("pointer",secondary,action,point.x,point.y,scroll);
        public static int NavigationHat()
        {
            if (!Available) return 0;
            try { using (var bridge = new AndroidJavaClass("com.openfusion.dualscreen.PocketPlatform")) return bridge.CallStatic<int>("navigationHat"); }
            catch (Exception) { return 0; }
        }
        public static void Back()=>Call<int>("back");
        public static void ShareDiagnostics(string report)=>Call<int>("shareDiagnostics", report);
    }
}
