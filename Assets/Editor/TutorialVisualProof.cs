using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenFusion.Screens;
using OpenFusion.Tables;
using OpenFusion.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    // Runs the real cinematic, with real cached assets and no account/network.
    [InitializeOnLoad]
    public static class TutorialVisualProof
    {
        const string Flag = "OpenFusion.TutorialVisualProof";
        static TutorialIntroScreen intro;
        static double started, sampled;
        static float animationTime;
        static TutorialVisualProof() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Proof camera").AddComponent<Camera>().cullingMask = 0;
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Flag, false) || state != PlayModeStateChange.EnteredPlayMode) return;
            try {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath,"../../ffcache/238bd38f-3add-4dc5-a29a-06213461a206"));
                var source = new FfCacheBundleSource(root); var store = new AssetStore(); var index = new AssetIndex();
                foreach (string bundle in new[]{"CharacterCreation.resourceFile","NpcTexture.resourceFile","Effects.resourceFile","Tutorial.resourceFile"})
                    foreach(var file in source.Load(bundle)) if(file.Key != "__info") index.Add(file.Key,store.Add(file.Key,file.Value));
                var models = new ModelFactory(store,index,new DongBuilder(store));
                var tables = TableLoader.Load(source.Load("TableData.resourceFile"));
                var go = new GameObject("Cinematic proof"); Sfx.Boot(go,models,null);
                intro = go.AddComponent<TutorialIntroScreen>();
                _ = intro.Run(models,tables.Xdt,"visual-proof.invalid",-1);
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += Poll;
            } catch(Exception e) { Fail(e); }
        }
        static object Field(string name) => typeof(TutorialIntroScreen).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(intro);
        static void Poll()
        {
            try {
                if(EditorApplication.timeSinceStartup-started > 90) throw new Exception("Cinematic proof timed out");
                if(!(bool)Field("prepared")) return;
                var actor = (GameObject)Field("Dexter");
                var anim = actor.GetComponentsInChildren<Animation>(true).First(a=>a["excellent"]!=null);
                if(sampled == 0) { sampled=EditorApplication.timeSinceStartup; animationTime=anim["excellent"].time; return; }
                if(EditorApplication.timeSinceStartup-sampled < 3) return;
                if(Mathf.Abs(anim["excellent"].time-animationTime)<.1f) throw new Exception("Dexter animation did not advance");
                var view = (CharView)Field("view"); var old = RenderTexture.active; RenderTexture.active=view.Rt;
                var image = new Texture2D(view.Rt.width,view.Rt.height,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();RenderTexture.active=old;
                int opaque=image.GetPixels32().Count(p=>p.a>32);
                Directory.CreateDirectory("Logs/tutorial-audit");
                File.WriteAllBytes("Logs/tutorial-audit/intro-actors.png",image.EncodeToPNG());
                if(opaque<1000) throw new Exception("Cinematic actor render is empty: "+opaque);
                Debug.Log("[cinematic proof] rendered actor pixels="+opaque+" animation time advanced="+(anim["excellent"].time-animationTime));
                SessionState.SetBool(Flag,false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
            } catch(Exception e) { Fail(e); }
        }
        static void Fail(Exception e) { Debug.LogException(e);SessionState.SetBool(Flag,false);EditorApplication.update-=Poll;EditorApplication.Exit(1); }
    }
}
