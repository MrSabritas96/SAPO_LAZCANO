using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimuladorBloqueos
{
    public enum ProtesterType { Minero = 0, Cholita = 1, PonchoRojo = 2 }
    public enum ProtesterMood { Calm = 0, Tense = 1, Agitated = 2 }

    /// <summary>
    /// Manifestante NPC (Minero, Cholita o Poncho Rojo). Patrulla, mira al jugador, lanza piedras
    /// (solo el minero puede lanzar dinamita), reacciona a golpes y — sobre todo — reacciona automáticamente
    /// al ambiente y a las decisiones del jugador mediante el ScenarioDirector (estado de ánimo, lanzamientos
    /// dirigidos a un punto de la escena, huida, lesiones con causa identificable).
    /// </summary>
    public class MinerBlockerNPCController : MonoBehaviour
    {
        public enum MinerBehaviorState
        {
            PatrolIdleUpright = 0, PatrolIdleDefiant, PatrolWalking, FacingPlayerReady, SoftThrow, StrongOverhandThrow,
            FleeingTearGas, ReturningFromFlee, ChargeSprintDemo, DefiantPoseDemo, HitReaction, ScriptedMoving
        }

        public static readonly List<MinerBlockerNPCController> All = new List<MinerBlockerNPCController>();
        public static event Action<MinerBlockerNPCController, string> OnProtesterEvent;

        [Header("Identidad")]
        public ProtesterType protesterType = ProtesterType.Minero;
        public int minerIndex = 1;
        public string minerRoleTitle = "Minero Bloqueador #1";
        public bool canThrowDynamite = true;
        public float patrolRadius = 2.6f;
        public float walkSpeed = 1.9f;
        public float fleeSpeed = 4.4f;

        [Header("Referencias")]
        public Animator targetAnimator;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public Transform hipsBone;
        public Transform rightHandBone;
        public NpcNavigator nav;

        [Header("Clips")]
        public AnimationClip uprightIdleClip, idle2Clip, idle4Clip, walkClip, running2Clip, run2Clip, sprintClip,
                             runLookBackClip, goalieThrowClip, overhandThrowClip, headHitClip;
        [Tooltip("Segundos máximos de reproducción del clip de reacción (útil si el clip termina en el suelo).")]
        public float hitClipMaxSeconds = 99f;

        [Header("Estado")]
        public MinerBehaviorState currentState = MinerBehaviorState.PatrolIdleUpright;
        public ProtesterMood mood = ProtesterMood.Calm;
        public Vector3 scenarioTarget;
        public bool hasScenarioTarget;
        public float blendSpeed = 6.5f;
        public float autoThrowIntervalMin = 45f;
        public float autoThrowIntervalMax = 60f;

        [SerializeField] private Vector3 _homePosition;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable[] _clips;
        private float[] _cur, _tgt;
        private float _stateTimer, _waitDuration = 3f, _nextAutoThrow;
        private bool _projectileSpawned, _graphInitialized, _graphPaused;
        private Vector3 _patrolTarget, _fleeTarget, _scriptedTarget;
        private Action _scriptedArrive;
        private Transform _playerTransform;
        private UTOPCharacterAnimationController _playerAnim;
        private bool _strongIsDynamite;

        private static MinerBlockerNPCController _closest;
        private static float _closestDist = 999f;
        private static int _lastFrame = -1;
        private static Texture2D _bgTex;

        private const int S_UP = 0, S_I2 = 1, S_I4 = 2, S_WALK = 3, S_RUNNING2 = 4, S_RUN2 = 5, S_SPRINT = 6, S_LOOKBACK = 7, S_SOFT = 8, S_STRONG = 9, S_HIT = 10, N = 11;

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        private void Awake()
        {
            if (targetAnimator == null) targetAnimator = GetComponentInChildren<Animator>();
            if (skinnedMeshRenderer == null) skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (nav == null) nav = GetComponent<NpcNavigator>();
            if (nav == null) nav = gameObject.AddComponent<NpcNavigator>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (hipsBone == null && t.name == "Hips") hipsBone = t;
                else if (rightHandBone == null && t.name == "RightHand") rightHandBone = t;
            }
        }

        private void Start()
        {
            if (_homePosition.sqrMagnitude < 0.01f) _homePosition = transform.position;
            _patrolTarget = _homePosition;
            _nextAutoThrow = Time.time + UnityEngine.Random.Range(5f, 9f);
            InitializeGraph();
            if (minerIndex % 2 == 0) { PickNewPatrolPoint(); EnterState(MinerBehaviorState.PatrolWalking); }
        }

        public void SetHomePosition(Vector3 pos) { _homePosition = pos; _patrolTarget = pos; transform.position = pos; }

        private AnimationClip Fallback(AnimationClip c, params AnimationClip[] alt)
        {
            if (c != null) return c;
            foreach (var a in alt) if (a != null) return a;
            return null;
        }

        private void InitializeGraph()
        {
            if (_graphInitialized || targetAnimator == null) return;
            targetAnimator.applyRootMotion = false;
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graph = PlayableGraph.Create("Protester_Graph_" + protesterType + minerIndex);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "ProtOut", targetAnimator);
            _mixer = AnimationMixerPlayable.Create(_graph, N);
            output.SetSourcePlayable(_mixer);
            _clips = new AnimationClipPlayable[N]; _cur = new float[N]; _tgt = new float[N];
            var up = Fallback(uprightIdleClip, idle2Clip, idle4Clip);
            var runA = Fallback(running2Clip, run2Clip, sprintClip, walkClip);
            AnimationClip[] cl =
            {
                up, Fallback(idle2Clip, idle4Clip, up), Fallback(idle4Clip, up), Fallback(walkClip, up), runA,
                Fallback(run2Clip, runA), Fallback(sprintClip, run2Clip, runA), Fallback(runLookBackClip, runA),
                Fallback(goalieThrowClip, overhandThrowClip, up), Fallback(overhandThrowClip, goalieThrowClip, up), Fallback(headHitClip, up)
            };
            for (int i = 0; i < N; i++)
            {
                if (cl[i] != null) { _clips[i] = AnimationClipPlayable.Create(_graph, cl[i]); _graph.Connect(_clips[i], 0, _mixer, i); }
                float w = i == S_UP ? 1f : 0f; _cur[i] = w; _tgt[i] = w; _mixer.SetInputWeight(i, w);
            }
            _graph.Play(); _graphInitialized = true;
        }

        private void ResetClip(int slot)
        {
            if (_clips != null && slot >= 0 && slot < N && _clips[slot].IsValid()) { _clips[slot].SetTime(0); _clips[slot].SetSpeed(1); }
        }

        // ------------------------------------------------------------------ API para el ScenarioDirector
        public void SetMood(ProtesterMood m)
        {
            mood = m;
            patrolRadius = m == ProtesterMood.Calm ? 2.6f : (m == ProtesterMood.Tense ? 1.6f : 1.1f);
            walkSpeed = m == ProtesterMood.Agitated ? 2.4f : 1.9f;
        }

        public void SetScenarioTarget(Vector3 t) { scenarioTarget = t; hasScenarioTarget = true; }

        public void ScriptedThrow(Vector3 target, bool strong)
        {
            scenarioTarget = target; hasScenarioTarget = true;
            if (currentState == MinerBehaviorState.HitReaction) return;
            EnterState(strong && (canThrowDynamite || overhandThrowClip != null) ? MinerBehaviorState.StrongOverhandThrow : MinerBehaviorState.SoftThrow);
            _strongIsDynamite = strong && canThrowDynamite;
        }

        public void ScriptedThrow(bool useDynamiteIfAllowed)
        {
            Vector3 defaultGateTarget = new Vector3(4.8f, 0.2f, -6.2f);
            ScriptedThrow(defaultGateTarget, useDynamiteIfAllowed);
        }

        public void ScriptedFlee(Vector3 awayFrom)
        {
            Vector3 dir = transform.position - awayFrom; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            _fleeTarget = transform.position + dir.normalized * 6f;
            EnterState(MinerBehaviorState.FleeingTearGas);
        }

        public void ScriptedGoTo(Vector3 pos, Action onArrive = null)
        {
            _scriptedTarget = pos; _scriptedArrive = onArrive; EnterState(MinerBehaviorState.ScriptedMoving);
        }

        public void ScriptedInjury(string cause = "commotion")
        {
            EnterState(MinerBehaviorState.HitReaction);
            if (OnProtesterEvent != null) OnProtesterEvent(this, "injury:" + cause);
        }

        public void OnHitByPlayerPunch(Transform attacker)
        {
            if (attacker != null)
            {
                Vector3 d = attacker.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
            EnterState(MinerBehaviorState.HitReaction);
            if (OnProtesterEvent != null) OnProtesterEvent(this, "injury:player_punch");
        }

        // ------------------------------------------------------------------ bucle
        private void Update()
        {
            if (!_graphInitialized) { InitializeGraph(); if (!_graphInitialized) return; }
            if (_playerTransform == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) { _playerTransform = pc.transform; _playerAnim = pc.GetComponent<UTOPCharacterAnimationController>(); }
            }
            float dist = _playerTransform != null ? Vector3.Distance(transform.position, _playerTransform.position) : 999f;

            if (Time.frameCount != _lastFrame) { _lastFrame = Time.frameCount; _closest = null; _closestDist = 5.0f; }
            if (dist < _closestDist) { _closestDist = dist; _closest = this; }

            bool busy = currentState == MinerBehaviorState.FleeingTearGas || currentState == MinerBehaviorState.ReturningFromFlee ||
                        currentState == MinerBehaviorState.HitReaction || currentState == MinerBehaviorState.ScriptedMoving ||
                        currentState == MinerBehaviorState.SoftThrow || currentState == MinerBehaviorState.StrongOverhandThrow;
            bool pause = !busy && dist > 26f && (skinnedMeshRenderer == null || !skinnedMeshRenderer.isVisible);
            if (pause != _graphPaused) { _graphPaused = pause; if (pause) _graph.Stop(); else _graph.Play(); }
            if (_graphPaused) return;

            if (_closest == this && dist <= 5f) ReadDemoKeys();
            UpdateStateMachine(Time.deltaTime, dist);
            ComputeWeights();
            ApplyWeights(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (_graphPaused || hipsBone == null) return;
            Vector3 lp = hipsBone.localPosition; hipsBone.localPosition = new Vector3(0f, 0f, lp.z);
        }

        private void ReadDemoKeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current; if (kb == null) return;
            if (kb.tKey.wasPressedThisFrame) { _strongIsDynamite = false; EnterState(MinerBehaviorState.SoftThrow); }
            else if (kb.yKey.wasPressedThisFrame) { _strongIsDynamite = canThrowDynamite; EnterState(MinerBehaviorState.StrongOverhandThrow); }
            else if (kb.uKey.wasPressedThisFrame) { if (_playerTransform != null) ScriptedFlee(_playerTransform.position); }
            else if (kb.iKey.wasPressedThisFrame) EnterState(MinerBehaviorState.ChargeSprintDemo);
            else if (kb.oKey.wasPressedThisFrame) EnterState(MinerBehaviorState.DefiantPoseDemo);
#endif
        }

        private void EnterState(MinerBehaviorState s)
        {
            if (!_graphInitialized) InitializeGraph();
            currentState = s; _stateTimer = 0f; _projectileSpawned = false;
            switch (s)
            {
                case MinerBehaviorState.PatrolIdleUpright: nav.Stop(); _waitDuration = UnityEngine.Random.Range(2.4f, 4.5f); ResetClip(S_UP); break;
                case MinerBehaviorState.PatrolIdleDefiant: nav.Stop(); _waitDuration = UnityEngine.Random.Range(2f, 3.8f); ResetClip(S_I2); break;
                case MinerBehaviorState.PatrolWalking: ResetClip(S_WALK); break;
                case MinerBehaviorState.FacingPlayerReady: nav.Stop(); ResetClip(S_UP); break;
                case MinerBehaviorState.SoftThrow: nav.Stop(); ResetClip(S_SOFT); _strongIsDynamite = false; break;
                case MinerBehaviorState.StrongOverhandThrow: nav.Stop(); ResetClip(S_STRONG); break;
                case MinerBehaviorState.FleeingTearGas:
                    if (_fleeTarget == Vector3.zero) _fleeTarget = _homePosition + new Vector3(0f, 0f, -5.8f);
                    ResetClip(S_LOOKBACK); break;
                case MinerBehaviorState.ReturningFromFlee: ResetClip(S_RUNNING2); break;
                case MinerBehaviorState.ChargeSprintDemo: nav.Stop(); ResetClip(S_RUN2); ResetClip(S_SPRINT); break;
                case MinerBehaviorState.DefiantPoseDemo: nav.Stop(); ResetClip(S_I2); break;
                case MinerBehaviorState.HitReaction: nav.Stop(); ResetClip(S_HIT); break;
                case MinerBehaviorState.ScriptedMoving: ResetClip(S_RUNNING2); break;
            }
        }

        private void PickNewPatrolPoint()
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * patrolRadius;
            _patrolTarget = _homePosition + new Vector3(Mathf.Clamp(r.x, -2.2f, 2.2f), 0f, r.y);
        }

        private Vector3 LookDir(Vector3 playerLook)
        {
            if (hasScenarioTarget && mood != ProtesterMood.Calm) return scenarioTarget - transform.position;
            return playerLook;
        }

        private void UpdateStateMachine(float dt, float dist)
        {
            _stateTimer += dt;
            Vector3 toPlayer = _playerTransform != null ? _playerTransform.position - transform.position : transform.forward;
            bool near = dist <= 4.6f;
            MinerBehaviorState rest = near ? MinerBehaviorState.FacingPlayerReady : MinerBehaviorState.PatrolIdleUpright;

            // Lanzamientos autónomos hacia el punto del escenario (nunca contra el jugador salvo que esté en ese punto)
            if (mood != ProtesterMood.Calm && hasScenarioTarget && Time.time >= _nextAutoThrow &&
                (currentState == MinerBehaviorState.PatrolIdleUpright || currentState == MinerBehaviorState.PatrolIdleDefiant || currentState == MinerBehaviorState.PatrolWalking))
            {
                _nextAutoThrow = Time.time + UnityEngine.Random.Range(autoThrowIntervalMin, autoThrowIntervalMax);
                ScriptedThrow(scenarioTarget, mood == ProtesterMood.Agitated && UnityEngine.Random.value < 0.35f);
                return;
            }

            switch (currentState)
            {
                case MinerBehaviorState.PatrolIdleUpright:
                case MinerBehaviorState.PatrolIdleDefiant:
                    if (near) { EnterState(MinerBehaviorState.FacingPlayerReady); return; }
                    if (mood != ProtesterMood.Calm && hasScenarioTarget) nav.FaceDirection(scenarioTarget - transform.position, dt);
                    if (_stateTimer >= _waitDuration) { PickNewPatrolPoint(); EnterState(MinerBehaviorState.PatrolWalking); }
                    break;

                case MinerBehaviorState.PatrolWalking:
                    if (near) { EnterState(MinerBehaviorState.FacingPlayerReady); return; }
                    nav.MoveTo(_patrolTarget, walkSpeed); nav.FaceMovement(dt);
                    if (nav.FlatDistanceTo(_patrolTarget) <= 0.3f || _stateTimer >= 5f)
                        EnterState(UnityEngine.Random.value < 0.65f ? MinerBehaviorState.PatrolIdleUpright : MinerBehaviorState.PatrolIdleDefiant);
                    break;

                case MinerBehaviorState.FacingPlayerReady:
                    nav.FaceDirection(toPlayer, dt, 7f);
                    if (!near) EnterState(MinerBehaviorState.PatrolIdleUpright);
                    break;

                case MinerBehaviorState.SoftThrow:
                case MinerBehaviorState.StrongOverhandThrow:
                {
                    bool strong = currentState == MinerBehaviorState.StrongOverhandThrow;
                    Vector3 aim = hasScenarioTarget && mood != ProtesterMood.Calm ? scenarioTarget - transform.position : toPlayer;
                    nav.FaceDirection(aim, dt, 8f);
                    if (!_projectileSpawned && _stateTimer >= (strong ? 0.95f : 1.15f)) { _projectileSpawned = true; SpawnProjectile(strong); }
                    AnimationClip c = strong ? overhandThrowClip : goalieThrowClip;
                    float dur = c != null ? c.length : 2.5f;
                    if (_stateTimer >= dur - 0.2f) EnterState(rest);
                    break;
                }

                case MinerBehaviorState.FleeingTearGas:
                    nav.MoveTo(_fleeTarget, fleeSpeed); nav.FaceMovement(dt, 12f);
                    if (nav.FlatDistanceTo(_fleeTarget) <= 0.5f || _stateTimer >= 2.4f) { _fleeTarget = Vector3.zero; EnterState(MinerBehaviorState.ReturningFromFlee); }
                    break;

                case MinerBehaviorState.ReturningFromFlee:
                    nav.MoveTo(_homePosition, walkSpeed * 1.6f); nav.FaceMovement(dt);
                    if (nav.FlatDistanceTo(_homePosition) <= 0.4f || _stateTimer >= 4f) EnterState(rest);
                    break;

                case MinerBehaviorState.ScriptedMoving:
                    nav.MoveTo(_scriptedTarget, 2.6f); nav.FaceMovement(dt);
                    if (nav.FlatDistanceTo(_scriptedTarget) <= 0.5f || _stateTimer >= 12f)
                    {
                        var cb = _scriptedArrive; _scriptedArrive = null;
                        EnterState(MinerBehaviorState.PatrolIdleUpright); if (cb != null) cb();
                    }
                    break;

                case MinerBehaviorState.ChargeSprintDemo:
                    nav.FaceDirection(toPlayer, dt, 7f);
                    if (_stateTimer >= 2.4f) EnterState(rest);
                    break;

                case MinerBehaviorState.DefiantPoseDemo:
                    nav.FaceDirection(toPlayer, dt, 7f);
                    if (_stateTimer >= 2.5f) EnterState(rest);
                    break;

                case MinerBehaviorState.HitReaction:
                {
                    float dur = Mathf.Min(headHitClip != null ? headHitClip.length : 1.4f, hitClipMaxSeconds);
                    if (_stateTimer >= dur - 0.12f) EnterState(rest);
                    break;
                }
            }
        }

        private void SpawnProjectile(bool strong)
        {
            bool dynamite = strong && canThrowDynamite && _strongIsDynamite;
            Vector3 spawn = rightHandBone != null ? rightHandBone.position : transform.position + Vector3.up * 1.45f + transform.forward * 0.35f;
            var proj = GameObject.CreatePrimitive(dynamite ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
            proj.name = dynamite ? "Proyectil_Dinamita_Manifestante" : "Proyectil_Piedra_Manifestante";
            proj.transform.position = spawn;
            proj.transform.localScale = dynamite ? new Vector3(0.08f, 0.16f, 0.08f) : (strong ? new Vector3(0.20f, 0.17f, 0.2f) : new Vector3(0.13f, 0.11f, 0.14f));
            var col = proj.GetComponent<Collider>(); if (col != null) Destroy(col);
            var rend = proj.GetComponent<Renderer>();
            if (rend != null)
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(lit != null ? lit : Shader.Find("Standard"));
                mat.color = dynamite ? new Color(0.82f, 0.18f, 0.14f) : new Color(0.45f, 0.43f, 0.40f);
                rend.sharedMaterial = mat;
            }
            bool aimAtScenario = hasScenarioTarget && mood != ProtesterMood.Calm;
            Vector3 target = aimAtScenario ? scenarioTarget : (_playerTransform != null ? _playerTransform.position + Vector3.up * 0.9f : transform.position + transform.forward * 5f);
            var mover = proj.AddComponent<MinerProjectileArc>();
            mover.Initialize(spawn, target, dynamite ? 11.5f : 8f, dynamite ? 22f : (strong ? 18f : 14f), _playerAnim, dynamite);
            if (OnProtesterEvent != null) OnProtesterEvent(this, dynamite ? "throw:dynamite" : (strong ? "throw:big_stone" : "throw:stone"));
        }

        private void ComputeWeights()
        {
            for (int i = 0; i < N; i++) _tgt[i] = 0f;
            float speed = nav != null ? nav.Velocity.magnitude : 0f;
            switch (currentState)
            {
                case MinerBehaviorState.PatrolIdleUpright: case MinerBehaviorState.FacingPlayerReady: _tgt[S_UP] = 1f; break;
                case MinerBehaviorState.PatrolIdleDefiant: case MinerBehaviorState.DefiantPoseDemo: _tgt[S_I2] = 1f; break;
                case MinerBehaviorState.PatrolWalking: _tgt[speed < 0.2f ? S_UP : S_WALK] = 1f; break;
                case MinerBehaviorState.SoftThrow: _tgt[S_SOFT] = 1f; break;
                case MinerBehaviorState.StrongOverhandThrow: _tgt[S_STRONG] = 1f; break;
                case MinerBehaviorState.FleeingTearGas: _tgt[S_LOOKBACK] = 1f; break;
                case MinerBehaviorState.ReturningFromFlee: case MinerBehaviorState.ScriptedMoving: _tgt[speed < 0.2f ? S_UP : S_RUNNING2] = 1f; break;
                case MinerBehaviorState.ChargeSprintDemo: _tgt[_stateTimer < 1.1f ? S_RUN2 : S_SPRINT] = 1f; break;
                case MinerBehaviorState.HitReaction: _tgt[S_HIT] = 1f; break;
            }
            if (_clips != null && _clips[S_WALK].IsValid()) _clips[S_WALK].SetSpeed(Mathf.Clamp(speed / 1.9f, 0.7f, 1.5f));
        }

        private void ApplyWeights(float dt)
        {
            float sum = 0f;
            for (int i = 0; i < N; i++) { _cur[i] = Mathf.MoveTowards(_cur[i], _tgt[i], blendSpeed * dt); sum += _cur[i]; }
            if (sum > 0.0001f) for (int i = 0; i < N; i++) { float n = _cur[i] / sum; _cur[i] = n; _mixer.SetInputWeight(i, n); }
        }

        private void OnGUI()
        {
            if (_closest != this || _closestDist > 4.8f) return;
            if (_bgTex == null) { _bgTex = new Texture2D(1, 1); _bgTex.SetPixel(0, 0, Color.white); _bgTex.Apply(); }
            float w = 440f, h = 212f, x = Screen.width - w - 18f, y = 78f;
            GUI.color = new Color(0.11f, 0.08f, 0.05f, 0.92f); GUI.DrawTexture(new Rect(x, y, w, h), _bgTex);
            GUI.color = new Color(0.92f, 0.58f, 0.16f, 1f); GUI.DrawTexture(new Rect(x, y, 4f, h), _bgTex);
            var head = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.78f, 0.32f) } };
            var row = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.95f, 0.92f, 0.88f) } };
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12, y + 8, w - 20, 20), minerRoleTitle.ToUpper() + " — MOVIMIENTOS", head);
            string[] lines =
            {
                "• [R] o [Click Izq]: Golpear con puño -> reacción de daño",
                "• [T]: Lanzar piedra (intensidad suave/media)",
                canThrowDynamite ? "• [Y]: Lanzamiento fuerte (dinamita)" : "• [Y]: Lanzamiento fuerte (piedra grande, sin dinamita)",
                "• [U]: Escapar mirando atrás (RunLookBack)",
                "• [I]: Carrera de avance y carga",
                "• [O]: Postura desafiante de bloqueo",
                "• Ánimo: " + mood + "  |  Estado: " + currentState
            };
            for (int i = 0; i < lines.Length; i++) GUI.Label(new Rect(x + 12, y + 32 + i * 22, w - 20, 20), lines[i], row);
        }

        private void OnDestroy() { if (_graphInitialized && _graph.IsValid()) _graph.Destroy(); }
    }

    /// <summary>Trayectoria parabólica de un proyectil lanzado por un manifestante. La dinamita explota (efecto visual) al caer.</summary>
    public class MinerProjectileArc : MonoBehaviour
    {
        public static event Action<Vector3, bool> OnImpact;
        private Vector3 _start, _end; private float _speed, _damage, _progress, _distance; private bool _dynamite;
        private UTOPCharacterAnimationController _player;

        public void Initialize(Vector3 s, Vector3 e, float speed, float damage, UTOPCharacterAnimationController player, bool dynamite = false)
        { _start = s; _end = e; _speed = speed; _damage = damage; _player = player; _dynamite = dynamite; _distance = Mathf.Max(0.5f, Vector3.Distance(s, e)); }

        private void Update()
        {
            _progress += (_speed / _distance) * Time.deltaTime;
            if (_progress >= 1f)
            {
                if (_player != null && Vector3.Distance(_player.transform.position + Vector3.up * 0.9f, _end) <= 1.35f) _player.ApplyDamage(_damage);
                if (OnImpact != null) OnImpact(_end, _dynamite);
                Destroy(gameObject); return;
            }
            Vector3 p = Vector3.Lerp(_start, _end, _progress); p.y += Mathf.Sin(_progress * Mathf.PI) * 1.15f;
            transform.position = p; transform.Rotate(360f * Time.deltaTime, 180f * Time.deltaTime, 0f);
        }
    }
}
