using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Policía UTOP de la tropa. Vida casual en la Cuadra de Tropa 1, formación en el Patio de Honor,
    /// puestos asignados por las decisiones (distribución, equipamiento, verificación) y cinemática de armería.
    /// Locomoción mediante NavMeshAgent (no atraviesa muros, mesas ni otros NPC).
    /// </summary>
    public class UTOPTroopNPCController : MonoBehaviour
    {
        public enum TroopState
        {
            CasualStandingUpright = 0, CasualWalking, CasualSeated, CasualSalutingOnce,
            MovingToFormation, FormedInAttention, FormedSalutingUnison, ReturningToBarracks,
            MovingToStation, AtStation
        }

        [Header("Identificación del Efectivo UTOP")]
        public int troopIndex = 0;
        public int formationRow = 0;
        public int formationCol = 0;
        public string assignedRole = "Sin asignar";

        [Header("Referencias")]
        public Animator targetAnimator;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public Transform hipsBone;
        public InteractiveChair assignedChair;
        public NpcNavigator nav;

        [Header("Clips")]
        public AnimationClip uprightIdleClip;
        public AnimationClip militaryAttentionClip;
        public AnimationClip walkClip;
        public AnimationClip sprintClip;
        public AnimationClip saluteClip;
        public AnimationClip sitClip;

        [Header("Estado Actual")]
        public TroopState currentState = TroopState.CasualStandingUpright;
        public bool inContingent = true;

        [Header("Equipamiento Visual")]
        public bool hasShieldEquipped = false;
        public bool hasTwoHandedWeaponEquipped = false;
        private Transform _lArm, _lForeArm, _rArm, _rForeArm;

        // Serializados para que sobrevivan al guardado de la escena (causa del bug: iban todos a (0,0,0)).
        [SerializeField] private Vector3 _barracksHomePos;
        [SerializeField] private Quaternion _barracksHomeRot = Quaternion.identity;
        [SerializeField] private Vector3 _formationSlotPos;
        [SerializeField] private Quaternion _formationSlotRot = Quaternion.identity;
        [SerializeField] private bool _configured;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable[] _clipPlayables;
        private float[] _currentWeights;
        private float[] _targetWeights;
        private float _stateTimer;
        private float _waitDuration = 3.5f;
        private bool _hasSalutedPlayerCasual;
        private bool _graphInitialized, _graphPaused;
        private Vector3 _casualWalkTarget;
        private Vector3 _stationPos; private Quaternion _stationRot; private bool _stationAttention; private Action _onArrive;
        private Vector3 _moveTarget;

        public Vector3 BarracksHomePos => _barracksHomePos;
        public Vector3 FormationSlotPos => _formationSlotPos;
        public bool IsAtRest => currentState == TroopState.FormedInAttention || currentState == TroopState.AtStation;

        private const int SLOT_UPRIGHT = 0, SLOT_ATTENTION = 1, SLOT_WALK = 2, SLOT_SPRINT = 3, SLOT_SALUTE = 4, SLOT_SIT = 5, TOTAL_SLOTS = 6;

        private void Awake()
        {
            if (targetAnimator == null) targetAnimator = GetComponentInChildren<Animator>();
            if (skinnedMeshRenderer == null) skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (nav == null) nav = GetComponent<NpcNavigator>();
            if (nav == null) nav = gameObject.AddComponent<NpcNavigator>();
            if (hipsBone == null)
                foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "Hips") { hipsBone = t; break; }
        }

        public void ConfigureTroop(int index, int row, int col, Vector3 barracksPos, Quaternion barracksRot,
                                   Vector3 formationPos, Quaternion formationRot, InteractiveChair optionalChair)
        {
            troopIndex = index; formationRow = row; formationCol = col;
            _barracksHomePos = barracksPos; _barracksHomeRot = barracksRot;
            _casualWalkTarget = barracksPos;
            _formationSlotPos = formationPos; _formationSlotRot = formationRot;
            assignedChair = optionalChair;
            _configured = true;
            transform.SetPositionAndRotation(barracksPos, barracksRot);
        }

        private void Start()
        {
            if (!_configured) { _barracksHomePos = transform.position; _barracksHomeRot = transform.rotation; _configured = true; }
            _casualWalkTarget = _barracksHomePos;
            InitializeGraph();

            if (assignedChair != null)
            {
                assignedChair.isOccupied = true; assignedChair.currentOccupant = transform;
                nav.SetSeated(true);
                transform.SetPositionAndRotation(assignedChair.GetSeatedRootPosition(), assignedChair.GetSeatedRotation());
                EnterState(TroopState.CasualSeated);
            }
            else if (troopIndex % 3 == 0) { PickCasualWalkTarget(); EnterState(TroopState.CasualWalking); }
            else EnterState(TroopState.CasualStandingUpright);
        }

        private void InitializeGraph()
        {
            if (_graphInitialized || targetAnimator == null) return;
            targetAnimator.applyRootMotion = false;
            targetAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            _graph = PlayableGraph.Create("UTOP_Troop_" + troopIndex);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "TroopOut", targetAnimator);
            _mixer = AnimationMixerPlayable.Create(_graph, TOTAL_SLOTS);
            output.SetSourcePlayable(_mixer);
            _clipPlayables = new AnimationClipPlayable[TOTAL_SLOTS];
            _currentWeights = new float[TOTAL_SLOTS]; _targetWeights = new float[TOTAL_SLOTS];
            AnimationClip[] clips = { uprightIdleClip, militaryAttentionClip != null ? militaryAttentionClip : uprightIdleClip, walkClip, sprintClip, saluteClip, sitClip };
            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                if (clips[i] != null)
                {
                    _clipPlayables[i] = AnimationClipPlayable.Create(_graph, clips[i]);
                    if (i == SLOT_UPRIGHT || i == SLOT_WALK) _clipPlayables[i].SetTime((troopIndex * 0.17f) % Mathf.Max(0.5f, clips[i].length));
                    _graph.Connect(_clipPlayables[i], 0, _mixer, i);
                }
                float w = i == SLOT_UPRIGHT ? 1f : 0f;
                _currentWeights[i] = w; _targetWeights[i] = w; _mixer.SetInputWeight(i, w);
            }
            _graph.Play();
            _graphInitialized = true;
        }

        // ---------------------------------------------------------------- comandos externos
        private void LeaveChairIfSeated()
        {
            if (currentState == TroopState.CasualSeated && assignedChair != null)
            {
                Vector3 front = assignedChair.GetStandInFrontPosition();
                assignedChair.BeginStandSequence();
                nav.ReleaseAt(front, transform.rotation);
            }
        }

        public void CommandMoveToFormation(Vector3 slotPos, Quaternion slotRot)
        {
            _formationSlotPos = slotPos; _formationSlotRot = slotRot;
            LeaveChairIfSeated();
            EnterState(TroopState.MovingToFormation);
        }

        public void CommandBreakFormation()
        {
            LeaveChairIfSeated();
            EnterState(TroopState.ReturningToBarracks);
        }

        public void CommandMoveToStation(Vector3 pos, Quaternion rot, bool attention, Action onArrive = null, string role = null)
        {
            _stationPos = pos; _stationRot = rot; _stationAttention = attention; _onArrive = onArrive;
            if (role != null) assignedRole = role;
            LeaveChairIfSeated();
            EnterState(TroopState.MovingToStation);
        }

        public void CommandSaluteInUnison()
        {
            if (currentState == TroopState.FormedInAttention || currentState == TroopState.MovingToFormation)
            {
                EnterState(TroopState.FormedSalutingUnison);
            }
        }

        public void Equip(GameObject prop, string boneName, Vector3 localPos, Vector3 localEuler, float scale = 1f)
        {
            Transform bone = null;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == boneName) { bone = t; break; }
            if (bone == null || prop == null) return;
            prop.transform.SetParent(bone, false);
            prop.transform.localPosition = localPos;
            prop.transform.localRotation = Quaternion.Euler(localEuler);
            prop.transform.localScale = Vector3.one * scale;
        }

        // ---------------------------------------------------------------- estados
        private void EnterState(TroopState newState)
        {
            if (!_graphInitialized) InitializeGraph();
            if (_graphPaused) { _graphPaused = false; _graph.Play(); }
            currentState = newState; _stateTimer = 0f;
            switch (newState)
            {
                case TroopState.CasualStandingUpright: nav.Stop(); _waitDuration = UnityEngine.Random.Range(2.8f, 6.0f); break;
                case TroopState.CasualSeated: _waitDuration = UnityEngine.Random.Range(6f, 12f); ResetClip(SLOT_SIT, (troopIndex * 0.13f) % 1f); break;
                case TroopState.CasualSalutingOnce: nav.Stop(); ResetClip(SLOT_SALUTE, 0); break;
                case TroopState.FormedInAttention: nav.Stop(); ResetClip(SLOT_ATTENTION, 0); break;
                case TroopState.FormedSalutingUnison: nav.Stop(); ResetClip(SLOT_SALUTE, 0); break;
                case TroopState.AtStation: nav.Stop(); ResetClip(SLOT_ATTENTION, 0); break;
            }
        }

        private void ResetClip(int slot, double time)
        {
            if (_clipPlayables != null && slot >= 0 && slot < _clipPlayables.Length && _clipPlayables[slot].IsValid())
            { _clipPlayables[slot].SetTime(time); _clipPlayables[slot].SetSpeed(1.0); }
        }

        public void ManagedUpdate(float dt, Vector3 playerPos, bool playerNear)
        {
            if (!_graphInitialized) { InitializeGraph(); if (!_graphInitialized) return; }

            bool busy = currentState == TroopState.MovingToFormation || currentState == TroopState.ReturningToBarracks ||
                        currentState == TroopState.MovingToStation || currentState == TroopState.FormedSalutingUnison ||
                        currentState == TroopState.CasualSalutingOnce;
            bool visible = skinnedMeshRenderer != null && skinnedMeshRenderer.isVisible;
            float distSq = (transform.position - playerPos).sqrMagnitude;
            bool shouldPause = (!busy && !visible && distSq > 64f) || (!busy && distSq > 340f);
            if (shouldPause != _graphPaused) { _graphPaused = shouldPause; if (_graphPaused) _graph.Stop(); else _graph.Play(); }
            if (_graphPaused) return;

            UpdateBehavior(dt, playerPos, distSq);
            ComputeTargetWeights();
            BlendWeights(dt);
        }

        private void CacheLimbs()
        {
            if (_lArm != null) return;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "LeftArm") _lArm = t;
                else if (t.name == "LeftForeArm") _lForeArm = t;
                else if (t.name == "RightArm") _rArm = t;
                else if (t.name == "RightForeArm") _rForeArm = t;
            }
        }

        private void LateUpdate()
        {
            if (_graphPaused || hipsBone == null) return;
            Vector3 lp = hipsBone.localPosition;
            hipsBone.localPosition = new Vector3(0f, 0f, lp.z);

            // Ajuste esquelético si tiene equipamiento asignado
            if (currentState != TroopState.CasualSeated && currentState != TroopState.CasualSalutingOnce && currentState != TroopState.FormedSalutingUnison)
            {
                CacheLimbs();
                if (hasShieldEquipped)
                {
                    // Brazo izquierdo colocado al frente protegiéndose con el escudo
                    if (_lArm != null) _lArm.localRotation = new Quaternion(0.22f, -0.45f, -0.48f, 0.72f).normalized;
                    if (_lForeArm != null) _lForeArm.localRotation = new Quaternion(0.28f, -0.35f, -0.68f, 0.58f).normalized;
                }
                if (hasTwoHandedWeaponEquipped)
                {
                    // Brazos derecho e izquierdo sosteniendo el lanzagas / escopeta con ambas manos al frente
                    if (_rArm != null) _rArm.localRotation = new Quaternion(0.24f, -0.26f, 0.44f, 0.82f).normalized;
                    if (_rForeArm != null) _rForeArm.localRotation = new Quaternion(0.18f, 0.28f, 0.62f, 0.71f).normalized;
                    if (!hasShieldEquipped)
                    {
                        if (_lArm != null) _lArm.localRotation = new Quaternion(0.16f, -0.48f, -0.44f, 0.74f).normalized;
                        if (_lForeArm != null) _lForeArm.localRotation = new Quaternion(0.22f, -0.20f, -0.58f, 0.75f).normalized;
                    }
                }
            }
        }

        private void PickCasualWalkTarget()
        {
            Vector2 rnd = UnityEngine.Random.insideUnitCircle * 1.65f;
            float tx = Mathf.Clamp(_barracksHomePos.x + rnd.x, 21.2f, 33.0f);
            float tz = Mathf.Clamp(_barracksHomePos.z + rnd.y, -24.8f, -19.8f);
            _casualWalkTarget = new Vector3(tx, _barracksHomePos.y, tz);
        }

        private void UpdateBehavior(float dt, Vector3 playerPos, float distSqToPlayer)
        {
            _stateTimer += dt;

            if (!_hasSalutedPlayerCasual && (currentState == TroopState.CasualStandingUpright || currentState == TroopState.CasualWalking) && distSqToPlayer <= 12.5f)
            {
                Vector3 toPlayer = playerPos - transform.position;
                if (Mathf.Abs(toPlayer.y) < 1.5f)
                {
                    toPlayer.y = 0f;
                    if (Vector3.Dot(transform.forward, toPlayer.normalized) > -0.25f) { _hasSalutedPlayerCasual = true; EnterState(TroopState.CasualSalutingOnce); return; }
                }
            }

            switch (currentState)
            {
                case TroopState.CasualSeated:
                    if (assignedChair != null)
                    {
                        transform.position = Vector3.Lerp(transform.position, assignedChair.GetSeatedRootPosition(), dt * 6f);
                        transform.rotation = Quaternion.Slerp(transform.rotation, assignedChair.GetSeatedRotation(), dt * 6f);
                    }
                    break;

                case TroopState.CasualStandingUpright:
                    if (_stateTimer >= _waitDuration && inContingent) { PickCasualWalkTarget(); EnterState(TroopState.CasualWalking); }
                    break;

                case TroopState.CasualWalking:
                    nav.MoveTo(_casualWalkTarget, 1.7f); nav.FaceMovement(dt);
                    if (nav.FlatDistanceTo(_casualWalkTarget) <= 0.3f || _stateTimer >= 6f) EnterState(TroopState.CasualStandingUpright);
                    break;

                case TroopState.CasualSalutingOnce:
                    nav.FaceDirection(playerPos - transform.position, dt);
                    if (_stateTimer >= (saluteClip != null ? saluteClip.length : 2.83f) - 0.2f) EnterState(TroopState.CasualStandingUpright);
                    break;

                case TroopState.MovingToFormation:
                {
                    float d = nav.FlatDistanceTo(_formationSlotPos);
                    SteerThroughDoorways(_formationSlotPos, d > 2.5f ? 4.6f : 2.4f);
                    nav.FaceMovement(dt);
                    if (d <= 0.22f && _stateTimer > 0.5f) { nav.Warp(_formationSlotPos, _formationSlotRot); EnterState(TroopState.FormedInAttention); }
                    break;
                }

                case TroopState.FormedInAttention:
                case TroopState.AtStation:
                {
                    Quaternion target = currentState == TroopState.AtStation ? _stationRot : _formationSlotRot;
                    transform.rotation = Quaternion.Slerp(transform.rotation, target, dt * 8f);
                    break;
                }

                case TroopState.FormedSalutingUnison:
                    transform.rotation = Quaternion.Slerp(transform.rotation, _formationSlotRot, dt * 10f);
                    if (_stateTimer >= (saluteClip != null ? saluteClip.length : 2.83f) - 0.15f) EnterState(TroopState.FormedInAttention);
                    break;

                case TroopState.MovingToStation:
                {
                    float d = nav.FlatDistanceTo(_stationPos);
                    SteerThroughDoorways(_stationPos, d > 2.5f ? 3.8f : 2.3f);
                    nav.FaceMovement(dt);
                    if (d <= 0.25f && _stateTimer > 0.4f)
                    {
                        nav.Warp(_stationPos, _stationRot);
                        EnterState(TroopState.AtStation);
                        var cb = _onArrive; _onArrive = null; if (cb != null) cb();
                    }
                    break;
                }

                case TroopState.ReturningToBarracks:
                {
                    float d = nav.FlatDistanceTo(_barracksHomePos);
                    SteerThroughDoorways(_barracksHomePos, 3.2f);
                    nav.FaceMovement(dt);
                    if (d <= 0.3f)
                    {
                        if (assignedChair != null)
                        {
                            nav.SetSeated(true); assignedChair.BeginSitSequence(transform); EnterState(TroopState.CasualSeated);
                        }
                        else { transform.rotation = _barracksHomeRot; EnterState(TroopState.CasualStandingUpright); }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Garantiza que al cruzar entre la Cuadra de Tropa 1 (X > 19.85m, puerta en Z=-22.4),
        /// el Patio de Honor (X = 7..19m, Z = -28..-4.5m) y la Sala de Armas / Armería (Z > 5.6m, puerta en X=12.4, Z=5.8),
        /// el efectivo pase siempre por los vanos de las puertas sin tocar jamás un muro.
        /// </summary>
        private void SteerThroughDoorways(Vector3 finalDest, float speed)
        {
            Vector3 myPos = transform.position;
            float laneOffset = (formationRow - 1) * 0.28f;

            // 1. Saliendo de Cuadra de Tropa 1 (X > 19.85) hacia el Patio o Armería (finalDest.x < 19.5)
            if (myPos.x > 19.85f && finalDest.x < 19.5f)
            {
                float doorZ = -22.4f + laneOffset;
                if (Mathf.Abs(myPos.z - doorZ) > 0.85f)
                {
                    nav.MoveTo(new Vector3(20.8f, myPos.y, doorZ), speed);
                    return;
                }
                nav.MoveTo(new Vector3(18.6f, -0.30f, doorZ), speed);
                return;
            }

            // 2. Entrando del Patio (X < 19.6) hacia Cuadra de Tropa 1 (finalDest.x > 20.1)
            if (myPos.x < 19.6f && finalDest.x > 20.1f)
            {
                if (myPos.z > 5.2f)
                {
                    // Primero salir de la Armería al Garaje/Patio
                    nav.MoveTo(new Vector3(12.4f + laneOffset, -0.30f, 4.5f), speed);
                    return;
                }
                float doorZ = -22.4f + laneOffset;
                if (Mathf.Abs(myPos.z - doorZ) > 0.95f)
                {
                    nav.MoveTo(new Vector3(18.6f, -0.30f, doorZ), speed);
                    return;
                }
                nav.MoveTo(new Vector3(20.8f, finalDest.y, doorZ), speed);
                return;
            }

            // 3. Yendo del Patio (Z < 4.8) hacia la Sala de Armas / Armería (finalDest.z > 5.8)
            if (myPos.z < 4.8f && finalDest.z > 5.8f)
            {
                float armoryDoorX = 12.4f + laneOffset;
                if (Mathf.Abs(myPos.x - armoryDoorX) > 1.1f)
                {
                    nav.MoveTo(new Vector3(armoryDoorX, -0.30f, Mathf.Max(myPos.z, -5.0f)), speed);
                    return;
                }
                nav.MoveTo(new Vector3(armoryDoorX, -0.30f, 6.4f), speed);
                return;
            }

            // 4. Saliendo de la Sala de Armas / Armería (myPos.z > 5.6) hacia el Patio (finalDest.z < 4.8)
            if (myPos.z > 5.6f && finalDest.z < 4.8f)
            {
                float armoryDoorX = 12.4f + laneOffset;
                if (Mathf.Abs(myPos.x - armoryDoorX) > 0.95f)
                {
                    nav.MoveTo(new Vector3(armoryDoorX, -0.30f, 6.8f), speed);
                    return;
                }
                nav.MoveTo(new Vector3(armoryDoorX, -0.30f, 3.8f), speed);
                return;
            }

            nav.MoveTo(finalDest, speed);
        }

        private void ComputeTargetWeights()
        {
            for (int i = 0; i < TOTAL_SLOTS; i++) _targetWeights[i] = 0f;
            float speed = nav != null ? nav.Velocity.magnitude : 0f;
            switch (currentState)
            {
                case TroopState.CasualStandingUpright: _targetWeights[SLOT_UPRIGHT] = 1f; break;
                case TroopState.CasualSeated: _targetWeights[SLOT_SIT] = 1f; break;
                case TroopState.CasualSalutingOnce:
                case TroopState.FormedSalutingUnison: _targetWeights[SLOT_SALUTE] = 1f; break;
                case TroopState.FormedInAttention: _targetWeights[SLOT_ATTENTION] = 1f; break;
                case TroopState.AtStation: _targetWeights[_stationAttention ? SLOT_ATTENTION : SLOT_UPRIGHT] = 1f; break;
                default: // movimiento: se elige según la velocidad real del agente
                    if (speed < 0.25f) _targetWeights[SLOT_UPRIGHT] = 1f;
                    else if (speed > 3.3f) _targetWeights[SLOT_SPRINT] = 1f;
                    else _targetWeights[SLOT_WALK] = 1f;
                    break;
            }
            if (_clipPlayables != null && _clipPlayables[SLOT_WALK].IsValid())
                _clipPlayables[SLOT_WALK].SetSpeed(Mathf.Clamp(speed / 1.9f, 0.7f, 1.4f));
            if (_clipPlayables != null && _clipPlayables[SLOT_SPRINT].IsValid())
                _clipPlayables[SLOT_SPRINT].SetSpeed(Mathf.Clamp(speed / 4.6f, 0.8f, 1.2f));
        }

        private void BlendWeights(float dt)
        {
            float sum = 0f;
            for (int i = 0; i < TOTAL_SLOTS; i++) { _currentWeights[i] = Mathf.MoveTowards(_currentWeights[i], _targetWeights[i], 6.5f * dt); sum += _currentWeights[i]; }
            if (sum > 0.0001f)
                for (int i = 0; i < TOTAL_SLOTS; i++) { float n = _currentWeights[i] / sum; _currentWeights[i] = n; _mixer.SetInputWeight(i, n); }
        }

        private void OnDestroy() { if (_graphInitialized && _graph.IsValid()) _graph.Destroy(); }
    }
}
