using UnityEngine;
namespace OpenFusion.World
{
    /// <summary>Marca los objetos acoplados a un hueso (arma, sombrero...) para poder sustituirlos (ActorAssembler.ReplaceAttach).</summary>
    public sealed class ActorAttachTag : MonoBehaviour { public int Slot; }
}
