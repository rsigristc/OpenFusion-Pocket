// NpcIconFx.cs -- exact replica of the original FusionFall (Retrobution, Unity 2.5) NPC head icons and quest markers.
// Unity 2022 (Built-in or URP), no external dependencies. NOT compiled inside a Unity project (syntax-checked against a stub only).
// Every number below was read from the original client data (effects bundle ES[n] prefabs, PS[n] particle prefabs,
// materials/shaders, AnimationClips) and from the decompiled code (NpcMoveController, EffectEmitterController,
// ParticleEmitterController). See REPORT.md for the raw values and their sources.
//
// TWO different things exist in the original:
//  1) SERVICE ICONS (ES[669..686], ES[824..826], ES[670/671]): ONE legacy ParticleRenderer particle, 1 s life, re-emitted every
//     1 s, VerticalBillboard, size animated by a width curve, additive material. -> NpcIconFx + NpcIconBillboard.
//  2) QUEST MARKERS ES[865] ('?', "progress") / ES[866] ('!', "acquire"): NOT particles. A 3D skinned mesh with a looping 8 s
//     animation (spin -45 deg/s about Y + bob) and a custom "HologramSolid_Additive" shader.  -> NpcQuestMarkerFx.
using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    /// <summary>Static data + maths for the NPC head icons (service icons).</summary>
    public static class NpcIconFx
    {
        public enum BlendKind
        {
            /// <summary>Shader "particle_blendOneOne_zwriteOff_cullOff": Blend One One. (all icons except ES 669)</summary>
            Additive = 0,
            /// <summary>Shader "particle_blendSrcalphaInvsrccolor_zwriteOff_cullOff": Blend SrcAlpha OneMinusSrcColor. (ES 669 game_icon_17_d only)</summary>
            SrcAlphaInvSrcColor = 1
        }

        public struct Spec
        {
            public int Effect;          // NpcTable.m_iEffect  (= ES index)
            public string Texture;      // main texture name (as in the bundle, without .dds)
            public float SizeEnds;      // widthCurve value at age 0 and 1   (world units = metres)
            public float SizeMid;       // widthCurve value at age 0.5
            public bool Rocks;          // has a rotationCurve (ES 684, 685)
            public BlendKind Blend;
        }

        /// <summary>Particle life (ParticleEmitterController.lifeTime). Emission every 1/generationsPerSecond = 1 s.</summary>
        public const float Life = 1f;
        /// <summary>Original: effect object parented to the "Bip01 Head" bone with localPosition (-0.6,0,0) (bone X axis points DOWN, so this is +0.6 m above the bone).</summary>
        public static readonly Vector3 HeadBoneLocalOffset = new Vector3(-0.6f, 0f, 0f);
        /// <summary>Fallback when the model has no "Bip01 Head": NPC root + Vector3.up * (fHeight * 0.9), fHeight = NpcTable.m_iHeight * 0.01.</summary>
        public const float RootFallbackHeightFactor = 0.9f;
        /// <summary>Quest markers use NPC root + up * (fHeight * 0.85) (they do not look for the head bone).</summary>
        public const float QuestMarkerHeightFactor = 0.85f;

        static readonly Dictionary<int, Spec> specs = Build();

        static Dictionary<int, Spec> Build()
        {
            var d = new Dictionary<int, Spec>();
            void Add(int es, string tex, float ends, float mid, bool rocks = false, BlendKind b = BlendKind.Additive)
                => d[es] = new Spec { Effect = es, Texture = tex, SizeEnds = ends, SizeMid = mid, Rocks = rocks, Blend = b };
            Add(669, "game_icon_17_d", 0.35f, 0.30f, false, BlendKind.SrcAlphaInvSrcColor);
            Add(670, "ep_icon_02", 0.35f, 0.30f);
            Add(671, "ep_icon_03", 0.50f, 0.45f);
            for (int i = 0; i < 7; i++) Add(672 + i, "game_icon_" + (1 + i), 0.35f, 0.30f);   // 672..678 -> game_icon_1..7
            for (int i = 0; i < 3; i++) Add(679 + i, "game_icon_" + (8 + i), 0.35f, 0.30f);   // 679..681 -> game_icon_8..10
            Add(682, "game_icon_13", 0.35f, 0.30f);
            Add(683, "game_icon_14", 0.35f, 0.30f);
            Add(684, "game_icon_15", 0.35f, 0.30f, true);
            Add(685, "game_icon_16", 0.35f, 0.30f, true);
            Add(686, "game_icon_11", 0.35f, 0.30f);
            Add(824, "gameicon_vehicle", 0.40f, 0.35f);
            Add(825, "gameicon_combine", 0.35f, 0.30f);
            Add(826, "gameicon_egger", 0.35f, 0.30f);
            return d;
        }

        public static bool TryGetSpec(int effect, out Spec s) => specs.TryGetValue(effect, out s);

        /// <summary>
        /// Port of NpcMoveController.MakeGameIcon(): which ES to show for an NPC. Returns -1 for "no icon".
        /// ownXcomMatchesThisNpc: type 17 only, (cnOwnAvatarStatus.iRXCom == this NPC's id) -> effect 811 (yellow portal, not handled here).
        /// ringRace: cnOwnAvatarStatus.bRingRace.
        /// </summary>
        public static int ChooseEffect(int npcType, int tableEffect, bool ownXcomMatchesThisNpc, bool ringRace)
        {
            int e = -1;                                                    // iParticleNum initial value
            if (npcType == 17) e = ownXcomMatchesThisNpc ? 811 : tableEffect;
            else if (npcType == 13) { if (!ringRace) e = tableEffect; }
            else if (npcType == 14) { if (ringRace) e = tableEffect; }
            else if (npcType != 0 && npcType != 6 && tableEffect != 0) e = tableEffect;
            return (e >= 0 && e != 30) ? e : -1;                           // MakeGameIcon(int): iParticleNum >= 0 && != 30
        }

        // ---- animation curves (Unity AnimationCurve = same Hermite evaluation as the original runtime) -----------------
        static AnimationCurve SizeCurve(float ends, float mid)
        {
            return new AnimationCurve(new Keyframe(0f, ends, 0f, -0.1f), new Keyframe(0.5f, mid, -0.1f, 0.1f), new Keyframe(1f, ends, 0.1f, 0f));
        }
        static readonly AnimationCurve rockCurve = new AnimationCurve(   // degrees (values are 0.5 rad * 57.2958)
            new Keyframe(0f, 0f, 0f, 57.296f), new Keyframe(0.5f, 28.6479f, 57.296f, -57.296f), new Keyframe(1f, 0f, -57.296f, 0f));
        static readonly Dictionary<int, AnimationCurve> sizeCache = new Dictionary<int, AnimationCurve>();

        /// <summary>Quad edge length (world units) at normalised particle age 0..1.</summary>
        public static float Size(in Spec s, float age01)
        {
            if (!sizeCache.TryGetValue(s.Effect, out var c)) sizeCache[s.Effect] = c = SizeCurve(s.SizeEnds, s.SizeMid);
            return c.Evaluate(Mathf.Clamp01(age01));
        }
        /// <summary>Roll of the quad around the view axis, degrees (only ES 684/685 have a rotation curve).</summary>
        public static float RotationDeg(in Spec s, float age01) => s.Rocks ? rockCurve.Evaluate(Mathf.Clamp01(age01)) : 0f;

        /// <summary>Vertex colour: ParticleAnimator colorAnimation[0..4] are all 0xFFFFFFFF -> constant white, alpha 1. NO fade-in/out exists in the data.</summary>
        public static readonly Color VertexColor = Color.white;

        /// <summary>Per-icon state reproducing the original "emit one particle per second at the CURRENT anchor position; the particle then stays put (Simulate in Worldspace = true)".</summary>
        public struct State
        {
            public float CycleStart;       // time the current particle was emitted
            public Vector3 SnappedPos;     // world position where it was emitted
            public bool Started;
        }

        public struct Frame
        {
            public Vector3 Position;       // world-space centre of the quad
            public float Size;             // edge length, world units
            public float RollDeg;          // roll around the view axis
            public float Age01;
        }

        /// <summary>
        /// Advance one frame. anchorNow = current world anchor (see Anchor()). time = Time.time.
        /// snapToCycle = true reproduces the original exactly (position refreshed once per second, icon does not follow the NPC in between).
        /// snapToCycle = false makes the icon follow the anchor every frame (smoother; NOT the original behaviour).
        /// </summary>
        public static Frame Evaluate(in Spec s, ref State st, Vector3 anchorNow, float time, bool snapToCycle = true)
        {
            if (!st.Started) { st.Started = true; st.CycleStart = time; st.SnappedPos = anchorNow; }
            while (time - st.CycleStart >= Life) { st.CycleStart += Life; st.SnappedPos = anchorNow; }
            float age = (time - st.CycleStart) / Life;
            return new Frame
            {
                Position = snapToCycle ? st.SnappedPos : anchorNow,
                Size = Size(s, age),
                RollDeg = RotationDeg(s, age),
                Age01 = age
            };
        }

        /// <summary>
        /// World anchor of the icon. head = transform named "Bip01 Head" (may be null). npcRoot = NPC root. tableHeight = NpcTable.m_iHeight * 0.01.
        /// Original (GameIcon node does not exist in the cached NPC models): head.TransformPoint((-0.6,0,0)); else root + up*(h*0.9).
        /// </summary>
        public static Vector3 Anchor(Transform npcRoot, Transform head, float tableHeight)
        {
            if (head != null) return head.TransformPoint(HeadBoneLocalOffset);
            return npcRoot.position + Vector3.up * (tableHeight * RootFallbackHeightFactor);
        }

        /// <summary>
        /// Legacy ParticleRenderMode 5 = VerticalBillboard: the quad faces the camera POSITION but keeps its Y axis = world up (yaw-only billboard).
        /// Returns the quad's half-axes already multiplied by size/2 (so corners = pos +-right +-up).
        /// </summary>
        public static void BillboardAxes(Vector3 pos, Vector3 camPos, float size, float rollDeg, out Vector3 right, out Vector3 up)
        {
            Vector3 toCam = camPos - pos; toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-8f) toCam = Vector3.back; else toCam.Normalize();
            Vector3 viewFwd = -toCam;                                  // direction the camera looks (horizontally)
            Vector3 r = Vector3.Cross(Vector3.up, viewFwd).normalized;  // camera-right in Unity's left-handed system
            Vector3 u = Vector3.up;
            if (rollDeg != 0f)
            {
                var q = Quaternion.AngleAxis(-rollDeg, viewFwd);        // positive = counter-clockwise as seen by the camera (assumption, see REPORT)
                r = q * r; u = q * u;
            }
            right = r * (size * 0.5f); up = u * (size * 0.5f);
        }

        /// <summary>For IMGUI users: screen-space rectangle (GUI coordinates, y down) of the icon. Returns false if behind the camera.</summary>
        public static bool ScreenRect(Camera cam, Vector3 pos, float size, out Rect rect)
        {
            rect = default;
            Vector3 c = cam.WorldToScreenPoint(pos);
            if (c.z <= 0.01f) return false;
            Vector3 r = cam.WorldToScreenPoint(pos + cam.transform.right * (size * 0.5f));
            float half = Mathf.Abs(r.x - c.x);
            rect = new Rect(c.x - half, Screen.height - c.y - half, half * 2f, half * 2f);
            return true;
        }
    }

    /// <summary>
    /// World-space quad renderer for one NPC icon. Needs a material using the shader in NpcIconFx.shader ("OpenFusion/NpcIconParticle").
    /// Usage: var b = npc.AddComponent&lt;NpcIconBillboard&gt;(); b.Init(effectId, iconTexture, rootTransform, headBone, tableHeightMeters, material);
    /// Draws via Graphics.DrawMesh (no GameObject per icon, no sorting issues beyond the shader queue Transparent+3).
    /// </summary>
    public sealed class NpcIconBillboard : MonoBehaviour
    {
        public bool SnapToCycle = true;
        /// <summary>true mientras el NPC muestra un simbolo de mision (SetQuestSymbol destruye el icono de servicio).</summary>
        public bool Hidden;
        public Camera Cam;                       // default Camera.main
        NpcIconFx.Spec spec; NpcIconFx.State st; Transform root, head; float height; Material mat; Mesh mesh; bool ok;
        readonly Vector3[] v = new Vector3[4];
        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        public void Init(int effect, Texture2D icon, Transform npcRoot, Transform headBone, float tableHeightMeters, Material sharedMaterialTemplate)
        {
            ok = NpcIconFx.TryGetSpec(effect, out spec);
            if (!ok) return;
            root = npcRoot; head = headBone; height = tableHeightMeters;
            mat = new Material(sharedMaterialTemplate) { mainTexture = icon };
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", spec.Blend == NpcIconFx.BlendKind.Additive ? (int)UnityEngine.Rendering.BlendMode.One : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcColor);
            if (spec.Blend == NpcIconFx.BlendKind.SrcAlphaInvSrcColor) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.renderQueue = 3003;              // "Queue" = "Transparent+3"
            mesh = new Mesh { name = "NpcIconQuad" };
            mesh.vertices = new Vector3[4];
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };    // Cull Off in the shader, winding is irrelevant
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        }

        void LateUpdate()
        {
            if (!ok || Hidden || GameMenu.Blocking) return;   // con el menu o una ventana abierta no se dibuja (el icono es 3D y se veia por encima de la interfaz)
            var cam = Cam != null ? Cam : Camera.main; if (cam == null || root == null) return; if ((cam.transform.position - root.position).sqrMagnitude > 90f * 90f) return;
            Vector3 anchor = NpcIconFx.Anchor(root, head, height);
            var f = NpcIconFx.Evaluate(spec, ref st, anchor, Time.time, SnapToCycle);
            NpcIconFx.BillboardAxes(f.Position, cam.transform.position, f.Size, f.RollDeg, out var r, out var u);
            v[0] = f.Position - r - u; v[1] = f.Position + r - u; v[2] = f.Position + r + u; v[3] = f.Position - r + u;
            mesh.vertices = v;                                   // vertices are in WORLD space -> draw with identity matrix
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, gameObject.layer, null, 0, null, false, false);
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh); if (mat != null) Destroy(mat); }
    }
}
