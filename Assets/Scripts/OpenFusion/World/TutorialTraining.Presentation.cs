using System;
using System.Collections.Generic;
using OpenFusion.Screens;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed partial class TutorialTraining
    {
        public static readonly string[] IllustrationNames = { "tut_mouse", "tut_lmouse", "tut_rmouse", "tut_move", "tut_move_w", "tut_move_s", "tut_move_ad", "tut_jump", "tut_one", "tut_up", "tut_down", "tut_left", "tut_right" };
        readonly Dictionary<string,Texture2D> illustrations = new Dictionary<string,Texture2D>();
        void PresentDialogue(string sound,string text)
        {
            if(string.IsNullOrWhiteSpace(text)) return;
            ChatHud.AddEventString(4,text);
            int split=text.IndexOf(':');
            string speaker=split>0?text.Substring(0,split).Trim():"Computress";
            string message=split>0?text.Substring(split+1).Trim():text;
            bool computress=sound!=null&&sound.StartsWith("Computress",StringComparison.OrdinalIgnoreCase);
            int npc=computress?1171:speaker.IndexOf("Dexter",StringComparison.OrdinalIgnoreCase)>=0?2673:
                speaker.IndexOf("Buttercup",StringComparison.OrdinalIgnoreCase)>=0?2672:
                speaker.IndexOf("Two",StringComparison.OrdinalIgnoreCase)>=0?2671:0;
            NanoMessages.Tutorial(speaker,message,npc,Mathf.Clamp(message.Length/14f,6,20));
        }
        public static string IllustrationFor(Lesson lesson,float elapsed)
        {
            switch(lesson) {
                case Lesson.LookRight:case Lesson.LookLeft:case Lesson.LookUp:case Lesson.LookDown:case Lesson.Target:return "tut_mouse";
                case Lesson.Forward: return elapsed%3<1.5f?"tut_move":"tut_move_w";
                case Lesson.Backward:return "tut_move_s";
                case Lesson.Steering:return "tut_move_ad";
                case Lesson.Ledge:return "tut_move_w";
                case Lesson.Jump:return "tut_jump";
                case Lesson.Attack:case Lesson.ThreeEnemies:case Lesson.Boss:case Lesson.Transmitter:case Lesson.FusionButtercup:
                case Lesson.NumbuhTwo:case Lesson.Buttercup:case Lesson.Dexter:case Lesson.Dexbot:case Lesson.Infection:case Lesson.ExitLair:case Lesson.Escape:return "tut_lmouse";
                case Lesson.Summon:return "tut_one";
                case Lesson.Power:return "tut_rmouse";
                default:return null;
            }
        }
        public static string DirectionFor(Lesson lesson)
        {
            switch(lesson){case Lesson.LookRight:return "tut_right";case Lesson.LookLeft:return "tut_left";case Lesson.LookUp:return "tut_up";case Lesson.LookDown:return "tut_down";default:return null;}
        }
        Texture2D Illustration(string name)
        {
            if(name==null)return null;
            if(!illustrations.TryGetValue(name,out var t)){t=world?.Models?.Texture("tutorialassets/"+name+".png");illustrations[name]=t;}
            return t;
        }
        static string Binding(ControlMap.K key)
        {
            ControlMap.EnsureLoaded();int slot=ControlMap.PadActive?2:0;
            string value=ControlMap.Name(key,slot);if(string.IsNullOrEmpty(value))value=ControlMap.Name(key,1);
            if(value=="Mouse0")return "Left mouse";if(value=="Mouse1")return "Right mouse";
            if(value=="Space")return "Space";
            if(ControlMap.JoyType==3&&value.StartsWith("JoystickButton",StringComparison.Ordinal)&&int.TryParse(value.Substring(14),out int n)){
                string[] names={"A","B","X","Y","L1","R1","Select","Start","L3","R3"};if(n>=0&&n<names.Length)return names[n];
            }
            if(value=="Joy X")return "Left stick";if(value=="Joy Y")return "Left stick";
            if(ControlMap.JoyType==3&&(value=="Joy 3"||value=="Joy 4"))return "Right stick";
            return string.IsNullOrEmpty(value)?"Unassigned (Settings → Controls)":value;
        }
        string InputHint()
        {
            if(Current==Lesson.Arrival)return "Listen to Computress. Training begins shortly.";
            bool touch=TouchControls.Active&&!ControlMap.PadActive;
            if(Current>=Lesson.LookRight&&Current<=Lesson.LookDown||Current==Lesson.Target)return touch?"Drag on the right side to move the camera.":"Camera: "+Binding(ControlMap.K.CameraRight)+" / "+Binding(ControlMap.K.CameraUp)+(ControlMap.PadActive?"":" • Hold right mouse and drag");
            if(Current==Lesson.Jump)return touch?"Move with the joystick and tap JUMP.":"Jump: "+Binding(ControlMap.K.Jump)+" • Move: "+Binding(ControlMap.K.Up);
            if(Current>=Lesson.Forward&&Current<=Lesson.Ledge)return touch?"Use the movement joystick; drag to turn the camera.":"Forward: "+Binding(ControlMap.K.Up)+" • Back: "+Binding(ControlMap.K.Down)+" • Turn the camera while moving.";
            if(Current==Lesson.Summon)return touch?"Tap Nano 1 on the HUD.":"Nano 1: "+Binding(ControlMap.K.Nano1);
            if(Current==Lesson.Power)return touch?"Aim at the enemy, then tap POWER.":"Nano power: "+Binding(ControlMap.K.Fire2);
            if(Current==Lesson.Journal)return touch?"Open the menu, then select Journal.":"Journal: "+Binding(ControlMap.K.Journal);
            return touch?"Aim at the character or target and tap ATK.":"Attack / interact: "+Binding(ControlMap.K.Fire1);
        }
        void DrawPresentation()
        {
            if(!running)return;int depth=GUI.depth;var matrix=GUI.matrix;var color=GUI.color;bool enabledBefore=GUI.enabled;
            GUI.depth=-1000;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
            try {
                var usable=Screen.safeArea;var safe=new Rect(usable.x,Screen.height-usable.yMax,usable.width,usable.height);
                float scale=Mathf.Clamp(safe.height/720f,.65f,2f);
                var style=new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(16*scale),alignment=TextAnchor.MiddleCenter,wordWrap=true};style.normal.textColor=Color.white;
                var rect=new Rect(safe.x+safe.width*.17f,safe.y+safe.height*.22f,safe.width*.66f,56*scale);
                if(!GameMenu.Blocking){UiKit.Fill(rect,new Color(0,.025f,.06f,.72f));GUI.Label(rect,travelling?"Loading training area…":instruction,style);}
                if(!GameMenu.Blocking&&!travelling){
                    string name=IllustrationFor(Current,Time.time-since);var image=Illustration(name);
                    if(image!=null){
                        var picture=new Rect(safe.x+35*scale,safe.y+safe.height*.42f,205*scale,140*scale);
                        GUI.DrawTexture(picture,image,ScaleMode.ScaleToFit,true);
                        var arrow=Illustration(DirectionFor(Current));if(arrow!=null){float wave=Mathf.Sin(Time.unscaledTime*4)*6*scale;var dir=Current==Lesson.LookLeft?Vector2.left:Current==Lesson.LookRight?Vector2.right:Current==Lesson.LookUp?Vector2.up:Vector2.down;var center=picture.center+new Vector2(dir.x,-dir.y)*(85*scale+wave);GUI.DrawTexture(new Rect(center.x-22*scale,center.y-22*scale,44*scale,44*scale),arrow,ScaleMode.ScaleToFit,true);}
                        style.fontSize=Mathf.RoundToInt(11*scale);GUI.Label(new Rect(picture.x,picture.yMax,205*scale,22*scale),"LEGACY KEYBOARD / MOUSE",style);style.fontSize=Mathf.RoundToInt(15*scale);
                    }
                    var controls=new Rect(safe.x+safe.width*.27f,safe.yMax-140*scale,safe.width*.46f,52*scale);
                    UiKit.Fill(controls,new Color(0,.025f,.06f,.72f));GUI.Label(controls,InputHint(),style);
                }
                if(showMarker&&Camera.main!=null&&!GameMenu.Blocking){var p=Camera.main.WorldToScreenPoint(marker);if(p.z>0)GUI.Label(new Rect(p.x-25*scale,Screen.height-p.y-25*scale,50*scale,50*scale),"▼",style);}
                if(GUI.Button(new Rect(safe.center.x-80*scale,safe.yMax-45*scale,160*scale,34*scale),"Skip training…"))confirmSkip=true;
                if(!string.IsNullOrEmpty(failure)) {GUI.Label(new Rect(safe.x+safe.width*.15f,safe.y+safe.height*.4f,safe.width*.7f,100*scale),failure,style);if(GUI.Button(new Rect(safe.x+safe.width*.4f,safe.y+safe.height*.6f,safe.width*.2f,50*scale),"Return to launcher"))Finish(false);}
                if(confirmSkip){var box=new Rect(safe.x+safe.width*.2f,safe.y+safe.height*.35f,safe.width*.6f,180*scale);UiKit.Fill(box,new Color(0,.04f,.08f,.98f));GUI.Label(new Rect(box.x+10,box.y+10,box.width-20,70*scale),"Skip the remaining training and enter the world?",style);if(GUI.Button(new Rect(box.x+20,box.y+95*scale,box.width*.45f,50*scale),"Continue training"))confirmSkip=false;if(GUI.Button(new Rect(box.center.x+10,box.y+95*scale,box.width*.45f-20,50*scale),"Skip training"))Finish(true);}
            }finally{GUI.depth=depth;GUI.matrix=matrix;GUI.color=color;GUI.enabled=enabledBefore;}
        }
    }
}
