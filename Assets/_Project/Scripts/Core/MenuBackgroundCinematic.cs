using UnityEngine;

namespace SimuladorBloqueos.Core
{
    /// <summary>
    /// Genera y anima un fondo 3D cinematográfico sobrio para el Menú Principal:
    /// Siluetas arquitectónicas y topográficas estilizadas en tonos grafito/plomo,
    /// con iluminación tenue y deriva sutil de cámara.
    /// Diseñado para ser reemplazado modularmente por la escena 3D de Plaza Murillo en la Fase 2.
    /// </summary>
    public class MenuBackgroundCinematic : MonoBehaviour
    {
        [Header("Contenedor Modular")]
        public Transform backgroundContainer;

        [Header("Cinemática de Cámara")]
        public Transform cameraTransform;
        public float driftSpeed = 0.08f;
        public float driftAmplitude = 0.4f;

        private Vector3 _initialCamPos;

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform != null)
            {
                _initialCamPos = cameraTransform.position;
            }
        }

        private void Update()
        {
            if (cameraTransform == null) return;

            // Movimiento suave y cinematográfico de cámara (sin saltos bruscos)
            float t = Time.unscaledTime * driftSpeed;
            float offsetX = Mathf.Sin(t) * driftAmplitude;
            float offsetY = Mathf.Cos(t * 0.7f) * (driftAmplitude * 0.4f);

            cameraTransform.position = _initialCamPos + new Vector3(offsetX, offsetY, 0);
        }
    }
}
