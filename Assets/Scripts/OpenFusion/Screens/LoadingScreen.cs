// Pantalla de carga 1:1 con AssetLoader del cliente FusionFall (FullScreen / MiniScreen). IMGUI, escala = Screen.height/681. SIN COMPILAR EN UNITY.
// FullScreen (pivote Center): ventana 509x309 (LoadingWindow) con el tip, dos barras 448x17 (LoadingBar, ancho 15+448*p) y dos textos. MiniScreen (pivote TopRight): Rect(-160,361,150,30).
using UnityEngine;
using OpenFusion.World;

namespace OpenFusion.Screens
{
    public sealed class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen I;
        public bool Visible;
        /// <summary>true = mini barra arriba a la derecha (MiniScreen del original) en vez de la pantalla completa.</summary>
        public bool Mini { get; set; }

        string stepText = "LOADING", detail = ""; float current, total;   // current = barra superior, total = barra inferior (0..1)
        string tip = ""; WorldBootstrap bound;
        string failure; System.Action backToLauncher;
        BitmapFont fTip, fSmall; bool fontsTried;

        // Los 18 consejos del TextAsset "GameTips" del cliente original (inglés).
        static readonly string[] Tips =
        {
            "Always keep an eye on your active Nano's stamina. If your Nano gets tired, it will need to rest for a little while.",
            "Keyboard shortcuts are useful! Press \"I\" to access your pack, \"P\" to access your mail and \"M\" to access the map.",
            "Remember: shirts, pants and shoes are worn for protection. Items for your head, face and back are worn for fun!",
            "Buy Power Items from Dexbot shopkeepers if you want to change your Nano's active power.",
            "There are four different kinds of C.R.A.T.E.s. Standard, Special, Sooper and Sooper Dooper.",
            "Defeating Fuse's monsters isn't the only way to collect Fusion Matter! Try running a race inside an Infected Zone.",
            "Feed gumballs to your Nanos to make them temporarily more powerful!",
            "Not sure where to go next? Check your email. Your guide will often send you helpful messages.",
            "Remember: Adaptium beats Blastons, Blastons beat Cosmix, and Cosmix beats Adaptium.",
            "If your Nano is low on stamina, hit the \"C\" key to use a Nano Potion.",
            "If you want to swap out your Nanos or change their powers, visit a Nano Station!",
            "Selling tip: click the Right Mouse button when selecting an item from your pack to automatically sell it to a shopkeeper.",
            "Got too many items in your pack? Stop at Morbucks Savings and Loan to store your extra gear.",
            "There are six different kinds of weapons: pistols, rifles, shatterguns, rocket, melee and thrown.",
            "Ouch! Remember to watch out for toxic Fusion Matter patches on the ground.",
            "Having trouble with the mouse? Use the arrow keys to move around instead. Press the \"Z\" key to attack and the \"X\" key to use a Nano power.",
            "Need help finding your friends? Add your friends to your buddy list, then you can teleport to them using the Buddy Warp command.",
            "Once you have acquired your Eduardo Nano, you will be ready to use the time machine to return to the past."
        };

        public static LoadingScreen Ensure()
        {
            if (I == null) { var go = new GameObject("LoadingScreen"); DontDestroyOnLoad(go); I = go.AddComponent<LoadingScreen>(); }
            return I;
        }

        void Awake() { if (I != null && I != this) { Destroy(this); return; } I = this; }
        void OnDestroy() { Unbind(); if (I == this) I = null; }

        public void Show(string stepName)
        {
            failure = null; backToLauncher = null;
            stepText = Format(stepName); detail = ""; current = 0f; total = 0f; Visible = true;
            tip = Tips[UnityEngine.Random.Range(0, Tips.Length)];
        }
        public void Hide() { Visible = false; }
        public void ShowStage(string title) { Show("LOADING"); stepText = title; }
        public void ShowFailure(string message, System.Action back)
        {
            failure = message; backToLauncher = back; stepText = "STARTUP FAILED";
            detail = ""; current = total = 0f; Mini = false; Visible = true;
        }

        /// <summary>detail = texto de la fila superior; current/total = barras superior/inferior (0..1).</summary>
        public void SetProgress(string detail, float current, float total)
        {
            this.detail = detail ?? ""; this.current = Mathf.Clamp01(current); this.total = Mathf.Clamp01(total);
        }

        /// <summary>Se suscribe a WorldBootstrap.Status: el texto va al label de abajo y el progreso a la barra total (y a la superior, no hay progreso por archivo).</summary>
        public void Bind(WorldBootstrap b)
        {
            Unbind(); bound = b; if (b != null) b.Status += OnStatus;
        }
        void Unbind() { if (bound != null) bound.Status -= OnStatus; bound = null; }
        void OnStatus(string text, float p)
        {
            if (failure != null) return;
            stepText = text ?? ""; total = Mathf.Clamp01(p); current = total;
            if (!Visible) { Visible = true; if (string.IsNullOrEmpty(tip)) tip = Tips[UnityEngine.Random.Range(0, Tips.Length)]; }
        }

