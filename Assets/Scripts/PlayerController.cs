using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimuladorBloqueos
{
    /// <summary>
    /// Controlador del personaje jugador (V5.0):
    /// - Caminata predeterminada = Marcha Táctica (3.25 m/s con HumanSpWalking6).
    /// - Sistema de Barra de Vida (100%): cuando baja del 15% camina herido (1.65 m/s con HumanSpWalking)
    ///   sin poder correr ni caminar normal hasta reiniciar; al llegar a 0% cae derribado.
    /// - Acoplamiento físico suave a sillas reales (InteractiveChair) al presionar [2] o [Z].
    /// - HUD de Barra de Vida y avisos contextuales de interacción.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Velocidades")]
        public float injuredWalkSpeed = 1.65f;
        public float walkSpeed = 3.25f;
        public float tacticalWalkSpeed = 3.25f;
        public float backwardSpeed = 2.4f;
        public float runSpeed = 6.2f;
        public float rollSpeed = 5.0f;
        public float jumpVelocity = 6.2f;
        public float rotationSpeed = 11f;
        public float acceleration = 11f;
        public float gravity = -19.6f;

        [Header("Cámara")]
        public Transform cameraTransform;
        [HideInInspector]
        public bool isFirstPersonMode = false;

        private CharacterController _characterController;
        private ProceduralHumanoidAnimator _proceduralAnimator;
        private UTOPCharacterAnimationController _utopAnimator;
        private float _currentSpeed = 0f;
        private float _verticalVelocity = 0f;
        private Vector3 _lastMoveDirection = Vector3.forward;

        private static Texture2D _whiteTex;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _proceduralAnimator = GetComponent<ProceduralHumanoidAnimator>();
            _utopAnimator = GetComponent<UTOPCharacterAnimationController>();
            if (_utopAnimator == null)
                _utopAnimator = GetComponentInChildren<UTOPCharacterAnimationController>();
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            _lastMoveDirection = transform.forward;
        }

        private void Update()
        {
            ReadInput(out Vector2 moveInput, out bool isRunning, out bool jumpPressed, out bool rollPressed);

            bool isDead = (_utopAnimator != null && _utopAnimator.IsDead);
            bool isInjured = (_utopAnimator != null && _utopAnimator.IsInjured);
            bool isSeated = (_utopAnimator != null && _utopAnimator.IsSeatedOrSitting);

            if (isDead)
            {
                moveInput = Vector2.zero;
                isRunning = false;
                jumpPressed = false;
                rollPressed = false;
            }
            else if (isInjured)
            {
                // Por debajo del 15% de vida ya no puede correr ni saltar ni rodar
                isRunning = false;
                jumpPressed = false;
                rollPressed = false;
            }

            // Acoplamiento suave a la silla cuando está sentándose o sentado
            if (isSeated && _utopAnimator.currentChair != null)
            {
                InteractiveChair chair = _utopAnimator.currentChair;
                Vector3 targetPos = (_utopAnimator.currentAction == UTOPCharacterAnimationController.ActionState.PullingChairToSit)
                    ? chair.GetStandInFrontPosition()
                    : chair.GetSeatedRootPosition();
                targetPos.y = transform.position.y;

                Vector3 delta = targetPos - transform.position;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    _characterController.Move(delta * Mathf.Clamp01(Time.deltaTime * 9f));
                }
                transform.rotation = Quaternion.Slerp(transform.rotation, chair.GetSeatedRotation(), Time.deltaTime * 9f);

                if (_utopAnimator.currentAction == UTOPCharacterAnimationController.ActionState.Sitting && moveInput.sqrMagnitude > 0.2f)
                {
                    _utopAnimator.currentMoveSpeed = 1.0f;
                }
                else
                {
                    _utopAnimator.currentMoveSpeed = 0f;
                }
                return;
            }

            Vector3 camForward = transform.forward;
            Vector3 camRight = transform.right;

            if (cameraTransform != null)
            {
                camForward = cameraTransform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.001f) camForward.Normalize();

                camRight = cameraTransform.right;
                camRight.y = 0f;
                if (camRight.sqrMagnitude > 0.001f) camRight.Normalize();
            }

            Vector3 rawMoveDir = (camForward * moveInput.y + camRight * moveInput.x);
            if (rawMoveDir.sqrMagnitude > 1f) rawMoveDir.Normalize();

            bool isBackingUp = (moveInput.y < -0.15f && Mathf.Abs(moveInput.x) < 0.65f && !isRunning && !isInjured);

            // Salto con [Espacio]
            if (jumpPressed && _characterController.isGrounded && !isDead && !isInjured)
            {
                _verticalVelocity = jumpVelocity;
                if (_utopAnimator != null)
                {
                    _utopAnimator.TriggerJump();
                }
            }

            // Rodamiento táctico con [Q] o [Ctrl Izq]
            if (rollPressed && _characterController.isGrounded && _utopAnimator != null && !isDead && !isInjured)
            {
                if (rawMoveDir.sqrMagnitude > 0.01f && !isBackingUp)
                {
                    transform.rotation = Quaternion.LookRotation(rawMoveDir.normalized, Vector3.up);
                }
                _utopAnimator.TriggerRoll();
            }

            float targetSpeed = 0f;
            Vector3 moveDirection = rawMoveDir;

            if (_utopAnimator != null && _utopAnimator.IsRolling)
            {
                moveDirection = transform.forward;
                targetSpeed = rollSpeed;
            }
            else if (isDead)
            {
                targetSpeed = 0f;
            }
            else if (rawMoveDir.sqrMagnitude > 0.001f)
            {
                moveDirection = rawMoveDir.normalized;
                _lastMoveDirection = moveDirection;

                if (isInjured)
                {
                    // Menos del 15% de vida -> caminado herido lento
                    targetSpeed = injuredWalkSpeed;
                    if (!isFirstPersonMode)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    }
                }
                else if (isBackingUp)
                {
                    targetSpeed = backwardSpeed;
                    if (!isFirstPersonMode && camForward.sqrMagnitude > 0.001f)
                    {
                        Quaternion lookFwd = Quaternion.LookRotation(camForward, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, lookFwd, rotationSpeed * Time.deltaTime);
                    }
                }
                else
                {
                    // Por defecto camina con Marcha Táctica (tacticalWalkSpeed = 3.25 m/s)
                    targetSpeed = isRunning ? runSpeed : tacticalWalkSpeed;

                    if (!isFirstPersonMode)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    }
                }
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * Time.deltaTime);

            if (_characterController.isGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = -2.5f;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 activeHorizontalDir = (_currentSpeed > 0.01f && moveDirection.sqrMagnitude < 0.001f)
                ? _lastMoveDirection
                : moveDirection;

            Vector3 finalMove = (activeHorizontalDir * _currentSpeed) + (Vector3.up * _verticalVelocity);
            _characterController.Move(finalMove * Time.deltaTime);

            // Colisión radial sólida Jugador <-> NPCs (evita atravesar a cualquier efectivo, al Capitán o a los manifestantes)
            ApplyNpcCollisionSeparation();

            if (_utopAnimator != null)
            {
                _utopAnimator.currentMoveSpeed = _currentSpeed;
                _utopAnimator.isMovingBackward = isBackingUp && _currentSpeed > 0.1f;
                _utopAnimator.isSprinting = isRunning && !isBackingUp && !isInjured && _currentSpeed > 3.5f;
                _utopAnimator.isGrounded = _characterController.isGrounded;
            }

            if (_proceduralAnimator != null)
            {
                float speedRatio = 0f;
                if (_currentSpeed > 0.05f)
                {
                    if (isRunning)
                        speedRatio = Mathf.Lerp(0.5f, 1.0f, Mathf.InverseLerp(walkSpeed, runSpeed, _currentSpeed));
                    else
                        speedRatio = Mathf.Lerp(0.1f, 0.45f, _currentSpeed / walkSpeed);
                }
                _proceduralAnimator.normalizedSpeed = Mathf.Lerp(_proceduralAnimator.normalizedSpeed, speedRatio, Time.deltaTime * 10f);
                _proceduralAnimator.isGrounded = _characterController.isGrounded;
            }
        }

        private void ApplyNpcCollisionSeparation()
        {
            if (_characterController == null) return;
            Vector3 myPos = transform.position;
            const float minRadius = 0.64f;
            const float minRadiusSq = minRadius * minRadius;
            Vector3 totalPush = Vector3.zero;

            var navs = NpcNavigator.AllNavigators;
            for (int i = 0; i < navs.Count; i++)
            {
                var nav = navs[i];
                if (nav == null || !nav.isActiveAndEnabled) continue;
                Vector3 diff = myPos - nav.transform.position;
                if (Mathf.Abs(diff.y) > 1.55f) continue;
                diff.y = 0f;
                float dSq = diff.sqrMagnitude;
                if (dSq < minRadiusSq && dSq > 0.0001f)
                {
                    float d = Mathf.Sqrt(dSq);
                    totalPush += (diff / d) * (minRadius - d);
                }
            }

            if (totalPush.sqrMagnitude > 0.00001f)
            {
                _characterController.Move(totalPush);
            }
        }

        private void OnGUI()
        {
            if (_utopAnimator == null) return;

            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }

            // 1. Barra de Vida (100%) debajo de la tarjeta superior izquierda
            float panelX = 16f;
            float panelY = 92f;
            float panelW = 320f;
            float panelH = 68f;

            GUI.color = new Color(0.06f, 0.09f, 0.11f, 0.88f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), _whiteTex);

            float hpPct = Mathf.Clamp01(_utopAnimator.currentHealth / Mathf.Max(1f, _utopAnimator.maxHealth));
            Color barColor = new Color(0.22f, 0.82f, 0.38f, 1f);
            string statusText = "ESTADO: ÓPTIMO (Marcha Táctica)";

            if (_utopAnimator.IsDead)
            {
                barColor = new Color(0.75f, 0.12f, 0.12f, 1f);
                statusText = "¡CAÍDO EN SIMULACIÓN (0%)! [L/+ Reanimar]";
            }
            else if (_utopAnimator.IsInjured)
            {
                barColor = new Color(0.95f, 0.22f, 0.18f, 1f);
                statusText = "¡HERIDO (<15%)! Caminado Herido Activo";
            }
            else if (hpPct < 0.5f)
            {
                barColor = new Color(0.95f, 0.68f, 0.18f, 1f);
                statusText = "ESTADO: CON DAÑO MODERADO";
            }
            else if (_utopAnimator.currentAction == UTOPCharacterAnimationController.ActionState.MilitaryAttention)
            {
                statusText = "POSTURA: FIRMES MILITAR [F]";
            }

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.96f, 0.98f) }
            };
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.78f, 0.86f, 0.82f) }
            };

            GUI.color = Color.white;
            GUI.Label(new Rect(panelX + 10f, panelY + 6f, panelW - 20f, 18f),
                string.Format("VIDA DEL OFICIAL UTOP: {0:0}%", _utopAnimator.currentHealth), titleStyle);

            // Fondo de la barra
            GUI.color = new Color(0.18f, 0.20f, 0.22f, 1f);
            GUI.DrawTexture(new Rect(panelX + 10f, panelY + 26f, panelW - 20f, 14f), _whiteTex);

            // Relleno de la barra
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(panelX + 10f, panelY + 26f, (panelW - 20f) * hpPct, 14f), _whiteTex);

            // Marca del umbral del 15%
            float threshold15X = panelX + 10f + (panelW - 20f) * 0.15f;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(threshold15X, panelY + 24f, 2f, 18f), _whiteTex);

            GUI.color = Color.white;
            GUI.Label(new Rect(panelX + 10f, panelY + 44f, panelW - 20f, 18f),
                statusText + "  [-/K Daño | +/L Curar]", subStyle);

            // 2. Aviso Contextual de Silla Cercana (SOLO aparece cuando hay una silla real al alcance)
            if (!_utopAnimator.IsDead)
            {
                if (_utopAnimator.IsSeatedOrSitting && _utopAnimator.currentChair != null)
                {
                    DrawBottomPrompt(string.Format("SENTADO EN: {0}  —  Pulsa [2], [Z] o [WASD] para Levantarte",
                        _utopAnimator.currentChair.chairLabel.ToUpper()), new Color(0.20f, 0.72f, 0.42f, 0.92f));
                }
                else if (_utopAnimator.nearestAvailableChair != null)
                {
                    DrawBottomPrompt(string.Format("Pulsa [2] o [Z] para Sentarte en {0} (Jalar silla con la mano)",
                        _utopAnimator.nearestAvailableChair.chairLabel), new Color(0.85f, 0.65f, 0.15f, 0.92f));
                }
            }
        }

        private void DrawBottomPrompt(string message, Color accentColor)
        {
            float w = 540f;
            float h = 36f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 62f;

            GUI.color = new Color(0.05f, 0.08f, 0.10f, 0.90f);
            GUI.DrawTexture(new Rect(x, y, w, h), _whiteTex);

            GUI.color = accentColor;
            GUI.DrawTexture(new Rect(x, y, 5f, h), _whiteTex);

            GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.95f, 0.82f) }
            };

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, y, w - 16f, h), message, promptStyle);
        }

        private void ReadInput(out Vector2 moveInput, out bool isRunning, out bool jumpPressed, out bool rollPressed)
        {
            moveInput = Vector2.zero;
            isRunning = false;
            jumpPressed = false;
            rollPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                var kb = Keyboard.current;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) moveInput.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) moveInput.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveInput.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveInput.x += 1f;

                isRunning = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                jumpPressed = kb.spaceKey.wasPressedThisFrame;
                rollPressed = kb.qKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame;
            }
#else
            moveInput.x = Input.GetAxisRaw("Horizontal");
            moveInput.y = Input.GetAxisRaw("Vertical");
            isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            jumpPressed = Input.GetKeyDown(KeyCode.Space);
            rollPressed = Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftControl);
#endif
        }
    }
}
