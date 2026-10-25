using System;
using UnityEngine;
namespace OpenFusion.World
{
    public static partial class TouchLayout
    {
        public const int Joystick = 15, CameraStick = 16, ControlCount = 17;
        [Serializable] public sealed class ButtonSpec { public float x=-1, y=-1, size=1, opacity=.6f; }
        [Serializable] sealed class Geometry { public ButtonSpec[] buttons; public bool fixedJoystick, cameraStick, controllerStyle; }
        [Serializable] sealed class Profile { public int schema=1, mask; public float scale=1; public bool hide, fixedJoystick, cameraStick, controllerStyle; public ButtonSpec[] buttons; }
        static ButtonSpec[] buttons; static bool fixedJoystick, cameraStick, controllerStyle;
        public static bool FixedJoystick { get { Load(); return fixedJoystick; } set { Load(); fixedJoystick=value; } }
        public static bool CameraStickEnabled { get { Load(); return cameraStick; } set { Load(); cameraStick=value; } }
        public static bool ControllerStyle { get { Load(); return controllerStyle; } }
        static void ResetGeometry(){buttons=new ButtonSpec[ControlCount];for(int i=0;i<buttons.Length;i++)buttons[i]=new ButtonSpec();fixedJoystick=cameraStick=controllerStyle=false;}
        static void ReadGeometry(string json){ResetGeometry();if(string.IsNullOrEmpty(json))return;try{var g=JsonUtility.FromJson<Geometry>(json);if(g?.buttons!=null&&(g.buttons.Length==16||g.buttons.Length==ControlCount)){buttons=g.buttons;fixedJoystick=g.fixedJoystick;cameraStick=g.cameraStick;controllerStyle=g.controllerStyle;Validate();}}catch(ArgumentException){}}
        static string GeometryJson()=>JsonUtility.ToJson(new Geometry{buttons=buttons,fixedJoystick=fixedJoystick,cameraStick=cameraStick,controllerStyle=controllerStyle});
        static float Bounded(float value,float min,float max,float fallback)=>float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp(value,min,max);
        static void Validate(){if(buttons.Length==16)Array.Resize(ref buttons,ControlCount);for(int i=0;i<ControlCount;i++){var b=buttons[i]??(buttons[i]=new ButtonSpec());b.x=Bounded(b.x,-1,1,-1);b.y=Bounded(b.y,-1,1,-1);b.size=Bounded(b.size,.55f,1.8f,1);b.opacity=Bounded(b.opacity,.15f,1,.6f);}}
        public static ButtonSpec Spec(int index){Load();return buttons[Mathf.Clamp(index,0,ControlCount-1)];}
        public static float Opacity(int index)=>Spec(index).opacity;
        public static Rect ActionRect(int index,Rect safe,Rect fallback)
        {
            var b=Spec(index);float size=Mathf.Min(fallback.width*b.size,Mathf.Min(safe.width,safe.height));
            var center=b.x>=0&&b.y>=0?new Vector2(safe.x+b.x*safe.width,safe.y+b.y*safe.height):fallback.center;
            return new Rect(Mathf.Clamp(center.x-size/2,safe.x,safe.xMax-size),Mathf.Clamp(center.y-size/2,safe.y,safe.yMax-size),size,size);
        }
        public static void Move(int index,Vector2 center,Rect safe){var b=Spec(index);b.x=Mathf.Clamp01((center.x-safe.x)/Mathf.Max(1,safe.width));b.y=Mathf.Clamp01((center.y-safe.y)/Mathf.Max(1,safe.height));}
        public static string Snapshot(){Load();return JsonUtility.ToJson(new Profile{mask=mask,scale=scale,hide=hideWithPad,buttons=buttons,fixedJoystick=fixedJoystick,cameraStick=cameraStick,controllerStyle=controllerStyle});}
        public static bool Restore(string json){try{var p=JsonUtility.FromJson<Profile>(json);if(p==null||p.schema!=1||(p.buttons==null||(p.buttons.Length!=16&&p.buttons.Length!=ControlCount)))return false;buttons=p.buttons;Validate();mask=p.mask&((1<<Actions.Length)-1);scale=Bounded(p.scale,.65f,1.3f,1);hideWithPad=p.hide;fixedJoystick=p.fixedJoystick;cameraStick=p.cameraStick;controllerStyle=p.controllerStyle;loaded=true;return true;}catch(ArgumentException){return false;}}
        public static void SaveProfile(int slot){if(slot>=1&&slot<=3){PlayerPrefs.SetString("Touch.Profile."+slot,Snapshot());PlayerPrefs.Save();}}
        public static bool LoadProfile(int slot)=>slot>=1&&slot<=3&&Restore(PlayerPrefs.GetString("Touch.Profile."+slot,""));
        public static void ControllerPreset()
        {
            Reset();fixedJoystick=cameraStick=controllerStyle=true;
            Vector2[] positions={new Vector2(.9f,.64f),new Vector2(.84f,.77f),new Vector2(.78f,.64f),new Vector2(.84f,.51f),new Vector2(.94f,.24f),new Vector2(.06f,.24f),new Vector2(.72f,.32f),new Vector2(.42f,.3f),new Vector2(.5f,.3f),new Vector2(.58f,.3f),new Vector2(.68f,.64f),new Vector2(.27f,.68f),new Vector2(.5f,.45f),new Vector2(.37f,.45f),new Vector2(.63f,.45f),new Vector2(.14f,.7f),new Vector2(.65f,.8f)};
            for(int i=0;i<ControlCount;i++){buttons[i].x=positions[i].x;buttons[i].y=positions[i].y;}
            for(int i=0;i<Actions.Length;i++)Enable(i,i<=10||i==12||i==14);
        }
    }
}
