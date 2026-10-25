// Texto flotante con el nombre de la zona (cnDisplayMapName.ReceiveNewMapName / OnOwnGUI del original).
//  Se dispara al entrar al juego y cada vez que cambia el área (dong) del jugador. Elige al azar uno de 8 movimientos (iMovetype 0..7):
//   0/1: cruza la pantalla de izq->der / der->izq a 25 % de alto; 2/3: cae / sube por el centro; 4/5: como 0/1 con zig-zag vertical;
//   6/7: cae / sube con zig-zag horizontal. n = t/fMaxtime; n += sin(2*pi*n)*0.1; avance = n + sin(2*pi*n)*0.15; zig-zag = sin(6*pi*n)*|cos(pi*n)|*0.1.
//  Alfa = min(1,(1-n)*2); sombra negra +1,+1; rect (mapName.Length*16+20, 40), x -= Length*8; escala de UI alrededor del rect. GUI.depth 12.
//  fMaxtime por defecto del script = 2 s (el valor del prefab no se conoce): se usa 3 s.
// SIN COMPILAR EN UNITY.
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class ZoneBanner : MonoBehaviour
    {
        public float MaxTime = 3f;
        NanocomHud nano; string last, text; float t0, readyAt; int move; bool active; BitmapFont font; bool fontTried;
        bool Ready => !WorldBootstrap.Travelling && !WelcomePopup.Active && !(OpenFusion.Screens.LoadingScreen.I != null && OpenFusion.Screens.LoadingScreen.I.Visible) &&
            GameClient.FindSelf != null && GameClient.FindSelf.WorldConnected && GameClient.FindSelf.Bootstrap?.Entities?.Self != null;

        public static ZoneBanner Ensure(GameObject host) { var z = host.GetComponent<ZoneBanner>(); if (z == null) z = host.AddComponent<ZoneBanner>(); return z; }
        public void Init(NanocomHud n) { nano = n; last = null; }
        public void Show(string name, int movement = -1) { if (string.IsNullOrEmpty(name)) return; text = name; t0 = Time.time; move = movement >= 0 ? movement : Random.Range(0, 8); active = true; }

        void Update()
        {
            if(!Ready){active=false;last=null;readyAt=Time.unscaledTime+.5f;return;}
            if(Time.unscaledTime<readyAt)return;
            if (nano != null) { string d = nano.DongName; if (!string.IsNullOrEmpty(d) && d != "unknown" && d != last) { last = d; Show(d); } }
            if (active && Time.time - t0 > MaxTime) active = false;
        }

        void OnGUI()
        {
            var ev = Event.current; if (ev == null || ev.type != EventType.Repaint || !active || string.IsNullOrEmpty(text)) return;
            if (GameMenu.HudHidden || !Ready) return;
            if (!fontTried) { fontTried = true; font = BitmapFontLoader.Load("JEFFE___16__1012"); }
            if (font == null) return;
            float W = Screen.width, H = Screen.height; int len = text.Length;
            float n = (Time.time - t0) / MaxTime; n += Mathf.Sin(n * 2f * 3.14f) * 0.1f;
            float a = Mathf.Min(1f, (1f - n) * 2f); if (a <= 0f) return;
            float adv = n + Mathf.Sin(n * 2f * 3.14f) * 0.15f, zig = Mathf.Sin(n * 6f * 3.14f) * Mathf.Abs(Mathf.Cos(n * 3.14f)) * 0.1f;
            var r = new Rect(0f, 0f, len * 16 + 20, 40f);
            switch (move)
            {
                case 0: r.x = adv * W - len * 8; r.y = H * 0.25f; break;
                case 1: r.x = (1f - adv) * W - len * 8; r.y = H * 0.25f; break;
                case 2: r.x = W * 0.5f - len * 8; r.y = adv * H; break;
                case 3: r.x = W * 0.5f - len * 8; r.y = (1f - adv) * H; break;
                case 4: r.x = adv * W - len * 8; r.y = (0.25f + zig) * H; break;
                case 5: r.x = (1f - adv) * W - len * 8; r.y = (0.25f + zig) * H; break;
                case 6: r.x = (0.5f + zig) * W - len * 8; r.y = adv * H; break;
                default: r.x = (0.5f + zig) * W - len * 8; r.y = (1f - adv) * H; break;
            }
            float sc = GameOptions.UiScale; var om = GUI.matrix; int od = GUI.depth; GUI.depth = 12;
            try
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(r.center.x, r.center.y, 0f), Quaternion.identity, new Vector3(sc, sc, 1f)) * Matrix4x4.TRS(new Vector3(-r.center.x, -r.center.y, 0f), Quaternion.identity, Vector3.one);
                font.Draw(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, TextAnchor.MiddleCenter, false, new Color(0f, 0f, 0f, a), null);
                font.Draw(r, text, TextAnchor.MiddleCenter, false, new Color(1f, 1f, 1f, a), null);
            }
            finally { GUI.matrix = om; GUI.depth = od; }
        }
    }
}
