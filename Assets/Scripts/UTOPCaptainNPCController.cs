using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimuladorBloqueos
{
    /// <summary>
    /// Controlador de animaciones e interacción del Capitán de la UTOP en su despacho:
    /// - Por defecto permanece sentado en su sillón ejecutivo (HumanSpSitting).
    /// - Si el jugador lo saluda ([1] o [G]), el Capitán se levanta suavemente moviendo su sillón,
    ///   devuelve el saludo militar (HumanSpSalute) y vuelve a tomar asiento con transiciones fluidas.
    /// - Si el jugador lo golpea con el puño derecho ([R] o Click Izq), ejecuta la caída por impacto
    ///   (HumanSpShoulderHitAndFall) seguida fluidamente de la levantada acrobática (HumanSpKipUp).
    /// - Al acercarse muestra en pantalla la tabla interactiva para probar todos sus movimientos.
    /// </summary>
    public class UTOPCaptainNPCController : MonoBehaviour
    {
        public enum CaptainState
        {
            Seated = 0,
            StandingUpToSalute,
            SalutingPlayer,
            SittingBackDown,
            StandingUpright,
            PointingOrder,
            DemoWalking,
            DemoSprinting,
            HitAndFalling,
            KippingUp,
            SpecialDance
        }

        [Header("Referencias del Capitán UTOP")]
        public Animator targetAnimator;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public Transform hipsBone;
        public InteractiveChair executiveChair;

        [Header("Clips de Animación del Capitán UTOP")]
        public AnimationClip sittingClip;          // 0: Armature|HumanSpSitting_InPlace
        public AnimationClip uprightIdleClip;      // 1: HumanSpUprightIdle_InPlace (Erguido)
        public AnimationClip saluteClip;           // 2: Armature|HumanSpSalute_InPlace
        public AnimationClip pointingClip;         // 3: Armature|HumanSpPointingForward_InPlace
        public AnimationClip walkClip;             // 4: Armature|HumanSpWalking6_InPlace
        public AnimationClip sprintClip;           // 5: Armature|HumanSpTwoCycleSprint_InPlace
        public AnimationClip hitAndFallClip;       // 6: Armature|HumanSpShoulderHitAndFall_InPlace
        public AnimationClip kipUpClip;            // 7: Armature|HumanSpKipUp_InPlace
        public AnimationClip danceClip;            // 8: Armature|HumanSpHipHopDancing2_InPlace

        [Header("Estado Actual")]
        public CaptainState currentState = CaptainState.Seated;
        public float blendSpeed = 5.5f;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable[] _clipPlayables;
        private float[] _currentWeights;
        private float[] _targetWeights;
        private float _stateTimer = 0f;
        private bool _graphInitialized = false;
        private bool _graphPaused = false;

        [SerializeField] private Vector3 _seatedRootPos;
        [SerializeField] private Quaternion _seatedRootRot = Quaternion.identity;
        [SerializeField] private Vector3 _standingClearPos;
        [SerializeField] private bool _poseConfigured = false;
        private Transform _playerTransform;
        private bool _playerNear = false;
        private NpcNavigator _nav;

        private const int SLOT_SITTING = 0;
        private const int SLOT_UPRIGHT = 1;
        private const int SLOT_SALUTE = 2;
        private const int SLOT_POINTING = 3;
        private const int SLOT_WALK = 4;
        private const int SLOT_SPRINT = 5;
        private const int SLOT_FALL = 6;
        private const int SLOT_KIPUP = 7;
        private const int SLOT_DANCE = 8;
        private const int TOTAL_SLOTS = 9;

        private static Texture2D _bgTex;

        private void Awake()
        {
            if (targetAnimator == null)
                targetAnimator = GetComponentInChildren<Animator>();
            if (skinnedMeshRenderer == null)
                skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            _nav = GetComponent<NpcNavigator>();
            if (_nav == null) _nav = gameObject.AddComponent<NpcNavigator>();

            if (hipsBone == null)
            {
                Transform[] all = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == "Hips")
                    {
                        hipsBone = all[i];
                        break;
                    }
                }
            }
        }

        private void Start()
        {
            if (!_poseConfigured)
            {
                _seatedRootPos = transform.position;
                _seatedRootRot = transform.rotation;
                _standingClearPos = _seatedRootPos + (_seatedRootRot * new Vector3(0.95f, 0f, 0.55f));
                _poseConfigured = true;
            }

            if (executiveChair != null)
            {
                executiveChair.allowPlayerSit = false;
                executiveChair.isOccupied = true;
                executiveChair.currentOccupant = transform;
            }

            InitializeGraph();
        }

        public void SetSeatedHomePose(Vector3 pos, Quaternion rot, Vector3 clearStandingPos)
        {
            _seatedRootPos = pos;
            _seatedRootRot = rot;
            _standingClearPos = clearStandingPos;
            _poseConfigured = true;
            transform.position = pos;
            transform.rotation = rot;
        }

        public void TriggerTacticalBriefingOrder()
        {
            EnterState(CaptainState.PointingOrder);
        }

        private void InitializeGraph()
        {
            if (_graphInitialized || targetAnimator == null) return;

            targetAnimator.applyRootMotion = false;
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create("UTOP_Captain_Graph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var output = AnimationPlayableOutput.Create(_graph, "CaptainOutput", targetAnimator);
            _mixer = AnimationMixerPlayable.Create(_graph, TOTAL_SLOTS);
            output.SetSourcePlayable(_mixer);

            _clipPlayables = new AnimationClipPlayable[TOTAL_SLOTS];
            _currentWeights = new float[TOTAL_SLOTS];
            _targetWeights = new float[TOTAL_SLOTS];

            AnimationClip[] clips = new AnimationClip[TOTAL_SLOTS]
            {
                sittingClip,
                uprightIdleClip,
                saluteClip,
                pointingClip,
                walkClip,
                sprintClip,
                hitAndFallClip,
                kipUpClip,
                danceClip
            };

            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                if (clips[i] != null)
                {
                    _clipPlayables[i] = AnimationClipPlayable.Create(_graph, clips[i]);
                    _graph.Connect(_clipPlayables[i], 0, _mixer, i);
                }
                float w = (i == SLOT_SITTING) ? 1f : 0f;
                _currentWeights[i] = w;
                _targetWeights[i] = w;
                _mixer.SetInputWeight(i, w);
            }

            _graph.Play();
            _graphInitialized = true;
        }

        private void Update()
        {
            if (!_graphInitialized)
            {
                InitializeGraph();
                if (!_graphInitialized) return;
            }

            if (_playerTransform == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) _playerTransform = pc.transform;
            }

            float distToPlayer = 999f;
            if (_playerTransform != null)
            {
                distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
            }

            _playerNear = (distToPlayer <= 4.8f);

            // Optimización FPS: pausar evaluación esquelética si está lejos y fuera de cámara en estado sentado
            bool shouldPause = (distToPlayer > 14f && currentState == CaptainState.Seated &&
                                (skinnedMeshRenderer == null || !skinnedMeshRenderer.isVisible));
            if (shouldPause != _graphPaused)
            {
                _graphPaused = shouldPause;
                if (_graphPaused) _graph.Stop();
                else _graph.Play();
            }
            if (_graphPaused) return;

            if (_playerNear)
            {
                ReadDemoInputs();
            }

            UpdateStateMachine(Time.deltaTime);
            ComputeTargetWeights();
            ApplySmoothWeights(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (_graphPaused || hipsBone == null) return;
            Vector3 lp = hipsBone.localPosition;
            hipsBone.localPosition = new Vector3(0f, 0f, lp.z);
        }

        private void ReadDemoInputs()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame)
            {
                if (currentState == CaptainState.Seated)
                    EnterState(CaptainState.StandingUpright);
                else
                    EnterState(CaptainState.SittingBackDown);
            }
            else if (kb.f2Key.wasPressedThisFrame)
            {
                EnterState(CaptainState.PointingOrder);
            }
            else if (kb.f3Key.wasPressedThisFrame)
            {
                EnterState(CaptainState.DemoWalking);
            }
            else if (kb.f4Key.wasPressedThisFrame)
            {
                EnterState(CaptainState.DemoSprinting);
            }
            else if (kb.f5Key.wasPressedThisFrame)
            {
                EnterState(CaptainState.HitAndFalling);
            }
            else if (kb.f6Key.wasPressedThisFrame)
            {
                EnterState(CaptainState.SpecialDance);
            }
