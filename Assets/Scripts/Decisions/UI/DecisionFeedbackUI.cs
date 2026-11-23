using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SimuladorBloqueos.Decisions.UI
{
    /// <summary>
    /// Ventana emergente de retroalimentación correctiva EXCLUSIVA para la Decisión 2 ("Cantidad de efectivos")
    /// cuando el jugador elige una alternativa incorrecta (5 o 30 efectivos).
    /// Muestra: cantidad seleccionada, puntaje obtenido, explicación de la limitación, TIP formativo
    /// e indicación de que 15 efectivos era la alternativa excelente.
    /// </summary>
    public class DecisionFeedbackUI : MonoBehaviour
    {
        public GameObject rootModal;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI headerScoreText;
        public TextMeshProUGUI explanationText;
        public TextMeshProUGUI tipText;
        public TextMeshProUGUI excellentRevealText;
        public Button continueButton;

        private Action _onContinue;

        private void Awake()
        {
            if (rootModal != null)
                rootModal.SetActive(false);
        }

        public void ShowCorrectiveModal(DecisionFeedbackManager.CorrectiveTipPayload payload, Action onContinue)
        {
            if (payload == null || !payload.shouldShowModal)
            {
                Hide();
                onContinue?.Invoke();
                return;
            }

            _onContinue = onContinue;

            if (titleText != null)
                titleText.text = "OBSERVACIÓN TÁCTICA — CANTIDAD DE EFECTIVOS";
            if (headerScoreText != null)
                headerScoreText.text = payload.headerLine;
            if (explanationText != null)
                explanationText.text = payload.limitationExplanation;
            if (tipText != null)
                tipText.text = "TIP: " + payload.educationalTip;
            if (excellentRevealText != null)
                excellentRevealText.text = payload.excellentRevealLine;

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(HandleContinueClicked);
            }

            if (rootModal != null)
                rootModal.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (rootModal != null)
                rootModal.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void HandleContinueClicked()
        {
            Hide();
            var cb = _onContinue;
            _onContinue = null;
            cb?.Invoke();
        }

        private void Update()
        {
            if (rootModal == null || !rootModal.activeInHierarchy) return;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                if (continueButton != null && continueButton.interactable)
                {
                    continueButton.onClick.Invoke();
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (continueButton != null && continueButton.interactable)
                {
                    continueButton.onClick.Invoke();
                }
            }
#endif
        }
    }
}
