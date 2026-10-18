using UnityEngine;

namespace OpenFusion
{
    public sealed partial class GameClient
    {
        int cameraAuditFrames, cameraAuditReports;
        float cameraAuditAt, cameraAuditPositionSpan, cameraAuditAngleSpan, cameraAuditPlayerSpan;
        Vector3 cameraAuditPosition, cameraAuditPlayer;
        float cameraAuditMinY, cameraAuditMaxY, cameraAuditLook;
        Quaternion cameraAuditRotation;
        void AuditCamera(Camera camera, Transform player)
        {
            if (cameraAuditReports >= 8) return;
            if (cameraAuditFrames == 0) {
                cameraAuditAt = Time.unscaledTime; cameraAuditPosition = camera.transform.position;
                cameraAuditPlayer = player.position; cameraAuditRotation = camera.transform.rotation;
                cameraAuditPositionSpan = cameraAuditAngleSpan = cameraAuditPlayerSpan = 0;
                cameraAuditMinY = cameraAuditMaxY = player.position.y; cameraAuditLook = 0;
            }
            cameraAuditFrames++;
            cameraAuditPositionSpan = Mathf.Max(cameraAuditPositionSpan, Vector3.Distance(cameraAuditPosition, camera.transform.position));
            cameraAuditPlayerSpan = Mathf.Max(cameraAuditPlayerSpan, Vector3.Distance(cameraAuditPlayer, player.position));
            cameraAuditAngleSpan = Mathf.Max(cameraAuditAngleSpan, Quaternion.Angle(cameraAuditRotation, camera.transform.rotation));
            cameraAuditMinY = Mathf.Min(cameraAuditMinY, player.position.y); cameraAuditMaxY = Mathf.Max(cameraAuditMaxY, player.position.y);
            cameraAuditLook = Mathf.Max(cameraAuditLook, new Vector2(World.ControlMap.CameraX, World.ControlMap.CameraY).magnitude);
            if (Time.unscaledTime - cameraAuditAt < 5) return;
            Debug.Log("[camera stability] frames=" + cameraAuditFrames + " cameraSpan=" + cameraAuditPositionSpan.ToString("F5") +
                " angleSpan=" + cameraAuditAngleSpan.ToString("F5") + " playerSpan=" + cameraAuditPlayerSpan.ToString("F5") +
                " playerYRange=" + (cameraAuditMaxY - cameraAuditMinY).ToString("F5") + " maxPadLook=" + cameraAuditLook.ToString("F4") +
                " nearFar=" + camera.nearClipPlane.ToString("F3") + "/" + camera.farClipPlane.ToString("F1") + " graphics=" + SystemInfo.graphicsDeviceType);
            cameraAuditReports++; cameraAuditFrames = 0;
        }
    }
}
