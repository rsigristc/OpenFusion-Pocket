// Clip25 -> UnityEngine.AnimationClip (legacy, para el componente Animation). Depende de UnityEngine: SIN COMPILAR NI PROBAR aquí.
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public static class ClipBuilder
    {
        /// true = los clips "nif-default" (objetos de mapa: aspersores, molinos, neones...) los reproduce NifClipDriver (cada curva repite en su propio rango, componentes de color/UV sueltos, sin culling);
        /// el AnimationClip legacy conserva solo las curvas de transformación (para quien lo reproduzca a mano). false = comportamiento anterior (Animation con todo).
        public static bool UseNifDriver = true;
        public const string NifClipName = "nif-default";
        /// true = los clips incluyen el evento 'end' del original (AnimationEventHandler.end -> pEndAnimation; NanoAnimation/cnAvatarAnimation encadenan animaciones con el). Llega a AnimEventSink.end con el NOMBRE DEL CLIP.
        /// Por defecto false: nadie lo escucha todavia y son miles de eventos (8380 en los bundles).
        public static bool IncludeEndEvents = false;

        public static AnimationClip ToClip(Clip25 c)
        {
            var clip = new AnimationClip { name = c.Name, legacy = true };
            foreach (var sc in ClipCurves.Build(c))
            {
                var keys = new Keyframe[sc.T.Length];
                for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(sc.T[i], sc.V[i], sc.In[i], sc.Out[i]);
                clip.SetCurve(sc.Path, typeof(Transform), sc.Property, new AnimationCurve(keys));
            }
            // m_FloatCurves (NiMaterialColorController / NiTextureTransformController): parpadeo de luces/neones y UV-scroll (agua de aspersores, cintas, carteles)
            foreach (var sc in (UseNifDriver && c.Name == NifClipName) ? new System.Collections.Generic.List<ScalarCurve>() : ClipCurves.BuildFloats(c))
            {
                var keys = new Keyframe[sc.T.Length];
                for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(sc.T[i], sc.V[i], sc.In[i], sc.Out[i]);
                clip.SetCurve(sc.Path, typeof(Renderer), sc.Property, new AnimationCurve(keys));
            }
            // Eventos de animación que usa el cliente original (AnimationEventHandler): el receptor es AnimEventSink (en el mismo objeto que Animation)
            foreach (var e in c.Events)
            {
                bool isEnd = IncludeEndEvents && e.Func == "end";
                if (!isEnd && e.Func != "sound" && e.Func != "attacksound" && e.Func != "particle" && e.Func != "tag_p" && e.Func != "emote_face_on" && e.Func != "emote_face_off" && e.Func != "swordtail_on" && e.Func != "swordtail_off") continue;
                clip.AddEvent(new AnimationEvent { time = e.Time, functionName = e.Func, stringParameter = isEnd ? (c.Name ?? "") : (e.Data ?? "") });
            }
            // WrapMode de Unity 2.5: 0 Default, 1 Once, 2 Loop, 4 PingPong, 8 ClampForever (mismos valores hoy)
            clip.wrapMode = (WrapMode)c.WrapMode;
            // Los clips de objetos de mapa convertidos de NIF ("nif-default": aspersores, molinos, ruedas, carteles) no traen modo de repetición (Default = Once en el Unity moderno):
            // se quedaban congelados tras la primera pasada. En el original giran/parpadean en bucle.
            if (c.WrapMode == 0 && c.Name == NifClipName) clip.wrapMode = WrapMode.Loop;
            if (c.SampleRate > 0) clip.frameRate = c.SampleRate;
            return clip;   // sin EnsureQuaternionContinuity: ya se corrigió la hemisfera en ClipCurves
        }

        /// Añade/obtiene un componente Animation en root, registra los clips por nombre y reproduce `play` si existe.
        public static Animation Attach(GameObject root, IEnumerable<Clip25> clips, string play = null)
        {
            var anim = root.GetComponent<Animation>(); if (anim == null) anim = root.AddComponent<Animation>();   // sin '??': en el Editor GetComponent devuelve un 'falso null'
            foreach (var c in clips) anim.AddClip(ToClip(c), c.Name);
            if (play != null && anim.GetClip(play) != null) anim.Play(play);
            return anim;
        }
    }
}
