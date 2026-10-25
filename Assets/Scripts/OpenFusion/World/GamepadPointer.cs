using System.Collections;
using UnityEngine;
using OpenFusion.Screens;
namespace OpenFusion.World
{
    // Real Android UI pointer events preserve existing IMGUI/native actions and dialogs.
    [DefaultExecutionOrder(-500)]
    public sealed class GamepadPointer : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public static bool SuppressWorld => Active || Time.frameCount == toggleFrame || TouchProfileEditor.Active;
        static int toggleFrame = -1;
        bool manual, secondary, pressed, chord; Vector2 point = new Vector2(.5f,.5f);
        public static void Ensure(GameObject host) { if(host.GetComponent<GamepadPointer>()==null)host.AddComponent<GamepadPointer>(); }
        IEnumerator Start() {
            yield return new WaitForSecondsRealtime(5); PocketPlatform.Ready();
#if POCKET_INPUT_PREVIEW
            if(PocketPlatform.Available) Debug.Log("[platform smoke] encrypted credential roundtrip/tamper/delete="+PocketPlatform.SelfTest()+" graphics="+SystemInfo.graphicsDeviceType);
#endif
        }
        static float Axis(string axis) { try { float value=Input.GetAxisRaw(axis);return Mathf.Abs(value)>.2f?value:0; } catch { return 0; } }
        void Update()
        {
            bool combo=Input.GetKey(KeyCode.JoystickButton8)&&Input.GetKey(KeyCode.JoystickButton9);
            if(combo&&!chord&&!ControlMap.Capturing){manual=!manual;toggleFrame=Time.frameCount;}chord=combo;
            if(TutorialIntroScreen.Active){Hide();return;}
            var launcher=FindObjectOfType<LauncherScreen>();
            if(launcher!=null&&launcher.Visible&&!TouchProfileEditor.Active){Hide();return;}
            var client=GameClient.FindSelf;
            bool menu=TutorialTraining.OverlayBlocking||client==null||!client.WorldConnected||GameMenu.Blocking||NpcUI.Active||WelcomePopup.Active||ChatHud.Typing||TouchProfileEditor.Active;
            bool wanted=PocketPlatform.Available&&ControlMap.PadActive&&!ControlMap.Capturing&&(manual||menu);
            if(!wanted){Hide();return;}
            Active=true;
            bool dual=DualScreenController.Instance!=null&&DualScreenController.Instance.Active;
            if(Input.GetKeyDown(KeyCode.JoystickButton4)&&dual) { Release(); secondary=!secondary; }
            if(!dual)secondary=false;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            point.x=Mathf.Clamp(point.x+(Axis("Joy X")+(Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0))*dt*.65f,.005f,.995f);
            point.y=Mathf.Clamp(point.y+(Axis("Joy Y")+(Input.GetKey(KeyCode.DownArrow)?1:0)-(Input.GetKey(KeyCode.UpArrow)?1:0))*dt*.65f,.005f,.995f);
            PocketPlatform.Pointer(secondary,-1,point);
            if(Input.GetKeyDown(KeyCode.JoystickButton0)){pressed=true;PocketPlatform.Pointer(secondary,0,point);}
            else if(pressed&&Input.GetKey(KeyCode.JoystickButton0))PocketPlatform.Pointer(secondary,2,point);
            if(pressed&&Input.GetKeyUp(KeyCode.JoystickButton0)){PocketPlatform.Pointer(secondary,1,point);pressed=false;}
            float scroll=Axis("Joy 4");if(scroll!=0)PocketPlatform.Pointer(secondary,5,point,-scroll*dt*12);
            if(Input.GetKeyDown(KeyCode.JoystickButton1)){Release();if(manual&&!menu){manual=false;Hide();}else PocketPlatform.Back();}
        }
        void Release(){if(pressed)PocketPlatform.Pointer(secondary,3,point);pressed=false;}
        void Hide(){if(Active||pressed)PocketPlatform.Pointer(secondary,4,point);Active=false;pressed=false;}
        void OnApplicationFocus(bool focused){if(!focused)Hide();}
        void OnApplicationPause(bool paused){if(paused)Hide();}
        void OnDisable(){Hide();}
    }
}
