// LegacyParticles.cs -- convierte los prefabs de partículas de Unity 2.5 (EllipsoidParticleEmitter + ParticleAnimator + ParticleRenderer + el script
// ParticleEmitterController del cliente original) a un ParticleSystem de Unity 2022, y reproduce el script EffectEmitterController (claves de tiempo
// traslación/emisión) que los mapas usan para colocar fogatas, barreras, motores de fusión, etc. SIN COMPILAR en el sandbox (solo chequeo sintáctico).
//
// Fuentes (cliente original decompilado): ParticleEmitterController.cs / EffectEmitterController.cs. Datos: bundle Effects (prefabs "PS[n]").
//  - Emisión real = numberPerGeneration * generationsPerSecond partículas/s, vida = lifeTime (el emisor 2.5 trae m_Emit=false y lo maneja el script).
//  - Tamaño: si el ParticleRenderer trae widthCurve, es absoluto (metros) según la edad; si no, initialSize.
//  - Color: ParticleAnimator.colorAnimation[0..4] repartido en la vida (RGBA32, byte bajo = R). Los materiales son aditivos: negro = invisible.
//  - Posición de nacimiento: m_iGenType (punto, punto invertido, plano, donas X/Z/Y, normales/invertidas, rastro).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    public static class LegacyParticles
    {
        static float F(Dictionary<string, object> d, string k, float def) { object o; if (d != null && d.TryGetValue(k, out o) && o != null) { try { return System.Convert.ToSingle(o); } catch { } } return def; }
        static bool B(Dictionary<string, object> d, string k, bool def) { object o; if (d != null && d.TryGetValue(k, out o) && o != null) { try { return System.Convert.ToInt32(o) != 0; } catch { } } return def; }
        static Vector3 V(Dictionary<string, object> d, string k, Vector3 def)
        {
            object o; if (d == null || !d.TryGetValue(k, out o)) return def; var v = o as Dictionary<string, object>; if (v == null) return def;
            return new Vector3(F(v, "x", 0f), F(v, "y", 0f), F(v, "z", 0f));
        }
        static AnimationCurve Curve(Dictionary<string, object> r, string k)
        {
            object o; if (r == null || !r.TryGetValue(k, out o)) return null; var c = o as Dictionary<string, object>; if (c == null) return null;
            object ko; if (!c.TryGetValue("m_Curve", out ko)) return null; var arr = ko as object[]; if (arr == null || arr.Length == 0) return null;
            var keys = new Keyframe[arr.Length];
            for (int i = 0; i < arr.Length; i++) { var kf = arr[i] as Dictionary<string, object>; keys[i] = new Keyframe(F(kf, "time", 0f), F(kf, "value", 0f), F(kf, "inSlope", 0f), F(kf, "outSlope", 0f)); }
            return new AnimationCurve(keys);
        }
        static Color Rgba(Dictionary<string, object> a, string k, Color def)
        {
            object o; if (a == null || !a.TryGetValue(k, out o)) return def; var d = o as Dictionary<string, object>; if (d == null) return def;
            uint v; try { v = System.Convert.ToUInt32(d["rgba"]); } catch { return def; }
            return new Color32((byte)(v & 255), (byte)((v >> 8) & 255), (byte)((v >> 16) & 255), (byte)((v >> 24) & 255));
        }

        /// <summary>Crea el ParticleSystem en `go`. pec = campos del script ParticleEmitterController (puede ser null), em/an/rd = emisor, animador y renderer 2.5. mat = material (null = no se dibuja).</summary>
        public static ParticleSystem Build(GameObject go, Dictionary<string, object> pec, Dictionary<string, object> em, Dictionary<string, object> an, Dictionary<string, object> rd, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var psr = go.GetComponent<ParticleSystemRenderer>();

            float life = Mathf.Max(0.05f, pec != null ? F(pec, "lifeTime", F(em, "maxEnergy", 1f)) : F(em, "maxEnergy", 1f));
            float number = pec != null ? F(pec, "numberPerGeneration", 0f) : 0f, gps = pec != null ? F(pec, "generationsPerSecond", 0f) : 0f;
            float rate = number * gps; if (rate <= 0f) rate = Mathf.Max(F(em, "minEmission", 10f), 1f);
            int type = pec != null ? (int)F(pec, "m_iGenType", 0f) : 0;
            float randPos = pec != null ? F(pec, "randomPosition", 0f) : F(em, "m_MinEmitterRange", 0f);
            float randVel = pec != null ? Mathf.Clamp01(F(pec, "randomVelocity", 0f)) : 0f;
            Vector3 initVel = pec != null ? V(pec, "initVelocity", Vector3.zero) : V(em, "worldVelocity", Vector3.zero);
            Vector3 plane = pec != null ? V(pec, "plane", Vector3.zero) : Vector3.zero;
            float initSize = pec != null ? F(pec, "initialSize", 1f) : F(em, "minSize", 0.1f);

            var main = ps.main; main.loop = true; main.playOnAwake = true; main.duration = 1f; main.startSpeed = 0f;
            main.startLifetime = life; main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(rate * life * 1.5f) + 8, 16, 1500);
            main.simulationSpace = B(em, "Simulate in Worldspace?", true) ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.Automatic; main.startColor = Color.white;

            var emi = ps.emission; emi.enabled = pec == null || B(pec, "emit", true); emi.rateOverTime = rate;
            // el controlador original arranca con timer=2 (emite al instante) y repite cada 1/generationsPerSecond: con periodos largos (p. ej. el espiral del respawn, 1 partícula cada 500 s)
            // un caudal continuo tardaría siglos en dar la primera: se usa una ráfaga al inicio de cada ciclo de duración = periodo.
            if (pec != null && gps > 0f && gps < 0.7f && number > 0f)
            {
                main.duration = 1f / gps; emi.rateOverTime = 0f; emi.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, Mathf.RoundToInt(number))) });
                main.maxParticles = Mathf.Max(16, Mathf.CeilToInt(number * (life * gps + 1f)) + 4);
            }

            // forma de nacimiento
            var sh = ps.shape; sh.enabled = true; float sign = 0f;
            switch (type)
            {
                case 1: sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = Mathf.Max(randPos, 0.001f); sh.radiusThickness = 0f; sign = -1f; break;   // punto invertido: en la superficie, hacia dentro
                case 2: sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(Mathf.Max(Mathf.Abs(plane.x), 0.001f), Mathf.Max(Mathf.Abs(plane.y), 0.001f), Mathf.Max(Mathf.Abs(plane.z), 0.001f)); break;
                case 3: case 6: case 9: case 4: case 7: case 10: case 5: case 8: case 11:
                    sh.shapeType = ParticleSystemShapeType.Circle; sh.arc = 360f;
                    sh.radius = Mathf.Max(plane.z, 0.001f); sh.radiusThickness = Mathf.Clamp01((plane.z - plane.x) / Mathf.Max(plane.z, 0.001f));
                    bool isX = type == 3 || type == 6 || type == 9, isY = type == 5 || type == 8 || type == 11;
                    sh.rotation = isY ? new Vector3(90f, 0f, 0f) : isX ? new Vector3(0f, 90f, 0f) : Vector3.zero;   // el círculo de Unity está en el plano XY
                    if (type == 6 || type == 7 || type == 8) sign = -1f; else if (type >= 9 && type <= 11) sign = 1f; break;
                default: sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = Mathf.Max(randPos, 0.001f); sh.radiusThickness = 1f; break;   // 0 punto, 12 rastro
            }
            sh.randomDirectionAmount = 0f; sh.sphericalDirectionAmount = 0f;

            // velocidad (initVelocity en el espacio local, multiplicada por un azar 0..2*(1-randomVelocity)); radial para las variantes invertidas
            float vr = 2f * (1f - randVel);
            if (initVel.sqrMagnitude > 1e-6f || sign != 0f)
            {
                var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.Local;
                vol.x = new ParticleSystem.MinMaxCurve(Mathf.Min(0f, initVel.x * vr), Mathf.Max(0f, initVel.x * vr));
                vol.y = new ParticleSystem.MinMaxCurve(Mathf.Min(0f, initVel.y * vr), Mathf.Max(0f, initVel.y * vr));
                vol.z = new ParticleSystem.MinMaxCurve(Mathf.Min(0f, initVel.z * vr), Mathf.Max(0f, initVel.z * vr));
                vol.radial = new ParticleSystem.MinMaxCurve(Mathf.Min(0f, sign * vr), Mathf.Max(0f, sign * vr));
            }

            // tamaño
            var wc = Curve(rd, "m_WidthCurve");
            var hcv = Curve(rd, "m_HeightCurve");   // alto absoluto (m) según la edad: haces de luz, pantallas holográficas (no cuadradas)
            if (hcv != null)
            {
                main.startSize = 1f; var so = ps.sizeOverLifetime; so.enabled = true; so.separateAxes = true;
                var wcc = wc ?? AnimationCurve.Constant(0f, 1f, Mathf.Max(initSize, 0.001f));
                so.x = new ParticleSystem.MinMaxCurve(1f, wcc); so.y = new ParticleSystem.MinMaxCurve(1f, hcv); so.z = new ParticleSystem.MinMaxCurve(1f, wcc);
            }
            else if (wc != null) { main.startSize = 1f; var so = ps.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1f, wc); }
            else main.startSize = Mathf.Max(initSize, 0.001f);
            var rc = Curve(rd, "m_RotationCurve");
            if (rc != null && rc.length >= 2)
            {
                float dv = rc.keys[rc.length - 1].value - rc.keys[0].value;
                if (Mathf.Abs(dv) > 0.01f) { var ro = ps.rotationOverLifetime; ro.enabled = true; ro.z = dv * Mathf.Deg2Rad / life; }
            }

            // color por edad (ParticleAnimator): 5 muestras a 0, .25, .5, .75, 1
            if (an != null && B(an, "Does Animate Color?", false))
            {
                var ck = new GradientColorKey[5]; var ak = new GradientAlphaKey[5];
                for (int i = 0; i < 5; i++) { var c = Rgba(an, "colorAnimation[" + i + "]", Color.white); float t = i / 4f; ck[i] = new GradientColorKey(new Color(c.r, c.g, c.b, 1f), t); ak[i] = new GradientAlphaKey(c.a, t); }
                var g = new Gradient(); g.SetKeys(ck, ak); var col = ps.colorOverLifetime; col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(g);
            }
            Vector3 force = V(an, "force", Vector3.zero);
            if (force.sqrMagnitude > 1e-6f)
            {
                var fo = ps.forceOverLifetime; fo.enabled = true; fo.space = ParticleSystemSimulationSpace.World;
                fo.x = force.x; fo.y = force.y; fo.z = force.z;
            }

            // hoja de texturas (UV Animation: x Tile / y Tile / cycles)
            object uvo; Dictionary<string, object> uv = rd != null && rd.TryGetValue("UV Animation", out uvo) ? uvo as Dictionary<string, object> : null;
            int tx = (int)F(uv, "x Tile", 1f), ty = (int)F(uv, "y Tile", 1f);
            if (tx > 1 || ty > 1)
            {
                var ts = ps.textureSheetAnimation; ts.enabled = true; ts.mode = ParticleSystemAnimationMode.Grid; ts.numTilesX = Mathf.Max(tx, 1); ts.numTilesY = Mathf.Max(ty, 1);
                ts.animation = ParticleSystemAnimationType.WholeSheet; ts.cycleCount = Mathf.Max(1, Mathf.RoundToInt(F(uv, "cycles", 1f)));
                ts.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }

            // renderer
            int stretch = (int)F(rd, "m_StretchParticles", 0f);
            psr.renderMode = stretch == 3 ? ParticleSystemRenderMode.Stretch : stretch == 4 ? ParticleSystemRenderMode.HorizontalBillboard : stretch == 5 ? ParticleSystemRenderMode.VerticalBillboard : ParticleSystemRenderMode.Billboard;   // enum 2.5: 0 Billboard, 2 Sorted, 3 Stretch, 4 Horizontal, 5 Vertical
            if (stretch == 3) { psr.lengthScale = F(rd, "m_LengthScale", 2f); psr.velocityScale = F(rd, "m_VelocityScale", 0f); psr.cameraVelocityScale = F(rd, "m_CameraVelocityScale", 0f); }
            psr.maxParticleSize = Mathf.Clamp(F(rd, "m_MaxParticleSize", 0.5f), 0.05f, 1f);
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; psr.receiveShadows = false;
            psr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; psr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (mat != null) psr.sharedMaterial = mat; else psr.enabled = false;
            if (go.activeInHierarchy) ps.Play(true);
            ParticleDensity.Tag(ps);
            return ps;
        }
    }
}
