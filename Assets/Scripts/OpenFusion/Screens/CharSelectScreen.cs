// Pantalla de selección de personaje 1:1 con CnGuiCharSelection del cliente FusionFall (spec sección 3). IMGUI, escala = Screen.height/681.
// Escenario 3D en CharStage: 4 avatares en (0, 5*i, 0), miniaturas (distancia 0.5, altura 0.12, apuntando a "Bip01 Head") y vista grande (distancia 2, altura 0.75, ángulo -10). SIN COMPILAR EN UNITY.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables;
using OpenFusion.World;

namespace OpenFusion.Screens
{
    public sealed class CharSelectScreen : MonoBehaviour
    {
        const int SlotCount = 4;
        static readonly float[] BtnY = { 43f, 141f, 237f, 334f };     // rectButton0..3 (y dentro del grupo de selección)
        static readonly float[] ThumbY = { 31f, 128f, 225f, 321f };   // miniaturas
        static readonly Color Cyan = new Color(0.8f, 1f, 1f), DarkBlue = new Color(0f, 0f, 0.41f), NameUp = new Color(0.9f, 0.9f, 0.9f), NameDown = new Color(0f, 0.18f, 0.56f), LevelCol = new Color(0f, 0.8f, 1f);

        sealed class Slot { public CharInfo Info; public GameObject Avatar; public CharView Thumb; public string Name, Dong, ZoneKey; }
        readonly Slot[] slots = new Slot[SlotCount];

        TaskCompletionSource<SelectResult> tcs; LoginSession login; WorldBootstrap boot;
        CharStage stage; CharView main;
        bool running; int sel = -1;
        bool deleting, busy, focusDel; string delText = "", delError = "";
        float scroll; string tilesKey; Texture2D[] tiles;
        BitmapFont fJ14, fJ16, fJ12, fChaletSmall, fChalet;
        static bool music = true;   // bMusic
        static readonly Color Hov = new Color(0f, 0.28f, 0.48f), Txt9 = new Color(0.9f, 0.9f, 0.9f);

        public bool Visible { get { return running; } }

        // ============================================================== API ==============================================================
        public Task<SelectResult> Run(LoginSession login, WorldBootstrap boot, GameTables tables)
        {
            if (running && tcs != null) return tcs.Task;
            this.login = login; this.boot = boot;
            tcs = new TaskCompletionSource<SelectResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fJ14 = UiKit.Font("JEFFE___14__903"); fJ16 = UiKit.Font("JEFFE___16__1012"); fJ12 = UiKit.Font("JEFFE___12__953");
            fChaletSmall = UiKit.Font("ChaletBook-Regular_Small__1018"); fChalet = UiKit.Font("ChaletBook-Regular__1115");
            deleting = false; busy = false; delText = ""; delError = ""; scroll = 0f; tilesKey = null; tiles = null;
            MusicController.LobbyMuted = !music;
            running = true;
            Build();
            int last = login != null && login.Info != null ? login.Info.SlotNum - 1 : -1;   // último jugado (1..4)
            if (last >= 0 && last < SlotCount && slots[last] != null) Select(last); else Select(FirstFilled());
            return tcs.Task;
        }

        void Finish(SelectResult r)
        {
            if (!running) return;
            running = false; deleting = false; ClearStage(); MusicController.LobbyMuted = false;
            tcs.TrySetResult(r);
        }

        void ClearStage()
        {
            main = null;
            if (stage != null) { stage.Clear(); Destroy(stage.gameObject); stage = null; }
            for (int i = 0; i < SlotCount; i++) slots[i] = null;
        }

        void OnDestroy() { if (running) { running = false; if (stage != null) stage.Clear(); if (tcs != null) tcs.TrySetResult(new SelectResult { Action = SelectAction.Quit }); } }

