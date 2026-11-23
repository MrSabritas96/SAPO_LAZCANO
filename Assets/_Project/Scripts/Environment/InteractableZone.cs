using UnityEngine;
using UnityEngine.Events;
using SimuladorBloqueos.UI;

namespace SimuladorBloqueos.Environment
{
    /// <summary>
    /// Componente de punto de interacción por proximidad.
    /// Emite eventos cuando el jugador entra o sale de la zona y cuando presiona la tecla de interactuar.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class InteractableZone : MonoBehaviour
    {
        [Header("Datos de la Zona")]
        public string zoneTitle = "Punto de Interés";
        [TextArea(2, 5)]
        public string interactionDescription = "Descripción informativa del área.";
        public string actionPrompt = "Presiona [E] para interactuar";
        public float interactionRadius = 2.5f;

        [Header("Eventos")]
        public UnityEvent onInteract;

        private bool _isPlayerInside = false;
        private SphereCollider _triggerCollider;

        private void Reset()
        {
            SetupCollider();
        }

        private void Awake()
        {
            SetupCollider();
        }

        public void SetupCollider()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<SphereCollider>();
                if (_triggerCollider == null)
                {
                    _triggerCollider = gameObject.AddComponent<SphereCollider>();
                }
            }
            _triggerCollider.isTrigger = true;
            _triggerCollider.radius = interactionRadius;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
            {
                _isPlayerInside = true;
                InteractionHUD.Instance?.ShowPrompt(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
            {
                _isPlayerInside = false;
                InteractionHUD.Instance?.HidePrompt(this);
            }
        }

        public void TriggerInteraction()
        {
            onInteract?.Invoke();
            InteractionHUD.Instance?.ShowDetailModal(zoneTitle, interactionDescription);
        }

        public bool IsPlayerInside => _isPlayerInside;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
