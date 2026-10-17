// Avatar por piezas del cliente original (ActorSkinCombiner / ActorWearSkinTable / ActorWearIndexTable), parte pura sin Unity.
// Las piezas de ropa (wear/*.nif) tienen mallas con huesos propios; el combinador las funde en UNA malla y reasigna los huesos
// a los del esqueleto del actor (Actor/m.kfm, Actor/w.kfm) mediante tablas de índices (una por pieza y sexo).
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFusion.Unity25
{
    /// MonoBehaviour ActorSkinCombiner (en la raíz del actor): huesos del actor, y los campos de ajuste (altura/forma, colores, sexo).
    public sealed class ActorCombiner25
    {
        public PPtr25[] ActorBones; public bool Female; public float Height, Shape;
        public static bool IsMatch(Dictionary<string, object> v) => v.ContainsKey("actorBones") && v.ContainsKey("clothes");
        public static ActorCombiner25 From(Dictionary<string, object> v) => new ActorCombiner25
        {
            ActorBones = D.A(v["actorBones"]).Select(PPtr25.From).ToArray(),
            Female = v.ContainsKey("female") && D.I(v["female"]) != 0,
            Height = v.ContainsKey("height") ? D.F(v["height"]) : 0f, Shape = v.ContainsKey("shape") ? D.F(v["shape"]) : 0f
        };
    }

    /// MonoBehaviour ActorWearSkinTable (raíz de una pieza de ropa): sus SkinnedMeshRenderer.
    public sealed class ActorWearSkins25
    {
        public PPtr25[] Skins;
        public static bool IsMatch(Dictionary<string, object> v) => v.ContainsKey("skins");
        public static ActorWearSkins25 From(Dictionary<string, object> v) => new ActorWearSkins25 { Skins = D.A(v["skins"]).Select(PPtr25.From).ToArray() };
    }

    /// MonoBehaviour ActorWearIndexTable (junto a cada SkinnedMeshRenderer de ropa): hueso i de la pieza -> índice en actorBones.
    public sealed class ActorWearIndex25
    {
        public int[] M, F;
        public static bool IsMatch(Dictionary<string, object> v) => v.ContainsKey("transformIndicesM") && v.ContainsKey("transformIndicesF");
        public static ActorWearIndex25 From(Dictionary<string, object> v) => new ActorWearIndex25 { M = D.A(v["transformIndicesM"]).Select(D.I).ToArray(), F = D.A(v["transformIndicesF"]).Select(D.I).ToArray() };
        public int[] For(bool female) => female ? F : M;
    }

    /// Una pieza ya resuelta: su malla (con huesos propios) y la tabla de índices.
    public sealed class WearPart { public Mesh25 Mesh; public ActorWearIndex25 Index; public int Slot; }

    public sealed class MergedActorMesh
    {
        public Mesh25 Mesh;                 // una malla, un submesh por pieza (submesh 0 de cada una, como CombineInstance.subMeshIndex = 0)
        public int[] ActorBoneIndex;        // por hueso de la malla fundida: índice en ActorCombiner25.ActorBones
        public int[] PartOfSubmesh;         // submesh -> índice de pieza en la lista de entrada
    }

    public static class ActorMerge
    {
        /// Fusión equivalente a Mesh.CombineMeshes(mergeSubMeshes:false, useMatrices:false) + reasignación de huesos de ActorSkinCombiner.Generate.
        public static MergedActorMesh Merge(IList<WearPart> parts, bool female)
        {
            if (parts.Count == 0) throw new ArgumentException("sin piezas");
            int nv = parts.Sum(p => p.Mesh.Positions.Length / 3), nb = parts.Sum(p => p.Mesh.BindPose.Length);
            var m = new Mesh25 { Name = "actor", Positions = new float[nv * 3], Normals = new float[nv * 3], UV = new float[nv * 2], BoneIdx = new int[nv * 4], BoneW = new float[nv * 4],
                BindPose = new float[nb][], SubmeshTriangles = new int[parts.Count][] };
            bool uv1 = parts.Any(p => p.Mesh.UV1 != null && p.Mesh.UV1.Length > 0), tan = parts.Any(p => p.Mesh.Tangents != null && p.Mesh.Tangents.Length > 0);
            if (uv1) m.UV1 = new float[nv * 2]; if (tan) m.Tangents = new float[nv * 4];
            var abi = new int[nb]; int v0 = 0, b0 = 0;
            for (int pi = 0; pi < parts.Count; pi++)
            {
                var s = parts[pi].Mesh; int n = s.Positions.Length / 3;
                if (s.BoneIdx == null || s.BoneW == null || s.BoneIdx.Length != n * 4) throw new ArgumentException($"la pieza {pi} ({s.Name}) no tiene pesos de huesos");
                Array.Copy(s.Positions, 0, m.Positions, v0 * 3, n * 3);
                if (s.Normals != null && s.Normals.Length == n * 3) Array.Copy(s.Normals, 0, m.Normals, v0 * 3, n * 3);
                if (s.UV != null && s.UV.Length == n * 2) Array.Copy(s.UV, 0, m.UV, v0 * 2, n * 2);
                if (uv1 && s.UV1 != null && s.UV1.Length == n * 2) Array.Copy(s.UV1, 0, m.UV1, v0 * 2, n * 2);
                if (tan && s.Tangents != null && s.Tangents.Length == n * 4) Array.Copy(s.Tangents, 0, m.Tangents, v0 * 4, n * 4);
                for (int i = 0; i < n * 4; i++) { m.BoneIdx[v0 * 4 + i] = s.BoneIdx[i] + b0; m.BoneW[v0 * 4 + i] = s.BoneW[i]; }
                for (int i = 0; i < s.BindPose.Length; i++) m.BindPose[b0 + i] = s.BindPose[i];
                var idx = parts[pi].Index?.For(female); if (idx == null || idx.Length < s.BindPose.Length) throw new ArgumentException($"la pieza {pi} ({s.Name}): tabla de índices de huesos ausente o corta");
                for (int i = 0; i < s.BindPose.Length; i++) abi[b0 + i] = idx[i];
                var tri = s.SubmeshTriangles.Length > 0 ? s.SubmeshTriangles[0] : new int[0]; var o = new int[tri.Length];
                for (int i = 0; i < tri.Length; i++) o[i] = tri[i] + v0;
                m.SubmeshTriangles[pi] = o;
                v0 += n; b0 += s.BindPose.Length;
            }
            m.AabbCenter = new float[3]; m.AabbExtent = new float[3]; Bounds(m);
            return new MergedActorMesh { Mesh = m, ActorBoneIndex = abi, PartOfSubmesh = Enumerable.Range(0, parts.Count).ToArray() };
        }

        static void Bounds(Mesh25 m)
        {
            float[] lo = { float.MaxValue, float.MaxValue, float.MaxValue }, hi = { float.MinValue, float.MinValue, float.MinValue };
            for (int i = 0; i < m.Positions.Length; i += 3) for (int k = 0; k < 3; k++) { lo[k] = Math.Min(lo[k], m.Positions[i + k]); hi[k] = Math.Max(hi[k], m.Positions[i + k]); }
            for (int k = 0; k < 3; k++) { m.AabbCenter[k] = (lo[k] + hi[k]) / 2; m.AabbExtent[k] = (hi[k] - lo[k]) / 2; }
        }

        /// Reglas de material del cliente (ActorSkinCombiner.Generate): según el NOMBRE del material de la pieza.
        public enum Role { Primary, Sub, Override }
        public static Role RoleOf(string materialName) => materialName.Contains("sub") ? Role.Sub : materialName.Contains("override") ? Role.Override : Role.Primary;
        public static bool IsHair(string materialName) => materialName.Contains("hair");

        /// Huecos de ropa en ActorSkinCombiner.clothes (0..4) y de objetos acoplados en AttachObject (0..5).
        public enum Cloth { Hair = 0, Face = 1, Shirt = 2, Pants = 3, Shoes = 4 }
        public enum Attach { Hat = 0, Glasses = 1, LeftPistol = 2, RightPistol = 3, Back = 4, Zipline = 5 }
        const string Spine1 = "Bip01/Bip01 NonAccum/Bip01 Pelvis/Bip01 Spine/Bip01 Spine1/";
        /// Ruta del hueso donde se cuelga cada objeto (relativa a la raíz del actor).
        public static string AttachPath(Attach a)
        {
            switch (a)
            {
                case Attach.Hat: return Spine1 + "Bip01 Neck/Bip01 Head/Bip01 helmet01";
                case Attach.Glasses: return Spine1 + "Bip01 Neck/Bip01 Head/Bip01 glass01";
                case Attach.LeftPistol: return Spine1 + "Bip01 Neck/Bip01 L Clavicle/Bip01 L UpperArm/Bip01 L Forearm/Bip01 L Hand/Bip01 Lweapon01";
                case Attach.RightPistol: case Attach.Zipline: return Spine1 + "Bip01 Neck/Bip01 R Clavicle/Bip01 R UpperArm/Bip01 R Forearm/Bip01 R Hand/Bip01 Rweapon01";
                default: return Spine1 + "Bip01 back01";
            }
        }
    }
}
