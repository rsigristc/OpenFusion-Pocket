using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.Screens
{
    // Original cnDexterShipEventScene.SelectTutorial presentation. This does not
    // implement the separate offline training controller or complete its server flag.
    public sealed partial class TutorialIntroScreen : MonoBehaviour
    {
        public static bool Active { get; private set; }
        TaskCompletionSource<bool> completion;
        CharStage stage; CharView view; GameObject group;
        GameObject Dexter, DeeDee, Computress, HologramSound, SirenSound, invEffect;
        ModelFactory models; CutSceneTableScript texts;
        Texture2D background; Material hologram;
        string seenKey, VOSubTitle = "";
        bool active, prepared, previousMute, bEventFlag, bRenderHologram, bCameraShake, bJumpEvent;
        float fScreenAlpha, fShakeSize;
        int generation;
        Vector3 vNpcPosition;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> missingAnimations = new HashSet<string>();
        static readonly string[] Sounds = { "HologramOn", "HologramOff", "Machine_Running", "Dexter_CC01", "Beeping", "Computress_CC01", "Dexter_CCEnginePwr", "DeeDee_Laugh_01", "Engine", "Vibration1", "DeeDee_Laugh02", "Dexter_CCReadings", "DeeDee_Laugh_03", "Dexter_CCLaunch", "DeeDee_CCButton", "Button", "Vibration2", "Warning_Siren", "Dexter_CCDeeDeeNo", "Thunderous_Crash", "TimejumpText_Typed" };
        public static string Key(string server, long uid)
        {
            using(var hash = SHA256.Create()) return "intro.ship.v2." + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(PocketPlatform.Identity("intro", server, uid.ToString(System.Globalization.CultureInfo.InvariantCulture))))).Replace("-", "");
        }
        public static bool Seen(string server, long uid) => PlayerPrefs.GetInt(Key(server, uid), 0) != 0;
        public static bool ShouldPlay(CharInfo character, bool seen) => character?.Appearance != null && !character.NeedsCustomization && character.TutorialFlag == 0 && !seen;
        public static string SceneText(CutSceneTableScript table, int line)
        {
            var scene = table?.m_SceneData?.FirstOrDefault(s => s != null && s.m_iEvent == 1);
            return scene?.m_TextElement?.FirstOrDefault(r => r != null && r.m_iLine == line)?.m_strText ?? "";
        }
        public Task<bool> Run(ModelFactory factory, XdtTableScript tables, string server, long uid)
        {
            if(active) return completion.Task;
            completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            active = Active = true; prepared = false; ++generation;
            models = factory; texts = tables?.m_pCutSceneTable; seenKey = Key(server, uid);
            previousMute = MusicController.LobbyMuted; MusicController.LobbyMuted = true;
            try {
                if(string.IsNullOrEmpty(SceneText(texts, 5))) throw new InvalidOperationException("The introduction subtitle table is missing.");
                if(Sfx.Instance == null) Sfx.Boot(gameObject, factory, null);
                stage = CharStage.Create(null); group = new GameObject("Introduction actors"); group.transform.SetParent(stage.transform, false);
                vNpcPosition = group.transform.position;
                Dexter = Actor("npc_dexter2", "npc_dexter", "npc_dexter2_face_0", Vector3.zero);
                DeeDee = Actor("npc_deedee2", "npc_deedee", "npc_deedee_face_normal", new Vector3(5,-.8f,-2));
                Computress = Actor("npc_computress", "npc_computress", "npc_computress", new Vector3(.5f,-.2f,-.5f));
                var target = new GameObject("Hologram camera target"); target.transform.SetParent(group.transform, false); target.transform.localPosition = Vector3.up;
                view = stage.View(group, 512, 512, 2, 0, target.transform); view.Angle = Vector3.zero;
                background = models.Texture("charactercreationassets/charcreationbg.png");
                if(background == null) throw new InvalidOperationException("The original introduction background is missing.");
                var shader = Shader.Find("OpenFusion/IntroHologram");
                if(shader == null) throw new InvalidOperationException("Introduction shader is missing.");
                hologram = new Material(shader);
                StartCoroutine(TutorialAssets.Guard(PrepareAndPlay(),e=>{Cleanup();completion.TrySetException(e);}));
                Debug.Log("[intro] ship sequence started; no tutorial completion packet sent");
            } catch(Exception e) { Cleanup(); completion.TrySetException(e); }
            return completion.Task;
        }
        GameObject Actor(string model, string main, string sub, Vector3 position)
        {
            var actor = models.Instantiate(new ModelRef { Kind = ModelKind.Npc, Model = "mob/"+model+".kfm", ApplyTextures = true, Tex1 = "texture/"+main+".dds", Tex2 = "texture/"+sub+".dds" }, group.transform);
            if(actor == null) throw new InvalidOperationException("Missing introduction model: "+model);
            WorldEntities.ActivateAll(actor);
            actor.transform.localPosition = position; actor.transform.localRotation = Quaternion.identity;
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
            foreach(var animation in actor.GetComponentsInChildren<Animation>(true)) animation.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach(var child in actor.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = CharStage.Layer;
            foreach(var shadow in actor.GetComponentsInChildren<BlobShadow>(true)) Destroy(shadow);
            EventAni(actor, "stand1"); return actor;
        }
        GameObject SpawnHologramEffect()
        {
            var reference=models.Locate(TutorialAssets.EffectPath(740));
            if(!reference.Ok)throw new InvalidOperationException("The introduction hologram effect is missing.");
            var effect=models.Builder.BuildPrefabAt(reference,group.transform);
            if(effect==null)throw new InvalidOperationException("The introduction hologram effect could not be created.");
            WorldEntities.ActivateAll(effect);effect.transform.position=Dexter.transform.position;effect.transform.rotation=Dexter.transform.rotation;
            foreach(var child in effect.GetComponentsInChildren<Transform>(true))child.gameObject.layer=CharStage.Layer;
            return effect;
        }
        void EventAni(GameObject actor, string name)
        {
            if(actor == null) return;
            foreach(var anim in actor.GetComponentsInChildren<Animation>(true)) foreach(AnimationState state in anim) {
                if(!string.Equals(state.name, name, StringComparison.OrdinalIgnoreCase)) continue;
                state.wrapMode = WrapMode.Loop;
                anim.CrossFade(state.name, .2f); Debug.Log("[intro] animation="+name+" actor="+actor.name); return;
            }
            if(missingAnimations.Add(actor.name+":"+name)) Debug.LogWarning("[intro] missing animation "+actor.name+":"+name);
        }
        IEnumerator PrepareAndPlay()
        {
            int pending = Sounds.Length + 2, token = generation;
            Action<string> load = path => Sfx.Instance.Load(path, clip => { if(!active || token != generation) return; clips[path] = clip; --pending; });
            foreach(var name in Sounds) load("CC Sound/"+name+".wav");
            load("CC Sound/CharacterCreation_Loop.mp3"); load("ui sound/Comm_SlideIn.wav");
            float deadline = Time.realtimeSinceStartup + 25;
            while(pending > 0 && Time.realtimeSinceStartup < deadline) yield return null;
            prepared = true;
            PlayPath("CC Sound/CharacterCreation_Loop.mp3", Sfx.MusicVolume, true);
            yield return TutorialEvent();
            Finish(true);
        }
        GameObject PlayPath(string path, float volume, bool loop)
        {
            if(!active || group == null) return null;
            if(!clips.TryGetValue(path, out var clip) || clip == null) { Debug.LogWarning("[intro] unavailable audio "+path); return null; }
            var go = new GameObject("Intro audio"); go.transform.SetParent(group.transform, false);
            var source = go.AddComponent<AudioSource>(); source.clip = clip; source.volume = volume; source.spatialBlend = 0; source.loop = loop; source.Play();
            if(!loop) Destroy(go, clip.length + .25f); return go;
        }
        void PlaySound(string name) => PlayPath("CC Sound/"+name+".wav", Sfx.SfxVolume, false);
        void PlaySound(string name, string subtitle) { VOSubTitle = subtitle; PlayPath("CC Sound/"+name+".wav", Sfx.VoiceVolume, false); }
        GameObject LoopSound(string name) => PlayPath("CC Sound/"+name+".wav", Sfx.SfxVolume, true);
        void VOSubTitleClear() { VOSubTitle = ""; }
        void Update()
        {
            if(!active) return;
            if(Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1)) { Finish(false); return; }
            if(Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0)) Finish(true);
        }
        void OnGUI()
        {
            if(!active) return;
            int depth = GUI.depth; var matrix = GUI.matrix; var color = GUI.color;
            GUI.depth = -5000; GUI.matrix = Matrix4x4.identity; GUI.color = Color.white;
            try {
                var full = new Rect(0,0,Screen.width,Screen.height); UiKit.Fill(full, Color.black);
                float scale = Screen.height / 681f, side = 512*scale;
                Vector2 shake = bCameraShake ? new Vector2(Mathf.Sin(Time.time*22)*fShakeSize*4,Mathf.Sin(Time.time*17)*fShakeSize*3)*scale : Vector2.zero;
                if(background != null && prepared) GUI.DrawTexture(new Rect(shake.x,shake.y,Screen.width,Screen.height),background,ScaleMode.ScaleAndCrop);
                if(prepared && bRenderHologram && view?.Texture != null && Event.current.type == EventType.Repaint) {
                    hologram.SetFloat("_Opacity", .8f);
                    Graphics.DrawTexture(new Rect((Screen.width-side)*.5f+shake.x,(Screen.height-side)*.5f+shake.y,side,side),view.Texture,hologram);
                }
                if(fScreenAlpha>0) UiKit.Fill(full,new Color(0,0,0,fScreenAlpha));
                float bar = .15673982f*Screen.height; UiKit.Fill(new Rect(0,0,Screen.width,bar),Color.black); UiKit.Fill(new Rect(0,Screen.height-bar,Screen.width,bar),Color.black);
                var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13*scale), alignment = TextAnchor.MiddleCenter, wordWrap = true };
                label.normal.textColor = Color.white;
                GUI.Label(new Rect(16,12*scale,Screen.width-32,35*scale),prepared?"SPACE / A to skip  •  B to return":"Preparing introduction…",label);
                if(!string.IsNullOrEmpty(VOSubTitle)) GUI.Label(new Rect(25,Screen.height-bar+8*scale,Screen.width-50,bar-16*scale),VOSubTitle,label);
                if(bJumpEvent) { label.normal.textColor = new Color(1,0,0,Mathf.Abs(Mathf.Sin(Time.time*6))); GUI.Label(full,"TIMEJUMP ERROR",label); }
                if(GUI.Button(new Rect(Screen.width-145*scale,48*scale,125*scale,38*scale),"Skip intro")) Finish(true);
                if(GUI.Button(new Rect(20*scale,48*scale,125*scale,38*scale),"Back")) Finish(false);
            } finally {GUI.depth=depth;GUI.matrix=matrix;GUI.color=color;}
        }
        void Finish(bool continueGame)
        {
            if(!active) return;
            if(continueGame) { PlayerPrefs.SetInt(seenKey,1); PlayerPrefs.Save(); }
            Debug.Log("[intro] "+(continueGame?"finished/skipped":"cancelled")+"; server tutorial flag unchanged");
            Cleanup(); completion.TrySetResult(continueGame);
        }
        void Cleanup()
        {
            active = Active = false; ++generation; StopAllCoroutines();
            MusicController.LobbyMuted = previousMute;
            if(stage != null) { stage.Clear(); Destroy(stage.gameObject); }
            if(hologram != null) Destroy(hologram);
            stage=null;group=null;view=null;hologram=null;clips.Clear();
        }
        void OnDestroy() { if(active) {Cleanup();completion?.TrySetResult(false);} }
    }
}
