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
            // Pinned Java callback adapter; no Compose, extra activity or custom Unity player.
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
            doc.Save(manifest);
        }
    }
}
