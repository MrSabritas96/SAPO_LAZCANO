using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SimuladorBloqueos.Decisions.UI
{
    /// <summary>
    /// Panel de resultado tras confirmar una decisión y panel de Evaluación Final al terminar las 13 etapas.
    /// No revela cuál era la alternativa excelente (eso es exclusivo de DecisionFeedbackUI en la Decisión 2).
    /// </summary>
    public class DecisionResultUI : MonoBehaviour
    {
        public GameObject rootPanel;
        public TextMeshProUGUI headerTitleText;
        public TextMeshProUGUI scoreBadgeText;
        public TextMeshProUGUI consequenceBodyText;
        public TextMeshProUGUI variablesSummaryText;
        public Button confirmNextButton;
        public TextMeshProUGUI confirmButtonLabel;

        private Action _onConfirmNext;

        private void Awake()
        {
            if (rootPanel != null)
                rootPanel.SetActive(false);
        }

        public void ShowStepResult(DecisionResult result, SimulationState state, Action onConfirmNext)
        {
            if (result == null) return;
            _onConfirmNext = onConfirmNext;

            if (headerTitleText != null)
            {
                headerTitleText.text = string.Format("RESULTADO OPERATIVO — ETAPA {0}/13: {1}",
                    result.order, result.stageName.ToUpper());
            }

            if (scoreBadgeText != null)
            {
                scoreBadgeText.text = string.Format("Puntaje en esta decisión: {0:0.#} / 100   |   Acumulado ponderado: {1:0.0} pts",
                    result.scoreObtained, state != null ? state.currentScore : result.scoreObtained);
            }

            if (consequenceBodyText != null)
            {
                StringBuilder sb = new StringBuilder(512);
                sb.AppendLine("<b>DECISIÓN EJECUTADA:</b>");
                sb.AppendLine(result.selectedOptionText);
                sb.AppendLine();
                sb.AppendLine("<b>CONSECUENCIA OPERATIVA INMEDIATA:</b>");
                sb.AppendLine(result.immediateConsequence);

                if (result.triggeredDeferredConsequences != null && result.triggeredDeferredConsequences.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("<color=#FFD166><b>CONSECUENCIAS ACUMULATIVAS DE DECISIONES PREVIAS ACTIVADAS:</b></color>");
                    for (int i = 0; i < result.triggeredDeferredConsequences.Count; i++)
                    {
                        sb.AppendFormat("• {0}\n", result.triggeredDeferredConsequences[i]);
                    }
                }
                consequenceBodyText.text = sb.ToString();
            }

            if (variablesSummaryText != null)
            {
                if (result.modifiedVariablesSummary != null && result.modifiedVariablesSummary.Count > 0)
                {
                    variablesSummaryText.text = "Efecto en estado táctico: " + string.Join("   |   ", result.modifiedVariablesSummary);
                }
                else
                {
                    variablesSummaryText.text = "Efecto en estado táctico: Estabilidad mantenida.";
                }
            }

            if (confirmButtonLabel != null)
            {
                confirmButtonLabel.text = result.order >= 13
                    ? "VER EVALUACIÓN FINAL DE LA SIMULACIÓN"
                    : "OBSERVAR ACCIÓN EN ESCENA Y CONTINUAR ->";
            }

            if (confirmNextButton != null)
            {
                confirmNextButton.onClick.RemoveAllListeners();
                confirmNextButton.onClick.AddListener(HandleConfirmClicked);
            }

            if (rootPanel != null)
                rootPanel.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void ShowFinalEvaluationReport(
            EvaluationManager.FinalEvaluationReport report,
            DecisionHistory history,
            Action onRestartOrClose)
        {
            _onConfirmNext = onRestartOrClose;

            if (headerTitleText != null)
            {
                headerTitleText.text = string.Format("EVALUACIÓN FINAL UTOP — CLASIFICACIÓN: {0}",
                    report != null ? report.classificationLabel.ToUpper() : "COMPLETADO");
            }

            if (scoreBadgeText != null && report != null)
            {
                scoreBadgeText.text = string.Format("PUNTAJE FINAL: {0:0.0} / 100   ({1})",
                    report.finalScore, report.classificationLabel.ToUpper());
            }

            if (consequenceBodyText != null && report != null)
            {
                StringBuilder sb = new StringBuilder(2048);
                sb.AppendLine(report.fullSummaryText);
                if (history != null && history.Count > 0)
                {
                    sb.AppendLine("\n<b>HISTORIAL CRONOLÓGICO DE DECISIONES TOMADAS:</b>");
                    sb.Append(history.BuildDetailedReportText());
                }
                consequenceBodyText.text = sb.ToString();
            }

            if (variablesSummaryText != null)
            {
                variablesSummaryText.text = "Simulación de las 13 etapas tácticas completada. Puedes seguir recorriendo la escena o reiniciar.";
            }

            if (confirmButtonLabel != null)
            {
                confirmButtonLabel.text = "CERRAR INFORME Y EXPLORAR ESCENA";
            }

            if (confirmNextButton != null)
            {
                confirmNextButton.onClick.RemoveAllListeners();
                confirmNextButton.onClick.AddListener(HandleConfirmClicked);
            }

            if (rootPanel != null)
                rootPanel.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (rootPanel != null)
                rootPanel.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void HandleConfirmClicked()
        {
            Hide();
            var cb = _onConfirmNext;
            _onConfirmNext = null;
            cb?.Invoke();
        }

        private void Update()
        {
            if (rootPanel == null || !rootPanel.activeInHierarchy) return;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                if (confirmNextButton != null && confirmNextButton.interactable)
                {
                    confirmNextButton.onClick.Invoke();
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (confirmNextButton != null && confirmNextButton.interactable)
                {
                    confirmNextButton.onClick.Invoke();
                }
            }
#endif
        }
    }
}
