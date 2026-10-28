// Vuelo en escoba/Monkey (eTT 2). Original: cnOwnAvatarStatus/Broomstick: REP_PC_RIDING_SUCC {iPC_ID, eRT}, PC_RIDING {iPC_ID, eRT}, PC_BROOMSTICK_MOVE {iPC_ID, iToX, iToY, iToZ, iSpeed}.
// Original Monkey attachment, embedded animation and mount poses; movement follows server waypoints.
using System;
using System.Collections.Concurrent;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class BroomFlight : MonoBehaviour
    {
        public static bool Active;   // GameClient no mueve ni envía movimiento mientras sea true
        WorldEntities ents; PacketDispatcher disp; readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
        Vector3 target, lastSelf; float speed; bool moving;
        public Func<ModelFactory> GetModels;
        readonly System.Collections.Generic.Dictionary<int, GameObject> brooms = new System.Collections.Generic.Dictionary<int, GameObject>(); readonly System.Collections.Generic.Dictionary<int, float> retry = new System.Collections.Generic.Dictionary<int, float>();
        readonly System.Collections.Generic.HashSet<int> mounted = new System.Collections.Generic.HashSet<int>();
        readonly System.Collections.Generic.Dictionary<int,string> mountClips = new System.Collections.Generic.Dictionary<int,string>();
        static Transform FindDeep(Transform root, string name) { if (root.name == name) return root; for (int i = 0; i < root.childCount; i++) { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; } return null; }
        void SetMounted(int id, bool on)
        {
            var tr = ents?.TransformOfPlayer(id); var animator = tr != null ? tr.GetComponent<NpcAnimator>() : null;
            if (animator != null) animator.External = on;
            if (!on) mountClips.Remove(id);
            if (on) mounted.Add(id); else { mounted.Remove(id); GameObject g; if (brooms.TryGetValue(id, out g)) { if (g != null) Destroy(g); brooms.Remove(id); } retry.Remove(id); }
        }
        /// LoadBroomMonkey: wear/item_broom_01.nif en el hueso "Bip01 Broomstick" (rotado 90 en X) y animacion mount1/mount2 del jugador.
        void TickMonkey()
        {
            if (ents == null) return;
            foreach (var id in mounted)
            {
                var tr = ents.TransformOfPlayer(id); if (tr == null) continue;
                var an = tr.GetComponentInChildren<Animation>(true);
                if (an != null)
                {
                    var animator = tr.GetComponent<NpcAnimator>(); if (animator != null) animator.External = true;
                    mountClips.TryGetValue(id, out var current);
                    if (current == null || an[current] == null || !an.IsPlaying(current) || an[current].normalizedTime >= 1)
                    {
                        string clip = current != null && an["mount2"] != null && UnityEngine.Random.Range(0,2)==1 ? "mount2" : an["mount1"] != null ? "mount1" : null;
                        if (clip != null) { an[clip].wrapMode = WrapMode.Once; an[clip].time = 0; an.CrossFade(clip,.3f); mountClips[id] = clip; }
                    }
                }
                GameObject g; brooms.TryGetValue(id, out g); if (g != null) continue;
                float nr; if (retry.TryGetValue(id, out nr) && Time.unscaledTime < nr) continue; retry[id] = Time.unscaledTime + 2f;
                try
                {
                    var mf = GetModels != null ? GetModels() : null; if (mf == null) continue; var rf = mf.Locate("wear/item_broom_01.nif"); if (!rf.Ok) continue;
                    var go = mf.Builder.BuildPrefabAt(rf, null); if (go == null) continue; WorldEntities.ActivateAll(go);
                    var bone = FindDeep(tr, "Bip01 Broomstick"); go.transform.SetParent(bone != null ? bone : tr, false);
                    go.transform.localPosition = Vector3.zero; go.transform.localRotation = bone != null ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity; go.transform.localScale = Vector3.one;
                    // Legacy instantiates at world scale before parenting. The
                    // actual Broomstick bone is scaled to .36, so local scale 1
                    // shrank the Monkey into the rider. Maintain world scale
                    // after animated bone transforms, just as for hoverboards.
                    if (bone != null) go.AddComponent<VehicleWorldScale>().ApplyScale();
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = tr.gameObject.layer;
                    foreach (var rd in go.GetComponentsInChildren<Renderer>(true)) rd.enabled = true;
                    foreach (var a2 in go.GetComponentsInChildren<Animation>(true)) { a2.cullingType = AnimationCullingType.AlwaysAnimate; string first = null; foreach (AnimationState st in a2) { st.wrapMode = WrapMode.Loop; if(first==null)first=st.name; } if(first!=null)a2.Play(first); }
                    brooms[id] = go; Debug.Log("[monkey] modelo montado en " + (bone != null ? bone.name : "raiz") + " scale=" + go.transform.lossyScale + " renderers=" + go.GetComponentsInChildren<Renderer>(true).Length);
                }
                catch (Exception ex) { Debug.LogWarning("[monkey] " + ex.Message); }
            }
        }

        public static BroomFlight Ensure(GameObject host) { var b = host.GetComponent<BroomFlight>(); if (b == null) b = host.AddComponent<BroomFlight>(); return b; }
        void ClearMounts() { foreach(int id in new System.Collections.Generic.List<int>(mounted))SetMounted(id,false); retry.Clear(); mountClips.Clear(); while(queue.TryDequeue(out _)){} Active=false; moving=false; }
        public void Init(WorldEntities e, PacketDispatcher d) { if(disp!=null)disp.Unhandled-=OnPacket; ClearMounts(); ents = e; disp = d; if (disp != null) disp.Unhandled += OnPacket; }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; ClearMounts(); }
        static int I(byte[] b, int o) => b.Length >= o + 4 ? BitConverter.ToInt32(b, o) : 0;
        void OnPacket(uint type, byte[] b)
        {
            if (type != PacketIds.P_FE2CL_REP_PC_RIDING_SUCC && type != PacketIds.P_FE2CL_PC_RIDING && type != PacketIds.P_FE2CL_PC_BROOMSTICK_MOVE) return;
            queue.Enqueue(() => Handle(type, b));
        }
        void Handle(uint type, byte[] b)
        {
            if (ents == null) return; int id = I(b, 0); bool self = id == ents.SelfId; var tr = ents.TransformOfPlayer(id); if (tr == null) return;
            if (type == PacketIds.P_FE2CL_REP_PC_RIDING_SUCC || type == PacketIds.P_FE2CL_PC_RIDING)
            {
                bool start = I(b, 4) == 1;
                if (self)
                {
                    if (!start && Active) tr.position = target;   // SetBroomStick(false): se coloca en vBroomTo
                    Active = start; SetMounted(id, start); moving = false; if (!start) target = tr.position; speed = 0f;
                    if (start) { target = tr.position; SendNanoOff(); }
                }
                else { var rm = tr.GetComponent<RemoteMover>(); if (rm != null) rm.Flying = start; SetMounted(id, start); }
                return;
            }
            // PC_BROOMSTICK_MOVE: el servidor (Monkey Skyway) no manda RIDING(1): el primer movimiento monta (vTo = posicion actual) y los siguientes llevan
            // el siguiente punto; fBroomSpeed = distancia al punto (se alcanza en ~1 s) -- UserMoveController.SetBroomStick
            var to = PlayerAvatarFactory.ToClient(I(b, 4), I(b, 8), I(b, 12));
            if (self)
            {
                if (!Active) { Active = true; SetMounted(id, true); target = tr.position; speed = 0f; moving = false; SendNanoOff(); return; }
                if ((target - tr.position).magnitude > 5f) tr.position = target;
                target = to; speed = (to - tr.position).magnitude; moving = speed > 0.0001f;
            }
            else { var rm = tr.GetComponent<RemoteMover>(); if (rm != null) { bool first = !rm.Flying; rm.Flying = true; SetMounted(id, true); if (!first) rm.OnNpcMove(to, (to - tr.position).magnitude); } }
        }
        void SendNanoOff()
        {
            var nw = GetComponent<NanoWheelHud>(); var hud = GetComponent<PlayerHud>();
            if (nw != null && hud != null && hud.Data.ActiveNanoSlot >= 0) { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_NANO_ACTIVE, 2); p[4] = 0xFF; p[5] = 0xFF; NpcUI.Sender?.Invoke(p); }
        }
        void Update()
        {
            while (queue.TryDequeue(out var a)) { try { a(); } catch (Exception) { } }
            TickMonkey();
            if (!Active || !moving || ents == null || ents.Self == null) return;
            var t = ents.Self.transform; var d = target - t.position; float step = speed * Time.deltaTime;
            if (d.magnitude <= step) { t.position = target; moving = false; }
            else { t.position += d.normalized * step; var h = new Vector3(d.x, 0f, d.z); if (h.sqrMagnitude > 1e-6f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(h), 4f * Time.deltaTime); }
        }
    }
}
