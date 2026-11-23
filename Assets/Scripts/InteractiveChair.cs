using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Componente para sillas interactivas y con ruedas (Oficina del Capitán, Secretaría,
    /// Sala de Operaciones y Cuadra de Tropa).
    /// - Permite que el jugador o los NPCs sólo puedan sentarse cuando hay una silla disponible cerca.
    /// - Acopla el movimiento de la silla al gesto de la mano del personaje al jalarla/empujarla.
    /// - Simula el deslizamiento físico suave de sillas con ruedas cuando un personaje choca con ellas
    ///   al caminar, evitando bloqueos rígidos.
    /// </summary>
    public class InteractiveChair : MonoBehaviour
    {
        public static readonly List<InteractiveChair> AllChairs = new List<InteractiveChair>();

        [Header("Configuración de Silla Interactiva")]
        public string chairLabel = "Silla de Oficina";
        public bool isWheeled = true;
        public bool allowPlayerSit = true;
        public bool isOccupied = false;
        public Transform currentOccupant;

        [Header("Acoplamiento de Asiento")]
        [Tooltip("Desplazamiento local donde se ubican los pies/raíz del personaje sentado respecto al centro de la silla.")]
        public Vector3 occupantRootLocalOffset = new Vector3(0f, 0f, 0.08f);
        [Tooltip("Distancia que el personaje jala la silla hacia atrás con la mano antes de sentarse.")]
        public float pullBackDistance = 0.24f;
        [Tooltip("Radio máximo de empuje cuando el jugador camina rozando la silla con ruedas.")]
        public float maxPushRadius = 0.55f;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Vector3 _pushedOffset = Vector3.zero;
        private float _pushedYaw = 0f;

        private enum ChairAnimPhase
        {
            Idle = 0,
            PullingOutToSit,
            SlidingInSeated,
            SeatedHold,
            PushingBackToStand
        }

        private ChairAnimPhase _phase = ChairAnimPhase.Idle;
        private float _phaseTimer = 0f;
        private static Transform _cachedPlayerTransform;

        private void OnEnable()
        {
            if (!AllChairs.Contains(this))
                AllChairs.Add(this);
        }

        private void OnDisable()
        {
            AllChairs.Remove(this);
        }

        private void Start()
        {
            _homePosition = transform.position;
            _homeRotation = transform.rotation;

            // Desactivar colisionadores rígidos de las piezas de la silla para que no bloqueen
            // el paso como una pared inamovible y en su lugar la silla ruede suavemente al contacto.
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                cols[i].enabled = false;
            }
        }

        public void SetHomePose(Vector3 pos, Quaternion rot)
        {
            _homePosition = pos;
            _homeRotation = rot;
            transform.position = pos;
            transform.rotation = rot;
        }

        private Vector3 EffectiveHomePos => (_homePosition.sqrMagnitude > 0.0001f) ? _homePosition : transform.position;
        private Quaternion EffectiveHomeRot => (_homePosition.sqrMagnitude > 0.0001f) ? _homeRotation : transform.rotation;

        public Vector3 GetStandInFrontPosition()
        {
            Vector3 fwd = EffectiveHomeRot * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.001f) fwd.Normalize();
            return EffectiveHomePos + _pushedOffset + fwd * 0.34f;
        }

        public Vector3 GetSeatedRootPosition()
        {
            return EffectiveHomePos + _pushedOffset + (EffectiveHomeRot * occupantRootLocalOffset);
        }

        public Quaternion GetSeatedRotation()
        {
            Vector3 fwd = EffectiveHomeRot * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            return Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        public void BeginSitSequence(Transform occupant)
        {
            isOccupied = true;
            currentOccupant = occupant;
            _phase = ChairAnimPhase.PullingOutToSit;
            _phaseTimer = 0f;
        }

        public void BeginStandSequence()
        {
            _phase = ChairAnimPhase.PushingBackToStand;
            _phaseTimer = 0f;
        }

        public void ForceRelease()
        {
            isOccupied = false;
            currentOccupant = null;
            _phase = ChairAnimPhase.Idle;
            _phaseTimer = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 chairFwd = _homeRotation * Vector3.forward;
            chairFwd.y = 0f;
            if (chairFwd.sqrMagnitude > 0.001f) chairFwd.Normalize();
            else chairFwd = Vector3.forward;

            if (_phase == ChairAnimPhase.PullingOutToSit)
            {
                _phaseTimer += dt;
                // Durante los primeros 0.36s el personaje extiende la mano y jala la silla hacia atrás
                float t = Mathf.Clamp01(_phaseTimer / 0.36f);
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                Vector3 pullVec = -chairFwd * (pullBackDistance * smooth);
                transform.position = _homePosition + _pushedOffset + pullVec;
                transform.rotation = _homeRotation * Quaternion.Euler(0f, Mathf.Sin(smooth * Mathf.PI) * 9f, 0f);

                if (_phaseTimer >= 0.36f)
                {
                    _phase = ChairAnimPhase.SlidingInSeated;
                    _phaseTimer = 0f;
                }
                return;
            }

            if (_phase == ChairAnimPhase.SlidingInSeated)
            {
                _phaseTimer += dt;
                // Entre 0.36s y 0.82s el personaje se sienta y acerca la silla bajo su cuerpo
                float t = Mathf.Clamp01(_phaseTimer / 0.44f);
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                Vector3 pullVec = -chairFwd * (pullBackDistance * (1f - smooth));
                transform.position = _homePosition + _pushedOffset + pullVec;
                transform.rotation = Quaternion.Slerp(
                    _homeRotation * Quaternion.Euler(0f, 6f, 0f),
                    _homeRotation,
                    smooth);

                if (_phaseTimer >= 0.44f)
                {
                    _phase = ChairAnimPhase.SeatedHold;
                    transform.position = _homePosition + _pushedOffset;
                    transform.rotation = _homeRotation;
                }
                return;
            }

            if (_phase == ChairAnimPhase.SeatedHold)
            {
                transform.position = Vector3.Lerp(transform.position, _homePosition + _pushedOffset, dt * 8f);
                transform.rotation = Quaternion.Slerp(transform.rotation, _homeRotation, dt * 8f);
                return;
            }

            if (_phase == ChairAnimPhase.PushingBackToStand)
            {
                _phaseTimer += dt;
                // Al levantarse empuja ligeramente la silla hacia atrás y luego la suelta
                float t = Mathf.Clamp01(_phaseTimer / 0.38f);
                float bell = Mathf.Sin(t * Mathf.PI);
                Vector3 pushBack = -chairFwd * (pullBackDistance * 0.85f * bell);
                transform.position = _homePosition + _pushedOffset + pushBack;
                transform.rotation = _homeRotation * Quaternion.Euler(0f, -7f * bell, 0f);

                if (_phaseTimer >= 0.38f)
                {
                    isOccupied = false;
                    currentOccupant = null;
                    _phase = ChairAnimPhase.Idle;
                    _phaseTimer = 0f;
                }
                return;
            }

            // Estado libre: si el jugador camina rozando la silla, ésta rueda suavemente a un lado
            if (!isOccupied)
            {
                if (_cachedPlayerTransform == null)
                {
                    var pc = FindAnyObjectByType<PlayerController>();
                    if (pc != null) _cachedPlayerTransform = pc.transform;
                }

                bool pushedThisFrame = false;
                if (_cachedPlayerTransform != null)
                {
                    Vector3 currentChairPos = _homePosition + _pushedOffset;
                    Vector3 diff = currentChairPos - _cachedPlayerTransform.position;
                    if (Mathf.Abs(diff.y) < 1.3f)
                    {
                        diff.y = 0f;
                        float dist = diff.magnitude;
                        float contactRadius = 0.62f;
                        if (dist < contactRadius && dist > 0.001f)
                        {
                            pushedThisFrame = true;
                            Vector3 pushDir = diff / dist;
                            float penetration = (contactRadius - dist) / contactRadius;
                            float pushSpeed = isWheeled ? 2.6f : 1.1f;
                            _pushedOffset += pushDir * (penetration * pushSpeed * dt);
                            if (_pushedOffset.magnitude > maxPushRadius)
                            {
                                _pushedOffset = _pushedOffset.normalized * maxPushRadius;
                            }

                            float crossY = Vector3.Cross(chairFwd, pushDir).y;
                            _pushedYaw = Mathf.Clamp(_pushedYaw + crossY * 42f * dt, -22f, 22f);
                        }
                    }
                }

                if (!pushedThisFrame)
                {
                    // Retorno gradual y orgánico hacia su lugar si fue desplazada
                    _pushedOffset = Vector3.MoveTowards(_pushedOffset, Vector3.zero, dt * 0.18f);
                    _pushedYaw = Mathf.MoveTowards(_pushedYaw, 0f, dt * 12f);
                }

                transform.position = Vector3.Lerp(transform.position, _homePosition + _pushedOffset, dt * 10f);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    _homeRotation * Quaternion.Euler(0f, _pushedYaw, 0f),
                    dt * 10f);
            }
        }

        public static InteractiveChair FindNearestAvailableForPlayer(Vector3 worldPos, float maxRadius = 1.65f)
        {
            InteractiveChair best = null;
            float bestDistSq = maxRadius * maxRadius;

            for (int i = 0; i < AllChairs.Count; i++)
            {
                InteractiveChair c = AllChairs[i];
                if (c == null || !c.isActiveAndEnabled || !c.allowPlayerSit || c.isOccupied)
                    continue;

                Vector3 diff = c.transform.position - worldPos;
                if (Mathf.Abs(diff.y) > 1.35f)
                    continue;

                diff.y = 0f;
                float dSq = diff.sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = c;
                }
            }

            return best;
        }
    }
}
