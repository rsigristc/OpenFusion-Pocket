// InventoryWindow (parcial): autoapilado de objetos contables (gumballs) y boton SORT. SIN COMPILAR NI PROBAR EN UNITY.
// Todo se hace con REQ_ITEM_MOVE inventario->inventario (el servidor apila si ambos objetos son identicos, contables y ninguno esta lleno; si no, intercambia).
// Cola con espera de ACK (PC_ITEM_MOVE_SUCC): se valida cada movimiento contra el inventario real antes de enviarlo y se aborta la cola si algo cambio o no llega la respuesta (3 s).
// Autoapilado: se detectan por diferencia con una copia del inventario las ranuras que reciben un objeto nuevo por otra via distinta de ITEM_MOVE (recompensa, compra, caja, GIVE_ITEM, correo...).
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    public sealed partial class InventoryWindow
    {
        struct Mv { public int From, To; public short FT, FI, TT, TI; public bool Stack; }
        static readonly ItemBase[] snap = new ItemBase[50];
        static bool snapDirty = true;
        static readonly HashSet<int> changed = new HashSet<int>();
        static readonly List<Mv> plan = new List<Mv>();
        static int planPos;
        static bool inFlight, ackGot, sortRun;
        static Mv flight;
        static float flightT, nextAt, stackAt;
        /// <summary>SORT en curso: bloquea el panel de inventario.</summary>
        public static bool Sorting;
        static readonly Rect SortBut = new Rect(292, 4, 70, 21);

        static bool StackBusy { get { return inFlight || planPos < plan.Count; } }

        /// <summary>Respuesta PC_ITEM_MOVE_SUCC ya aplicada a Items: mantiene la copia y libera la cola.</summary>
        static void OnMoveAck(int ef, int fs, int et, int ts)
        {
            if (ef == 1 && fs >= 0 && fs < 50) snap[fs] = Items[fs];
            if (et == 1 && ts >= 0 && ts < 50) snap[ts] = Items[ts];
            if (inFlight && ef == 1 && et == 1 && ((fs == flight.From && ts == flight.To) || (fs == flight.To && ts == flight.From))) ackGot = true;
        }

        static void AbortQueue()
        {
            plan.Clear(); planPos = 0; inFlight = false; ackGot = false; sortRun = false; Sorting = false; changed.Clear();
            for (int i = 0; i < 50; i++) snap[i] = Items[i];
        }

        static bool Same(ItemBase a, short type, short id) { return a.Id == id && (id <= 0 || a.Type == type); }

        int StackOf(ItemBase it)
        {
            if (it.Id <= 0 || it.Type != 7) return 1;
            var m = ItemTbl.Get(tables, it.Type, it.Id); return m.Ok ? Mathf.Max(1, m.Stack) : 1;
        }

        static void SimStack(ItemBase[] sim, int from, int to, int st)
        {
            int sum = sim[to].Opt + sim[from].Opt;
            if (sum > st) { sim[to].Opt = st; sim[from].Opt = sum - st; }
            else { sim[to].Opt = sum; sim[from] = default(ItemBase); }
        }

        static Mv MakeMv(ItemBase[] sim, int from, int to, bool stack)
        {
            var f = sim[from]; var t = sim[to];
            return new Mv { From = from, To = to, FT = f.Type, FI = f.Id, TT = t.Type, TI = t.Id, Stack = stack };
        }

        /// <summary>Otra pila no llena del mismo objeto (la mas llena); -1 si no hay.</summary>
        static int Partner(ItemBase[] sim, int s, int st)
        {
            var it = sim[s]; int best = -1;
            for (int j = 0; j < 50; j++)
            {
                if (j == s) continue;
                var o = sim[j];
                if (o.Id != it.Id || o.Type != it.Type || o.Opt >= st) continue;
                if (best < 0 || o.Opt > sim[best].Opt) best = j;
            }
            return best;
        }

        bool WindowOk()
        {
            if (NpcUI.Sender == null || tables == null) return false;
            if (Machine) return false;
            var w = GameMenu.Current;
            if (w == GameMenu.Win.Trade || w == GameMenu.Win.EMail) return false;
            if (EnchantWindow.Active || CombineWindow.Active) return false;
            return true;
        }

        bool SortAllowed()
        {
            if (Machine) return false;
            var w = GameMenu.Current;
            return w == GameMenu.Win.MyStuff || w == GameMenu.Win.Vendor || w == GameMenu.Win.Bank;
        }

        void StackTick()
        {
            float now = Time.unscaledTime;
            if (snapDirty)
            {
                for (int i = 0; i < 50; i++) snap[i] = Items[i];
                snapDirty = false; changed.Clear(); plan.Clear(); planPos = 0; inFlight = false; ackGot = false; sortRun = false; Sorting = false;
                return;
            }
            if (sortRun && !WindowOk()) { AbortQueue(); return; }
            if (inFlight)
            {
                if (ackGot) { inFlight = false; ackGot = false; nextAt = now + 0.04f; }
                else if (now - flightT > 3f) { AbortQueue(); return; }
                else return;
            }
            for (int i = 0; i < 50; i++)
            {
                var a = Items[i]; var b = snap[i];
                if (a.Type == b.Type && a.Id == b.Id && a.Opt == b.Opt && a.TimeLimit == b.TimeLimit) continue;
                if (a.Id > 0 && (a.Id != b.Id || a.Type != b.Type) && !sortRun) { changed.Add(i); stackAt = now + 0.3f; }
                snap[i] = a;
            }
            bool idleOk = WindowOk() && !dragging && pendSlot < 0;
            if (!sortRun && planPos >= plan.Count && changed.Count > 0 && now >= stackAt && idleOk) BuildStackPlan();
            if (planPos < plan.Count)
            {
                if (now >= nextAt && (sortRun ? WindowOk() : idleOk)) SendNext();
            }
            else if (plan.Count > 0)
            {
                plan.Clear(); planPos = 0;
                if (sortRun) { sortRun = false; Sorting = false; changed.Clear(); Sfx.Ui("Action_Sucess"); }
            }
        }

        void BuildStackPlan()
        {
            var sim = (ItemBase[])Items.Clone();
            var ids = new List<int>(changed); ids.Sort(); changed.Clear();
            plan.Clear(); planPos = 0;
            for (int k = 0; k < ids.Count && plan.Count < 60; k++)
            {
                int s = ids[k];
                for (int guard = 0; guard < 50 && plan.Count < 60; guard++)
                {
                    var it = sim[s]; if (it.Id <= 0) break;
                    int st = StackOf(it); if (st <= 1 || it.Opt >= st) break;
                    int t = Partner(sim, s, st); if (t < 0) break;
                    plan.Add(MakeMv(sim, s, t, true)); SimStack(sim, s, t, st);
                }
            }
            if (plan.Count > 0) nextAt = Time.unscaledTime;
        }

        void SendNext()
        {
            var m = plan[planPos];
            var f = Items[m.From]; var t = Items[m.To];
            bool ok = Same(f, m.FT, m.FI) && Same(t, m.TT, m.TI);
            if (ok && m.Stack) { int st = StackOf(f); ok = st > 1 && f.Opt < st && t.Opt < st; }
            if (!ok) { AbortQueue(); return; }
            planPos++; flight = m; inFlight = true; ackGot = false; flightT = Time.unscaledTime;
            Send(ClientPackets.ItemMove(1, m.From, 1, m.To));
        }

        static int GroupRank(int type)
        {
            if ((type >= 0 && type <= 6) || type == 10) return 0;
            if (type == 7) return 1;
            if (type == 9) return 2;
            if (type == 8) return 3;
            return 4;
        }
        static int TypeRank(int type) { return type == 10 ? 7 : type; }
        static int CompareItems(ItemBase a, ItemBase b, int ia, int ib)
        {
            int c = GroupRank(a.Type).CompareTo(GroupRank(b.Type)); if (c != 0) return c;
            c = TypeRank(a.Type).CompareTo(TypeRank(b.Type)); if (c != 0) return c;
            c = a.Id.CompareTo(b.Id); if (c != 0) return c;
            c = b.Opt.CompareTo(a.Opt); if (c != 0) return c;
            return ia.CompareTo(ib);
        }

        /// <summary>Boton SORT: primero fusiona pilas, luego ordena (equipo por tipo, generales, cajas, mision; por id) y compacta huecos, simulando la permutacion.</summary>
        void StartSort()
        {
            if (Sorting || StackBusy || !WindowOk()) return;
            var sim = (ItemBase[])Items.Clone(); plan.Clear(); planPos = 0; changed.Clear();
            var seen = new HashSet<int>();
            for (int i = 0; i < 50; i++)
            {
                var it = sim[i]; if (it.Id <= 0 || it.Type != 7) continue;
                if (!seen.Add(it.Id)) continue;
                int st = StackOf(it); if (st <= 1) continue;
                for (int guard = 0; guard < 60 && plan.Count < 150; guard++)
                {
                    int lo = -1, hi = -1;
                    for (int j = 0; j < 50; j++)
                    {
                        var o = sim[j]; if (o.Id != it.Id || o.Type != 7 || o.Opt >= st) continue;
                        if (lo < 0 || o.Opt <= sim[lo].Opt) lo = j;
                    }
                    if (lo < 0) break;
                    for (int j = 0; j < 50; j++)
                    {
                        if (j == lo) continue;
                        var o = sim[j]; if (o.Id != it.Id || o.Type != 7 || o.Opt >= st) continue;
                        if (hi < 0 || o.Opt > sim[hi].Opt) hi = j;
                    }
                    if (hi < 0) break;
                    plan.Add(MakeMv(sim, lo, hi, true)); SimStack(sim, lo, hi, st);
                }
            }
            var orig = (ItemBase[])sim.Clone();
            var occ = new List<int>();
            for (int i = 0; i < 50; i++) if (sim[i].Id > 0) occ.Add(i);
            occ.Sort((x, y) => CompareItems(orig[x], orig[y], x, y));
            var cur = new int[50];
            for (int i = 0; i < 50; i++) cur[i] = sim[i].Id > 0 ? i : -1;
            for (int i = 0; i < occ.Count; i++)
            {
                int want = occ[i]; if (cur[i] == want) continue;
                int j = -1;
                for (int k = i + 1; k < 50; k++) if (cur[k] == want) { j = k; break; }
                if (j < 0) break;
                plan.Add(MakeMv(sim, j, i, false));
                var tmp = sim[i]; sim[i] = sim[j]; sim[j] = tmp;
                int tc = cur[i]; cur[i] = cur[j]; cur[j] = tc;
            }
            if (plan.Count == 0) { Sfx.Ui("No_Button"); return; }
            Sorting = true; sortRun = true; nextAt = Time.unscaledTime; Sfx.Button();
        }

        /// <summary>Usar / abrir el objeto de una ranura desde otra ventana (p. ej. OPEN de un cofre en la tienda).</summary>
        public void UseItemPub(int slot) { if (slot >= 0 && slot < 50 && Items[slot].Id > 0) Use(slot); }

        /// <summary>Velo y texto de progreso sobre la rejilla mientras se aplica SORT (coordenadas del grupo Panel).</summary>
        void DrawSortingBanner(bool rep, SkinStyle lab)
        {
            if (!Sorting || !rep) return;
            var oc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(Content.x, Content.y, Content.width, Content.height), Texture2D.whiteTexture); GUI.color = oc;
            if (lab != null) Skin.Label(new Rect(Content.x, Content.y + 220f, Content.width, 30f), Loc.T("SORTING") + "... " + planPos + "/" + plan.Count, Center(lab), Color.white);
        }
    }
}
