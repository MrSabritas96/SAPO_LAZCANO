using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Locomoción física de un NPC:
    /// - CapsuleCollider + Rigidbody kinemático (el jugador y otros objetos no pueden atravesarlo).
    /// - Separación radial sólida NPC <-> NPC para evitar solapamientos.
    /// - Evasión física de mesas, escritorios y muros mediante detección tangencial de colisiones.
    /// </summary>
    public class NpcNavigator : MonoBehaviour
    {
        public static readonly List<NpcNavigator> AllNavigators = new List<NpcNavigator>(64);

        public CapsuleCollider Body { get; private set; }
        public bool Seated { get; private set; }
        public float radius = 0.28f;
        public int priority = 50;

        private Vector3 _target;
        private bool _moving;
        private float _speed;
        private Vector3 _currentVelocity;

        public Vector3 Velocity => _moving ? _currentVelocity : Vector3.zero;

        private void OnEnable()
        {
            if (!AllNavigators.Contains(this)) AllNavigators.Add(this);
        }

        private void OnDisable()
        {
            AllNavigators.Remove(this);
        }

        private void Awake()
        {
            Body = GetComponent<CapsuleCollider>();
            if (Body == null) Body = gameObject.AddComponent<CapsuleCollider>();
            Body.direction = 1;
            Body.height = 1.75f;
            Body.radius = radius;
            Body.center = new Vector3(0f, 0.9f, 0f);

            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            priority = 30 + Random.Range(0, 40);
        }

        public void SetSeated(bool seated)
        {
            Seated = seated;
            if (Body != null) Body.enabled = !seated;
            _moving = false;
            _currentVelocity = Vector3.zero;
        }

        /// <summary>Sale de la silla y vuelve a activar la navegación en la posición indicada.</summary>
        public void ReleaseAt(Vector3 pos, Quaternion rot)
        {
            Seated = false;
            if (Body != null) Body.enabled = true;
            _moving = false;
            _currentVelocity = Vector3.zero;
            transform.SetPositionAndRotation(pos, rot);
        }

        public void MoveTo(Vector3 dest, float speed)
        {
            if (Seated) return;
            _target = dest;
            _speed = speed;
            _moving = true;

            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            Vector3 myPos = transform.position;
            Vector3 toDest = dest - myPos;
            Vector3 flatDir = new Vector3(toDest.x, 0f, toDest.z);
            float flatDist = flatDir.magnitude;

            if (flatDist <= 0.02f)
            {
                transform.position = dest;
                _currentVelocity = Vector3.zero;
                return;
            }

            Vector3 moveDir = flatDir / flatDist;

            // Evasión tangencial de mesas, escritorios y obstáculos estáticos
            Vector3 rayOrigin = myPos + Vector3.up * 0.65f;
            if (Physics.SphereCast(rayOrigin, radius * 0.85f, moveDir, out RaycastHit hit, 0.55f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform) &&
                    hit.transform.GetComponentInParent<NpcNavigator>() == null &&
                    hit.transform.GetComponentInParent<PlayerController>() == null)
                {
                    Vector3 n = hit.normal;
                    n.y = 0f;
                    if (n.sqrMagnitude > 0.01f)
                    {
                        n.Normalize();
                        Vector3 slide = Vector3.ProjectOnPlane(moveDir, n);
                        if (slide.sqrMagnitude < 0.05f)
                            slide = Vector3.Cross(Vector3.up, n);
                        moveDir = (moveDir * 0.35f + slide.normalized * 0.65f).normalized;
                    }
                }
            }

            float step = Mathf.Min(flatDist, speed * dt);
            Vector3 nextPos = myPos + moveDir * step;
            nextPos.y = Mathf.MoveTowards(myPos.y, dest.y, speed * dt);

            // Separación radial entre NPCs para que jamás se atraviesen entre sí
            Vector3 push = ComputeSeparationOffset(nextPos, radius * 1.95f);
            nextPos.x += push.x * Mathf.Clamp01(dt * 5.0f);
            nextPos.z += push.z * Mathf.Clamp01(dt * 5.0f);

            _currentVelocity = (nextPos - myPos) / dt;
            transform.position = nextPos;
        }

        public Vector3 ComputeSeparationOffset(Vector3 myPos, float minSeparation)
        {
            Vector3 push = Vector3.zero;
            float minSq = minSeparation * minSeparation;
            for (int i = 0; i < AllNavigators.Count; i++)
            {
                var other = AllNavigators[i];
                if (other == null || other == this || other.Seated) continue;
                Vector3 diff = myPos - other.transform.position;
                if (Mathf.Abs(diff.y) > 1.4f) continue;
                diff.y = 0f;
                float dSq = diff.sqrMagnitude;
                if (dSq < minSq && dSq > 0.0001f)
                {
                    float d = Mathf.Sqrt(dSq);
                    push += (diff / d) * (minSeparation - d);
                }
            }
            return push;
        }

        public void Stop()
        {
            _moving = false;
            _currentVelocity = Vector3.zero;
        }

        public void Warp(Vector3 pos, Quaternion rot)
        {
            _moving = false;
            _currentVelocity = Vector3.zero;
            transform.SetPositionAndRotation(pos, rot);
        }

        public float FlatDistanceTo(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>Gira suavemente hacia la dirección de avance real del agente.</summary>
        public void FaceMovement(float dt, float turnSpeed = 10f)
        {
            Vector3 v = Velocity;
            v.y = 0f;
            if (v.sqrMagnitude > 0.04f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(v.normalized, Vector3.up), dt * turnSpeed);
        }

        public void FaceDirection(Vector3 dir, float dt, float turnSpeed = 8f)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), dt * turnSpeed);
        }
    }
}
