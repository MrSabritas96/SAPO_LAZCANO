using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimuladorBloqueos.CameraSystem
{
    /// <summary>
    /// Controlador de cámara dual de alta fidelidad:
    /// - Inicia en PRIMERA PERSONA por defecto.
    /// - Alterna dinámicamente entre Primera Persona y Tercera Persona con la tecla [V].
    /// - En Primera Persona: punto de vista a la altura de los ojos, giro con ratón y ocultación de la cabeza para evitar auto-oclusión.
    /// - En Tercera Persona: seguimiento orbital con verificación de colisiones (SphereCast) para no atravesar paredes.
    /// - Compatible 100% con New Input System.
    /// </summary>
    public class DualPerspectiveCamera : MonoBehaviour
    {
        [Header("Objetivo")]
        public Transform target;
        public PlayerController playerController;

        [Header("Modo de Vista")]
        public bool isFirstPerson = true; // Inicia en primera persona por defecto

        [Header("Primera Persona")]
        public float eyeHeight = 1.68f;
        public float fpMouseSensitivityX = 1.5f;
        public float fpMouseSensitivityY = 1.3f;
        public float minPitchFP = -80f;
        public float maxPitchFP = 80f;

        [Header("Tercera Persona")]
        public Vector3 tpTargetOffset = new Vector3(0, 1.45f, 0);
        public float tpDistance = 3.6f;
        public float minDistance = 0.8f;
        public float tpMouseSensitivityX = 1.6f;
        public float tpMouseSensitivityY = 1.3f;
        public float minPitchTP = -25f;
        public float maxPitchTP = 70f;
        public float smoothFollowSpeed = 16f;
        public float collisionRadius = 0.22f;
        public LayerMask obstacleLayers = ~0; // Todas las capas excepto jugador

        [Header("Renderers a Ocultar en Primera Persona")]
        public Renderer[] headRenderers;

        private float _yaw = 0f;
        private float _pitch = 0f;
        private Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;

            // Excluir capas de jugador o triggers de la máscara de colisión de cámara
            obstacleLayers &= ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (target != null)
            {
                _yaw = target.eulerAngles.y;
            }

            if (playerController == null && target != null)
            {
                playerController = target.GetComponent<PlayerController>();
            }

            FindHeadRenderers();
            ApplyPerspectiveMode(isFirstPerson, immediate: true);
        }

        private void Update()
        {
            ReadInput();
        }

        private void ReadInput()
        {
            // Si hay un panel de decisión, feedback o reporte abierto, nunca bloquear el cursor para permitir clics inmediatos
            bool isDecisionUIOpen = SimuladorBloqueos.Decisions.DecisionManager.IsAnyDecisionUIOpen;

#if ENABLE_INPUT_SYSTEM
            // Control de bloqueo del cursor (sólo si no hay menú/decisión activa y el puntero no está sobre la UI)
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

            // Alternancia de perspectiva con tecla [V]
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            {
                TogglePerspective();
            }

            // Lectura de ratón
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                float sensX = isFirstPerson ? fpMouseSensitivityX : tpMouseSensitivityX;
                float sensY = isFirstPerson ? fpMouseSensitivityY : tpMouseSensitivityY;

                _yaw += delta.x * 0.1f * sensX;
                _pitch -= delta.y * 0.1f * sensY;

                float minP = isFirstPerson ? minPitchFP : minPitchTP;
                float maxP = isFirstPerson ? maxPitchFP : maxPitchTP;
                _pitch = Mathf.Clamp(_pitch, minP, maxP);
            }
#else
            if (!isDecisionUIOpen && Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                TogglePerspective();
            }
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float sensX = isFirstPerson ? fpMouseSensitivityX : tpMouseSensitivityX;
                float sensY = isFirstPerson ? fpMouseSensitivityY : tpMouseSensitivityY;
                _yaw += Input.GetAxis("Mouse X") * sensX;
                _pitch -= Input.GetAxis("Mouse Y") * sensY;
                float minP = isFirstPerson ? minPitchFP : minPitchTP;
                float maxP = isFirstPerson ? maxPitchFP : maxPitchTP;
                _pitch = Mathf.Clamp(_pitch, minP, maxP);
            }
#endif
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (isFirstPerson)
            {
                UpdateFirstPerson();
            }
            else
            {
                UpdateThirdPerson();
            }
        }

        private void UpdateFirstPerson()
        {
            Vector3 eyePos = target.position + Vector3.up * eyeHeight;
            transform.position = eyePos;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0);

            // Orientar el cuerpo del jugador horizontalmente con la vista
            target.rotation = Quaternion.Euler(0, _yaw, 0);

            if (playerController != null)
            {
                playerController.isFirstPersonMode = true;
            }
        }

        private void UpdateThirdPerson()
        {
            if (playerController != null)
            {
                playerController.isFirstPersonMode = false;
            }

            Vector3 pivot = target.position + tpTargetOffset;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0);
            Vector3 desiredDir = -(rot * Vector3.forward);
            float targetDist = tpDistance;

            // Prevención de colisión con muros y obstáculos (SphereCast)
            Ray ray = new Ray(pivot, desiredDir);
            if (Physics.SphereCast(ray, collisionRadius, out RaycastHit hit, tpDistance, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                // No colisionar contra el propio jugador
                if (!hit.transform.IsChildOf(target))
                {
                    targetDist = Mathf.Clamp(hit.distance - 0.05f, minDistance, tpDistance);
                }
            }

            Vector3 targetCamPos = pivot + desiredDir * targetDist;
            transform.position = Vector3.Lerp(transform.position, targetCamPos, Time.deltaTime * smoothFollowSpeed);
            transform.LookAt(pivot);
        }

        public void TogglePerspective()
        {
            ApplyPerspectiveMode(!isFirstPerson, immediate: false);
        }

        public void ApplyPerspectiveMode(bool firstPerson, bool immediate)
        {
            isFirstPerson = firstPerson;

            if (playerController != null)
            {
                playerController.isFirstPersonMode = isFirstPerson;
            }

            // Ocultar o mostrar cabeza para evitar que el visor o cráneo tapen la cámara FP
            SetHeadRenderersVisible(!isFirstPerson);

            Debug.Log($"<color=cyan>[DualPerspectiveCamera] Perspectiva cambiada a: {(isFirstPerson ? "PRIMERA PERSONA" : "TERCERA PERSONA")}</color>");
        }

        private void FindHeadRenderers()
        {
            if (target == null) return;

            var rends = target.GetComponentsInChildren<Renderer>(true);
            var headList = new System.Collections.Generic.List<Renderer>();
            foreach (var r in rends)
            {
                string n = r.gameObject.name.ToLower();
                if (n.Contains("head") || n.Contains("visor") || n.Contains("neck") || r is SkinnedMeshRenderer)
                {
                    headList.Add(r);
                }
            }
            headRenderers = headList.ToArray();
        }

        private void SetHeadRenderersVisible(bool visible)
        {
            if (headRenderers == null || headRenderers.Length == 0)
            {
                FindHeadRenderers();
            }

            if (headRenderers != null)
            {
                foreach (var r in headRenderers)
                {
                    if (r != null)
                    {
                        // En primera persona se proyecta sombra pero no se renderiza para la cámara
                        r.shadowCastingMode = visible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                        r.enabled = true;
                    }
                }
            }
        }
    }
}
