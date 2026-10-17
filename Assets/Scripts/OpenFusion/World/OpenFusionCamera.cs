// Cámara de pruebas: sigue al jugador propio cuando el servidor lo ha creado; si no, vuelo libre (WASD/QE/ratón derecho, o un dedo para desplazar y dos para subir/bajar).
// SIN COMPILAR NI PROBAR EN UNITY.
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class OpenFusionCamera : MonoBehaviour
    {
        public WorldBootstrap Bootstrap;
        public Vector3 FollowOffset = new Vector3(0f, 7f, -9f);
        public float FlySpeed = 60f, TouchSpeed = 0.5f, Look = 2f;
        float yaw, pitch;

        void Start() { var e = transform.eulerAngles; yaw = e.y; pitch = e.x; }

        void LateUpdate()
        {
            if (Bootstrap != null && Bootstrap.Entities != null && Bootstrap.Player != null)
            {
                var p = Bootstrap.Player.position; transform.position = p + FollowOffset; transform.LookAt(p + new Vector3(0f, 1.5f, 0f)); return;
            }
            float dt = Time.deltaTime, sp = FlySpeed * (Input.GetKey(KeyCode.LeftShift) ? 4f : 1f) * dt;
            var move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")) * sp;
            if (Input.GetKey(KeyCode.E)) move.y += sp; if (Input.GetKey(KeyCode.Q)) move.y -= sp;
            if (Input.GetMouseButton(1)) { yaw += Input.GetAxis("Mouse X") * Look; pitch -= Input.GetAxis("Mouse Y") * Look; }
            if (Input.touchCount == 1) { var d = Input.GetTouch(0).deltaPosition; move += new Vector3(-d.x, 0f, -d.y) * TouchSpeed * 0.2f; }
            else if (Input.touchCount >= 2) { move.y += Input.GetTouch(0).deltaPosition.y * TouchSpeed * 0.2f; }
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position += Quaternion.Euler(0f, yaw, 0f) * new Vector3(move.x, 0f, move.z) + new Vector3(0f, move.y, 0f);
        }
    }
}