        // ============================================================== datos / escenario ==============================================================
        void Build()
        {
            if (stage != null) { stage.Clear(); Destroy(stage.gameObject); stage = null; }
            main = null;
            for (int i = 0; i < SlotCount; i++) slots[i] = null;
            var av = boot != null ? boot.Avatars : null;
            if (av != null) { try { stage = CharStage.Create(av); } catch (Exception e) { Debug.LogWarning("[charselect] escenario: " + e.Message); stage = null; } }

            IReadOnlyList<CharInfo> list = login != null ? login.Characters : null;
            if (list != null)
                foreach (var ci in list)
                {
                    if (ci == null || ci.Appearance == null) continue;
                    int idx = ci.Slot - 1;   // iSlotNum 1..4
                    if (idx < 0 || idx >= SlotCount || slots[idx] != null) { idx = -1; for (int k = 0; k < SlotCount; k++) if (slots[k] == null) { idx = k; break; } }
                    if (idx < 0) continue;
                    slots[idx] = MakeSlot(ci, idx);
                }
            if (stage != null) main = stage.View(null, 733, 777, 2f, 0.75f);   // RTCamera: vAngle (-10,0,0), Distance 2, fHeight 0.75
            if (main != null) { main.Angle = new Vector3(-10f, 0f, 0f); main.Active = false; }
        }

        Slot MakeSlot(CharInfo ci, int idx)
        {
            var a = ci.Appearance;
            var s = new Slot { Info = ci };
            s.Name = a.NameChecked ? ((a.FirstName ?? "") + " " + (a.LastName ?? "")).Trim() : "Player " + a.Uid;   // iNameCheck != 1 -> "Player {uid}"
            string zone; s.Dong = LookupDong(ci.X / 100f, ci.Y / 100f, out zone).ToUpperInvariant(); s.ZoneKey = ZoneKey(zone);
            if (stage != null)
            {
                s.Avatar = stage.Spawn(a, new Vector3(0f, 5f * idx, 0f));
                if (s.Avatar != null)
                {
                    try { var anim = s.Avatar.GetComponentInChildren<Animation>(); if (anim != null && anim.GetClip("stand1") != null) anim.Play("stand1"); } catch (Exception) { }
                    var head = CharStage.Head(s.Avatar);
                    s.Thumb = stage.View(s.Avatar, 126, 170, 0.5f, 0.12f, head); if (s.Thumb != null) s.Thumb.Angle = new Vector3(-10f, 0f, 0f);   // cnSimpleCharRenderCamera.vAngle
                }
            }
            return s;
        }

        /// Área de contenido de un GUIStyle con padding (L10, R6, T4, B6): Transparent2/3 y CharNameUp/Down/CharLevelUp del GUISkin del original.
        static Rect Pad(Rect r) { return new Rect(r.x + 10f, r.y + 4f, r.width - 16f, r.height - 10f); }

        int FirstFilled() { for (int i = 0; i < SlotCount; i++) if (slots[i] != null) return i; return -1; }
        int FirstEmpty() { for (int i = 0; i < SlotCount; i++) if (slots[i] == null) return i; return -1; }

        void Select(int i)
        {
            sel = i;
            if (main == null) return;
            var s = i >= 0 && i < SlotCount ? slots[i] : null;
            main.Avatar = s != null ? s.Avatar : null; main.Active = main.Avatar != null; main.Angle = new Vector3(-10f, 0f, 0f);
        }

        // ---- zona ("DONG - ZONE") desde Resources/OpenFusion/UI/Data/worldname.json (mismo formato que NanocomHud: [x, y, w, h, dong, zone]) ----
        sealed class Area { public Rect R; public string Dong, Zone; }
        static List<Area> areas;
        static string LookupDong(float x, float y, out string zone)
        {
            zone = null;
            if (areas == null)
            {
                areas = new List<Area>();
                try
                {
                    var ta = Resources.Load<TextAsset>("OpenFusion/UI/Data/worldname");
                    var root = ta != null ? MiniJson.Parse(ta.text) as Dictionary<string, object> : null;
                    var arr = root != null && root.ContainsKey("a") ? root["a"] as List<object> : null;
                    if (arr != null) foreach (var o in arr)
                        {
                            var r = o as List<object>; if (r == null || r.Count < 6) continue;
                            areas.Add(new Area { R = new Rect(F(r[0]), F(r[1]), F(r[2]), F(r[3])), Dong = r[4] as string ?? "", Zone = r[5] as string ?? "" });
                        }
                }
                catch (Exception e) { Debug.LogWarning("[charselect] worldname: " + e.Message); }
            }
            foreach (var a in areas)
                if (x >= a.R.xMin && x < a.R.xMax && y >= a.R.yMin && y < a.R.yMax && a.Dong != "null") { zone = a.Zone; return a.Dong + (string.IsNullOrEmpty(a.Zone) ? "" : " - " + a.Zone); }
            return "—";
        }
        static float F(object o) { return o is double ? (float)(double)o : 0f; }
        static string ZoneKey(string zone)
        {
            switch (zone) { case "The Future": return "Future"; case "The Suburbs": return "Suburbs"; case "Downtown": return "Downtown"; case "The Wilds": return "Wilds"; case "The Darklands": return "Darklands"; }
            return null;   // otras zonas (WorldDataContainer.GetExpansiveCSBG) no soportadas
        }

