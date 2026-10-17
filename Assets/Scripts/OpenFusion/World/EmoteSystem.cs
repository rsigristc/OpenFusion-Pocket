// Emotes del avatar (CnGuiChat.EmoteChat + cnOwnAvatarStatus/cnAvatarStatus + cnAvatarAnimation.AvatarEmote/EndEmote). SIN COMPILAR NI PROBAR EN UNITY.
//   Envio:    REQ_PC_AVATAR_EMOTES_CHAT { iID_From, iEmoteCode } (solo si no se va en vehiculo: si no, "Action_Failure01").
//   Recepcion: REP_PC_AVATAR_EMOTES_CHAT { iID_From, iEmoteCode } llega TAMBIEN al propio jugador (el servidor lo devuelve a todos) y es entonces cuando se anima.
//   Tambien se anima con los mensajes de chat (libre o de menu) que traen iEmoteCode > 0 (ReceiveChat / ReceiveMenuChat), salvo en vehiculo.
//   Animacion: EmoteTable.m_pEmoteAnimationData[emote] -> m_iAvatarAnimation = indice en AnimationTable.m_pAvatarData (m_szSequenceName) y m_iNanoAnimation (nano, AnimationTable.m_pNanoData).
//   Bucles del propio jugador (EndAnimation): "dance..." al acabar reenvia un baile aleatorio (0 -> 6, si no 16+n); "beachN" reenvia N+21. Cualquier movimiento, salto o ataque lo corta (EndEmote).
// Pendiente (ver doc): cambio de textura de cara (m_iTexture / emote_face_off), emotes de intercambio (TRADE_EMOTES_CHAT).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class EmoteSystem : MonoBehaviour
    {
        public static EmoteSystem Instance { get; private set; }
        WorldEntities ents; PacketDispatcher disp; XdtTableScript tables; NanoSystem nano; GameClient gc;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        readonly Dictionary<int, int> token = new Dictionary<int, int>();   // pcId -> version de la animacion remota en curso
        int loops;                                                           // reenvios seguidos del bucle propio (tope de seguridad)

        public static EmoteSystem Ensure(GameObject host) { var e = host.GetComponent<EmoteSystem>(); if (e == null) e = host.AddComponent<EmoteSystem>(); Instance = e; return e; }

        public void Init(WorldEntities e, PacketDispatcher d, XdtTableScript t, NanoSystem n, GameClient g)
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            ents = e; disp = d; tables = t; nano = n; gc = g;
            if (disp != null) disp.Unhandled += OnPacket;
            if (gc != null) gc.EmoteEnded = OnOwnEnded;
            var chat = ChatHud.Instance; if (chat != null) { chat.ChatBubble -= OnChatEmote; chat.ChatBubble += OnChatEmote; }
        }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; if (Instance == this) Instance = null; }

        // ---------------- envio ----------------
        /// <summary>CnGuiChat.EmoteChat. false si no se pudo (vehiculo).</summary>
        public bool Send(int code)
        {
            if (NpcUI.Sender == null || ents == null) return false;
            if (GameMenu.Riding) { Sfx.Ui("Action_Failure01"); return false; }
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_AVATAR_EMOTES_CHAT, 8); ClientPackets.PutInt(p, 4, ents.SelfId); ClientPackets.PutInt(p, 8, code); NpcUI.Sender(p); return true;
        }

        /// <summary>Emote pedido por el jugador (menu de emotes): reinicia el contador de bucles.</summary>
        public bool SendUser(int code) { loops = 0; return Send(code); }

        // ---------------- recepcion ----------------
        void OnPacket(uint type, byte[] b)   // hilo de red
        {
            if (type != PacketIds.P_FE2CL_REP_PC_AVATAR_EMOTES_CHAT || b == null || b.Length < 8) return;
            int id = BitConverter.ToInt32(b, 0), code = BitConverter.ToInt32(b, 4); q.Enqueue(() => PlayFor(id, code));
        }
        void OnChatEmote(int pcId, string msg, int emote, int kind)
        {   // kind 0 libre, 1 menu, 2 NPC (se ignora). 420 / 666 son extensiones de Retrobution, no emotes.
            if (kind == 2 || emote <= 0 || emote == 420 || emote == 666) return; q.Enqueue(() => PlayFor(pcId, emote));
        }
        void Update() { Action a; int n = 0; while (n++ < 16 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[emote] " + ex.Message); } } }

        /// <summary>Nombre del clip de avatar y del clip de nano de un emote (null si la tabla no lo tiene).</summary>
        public bool Resolve(int code, out string clip, out string nanoClip)
        {
            clip = null; nanoClip = null; var et = tables != null ? tables.m_pEmoteTable : null; var at = tables != null ? tables.m_pAnimationTable : null;
            if (et == null || et.m_pEmoteAnimationData == null || at == null) return false;
            EmoteAnimationElement el = null; var arr = et.m_pEmoteAnimationData;
            for (int i = 0; i < arr.Length; i++) if (arr[i] != null && arr[i].m_iEmoteNumber == code) { el = arr[i]; break; }
            if (el == null && code >= 0 && code < arr.Length) el = arr[code];
            if (el == null) return false;
            if (at.m_pAvatarData != null && el.m_iAvatarAnimation > 0 && el.m_iAvatarAnimation < at.m_pAvatarData.Length && at.m_pAvatarData[el.m_iAvatarAnimation] != null) clip = at.m_pAvatarData[el.m_iAvatarAnimation].m_szSequenceName;
            if (at.m_pNanoData != null && el.m_iNanoAnimation > 0 && el.m_iNanoAnimation < at.m_pNanoData.Length && at.m_pNanoData[el.m_iNanoAnimation] != null) nanoClip = at.m_pNanoData[el.m_iNanoAnimation].m_szSequenceName;
            return !string.IsNullOrEmpty(clip);
        }

        static string Find(Animation an, string name)
        {
            if (an == null || string.IsNullOrEmpty(name)) return null; if (an[name] != null) return name;
            foreach (AnimationState st in an) if (string.Equals(st.name, name, StringComparison.OrdinalIgnoreCase)) return st.name;
            return null;
        }

        void PlayFor(int pcId, int code)
        {
            if (ents == null) return; string clip, nanoClip; if (!Resolve(code, out clip, out nanoClip)) return;
            bool own = pcId == ents.SelfId;
            var t = ents.TransformOfPlayer(pcId); if (t == null) return;
            if (own)
            {
                if (GameMenu.Riding || gc == null) return;
                var an0 = t.GetComponentInChildren<Animation>(true); string c0 = Find(an0, clip); if (c0 == null) { Debug.Log("[emote] sin clip '" + clip + "' para el emote " + code); return; }
                if (gc.PlayEmote(c0, code) && nano != null && nanoClip != null) nano.PlayEmote(pcId, nanoClip);
                return;
            }
            if (VehicleRide.IsRiding(pcId)) return;   // GameFrame/CnGuiChat: AvatarEmote solo con iVehicle == 0
            var an = t.GetComponentInChildren<Animation>(true); string c = Find(an, clip); if (c == null) { Debug.Log("[emote] sin clip '" + clip + "' para el emote " + code); return; }
            var st = an[c]; st.wrapMode = WrapMode.Once; st.time = 0f; st.speed = 1f; an.cullingType = AnimationCullingType.AlwaysAnimate; an.CrossFade(c, 0.15f);
            int v; token.TryGetValue(pcId, out v); v++; token[pcId] = v; StartCoroutine(RestoreRemote(pcId, an, st.length, v));
            if (nano != null && nanoClip != null) nano.PlayEmote(pcId, nanoClip);
        }
        IEnumerator RestoreRemote(int pcId, Animation an, float len, int v)
        {
            yield return new WaitForSeconds(Mathf.Max(0.3f, len));
            int cur; if (an == null || !token.TryGetValue(pcId, out cur) || cur != v) yield break;
            var fs = an.GetComponent<AnimEventSink>(); if (fs != null) fs.emote_face_off("");
            string stand = Find(an, "stand1"); if (stand != null) { an[stand].wrapMode = WrapMode.Loop; an.CrossFade(stand, 0.15f); }
        }

        // ---------------- bucle del propio jugador (cnAvatarAnimation.EndAnimation) ----------------
        /// <summary>Lo llama GameClient al terminar el clip del emote propio. true = mantener el estado de emote (se reenvia el siguiente).</summary>
        bool OnOwnEnded(int code, string clip)
        {
            string lc = (clip ?? "").ToLowerInvariant();
            if (loops > 60) { loops = 0; return false; }
            if (lc.Contains("dance")) { int n = UnityEngine.Random.Range(0, 5); loops++; return Send(n == 0 ? 6 : 16 + n); }
            if (lc.Contains("beach") && lc.Length > 0) { int d; if (int.TryParse(lc.Substring(lc.Length - 1), out d)) { loops++; return Send(d + 21); } }
            loops = 0; return false;
        }
        public void ResetLoop() { loops = 0; }
    }
}
