// Crea el avatar 3D de un jugador (propio o ajeno) a partir de sPCAppearanceData / sPCLoadData2CL.
// SIN COMPILAR NI PROBAR EN UNITY (usa ActorAssembler). La lógica de elección de piezas (PlayerLookBuilder) y el parseo de red están verificados en tests.
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class PlayerAvatarFactory
    {
        readonly PlayerLookBuilder looks; readonly ActorAssembler assembler;
        public bool CombineEquip;      // localized.combine del cliente original (Opt >> 16 = aspecto alternativo); desconocido para Retrobution
        public PlayerAvatarFactory(XdtTableScript tables, ModelFactory models) { looks = new PlayerLookBuilder(tables); assembler = new ActorAssembler(models); }
        public ActorAssembler Assembler => assembler;
        /// Cambia el arma visible de un avatar ya creado (equipo con el arma nueva ya aplicada en `a.Equip`).
        public void RefreshWeapon(GameObject holder, PcAppearance a)
        {
            if (holder == null) return; var an = holder.GetComponentInChildren<Animation>(true); if (an == null) return;
            var look = looks.Build(a.Style, a.VisibleEquip(CombineEquip)); if (look == null) return;
            assembler.ReplaceAttach(an.gameObject, look, (int)ActorMerge.Attach.RightPistol);
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
            if (model != null) WorldEntities.ActivateAll(model);
            if (model != null) { model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.Euler(0f, own ? 0f : 180f, 0f); model.transform.localScale = Vector3.one; }
            if (model != null) WorldEntities.PlayIdle(model, own ? "yo" : "jugador");
            BlobShadow.Attach(holder, 1.1f);
            return holder;
        }
    }
}
