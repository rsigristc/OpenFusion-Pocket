// Guardian de emotes "sostenidos" del avatar (propio y remotos): sentarse / tumbarse / reclinarse (beach1..3 = emotes 22..24) y bailes (dance1..5).
// SIN COMPILAR NI PROBAR EN UNITY.
//
// PROBLEMA (personaje AFK que "se levanta y se acuesta"): el cliente original reproduce el clip beachN una vez, y al terminar (evento 'end' -> cnAvatarAnimation.EndAnimation)
// el propio jugador reenvia el emote N+21 y el clip se REPITE sin soltar la pose (CrossFadeQueued del mismo clip; el eco del servidor ya no lo reinicia porque CrossFade
// sobre un clip que ya suena no lo rebobina). El puerto, en cambio, tenia 4 mecanismos que devolvian al avatar a stand1 o rebobinaban el clip:
//   1) EmoteSystem.RestoreRemote: a los len segundos de cada eco cruza a stand1 y justo despues llega el siguiente eco -> el remoto se levanta y vuelve a tumbarse en cada vuelta.
//   2) NpcAnimator.Update (jugadores remotos) no sabia de emotes: al acabar su clip de reposo encadenaba otro stand y pisaba el emote.
//   3) GameClient.PlayEmote/EmoteSystem.PlayFor hacen st.time = 0 en CADA eco: la pose vuelve al principio del clip (de pie -> sentado) en cada vuelta.
//   4) EmoteSystem.loops: tope de 60 reenvios -> el propio jugador se levanta solo tras ~60 vueltas.
// Este componente (uno por Animation de jugador, creado por WorldEntities) los neutraliza sin tocar esos archivos:
//   * los clips beach*/dance* pasan a la capa HoldLayer (50): los CrossFade de stand1/run/etc. (capa 0) ya no los apagan;
//   * se sueltan (Blend a 0 en 0,2 s) solo cuando el original lo haria: movimiento / salto / vehiculo / otro emote / EndEmote del propio (GameClient.EmoteActive == false);
//   * beach: si el clip NO es ciclico (pose final != pose inicial, medido muestreando el clip) se mantiene la ULTIMA pose (ClampForever) y un reinicio por eco se ignora;
//             si es ciclico se deja en bucle (equivale a repetirlo);
//   * dance: bucle (loopNames del original) hasta que algo lo corte; remoto sin eco en 120 s se suelta (seguridad);
//   * propio con beach: EmoteSystem.ResetLoop() cada fotograma (sin tope de 60 reenvios).
using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    [DefaultExecutionOrder(1000)]   // despues de EmoteSystem / GameClient / NpcAnimator: corrige el estado que dejaron en este mismo fotograma, antes de que Animation lo evalue
    public sealed class AvatarEmoteGuard : MonoBehaviour
    {
        public bool Own;                       // avatar del jugador local (se suelta con GameClient.EmoteActive == false)
        public const int HoldLayer = 50;       // por debajo de upper/wound/altura-forma (100+) y por encima del locomotor (0)

        Animation an; NpcAnimator na; GameClient gc; float nextGcTry;
        readonly List<AnimationState> hold = new List<AnimationState>();
        readonly List<AnimationState> others = new List<AnimationState>();
        float[] prev; bool[] cyclic; bool[] measured;
        int active = -1; float startedAt, refreshedAt, releaseStopAt = -1f, lastW; bool releasing;
        static readonly Dictionary<string, bool> cyclicCache = new Dictionary<string, bool>();
        static readonly string[] OtherEmotes = { "agree", "angry", "applaud", "cheer", "cry", "flex", "goodbye", "hello", "kiss", "laugh", "no", "ok", "tease", "thank", "shocked", "scratch", "enough", "coin", "report", "observe", "foldedarms", "usestanding", "talk", "talkquestion", "talkexclamation" };

        public static bool IsHeldName(string n) { return n != null && (n.StartsWith("beach") || n.StartsWith("dance")); }

        bool Setup()
        {
            if (an == null) an = GetComponent<Animation>(); if (an == null) return false;
            hold.Clear(); others.Clear();
            foreach (AnimationState st in an)
            {
                if (IsHeldName(st.name)) { st.layer = HoldLayer; hold.Add(st); }
                else if (System.Array.IndexOf(OtherEmotes, st.name) >= 0) others.Add(st);
            }
            prev = new float[hold.Count]; cyclic = new bool[hold.Count]; measured = new bool[hold.Count];
            for (int i = 0; i < prev.Length; i++) prev[i] = -1f;
            na = GetComponentInParent<NpcAnimator>();
            return hold.Count > 0;
        }

        void Update()
        {
            if (hold.Count == 0 && !Setup()) return;
            // 1) clip sostenido actual = el de mayor peso
            int best = -1; float bw = 0.001f;
            for (int i = 0; i < hold.Count; i++) { var s = hold[i]; if (s != null && s.enabled && s.weight > bw) { bw = s.weight; best = i; } }
            if (releaseStopAt > 0f && Time.time >= releaseStopAt)
            {
                releaseStopAt = -1f;
                for (int i = 0; i < hold.Count; i++) { var s = hold[i]; if (s != null && s.enabled && i != best && s.weight < 0.05f) s.enabled = false; }
            }
            if (best < 0) { active = -1; releasing = false; for (int i = 0; i < prev.Length; i++) prev[i] = -1f; return; }
            var st = hold[best]; bool beach = st.name.StartsWith("beach");
            if (releasing)
            {   // desvaneciendose tras Release: si el peso vuelve a subir, un nuevo eco/emote lo ha reactivado
                if (st.weight > lastW + 0.01f) releasing = false;
                lastW = st.weight; if (releasing) return;
            }
            if (best != active) { active = best; startedAt = Time.time; refreshedAt = Time.time; }

            // 2) modo de repeticion
            if (beach)
            {
                if (!measured[best]) { measured[best] = true; cyclic[best] = MeasureCyclic(st); }
                if (cyclic[best]) st.wrapMode = WrapMode.Loop;
                else
                {
                    st.wrapMode = WrapMode.ClampForever; float len = st.length;
                    // reinicio por eco del servidor (st.time = 0 con la pose ya al final): se ignora y se mantiene la pose final
                    if (prev[best] > len * 0.95f && st.time < len * 0.5f) { st.time = len; refreshedAt = Time.time; }
                    prev[best] = st.time;
                }
                if (Own && EmoteSystem.Instance != null) EmoteSystem.Instance.ResetLoop();   // sin tope de 60 reenvios (el original no lo tiene)
            }
            else
            {
                st.wrapMode = WrapMode.Loop;
                if (st.time < prev[best] - st.length * 0.5f) refreshedAt = Time.time;
                prev[best] = st.time;
            }

            // 3) soltar
            if (Time.time - startedAt < 0.3f) return;
            bool release = false;
            if (Own)
            {
                if (gc == null && Time.time >= nextGcTry) { nextGcTry = Time.time + 1f; gc = Object.FindObjectOfType<GameClient>(); }
                release = gc == null || !gc.EmoteActive;
            }
            else if (na != null) release = na.IsMoving || na.External || na.Vehicle > 0 || na.Dying || na.IsAirborne || na.Inven || na.Dead || na.Dashing;   // muerte / dash remotos (RemoteCombatAnim) cortan el emote sostenido
            if (!release)
            {   // otro emote (hello, laugh...) recien empezado: sustituye al sostenido (AvatarEmote -> CrossFade)
                for (int i = 0; i < others.Count; i++) { var o = others[i]; if (o != null && o.enabled && o.weight > 0.05f && o.time < 0.3f) { release = true; break; } }
            }
            if (!release && !beach && !Own && Time.time - refreshedAt > 120f) release = true;
            if (release) Release();
        }

        void Release()
        {
            for (int i = 0; i < hold.Count; i++) { var s = hold[i]; if (s != null && s.enabled && s.weight > 0.001f) an.Blend(s.name, 0f, 0.2f); }
            releaseStopAt = Time.time + 0.3f; releasing = true; lastW = active >= 0 && active < hold.Count && hold[active] != null ? hold[active].weight : 1f; active = -1; for (int i = 0; i < prev.Length; i++) prev[i] = -1f;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null; if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++) { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; }
            return null;
        }

        /// ¿La pose al final del clip coincide con la del principio (bucle sin salto)? Se muestrea el clip sobre este mismo esqueleto (una vez por nombre de clip).
        bool MeasureCyclic(AnimationState st)
        {
            bool c;
            if (cyclicCache.TryGetValue(st.name, out c)) return c;
            c = false;
            try
            {
                var clip = st.clip; var root = an.transform;
                if (clip != null && st.length > 0.05f)
                {
                    var bones = new List<Transform>();
                    foreach (var bn in new[] { "Bip01 Head", "Bip01 L Hand", "Bip01 R Hand", "Bip01 L Foot", "Bip01 R Foot", "Bip01 Pelvis" }) { var b = FindDeep(root, bn); if (b != null) bones.Add(b); }
                    if (bones.Count >= 3)
                    {
                        var p0 = new Vector3[bones.Count];
                        clip.SampleAnimation(an.gameObject, 0f); for (int i = 0; i < bones.Count; i++) p0[i] = root.InverseTransformPoint(bones[i].position);
                        clip.SampleAnimation(an.gameObject, st.length); float mx = 0f;
                        for (int i = 0; i < bones.Count; i++) mx = Mathf.Max(mx, (root.InverseTransformPoint(bones[i].position) - p0[i]).magnitude);
                        c = mx < 0.08f;
                        Debug.Log("[emote-guard] " + st.name + " len=" + st.length.ToString("F2") + " desvio inicio/fin=" + mx.ToString("F3") + " m -> " + (c ? "ciclico (bucle)" : "no ciclico (se mantiene la ultima pose)"));
                    }
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[emote-guard] " + ex.Message); }
            cyclicCache[st.name] = c; return c;
        }
    }
}
