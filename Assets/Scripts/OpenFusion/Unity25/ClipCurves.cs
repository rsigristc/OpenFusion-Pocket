// Preparación pura (sin UnityEngine) de curvas de Clip25 para construir un AnimationClip moderno.
using System;
using System.Collections.Generic;

namespace OpenFusion.Unity25
{
    /// Una curva escalar lista para Keyframe(time, value, inTangent, outTangent).
    public sealed class ScalarCurve { public string Path, Property; public float[] T, V, In, Out; public bool PreLoop, PostLoop; }

    public static class ClipCurves
    {
        /// Todas las curvas escalares de un clip: localPosition.xyz, localRotation.xyzw, localScale.xyz.
        public static List<ScalarCurve> Build(Clip25 c) { return Build(c, false); }

        /// fileSlopes = true: las rotaciones usan las pendientes (tangentes) guardadas en el archivo (curvas comprimidas: m_Slopes), como el Unity 2.5; false = Catmull-Rom.
        public static List<ScalarCurve> Build(Clip25 c, bool fileSlopes)
        {
            var r = new List<ScalarCurve>();
            foreach (var p in c.Position) AddVec(r, p, "localPosition");
            foreach (var s in c.Scale) AddVec(r, s, "localScale");
            foreach (var q in c.Rotation) AddRot(r, q, fileSlopes);
            return r;
        }

        /// Curvas de propiedades de material (m_FloatCurves) traducidas a nombres de propiedad de Renderer ("material._Color.r", "material._MainTex_ST.z").
        /// Path = ruta del nodo animado. Se descartan las que el shader OpenFusion no tiene (_SpecColor, rotación de textura, "[n]." por material).
        /// Interruptor: false = no se anima ningun material (nunca oculta objetos).
        public static bool MaterialCurvesEnabled = true;

        public static List<ScalarCurve> BuildFloats(Clip25 c)
        {
            var r = new List<ScalarCurve>();
            if (!MaterialCurvesEnabled) return r;
            // Animar UN solo componente de un vector (p.ej. solo _Color.a o solo _MainTex_ST.z) deja los demas componentes a 0 en Unity: color negro, escala de UV 0 = objeto INVISIBLE.
            // Solo se conservan vectores con sus 4 componentes animados (r,g,b,a / x,y,z,w); el resto no se anima (queda el valor estatico del material).
            var groups = new Dictionary<string, List<ScalarCurve>>();
            foreach (var f in c.Floats)
            {
                var prop = MaterialProperty(f.Attribute); if (prop == null || f.T == null || f.T.Length == 0) continue;
                int dot = prop.LastIndexOf('.'); string key = f.Path + "|" + prop.Substring(0, dot);
                if (!groups.TryGetValue(key, out var g)) groups[key] = g = new List<ScalarCurve>();
                g.Add(new ScalarCurve { Path = f.Path, Property = prop, T = f.T, V = f.V, In = f.In, Out = f.Out, PreLoop = f.Pre == 1, PostLoop = f.Post == 1 });
            }
            foreach (var g in groups.Values)
            {
                var comps = new HashSet<string>(); foreach (var sc in g) comps.Add(sc.Property.Substring(sc.Property.LastIndexOf('.') + 1));
                bool full = g[0].Property.Contains("_MainTex_ST") ? (comps.Contains("x") && comps.Contains("y") && comps.Contains("z") && comps.Contains("w")) : (comps.Contains("r") && comps.Contains("g") && comps.Contains("b") && comps.Contains("a"));
                if (full) r.AddRange(g);
            }
            return r;
        }

