using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class PocketAppIcon
    {
        const string Root = "Assets/Art/AppIcon/";
        public static void Configure()
        {
            string backgroundPath = Root + "FusionSpawnBackground-v1.png";
            if (!File.Exists(backgroundPath))
            {
                var background = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
                try
                {
                    var pixels = new Color32[1024 * 1024];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 20, 47, 255);
                    background.SetPixels32(pixels); background.Apply();
                    File.WriteAllBytes(backgroundPath, background.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(background); }
            }
            var flat = Import(Root + "FusionSpawnIcon-v1.png", false);
            var foreground = Import(Root + "FusionSpawnForeground-v1.png", true);
            var back = Import(backgroundPath, false);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { flat }, IconKind.Any);
            foreach (var kind in new[] { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Adaptive })
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var icon in icons)
                    icon.SetTextures(kind == AndroidPlatformIconKind.Adaptive ? new[] { back, foreground } : new[] { flat });
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
            Debug.Log("[app-icon] Fusion Spawn: default, Android legacy/round and layered adaptive icons configured.");
        }

        static Texture2D Import(string path, bool transparent)
        {
            if (!File.Exists(path)) throw new BuildFailedException("App icon asset missing: " + path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.alphaIsTransparency != transparent || importer.npotScale != TextureImporterNPOTScale.None || importer.maxTextureSize != 2048)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = transparent;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new BuildFailedException("Could not import app icon: " + path);
        }
    }
}