        // "LOADING " + paso + " ASSETS" salvo que ya venga con "LOADING"
        static string Format(string step)
        {
            if (string.IsNullOrEmpty(step)) return "LOADING";
            if (step.ToUpperInvariant().Contains("LOADING")) return step;
            return "LOADING " + step.ToUpperInvariant() + " ASSETS";
        }

        void EnsureFonts()
        {
            if (fontsTried) return; fontsTried = true;
            fTip = UiKit.Font("ChaletBook-Regular__1115"); fSmall = UiKit.Font("JEFFE___14__903");   // GameTips = ChaletBook-Regular; "label" del skin de AssetLoader = JEFFE___14, MiddleCenter, color (0,718 0,855 0,867)
        }

        void OnGUI()
        {
            if (!Visible) return;
            GUI.depth = 5;   // como AssetLoader.DrawAll
            EnsureFonts();
            if (Mini) DrawMini(); else DrawFull();
        }

        void DrawFull()
        {
            // fondo: negro + load.png (ScaleToFit)
            UiKit.BeginPixels();
            var full = new Rect(0, 0, Screen.width, Screen.height);
            UiKit.Fill(full, Color.black);
            UiKit.Img(full, UiKit.Tex("load"), ScaleMode.ScaleToFit);
            UiKit.End();

            UiKit.BeginCenter();
            var w = new Rect(-254f, -154f, 509f, 309f);
            UiKit.Img(w, UiKit.Tex("LoadingWindow"));
            // tip (GameTips: ChaletBook-Regular, color (0.11,0.21,0.39), UpperLeft)
            UiKit.Label(fTip, new Rect(w.x + 120, w.y + 62, 335, 112), failure ?? tip, TextAnchor.UpperLeft, new Color(0.11f, 0.21f, 0.39f), true);
            if (failure != null)
            {
                UiKit.Label(fSmall, new Rect(w.x + 29, w.y + 190, 450, 24), stepText, TextAnchor.MiddleCenter, LabelColor);
                bool back = UiKit.Button(new Rect(w.x + 130, w.y + 232, 250, 32), UiKit.Tex("blue_button_normal"), UiKit.Tex("blue_button_over"), "BACK TO LAUNCHER", fSmall);
                UiKit.End();
                if (back) { var action = backToLauncher; backToLauncher = null; Hide(); action?.Invoke(); }
                return;
            }
            // fila superior (en el original "DOWNLOADING " + nombre de archivo)
            UiKit.Label(fSmall, new Rect(w.x + 29, w.y + 196, 450, 19), string.IsNullOrEmpty(detail) ? "LOADING" : detail, TextAnchor.MiddleCenter, LabelColor);
            Bar(new Rect(w.x + 30, w.y + 217, 448, 17), current);
            // nombre del paso
            UiKit.Label(fSmall, new Rect(w.x + 29, w.y + 236, 450, 19), stepText, TextAnchor.MiddleCenter, LabelColor);
            Bar(new Rect(w.x + 30, w.y + 257, 448, 17), total);
            UiKit.End();
        }

        // estilo "bar": border L3 R15 T2 B2, ancho 15 + 448*p
        static readonly Color LabelColor = new Color(0.7176f, 0.8549f, 0.8667f);
        static void Bar(Rect group, float p)
        {   // GUI.BeginGroup(448x17) recorta: a p=1 el Box mide 463 y el borde sobrante queda fuera de la barra
            GUI.BeginGroup(group);
            UiKit.Slice(new Rect(0f, 0f, 15f + 448f * Mathf.Clamp01(p), group.height), UiKit.Tex("LoadingBar"), 3, 15, 2, 2);
            GUI.EndGroup();
        }

        void DrawMini()
        {
            // MiniScreen: Rect(W-160, 361, 150, 30) con pivote TopRight; SBox/SBar = SSBox / SSBoxInside, border 3
            UiKit.BeginTopRight();
            var r = new Rect(-160f, 361f, 150f, 30f);
            int pct = Mathf.RoundToInt(total * 100f);
            UiKit.Label(fSmall, new Rect(r.x, r.y, 150, 10), stepText + " " + pct + "%", TextAnchor.UpperCenter, Color.white);
            var box = new Rect(r.x + 10, r.y + 15, 130, 8);
            UiKit.Slice(box, UiKit.Tex("SSBox"), 3, 3, 3, 3);
            UiKit.Slice(new Rect(box.x + 1, box.y + 1, 128f * total, 6), UiKit.Tex("SSBoxInside"), 3, 3, 3, 3);
            UiKit.End();
        }
    }
}
