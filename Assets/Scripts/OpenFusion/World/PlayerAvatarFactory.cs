// Crea el avatar 3D de un jugador (propio o ajeno) a partir de sPCAppearanceData / sPCLoadData2CL.
// SIN COMPILAR NI PROBAR EN UNITY (usa ActorAssembler). La lógica de elección de piezas (PlayerLookBuilder) y el parseo de red están verificados en tests.
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class AvatarModelRoot : MonoBehaviour { }
    public sealed class AvatarOutfitStamp : MonoBehaviour { public string Signature; }

    public sealed class PlayerAvatarFactory
    {
        readonly PlayerLookBuilder looks; readonly ActorAssembler assembler; readonly ModelFactory models;
        public bool CombineEquip = true;      // localized.combine del cliente original (Opt >> 16 = aspecto alternativo); Croc Pot en Retrobution
        public PlayerAvatarFactory(XdtTableScript tables, ModelFactory models) { this.models = models; looks = new PlayerLookBuilder(tables); assembler = new ActorAssembler(models); }
        public ActorAssembler Assembler => assembler;
        /// Cambia el arma visible de un avatar ya creado (equipo con el arma nueva ya aplicada en `a.Equip`).
        public void RefreshWeapon(GameObject holder, PcAppearance a)
        {
            if (holder == null) return; var an = holder.GetComponentInChildren<Animation>(true); if (an == null) return;
            var look = looks.Build(a.Style, a.VisibleEquip(CombineEquip)); if (look == null) return;
            assembler.ReplaceAttach(an.gameObject, look, (int)ActorMerge.Attach.RightPistol);
        }

        public static string OutfitSignature(PcAppearance a, bool combine = true)
        {
            var e = a.VisibleEquip(combine);
            return e.Shirt + ":" + e.Pants + ":" + e.Shoes + ":" + e.Hat + ":" + e.Glass + ":" + e.Back;
        }
        // Keep the network holder, movement, target references and session intact.
        public bool RefreshEquipment(GameObject holder, PcAppearance a)
        {
            if (holder == null || a == null) return false;
            var stamp = holder.GetComponent<AvatarOutfitStamp>();
            string signature = OutfitSignature(a, CombineEquip);
            if (stamp != null && stamp.Signature == signature) { RefreshWeapon(holder, a); return true; }
            var oldAnimation = holder.GetComponentInChildren<Animation>(true); if (oldAnimation == null) return false;
            var oldRoot = oldAnimation.transform; while (oldRoot.parent != null && oldRoot.parent != holder.transform) oldRoot = oldRoot.parent;
            if (oldRoot.parent != holder.transform) return false;
            var look = looks.Build(a.Style, a.VisibleEquip(CombineEquip)); if (look == null) return false;
            // Defer replacement while a required downloaded asset is still unavailable.
            foreach (var path in look.RequiredPaths()) if (!string.IsNullOrEmpty(path) && !models.Locate(path).Ok) return false;
            var model = assembler.Build(look, null, holder.transform); if (model == null) return false;
            model.transform.localPosition = oldRoot.localPosition; model.transform.localRotation = oldRoot.localRotation; model.transform.localScale = oldRoot.localScale;
            model.AddComponent<AvatarModelRoot>();
            WorldEntities.ActivateAll(model); WorldEntities.PlayIdle(model, "equipment refresh");
            var animation = model.GetComponentInChildren<Animation>(true);
            if (oldAnimation.GetComponent<AvatarEmoteGuard>() != null && animation != null) animation.gameObject.AddComponent<AvatarEmoteGuard>();
            var remote = holder.GetComponent<NpcAnimator>();
            if (remote != null) { bool dead = remote.Dead; remote.Init(model, null, null); if (dead) { remote.Dead = false; remote.PlayerDie(); } }
            oldRoot.gameObject.SetActive(false); oldRoot.SetParent(null, true); Object.Destroy(oldRoot.gameObject);
            if (stamp == null) stamp = holder.AddComponent<AvatarOutfitStamp>(); stamp.Signature = signature;
            return true;
        }

        // Repair an empty remote avatar after shared bundles become available. Preserve id,
        // world transform, movement and combat references; discard the old model only on success.
        public bool RepairRemoteModel(GameObject holder, PcAppearance appearance)
        {
            if (holder == null || appearance == null) return false;
            if (WorldEntities.HasPlayerModel(holder)) return true;
            var look = looks.Build(appearance.Style, appearance.VisibleEquip(CombineEquip));
            if (look == null || !models.Locate(look.Body).Ok) return false;
            foreach (var cloth in look.Cloth) if (!string.IsNullOrEmpty(cloth) && !models.Locate(cloth).Ok) return false;
            var model = assembler.Build(look, null, holder.transform);
            if (model == null) return false;
            if (!WorldEntities.HasPlayerModel(model)) { Object.Destroy(model); return false; }
            RetireRemoteModel(holder, model);
            model.AddComponent<AvatarModelRoot>();
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.Euler(0, 180, 0); model.transform.localScale = Vector3.one;
            WorldEntities.ActivateAll(model); WorldEntities.PlayIdle(model, "remote recovery");
            var stamp = holder.GetComponent<AvatarOutfitStamp>(); if (stamp == null) stamp = holder.AddComponent<AvatarOutfitStamp>();
            stamp.Signature = OutfitSignature(appearance, CombineEquip); return true;
        }

        public static void RetireRemoteModel(GameObject holder, GameObject replacement)
        {
            var obsolete = new List<GameObject>();
            foreach (Transform child in holder.transform)
                if (child.gameObject != replacement && (child.GetComponent<AvatarModelRoot>() != null || child.name == "RemoteAvatarPlaceholder")) obsolete.Add(child.gameObject);
            foreach (var child in obsolete) { child.SetActive(false); Object.Destroy(child); }
        }

        /// Rutas de asset que hay que tener descargadas antes de montar el avatar.
        public IEnumerable<string> Required(PcAppearance a) { var l = looks.Build(a.Style, a.VisibleEquip(CombineEquip)); return l == null ? new string[0] : l.RequiredPaths(); }

        /// Conversión de coordenadas del servidor (CoordUtil.ServerToClient): x -> x, z -> y, y -> z, escala 0,01.
        public static Vector3 ToClient(int x, int y, int z) => new Vector3(x * 0.01f, z * 0.01f, y * 0.01f);

        /// Instancia el avatar. El contenedor recibe la posición del servidor y el ángulo; el modelo va girado 180° como en el cliente.
        public GameObject Create(PcAppearance a, Transform parent = null, bool own = false)
        {
            var look = looks.Build(a.Style, a.VisibleEquip(CombineEquip)); if (look == null) { Debug.LogWarning("Género inválido " + a.Style.Gender); return null; }
            var holder = new GameObject((own ? "Player " : "Player#" + a.Id + " ") + a.FirstName + " " + a.LastName);
            if (parent != null) holder.transform.SetParent(parent, false);
            var pos = ToClient(a.X, a.Y, a.Z);   // el original suma 1 m y deja caer al avatar con física; aquí no hay física, así que se coloca en el suelo
            pos.y = Ground.Y(pos); holder.transform.position = pos; holder.transform.rotation = Quaternion.Euler(0f, own ? a.Angle + 180 : a.Angle, 0f);
            var model = assembler.Build(look, null, holder.transform);
            if (model != null) { model.AddComponent<AvatarModelRoot>(); WorldEntities.ActivateAll(model); }
            if (model != null) { model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.Euler(0f, own ? 0f : 180f, 0f); model.transform.localScale = Vector3.one; }
            if (model != null) WorldEntities.PlayIdle(model, own ? "yo" : "jugador");
            holder.AddComponent<AvatarOutfitStamp>().Signature = OutfitSignature(a, CombineEquip);
            BlobShadow.Attach(holder, 1.1f);
            return holder;
        }
    }
}
