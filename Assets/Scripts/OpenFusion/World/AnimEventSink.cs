// Receptor de los AnimationEvent de los clips 2.5 (AnimationEventHandler del original): sound, attacksound, particle y tag_p. Va en el mismo objeto que el componente Animation.
// sound: "Sound/"+dato (con "(RAND:a-b)" sustituido por un entero aleatorio); no suena si !IsPlayable o si el dueño está "quieto" (IsStand); "_Nan" de un nano usa el volumen de voz.
// attacksound: sonido del arma (SoundElement de la tabla de armas: 70 % cadena1, 15 % cadena2, 15 % cadena3; con batería, cadena1 del sonido2). particle/tag_p: efecto ES[id] (en el hueso indicado, rot. 90,0,0).
// SIN COMPILAR EN UNITY.
using System;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class AnimEventSink : MonoBehaviour
    {
        /// Resuelve la ruta "sound/..." del arma para el objeto que ataca (null = nada). La fija CombatSystem.
        public static Func<Transform, string> AttackSound;
        /// ¿El dueño está "quieto" (NpcMoveController.IsStand, etc.)? Si es true no suenan los sonidos de animación.
        public static Func<Transform, string, bool> Muted;
        /// Carga la textura de cara de un emote ("Texture/" + dato en el original). La fija WorldBootstrap.
        public static Func<string, Texture> FaceTexture;
        Material faceMat; Texture oldFace;
        Material GetFaceMaterial()
        {   // AnimationEventHandler.GetFaceMaterial: primer material cuya textura principal contiene "_face"
            if (faceMat != null) return faceMat;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var m in r.materials) if (m != null && m.HasProperty("_MainTex") && m.mainTexture != null && m.mainTexture.name.IndexOf("_face", StringComparison.OrdinalIgnoreCase) >= 0) { faceMat = m; return m; }
            return null;
        }
        public void emote_face_on(string data)
        {
            var m = GetFaceMaterial(); if (m == null || FaceTexture == null || string.IsNullOrEmpty(data)) return;
            var t = FaceTexture(data); if (t == null) return; if (!m.HasProperty("_MainTex")) return; if (oldFace == null) oldFace = m.mainTexture; m.mainTexture = t;
        }
        public void emote_face_off(string data)
        {
            if (faceMat != null && oldFace != null) { faceMat.mainTexture = oldFace; oldFace = null; }
        }
        public bool IsNano;   // los "_Nan" usan el volumen de voz
        /// AnimationEventHandler.end -> pEndAnimation: fin de un clip (parametro = nombre del clip). Solo se dispara si ClipBuilder.IncludeEndEvents = true al construir los clips.
        public event Action<string> EndAnimation;
        public void end(string clipName) { if (EndAnimation != null) EndAnimation(clipName); }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null; if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++) { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; }
            return null;
        }

        public void sound(string data)
        {
            var sfx = Sfx.Instance; if (sfx == null || string.IsNullOrEmpty(data)) return;
            var pos = transform.position; if (!sfx.IsPlayable(pos)) return;
            if (Muted != null && Muted(transform, data) && !data.Contains("_SFX_Dance")) return;
            bool voice = IsNano && data.Contains("_Nan");
            var path = AnimationAudioPaths.Choose(data, sfx.HasClip, UnityEngine.Random.Range(0, int.MaxValue));
            sfx.At(path, pos, 1f, false, 0f, null, voice);
        }

        public void attacksound(string data)
        {
            var sfx = Sfx.Instance; if (sfx == null || AttackSound == null) return;
            var pos = transform.position; if (!sfx.IsPlayable(pos)) return;
            var p = AttackSound(transform); if (!string.IsNullOrEmpty(p)) sfx.At(p, pos, 1f, false, 0.1f);
        }

        public void swordtail_on() { foreach (var trail in GetComponentsInChildren<MeleeTrail>(true)) trail.SetEmitting(true); }
        public void swordtail_off() { foreach (var trail in GetComponentsInChildren<MeleeTrail>(true)) trail.SetEmitting(false); }

        public void particle(string data) { Fx(data, null); }
        public void tag_p(string data)
        {
            if (string.IsNullOrEmpty(data)) return; int c = data.IndexOf(':'); if (c < 0) { Fx(data, null); return; }
            Fx(data.Substring(0, c), data.Substring(c + 1));
        }

        void Fx(string idStr, string bone)
        {
            var fx = BulletFx.Instance; int id; if (fx == null || !int.TryParse(idStr, out id)) return;
            var cam = Camera.main; if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 80f * 80f) return;
            Transform t = bone != null ? FindDeep(transform, bone) : null; var rot = Quaternion.identity;
            if (t != null) rot = Quaternion.Euler(90f, 0f, 0f); else t = transform;
            var go = fx.Spawn(id, t.position, t.rotation * rot, 1f, t, Persist ? 120f : 4f);
            if (go != null && Persist)
            {   // AnimationEventHandler.currentEffect: un efecto del mismo nombre sustituye al anterior (el clip 'inven' lo repite en cada vuelta)
                for (int i = persisted.Count - 1; i >= 0; i--) { if (persisted[i] == null) persisted.RemoveAt(i); else if (persisted[i].name == go.name) { Destroy(persisted[i]); persisted.RemoveAt(i); } }
                persisted.Add(go);
            }
        }
        /// <summary>true = los efectos de los eventos viven hasta DeleteCurrentObject (modo inventario: holograma del ordenador).</summary>
        public bool Persist;
        readonly System.Collections.Generic.List<GameObject> persisted = new System.Collections.Generic.List<GameObject>();
        /// <summary>AnimationEventHandler.DeleteCurrentObject.</summary>
        public void DeleteCurrentObject() { for (int i = 0; i < persisted.Count; i++) if (persisted[i] != null) Destroy(persisted[i]); persisted.Clear(); }
    }
}
