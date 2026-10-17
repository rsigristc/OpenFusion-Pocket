// NifClipDriver.cs -- reproduce los clips "nif-default" de los objetos de mapa (aspersores, molinos, ruedas, neones, banderas...) sin pasar por el componente Animation de Unity.
// Por que: en el Unity 2.5 cada CURVA decide si se repite (m_PreInfinity/m_PostInfinity: 1 = repetir en su propio rango, 2 = mantener el ultimo valor), el clip no tiene modo de repeticion
// y el Animation corre siempre. En el Unity moderno un clip legacy en Loop repite TODAS las curvas con la duracion del clip (las mas cortas se quedan quietas y las de un solo disparo se reinician),
// el culling por renderers puede congelarlo y las curvas sueltas de un vector (solo _MainTex.offset.y, solo _Emission.rgb) ponen a 0 el resto de componentes.
// Aqui se evalua cada curva con ClipCurves.EvalWrapped (Hermite con las tangentes del archivo) y se escribe en Transform / Material; los componentes sin curva conservan el valor del material.
// Solo se anima cerca de la camara (el original: AnimateIfVisible). Si alguien reproduce el Animation legacy (vehiculos, efectos), el driver se aparta. SIN COMPILAR EN UNITY.
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class NifClipDriver : MonoBehaviour
    {
        /// Distancia (m) a la camara a partir de la cual se detiene la animacion del objeto.
        public static float ActiveDistance = 220f;
        /// true = el clip entero repite con su duracion (EffectEmitterController.nifObject: animation.wrapMode = Loop); false = cada curva repite segun su m_PreInfinity/m_PostInfinity (objetos de mapa).
        public bool LoopWhole;
        static Camera cam; static int camFrame = -1000; static int logged;

        sealed class XB { public Transform Tr; public ClipCurves.XformTrack T; }
        sealed class MB { public Material Mat; public ClipCurves.MatTrack T; public int Id; public Color BaseC; public Vector2 BaseS, BaseO; }

        ClipCurves.ClipTracks tracks; Animation anim; bool bound; float t0; int missing;
        readonly List<XB> xb = new List<XB>(); readonly List<MB> mb = new List<MB>();

        public void Init(ClipCurves.ClipTracks tk, Animation a) { tracks = tk; anim = a; t0 = Time.time; }

        void OnEnable() { t0 = Time.time; }   // los prefabs 2.5 llegan inactivos: la animacion arranca al activarlos (como el playAutomatically del original)

        void OnDestroy() { for (int i = 0; i < mb.Count; i++) if (mb[i].Mat != null) Destroy(mb[i].Mat); }   // materiales instanciados por este driver

        static bool NearCamera(Vector3 p)
        {
            if (cam == null || Time.frameCount - camFrame > 30) { camFrame = Time.frameCount; cam = Camera.main; }
            if (cam == null) return true;
            return (cam.transform.position - p).sqrMagnitude <= ActiveDistance * ActiveDistance;
        }

        void Update()
        {
            if (tracks == null) return;
            if (anim != null && anim.isPlaying) return;   // otro codigo reproduce el Animation legacy (vehiculos, modelos con play explicito)
            if (!NearCamera(transform.position)) return;
            if (!bound) Bind();
            float t = Time.time - t0;
            if (LoopWhole && tracks.Length > 1e-4f) t -= tracks.Length * Mathf.Floor(t / tracks.Length);
            Apply(t);
        }

        Transform Find(string path) { return string.IsNullOrEmpty(path) ? transform : transform.Find(path); }

        void Bind()
        {
            bound = true;
            for (int i = 0; i < tracks.X.Count; i++)
            {
                var tr = Find(tracks.X[i].Path); if (tr == null) { missing++; continue; }
                xb.Add(new XB { Tr = tr, T = tracks.X[i] });
            }
            int matOk = 0;
            if (ClipCurves.MaterialCurvesEnabled)
                for (int i = 0; i < tracks.M.Count; i++)
                {
                    var m = tracks.M[i]; var tr = Find(m.Path); if (tr == null) { missing++; continue; }
                    var rd = tr.GetComponent<Renderer>(); if (rd == null) { missing++; continue; }
                    var sm = rd.sharedMaterial; if (sm == null || sm.shader == null) continue;
                    if (sm.shader.name == "OpenFusion/StationFx") continue;   // la estacion se anima en su shader (sus curvas son de relleno)
                    if (m.IsST ? !sm.HasProperty("_MainTex") : !sm.HasProperty(m.Prop)) continue;   // comprobar ANTES de instanciar: destruir la instancia dejaba el renderer en magenta
                    var mat = rd.material;   // instancia propia (el material del cache es compartido por todos los objetos iguales)
                    if (mat == null) continue;
                    var b = new MB { Mat = mat, T = m };
                    if (m.IsST) { b.BaseS = mat.mainTextureScale; b.BaseO = mat.mainTextureOffset; }
                    else { b.Id = Shader.PropertyToID(m.Prop); b.BaseC = mat.GetColor(b.Id); }
                    mb.Add(b); matOk++;
                }
            if (logged++ < 10) Debug.Log("[nifanim] " + name + ": pistas de transformacion " + xb.Count + "/" + tracks.X.Count + ", de material " + matOk + "/" + tracks.M.Count + ", rutas sin resolver " + missing + ", curvas no soportadas " + tracks.Dropped + (tracks.DroppedAttrs.Count > 0 ? " (" + string.Join(",", tracks.DroppedAttrs.ToArray()) + ")" : ""));
        }

        void Apply(float t)
        {
            for (int i = 0; i < xb.Count; i++)
            {
                var b = xb[i]; if (b.Tr == null) continue; var tk = b.T;
                if (tk.Pos != null)
                {
                    var p = b.Tr.localPosition;
                    if (tk.Pos[0] != null) p.x = ClipCurves.EvalWrapped(tk.Pos[0], t); if (tk.Pos[1] != null) p.y = ClipCurves.EvalWrapped(tk.Pos[1], t); if (tk.Pos[2] != null) p.z = ClipCurves.EvalWrapped(tk.Pos[2], t);
                    b.Tr.localPosition = p;
                }
                if (tk.Scl != null)
                {
                    var s = b.Tr.localScale;
                    if (tk.Scl[0] != null) s.x = ClipCurves.EvalWrapped(tk.Scl[0], t); if (tk.Scl[1] != null) s.y = ClipCurves.EvalWrapped(tk.Scl[1], t); if (tk.Scl[2] != null) s.z = ClipCurves.EvalWrapped(tk.Scl[2], t);
                    b.Tr.localScale = s;
                }
                if (tk.Rot != null && tk.Rot[0] != null && tk.Rot[1] != null && tk.Rot[2] != null && tk.Rot[3] != null)
                {
                    float x = ClipCurves.EvalWrapped(tk.Rot[0], t), y = ClipCurves.EvalWrapped(tk.Rot[1], t), z = ClipCurves.EvalWrapped(tk.Rot[2], t), w = ClipCurves.EvalWrapped(tk.Rot[3], t);
                    float l = Mathf.Sqrt(x * x + y * y + z * z + w * w);
                    if (l > 1e-6f) { l = 1f / l; b.Tr.localRotation = new Quaternion(x * l, y * l, z * l, w * l); }   // la interpolacion por componente no conserva la norma: se normaliza como Unity
                }
            }
            for (int i = 0; i < mb.Count; i++)
            {
                var b = mb[i]; if (b.Mat == null) continue; var tk = b.T;
                if (tk.IsST)
                {
                    var sc = b.BaseS; var of = b.BaseO;
                    if (tk.C[0] != null) sc.x = ClipCurves.EvalWrapped(tk.C[0], t); if (tk.C[1] != null) sc.y = ClipCurves.EvalWrapped(tk.C[1], t);
                    if (tk.C[2] != null) of.x = ClipCurves.EvalWrapped(tk.C[2], t); if (tk.C[3] != null) of.y = ClipCurves.EvalWrapped(tk.C[3], t);
                    b.Mat.mainTextureScale = sc; b.Mat.mainTextureOffset = of;
                }
                else
                {
                    var c = b.BaseC;
                    if (tk.C[0] != null) c.r = ClipCurves.EvalWrapped(tk.C[0], t); if (tk.C[1] != null) c.g = ClipCurves.EvalWrapped(tk.C[1], t);
                    if (tk.C[2] != null) c.b = ClipCurves.EvalWrapped(tk.C[2], t); if (tk.C[3] != null) c.a = ClipCurves.EvalWrapped(tk.C[3], t);
                    b.Mat.SetColor(b.Id, c);
                }
            }
        }
    }
}
