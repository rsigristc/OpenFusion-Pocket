using System;
using UnityEngine;
using OpenFusion.Screens;
namespace OpenFusion.World
{
    public sealed class TouchProfileEditor : MonoBehaviour
    {
        public static bool Active {get;private set;}
        static TouchProfileEditor instance;string original,notice="";bool save;Action complete;int selected=-1,drag=-1,slot=1;Vector2 offset;
        public static void Show(bool saveOnDone,Action onDone=null)
        {
            if(Active)return;if(instance==null)instance=new GameObject("Touch layout editor").AddComponent<TouchProfileEditor>();
            instance.original=TouchLayout.Snapshot();instance.save=saveOnDone;instance.complete=onDone;instance.notice="";instance.selected=instance.drag=-1;Active=true;
            TouchControls.Instance?.CancelForSurfaceChange();
        }
        void Close(bool accept){ControlMap.CancelCaptureFrame=Time.frameCount;if(!accept)TouchLayout.Restore(original);else{if(save){TouchLayout.Save();PlayerPrefs.Save();}complete?.Invoke();}Active=false;drag=-1;GUIUtility.hotControl=0;TouchControls.Instance?.CancelForSurfaceChange();}
        void Update(){if(Active&&Input.GetKeyDown(KeyCode.Escape))Close(false);}
        void OnDestroy(){if(instance==this){if(Active)TouchLayout.Restore(original);Active=false;instance=null;}}
        void OnGUI()
        {
            if(!Active)return;var oldMatrix=GUI.matrix;int oldDepth=GUI.depth;var oldColor=GUI.color;GUI.depth=-25000;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);if(scale<=0){GUI.depth=oldDepth;return;}
            try {
                GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),Quaternion.identity,Vector3.one*scale);
                GUI.Box(new Rect(0,0,1280,720),"");
                GUI.Label(new Rect(20,12,1240,28),"TOUCH LAYOUT EDITOR — drag controls; select one to adjust size / opacity");
                if(GUI.Button(new Rect(20,48,130,38),"Classic"))TouchLayout.Reset();
                if(GUI.Button(new Rect(160,48,150,38),"Controller"))TouchLayout.ControllerPreset();
                TouchLayout.FixedJoystick=GUI.Toggle(new Rect(330,55,190,30),TouchLayout.FixedJoystick,"Fixed joystick");
                if(GUI.Button(new Rect(535,48,100,38),"Profile "+slot))slot=slot%3+1;
                if(GUI.Button(new Rect(645,48,135,38),"Save profile")){TouchLayout.SaveProfile(slot);notice="Profile "+slot+" saved.";}
                if(GUI.Button(new Rect(790,48,135,38),"Load profile"))notice=TouchLayout.LoadProfile(slot)?"Profile loaded.":"This profile is empty.";
                if(GUI.Button(new Rect(950,48,140,38),"Cancel")){Close(false);return;}
                if(GUI.Button(new Rect(1100,48,160,38),save?"Save and close":"Done")){Close(true);return;}
                var actual=TouchControls.LayoutArea;float aspect=actual.width/Mathf.Max(1,actual.height);
                float previewWidth=Mathf.Min(1240,490*aspect),previewHeight=Mathf.Min(490,1240/Mathf.Max(.1f,aspect));
                Rect usable=new Rect((1280-previewWidth)/2,100+(490-previewHeight)/2,previewWidth,previewHeight);GUI.Box(usable,"Layout preview");
                var ev=Event.current;
                for(int i=0;i<TouchLayout.ControlCount;i++){
                    var fallback=(i==15||i==16)?new Rect(i==15?100:775,420,100,100):new Rect(usable.xMax-((i%3)+1)*70,usable.yMax-((i/3)+1)*72,58,58);
                    var rect=TouchLayout.ActionRect(i,usable,fallback);
                    if(TouchControls.Instance!=null){var live=TouchControls.Instance.LayoutRect(i);float ratio=usable.width/Mathf.Max(1,actual.width);rect=new Rect(usable.x+(live.x-actual.x)*ratio,usable.y+(live.y-actual.y)*ratio,live.width*ratio,live.height*ratio);}
                    int id=GUIUtility.GetControlID(9000+i,FocusType.Passive);
                    if(ev.type==EventType.MouseDown&&ev.button==0&&rect.Contains(ev.mousePosition)){selected=drag=i;offset=rect.center-ev.mousePosition;GUIUtility.hotControl=id;ev.Use();}
                    if(drag==i&&GUIUtility.hotControl==id&&ev.type==EventType.MouseDrag){TouchLayout.Move(i,ev.mousePosition+offset,usable);ev.Use();}
                    if(drag==i&&ev.type==EventType.MouseUp){drag=-1;GUIUtility.hotControl=0;ev.Use();}
                    var b=TouchLayout.Spec(i);GUI.color=new Color(selected==i?.7f:1,selected==i?1:.8f,1,b.opacity);
                    GUI.Box(rect,TouchLayout.ControlLabel(i));GUI.color=Color.white;
                }
                if(selected>=0){var b=TouchLayout.Spec(selected);GUI.Label(new Rect(20,610,180,25),TouchLayout.ControlLabel(selected));
                    int action=TouchLayout.ActionIndex(selected);
                    if(action>=0)TouchLayout.Enable(action,GUI.Toggle(new Rect(180,610,120,30),TouchLayout.Enabled(action),"Enabled"));
                    else if(selected==16)TouchLayout.CameraStickEnabled=GUI.Toggle(new Rect(180,610,120,30),TouchLayout.CameraStickEnabled,"Enabled");
                    GUI.Label(new Rect(320,610,60,25),"Size");b.size=GUI.HorizontalSlider(new Rect(385,620,240,25),b.size,.55f,1.8f);
                    GUI.Label(new Rect(660,610,80,25),"Opacity");b.opacity=GUI.HorizontalSlider(new Rect(745,620,240,25),b.opacity,.15f,1);
                }
                GUI.Label(new Rect(20,660,1240,42),notice+(save?"  Save and close applies this layout.":"  Use APPLY CHANGE or SAVE AND EXIT in Settings to save the current layout. Saved profile slots persist immediately."));
                if(ev.type==EventType.MouseDown||ev.type==EventType.MouseUp||ev.type==EventType.MouseDrag||ev.type==EventType.ScrollWheel)ev.Use();
            }finally{GUI.matrix=oldMatrix;GUI.depth=oldDepth;GUI.color=oldColor;}
        }
    }
}
