using System;
using System.Reflection;
using OpenFusion.Screens;
using OpenFusion.World;
using UnityEngine;
namespace OpenFusion.EditorTools
{
    public static class PlatformInputRegressionChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        public static void Run()
        {
            string previous=TouchLayout.Snapshot();
            var active=typeof(GamepadPointer).GetField("<Active>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
            bool oldActive=(bool)active.GetValue(null);
            GameObject host=null;
            try {
                Check(!TouchControls.CanShow(false,true,false,false,false)&&!TouchControls.CanShow(true,true,true,false,false)&&!TouchControls.CanShow(true,true,false,true,false)&&!TouchControls.CanShow(true,true,false,false,true)&&TouchControls.CanShow(true,true,false,false,false),"Touch overlay must require an active world and reject launcher/loading/selection.");
                var focusNodes = new System.Collections.Generic.List<Rect> { new Rect(0,0,100,40), new Rect(110,0,100,40), new Rect(0,60,100,40), new Rect(110,60,100,40) };
                Check(LauncherNavigation.Neighbor(focusNodes,0,Vector2.right)==1 && LauncherNavigation.Neighbor(focusNodes,0,new Vector2(0,1))==2,
                    "Launcher focus must select the aligned control with stick / D-pad directions.");
                Check(LauncherNavigation.Neighbor(focusNodes,0,Vector2.left)==-1 && LauncherNavigation.Neighbor(focusNodes,-1,Vector2.right)==-1,
                    "Launcher focus must not wrap into unrelated actions or accept a stale selection.");
                Check(TouchLayout.GeometryIndex(15)==17 && TouchLayout.GeometryIndex(16)==18 && TouchLayout.ActionIndex(15)==-1 && TouchLayout.ActionIndex(17)==15,
                    "Zoom geometry must never overwrite saved joystick or camera-stick positions.");
                Check(TouchLayout.Restore("{\"schema\":1,\"buttons\":[{},{},{},{},{},{},{},{},{},{},{},{},{},{},{},{\"x\":0.2,\"y\":0.8},{\"x\":0.7,\"y\":0.9}]}"), "Existing 17-control profiles must migrate.");
                Check(Mathf.Approximately(TouchLayout.Spec(15).x,.2f) && Mathf.Approximately(TouchLayout.Spec(16).x,.7f) && TouchLayout.Spec(17).x==-1,
                    "Profile migration must preserve both sticks and initialize separate optional zoom buttons.");
                TouchLayout.ControllerPreset();Check(TouchLayout.FixedJoystick,"Controller preset uses a fixed joystick.");
                var safe=new Rect(25,40,600,400);TouchLayout.Move(0,new Vector2(625,440),safe);TouchLayout.Spec(0).size=1.8f;
                var rect=TouchLayout.ActionRect(0,safe,new Rect(0,0,58,58));
                Check(rect.x>=safe.x&&rect.y>=safe.y&&rect.xMax<=safe.xMax&&rect.yMax<=safe.yMax,"Custom buttons must remain inside safe bounds.");
                string custom=TouchLayout.Snapshot();TouchLayout.Push();TouchLayout.Reset();TouchLayout.Pop();
                Check(TouchLayout.Snapshot()==custom,"Apply/Cancel backup must retain button positions, opacity and joystick style.");
                TouchLayout.Reset();Check(TouchLayout.Restore(custom)&&TouchLayout.FixedJoystick,"Saved profile must restore the layout.");
                string before=TouchLayout.Snapshot();Check(!TouchLayout.Restore("{}")&&TouchLayout.Snapshot()==before,"Reject invalid profiles without resetting the user's current layout.");
                string a=PocketPlatform.Identity("server","https://EXAMPLE.com/api/","alice");
                Check(a==PocketPlatform.Identity("server","https://example.com/api","alice")&&a!=PocketPlatform.Identity("server","https://other.example/api","alice")&&a!=PocketPlatform.Identity("server","https://example.com/api","bob"),"Saved credentials must be scoped to canonical server endpoint and account.");
                TouchLayout.ControllerPreset();
                host=new GameObject("Controller layout fixture");var touch=host.AddComponent<TouchControls>();
                var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                typeof(TouchControls).GetMethod("Awake",flags).Invoke(touch,null);
                float height=Screen.height>0?Screen.height:720;
                Func<Vector2,Vector2> screen=p=>new Vector2(p.x,height-p.y);
                var attack=screen(((Rect)typeof(TouchControls).GetMethod("ActionRect",flags).Invoke(touch,new object[]{0})).center);
                var joy=screen(((Rect)typeof(TouchControls).GetProperty("JoyRect",flags).GetValue(touch)).center);
                var camera=screen(((Rect)typeof(TouchControls).GetProperty("CameraStickRect",flags).GetValue(touch)).center);
                Action<Touch[]> frame=t=>typeof(TouchControls).GetMethod("ProcessTouches",flags).Invoke(touch,new object[]{t,true,false});
                frame(new[]{new Touch{fingerId=1,position=attack,phase=TouchPhase.Began},new Touch{fingerId=2,position=joy,phase=TouchPhase.Began},new Touch{fingerId=3,position=camera,phase=TouchPhase.Began}});
                frame(new[]{new Touch{fingerId=1,position=attack,phase=TouchPhase.Stationary},new Touch{fingerId=2,position=joy+Vector2.up*25,phase=TouchPhase.Moved},new Touch{fingerId=3,position=camera+Vector2.right*25,phase=TouchPhase.Moved}});
                Check(TouchControls.Held(ControlMap.K.Fire1)&&TouchControls.Move.y>0,"Controller layout must retain attack while both sticks are in use.");
                frame(new[]{new Touch{fingerId=1,position=attack,phase=TouchPhase.Stationary},new Touch{fingerId=2,position=joy+Vector2.up*25,phase=TouchPhase.Stationary},new Touch{fingerId=3,position=camera,phase=TouchPhase.Ended}});
                Check(!TouchControls.TryTap(out _)&&TouchControls.Held(ControlMap.K.Fire1),"Releasing the camera stick must not select an NPC or interrupt attack.");
                frame(Array.Empty<Touch>());
                active.SetValue(null,false);
                TouchLayout.Enable(15,true);
                var zoom=screen(((Rect)typeof(TouchControls).GetMethod("ActionRect",flags).Invoke(touch,new object[]{15})).center);
                frame(new[]{new Touch{fingerId=5,position=zoom,phase=TouchPhase.Began}});
                Check(TouchControls.Held(ControlMap.K.ZoomIn) && ControlMap.Zoom>0, "An enabled touch zoom button must reach the same camera axis as the keyboard.");
                frame(Array.Empty<Touch>());
                Check(!TouchControls.Held(ControlMap.K.ZoomIn), "Releasing zoom must stop changing camera distance.");
                active.SetValue(null,true);
                Check(ControlMap.Forward==0&&ControlMap.Strafe==0&&ControlMap.CameraX==0&&!ControlMap.Held(ControlMap.K.Fire1)&&!ControlMap.Down(ControlMap.K.Jump),"UI pointer must not simultaneously move, attack or turn the game camera.");
                typeof(TouchControls).GetField("on",flags).SetValue(touch,true);
                var presses=(System.Collections.Generic.HashSet<ControlMap.K>)typeof(TouchControls).GetField("down",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);presses.Add(ControlMap.K.WorldMap);
                Check(!ControlMap.Down(ControlMap.K.WorldMap,false)&&ControlMap.Down(ControlMap.K.WorldMap,false,true),"Window hotkeys must remain available while the UI pointer suppresses gameplay");
                bool registered=GameMenu.Implemented.Contains(GameMenu.Win.Map);GameMenu.Implemented.Add(GameMenu.Win.Map);var menu=host.AddComponent<GameMenu>();
                try{GameMenu.CloseWindow();var hotkey=typeof(GameMenu).GetMethod("Hotkey",flags);hotkey.Invoke(menu,new object[]{ControlMap.K.WorldMap,GameMenu.Win.Map});Check(GameMenu.Current==GameMenu.Win.Map,"Window action must open the map");hotkey.Invoke(menu,new object[]{ControlMap.K.WorldMap,GameMenu.Win.Map});Check(GameMenu.Current==GameMenu.Win.None,"Repeating the same window action must close it despite pointer mode");
                    presses.Clear();presses.Add(ControlMap.K.Esc);GameMenu.SetOpen(true,false);typeof(GameMenu).GetMethod("Update",flags).Invoke(menu,null);Check(!GameMenu.Open,"Repeating the menu action must close the general menu while pointer mode is active");}
                finally{typeof(GameMenu).GetMethod("OnDestroy",flags).Invoke(menu,null);if(!registered)GameMenu.Implemented.Remove(GameMenu.Win.Map);}
                Debug.Log("[platform input regression] strict world overlay; safe layout/profile/rollback; credential scoping; pointer game-input isolation passed.");
            } finally {if(host!=null){typeof(TouchControls).GetMethod("OnDestroy",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(host.GetComponent<TouchControls>(),null);UnityEngine.Object.DestroyImmediate(host);}active.SetValue(null,oldActive);TouchLayout.Restore(previous);}
        }
    }
}