        // ============================================================== bucle ==============================================================
        void Update()
        {
            if (!running) return;
            scroll = (scroll + Time.unscaledDeltaTime * 100f) % 5420f;   // 10 tiles de 542 px a 100 px/s
            if (deleting && Input.GetKeyDown(KeyCode.Escape)) { deleting = false; }
        }

        void OnGUI()
        {
            if (!running) return;
            GUI.depth = 10;   // por debajo de LoadingScreen (depth 5)
            DrawBackground();
            bool input = !deleting && !busy;

            UiKit.BeginCenter();
            DrawAvatarWindow(input);
            DrawSelWindow(input);
            UiKit.End();

            // QUIT (20, H-38, 90, 23), pivote BottomLeft
            UiKit.BeginBottomLeft();
            if (SBtn(new Rect(20f, -38f, 90f, 23f), "red_button_normal", "red_button_over", "QUIT", fJ14, Color.white, Color.white, input)) Finish(new SelectResult { Action = SelectAction.Quit });
            UiKit.End();

            // alternar pantalla completa/ventana (W-45, H-35, 35x31), pivote BottomRight; en móvil no tiene sentido
            if (!Application.isMobilePlatform)
            {
                UiKit.BeginBottomRight();
                var fr = new Rect(-45f, -35f, 35f, 31f); bool fs = Screen.fullScreen;
                if (UiKit.Button(fr, UiKit.Tex(fs ? "CSFullToWindow" : "CSFullScreenButton"), UiKit.Tex(fs ? "WindowModeButton_Over" : "FullscreenModeButton_Over"), null, null, null, input))
                    Screen.fullScreen = !fs;
                UiKit.End();
            }

            if (deleting) DrawDeleteWindow();
        }

        // ============================================================== dibujo ==============================================================
        static Texture2D gradient;
        static Texture2D Gradient()
        {
            if (gradient != null) return gradient;
            gradient = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) { float t = y / 63f; gradient.SetPixel(0, y, new Color(0.03f, 0.05f + 0.10f * t, 0.10f + 0.18f * t, 0.85f)); }
            gradient.Apply(); return gradient;
        }

        void DrawBackground()
        {
            var full = new Rect(0, 0, Screen.width, Screen.height);
            UiKit.BeginPixels();
            UiKit.Fill(full, new Color(0.02f, 0.03f, 0.06f));
            var csbg = UiKit.Tex("CSBG");
            if (csbg == null)
            {
                // respaldo: load.png (o CCBG) oscurecida + degradado
                var back = UiKit.Tex("load") ?? UiKit.Tex("CCBG");
                if (back != null) UiKit.Img(full, back, new Color(0.55f, 0.55f, 0.55f, 1f), ScaleMode.ScaleAndCrop);
                UiKit.Img(full, Gradient());
            }
            UiKit.End();

            UiKit.BeginCenter();
            DrawZoneTiles();   // OnGUIRotateBackground primero...
            if (csbg != null) UiKit.Img(new Rect(-960f, -720f, 1920f, 1440f), csbg);   // ...y texBGBG encima: rectFullScreen ((W-1920)/2, (H-1440)/2, 1920, 1440)
            UiKit.End();
        }