#endif
        }

        public void OnPlayerSaluted(Transform player)
        {
            _playerTransform = player;
            if (currentState == CaptainState.HitAndFalling || currentState == CaptainState.KippingUp)
                return;

            if (currentState == CaptainState.Seated)
            {
                EnterState(CaptainState.StandingUpToSalute);
            }
            else
            {
                EnterState(CaptainState.SalutingPlayer);
            }
        }

        public void OnHitByPlayerPunch(Transform player)
        {
            _playerTransform = player;
            EnterState(CaptainState.HitAndFalling);
        }

        private void EnterState(CaptainState newState)
        {
            if (!_graphInitialized) InitializeGraph();

            // Si sale de estar sentado, animar la silla ejecutiva deslizándose hacia atrás
            if (currentState == CaptainState.Seated && newState != CaptainState.Seated && executiveChair != null)
            {
                executiveChair.BeginStandSequence();
            }

            currentState = newState;
            _stateTimer = 0f;

            switch (newState)
            {
                case CaptainState.Seated:
                    if (executiveChair != null)
                    {
                        executiveChair.isOccupied = true;
                        executiveChair.currentOccupant = transform;
                    }
                    ResetClip(SLOT_SITTING);
                    break;

                case CaptainState.StandingUpToSalute:
                    ResetClip(SLOT_UPRIGHT);
                    break;

                case CaptainState.SalutingPlayer:
                    ResetClip(SLOT_SALUTE);
                    break;

                case CaptainState.SittingBackDown:
                    if (executiveChair != null)
                    {
                        executiveChair.BeginSitSequence(transform);
                    }
                    ResetClip(SLOT_UPRIGHT);
                    break;

                case CaptainState.StandingUpright:
                    ResetClip(SLOT_UPRIGHT);
                    break;

                case CaptainState.PointingOrder:
                    ResetClip(SLOT_POINTING);
                    break;

                case CaptainState.DemoWalking:
                    ResetClip(SLOT_WALK);
                    break;

                case CaptainState.DemoSprinting:
                    ResetClip(SLOT_SPRINT);
                    break;

                case CaptainState.HitAndFalling:
                    ResetClip(SLOT_FALL);
                    break;

                case CaptainState.KippingUp:
                    ResetClip(SLOT_KIPUP);
                    break;

                case CaptainState.SpecialDance:
                    ResetClip(SLOT_DANCE);
                    break;
            }
        }

        private void ResetClip(int slot)
        {
            if (_clipPlayables != null && slot >= 0 && slot < _clipPlayables.Length && _clipPlayables[slot].IsValid())
            {
                _clipPlayables[slot].SetTime(0.0);
                _clipPlayables[slot].SetSpeed(1.0);
            }
        }

        private void UpdateStateMachine(float dt)
        {
            _stateTimer += dt;

            Quaternion lookAtPlayerRot = _seatedRootRot;
            if (_playerTransform != null)
            {
                Vector3 dir = _playerTransform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    lookAtPlayerRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            switch (currentState)
            {
                case CaptainState.Seated:
                    transform.position = Vector3.Lerp(transform.position, _seatedRootPos, dt * 6f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, _seatedRootRot, dt * 6f);
                    break;

                case CaptainState.StandingUpToSalute:
                    // Se levanta suavemente detrás de su escritorio orientándose hacia el jugador
                    Vector3 standSalutePos = _seatedRootPos + (_seatedRootRot * new Vector3(0f, 0f, 0.14f));
                    transform.position = Vector3.Lerp(transform.position, standSalutePos, dt * 6f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookAtPlayerRot, dt * 6f);
                    if (_stateTimer >= 0.48f)
                    {
                        EnterState(CaptainState.SalutingPlayer);
                    }
                    break;

                case CaptainState.SalutingPlayer:
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookAtPlayerRot, dt * 6f);
                    float saluteDur = saluteClip != null ? saluteClip.length : 2.83f;
                    if (_stateTimer >= saluteDur - 0.18f)
                    {
                        EnterState(CaptainState.SittingBackDown);
                    }
                    break;

                case CaptainState.SittingBackDown:
                    transform.position = Vector3.Lerp(transform.position, _seatedRootPos, dt * 6.5f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, _seatedRootRot, dt * 6.5f);
                    if (_stateTimer >= 0.45f)
                    {
                        EnterState(CaptainState.Seated);
                    }
                    break;

                case CaptainState.StandingUpright:
                    transform.position = Vector3.Lerp(transform.position, _standingClearPos, dt * 5f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookAtPlayerRot, dt * 5f);
                    break;

                case CaptainState.PointingOrder:
                    transform.position = Vector3.Lerp(transform.position, _standingClearPos, dt * 5f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookAtPlayerRot, dt * 6f);
                    float pointDur = pointingClip != null ? pointingClip.length : 4.70f;
                    if (_stateTimer >= pointDur - 0.2f)
                    {
                        EnterState(CaptainState.SittingBackDown);
                    }
                    break;

                case CaptainState.DemoWalking:
                case CaptainState.DemoSprinting:
                    // Camina o corre en un pequeño arco dentro del despacho y luego regresa a su sillón
                    float walkOffset = Mathf.Sin(_stateTimer * 2.0f) * 0.65f;
                    Vector3 demoPos = _standingClearPos + (_seatedRootRot * new Vector3(0f, 0f, walkOffset));
                    transform.position = Vector3.Lerp(transform.position, demoPos, dt * 5f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookAtPlayerRot, dt * 6f);
                    if (_stateTimer >= 3.2f)
                    {
                        EnterState(CaptainState.SittingBackDown);
                    }
                    break;

                case CaptainState.HitAndFalling:
                    transform.position = Vector3.Lerp(transform.position, _standingClearPos, dt * 7f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookAtPlayerRot, dt * 7f);
                    float fallDur = hitAndFallClip != null ? hitAndFallClip.length : 3.10f;
                    // Iniciar la transición suave hacia KipUp justo cuando termina de caer al suelo
                    if (_stateTimer >= fallDur - 0.22f)
                    {
                        EnterState(CaptainState.KippingUp);
                    }
                    break;

                case CaptainState.KippingUp:
                    transform.position = Vector3.Lerp(transform.position, _standingClearPos, dt * 5f);
                    float kipDur = kipUpClip != null ? kipUpClip.length : 2.03f;
                    if (_stateTimer >= kipDur - 0.15f)
                    {
                        EnterState(CaptainState.StandingUpright);
                    }
                    break;

                case CaptainState.SpecialDance:
                    transform.position = Vector3.Lerp(transform.position, _standingClearPos, dt * 5f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookAtPlayerRot, dt * 5f);
                    if (_stateTimer >= 6.5f)
                    {
                        EnterState(CaptainState.SittingBackDown);
                    }
                    break;
            }
        }

        private void ComputeTargetWeights()
        {
            for (int i = 0; i < TOTAL_SLOTS; i++)
                _targetWeights[i] = 0f;

            switch (currentState)
            {
                case CaptainState.Seated:
                    _targetWeights[SLOT_SITTING] = 1f;
                    break;
                case CaptainState.StandingUpToSalute:
                case CaptainState.StandingUpright:
                    _targetWeights[SLOT_UPRIGHT] = 1f;
                    break;
                case CaptainState.SittingBackDown:
                    _targetWeights[SLOT_SITTING] = 0.65f;
                    _targetWeights[SLOT_UPRIGHT] = 0.35f;
                    break;
                case CaptainState.SalutingPlayer:
                    _targetWeights[SLOT_SALUTE] = 1f;
                    break;
                case CaptainState.PointingOrder:
                    _targetWeights[SLOT_POINTING] = 1f;
                    break;
                case CaptainState.DemoWalking:
                    _targetWeights[SLOT_WALK] = 1f;
                    break;
                case CaptainState.DemoSprinting:
                    _targetWeights[SLOT_SPRINT] = 1f;
                    break;
                case CaptainState.HitAndFalling:
                    _targetWeights[SLOT_FALL] = 1f;
                    break;
                case CaptainState.KippingUp:
                    _targetWeights[SLOT_KIPUP] = 1f;
                    break;
                case CaptainState.SpecialDance:
                    _targetWeights[SLOT_DANCE] = 1f;
                    break;
            }
        }

        private void ApplySmoothWeights(float dt)
        {
            float sum = 0f;
            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                _currentWeights[i] = Mathf.MoveTowards(_currentWeights[i], _targetWeights[i], blendSpeed * dt);
                sum += _currentWeights[i];
            }

            if (sum > 0.0001f)
            {
                for (int i = 0; i < TOTAL_SLOTS; i++)
                {
                    float norm = _currentWeights[i] / sum;
                    _currentWeights[i] = norm;
                    _mixer.SetInputWeight(i, norm);
                }
            }
        }

        private void OnGUI()
        {
            if (!_playerNear) return;

            if (_bgTex == null)
            {
                _bgTex = new Texture2D(1, 1);
                _bgTex.SetPixel(0, 0, Color.white);
                _bgTex.Apply();
            }

            float w = 395f;
            float h = 216f;
            float x = Screen.width - w - 18f;
            float y = 78f;

            GUI.color = new Color(0.05f, 0.09f, 0.08f, 0.90f);
            GUI.DrawTexture(new Rect(x, y, w, h), _bgTex);

            GUI.color = new Color(0.25f, 0.78f, 0.45f, 1f);
            GUI.DrawTexture(new Rect(x, y, 4f, h), _bgTex);

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.45f, 0.95f, 0.62f) }
            };
            GUIStyle rowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.90f, 0.94f, 0.92f) }
            };

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 8f, w - 20f, 20f),
                "CAPITÁN COMANDANTE UTOP — TABLA DE INTERACCIÓN", headerStyle);

            string[] lines = new string[]
            {
                "• [1] o [G]: Saludar -> Se levanta del sillón, saluda y se sienta",
                "• [R] o [Click Izq]: Golpear con Puño -> Caída + Kip-Up Acrobático",
                "• [F1]: Alternar Sentado (Sitting) / De Pie Erguido",
                "• [F2]: Impartir Orden Táctica (PointingForward)",
                "• [F3]: Demostrar Marcha Táctica (Walking6)",
                "• [F4]: Demostrar Carrera Rápida (TwoCycleSprint)",
                "• [F5]: Demostrar Caída por Impacto + Levantada Kip-Up",
                "• [F6]: Movimiento Especial (HipHopDancing2)"
            };

            for (int i = 0; i < lines.Length; i++)
            {
                GUI.Label(new Rect(x + 12f, y + 32f + i * 21f, w - 20f, 20f), lines[i], rowStyle);
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
