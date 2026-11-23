using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SimuladorBloqueos.Decisions.UI
{
    /// <summary>
    /// Panel principal de toma de decisiones tácticas UTOP.
    /// - Muestra etapa, contexto dinámico, pregunta, indicador de puntaje y cuadrícula 2x2 de 4 opciones.
    /// - En la Decisión 3 ("Distribución de efectivos"), activa además el widget interactivo para repartir
    ///   exactamente los efectivos elegidos en la Decisión 2 entre:
    ///   1) Protección, 2) Observación y reporte, 3) Coordinación y comunicaciones,
    ///   validando automáticamente que la suma coincida con el contingente seleccionado.
    /// </summary>
    public class DecisionPanelUI : MonoBehaviour
    {
        [Header("Contenedores Principales")]
        public GameObject rootPanel;
        public TextMeshProUGUI stageHeaderText;
        public TextMeshProUGUI liveMetricsText;
        public TextMeshProUGUI contextBodyText;
        public TextMeshProUGUI questionText;
        public DecisionOptionUI[] optionButtons = new DecisionOptionUI[4];
        public Button minimizeToggleButton;
        public TextMeshProUGUI minimizeButtonLabel;

        [Header("Widget Exclusivo Decisión 3 — Distribución de Efectivos")]
        public GameObject distributionWidgetRoot;
        public TextMeshProUGUI distributionHeaderText;
        public TextMeshProUGUI protValueText;
        public TextMeshProUGUI obsValueText;
        public TextMeshProUGUI commValueText;
        public TextMeshProUGUI validationStatusText;
        public Button protMinusBtn;
        public Button protPlusBtn;
        public Button obsMinusBtn;
        public Button obsPlusBtn;
        public Button commMinusBtn;
        public Button commPlusBtn;
        public Button confirmCustomDistributionBtn;

        private DecisionData _currentDecision;
        private readonly List<DecisionOption> _displayedOptions = new List<DecisionOption>(4);
        private Action<DecisionOption, bool, int, int, int> _onOptionConfirmed;
        private int _requiredContingent = 15;
        private int _customProt = 7;
        private int _customObs = 4;
        private int _customComm = 4;
        private bool _isMinimized = false;

        private void Awake()
        {
            WireDistributionButtons();
            if (minimizeToggleButton != null)
            {
                minimizeToggleButton.onClick.RemoveAllListeners();
                minimizeToggleButton.onClick.AddListener(ToggleMinimized);
            }
        }

        private void WireDistributionButtons()
        {
            if (protMinusBtn != null) { protMinusBtn.onClick.RemoveAllListeners(); protMinusBtn.onClick.AddListener(() => AdjustDist(-1, 0, 0)); }
            if (protPlusBtn != null) { protPlusBtn.onClick.RemoveAllListeners(); protPlusBtn.onClick.AddListener(() => AdjustDist(+1, 0, 0)); }
            if (obsMinusBtn != null) { obsMinusBtn.onClick.RemoveAllListeners(); obsMinusBtn.onClick.AddListener(() => AdjustDist(0, -1, 0)); }
            if (obsPlusBtn != null) { obsPlusBtn.onClick.RemoveAllListeners(); obsPlusBtn.onClick.AddListener(() => AdjustDist(0, +1, 0)); }
            if (commMinusBtn != null) { commMinusBtn.onClick.RemoveAllListeners(); commMinusBtn.onClick.AddListener(() => AdjustDist(0, 0, -1)); }
            if (commPlusBtn != null) { commPlusBtn.onClick.RemoveAllListeners(); commPlusBtn.onClick.AddListener(() => AdjustDist(0, 0, +1)); }
            if (confirmCustomDistributionBtn != null)
            {
                confirmCustomDistributionBtn.onClick.RemoveAllListeners();
                confirmCustomDistributionBtn.onClick.AddListener(HandleConfirmCustomDistribution);
            }
        }

        public void ShowDecision(
            DecisionData decision,
            string dynamicContext,
            SimulationState state,
            Action<DecisionOption, bool, int, int, int> onConfirmed)
        {
            if (decision == null) return;
            _currentDecision = decision;
            _onOptionConfirmed = onConfirmed;
            _isMinimized = false;

            if (stageHeaderText != null)
            {
                stageHeaderText.text = string.Format("DECISIÓN TÁCTICA {0} DE 13 — {1}",
                    decision.order, decision.stageName.ToUpper());
            }

            UpdateLiveMetrics(state);

            if (contextBodyText != null)
                contextBodyText.text = dynamicContext;

            if (questionText != null)
                questionText.text = decision.question;

            // Preparar las 4 opciones en la cuadrícula 2x2 sin revelar su clasificación
            _displayedOptions.Clear();
            if (decision.options != null)
            {
                _displayedOptions.AddRange(decision.options);
                if (decision.shuffleOptions && decision.kind != DecisionKind.PersonnelCount)
                {
                    // Mezcla determinista por orden de etapa para variar la posición en la cuadrícula 2x2
                    int seed = decision.order * 37 + 11;
                    for (int i = _displayedOptions.Count - 1; i > 0; i--)
                    {
                        seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                        int swapIdx = seed % (i + 1);
                        var tmp = _displayedOptions[i];
                        _displayedOptions[i] = _displayedOptions[swapIdx];
                        _displayedOptions[swapIdx] = tmp;
                    }
                }
            }

            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] == null) continue;
                if (i < _displayedOptions.Count)
                {
                    optionButtons[i].gameObject.SetActive(true);
                    DecisionOption opt = _displayedOptions[i];

                    // Si es la Decisión 3, mostrar las cantidades exactas del contingente en cada tarjeta 2x2
                    if (decision.kind == DecisionKind.Distribution && state != null)
                    {
                        DecisionEvaluator.GetPresetDistribution(state.selectedContingentCount, opt.classification,
                            out int p, out int o, out int c);
                        DecisionOption displayCopy = new DecisionOption
                        {
                            id = opt.id,
                            classification = opt.classification,
                            score = opt.score,
                            immediateConsequence = opt.immediateConsequence,
                            variableEffects = opt.variableEffects,
                            deferredConsequences = opt.deferredConsequences,
                            setFlags = opt.setFlags,
                            criticalViolation = opt.criticalViolation,
                            reactionTag = opt.reactionTag,
                            numericValue = opt.numericValue,
                            text = string.Format("{0}\n[Protección: {1} | Observación: {2} | Comunicaciones: {3} — Total: {4}]",
                                opt.text, p, o, c, state.selectedContingentCount)
                        };
                        optionButtons[i].Bind(i, displayCopy, OnGridOptionClicked);
                    }
                    else
                    {
                        optionButtons[i].Bind(i, opt, OnGridOptionClicked);
                    }
                }
                else
                {
                    optionButtons[i].gameObject.SetActive(false);
                }
            }

            // Configurar el widget interactivo de distribución solo en la Decisión 3
            bool isDistribution = (decision.kind == DecisionKind.Distribution);
            if (distributionWidgetRoot != null)
            {
                distributionWidgetRoot.SetActive(isDistribution);
            }

            if (isDistribution && state != null)
            {
                _requiredContingent = Mathf.Max(3, state.selectedContingentCount);
                DecisionEvaluator.GetPresetDistribution(_requiredContingent, OptionClass.Excellent,
                    out _customProt, out _customObs, out _customComm);
                RefreshDistributionUI();
            }

            if (rootPanel != null)
                rootPanel.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void UpdateLiveMetrics(SimulationState state)
        {
            if (liveMetricsText == null || state == null) return;
            liveMetricsText.text = string.Format(
                "Puntaje: {0:0.0} pts  |  Efectivos: {1}  |  Protección: {2:0}%  |  Com.: {3:0}%  |  Obs.: {4:0}%  |  Seguridad: {5:0}%",
                state.currentScore, state.selectedContingentCount,
                state.protectionLevel, state.communicationLevel, state.observationCapacity, state.publicSafety);
        }

        private void AdjustDist(int dProt, int dObs, int dComm)
        {
            _customProt = Mathf.Clamp(_customProt + dProt, 0, _requiredContingent);
            _customObs = Mathf.Clamp(_customObs + dObs, 0, _requiredContingent);
            _customComm = Mathf.Clamp(_customComm + dComm, 0, _requiredContingent);
            RefreshDistributionUI();
        }

        private void RefreshDistributionUI()
        {
            int sum = _customProt + _customObs + _customComm;
            bool validSum = (sum == _requiredContingent);

            if (distributionHeaderText != null)
            {
                distributionHeaderText.text = string.Format(
                    "DISTRIBUCIÓN MANUAL DEL CONTINGENTE ({0} EFECTIVOS SELECCIONADOS EN DECISIÓN 2):",
                    _requiredContingent);
            }
            if (protValueText != null) protValueText.text = string.Format("Protección: {0}", _customProt);
            if (obsValueText != null) obsValueText.text = string.Format("Observación: {0}", _customObs);
            if (commValueText != null) commValueText.text = string.Format("Comunicaciones: {0}", _customComm);

            if (validationStatusText != null)
            {
                if (validSum)
                {
                    validationStatusText.text = string.Format(
                        "<color=#52E584>✔ Suma válida ({0}/{1} efectivos asignados). Puedes confirmar tu distribución personalizada o elegir una de las 4 opciones superiores.</color>",
                        sum, _requiredContingent);
                }
                else
                {
                    int diff = _requiredContingent - sum;
                    string msg = diff > 0
                        ? string.Format("Faltan asignar {0} efectivo(s)", diff)
                        : string.Format("Exceso de {0} efectivo(s)", -diff);
                    validationStatusText.text = string.Format(
                        "<color=#FF6B6B>⚠ Suma actual: {0}/{1} ({2}). La suma debe coincidir exactamente con {1}.</color>",
                        sum, _requiredContingent, msg);
                }
            }

            if (confirmCustomDistributionBtn != null)
            {
                confirmCustomDistributionBtn.interactable = validSum;
            }
        }

        private void HandleConfirmCustomDistribution()
        {
            if (_currentDecision == null || (_customProt + _customObs + _customComm) != _requiredContingent)
                return;

            OptionClass classified = DecisionEvaluator.ClassifyDistribution(
                _requiredContingent, _customProt, _customObs, _customComm);

            DecisionOption matchedOption = _currentDecision.options[0];
            for (int i = 0; i < _currentDecision.options.Count; i++)
            {
                if (_currentDecision.options[i].classification == classified)
                {
                    matchedOption = _currentDecision.options[i];
                    break;
                }
            }

            Hide();
            _onOptionConfirmed?.Invoke(matchedOption, true, _customProt, _customObs, _customComm);
        }

        private void OnGridOptionClicked(int index, DecisionOption clickedDisplayOption)
        {
            if (_currentDecision == null) return;

            // Recuperar la opción original en DecisionData por su ID
            DecisionOption realOption = clickedDisplayOption;
            for (int i = 0; i < _currentDecision.options.Count; i++)
            {
                if (_currentDecision.options[i].id == clickedDisplayOption.id)
                {
                    realOption = _currentDecision.options[i];
                    break;
                }
            }

            if (_currentDecision.kind == DecisionKind.Distribution)
            {
                DecisionEvaluator.GetPresetDistribution(_requiredContingent, realOption.classification,
                    out int p, out int o, out int c);
                Hide();
                _onOptionConfirmed?.Invoke(realOption, true, p, o, c);
            }
            else
            {
                Hide();
                _onOptionConfirmed?.Invoke(realOption, false, 0, 0, 0);
            }
        }

        public void ToggleMinimized()
        {
            _isMinimized = !_isMinimized;
            if (rootPanel != null)
                rootPanel.SetActive(!_isMinimized);
            if (minimizeButtonLabel != null)
                minimizeButtonLabel.text = _isMinimized ? "ABRIR PANEL DE DECISIÓN [T]" : "OCULTAR PANEL [T] (EXPLORAR 3D)";
        }

        public void SetMinimized(bool minimized)
        {
            _isMinimized = minimized;
            if (rootPanel != null)
                rootPanel.SetActive(!_isMinimized);
            if (minimizeButtonLabel != null)
                minimizeButtonLabel.text = _isMinimized ? "ABRIR PANEL DE DECISIÓN [T]" : "OCULTAR PANEL [T] (EXPLORAR 3D)";
        }

        public void Hide()
        {
            if (rootPanel != null)
                rootPanel.SetActive(false);
        }

        private void Update()
        {
            if (rootPanel == null || !rootPanel.activeInHierarchy || _isMinimized) return;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            {
                TriggerOptionByIndex(0);
            }
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            {
                TriggerOptionByIndex(1);
            }
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            {
                TriggerOptionByIndex(2);
            }
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
            {
                TriggerOptionByIndex(3);
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                TriggerOptionByIndex(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                TriggerOptionByIndex(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                TriggerOptionByIndex(2);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                TriggerOptionByIndex(3);
            }
#endif
        }

        private void TriggerOptionByIndex(int index)
        {
            if (optionButtons != null && index >= 0 && index < optionButtons.Length)
            {
                var optBtn = optionButtons[index];
                if (optBtn != null && optBtn.gameObject.activeInHierarchy && optBtn.button != null && optBtn.button.interactable)
                {
                    optBtn.button.onClick.Invoke();
                }
            }
        }
    }
}