        void DrawZoneTiles()
        {
            string key = sel >= 0 && slots[sel] != null ? slots[sel].ZoneKey : null;
            if (key != tilesKey)
            {
                tilesKey = key; tiles = null;
                if (key != null)
                {
                    var first = UiKit.Tex("CS" + key + "BG_1");
                    if (first != null) { tiles = new Texture2D[10]; tiles[0] = first; for (int i = 1; i < 10; i++) tiles[i] = UiKit.Tex("CS" + key + "BG_" + (i + 1)); }
                }
            }
            if (tiles == null) return;   // sin texturas CS*BG_n: se omite
            float baseX = -960f, y = -720f + 418f, left = -UiKit.RefSize.x * 0.5f - 542f, right = UiKit.RefSize.x * 0.5f;
            for (int i = 0; i < 10; i++)
            {
                if (tiles[i] == null) continue;
                float u = ((i * 542f - scroll) % 5420f + 5420f) % 5420f; if (u > 5420f - 542f) u -= 5420f;   // wrap
                float x = baseX + u; if (x < left || x > right) continue;
                UiKit.Img(new Rect(x, y, 542f, 477f), tiles[i]);
            }
        }

        // botón de textura por nombre (normal/hover); deshabilitado = semitransparente
        bool Btn(Rect r, string normal, string over, string label, BitmapFont f, Color tc, bool enabled)
        {
            var o = GUI.color; if (!enabled) GUI.color = new Color(o.r, o.g, o.b, o.a * 0.5f);
            bool res = UiKit.Button(r, UiKit.Tex(normal), UiKit.Tex(over), label, f, tc, enabled);
            GUI.color = o; return res;
        }

