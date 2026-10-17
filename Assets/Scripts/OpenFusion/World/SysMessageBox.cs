// SysMessageBox: caja de mensajes modal del sistema (cnSystemMessageManager.SendSystemMessageBox). SIN COMPILAR NI PROBAR EN UNITY.
//  - Texto = fila numerada de la tabla de mensajes (MessageElement.m_szString) con AvatarUtil.PrintF (%s / %d sustituidos en orden por los argumentos).
//  - Botones segun m_iButtonType: 0/1 OKAY; 2 OKAY + CANCEL; 3 YES + NO; 10 -> como 1 y 11 -> como 2 (con sonido de fallo Action_Failure01/02); resto: OKAY.
//    Primer boton = funcion 1 (ok), segundo = funcion 2 (cancel), como CallFuncName[0] / [1].
//  - Mientras hay una caja abierta el resto de la interfaz queda bloqueado (GUI.enabled = false en el original): SysMessageBox.Active.
//  - Las cajas se apilan: se muestra una por vez (la mas antigua primero).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class SysMessageBox : MonoBehaviour
    {
        public static SysMessageBox Instance;
        public static SysMessageBox Ensure(GameObject host) { var s = host.GetComponent<SysMessageBox>(); if (s == null) s = host.AddComponent<SysMessageBox>(); Instance = s; return s; }
        /// <summary>cnSystemMessageManager.IsSysPopUp().</summary>
        public static bool Active { get { return Instance != null && Instance.list.Count > 0; } }
        /// <summary>cnSystemMessageManager.SetFocusOUt: con true las cajas se dibujan con GUI.enabled = false (botones sin respuesta) mientras espera la respuesta de un warp.</summary>
        public static bool FocusOut;

        sealed class Item { public string Text; public int Btn; public Action Ok, Cancel; }
        readonly List<Item> list = new List<Item>();
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        MessageTableScript msgT;

        public void Init(XdtTableScript t) { msgT = t != null ? t.m_pMessageTable : null; }
        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Texto de la tabla de mensajes ("(message N)" si no existe).</summary>
        public static string Message(int id)
        {
            var t = Instance != null ? Instance.msgT : null;
            if (t != null && t.m_pMessageData != null && id >= 0 && id < t.m_pMessageData.Length && t.m_pMessageData[id] != null) return t.m_pMessageData[id].m_szString ?? "";
            return "(message " + id + ")";
        }
        static int ButtonOf(int id)
        {
            var t = Instance != null ? Instance.msgT : null;
            if (t != null && t.m_pMessageData != null && id >= 0 && id < t.m_pMessageData.Length && t.m_pMessageData[id] != null) return t.m_pMessageData[id].m_iButtonType;
            return 1;
        }

        /// <summary>AvatarUtil.PrintF: cada argumento sustituye al primer %d / %s que aparezca (el que este antes).</summary>
        public static string PrintF(string s, params string[] args)
        {
            if (s == null) return "";
            if (args == null) return s;
            foreach (string a in args)
            {
                int d = s.IndexOf("%d"), p = s.IndexOf("%s"); int at;
                if (d >= 0 && p >= 0) at = Math.Min(d, p); else if (d >= 0) at = d; else if (p >= 0) at = p; else break;
                s = s.Substring(0, at) + (a ?? "") + s.Substring(at + 2);
            }
            return s;
        }

        /// <summary>SendSystemMessageBox(iStrNum, funcName1, funcName2, args). ok = primer boton; cancel = segundo boton (NO / CANCEL).</summary>
        public static void Show(int id, Action ok, Action cancel, params string[] args)
        {
            if (Instance == null) { Debug.LogWarning("[sysmsg] sin inicializar: mensaje " + id); return; }
            int bt = ButtonOf(id); string txt = PrintF(Loc.T(Message(id)), args);
            Instance.q.Enqueue(() => Instance.Add(txt, bt, ok, cancel));
        }
        /// <summary>SendSystemMessageBox(Type, ..., outstring): texto fijo. type 1 = OK, 2 = OK/CANCEL, 3 = YES/NO.</summary>
        public static void ShowText(string text, int type, Action ok, Action cancel)
        {
            if (Instance == null) return; string txt = text ?? "";
            Instance.q.Enqueue(() => Instance.Add(txt, type, ok, cancel));
        }

        void Add(string text, int bt, Action ok, Action cancel)
        {
            if (bt == 10 || bt == 11) { bt = bt == 10 ? 1 : 2; Sfx.Ui("Action_Failure0" + UnityEngine.Random.Range(1, 3)); }
            list.Add(new Item { Text = text, Btn = bt, Ok = ok, Cancel = cancel });
            Sfx.Ui("Affirmation_Screen_Popup");
        }

        void Update()
        {
            Action a; int n = 0; while (n++ < 16 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[sysmsg] " + ex.Message); } }
            if (list.Count > 0 && !FocusOut && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Resolve(list[0], true);
        }

        void Resolve(Item it, bool first)
        {
            if (!list.Remove(it)) return;
            Sfx.Ui(first ? "Yes_Button" : "No_Button");
            var cb = first ? it.Ok : it.Cancel;
            if (cb != null) { try { cb(); } catch (Exception ex) { Debug.LogWarning("[sysmsg] callback: " + ex.Message); } }
        }

        static void Panel(Rect r)
        {
            GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = new Color(0f, 0.8f, 0.9f, 1f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture); GUI.color = Color.white;
        }

        void OnGUI()
        {
            if (list.Count == 0 || Screen.height <= 0) return; var e = Event.current; if (e == null) return;
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f);
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1600;
            try
            {
                var it = list[0];
                var red = Skin.Get(Skin.Menu, "RedButton") ?? Skin.Get(Skin.Inven, "RedButton"); var cancel = Skin.Get(Skin.Menu, "CancelButton") ?? Skin.Get(Skin.Inven, "CancelButton"); var lab = Skin.Get(Skin.Inven, "label");
                if (e.type == EventType.Repaint) { GUI.color = new Color(0f, 0f, 0f, 0.75f); GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }
                GUI.matrix = Matrix4x4.TRS(new Vector3(W * 0.5f, H * 0.5f, 0f), Quaternion.identity, new Vector3(Sc, Sc, 1f));
                var r = new Rect(-260f, -82f, 520f, 164f);
                if (e.type == EventType.Repaint) Panel(r);
                GUI.BeginGroup(r);
                string[] lines = (it.Text ?? "").Split('\n');
                if (lines.Length >= 2)
                {   // primera linea en cian (titulo), resto como texto
                    Skin.Label(new Rect(40, 14, 440, 24), lines[0], lab, new Color(0f, 1f, 1f));
                    Skin.Label(new Rect(40, 38, 440, 80), string.Join("\n", lines, 1, lines.Length - 1), lab, Color.white);
                }
                else Skin.Label(new Rect(40, 20, 440, 98), it.Text, lab, Color.white);
                bool two = it.Btn == 2 || it.Btn == 3;
                string t1 = it.Btn == 3 ? Loc.T("YES") : Loc.T("OKAY"), t2 = it.Btn == 3 ? Loc.T("NO") : Loc.T("CANCEL");
                if (two)
                {
                    if (Skin.Button(new Rect(44, 124, 150, 25), t2, cancel, !FocusOut)) { GUI.EndGroup(); Resolve(it, false); return; }
                    if (Skin.Button(new Rect(334, 124, 150, 25), t1, red, !FocusOut)) { GUI.EndGroup(); Resolve(it, true); return; }
                }
                else if (Skin.Button(new Rect(185, 124, 150, 25), t1, red, !FocusOut)) { GUI.EndGroup(); Resolve(it, true); return; }
                GUI.EndGroup();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();   // modal: nada llega al resto de la interfaz
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }
    }
}