        /// null = no se anima. offset (z,w) y scale (x,y) de _MainTex viven en _MainTex_ST (xy escala, zw desplazamiento).
        public static string MaterialProperty(string a)
        {
            if (string.IsNullOrEmpty(a)) return null;
            switch (a)
            {
                case "_MainTex.offset.x": return "material._MainTex_ST.z";
                case "_MainTex.offset.y": return "material._MainTex_ST.w";
                case "_MainTex.scale.x": return "material._MainTex_ST.x";
                case "_MainTex.scale.y": return "material._MainTex_ST.y";
            }
            int dot = a.IndexOf('.'); if (dot < 0) return null; string name = a.Substring(0, dot), comp = a.Substring(dot + 1);
            if ((name == "_Color" || name == "_AmbColor" || name == "_TintColor") && (comp == "r" || comp == "g" || comp == "b" || comp == "a")) return "material." + a;
            return null;
        }

        static readonly string[] XYZ = { "x", "y", "z" }, XYZW = { "x", "y", "z", "w" };

        static void AddVec(List<ScalarCurve> r, Curve25 c, string prop)
        {
            int n = c.Keys.Length; if (n == 0) return;
            for (int d = 0; d < 3; d++)
            {
                var sc = new ScalarCurve { Path = c.Path, Property = prop + "." + XYZ[d], T = new float[n], V = new float[n], In = new float[n], Out = new float[n], PreLoop = c.Pre == 1, PostLoop = c.Post == 1 };
                for (int i = 0; i < n; i++) { var k = c.Keys[i]; sc.T[i] = k.Time; sc.V[i] = k.Value[d]; sc.In[i] = k.InSlope[d]; sc.Out[i] = k.OutSlope[d]; }
                r.Add(sc);
            }
        }

        /// Rotaciones: sin tangentes en el archivo (curvas comprimidas) -> se mantiene la misma hemisfera y se usan pendientes de Catmull-Rom,
        /// para que la interpolación por componente (como la del Unity 2.5) no dé saltos de signo.
        static void AddRot(List<ScalarCurve> r, RotCurve25 c, bool fileSlopes)
        {
            int n = c.Times.Length; if (n == 0) return;
            var q = FixHemisphere(c.Quats); var sg = HemisphereSigns(c.Quats);
            bool useIn = fileSlopes && c.SlopesIn != null && c.SlopesIn.Length == n * 4, useOut = fileSlopes && c.SlopesOut != null && c.SlopesOut.Length == n * 4;
            for (int d = 0; d < 4; d++)
            {
                var v = new float[n]; for (int i = 0; i < n; i++) v[i] = q[i][d];
                Slopes(c.Times, v, out var sin, out var sout);
                if (useIn) for (int i = 0; i < n; i++) sin[i] = sg[i] * c.SlopesIn[4 * i + d];   // q y -q son la misma rotación: si se invirtió la clave, también su pendiente
                if (useOut) for (int i = 0; i < n; i++) sout[i] = sg[i] * c.SlopesOut[4 * i + d];
                r.Add(new ScalarCurve { Path = c.Path, Property = "localRotation." + XYZW[d], T = c.Times, V = v, In = sin, Out = sout, PreLoop = c.Pre == 1, PostLoop = c.Post == 1 });
            }
        }

        /// +1 / -1 por clave: -1 cuando FixHemisphere invierte esa clave.
        public static float[] HemisphereSigns(float[][] q)
        {
            var sg = new float[q.Length]; var prev = new float[4];
            for (int i = 0; i < q.Length; i++)
            {
                float s = 1f;
                if (i > 0) { float dot = 0; for (int k = 0; k < 4; k++) dot += prev[k] * q[i][k]; if (dot < 0) s = -1f; }
                sg[i] = s; for (int k = 0; k < 4; k++) prev[k] = s * q[i][k];
            }
            return sg;
        }

        /// Invierte q[i] cuando dot(q[i-1], q[i]) < 0 (q y -q son la misma rotación).
        public static float[][] FixHemisphere(float[][] q)
        {
            var o = new float[q.Length][];
            for (int i = 0; i < q.Length; i++)
            {
                o[i] = (float[])q[i].Clone();
                if (i > 0) { float dot = 0; for (int k = 0; k < 4; k++) dot += o[i - 1][k] * o[i][k]; if (dot < 0) for (int k = 0; k < 4; k++) o[i][k] = -o[i][k]; }
            }
            return o;
        }

