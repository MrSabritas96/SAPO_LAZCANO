using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimuladorBloqueos
{
    /// <summary>
    /// Controlador de animaciones esqueléticas y estado vital del Oficial de la UTOP (V5.0):
    /// - Caminata predeterminada = Marcha Táctica (HumanSpWalking6).
    /// - Barra de Vida al 100%: Caminado Herido (HumanSpWalking) EXCLUSIVAMENTE cuando la vida < 15%,
    ///   y Caída/Muerte (HumanSpTwoHandedSwordDeath) EXCLUSIVAMENTE cuando la vida llega a 0%.
    /// - Postura Erguida Natural al detenerse (UprightIdle) + Posición de Firmes Militar con tecla [F]
    ///   (pies juntos y manos pegadas a los muslos).
    /// - Sentarse con [2] o [Z] EXCLUSIVAMENTE frente a una silla real (InteractiveChair), con gesto
    ///   de mover la silla con la mano antes de tomar asiento.
    /// - Golpe con el Puño Derecho ([R] o Click Izquierdo) con impacto real sobre Mineros y Capitán.
    /// </summary>
    public class UTOPCharacterAnimationController : MonoBehaviour
    {
        public enum ActionState
        {
            None = 0,
            MilitaryAttention,   // Posición de Firmes Militar [F] (pies juntos, manos en los muslos)
            Jumping,
            Rolling,
            Saluting,            // Saludo Policial [1 / G]
            PullingChairToSit,   // Gesto con la mano jalando la silla antes de sentarse
            Sitting,             // Sentado en una silla real [2 / Z]
            RightPunch,          // Golpe con el puño derecho [R / Click Izq]
            KnockedDown          // Caído únicamente cuando currentHealth <= 0%
        }

        [Header("Referencias del Esqueleto UTOP")]
        public Animator targetAnimator;
        public Transform hipsBone;
        public Transform spineBone;

        [Header("Clips de Movimiento y Postura UTOP")]
        public AnimationClip idleClip;               // 0: Postura Erguida Natural (HumanSpUprightIdle_InPlace)
        public AnimationClip walkClip;               // 1: Caminado Herido (<15% Vida: HumanSpWalking_InPlace)
        public AnimationClip tacticalWalkClip;       // 2: Marcha Táctica Predeterminada (HumanSpWalking6_InPlace)
        public AnimationClip sprintClip;             // 3: Carrera Sprint (HumanSpTwoCycleSprint_InPlace)
        public AnimationClip runBackwardClip;        // 4: Retroceso Táctico (HumanSpRunBackward_InPlace)
        public AnimationClip jumpClip;               // 5: Salto en Carrera (HumanSpRunningJump_InPlace)
        public AnimationClip rollClip;               // 6: Rodamiento Táctico (HumanSpQuickRollToRun_InPlace)
        public AnimationClip saluteClip;             // 7: Saludo Policial (HumanSpSalute_InPlace)
        public AnimationClip sitClip;                // 8: Sentado en Silla (HumanSpSitting_InPlace)
        public AnimationClip knockdownClip;          // 9: Caída por Vida 0% (HumanSpTwoHandedSwordDeath_InPlace)
        public AnimationClip militaryAttentionClip;  // 10: Posición de Firmes Militar (HumanSpMilitaryAttention_InPlace)
        public AnimationClip rightPunchClip;         // 11: Golpe con el Puño Derecho (HumanSpRightPunch_InPlace)
        public AnimationClip pullChairClip;          // 12: Jalar Silla con la Mano (HumanSpPullChair_InPlace)

        [Header("Sistema de Salud (Barra de Vida 100%)")]
        [Range(0f, 100f)]
        public float currentHealth = 100f;
        public float maxHealth = 100f;
        [Tooltip("Una vez que la vida baja del 15%, el personaje queda herido hasta finalizar o reiniciar.")]
        public bool permanentlyInjuredUntilReset = false;

        [Header("Parámetros de Suavidad y Correlación")]
        public float locomotionBlendSpeed = 7.5f;
        public float actionBlendSpeed = 6.5f;
        public bool tacticalMarchMode = true;

        [Header("Estado en Tiempo Real")]
        public ActionState currentAction = ActionState.None;
        public float currentMoveSpeed = 0f;
        public bool isMovingBackward = false;
        public bool isSprinting = false;
        public bool isGrounded = true;
        public InteractiveChair currentChair;
        public InteractiveChair nearestAvailableChair;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable[] _clipPlayables;
        private float[] _currentWeights;
        private float[] _targetWeights;
        private float _actionTimer = 0f;
        private float _actionDuration = 0f;
        private bool _punchHitChecked = false;
        private float _previousYaw = 0f;
        private float _smoothedTurnLean = 0f;
        private bool _graphInitialized = false;

        private const int SLOT_IDLE = 0;
        private const int SLOT_INJURED_WALK = 1;
        private const int SLOT_TACTICAL_WALK = 2;
        private const int SLOT_SPRINT = 3;
        private const int SLOT_RUN_BACKWARD = 4;
        private const int SLOT_JUMP = 5;
        private const int SLOT_ROLL = 6;
        private const int SLOT_SALUTE = 7;
        private const int SLOT_SIT = 8;
        private const int SLOT_KNOCKDOWN = 9;
        private const int SLOT_MILITARY_ATTENTION = 10;
        private const int SLOT_RIGHT_PUNCH = 11;
        private const int SLOT_PULL_CHAIR = 12;
        private const int TOTAL_SLOTS = 13;

        public bool IsInjured => (currentHealth > 0f && currentHealth < 15f) || (permanentlyInjuredUntilReset && currentHealth > 0f);
        public bool IsDead => currentHealth <= 0f;

        public bool IsPerformingLockedAction =>
            currentAction == ActionState.Rolling ||
            currentAction == ActionState.PullingChairToSit ||
            currentAction == ActionState.Sitting ||
            currentAction == ActionState.KnockedDown;

        public bool IsRolling => currentAction == ActionState.Rolling;
        public bool IsSeatedOrSitting => currentAction == ActionState.PullingChairToSit || currentAction == ActionState.Sitting;

        private void Awake()
        {
            if (targetAnimator == null)
                targetAnimator = GetComponentInChildren<Animator>();

            AutoFindBones();
        }

        private void Start()
        {
            InitializePlayableGraph();
            _previousYaw = transform.eulerAngles.y;
        }

        public void AutoFindBones()
        {
            if (hipsBone != null && spineBone != null) return;

            Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (hipsBone == null && t.name == "Hips")
                    hipsBone = t;
                else if (spineBone == null && (t.name == "Spine1" || t.name == "Spine"))
                    spineBone = t;
            }
        }

        private void InitializePlayableGraph()
        {
            if (_graphInitialized) return;
            if (targetAnimator == null)
            {
                targetAnimator = GetComponentInChildren<Animator>();
                if (targetAnimator == null) return;
            }

            targetAnimator.applyRootMotion = false;
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create("UTOP_Officer_AnimationGraph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var output = AnimationPlayableOutput.Create(_graph, "AnimationOutput", targetAnimator);
            _mixer = AnimationMixerPlayable.Create(_graph, TOTAL_SLOTS);
            output.SetSourcePlayable(_mixer);

            _clipPlayables = new AnimationClipPlayable[TOTAL_SLOTS];
            _currentWeights = new float[TOTAL_SLOTS];
            _targetWeights = new float[TOTAL_SLOTS];

            AnimationClip[] clips = new AnimationClip[TOTAL_SLOTS]
            {
                idleClip,
                walkClip,
                tacticalWalkClip,
                sprintClip,
                runBackwardClip,
                jumpClip,
                rollClip,
                saluteClip,
                sitClip,
                knockdownClip,
                militaryAttentionClip != null ? militaryAttentionClip : idleClip,
                rightPunchClip != null ? rightPunchClip : saluteClip,
                pullChairClip != null ? pullChairClip : sitClip
            };

            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                if (clips[i] != null)
                {
                    _clipPlayables[i] = AnimationClipPlayable.Create(_graph, clips[i]);
                    _graph.Connect(_clipPlayables[i], 0, _mixer, i);
                }
                _mixer.SetInputWeight(i, i == SLOT_IDLE ? 1f : 0f);
                _currentWeights[i] = (i == SLOT_IDLE) ? 1f : 0f;
                _targetWeights[i] = (i == SLOT_IDLE) ? 1f : 0f;
            }

            _graph.Play();
            _graphInitialized = true;
        }

        private void Update()
        {
            if (!_graphInitialized)
            {
                InitializePlayableGraph();
                if (!_graphInitialized) return;
            }

            // Buscar silla disponible cercana si no está sentado
            if (!IsSeatedOrSitting)
            {
                nearestAvailableChair = InteractiveChair.FindNearestAvailableForPlayer(transform.position, 1.65f);
            }
            else
            {
                nearestAvailableChair = null;
            }

            // Verificar estado de salud (<15% herido permanente hasta reinicio, <=0% caída)
            if (currentHealth < 15f && currentHealth > 0f)
            {
                permanentlyInjuredUntilReset = true;
            }
            if (currentHealth <= 0f && currentAction != ActionState.KnockedDown)
            {
                if (currentChair != null)
                {
                    currentChair.ForceRelease();
                    currentChair = null;
                }
                TriggerAction(ActionState.KnockedDown);
            }

            ReadActionInputs();
            UpdateActionStateMachine(Time.deltaTime);
            ComputeTargetWeights();
            BlendAndApplyWeights(Time.deltaTime);
            UpdatePlaybackSpeeds();
        }

        private void LateUpdate()
        {
            if (hipsBone != null)
            {
                Vector3 localPos = hipsBone.localPosition;
                hipsBone.localPosition = new Vector3(0f, 0f, localPos.z);
            }

            float currentYaw = transform.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(_previousYaw, currentYaw);
            _previousYaw = currentYaw;

            float yawSpeed = deltaYaw / Mathf.Max(0.001f, Time.deltaTime);
            float targetLean = 0f;
            if (currentMoveSpeed > 0.5f && currentAction == ActionState.None)
            {
                float speedFactor = Mathf.Clamp01(currentMoveSpeed / 5.5f);
                targetLean = Mathf.Clamp(-yawSpeed * 0.035f * speedFactor, -7.5f, 7.5f);
            }

            _smoothedTurnLean = Mathf.Lerp(_smoothedTurnLean, targetLean, Time.deltaTime * 8f);
            if (spineBone != null && Mathf.Abs(_smoothedTurnLean) > 0.05f)
            {
                spineBone.rotation = Quaternion.AngleAxis(_smoothedTurnLean, transform.forward) * spineBone.rotation;
            }
        }

        public void ApplyDamage(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
            if (currentHealth < 15f && currentHealth > 0f)
            {
                permanentlyInjuredUntilReset = true;
            }
            if (currentHealth <= 0f)
            {
                if (currentChair != null)
                {
                    currentChair.ForceRelease();
                    currentChair = null;
                }
                TriggerAction(ActionState.KnockedDown);
            }
        }

        public void RestoreFullHealth()
        {
            currentHealth = maxHealth;
            permanentlyInjuredUntilReset = false;
            if (currentAction == ActionState.KnockedDown)
            {
                currentAction = ActionState.None;
            }
        }

        private void ReadActionInputs()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            // Teclas de prueba de Salud: [K] o [-] reduce vida (100 -> 55 -> 12 [<15% Herido] -> 0 [Caído]); [L] o [+] restaura al 100%
            if (kb.kKey.wasPressedThisFrame || kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)
            {
                if (currentHealth > 55f) ApplyDamage(45f);
                else if (currentHealth >= 15f) ApplyDamage(currentHealth - 12f); // Queda en 12% (<15% caminado herido)
                else ApplyDamage(20f); // Llega a 0% (muerte en simulación)
            }
            if (kb.lKey.wasPressedThisFrame || kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)
            {
                RestoreFullHealth();
                return;
            }

            if (IsDead) return;

            // [F]: Posición de Firmes Militar (pies juntos y manos pegadas a los muslos)
            if (kb.fKey.wasPressedThisFrame && !IsSeatedOrSitting)
            {
                if (currentAction == ActionState.MilitaryAttention)
                    currentAction = ActionState.None;
                else
                    TriggerAction(ActionState.MilitaryAttention);
            }

            // [1] o [G]: Saludo Policial / Militar UTOP
            if ((kb.digit1Key.wasPressedThisFrame || kb.gKey.wasPressedThisFrame) && !IsSeatedOrSitting)
            {
                TriggerAction(ActionState.Saluting);
                NotifySaluteToCaptainAndTroops();
            }

            // [2] o [Z]: Sentarse EXCLUSIVAMENTE si hay una silla disponible cerca (o levantarse si ya está sentado)
            if (kb.digit2Key.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)
            {
                if (IsSeatedOrSitting)
                {
                    StandUpFromChair();
                }
                else if (nearestAvailableChair != null)
                {
                    SitOnChair(nearestAvailableChair);
                }
            }

            // [R] o Click Izquierdo (cuando el cursor está bloqueado y no hay menús abiertos): Golpe con el Puño Derecho
            bool punchClicked = false;
            bool uiOpen = SimuladorBloqueos.Decisions.DecisionManager.IsAnyDecisionUIOpen;
            if (!uiOpen && Mouse.current != null && Cursor.lockState == CursorLockMode.Locked && Mouse.current.leftButton.wasPressedThisFrame)
            {
                punchClicked = true;
            }
            if (!uiOpen && (kb.rKey.wasPressedThisFrame || punchClicked) && !IsSeatedOrSitting)
            {
                TriggerFastPunchOrBatonStrike();
            }
#endif
        }

        public void TriggerFastPunchOrBatonStrike()
        {
            if (IsDead || IsSeatedOrSitting) return;
            // Permitir golpes y ataques seguidos sin trabas ni esperas largas
            currentAction = ActionState.RightPunch;
            _actionTimer = 0f;
            _punchHitChecked = false;
            _actionDuration = 0.38f; // Animación de golpe rápida y fluida
            ResetClipPlayable(SLOT_RIGHT_PUNCH);
        }

        private void SitOnChair(InteractiveChair chair)
        {
            if (chair == null) return;
            currentChair = chair;
            currentChair.BeginSitSequence(transform);
            TriggerAction(ActionState.PullingChairToSit);
        }

        public void StandUpFromChair()
        {
            if (currentChair != null)
            {
                currentChair.BeginStandSequence();
                currentChair = null;
            }
            currentAction = ActionState.None;
        }

        private void NotifySaluteToCaptainAndTroops()
        {
            // Notificar al Capitán UTOP si está cerca para que se levante suavemente, devuelva el saludo y se vuelva a sentar
            var captains = FindObjectsByType<UTOPCaptainNPCController>();
            for (int i = 0; i < captains.Length; i++)
            {
                if (captains[i] != null && Vector3.Distance(transform.position, captains[i].transform.position) <= 8.5f)
                {
                    captains[i].OnPlayerSaluted(transform);
                }
            }

            // Notificar al escuadrón de 30 policías UTOP si están formados en el Patio de Honor
            if (UTOPTroopFormationManager.Instance != null)
            {
                UTOPTroopFormationManager.Instance.OnPlayerSaluted();
            }
        }

        private void CheckPunchImpact()
        {
            Vector3 punchOrigin = transform.position + Vector3.up * 1.1f;
            Vector3 fwd = transform.forward;

            // Verificar impacto sobre Mineros Bloqueadores
            var miners = FindObjectsByType<MinerBlockerNPCController>();
            for (int i = 0; i < miners.Length; i++)
            {
                if (miners[i] == null) continue;
                Vector3 toTarget = miners[i].transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude <= 2.35f && Vector3.Dot(fwd, toTarget.normalized) > 0.25f)
                {
                    miners[i].OnHitByPlayerPunch(transform);
                }
            }

            // Verificar impacto sobre Capitán UTOP (reacción de caída ShoulderHitAndFall + KipUp)
            var captains = FindObjectsByType<UTOPCaptainNPCController>();
            for (int i = 0; i < captains.Length; i++)
            {
                if (captains[i] == null) continue;
                Vector3 toTarget = captains[i].transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude <= 2.45f && Vector3.Dot(fwd, toTarget.normalized) > 0.20f)
                {
                    captains[i].OnHitByPlayerPunch(transform);
                }
            }
        }

        public void TriggerJump()
        {
            if (IsDead || IsInjured || IsSeatedOrSitting) return;
            TriggerAction(ActionState.Jumping);
        }

        public void TriggerRoll()
        {
            if (IsDead || IsInjured || IsSeatedOrSitting || currentAction == ActionState.Rolling)
                return;
            TriggerAction(ActionState.Rolling);
        }

        public void TriggerAction(ActionState newAction)
        {
            if (!_graphInitialized) return;

            currentAction = newAction;
            _actionTimer = 0f;
            _punchHitChecked = false;

            switch (newAction)
            {
                case ActionState.MilitaryAttention:
                    _actionDuration = 9999f;
                    ResetClipPlayable(SLOT_MILITARY_ATTENTION);
                    break;

                case ActionState.Jumping:
                    _actionDuration = jumpClip != null ? jumpClip.length : 0.90f;
                    ResetClipPlayable(SLOT_JUMP);
                    break;

                case ActionState.Rolling:
                    _actionDuration = rollClip != null ? rollClip.length : 1.40f;
                    ResetClipPlayable(SLOT_ROLL);
                    break;

                case ActionState.Saluting:
                    _actionDuration = saluteClip != null ? saluteClip.length : 2.83f;
                    ResetClipPlayable(SLOT_SALUTE);
                    break;

                case ActionState.PullingChairToSit:
                    _actionDuration = 0.36f;
                    ResetClipPlayable(SLOT_PULL_CHAIR);
                    break;

                case ActionState.Sitting:
                    _actionDuration = sitClip != null ? sitClip.length : 1.17f;
                    ResetClipPlayable(SLOT_SIT);
                    break;

                case ActionState.RightPunch:
                    _actionDuration = rightPunchClip != null ? rightPunchClip.length : 0.68f;
                    ResetClipPlayable(SLOT_RIGHT_PUNCH);
                    break;

                case ActionState.KnockedDown:
                    _actionDuration = knockdownClip != null ? knockdownClip.length : 2.40f;
                    ResetClipPlayable(SLOT_KNOCKDOWN);
                    break;
            }
        }

        private void ResetClipPlayable(int slot)
        {
            if (_clipPlayables != null && slot >= 0 && slot < _clipPlayables.Length && _clipPlayables[slot].IsValid())
            {
                _clipPlayables[slot].SetTime(0.0);
                _clipPlayables[slot].SetSpeed(1.0);
            }
        }

        private void UpdateActionStateMachine(float dt)
        {
            if (currentAction == ActionState.None) return;

            _actionTimer += dt;

            switch (currentAction)
            {
                case ActionState.MilitaryAttention:
                    // Si el jugador empieza a caminar, sale suavemente de la posición de Firmes
                    if (currentMoveSpeed > 0.25f)
                        currentAction = ActionState.None;
                    break;

                case ActionState.Jumping:
                    if (_actionTimer >= _actionDuration - 0.16f)
                        currentAction = ActionState.None;
                    break;

                case ActionState.Rolling:
                    if (_actionTimer >= _actionDuration - 0.22f)
                        currentAction = ActionState.None;
                    break;

                case ActionState.Saluting:
                    if (_actionTimer >= _actionDuration - 0.25f || (currentMoveSpeed > 1.8f && _actionTimer > 0.35f))
                        currentAction = ActionState.None;
                    break;

                case ActionState.PullingChairToSit:
                    if (_actionTimer >= _actionDuration)
                    {
                        TriggerAction(ActionState.Sitting);
                    }
                    break;

                case ActionState.Sitting:
                    // Si presiona WASD después de haberse sentado, se levanta empujando ligeramente la silla
                    if (currentMoveSpeed > 0.4f && _actionTimer > 0.55f)
                    {
                        StandUpFromChair();
                    }
                    break;

                case ActionState.RightPunch:
                    if (!_punchHitChecked && _actionTimer >= 0.16f)
                    {
                        _punchHitChecked = true;
                        CheckPunchImpact();
                    }
                    // Si el jugador se mueve mientras o después de golpear, cancela el golpe de inmediato y retoma la marcha fluida
                    if (currentMoveSpeed > 0.25f || _actionTimer >= _actionDuration)
                    {
                        currentAction = ActionState.None;
                    }
                    break;

                case ActionState.KnockedDown:
                    // Congelar en el suelo mientras la vida sea 0%
                    if (_clipPlayables[SLOT_KNOCKDOWN].IsValid() && _actionTimer >= _actionDuration - 0.05f)
                    {
                        _clipPlayables[SLOT_KNOCKDOWN].SetTime(_actionDuration - 0.05f);
                        _clipPlayables[SLOT_KNOCKDOWN].SetSpeed(0.0);
                    }
                    break;
            }
        }

        private void ComputeTargetWeights()
        {
            for (int i = 0; i < TOTAL_SLOTS; i++)
                _targetWeights[i] = 0f;

            switch (currentAction)
            {
                case ActionState.MilitaryAttention:
                    _targetWeights[SLOT_MILITARY_ATTENTION] = 1f;
                    return;
                case ActionState.Jumping:
                    _targetWeights[SLOT_JUMP] = 1f;
                    return;
                case ActionState.Rolling:
                    _targetWeights[SLOT_ROLL] = 1f;
                    return;
                case ActionState.Saluting:
                    _targetWeights[SLOT_SALUTE] = 1f;
                    return;
                case ActionState.PullingChairToSit:
                    _targetWeights[SLOT_PULL_CHAIR] = 1f;
                    return;
                case ActionState.Sitting:
                    _targetWeights[SLOT_SIT] = 1f;
                    return;
                case ActionState.RightPunch:
                    _targetWeights[SLOT_RIGHT_PUNCH] = 1f;
                    return;
                case ActionState.KnockedDown:
                    _targetWeights[SLOT_KNOCKDOWN] = 1f;
                    return;
            }

            // Detenido -> Postura Erguida Natural (SLOT_IDLE = HumanSpUprightIdle_InPlace)
            if (currentMoveSpeed < 0.08f)
            {
                _targetWeights[SLOT_IDLE] = 1f;
                return;
            }

            // Si la vida está por debajo del 15% (IsInjured), camina EXCLUSIVAMENTE con el movimiento de herido (SLOT_INJURED_WALK)
            if (IsInjured)
            {
                float tInj = Mathf.Clamp01(currentMoveSpeed / 1.4f);
                _targetWeights[SLOT_IDLE] = 1f - tInj;
                _targetWeights[SLOT_INJURED_WALK] = tInj;
                return;
            }

            if (isMovingBackward)
            {
                float backT = Mathf.Clamp01(currentMoveSpeed / 2.2f);
                _targetWeights[SLOT_IDLE] = 1f - backT;
                _targetWeights[SLOT_RUN_BACKWARD] = backT;
                return;
            }

            // Caminata Predeterminada = Marcha Táctica (SLOT_TACTICAL_WALK = HumanSpWalking6_InPlace)
            if (currentMoveSpeed <= 3.6f)
            {
                float t = Mathf.Clamp01(currentMoveSpeed / 2.8f);
                _targetWeights[SLOT_IDLE] = 1f - t;
                _targetWeights[SLOT_TACTICAL_WALK] = t;
            }
            else
            {
                // Transición suave de Marcha Táctica (Walking6) a Carrera Sprint (TwoCycleSprint)
                float t = Mathf.InverseLerp(3.6f, 5.8f, currentMoveSpeed);
                _targetWeights[SLOT_TACTICAL_WALK] = 1f - t;
                _targetWeights[SLOT_SPRINT] = t;
            }
        }

        private void BlendAndApplyWeights(float dt)
        {
            float speed = (currentAction != ActionState.None) ? actionBlendSpeed : locomotionBlendSpeed;
            float sum = 0f;

            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                _currentWeights[i] = Mathf.MoveTowards(_currentWeights[i], _targetWeights[i], speed * dt);
                sum += _currentWeights[i];
            }

            if (sum > 0.0001f)
            {
                for (int i = 0; i < TOTAL_SLOTS; i++)
                {
                    float normalized = _currentWeights[i] / sum;
                    _currentWeights[i] = normalized;
                    _mixer.SetInputWeight(i, normalized);
                }
            }
            else
            {
                _mixer.SetInputWeight(SLOT_IDLE, 1f);
            }
        }

        private void UpdatePlaybackSpeeds()
        {
            if (_clipPlayables[SLOT_INJURED_WALK].IsValid())
            {
                float injRate = Mathf.Clamp(currentMoveSpeed / 1.55f, 0.75f, 1.15f);
                _clipPlayables[SLOT_INJURED_WALK].SetSpeed(injRate);
            }

            if (_clipPlayables[SLOT_TACTICAL_WALK].IsValid())
            {
                float tacRate = Mathf.Clamp(currentMoveSpeed / 3.1f, 0.82f, 1.25f);
                _clipPlayables[SLOT_TACTICAL_WALK].SetSpeed(tacRate);
            }

            if (_clipPlayables[SLOT_SPRINT].IsValid())
            {
                float sprintRate = Mathf.Clamp(currentMoveSpeed / 5.8f, 0.82f, 1.22f);
                _clipPlayables[SLOT_SPRINT].SetSpeed(sprintRate);
            }

            if (_clipPlayables[SLOT_RUN_BACKWARD].IsValid())
            {
                float backRate = Mathf.Clamp(currentMoveSpeed / 2.3f, 0.80f, 1.25f);
                _clipPlayables[SLOT_RUN_BACKWARD].SetSpeed(backRate);
            }
        }

        private void OnDestroy()
        {
            if (_graphInitialized && _graph.IsValid())
            {
                _graph.Destroy();
            }
        }
    }
}
