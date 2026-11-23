using UnityEngine;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Controlador de Inteligencia Artificial para los personajes no jugadores:
    /// - Navegación autónoma y deambulación natural por el escenario.
    /// - Estados de espera (idle), caminata y trote/carrera.
    /// - Rotación fluida y animación procedural coordinada.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(ProceduralHumanoidAnimator))]
    public class AIController : MonoBehaviour
    {
        public enum AIState { Idle, Walking, Jogging }

        [Header("Comportamiento")]
        public AIState currentState = AIState.Idle;
        public float wanderRadius = 18f;
        public float walkSpeed = 2.2f;
        public float jogSpeed = 4.8f;
        public float rotationSpeed = 6f;

        [Header("Tiempos de Decisión")]
        public float minWaitTime = 1.5f;
        public float maxWaitTime = 4.0f;

        private CharacterController _characterController;
        private ProceduralHumanoidAnimator _animator;
        private Vector3 _targetDestination;
        private float _stateTimer = 0f;
        private float _currentSpeed = 0f;
        private float _verticalVelocity = 0f;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _animator = GetComponent<ProceduralHumanoidAnimator>();
        }

        private void Start()
        {
            PickNewStateAndDestination();
        }

        private void Update()
        {
            _stateTimer -= Time.deltaTime;

            Vector3 moveDirection = Vector3.zero;

            switch (currentState)
            {
                case AIState.Idle:
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, 6f * Time.deltaTime);
                    if (_stateTimer <= 0)
                    {
                        PickNewStateAndDestination();
                    }
                    break;

                case AIState.Walking:
                case AIState.Jogging:
                    float targetSpeed = (currentState == AIState.Jogging) ? jogSpeed : walkSpeed;
                    Vector3 toTarget = _targetDestination - transform.position;
                    toTarget.y = 0;

                    if (toTarget.magnitude > 0.6f && _stateTimer > 0)
                    {
                        moveDirection = toTarget.normalized;
                        Quaternion targetRot = Quaternion.LookRotation(moveDirection, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, 4f * Time.deltaTime);
                    }
                    else
                    {
                        // Llegó a su destino o se agotó el tiempo
                        currentState = AIState.Idle;
                        _stateTimer = Random.Range(minWaitTime, maxWaitTime);
                    }
                    break;
            }

            // Gravedad
            if (_characterController.isGrounded)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += -20f * Time.deltaTime;
            }

            Vector3 finalMove = (moveDirection * _currentSpeed) + (Vector3.up * _verticalVelocity);
            _characterController.Move(finalMove * Time.deltaTime);

            // Transmitir velocidad al animador procedural
            float speedRatio = 0f;
            if (_currentSpeed > 0.05f)
            {
                if (currentState == AIState.Jogging)
                {
                    speedRatio = Mathf.Lerp(0.5f, 0.9f, _currentSpeed / jogSpeed);
                }
                else
                {
                    speedRatio = Mathf.Lerp(0.1f, 0.45f, _currentSpeed / walkSpeed);
                }
            }

            _animator.normalizedSpeed = Mathf.Lerp(_animator.normalizedSpeed, speedRatio, Time.deltaTime * 8f);
            _animator.isGrounded = _characterController.isGrounded;
        }

        private void PickNewStateAndDestination()
        {
            // Decidir aleatoriamente si camina, trota o se queda quieto
            float roll = Random.value;
            if (roll < 0.25f)
            {
                currentState = AIState.Idle;
                _stateTimer = Random.Range(minWaitTime, maxWaitTime);
            }
            else
            {
                currentState = (roll > 0.75f) ? AIState.Jogging : AIState.Walking;
                _stateTimer = Random.Range(5f, 10f);

                // Escoger un punto dentro del radio permitido
                Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
                _targetDestination = new Vector3(randomCircle.x, 0, randomCircle.y);
            }
        }
    }
}