        /// Pendientes de diferencias finitas centradas (extremos: lado único); iguales de entrada y salida.
        public static void Slopes(float[] t, float[] v, out float[] sin, out float[] sout)
        {
            int n = t.Length; sin = new float[n]; sout = new float[n];
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(n - 1, i + 1);
                float dt = t[b] - t[a]; float s = dt > 1e-6f ? (v[b] - v[a]) / dt : 0f;
                sin[i] = sout[i] = s;
            }
        }

        /// Hermite cúbico entre dos claves (como evalúa Unity). Para verificar la preparación.
        public static float Eval(ScalarCurve c, float time)
        {
            int n = c.T.Length; if (n == 0) return 0; if (time <= c.T[0]) return c.V[0]; if (time >= c.T[n - 1]) return c.V[n - 1];
            int i = 0; while (i + 2 < n && c.T[i + 1] <= time) i++;
            float dt = c.T[i + 1] - c.T[i]; if (dt <= 1e-6f) return c.V[i + 1];
            float u = (time - c.T[i]) / dt, u2 = u * u, u3 = u2 * u;
            return (2 * u3 - 3 * u2 + 1) * c.V[i] + (u3 - 2 * u2 + u) * dt * c.Out[i] + (-2 * u3 + 3 * u2) * c.V[i + 1] + (u3 - u2) * dt * c.In[i + 1];
        }

        /// Como Eval, pero antes de la primera clave / después de la última se repite la curva (PreLoop/PostLoop = m_PreInfinity/m_PostInfinity == 1) dentro de su propio rango [primera, última];
        /// con 2 (ClampForever) o 0 se mantiene el valor del extremo. Así lo hace el Unity 2.5 con los clips de objetos de mapa (cada curva decide si se repite).
        public static float EvalWrapped(ScalarCurve c, float time)
        {
            int n = c.T.Length; if (n == 0) return 0f;
            float t0 = c.T[0], t1 = c.T[n - 1], span = t1 - t0;
            if (span > 1e-5f)
            {
                if (time > t1 && c.PostLoop) { float x = time - t0; time = t0 + (x - span * (float)Math.Floor(x / span)); }
                else if (time < t0 && c.PreLoop) { float x = t0 - time; time = t1 - (x - span * (float)Math.Floor(x / span)); }
            }
            return Eval(c, time);
        }

        public static float Duration(List<ScalarCurve> cs) { float m = 0; foreach (var c in cs) if (c.T.Length > 0) m = Math.Max(m, c.T[c.T.Length - 1]); return m; }

        // ---- Pistas para reproducir un clip "nif-default" sin el componente Animation (ver NifClipDriver) ----

        /// Curvas de transformación de un nodo (ruta relativa al objeto con el Animation). Los arreglos son null si el clip no anima esa propiedad.
        public sealed class XformTrack { public string Path; public ScalarCurve[] Pos, Scl, Rot; }

        /// Curvas de una propiedad de material de un nodo. Prop = "_Color", "_AmbColor", "_Emission", "_TintColor" (comp 0..3 = r,g,b,a) o "_MainTex_ST" (0..3 = escala x, escala y, desplazamiento x, desplazamiento y).
        /// Los componentes sin curva (null) conservan el valor del material (no se ponen a 0 como pasaba con las curvas de Renderer del Animation).
        public sealed class MatTrack { public string Path, Prop; public ScalarCurve[] C = new ScalarCurve[4]; public bool IsST; }

        public sealed class ClipTracks
        {
            public List<XformTrack> X = new List<XformTrack>(); public List<MatTrack> M = new List<MatTrack>();
            public int Dropped; public List<string> DroppedAttrs = new List<string>();
            public float Length;   // duracion del clip: la ultima clave de todas las curvas
            public bool Any { get { return X.Count > 0 || M.Count > 0; } }
        }

