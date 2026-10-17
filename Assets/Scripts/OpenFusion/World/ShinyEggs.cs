// Huevos de Coco (cnShinyContainer / cnShinyController / cnAvatarAttack.CheckShiny): aparecen con SHINY_ENTER/NEW/AROUND, desaparecen con SHINY_EXIT/AROUND_DEL_SHINY
// y se recogen al acercarse a <= 2*fShinyRadius (1 m): REQ_SHINY_PICKUP + efecto (m_iGetEffect o 397) y se borran localmente.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class ShinyEggs : MonoBehaviour
    {
        public static ShinyEggs Instance;
        public float PickupRadius = 1f;
        sealed class Egg { public int Id, Type; public GameObject Go; public ShinyElement Row; }
        readonly Dictionary<int, Egg> eggs = new Dictionary<int, Egg>();
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        WorldEntities ents; ModelFactory models; ModelCatalog catalog; XdtTableScript tables; PacketDispatcher disp; Transform root; int logN;
        float lastReq;

        public void Init(WorldEntities e, PacketDispatcher d, XdtTableScript t, ModelFactory m, ModelCatalog c, Transform parent)
        {
            Instance = this; ents = e; tables = t; models = m; catalog = c; root = parent != null ? parent : transform;
            if (disp != null) disp.Unhandled -= OnPacket; disp = d; if (d != null) d.Unhandled += OnPacket;
        }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; if (Instance == this) Instance = null; }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }

        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if ((type == PacketIds.P_FE2CL_SHINY_ENTER || type == PacketIds.P_FE2CL_SHINY_NEW) && b.Length >= 24) { var c = b; q.Enqueue(() => Add(c, 0)); }
            else if (type == PacketIds.P_FE2CL_SHINY_AROUND && b.Length >= 4)
            {   // sP_FE2CL_SHINY_AROUND {iShinyCnt} + sShinyAppearanceData[] (24 B)
                var c = b; q.Enqueue(() => { int n = I(c, 0); for (int i = 0; i < n && 4 + 24 * (i + 1) <= c.Length; i++) Add(c, 4 + 24 * i); });
            }
            else if (type == PacketIds.P_FE2CL_SHINY_EXIT && b.Length >= 4) { int id = I(b, 0); q.Enqueue(() => Del(id)); }
            else if (type == PacketIds.P_FE2CL_AROUND_DEL_SHINY && b.Length >= 4)
            {   // {iShinyCnt} + int[]
                var c = b; q.Enqueue(() => { int n = I(c, 0); for (int i = 0; i < n && 4 + 4 * (i + 1) <= c.Length; i++) Del(I(c, 4 + 4 * i)); });
            }
        }

        void Add(byte[] b, int o)
        {
            int id = I(b, o), type = I(b, o + 4), x = I(b, o + 12), y = I(b, o + 16), z = I(b, o + 20);   // iShiny_ID, iShinyType, iMapNum, iX, iY, iZ
            Del(id);
            ShinyElement row = null; var st = tables != null ? tables.m_pShinyTable : null;
            if (st != null && st.m_pShinyData != null) foreach (var e in st.m_pShinyData) if (e != null && e.m_iShinyID == type) { row = e; break; }
            var go = new GameObject("Shiny#" + id + " t" + type); go.transform.SetParent(root, false);
            var p = PlayerAvatarFactory.ToClient(x, y, z); float gy = Ground.Y(p); if (!float.IsNaN(gy) && !float.IsInfinity(gy)) p.y = gy; go.transform.position = p;
            GameObject model = null; var mr = catalog != null ? catalog.Shiny(type) : null;
            try { if (models != null && mr != null) model = models.Instantiate(mr, go.transform); } catch (Exception ex) { Debug.LogWarning("[huevo] modelo: " + ex.Message); }
            if (model != null) { WorldEntities.ActivateAll(model); model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; }
            else
            {   // sin modelo disponible: esfera luminosa para que se vea y se pueda recoger
                var sp = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(sp.GetComponent<Collider>()); sp.transform.SetParent(go.transform, false); sp.transform.localPosition = Vector3.up * 0.3f; sp.transform.localScale = Vector3.one * 0.5f;
                var r = sp.GetComponent<Renderer>(); var sh = Shader.Find("Sprites/Default"); if (sh != null) r.material = new Material(sh); r.material.color = new Color(1f, 0.9f, 0.3f);
            }
            eggs[id] = new Egg { Id = id, Type = type, Go = go, Row = row };
            if (logN++ < 6) Debug.Log("[huevo] id=" + id + " tipo=" + type + " modelo=" + (mr != null ? mr.Model : "?") + (model == null ? " (SIN MODELO)" : "") + " pos=" + p);
        }
        void Del(int id) { Egg e; if (eggs.TryGetValue(id, out e)) { eggs.Remove(id); if (e.Go != null) Destroy(e.Go); } }

        readonly List<int> picked = new List<int>();
        void Update()
        {
            Action a; while (q.TryDequeue(out a)) a();
            if (eggs.Count == 0 || ents == null || ents.Self == null) return;
            var sp = ents.Self.transform.position; picked.Clear();
            foreach (var kv in eggs) { var e = kv.Value; if (e.Go == null) continue; if ((e.Go.transform.position - sp).magnitude <= PickupRadius) picked.Add(kv.Key); }
            foreach (var id in picked)
            {
                var e = eggs[id]; int eff = e.Row != null && e.Row.m_iGetEffect > 0 ? e.Row.m_iGetEffect : 397;
                if (BulletFx.Instance != null) BulletFx.Instance.Spawn(eff, e.Go.transform.position, e.Go.transform.rotation, 1f, null, 3f);
                Sfx.Ui("Egg_Pickup");   // SoundUtil.Playsound("Egg_Pickup")
                if (NpcUI.Sender != null) NpcUI.Sender(ClientPackets.ShinyPickup(id));
                Debug.Log("[huevo] recogido id=" + id + " efecto=" + eff);
                Del(id);
            }
        }
    }
}