        // botón con estilo del GUISkin: nine-slice 5px para red/blue_button (20x25), color de texto normal/hover
        bool SBtn(Rect r, string normal, string over, string label, BitmapFont f, Color cN, Color cH, bool enabled)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, r); var e = Event.current; bool hov = r.Contains(e.mousePosition), res = false;
            if (enabled)
            {
                if (e.type == EventType.MouseDown && hov && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; if (hov) res = true; e.Use(); }
            }
            if (e.type == EventType.Repaint)
            {
                bool hot = enabled && (hov || GUIUtility.hotControl == id);
                var t = UiKit.Tex(hot ? over : normal) ?? UiKit.Tex(normal);
                float a = enabled ? 1f : 0.5f; var o = GUI.color; GUI.color = new Color(o.r, o.g, o.b, o.a * a);
                if (t != null) { if (t.width <= 32) UiKit.Slice(r, t, 5, 5, 5, 5); else GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); }
                UiKit.Label(f, r, label, TextAnchor.MiddleCenter, hot ? cH : cN);
                GUI.color = o;
            }
            return res;
        }

        // ---- ventana del avatar: rectCharRT = (W/2-510, H/2-315, 600, 600) ----
        void DrawAvatarWindow(bool input)
        {
            float ox = -510f, oy = -315f;
            var s = sel >= 0 ? slots[sel] : null;
            var cam = new Rect(ox, oy, 550f, 583f);
            if (main != null && s != null && s.Avatar != null) UiKit.Img(cam, main.Texture);
            else if (s != null)
            {
                // sin avatar 3D: recuadro con el nombre
                UiKit.Fill(new Rect(cam.x + 120, cam.y + 110, 310, 300), new Color(0f, 0f, 0f, 0.35f));
                UiKit.Outline(new Rect(cam.x + 120, cam.y + 110, 310, 300), new Color(0.3f, 0.7f, 0.9f, 0.8f), 2f);
                UiKit.Label(fJ16, new Rect(cam.x + 120, cam.y + 110, 310, 300), s.Name, TextAnchor.MiddleCenter, Cyan, true);
            }

            // rotar izquierda/derecha (RepeatButton: vAngle.y -/+ dt*100)
            bool left, right;
            var tl = UiKit.Tex("CCRotateLeftNormal"); var tlo = UiKit.Tex("CCRotateLeftOver"); var tr = UiKit.Tex("CCRotateRightNormal"); var tro = UiKit.Tex("CCRoatateRightOver");
            var rl = new Rect(ox + 50f, oy + 480f, 66f, 110f); var rr = new Rect(ox + 440f, oy + 480f, 66f, 110f);
            if (input) { left = UiKit.Held(rl, tl, tlo); right = UiKit.Held(rr, tr, tro); }
            else { UiKit.Img(rl, tl); UiKit.Img(rr, tr); left = right = false; }
            if (main != null && Event.current.type == EventType.Repaint && (left || right))
            {
                var ang = main.Angle; ang.y += (right ? 1f : -1f) * Time.unscaledDeltaTime * 100f; main.Angle = ang;
            }

            if (s != null)
            {
                UiKit.Label(fChalet, new Rect(ox, oy + 23f, 550f, 15f), s.Name, TextAnchor.MiddleCenter, Cyan);   // AvatarName (0.8,1,1)
            }
        }

        // ---- ventana de selección: rectSelButton = (W/2+30, H/2-288, 478, 600) ----
        void DrawSelWindow(bool input)
        {
            float ox = 30f, oy = -288f;
            UiKit.Label(fJ14, Pad(new Rect(ox + 134f, oy + 2f, 217f, 25f)), "SELECT A CHARACTER", TextAnchor.MiddleCenter, DarkBlue);   // Transparent2

            var tN = UiKit.Tex("CSCharButtonNormal"); var tO = UiKit.Tex("CSCharButtonOver"); var tE = UiKit.Tex("CSCharButtonEmpty");
            var dBack = UiKit.Tex("DiskBack"); var dFront = UiKit.Tex("DiskFront");
            for (int i = 0; i < SlotCount; i++)
            {
                var btn = new Rect(ox + 43f, oy + BtnY[i], 413f, 67f);
                float off = BtnY[i] - 43f;
                var s = slots[i];
                if (s == null)
                {
                    UiKit.Img(btn, tE);
                    UiKit.Label(fJ14, Pad(new Rect(ox + 220f, oy + BtnY[i] + 28f, 63f, 15f)), "EMPTY", TextAnchor.MiddleCenter, Cyan);   // Transparent3 (220, y+28, 63, 15)
                    UiKit.Img(new Rect(ox + 23f, oy + 93f + off, 69f, 23f), dBack);
                    UiKit.Img(new Rect(ox + 23f, oy + 106f + off, 69f, 23f), dFront);
                    continue;
                }
                bool selected = i == sel;
                bool hot = selected || UiKit.Hover(btn);
                if (UiKit.Button(btn, selected ? tO : tN, tO, null, null, null, input)) Select(i);   // tocar el slot lo selecciona
                UiKit.Img(new Rect(ox + 23f, oy + 93f + off, 69f, 23f), dBack);
                if (s.Thumb != null) UiKit.Img(new Rect(ox + 26f, oy + ThumbY[i], 63f, 85f), s.Thumb.Texture);
                UiKit.Img(new Rect(ox + 23f, oy + 106f + off, 69f, 23f), dFront);
                UiKit.Label(fChaletSmall, Pad(new Rect(ox + 85f, oy + 52f + off, 370f, 16f)), s.Name, TextAnchor.MiddleLeft, hot ? NameDown : NameUp);   // CharNameUp/Down
                UiKit.Label(fChaletSmall, Pad(new Rect(ox + 98f, oy + 70f + off, 97f, 16f)), "LEVEL " + s.Info.Level, TextAnchor.MiddleLeft, hot ? NameDown : LevelCol);    // CharLevelUp / CharNameDown
                UiKit.Label(fChaletSmall, Pad(new Rect(ox + 98f, oy + 87f + off, 250f, 16f)), s.Dong, TextAnchor.MiddleLeft, hot ? NameDown : LevelCol);
            }

            bool has = sel >= 0 && slots[sel] != null; int empty = FirstEmpty();
            if (SBtn(new Rect(ox + 47f, oy + 449f, 188f, 25f), "red_button_normal", "red_button_over", "DELETE CHARACTER", fJ14, Color.white, Color.white, input && has)) { deleting = true; delText = ""; delError = ""; focusDel = true; }
            if (SBtn(new Rect(ox + 244f, oy + 449f, 188f, 25f), "blue_button_normal", "blue_button_over", "CREATE CHARACTER", fJ14, Txt9, Hov, input && empty >= 0)) Finish(new SelectResult { Action = SelectAction.Create, Slot = empty + 1 });
            if (SBtn(new Rect(ox + 83f, oy + 503f, 325f, 57f), "CSEnterButton", "CSEnterGameOver", "ENTER THE GAME", fJ16, Cyan, Hov, input && has)) Finish(new SelectResult { Action = SelectAction.Play, Char = slots[sel].Info, Slot = sel + 1 });

            // MusicToggle (225,567,86,30): con la música activa muestra "musicoff" (OnNormal), apagada "musicon"
            if (UiKit.Button(new Rect(ox + 225f, oy + 567f, 86f, 30f), UiKit.Tex(music ? "musicoff" : "musicon"), UiKit.Tex(music ? "musicoff" : "musicon"), null, null, null, input))
            { music = !music; MusicController.LobbyMuted = !music; }
        }

        // ---- ventana de borrado (GUI.Window 99): oscurece, texDelWin 526x164 centrada ----
        void DrawDeleteWindow()
        {
            UiKit.BeginPixels();
            var dim = UiKit.Tex("CSDeleteBG"); var full = new Rect(0, 0, Screen.width, Screen.height);
            if (dim != null) UiKit.Img(full, dim, new Color(1, 1, 1, 0.75f)); else UiKit.Fill(full, new Color(0, 0, 0, 0.75f));
            UiKit.End();   // la entrada de lo de debajo ya está desactivada (input=false)

            UiKit.BeginCenter();
            var w = new Rect(-263f, -82f, 526f, 164f);
            UiKit.Img(w, UiKit.Tex("CSDeleteWindow"));
            UiKit.Label(fJ14, new Rect(w.x + 176f, w.y + 15f, 173f, 17f), "DELETE CHARACTER", TextAnchor.MiddleCenter, Cyan);
            UiKit.Label(fChaletSmall, new Rect(w.x + 118f, w.y + 36f, 288f, 17f), "Please enter your character's first name to delete", TextAnchor.MiddleCenter, Color.white);
            if (!busy) delText = UiKit.Field(new Rect(w.x + 75f, w.y + 73f, 378f, 24f), delText, 32, "delname");
            else UiKit.Label(fChaletSmall, new Rect(w.x + 75f, w.y + 73f, 378f, 24f), "Deleting...", TextAnchor.MiddleCenter, Color.white);
            if (focusDel && Event.current.type == EventType.Repaint) { GUI.FocusControl("delname"); focusDel = false; }
            if (!string.IsNullOrEmpty(delError)) UiKit.Label(fChaletSmall, new Rect(w.x + 75f, w.y + 100f, 378f, 18f), delError, TextAnchor.MiddleCenter, new Color(1f, 0.45f, 0.4f));
            bool cancel = Btn(new Rect(w.x + 40f, w.y + 125f, 152f, 27f), "CancelNormal", "blue_button_normal", "CANCEL", fJ12, Cyan, !busy);
            bool del = SBtn(new Rect(w.x + 335f, w.y + 125f, 152f, 27f), "red_button_normal", "red_button_over", "DELETE", fJ14, Color.white, Color.white, !busy);
            UiKit.End();

            if (UiKit.EnterPressed() && !busy) del = true;
            if (cancel) deleting = false;
            else if (del) DoDelete();
        }

        // DeleteCharacter: el texto debe igualar szFirstName (nameCheck==1) o "Player"
        async void DoDelete()
        {
            var s = sel >= 0 ? slots[sel] : null; if (s == null || busy || login == null) return;
            string need = s.Info.Appearance.NameChecked ? (s.Info.Appearance.FirstName ?? "") : "Player";
            if (!string.Equals((delText ?? "").Trim(), need, StringComparison.OrdinalIgnoreCase)) { delError = "The name does not match."; return; }
            busy = true; delError = "";
            try
            {
                await login.DeleteCharacterAsync(s.Info.Appearance.Uid);
                if (!running) return;
                deleting = false; delText = "";
                Build(); Select(FirstFilled());   // refresca desde login.Characters
            }
            catch (Exception e) { delError = "Could not delete the character. " + e.Message; }
            finally { busy = false; }
        }
    }
}
