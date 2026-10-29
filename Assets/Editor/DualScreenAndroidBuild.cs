#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace OpenFusion.EditorTools
{
    public sealed class DualScreenAndroidBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 20;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // Pinned Java callback adapter and startup-only graphics preference.
            var module = Path.Combine(path, "PocketDualScreen.androidlib", "build.gradle");
            if (!File.Exists(module)) throw new FileNotFoundException("Dual Screen Android module was not exported", module);
            File.AppendAllText(module, "\ndependencies {\n implementation 'androidx.core:core:1.9.0'\n implementation 'androidx.window:window:1.2.0'\n implementation 'androidx.window:window-java:1.2.0'\n}\n");
            var properties = Path.Combine(Path.GetDirectoryName(path), "gradle.properties");
            File.AppendAllText(properties, "\nandroid.useAndroidX=true\n");
            var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            var doc = new XmlDocument(); doc.Load(manifest);
            foreach (XmlElement activity in doc.GetElementsByTagName("activity"))
                if (activity.GetAttribute("name", "http://schemas.android.com/apk/res/android") == "com.unity3d.player.UnityPlayerActivity")
                    activity.SetAttribute("resizeableActivity", "http://schemas.android.com/apk/res/android", "true");
            foreach (XmlElement activity in doc.GetElementsByTagName("activity"))
                if (activity.GetAttribute("name", "http://schemas.android.com/apk/res/android") == "com.unity3d.player.UnityPlayerActivity" ||
                    activity.GetAttribute("name", "http://schemas.android.com/apk/res/android") == "com.openfusion.platform.PocketActivity")
                {
                    activity.SetAttribute("name", "http://schemas.android.com/apk/res/android", "com.openfusion.platform.PocketActivity");
                    activity.SetAttribute("launchMode", "http://schemas.android.com/apk/res/android", "singleTask");
                    activity.SetAttribute("exported", "http://schemas.android.com/apk/res/android", "true");
                    var filter = doc.CreateElement("intent-filter");
                    foreach (var item in new[] { "action:android.intent.action.VIEW", "category:android.intent.category.DEFAULT", "category:android.intent.category.BROWSABLE" })
                    {
                        int split = item.IndexOf(':'); var element = doc.CreateElement(item.Substring(0, split));
                        element.SetAttribute("name", "http://schemas.android.com/apk/res/android", item.Substring(split + 1)); filter.AppendChild(element);
                    }
                    var data = doc.CreateElement("data");
                    data.SetAttribute("scheme", "http://schemas.android.com/apk/res/android", "ofl");
                    data.SetAttribute("host", "http://schemas.android.com/apk/res/android", "server"); filter.AppendChild(data);
                    bool hasLink = false;
                    foreach (XmlElement existing in activity.GetElementsByTagName("data"))
                        if (existing.GetAttribute("scheme", "http://schemas.android.com/apk/res/android") == "ofl") hasLink = true;
                    if (!hasLink) activity.AppendChild(filter);
                }
            var activityPath=Path.Combine(path,"src","main","java","com","openfusion","platform");
            Directory.CreateDirectory(activityPath);
            File.WriteAllText(Path.Combine(activityPath,"PocketActivity.java"), @"package com.openfusion.platform;
public class PocketActivity extends com.unity3d.player.UnityPlayerActivity {
    @Override public boolean dispatchTouchEvent(android.view.MotionEvent event) {
        com.openfusion.dualscreen.PocketPlatform.physicalTouch(event);
        return super.dispatchTouchEvent(event);
    }
    @Override public boolean dispatchGenericMotionEvent(android.view.MotionEvent event) {
        com.openfusion.dualscreen.PocketPlatform.navigationMotion(event);
        return super.dispatchGenericMotionEvent(event);
    }
    @Override public void onWindowFocusChanged(boolean focused) {
        if(!focused) com.openfusion.dualscreen.PocketPlatform.resetNavigation();
        super.onWindowFocusChanged(focused);
    }
    @Override protected String updateUnityCommandLineArguments(String arguments) {
        android.content.SharedPreferences prefs=getSharedPreferences(""pocket.renderer"",0);
        boolean vulkan=prefs.getBoolean(""vulkan"",false)&&!prefs.getBoolean(""starting"",false)&&getPackageManager().hasSystemFeature(""android.hardware.vulkan.version"");
        prefs.edit().putBoolean(""vulkan"",vulkan).putBoolean(""starting"",vulkan).commit();
        String base=arguments==null?"""":arguments.replace(""-force-vulkan"","""").replace(""-force-gles30"","""");
        return base+(vulkan?"" -force-vulkan"":"""");
    }
}");
            doc.Save(manifest);
        }
    }
}
#endif
