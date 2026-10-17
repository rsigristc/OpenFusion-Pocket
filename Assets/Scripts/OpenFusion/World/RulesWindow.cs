// RulesWindow: port de cnRule (ventana "RULES" de los NPC con icono 23, NpcIconMode.StartRule). SIN COMPILAR NI PROBAR EN UNITY.
// Fuente original: Assembly-CSharp/cnRule.cs, NpcIconMode (iRuleIndex: m_iServiceNumber 10 -> 2, 9 -> 1, m_iMapIcon 12 -> 3, si no 0 = sin boton).
// Datos: XdtTableScript.m_pRulesTable (TableContainer 32): m_pRulesData[ruleIndex] {m_iString, m_iImage, m_iPrev, m_iNext}; m_pRulesString[m_iString + 0..6].m_strName = textos
//   (0 titulo, 1 subtitulo 1, 2 subtitulo 2, 3 contenido 1, 4 subtitulo 3, 5 contenido 2, 6 comentario final); m_pRulesString[m_iImage + 0..3].m_strComment = nombre de "Icons/<nombre>.png".
//   PREVIOUS / NEXT recargan la pagina m_iPrev / m_iNext (indices de RulesTable) si son > 0.
// Skins: 1381 (CyanTitle, BlueTitle, BlueText, BlueTitleMid, BlueButton) como cnRule.pSkin; 1366 closebut como InventoryManager.FFGuiSkin.
// Texturas (TexLoader del bundle): TutorialAssets/panelback, CharacterCreationAssets/RuleBack, Icons/<nombre>. Ventana de 1020x638 centrada, nunca escalada por encima de 1.
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class RulesWindow : MonoBehaviour
    {
        public static RulesWindow Instance;
        public static RulesWindow Ensure(GameObject host) { var w = host.GetComponent<RulesWindow>(); if (w == null) w = host.AddComponent<RulesWindow>(); Instance = w; return w; }
        /// <summary>Ventana abierta: bloquea clics al mundo y movimiento (anadir a GameMenu.Blocking / busyPrev).</summary>
        public static bool Active { get { return Instance != null && Instance.open; } }

        /// <summary>Carga de texturas del bundle ("TutorialAssets/panelback" + ext). Imprescindible para las imagenes de las reglas.</summary>
        public Func<string, Texture> TexLoader;

        const int SkRule = 1381;
        static readonly Rect WindowRect = new Rect(0, 0, 1020, 638), FrameRect = new Rect(10, 50, 980, 540), ExitButton = new Rect(987, 0, 30, 30), BackRect = new Rect(500, 605, 80, 25),
            RuleBackRect = new Rect(-6, 42, 1034, 551), TitleR = new Rect(15, -10, 500, 60), SubTitle1 = new Rect(0, -35, 500, 100), SubTitle2 = new Rect(130, 25, 100, 50), Content1 = new Rect(130, 60, 380, 300),
            SubTitle3 = new Rect(560, 250, 200, 100), Content2 = new Rect(560, 320, 320, 320), LastComment = new Rect(0, 505, 1000, 50),
            PrevRect = new Rect(380, 605, 100, 25), NextRect = new Rect(600, 605, 100, 25);
        static readonly Rect[] ImgRect = { new Rect(15, 40, 104, 91), new Rect(0, 150, 542, 361), new Rect(550, 0, 452, 282), new Rect(895, 305, 103, 201) };

        XdtTableScript tables;
        bool open; int iPrev, iNext; readonly string[] strRule = new string[7]; readonly string[] imgName = new string[4]; readonly Texture[] imgTex = new Texture[4];
        Texture backPanel, ruleBack; bool texTried; float texRetry;

        public void Init(XdtTableScript t) { tables = t; }
        public string Title(int ruleIndex) { return TitleOf(tables, ruleIndex); }
        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        // ============================================================ utilidades para NpcUI ============================================================
        /// <summary>NpcIconMode: indice de reglas del NPC segun m_iServiceNumber (10 -> 2, 9 -> 1) y m_iMapIcon (12 -> 3); 0 = el NPC no tiene boton RULES.</summary>
        public static int RuleIndexFor(int serviceNumber, int mapIcon)
        {
            if (serviceNumber == 10) return 2; if (serviceNumber == 9) return 1; if (mapIcon == 12) return 3; return 0;
        }

        /// <summary>Titulo (texto del boton RULES): m_pRulesString[RulesData[idx].m_iString].m_strName. null si no hay datos.</summary>
        public static string TitleOf(XdtTableScript t, int ruleIndex)
        {
            var rt = t != null ? t.m_pRulesTable : null; if (rt == null || rt.m_pRulesData == null || rt.m_pRulesString == null) return null;
            if (ruleIndex <= 0 || ruleIndex >= rt.m_pRulesData.Length || rt.m_pRulesData[ruleIndex] == null) return null;
            int si = rt.m_pRulesData[ruleIndex].m_iString; if (si < 0 || si >= rt.m_pRulesString.Length || rt.m_pRulesString[si] == null) return null;
            return rt.m_pRulesString[si].m_strName;
        }

        // ============================================================ apertura (cnRule.ReceiveInit) ============================================================
        /// <summary>NpcIconMode.StartRule: evento (2,3,0) con iRuleIndex (1, 2 o 3).</summary>
        public void Open(int ruleIndex)
        {
            if (open) return; if (!Load(ruleIndex)) { Debug.LogWarning("[reglas] sin datos para el indice " + ruleIndex); return; }
            texTried = false; texRetry = 0f; LoadTextures(); open = true; Sfx.Ui("Open_Screen");
        }

        bool Load(int idx)
        {
            var rt = tables != null ? tables.m_pRulesTable : null; if (rt == null || rt.m_pRulesData == null || rt.m_pRulesString == null) return false;
            if (idx < 0 || idx >= rt.m_pRulesData.Length || rt.m_pRulesData[idx] == null) return false;
            var val = rt.m_pRulesData[idx]; iPrev = val.m_iPrev; iNext = val.m_iNext;
            for (int i = 0; i < 7; i++) { int k = val.m_iString + i; strRule[i] = k >= 0 && k < rt.m_pRulesString.Length && rt.m_pRulesString[k] != null ? rt.m_pRulesString[k].m_strName : ""; }
            for (int j = 0; j < 4; j++)
            {
                int k = val.m_iImage + j; imgName[j] = k >= 0 && k < rt.m_pRulesString.Length && rt.m_pRulesString[k] != null ? rt.m_pRulesString[k].m_strComment : null; imgTex[j] = null;
            }
            texTried = false; texRetry = 0f; return true;
        }

        void Exit(bool sound)
        {
            if (!open) return; open = false; backPanel = ruleBack = null; for (int i = 0; i < 4; i++) imgTex[i] = null; texTried = false;
            if (sound) Sfx.Ui("Close_Screen");
        }

        void LoadTextures()
        {
            if (TexLoader == null || Time.unscaledTime < texRetry) return; texRetry = Time.unscaledTime + 2f;
            if (backPanel == null) backPanel = LoadTex("TutorialAssets/panelback");
            if (ruleBack == null) ruleBack = LoadTex("CharacterCreationAssets/RuleBack");
            for (int i = 0; i < 4; i++) if (imgTex[i] == null && !string.IsNullOrEmpty(imgName[i])) imgTex[i] = LoadTex("Icons/" + imgName[i]);
        }
        Texture LoadTex(string path)
        {
            foreach (var ext in new[] { ".png", ".psd", ".dds", ".tga", "" }) { try { var t = TexLoader(path + ext); if (t != null) return t; } catch (Exception) { } }
            return null;
        }
        bool AllLoaded() { if (backPanel == null || ruleBack == null) return false; for (int i = 0; i < 4; i++) if (imgTex[i] == null && !string.IsNullOrEmpty(imgName[i])) return false; return true; }

        void Update()
        {
            if (!open) return;
            if (!AllLoaded()) LoadTextures();
            if (Input.GetKeyDown(KeyCode.Escape)) Exit(true);
        }

        // ============================================================ dibujo (cnRule.OnGUI) ============================================================
        static SkinStyle St(string n) { return Skin.Get(SkRule, n); }
        static void Pic(Rect r, Texture t) { if (t != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); }

        void OnGUI()
        {
            if (!open || Screen.height <= 0) return; var e = Event.current; if (e == null) return;
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(1f, GameOptions.UiScale, W / 1040f, H / 660f); if (Sc < 0.3f) Sc = 0.3f;
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1500; GUI.color = Color.white;
            try
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(W * 0.5f, H * 0.5f, 0f), Quaternion.identity, new Vector3(Sc, Sc, 1f));
                if (e.type == EventType.Repaint)
                {
                    GUI.color = new Color(0f, 0f, 0f, backPanel != null ? 1f : 0.75f); GUI.DrawTexture(new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc), Texture2D.whiteTexture); GUI.color = Color.white;
                    if (backPanel != null) GUI.DrawTexture(new Rect(-960f, -720f, 1920f, 1440f), backPanel, ScaleMode.ScaleToFit);
                }
                GUI.BeginGroup(new Rect(-WindowRect.width * 0.5f, -WindowRect.height * 0.5f, WindowRect.width, WindowRect.height));
                Pic(RuleBackRect, ruleBack);
                Skin.Label(TitleR, strRule[0], St("CyanTitle"));
                GUI.BeginGroup(FrameRect);
                Skin.Label(SubTitle1, strRule[1], St("BlueTitleMid"));
                Skin.Label(SubTitle2, strRule[2], St("BlueTitle"));
                Skin.Label(Content1, strRule[3], St("BlueText"));
                Skin.Label(SubTitle3, strRule[4], St("BlueTitle"));
                Skin.Label(Content2, strRule[5], St("BlueText"));
                Skin.Label(LastComment, strRule[6], St("BlueTitleMid"));
                for (int i = 0; i < 4; i++) Pic(ImgRect[i], imgTex[i]);
                GUI.EndGroup();
                if (Skin.Button(ExitButton, "", Skin.Get(Skin.Inven, "closebut"), true, true)) { Exit(true); GUI.EndGroup(); return; }
                if (Skin.Button(BackRect, Loc.T("BACK"), St("BlueButton"), true, true)) { Sfx.Button(); Exit(false); GUI.EndGroup(); return; }
                if (iPrev > 0 && Skin.Button(PrevRect, Loc.T("PREVIOUS"), St("BlueButton"), true, true)) { Sfx.Button(); Load(iPrev); }
                else if (iNext > 0 && Skin.Button(NextRect, Loc.T("NEXT"), St("BlueButton"), true, true)) { Sfx.Button(); Load(iNext); }
                GUI.EndGroup();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }
    }
}
