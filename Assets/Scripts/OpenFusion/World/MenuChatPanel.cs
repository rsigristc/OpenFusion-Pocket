// Frases rapidas ("MENU CHAT") y emotes del chat: port de MenuChatScript (frases en 3 niveles desde ChatTable, lista de emotes InitLargeChatEmoteList). SIN COMPILAR NI PROBAR EN UNITY.
//   Los botones del chat (menuChatButton / emoteButton) llaman a Swap(true/false) (CnGuiChat l.1170-1190: FirstUse 32 / 31 + sonido).
//   Frase = REQ_SEND_*MENUCHAT_MESSAGE { szFreeChat, iEmoteCode = emote enlazado de MenuChatEmote } segun la pestana activa (ChatHud.SendMenuChat); el servidor la devuelve y la animacion del emote enlazado se reproduce al recibirla (EmoteSystem).
//   Emote = REQ_PC_AVATAR_EMOTES_CHAT (EmoteSystem.SendUser).
//   Geometria (OnOwnGUI/DoMenuChat/DoCommonChat/RefreshListSize): EmoteMenuPos = (0, H-385, 170, 235); listas con area = (x, y + 149, ancho + 28 + 10, alto + 5), filas de 18 px, caja newEmoteMenuBox, botones ChatButton / ChatLock2 (abierto, +17 px), flecha HasNextMenu_Arrow, iconos de emote 26 px.
//   Animaciones: nivel 0 x 0->40 (frases) / 72 (emotes), nivel 1 x 60 (frases) o 5/95 (emotes) -> + ancho nivel 0, nivel 2 + ancho nivel 1; 0,4 s al abrir (Berp), 0,3 s al cerrar (Lerp).
// Diferencias: el cierre al hacer clic fuera usa las areas reales de las listas (el original usaba el rect sin el desplazamiento de 149 px); modo macro (teclas 1..0 - =) incluido pero sin tecla de activacion (localized.MacroChatKey desconocida).
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class MenuChatPanel : MonoBehaviour
    {
        public static MenuChatPanel Instance { get; private set; }
        public static bool MouseOver { get; private set; }
        public static bool Active { get { return Instance != null && (Instance.showMenu || Instance.showEmotes); } }
        public Func<string, Texture> TexLoader;

        sealed class Item
        {
            public int Id, Parent, Emote, Level; public string Text; public bool Container, Opened;
            public Item(int id, int parent, string text, int emote, bool container, int level) { Id = id; Parent = parent; Text = text; Emote = emote; Container = container; Level = level; }
        }
        struct Tw
        {
            public bool On, Berp; public float T0, Dur, A, B, Al0, Al1;
            public float X(float cur) { if (!On) return cur; float t = Mathf.Clamp01((Time.unscaledTime - T0) / Dur); if (Berp) return Mathf.LerpUnclamped(A, B, BerpK(t)); return Mathf.Lerp(A, B, t); }
            public float Alpha(float cur) { if (!On) return cur; return Mathf.Lerp(Al0, Al1, Mathf.Clamp01((Time.unscaledTime - T0) / Dur)); }
            public bool Done { get { return Time.unscaledTime - T0 >= Dur; } }
        }
        static float BerpK(float v) { v = Mathf.Clamp01(v); return (Mathf.Sin(v * Mathf.PI * (0.2f + 2.5f * v * v * v)) * Mathf.Pow(1f - v, 2.2f) + v) * (1f + 1.2f * (1f - v)); }   // Mathfx.Berp

        ChatHud chat; XdtTableScript tables; NanocomHud nano; EmoteSystem emotes;
        readonly List<Item> menuList = new List<Item>(), emoteList = new List<Item>();
        readonly Texture[] icons = new Texture[19]; float iconRetry;
        bool showMenu, showEmotes, macro; public bool Hold;
        Vector2 size0, size1, size2; Vector2 pos0, pos1, pos2; float alpha1 = 1f, alpha2 = 1f;
        Tw tw0, tw1, tw2; bool close0;
        const int RowH = 18, Overflow = 10;
        Rect emp;   // EmoteMenuPos
        Texture2D tArrow; bool texTried;
        SkinStyle sBtn, sLock, sBox, sMenuLock, sEmoteLock;

        public static MenuChatPanel Ensure(GameObject host) { var p = host.GetComponent<MenuChatPanel>(); if (p == null) p = host.AddComponent<MenuChatPanel>(); Instance = p; return p; }
        public void Init(ChatHud c, XdtTableScript t, NanocomHud nh, EmoteSystem em)
        {
            if (chat != null) { chat.OnMenuChatButton = null; chat.OnEmoteButton = null; }
            chat = c; tables = t; nano = nh; emotes = em;
            if (chat != null) { chat.OnMenuChatButton = () => { FirstUse.Check(32); Swap(true); Sfx.Button(); Sfx.Ui("Click_WindowSlideOut"); }; chat.OnEmoteButton = () => { FirstUse.Check(31); Swap(false); Sfx.Button(); Sfx.Ui("Click_WindowSlideOut"); }; }
            InitEmotes();
        }
        void OnDestroy() { if (Instance == this) Instance = null; MouseOver = false; if (chat != null) { chat.OnMenuChatButton = null; chat.OnEmoteButton = null; } }
        void OnDisable() { MouseOver = false; }
        public bool Animating { get { return tw0.On || tw1.On || tw2.On; } }

        // ============================================================ datos ============================================================
        void InitEmotes()
        {   // InitLargeChatEmoteList
            emoteList.Clear(); var L = emoteList;
            string[] top = { "Hello", "Goodbye", "Yes", "No", "Okay", "Thanks", "Happy", "Sad", "Angry", "Scared", "Love", "Laugh", "Taunt", "Cheer", "Clap", "Flex" }; int[] code = { 4, 21, 8, 10, 13, 5, 16, 1, 2, 3, 7, 9, 12, 15, 14, 11 };
            for (int i = 0; i < top.Length; i++) L.Add(new Item(i, 0, top[i], code[i], false, 0));
            L.Add(new Item(16, 0, "Dance", 6, true, 0)); L.Add(new Item(17, 0, "Chill Out", 22, true, 0)); L.Add(new Item(18, 0, "Extras", 25, true, 0));
            L.Add(new Item(111, 16, "Hipshake", 6, false, 1)); L.Add(new Item(112, 16, "Point and Shimmy", 17, false, 1)); L.Add(new Item(113, 16, "Break it Down", 18, false, 1)); L.Add(new Item(114, 16, "Rock it Out", 19, false, 1)); L.Add(new Item(115, 16, "All Dances", 20, false, 1));
            L.Add(new Item(211, 17, "Sit", 22, false, 1)); L.Add(new Item(212, 17, "Lie Down", 23, false, 1)); L.Add(new Item(213, 17, "Lounge", 24, false, 1));
            L.Add(new Item(311, 18, "Earpiece", 25, false, 1)); L.Add(new Item(312, 18, "Coin Flip", 26, false, 1)); L.Add(new Item(313, 18, "Look There!", 27, false, 1)); L.Add(new Item(314, 18, "Head Scratch", 28, false, 1));
            L.Add(new Item(315, 18, "Oomph!", 29, false, 1)); L.Add(new Item(315, 18, "Power Up", 30, false, 1));
        }
        void LoadIcons()
        {
            if (TexLoader == null || Time.unscaledTime < iconRetry) return; iconRetry = Time.unscaledTime + 3f; bool miss = false;
            for (int i = 0; i < icons.Length && i < emoteList.Count; i++)
            {
                if (icons[i] != null) continue;
                foreach (var ext in new[] { ".png", ".psd", ".dds", ".tga" }) { try { icons[i] = TexLoader("TutorialAssets/emoteChatIcon/" + emoteList[i].Text + "Icon" + ext); } catch (Exception) { icons[i] = null; } if (icons[i] != null) break; }
                if (icons[i] == null) miss = true;
            }
            if (!miss) iconRetry = float.MaxValue;
        }
        string MissionName()
        {   // MissionManager.SelectMissionTask: nombre de la mision actual del diario
            var mt = tables != null ? tables.m_pMissionTable : null; if (mt == null || nano == null || mt.m_pMissionData == null || mt.m_pMissionStringData == null) return "";
            int id = nano.JournalSelected; if (id == 0) return "";
            foreach (var m in mt.m_pMissionData) if (m != null && m.m_iHMissionID == id) { int ni = m.m_iHMissionName; return ni >= 0 && ni < mt.m_pMissionStringData.Length && mt.m_pMissionStringData[ni] != null ? mt.m_pMissionStringData[ni].m_pstrNameString ?? "" : ""; }
            return "";
        }
        string StrAt(ChatTableScript ct, int idx) { return ct.m_pChatStringData != null && idx >= 0 && idx < ct.m_pChatStringData.Length && ct.m_pChatStringData[idx] != null ? ct.m_pChatStringData[idx].m_pstrMMeshModelString ?? "" : ""; }
        int EmoteAt(ChatTableScript ct, int idx) { return ct.m_pMenuChatEmoteData != null && idx >= 0 && idx < ct.m_pMenuChatEmoteData.Length && ct.m_pMenuChatEmoteData[idx] != null ? ct.m_pMenuChatEmoteData[idx].m_iLinkEmoteNum : 0; }
        void InitMenuList()
        {   // InitChatMenuList: ChatTable.m_pFirstChatData (nivel 0) y m_pSecondChatData (nivel 1, hasta 30 por frase)
            menuList.Clear(); var ct = tables != null ? tables.m_pChatTable : null; if (ct == null || ct.m_pFirstChatData == null) return;
            for (int num = 1; num < ct.m_pFirstChatData.Length; num++)
            {
                var e = ct.m_pFirstChatData[num]; if (e == null || e.m_iItemConditionNum <= 0) continue;
                bool cont = e.m_iNextItem != null && e.m_iNextItem.Length > 0 && e.m_iNextItem[0] != 0;
                menuList.Add(new Item(num, 0, StrAt(ct, e.m_iItemStringIndex), EmoteAt(ct, e.m_iItemStringIndex), cont, 0));
                if (!cont) continue;
                for (int j = 0; j < e.m_iNextItem.Length && j < 30 && e.m_iNextItem[j] > 0; j++)
                {
                    int ni = e.m_iNextItem[j]; if (ct.m_pSecondChatData == null || ni >= ct.m_pSecondChatData.Length) break; var c5 = ct.m_pSecondChatData[ni]; if (c5 == null) continue;
                    string text = StrAt(ct, c5.m_iItemStringIndex);
                    if (c5.m_iItemConditionNum == 2) text = text.Replace("&&", nano != null ? nano.DongName : "");           // pNanocom.dongName
                    else if (c5.m_iItemConditionNum == 3) text = text.Replace("&&", MissionName());                           // nombre de la mision seleccionada
                    menuList.Add(new Item(num * 100 + j, num, text, EmoteAt(ct, c5.m_iItemStringIndex), false, 1));
                }
            }
        }
        List<Item> Cur { get { return showEmotes ? emoteList : menuList; } }

        // ============================================================ estado ============================================================
        /// <summary>SwapMenuChatEmotes: true = frases, false = emotes.</summary>
        public void Swap(bool menu)
        {
            size0.x = size1.x = size2.x = 0f; if (macro && !menu) return;
            if (showMenu) CloseAll(menuList); if (showEmotes) CloseAll(emoteList);
            showMenu = menu; showEmotes = !menu; close0 = false;
            if (menu) InitMenuList();
            Anim(0, true, 0.4f);
        }
        public void SetMacro(bool on)
        {
            if (on) { if (!macro) { macro = true; Clear(); Swap(true); } } else if (macro) Clear();
            macro = on;
        }
        static void CloseAll(List<Item> l) { foreach (var i in l) i.Opened = false; }
        /// <summary>ClearMenuChat / ClearEmote.</summary>
        public void Clear()
        {
            CloseAll(menuList); CloseAll(emoteList); close0 = true; Anim(0, false, 0.3f); Anim(1, false, 0.3f); Anim(2, false, 0.3f);
        }
        void Anim(int level, bool vis, float dur)
        {
            float b0 = showMenu ? 60f : 95f; Tw t = new Tw { On = true, T0 = Time.unscaledTime, Dur = dur, Berp = vis, Al0 = vis ? 0f : 1f, Al1 = vis ? 1f : 0f };
            if (level == 0)
            {
                float far = showMenu ? 40f : 72f; t.A = vis ? 0f : far; t.B = vis ? far : 0f; t.Al0 = t.Al1 = 1f; tw0 = t;
            }
            else if (level == 1)
            {
                if (showMenu) { t.A = vis ? 60f : 60f + size0.x; t.B = vis ? 60f + size0.x : 60f; } else { t.A = vis ? 5f : 95f + size0.x; t.B = vis ? 95f + size0.x : 95f; }
                tw1 = t;
            }
            else
            {
                float w = size0.x + size1.x; if (showMenu) { t.A = vis ? 60f : 60f + w; t.B = vis ? 60f + w : 60f; } else { t.A = vis ? 95f : 95f + w; t.B = vis ? 95f + w : 95f; }
                tw2 = t;
            }
            _ = b0;
        }
        void Toggle(Item it, List<Item> l)
        {   // ToggleSelection + CloseMenuChat(id, level)
            var prev = l.Find(m => m.Opened && m.Level == it.Level); if (prev != null) prev.Opened = false;
            foreach (var m in l) if (m.Level > it.Level) m.Opened = false;
            it.Opened = true;
        }

        void Update()
        {
            if (tw0.On && tw0.Done) { tw0.On = false; pos0.x = tw0.B; if (close0) { showMenu = showEmotes = false; close0 = false; } }
            if (tw1.On && tw1.Done) { tw1.On = false; pos1.x = tw1.B; alpha1 = tw1.Al1; }
            if (tw2.On && tw2.Done) { tw2.On = false; pos2.x = tw2.B; alpha2 = tw2.Al1; }
            if (tw0.On) pos0.x = tw0.X(pos0.x); if (tw1.On) { pos1.x = tw1.X(pos1.x); alpha1 = tw1.Alpha(alpha1); } if (tw2.On) { pos2.x = tw2.X(pos2.x); alpha2 = tw2.Alpha(alpha2); }
            if ((showMenu || showEmotes) && NpcUI.Active) { showMenu = showEmotes = false; tw0.On = tw1.On = tw2.On = false; }
            if (macro) UpdateMacro();
            // ratón sobre las listas: no se dispara ni se selecciona detras
            bool over = false;
            if (showMenu || showEmotes)
            {
                float s = GameOptions.UiScale, H = Screen.height; Vector2 m = new Vector2(Input.mousePosition.x / s, H - Input.mousePosition.y / s);
                over = Area(size2, pos2).Contains(m) && size2.y > 0f && OpenAt(1) || Area(size1, pos1).Contains(m) && size1.y > 0f && OpenAt(0) || Area(size0, pos0).Contains(m);
            }
            MouseOver = over;
        }
        bool OpenAt(int level) { return Cur.Exists(i => i.Opened && i.Level == level); }
        static Rect Area(Vector2 size, Vector2 pos) { return new Rect(pos.x, pos.y + 149f, size.x + 28f + Overflow, size.y + 5f); }

        static readonly KeyCode[] Keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus, KeyCode.Equals };
        void UpdateMacro()
        {   // modo macro: 1..0 - = eligen la opcion numerada del nivel mas profundo abierto
            showMenu = true; var l = menuList; var o1 = l.Find(i => i.Opened && i.Level == 1); var o0 = l.Find(i => i.Opened && i.Level == 0);
            int parent = o1 != null ? o1.Id : o0 != null ? o0.Id : 0; int n = 0;
            foreach (var it in l)
            {
                if (it.Parent != parent) continue; if (n >= 12) break;
                if (Input.GetKeyDown(Keys[n]))
                {
                    if (!it.Container) { Send(it); InitMenuList(); }
                    else { Toggle(it, l); RefreshMenuSize(); Anim(o0 != null ? 2 : 1, true, 0.4f); Sfx.Button(); }
                    return;
                }
                n++;
            }
        }
        void Send(Item it) { if (chat != null) chat.SendMenuChat(it.Text, it.Emote); Sfx.Button(); }
        void SendEmote(Item it) { if (emotes != null) emotes.SendUser(it.Emote); Sfx.Button(); }

        // ============================================================ tamanos (RefreshListSize / RefreshEmoteListSize) ============================================================
        float W(string s) { return sBtn != null && sBtn.Font != null ? sBtn.Font.Width(s) + sBtn.pL + sBtn.pR : s.Length * 7f + 10f; }
        void RefreshMenuSize() { Refresh(menuList, false); }
        void Refresh(List<Item> l, bool emote)
        {
            size0 = size1 = size2 = Vector2.zero; int num = 0, num2 = 0, num3 = 0, num4 = 0; string pre = macro && !emote ? "10." : ""; float extra = emote ? 20f : 0f;
            foreach (var c in l)
            {
                if (c.Parent != 0) continue;
                size0.x = Mathf.Max(size0.x, W(pre + c.Text) + extra); size0.y += RowH;
                if (c.Opened)
                {
                    foreach (var c2 in l)
                    {
                        if (c2.Parent != c.Id) continue;
                        size1.x = Mathf.Max(size1.x, W(pre + c2.Text) + extra); size1.y += RowH;
                        if (c2.Opened)
                        {
                            num3 = num;
                            foreach (var c3 in l) if (c3.Parent == c2.Id) { size2.x = Mathf.Max(size2.x, W(pre + c3.Text) + extra); size2.y += RowH; }
                            num4 = num2;
                        }
                        num2++;
                    }
                    num3 = num;
                }
                num++;
            }
            float add = emote ? 0f : 20f; size0.x = Mathf.Max(30f, size0.x + add); size1.x = Mathf.Max(30f, size1.x + add); size2.x = Mathf.Max(30f, size2.x + add);
            int half = emote ? 1 : 2;   // la version de frases divide por 2 (int): num * 18 / 2
            pos0.y = emp.y + emp.height - (size0.y + 18f);
            if (size1.y > size0.y) pos1.y = emp.y + emp.height - (size1.y + 18f);
            else { pos1.y = pos0.y + (num3 * RowH / half + 18f); if (pos1.y + size1.y > pos0.y + size0.y) pos1.y = emp.y + emp.height - (size1.y + 18f); }
            if (size2.y > size1.y) { pos2.y = emp.y + size1.y - size2.y - 18f; return; }
            pos2.y = emp.y + emp.height - (size1.y + num4 * RowH / half + 18f);
            if (pos2.y + (emote ? size1.y : size2.y) > pos1.y + size1.y) pos2.y = emp.y + emp.height - (size2.y + 18f);
        }

        // ============================================================ dibujo ============================================================
        void Styles()
        {
            if (texTried) return; texTried = true;
            sBtn = Skin.Get(Skin.Chat, "ChatButton"); sLock = Skin.Get(Skin.Chat, "ChatLock2"); sBox = Skin.Get(Skin.Chat, "newEmoteMenuBox");
            sMenuLock = Skin.Get(Skin.Chat, "MenuChatLock"); sEmoteLock = Skin.Get(Skin.Chat, "EmoteChatLock"); var ar = Skin.Get(Skin.Chat, "nextArrow"); tArrow = ar != null ? ar.N : null;
        }

        void OnGUI()
        {
            var e = Event.current; if (e == null || Screen.height <= 0) return;
            if (!(showMenu || showEmotes || macro)) return; if (GameMenu.HudHidden || NpcUI.Active) return;
            Styles(); if (sBtn == null) return; LoadIcons();
            float H = Screen.height, s = GameOptions.UiScale; emp = new Rect(0f, H - 385f, 170f, 235f);
            int od = GUI.depth; var om = GUI.matrix; var oc = GUI.color; bool oe = GUI.enabled; GUI.depth = 8;
            GUIUtility.ScaleAroundPivot(new Vector2(s, s), new Vector2(0f, H)); GUI.enabled = !Hold && !(NpcUI.Active);
            try
            {
                if (size0.x == 0f) Refresh(Cur, showEmotes);
                else if (e.type == EventType.Layout) Refresh(Cur, showEmotes);   // posiciones/tamanos siempre vigentes (el original solo refresca al abrir un contenedor; equivale)
                GUI.color = new Color(1f, 1f, 1f, alpha2); DrawList(Area(size2, pos2), 1);
                GUI.color = new Color(1f, 1f, 1f, alpha1); DrawList(Area(size1, pos1), 0);
                GUI.color = Color.white; DrawList(Area(size0, pos0), -1);
                // marcas de boton activo sobre la fila del chat (MenuChatLock / EmoteChatLock en emp.x + 6 / + 41, y = emp.y + 359,4)
                if (showMenu && sMenuLock != null) Skin.Box(new Rect(emp.x + 6f, emp.y + 359.4f, 37f, 18f), sMenuLock);
                if (showEmotes && sEmoteLock != null) Skin.Box(new Rect(emp.x + 41f, emp.y + 359.4f, 37f, 18f), sEmoteLock);
                // clic fuera: se cierra (no en modo macro). Dentro cuentan las listas y la fila de botones del chat.
                if (e.type == EventType.MouseUp && !macro && !Animating)
                {
                    Vector2 m = e.mousePosition; bool inside = new Rect(0f, H - 32f, 80f, 26f).Contains(m) || Area(size0, pos0).Contains(m) || (OpenAt(0) && Area(size1, pos1).Contains(m)) || (OpenAt(1) && Area(size2, pos2).Contains(m));
                    if (!inside) Clear();
                }
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.enabled = oe; GUI.depth = od; }
        }

        /// <summary>DoCommonChat: level -1 = lista principal, 0 = segunda, 1 = tercera.</summary>
        void DrawList(Rect area, int level)
        {
            var l = Cur; Item opened = null;
            if (level >= 0) { opened = l.Find(i => i.Opened && i.Level == level); if (opened == null) return; }
            if (area.width <= 38f || area.height <= 5f) return;
            var e = Event.current;
            if (e.type == EventType.Repaint) Skin.Bg(new Rect(area.x, area.y, area.width - Overflow, area.height), sBox != null ? sBox.N : null, sBox);   // newEmoteMenuBox con overflow.right = -10
            float bw = area.width - 20f; int row = 0;
            for (int i = 0; i < l.Count; i++)
            {
                var it = l[i]; if (level >= 0 ? it.Parent != opened.Id : it.Parent != 0) continue;
                bool openedMark = it.Opened; var st = openedMark ? sLock : sBtn;
                string text = it.Text; if (macro && !showEmotes) text = (row + 1) + ". " + text;
                bool flag2 = level < 0 && openedMark;
                if (level < 0) { if (showEmotes && i < icons.Length + 1) text = "    " + text; if (it.Container) text += "..."; }
                var btn = new Rect(area.x + 4f, area.y + 6f + row * RowH, bw + (openedMark ? Overflow + 7f : 0f), 16f);
                if (Skin.Button(btn, text, st, GUI.enabled, true) && !Animating && !flag2) Pick(it, level, l);
                if (level < 0 && showEmotes && i < icons.Length && icons[i] != null && e.type == EventType.Repaint) { var ic = icons[i]; float iw = ic.width, ih = ic.height, k = Mathf.Min(1f, Mathf.Min(26f / Mathf.Max(1f, iw), 17f / Mathf.Max(1f, ih))); iw *= k; ih *= k; GUI.DrawTexture(new Rect(area.x + 3f + (26f - iw) / 2f, area.y + 6f + row * RowH + (16f - ih) / 2f * (k < 1f ? 1f : 0f), iw, ih), ic, ScaleMode.StretchToFill, true); }   // tamano nativo del original; solo se reduce si el icono no cabe en 26x17 (evita solaparse con la etiqueta)
                if (it.Container && !flag2 && tArrow != null && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(area.x + area.width - 25f, area.y + 3f + row * RowH, 8f, 14f), tArrow);
                row++;
            }
        }
        void Pick(Item it, int level, List<Item> l)
        {
            if (level <= 0 && it.Container)
            {
                Toggle(it, l); Refresh(l, showEmotes); Anim(level == 0 ? 2 : 1, true, 0.4f); Sfx.Button(); Sfx.Ui("Click_WindowSlideOut"); return;
            }
            if (showMenu) Send(it); else if (showEmotes) SendEmote(it);
            Clear();
        }

        // ============================================================ modo en linea (intercambio, etc.) ============================================================
        // Las mismas frases de MENU CHAT, pero dibujadas dentro de la ventana que las pide (coordenadas locales del grupo actual) y enviadas con su propio callback.
        Action<string, int> inlineSend; bool inlineOn; int inlineFrame;
        public bool InlineOpen { get { return inlineOn; } }
        public void OpenInline(Action<string, int> send)
        {
            Styles(); CloseAll(menuList); InitMenuList(); inlineSend = send; inlineOn = true; inlineFrame = Time.frameCount;
        }
        public void CloseInline() { if (!inlineOn) return; inlineOn = false; inlineSend = null; CloseAll(menuList); }
        /// <summary>Dibuja las listas hacia arriba desde origin (esquina inferior izquierda de la primera lista).</summary>
        public void DrawInline(Event e, Vector2 origin)
        {
            if (!inlineOn || e == null) return; Styles(); if (sBtn == null) return;
            var l = menuList; Item o0 = l.Find(i => i.Opened && i.Level == 0); bool inside = false; Item picked = null;
            float x = origin.x, bottom = origin.y - 4f;
            for (int lv = 0; lv < 2; lv++)
            {
                if (lv == 1 && o0 == null) break;
                int parent = lv == 0 ? 0 : o0.Id; int n = 0; float w = 30f;
                foreach (var it in l) { if (it.Parent != parent) continue; w = Mathf.Max(w, W(it.Text) + 20f); n++; }
                if (n == 0) break;
                var box = new Rect(x, bottom - (n * RowH + 11f), w + 18f, n * RowH + 11f);
                if (box.Contains(e.mousePosition)) inside = true;
                if (e.type == EventType.Repaint) Skin.Bg(box, sBox != null ? sBox.N : null, sBox);
                int row = 0;
                foreach (var it in l)
                {
                    if (it.Parent != parent) continue;
                    string text = it.Container ? it.Text + "..." : it.Text;
                    var btn = new Rect(box.x + 4f, box.y + 6f + row * RowH, box.width - 8f, 16f);
                    if (Skin.Button(btn, text, it.Opened ? sLock : sBtn, true, true)) picked = it;
                    row++;
                }
                x += box.width;
            }
            if (picked != null)
            {
                if (picked.Container) { Toggle(picked, l); Sfx.Button(); }
                else { var cb = inlineSend; CloseInline(); Sfx.Button(); if (cb != null) cb(picked.Text, picked.Emote); }
            }
            else if (e.type == EventType.MouseUp && !inside && Time.frameCount != inlineFrame) CloseInline();
        }
    }
}
