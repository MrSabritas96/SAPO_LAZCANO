using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimuladorBloqueos
{
    /// <summary>
    /// Cámara orbital en tercera persona:
    /// - Control con el ratón (ojos del personaje / ángulo de visión).
    /// - Seguimiento suave del jugador.
    /// - Bloqueo de cursor y límites de inclinación vertical (pitch).
    /// - Compatible 100% con el nuevo Input System de Unity.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Objetivo")]
        public Transform target;
        public Vector3 targetOffset = new Vector3(0, 1.35f, 0); // Altura de hombros/cabeza

        [Header("Distancia y Ángulos")]
        public float distance = 3.8f;
        public float minPitch = -25f;
        public float maxPitch = 65f;
        public float mouseSensitivityX = 1.6f;
        public float mouseSensitivityY = 1.3f;

        [Header("Suavizado")]
        public float smoothSpeed = 15f;

        private float _yaw = 0f;
        private float _pitch = 15f;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (target != null)
            {
                _yaw = target.eulerAngles.y;
            }
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            bool isDecisionUIOpen = SimuladorBloqueos.Decisions.DecisionManager.IsAnyDecisionUIOpen;

            // Desbloqueo y bloqueo de cursor con New Input System (solo si no hay panel de decisiones activo)
            if (!isDecisionUIOpen && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Lectura del movimiento del ratón
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                _yaw += delta.x * 0.1f * mouseSensitivityX;
                _pitch -= delta.y * 0.1f * mouseSensitivityY;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }
#endif
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 focusPoint = target.position + targetOffset;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
            Vector3 desiredPosition = focusPoint - (rotation * Vector3.forward * distance);

            // Suavizado de posición de cámara
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
            transform.LookAt(focusPoint);
        }
    }
}
