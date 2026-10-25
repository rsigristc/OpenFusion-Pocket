using System.Collections.Generic;
using UnityEngine;
using OpenFusion.World;

namespace OpenFusion.Screens
{
    public sealed partial class LauncherScreen
    {
        sealed class NavigationNode
        {
            public string key;
            public Rect rect, viewport;
            public Scroller scroll;
        }
        readonly List<NavigationNode> navigation = new List<NavigationNode>();
        readonly Stack<NavigationNode> navigationScroll = new Stack<NavigationNode>();
        string navigationScope, navigationFocus, navigationActivate;
        bool navigationVisible;
        Vector2 navigationDirection;
        float navigationRepeat;
        TouchScreenKeyboard navKeyboard;
        string navKeyboardName;
        int navigationBackFrame = -1;
        string NavigationScope => page + ":" + modal + ":" + modalToken + ":" + stab + ":" + loginTab + ":" + adTab + ":" + confirmOpen;
        static float NavigationAxis(string name) { try { return UnityEngine.Input.GetAxisRaw(name); } catch { return 0; } }
        void NavigateBack()
        {
            if (navigationBackFrame == Time.frameCount) return;
            navigationBackFrame = Time.frameCount;
            navigationActivate = null;
            if (navKeyboard != null && navKeyboard.active) { navKeyboard.active = false; return; }
            if (confirmOpen) confirmOpen = false;
            else if (modal != Modal.None) CloseModal();
            else if (page == Page.Settings) page = Page.Home;
            else sel = -1;
        }
        void UpdateGamepadNavigation()
        {
            if (!ControlMap.PadActive || ControlMap.Capturing) return;
            if (UnityEngine.Input.touchCount > 0 || UnityEngine.Input.GetMouseButtonDown(0)) navigationVisible = false;
            if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1)) { navigationVisible = true; NavigateBack(); return; }
            if (navKeyboard != null && navKeyboard.active) return;
            int shoulder = (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton5) ? 1 : 0) - (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton4) ? 1 : 0);
            if (shoulder != 0 && page == Page.Settings && modal == Modal.None)
            { stab = (stab + shoulder + TabNames.Length) % TabNames.Length; scrPage.y = scrPage.vel = 0; navigationVisible = true; }
            if (navigationScope != NavigationScope) { navigationActivate = null; return; }
            float x = NavigationAxis("Joy X"), y = NavigationAxis("Joy Y");
            // Android HAT axes avoid controller-specific legacy Unity axis numbering.
            int hat = PocketPlatform.NavigationHat();
            if ((hat & 3) != 0) x = (hat & 1) != 0 ? -1 : 1;
            if ((hat & 12) != 0) y = (hat & 4) != 0 ? -1 : 1;
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) x = -1;
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) x = 1;
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow)) y = -1;
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) y = 1;
            Vector2 direction = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) < .55f ? Vector2.zero :
                Mathf.Abs(x) > Mathf.Abs(y) ? new Vector2(Mathf.Sign(x), 0) : new Vector2(0, Mathf.Sign(y));
            if (direction != Vector2.zero && (direction != navigationDirection || Time.unscaledTime >= navigationRepeat))
            {
                bool first = !navigationVisible; navigationVisible = true;
                if (!first) MoveNavigation(direction);
                navigationRepeat = Time.unscaledTime + (direction != navigationDirection ? .32f : .13f);
            }
            navigationDirection = direction;
            if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                // The first press reveals focus; activation always belongs to the current modal/page.
                if (navigationVisible) navigationActivate = navigationFocus;
                navigationVisible = true;
            }
        }
        void MoveNavigation(Vector2 direction)
        {
            int current = navigation.FindIndex(n => n.key == navigationFocus);
            if (current < 0) return;
            int next = LauncherNavigation.Neighbor(navigation.ConvertAll(n => n.rect), current, direction);
            if (next < 0) return;
            var node = navigation[next]; navigationFocus = node.key;
            if (node.scroll != null)
            {
                float delta = node.rect.yMin < node.viewport.yMin ? node.rect.yMin - node.viewport.yMin :
                    node.rect.yMax > node.viewport.yMax ? node.rect.yMax - node.viewport.yMax : 0;
                node.scroll.y = Mathf.Clamp(node.scroll.y + delta / UiKit.S, 0, node.scroll.maxY); node.scroll.vel = 0;
            }
        }
        void BeginNavigation()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (navigationScope != NavigationScope)
            {
                navigationScope = NavigationScope; navigationFocus = navigationActivate = null;
                if (navKeyboard != null) navKeyboard.active = false;
                navKeyboard = null; navKeyboardName = null;
            }
            navigation.Clear(); navigationScroll.Clear();
        }
        Rect NavigationScreenRect(Rect r)
        {
            // GUIToScreenPoint scales local coordinates but leaves IMGUI group offsets
            // unscaled on Android. Compose our scroll transform explicitly instead.
            Vector2 origin = Vector2.zero;
            if (navigationScroll.Count > 0)
            {
                var parent = navigationScroll.Peek();
                origin = parent.viewport.position - Vector2.up * parent.scroll.y * UiKit.S;
            }
            return new Rect(origin + r.position * UiKit.S, r.size * UiKit.S);
        }
        void NavigationScrollBegin(Scroller scroll, Rect viewport)
        { navigationScroll.Push(new NavigationNode { scroll = scroll, viewport = NavigationScreenRect(viewport) }); }
        void NavigationScrollEnd() { if (navigationScroll.Count > 0) navigationScroll.Pop(); }
        bool NavigateControl(Rect rect, string identity, bool enabled)
        {
            if (!enabled || !GUI.enabled || Event.current.type != EventType.Repaint || navigationScope != NavigationScope) return false;
            var scroll = navigationScroll.Count > 0 ? navigationScroll.Peek() : null;
            string key = identity + ":" + rect.x + ":" + rect.y + ":" + rect.width + ":" + rect.height;
            var node = new NavigationNode { key = key, rect = NavigationScreenRect(rect), scroll = scroll?.scroll, viewport = scroll != null ? scroll.viewport : default(Rect) };
            navigation.Add(node);
            if (navigationActivate != key) return false;
            navigationActivate = null;
            return true;
        }
        void EndNavigation()
        {
            if (Event.current.type != EventType.Repaint) return;
            navigationActivate = null;
            if (navigationFocus == null || !navigation.Exists(n => n.key == navigationFocus))
            {
                var preferred = modal != Modal.None ? navigation.Find(n => n.key.StartsWith("button:Close:") || n.key.StartsWith("button:Cancel:")) :
                    page == Page.Home && Sel() != null ? navigation.Find(n => n.key.StartsWith("server:" + Sel().uuid + ":")) : null;
                navigationFocus = preferred != null ? preferred.key : navigation.Count > 0 ? navigation[0].key : null;
            }
            var node = navigation.Find(n => n.key == navigationFocus);
            if (node == null) { navigationFocus = navigation.Count > 0 ? navigation[0].key : null; return; }
            if (!navigationVisible || navigationScope != NavigationScope) return;
            Rect r = node.rect;
            if (node.scroll != null) r = Rect.MinMaxRect(Mathf.Max(r.xMin,node.viewport.xMin),Mathf.Max(r.yMin,node.viewport.yMin),Mathf.Min(r.xMax,node.viewport.xMax),Mathf.Min(r.yMax,node.viewport.yMax));
            if (r.width <= 0 || r.height <= 0) return;
            var matrix = GUI.matrix; GUI.matrix = Matrix4x4.identity;
            UiKit.Fill(r, new Color(.8f,1f,0f,.12f)); UiKit.Outline(r, new Color(.85f,1f,0f,1), 3);
            GUI.matrix = matrix;
        }
    }
    public static class LauncherNavigation
    {
        // Directional spatial focus: favor aligned controls, with no wrap into unrelated actions.
        public static int Neighbor(List<Rect> nodes, int current, Vector2 direction)
        {
            if (current < 0 || current >= nodes.Count || direction == Vector2.zero) return -1;
            int best = -1; float score = float.PositiveInfinity;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i == current) continue;
                Vector2 delta = nodes[i].center - nodes[current].center;
                float forward = Vector2.Dot(delta, direction);
                if (forward <= 1) continue;
                float cross = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
                float value = forward + cross * 3f;
                if (value < score) { score = value; best = i; }
            }
            return best;
        }
    }
}
