// Números de daño/curación flotantes. Réplica de FloatingPoint.cs + BulletContainer del cliente original (GUI.depth 12, gated por displayOption.bFloatNumber).
//  Tipos: 0 daño, 1 DoT, 2 curación, 3 daño de skill, 4/5 corrupción (gana/pierde), 6 mega daño, 7 crítico.
//  Dueño: 0 = el objetivo es un jugador, 1 = un monstruo.
// SIN COMPILAR EN UNITY.
using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class FloatingPoints : MonoBehaviour
    {
        public const int Damage = 0, Dot = 1, Heal = 2, SkillDamage = 3, CorruptWin = 4, CorruptLose = 5, Mega = 6, Critical = 7;
        static readonly Color[] ColOwner0 = { new Color(1f, 0f, 0f), new Color(1f, 0f, 0f), new Color(0.2f, 0.9f, 0f), new Color(1f, 1f, 1f), new Color(0.2f, 0.9f, 0f), new Color(1f, 0f, 0f), new Color(1f, 0.5f, 0.5f), new Color(1f, 0f, 0f) };
        static readonly Color[] ColOwner1 = { new Color(0.2f, 0.4f, 1f), new Color(0.2f, 0.4f, 1f), new Color(0.2f, 0.9f, 0f), new Color(0f, 0.8f, 1f), new Color(0.2f, 0.9f, 0f), new Color(1f, 0f, 0f), new Color(0.2f, 0.4f, 1f), new Color(1f, 0.6f, 0f) };
        static readonly float[] MaxTime = { 1.5f, 1f, 1f, 2f, 1f, 1f, 1f, 2f };

        sealed class Pt { public Vector3 Pos; public int Owner, Type; public string Text; public float T0; }
        static readonly List<Pt> pts = new List<Pt>();
        static FloatingPoints inst;
        BitmapFont font; bool fontTried;

        public static void Ensure(GameObject host) { if (inst == null) inst = host.GetComponent<FloatingPoints>() ?? host.AddComponent<FloatingPoints>(); }

        /// <summary>FloatingPoint.Text: curación (2): sin popup si valor <= 0, "+N"; DoT (1): <= -2 "Protected", si no "-N"; resto: -3 vacío, -2 Protected, -1 Miss, 0 Block, N número.</summary>
        public static string TextFor(int type, int v)
        {
            if (type == Heal) return v <= 0 ? null : "+" + v;
            if (type == Dot) return v <= -2 ? "Protected" : "-" + v;
            if (v == -3) return null; if (v == -2) return "Protected"; if (v == -1) return "Miss"; if (v == 0) return "Block"; return v.ToString();
        }

        /// <param name="top">pies + altura del objetivo (CharacterController.height / NpcMoveController.fHeight)</param>
        public static void Add(Vector3 top, int owner, int type, int value)
        {
            if (!GameOptions.Cur.FloatNumber) return;
            if (type < 0 || type > 7) type = 0; string t = TextFor(type, value); if (t == null) return;
            pts.Add(new Pt { Pos = top, Owner = owner, Type = type, Text = t, T0 = Time.time });
            if (pts.Count > 80) pts.RemoveAt(0);
        }
        public static void AddOn(GameObject go, int owner, int type, int value, float height)
        {
            if (go == null) return; var cc = go.GetComponent<CharacterController>(); float h = cc != null ? cc.height : height;
            Add(go.transform.position + Vector3.up * h, owner, type, value);
        }

        void Update() { for (int i = pts.Count - 1; i >= 0; i--) if (Time.time - pts[i].T0 > MaxTime[pts[i].Type]) pts.RemoveAt(i); }

        void OnGUI()
        {
            if (GameMenu.HudHidden) return;
            var ev = Event.current; if (ev == null || ev.type != EventType.Repaint || pts.Count == 0) return;
            if (!GameOptions.Cur.FloatNumber) { pts.Clear(); return; }
            var cam = Camera.main; if (cam == null) return;
            if (!fontTried) { fontTried = true; font = BitmapFontLoader.Load("JEFFE___14__903"); }
            if (font == null) return;
            float sc = GameOptions.UiScale; int od = GUI.depth; GUI.depth = 12; var om = GUI.matrix;
            try
            {
                foreach (var p in pts)
                {
                    var sp = cam.WorldToScreenPoint(p.Pos); if (sp.z <= 0.1f) continue;
                    float t = Time.time - p.T0, mt = MaxTime[p.Type];
                    float x = sp.x / sc - p.Text.Length * 4f, y = (Screen.height - sp.y) / sc;
                    if (p.Type == Heal) y += t * 40f;
                    else if (p.Type == Dot) y -= t * 40f;
                    else { float n3 = -2f + 3f * t / mt; y += n3 * t * 40f; x += (p.Owner == 1 ? 1f : -1f) * t * 40f; }
                    GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
                    var r = new Rect(x, y, 100f, 24f); var col = (p.Owner == 1 ? ColOwner1 : ColOwner0)[p.Type];
                    font.Draw(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), p.Text, TextAnchor.MiddleLeft, false, new Color(0f, 0f, 0f, 0.8f), null);
                    font.Draw(r, p.Text, TextAnchor.MiddleLeft, false, col, null);
                }
            }
            finally { GUI.matrix = om; GUI.depth = od; GUI.color = Color.white; }
        }
    }
}
