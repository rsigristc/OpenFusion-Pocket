using System.Collections.Generic;
using UnityEngine;

namespace OpenFusion.World
{
    // Solo recursos creados exclusivamente para este objeto; nunca assets compartidos.
    public sealed class OwnedWorldResources : MonoBehaviour
    {
        readonly List<Object> resources = new List<Object>();
        public void Track(Object resource) { if (resource != null) resources.Add(resource); }
        void OnDestroy()
        {
            foreach (var resource in resources) if (resource != null)
            {
                if (Application.isPlaying) Destroy(resource);
                else DestroyImmediate(resource);
            }
            resources.Clear();
        }
    }
}
