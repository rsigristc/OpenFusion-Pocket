// Contratos entre GameClient y las pantallas. SIN COMPILAR EN UNITY.
using OpenFusion.Net;

namespace OpenFusion.Screens
{
    /// <summary>Servidor guardado por el launcher (formato análogo al servers.json del OpenFusionLauncher).</summary>
    [System.Serializable] public sealed class ServerEntry { public string uuid, description, endpoint, ip, version; }

    /// <summary>Lo que el launcher entrega al pulsar Connect: la API a usar y las credenciales que escribió la persona (solo en memoria).</summary>
    public sealed class LauncherResult { public string ServerUrl, ServerId, ServerName, Username, Password, LoginAddress, VersionUuid; }

    public enum SelectAction { Play, Create, Quit }
    public sealed class SelectResult { public SelectAction Action; public CharInfo Char; public int Slot; }
}