        /// Traduce un atributo de m_FloatCurves a (propiedad del shader, componente). false = no soportado (_SpecColor, rotación de textura, "[n]." por material...).
        public static bool MaterialSlot(string a, out string prop, out int comp)
        {
            prop = null; comp = 0; if (string.IsNullOrEmpty(a)) return false;
            switch (a)
            {
                case "_MainTex.scale.x": prop = "_MainTex_ST"; comp = 0; return true;
                case "_MainTex.scale.y": prop = "_MainTex_ST"; comp = 1; return true;
                case "_MainTex.offset.x": prop = "_MainTex_ST"; comp = 2; return true;
                case "_MainTex.offset.y": prop = "_MainTex_ST"; comp = 3; return true;
            }
            int dot = a.LastIndexOf('.'); if (dot <= 0 || dot >= a.Length - 1 || a[0] != '_') return false;
            string name = a.Substring(0, dot), c = a.Substring(dot + 1);
            if (name == "_SpecColor") return false;   // el shader OpenFusion no tiene brillo especular
            if (name.IndexOf('.') >= 0) return false;   // _MainTex.rotation y similares
            if (c == "r") comp = 0; else if (c == "g") comp = 1; else if (c == "b") comp = 2; else if (c == "a") comp = 3; else return false;
            prop = name; return true;
        }

        public static ClipTracks BuildTracks(Clip25 c)
        {
            var r = new ClipTracks(); var xs = new Dictionary<string, XformTrack>();
            foreach (var sc in Build(c, true))
            {
                int dot = sc.Property.IndexOf('.'); if (dot < 0 || dot >= sc.Property.Length - 1) continue;
                string prop = sc.Property.Substring(0, dot); int comp = "xyzw".IndexOf(sc.Property[dot + 1]); if (comp < 0) continue;
                XformTrack xt; if (!xs.TryGetValue(sc.Path, out xt)) { xt = new XformTrack { Path = sc.Path }; xs[sc.Path] = xt; r.X.Add(xt); }
                if (prop == "localPosition") { if (xt.Pos == null) xt.Pos = new ScalarCurve[3]; if (comp < 3) xt.Pos[comp] = sc; }
                else if (prop == "localScale") { if (xt.Scl == null) xt.Scl = new ScalarCurve[3]; if (comp < 3) xt.Scl[comp] = sc; }
                else if (prop == "localRotation") { if (xt.Rot == null) xt.Rot = new ScalarCurve[4]; xt.Rot[comp] = sc; }
            }
            var ms = new Dictionary<string, MatTrack>();
            foreach (var f in c.Floats)
            {
                if (f.T == null || f.T.Length == 0) continue;
                string prop; int comp;
                if (!MaterialSlot(f.Attribute, out prop, out comp)) { r.Dropped++; if (r.DroppedAttrs.Count < 6 && !r.DroppedAttrs.Contains(f.Attribute ?? "")) r.DroppedAttrs.Add(f.Attribute ?? ""); continue; }
                string key = f.Path + "|" + prop; MatTrack mt;
                if (!ms.TryGetValue(key, out mt)) { mt = new MatTrack { Path = f.Path, Prop = prop, IsST = prop == "_MainTex_ST" }; ms[key] = mt; r.M.Add(mt); }
                mt.C[comp] = new ScalarCurve { Path = f.Path, Property = f.Attribute, T = f.T, V = f.V, In = f.In, Out = f.Out, PreLoop = f.Pre == 1, PostLoop = f.Post == 1 };
                r.Length = Math.Max(r.Length, f.T[f.T.Length - 1]);
            }
            foreach (var xt2 in r.X)
            {
                if (xt2.Pos != null) foreach (var sc2 in xt2.Pos) if (sc2 != null) r.Length = Math.Max(r.Length, sc2.T[sc2.T.Length - 1]);
                if (xt2.Scl != null) foreach (var sc2 in xt2.Scl) if (sc2 != null) r.Length = Math.Max(r.Length, sc2.T[sc2.T.Length - 1]);
                if (xt2.Rot != null) foreach (var sc2 in xt2.Rot) if (sc2 != null) r.Length = Math.Max(r.Length, sc2.T[sc2.T.Length - 1]);
            }
            return r;
        }
    }
}
