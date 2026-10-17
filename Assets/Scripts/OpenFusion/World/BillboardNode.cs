// BillboardNode.cs -- port de BillboardNode (cliente original, Assembly-CSharp-first_pass): planos "*_BillboardCamera" (halos de farolas, destellos, carteles de luz)
// que giran cada fotograma hacia la cámara. mode 0 = Camera (rotación de la cámara girada 180 en Y), 1 = Up (solo guiñada, vertical). Sin esto los halos quedan
// con la orientación fija del modelo y se ven de canto o invisibles. SIN COMPILAR EN UNITY.
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class BillboardNode : MonoBehaviour
    {
        public int Mode;   // 0 Camera, 1 Up (2..4 RigidCamera/Center/RigidCenter: el original no hace nada)
        static Quaternion camRot = Quaternion.identity, upRot = Quaternion.identity; static int lastFrame = -1; static Camera cam;

        static void Refresh()
        {
            lastFrame = Time.frameCount;
            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main; if (cam == null) return;
            camRot = cam.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            var f = cam.transform.forward; f.y = 0f;
            if (f.sqrMagnitude > 1e-12f) upRot = Quaternion.LookRotation(f) * Quaternion.Euler(0f, 180f, 0f);
        }

        // VisibleSwitch del original (script gemelo en el mismo plano): el BillboardNode solo se ejecuta mientras el renderer es visible (OnBecameVisible/OnBecameInvisible).
        // Sin renderer propio nunca llegan los mensajes y se queda en true (siempre activo).
        bool visible = true;
        void OnBecameVisible() { visible = true; }
        void OnBecameInvisible() { visible = false; }

        void LateUpdate()
        {
            if (!visible) return;
            if (lastFrame != Time.frameCount) Refresh();
            if (cam == null) return;
            if (Mode == 0) transform.rotation = camRot; else if (Mode == 1) transform.rotation = upRot;
        }
    }
}
