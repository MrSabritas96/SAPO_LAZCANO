using UnityEngine;
using TMPro;
using UnityEngine.UI;
using SimuladorBloqueos.Environment;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimuladorBloqueos.UI
{
    /// <summary>
    /// HUD de interacción por proximidad compatible con New Input System.
    /// Muestra aviso flotante al acercarse a puntos de interés y abre un modal informativo táctico al presionar [E].
    /// </summary>
    public class InteractionHUD : MonoBehaviour
    {
        private static InteractionHUD _instance;
        public static InteractionHUD Instance => _instance;

        [Header("Elementos UI")]
        public GameObject promptContainer;
        public TextMeshProUGUI promptText;
        public GameObject modalContainer;
        public TextMeshProUGUI modalTitleText;
        public TextMeshProUGUI modalBodyText;
        public Button modalCloseButton;

        private InteractableZone _currentActiveZone;
        private bool _isModalOpen = false;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (modalCloseButton != null)
            {
                modalCloseButton.onClick.AddListener(CloseModal);
            }

            if (promptContainer != null) promptContainer.SetActive(false);
            if (modalContainer != null) modalContainer.SetActive(false);
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            bool interactPressed = false;
            bool cancelPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                interactPressed = Keyboard.current.eKey.wasPressedThisFrame;
                cancelPressed = Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame;
            }
#else
            interactPressed = Input.GetKeyDown(KeyCode.E);
            cancelPressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space);
#endif

            if (_isModalOpen)
            {
                if (interactPressed || cancelPressed)
                {
                    CloseModal();
                }
            }
            else if (_currentActiveZone != null && _currentActiveZone.IsPlayerInside)
            {
                if (interactPressed)
                {
                    _currentActiveZone.TriggerInteraction();
                }
            }
        }

        public void ShowPrompt(InteractableZone zone)
        {
            _currentActiveZone = zone;
            if (_isModalOpen) return;

            if (promptContainer != null)
            {
                promptContainer.SetActive(true);
            }
            if (promptText != null)
            {
                promptText.text = $"<color=#00E5FF>[E]</color> <b>{zone.zoneTitle}</b> — <i>{zone.actionPrompt}</i>";
            }
        }

        public void HidePrompt(InteractableZone zone)
        {
            if (_currentActiveZone == zone)
            {
                _currentActiveZone = null;
                if (promptContainer != null)
                {
                    promptContainer.SetActive(false);
                }
            }
        }

        public void ShowDetailModal(string title, string content)
        {
            _isModalOpen = true;

            if (promptContainer != null) promptContainer.SetActive(false);

            if (modalContainer != null)
            {
                modalContainer.SetActive(true);
            }
            if (modalTitleText != null)
            {
                modalTitleText.text = title.ToUpper();
            }
            if (modalBodyText != null)
            {
                modalBodyText.text = content;
            }
        }

        public void CloseModal()
        {
            _isModalOpen = false;
            if (modalContainer != null)
            {
                modalContainer.SetActive(false);
            }

            if (_currentActiveZone != null && _currentActiveZone.IsPlayerInside)
            {
                ShowPrompt(_currentActiveZone);
            }
        }

        public bool IsModalOpen => _isModalOpen;
    }
}
