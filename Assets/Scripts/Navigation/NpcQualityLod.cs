using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Mantiene la calidad ORIGINAL completa (~400k vértices) en los efectivos más cercanos a la cámara
    /// y usa una malla de alta densidad en los más alejados para sostener altos FPS sin pérdida visual.
    /// </summary>
    public class NpcQualityLod : MonoBehaviour
    {
        public Mesh fullMesh;
        public Mesh lodMesh;
        public float fullMeshDistance = 14.0f;
        public int maxFullMeshCount = 12;
        public List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>(30);

        private float _nextUpdate;

        private void Update()
        {
            if (Time.unscaledTime < _nextUpdate) return;
            _nextUpdate = Time.unscaledTime + 0.45f;

            if (renderers == null || renderers.Count == 0 || fullMesh == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 camPos = cam.transform.position;
            float maxDistSq = fullMeshDistance * fullMeshDistance;
            int fullAssigned = 0;

            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                float dSq = (r.transform.position - camPos).sqrMagnitude;
                bool useFull = (fullAssigned < maxFullMeshCount && dSq <= maxDistSq) || lodMesh == null;
                if (useFull) fullAssigned++;
                Mesh target = useFull ? fullMesh : lodMesh;
                if (r.sharedMesh != target)
                    r.sharedMesh = target;
            }
        }
    }
}
