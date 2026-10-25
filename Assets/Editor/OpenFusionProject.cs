// Configuración automática del proyecto, creación de escenas, validación de shaders y compilación a Android.
// Se ejecuta sola la primera vez que se abre el proyecto (menú OpenFusion para repetirla) y desde la línea de comandos (tools/unity-build.ps1).
// SIN COMPILAR NI PROBAR EN UNITY: escrito contra las APIs de UnityEditor 2022.3 / 6 (se comprobaron las firmas en UnityCsReference).
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using OpenFusion.Materials;
using OpenFusion.World;

namespace OpenFusion.EditorTools
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Marker = "ProjectSettings/OpenFusionSetup.done";
        public const string GameScene = "Assets/Scenes/Game.unity";

        static ProjectSetup()
        {
            if (File.Exists(Marker) || Application.isBatchMode) return;     // en lote lo llama cada punto de entrada
            EditorApplication.delayCall += Auto;
        }

        static void Auto()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Auto; return; }
            try { ConfigureAll(); EditorUtility.DisplayDialog("OpenFusion", "Proyecto configurado.\nAbre Assets/Scenes/Game y pulsa Play para iniciar el launcher.", "OK"); }
            catch (Exception e) { Debug.LogError("OpenFusion: la configuración automática falló: " + e); }
        }

        [MenuItem("OpenFusion/1 Configurar proyecto (todo)")]
        public static void ConfigureAll()
        {
            ConfigurePlayer(); ConfigureRenderPipeline(); ConfigureShaders(); CreateScenes();
            File.WriteAllText(Marker, DateTime.UtcNow.ToString("o")); AssetDatabase.SaveAssets();
            Debug.Log("OpenFusion: proyecto configurado.");
        }

        public static void ConfigurePlayer()
        {
            PocketAppIcon.Configure();
            PlayerSettings.companyName = "OpenFusion"; PlayerSettings.productName = "OpenFusion:Pocket";
            PlayerSettings.bundleVersion = OpenFusion.Api.PocketRelease.AppVersion; PlayerSettings.Android.bundleVersionCode = OpenFusion.Api.PocketRelease.BuildCode;
            PlayerSettings.colorSpace = ColorSpace.Gamma;      // el juego original está hecho en gamma
            var a = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(a, "com.openfusion.android");
            PlayerSettings.SetScriptingBackend(a, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(a, ManagedStrippingLevel.Low);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
            Debug.Log("OpenFusion: Android IL2CPP, ARMv7/ARM64, API 23+, OpenGL ES 3, Internet.");
        }

        /// Crea el asset de URP si el proyecto no tiene uno (si se creó desde la plantilla URP, se respeta el que haya).
        public static void ConfigureRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                Directory.CreateDirectory("Assets/Settings");
                const string rdPath = "Assets/Settings/OpenFusion_URP_Renderer.asset", apPath = "Assets/Settings/OpenFusion_URP.asset";
                var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath);
                if (rd == null) { rd = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(rd, rdPath); }
                var ap = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(apPath);
                if (ap == null) { ap = UniversalRenderPipelineAsset.Create(rd); AssetDatabase.CreateAsset(ap, apPath); }
                GraphicsSettings.defaultRenderPipeline = ap;
                int q = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = ap; }
                QualitySettings.SetQualityLevel(q, false);
                // el renderer carga sus shaders por defecto al crearse; se fuerza y se guarda para que entren en el build
                try { var _ = ap.scriptableRenderer; } catch (Exception e) { Debug.LogWarning("OpenFusion: no se pudo crear el renderer de URP al configurar: " + e.Message); }
                EditorUtility.SetDirty(rd); EditorUtility.SetDirty(ap); AssetDatabase.SaveAssets();
                Debug.Log("OpenFusion: creado y asignado " + apPath);
            }
            else Debug.Log("OpenFusion: el proyecto ya tiene render pipeline (" + GraphicsSettings.defaultRenderPipeline.name + "); se respeta.");
        }

        /// Los materiales se crean en tiempo de ejecución con Shader.Find: sin esto los shaders no entran en el APK y todo sale magenta.
        public static void ConfigureShaders()
        {
            var shaders = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Shaders" }).Select(g => AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(g))).Where(s => s != null).ToList();
            foreach (var n in new[] { "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Terrain/Lit", "Hidden/Universal Render Pipeline/Terrain/Lit (Add Pass)", "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)", "Hidden/Universal Render Pipeline/Terrain/Lit (Basemap Gen)" })
            { var s = Shader.Find(n); if (s != null) shaders.Add(s); else Debug.LogWarning("OpenFusion: no se encontró el shader " + n); }
            var gs = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset"));
            var arr = gs.FindProperty("m_AlwaysIncludedShaders"); int added = 0;
            // Lit y Simple Lit no se usan con Shader.Find. Always Included impide
            // eliminar sus cientos de miles de variantes y puede agotar el editor.
            for (int i = arr.arraySize - 1; i >= 0; i--)
            {
                var shader = arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader == null || (shader.name != "Universal Render Pipeline/Lit" && shader.name != "Universal Render Pipeline/Simple Lit")) continue;
                arr.GetArrayElementAtIndex(i).objectReferenceValue = null;
                arr.DeleteArrayElementAtIndex(i);
            }
            foreach (var s in shaders.Distinct())
            {
                bool has = false; for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == s) { has = true; break; }
                if (has) continue; arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = s; added++;
            }
            // Game no contiene terrenos estaticos: sus variantes instanciadas se
            // necesitan cuando DongLoader crea el Terrain tras descargar el mapa.
            gs.FindProperty("m_FogStripping").intValue = 1;
            gs.FindProperty("m_FogKeepLinear").boolValue = true; gs.FindProperty("m_FogKeepExp").boolValue = true; gs.FindProperty("m_FogKeepExp2").boolValue = true;
            gs.FindProperty("m_InstancingStripping").intValue = 2; // Keep All
            gs.ApplyModifiedProperties(); AssetDatabase.SaveAssets();
            Debug.Log("OpenFusion: " + added + " shaders añadidos a Always Included Shaders (" + shaders.Count + " candidatos).");
        }

        [MenuItem("OpenFusion/2 Preparar escena Game")]
        public static void CreateScenes()
        {
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(GameScene)) BuildScene(GameScene, true, new Vector3(6912f, 0f, 1792f));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScene, true) };
            Debug.Log("OpenFusion: Game es la unica escena de arranque.");
        }

        static void BuildScene(string path, bool withClient, Vector3 start)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.7f, 0.9f); cam.farClipPlane = 2500f; cam.nearClipPlane = 0.3f;
            camGo.transform.position = start + new Vector3(0f, 60f, -60f); camGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var player = new GameObject("Player"); player.transform.position = start;
            var game = new GameObject("Game");
            var loader = game.AddComponent<DongLoader>(); var boot = game.AddComponent<WorldBootstrap>(); var light = game.AddComponent<OpenFusionLighting>();
            loader.Cam = cam; loader.Player = player.transform; light.MainLight = sun;
            boot.Loader = loader; boot.Player = player.transform; boot.StartPosition = start; boot.AutoStart = false;
            var oc = camGo.AddComponent<OpenFusionCamera>(); oc.Bootstrap = boot;
            var gc = game.AddComponent<GameClient>(); gc.Bootstrap = boot; gc.enabled = withClient; gc.UseScreens = true;
            EditorSceneManager.SaveScene(scene, path);
        }

        [MenuItem("OpenFusion/3 Validar shaders")]
        public static void ValidateShaders() => Validation.Run();
    }

    public static class Validation
    {
        /// Escribe Logs/openfusion-shader-report.txt con los mensajes del compilador de cada shader de Assets/Shaders. Devuelve el número de errores.
        public static int Run()
        {
            AssetDatabase.Refresh(); var sb = new StringBuilder(); int errors = 0, warnings = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Shaders" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g); var sh = AssetDatabase.LoadAssetAtPath<Shader>(path); if (sh == null) { sb.AppendLine("NO SE PUDO CARGAR " + path); errors++; continue; }
                var msgs = ShaderUtil.GetShaderMessages(sh); int e = msgs.Count(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
                errors += e; warnings += msgs.Length - e;
                sb.AppendLine((e > 0 ? "ERROR  " : msgs.Length > 0 ? "AVISO  " : "OK     ") + sh.name + "  (" + path + ")  soportado=" + sh.isSupported);
                foreach (var m in msgs) sb.AppendLine("    " + m.severity + " línea " + m.line + " [" + m.platform + "]: " + m.message.Trim());
            }
            sb.AppendLine().AppendLine("Errores: " + errors + ", avisos: " + warnings);
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/openfusion-shader-report.txt", sb.ToString());
            Debug.Log(sb.ToString()); return errors;
        }

        /// Compilación rápida para iterar: IL2CPP incremental (reutiliza Library/Bee e Il2cppBuildCache entre compilaciones), configuración Debug del compilador C++,
        /// generación OptimizeSize (menos código C++ que compilar), LZ4 y sin reconfigurar el proyecto. La primera vez tarda como siempre; las siguientes sólo recompilan lo cambiado.
        [MenuItem("OpenFusion/5 Compilar APK RAPIDO (iterar)")]
        public static void AndroidFastMenu() { var ok = BuildFast("Builds/OpenFusion-fast.apk"); if (ok) EditorUtility.RevealInFinder("Builds/OpenFusion-fast.apk"); }
        public static bool BuildFast(string output)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)) { Debug.LogError("Falta Android Build Support."); return false; }
            ProjectSetup.ConfigurePlayer();
            ProjectSetup.ConfigureShaders();
            ProjectSetup.CreateScenes();
            var nb = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(nb, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.SetIl2CppCompilerConfiguration(nb, Il2CppCompilerConfiguration.Debug);
            PlayerSettings.SetIl2CppCodeGeneration(nb, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);

            EditorUserBuildSettings.buildAppBundle = false; EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ETC2;
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds");
            var opts = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(sc => sc.enabled).Select(sc => sc.path).ToArray(), locationPathName = output,
                target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.CompressWithLz4
            };
            var t0 = DateTime.Now; var report = BuildPipeline.BuildPlayer(opts); var sum = report.summary;
            Debug.Log("OpenFusion: compilación RAPIDA " + sum.result + " en " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " s, " + (sum.totalSize / 1048576) + " MB → " + output);
            return sum.result == BuildResult.Succeeded;
        }

        /// Punto de entrada en lote: Unity -batchmode -executeMethod OpenFusion.EditorTools.Validation.Batch
        public static void Batch() { ProjectSetup.ConfigureAll(); int e = Run(); EditorApplication.Exit(e == 0 ? 0 : 3); }
        /// Si se llega aquí, todo el código de Assets compiló: Unity en lote no ejecuta métodos cuando hay errores de compilación.
        public static void CompileCheck() { Debug.Log("OpenFusion: compilación OK"); EditorApplication.Exit(0); }
    }

    public static class BuildScript
    {
        /// APK compatible: IL2CPP Release, ARMv7/ARM64, ETC2, sin simbolos de depuracion.
        [MenuItem("OpenFusion/6 Compilar APK RELEASE (normal)")]
        public static void AndroidReleaseMenu()
        {
            var nb = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(nb, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.SetIl2CppCompilerConfiguration(nb, Il2CppCompilerConfiguration.Release);   // el APK RAPIDO deja la configuracion en Debug: aqui se restaura
            PlayerSettings.SetIl2CppCodeGeneration(nb, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ETC2;
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Disabled;
            EditorUserBuildSettings.development = false;
            var ok = Build(false, "Builds/OpenFusion-release.apk"); if (ok) EditorUtility.RevealInFinder("Builds/OpenFusion-release.apk");
        }

        [MenuItem("OpenFusion/4 Compilar APK de Android (debug)")]
        public static void AndroidDebugMenu() { var ok = Build(true, "Builds/OpenFusion-debug.apk"); if (ok) EditorUtility.RevealInFinder("Builds/OpenFusion-debug.apk"); }

        /// Punto de entrada en lote: Unity -batchmode -executeMethod OpenFusion.EditorTools.BuildScript.AndroidDebug
        public static void AndroidDebug() { bool ok = false; try { ok = Build(true, "Builds/OpenFusion-debug.apk"); } catch (Exception e) { Debug.LogError(e); } EditorApplication.Exit(ok ? 0 : 1); }
        public static void AndroidRelease() { bool ok = false; try { ok = Build(false, "Builds/OpenFusion-release.apk"); } catch (Exception e) { Debug.LogError(e); } EditorApplication.Exit(ok ? 0 : 1); }

        public static bool Build(bool development, string output, string[] extraDefines = null)
        {
            // Compilar no debe regenerar ni sobrescribir escenas existentes.
            ProjectSetup.ConfigurePlayer();
            ProjectSetup.ConfigureRenderPipeline();
            ProjectSetup.ConfigureShaders();
            ProjectSetup.CreateScenes();
            if (!EditorBuildSettings.scenes.Any(s => s.enabled && File.Exists(s.path)))
                throw new BuildFailedException("No hay escenas habilitadas. Usa OpenFusion/2 Crear escenas o configura Build Settings.");
            var nb = NamedBuildTarget.Android;
            PlayerSettings.SetIl2CppCompilerConfiguration(nb, development ? Il2CppCompilerConfiguration.Debug : Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(nb, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ETC2;
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Disabled;
            EditorUserBuildSettings.development = development;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            { Debug.LogError("Falta el módulo Android Build Support (Unity Hub → Installs → tu versión → Add modules → Android Build Support + SDK/NDK/OpenJDK)."); return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds");
            EditorUserBuildSettings.buildAppBundle = false;
            var opts = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), locationPathName = output, extraScriptingDefines = extraDefines ?? new string[0],
                target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.CompressWithLz4 | (development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None)
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary; Debug.Log("OpenFusion: compilación " + sum.result + ", " + (sum.totalSize / 1048576) + " MB, " + sum.totalErrors + " errores, " + sum.totalWarnings + " avisos → " + output);
            if (sum.result == BuildResult.Succeeded) UpdaterRegressionChecks.VerifyApk(output);
            return sum.result == BuildResult.Succeeded;
        }
    }
}
