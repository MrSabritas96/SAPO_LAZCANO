using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using SimuladorBloqueos.Decisions.UI;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>
    /// Orquestador central del Sistema de Toma de Decisiones Tácticas UTOP (13 etapas).
    /// Coordina el flujo entre DecisionSet, SimulationState, DecisionEvaluator, DecisionBranchManager,
    /// DecisionFeedbackManager, ScoreManager, EvaluationManager, DecisionHistory, ScenarioDirector y la UI.
    /// Garantiza además que mientras cualquier panel de decisión esté visible el cursor esté siempre libre
    /// para hacer clic directamente sin necesidad de presionar ESC y sin disparar golpes por error.
    /// </summary>
    public class DecisionManager : MonoBehaviour
    {
        public static DecisionManager Instance { get; private set; }

        /// <summary>
        /// Devuelve true si cualquiera de los paneles uGUI de decisiones (Opciones 2x2, Modal TIP Decisión 2 o Resultado)
        /// se encuentra abierto y visible en pantalla.
        /// </summary>
        public static bool IsAnyDecisionUIOpen
        {
            get
            {
                if (Instance == null) return false;
                bool panelOpen = Instance.decisionPanelUI != null &&
                                 Instance.decisionPanelUI.rootPanel != null &&
                                 Instance.decisionPanelUI.rootPanel.activeInHierarchy;
                bool feedbackOpen = Instance.decisionFeedbackUI != null &&
                                    Instance.decisionFeedbackUI.rootModal != null &&
                                    Instance.decisionFeedbackUI.rootModal.activeInHierarchy;
                bool resultOpen = Instance.decisionResultUI != null &&
                                  Instance.decisionResultUI.rootPanel != null &&
                                  Instance.decisionResultUI.rootPanel.activeInHierarchy;
                return panelOpen || feedbackOpen || resultOpen;
            }
        }

        [Header("Datos de Decisiones")]
        public DecisionSet decisionSet;
        public bool autoStartOnPlay = true;
        public float initialDelaySeconds = 1.0f;
        [Tooltip("Segundos para observar en 3D la acción automática de los NPCs tras pulsar Siguiente antes de abrir la próxima decisión.")]
        public float scenicActionObservationSeconds = 4.2f;

        [Header("Módulos del Sistema")]
        public SimulationState simulationState;
        public DecisionEvaluator evaluator;
        public DecisionBranchManager branchManager;
        public DecisionFeedbackManager feedbackManager;
        public DecisionHistory history;
        public ScoreManager scoreManager;
        public EvaluationManager evaluationManager;
        public ScenarioDirector scenarioDirector;

        [Header("Componentes de Interfaz (UI)")]
        public DecisionPanelUI decisionPanelUI;
        public DecisionFeedbackUI decisionFeedbackUI;
        public DecisionResultUI decisionResultUI;

        [Header("Estado de Ejecución")]
        [SerializeField] private int _currentDecisionIndex = -1;
        [SerializeField] private float _activeTopScoreDelta = 0f;
        [SerializeField] private bool _simulationCompleted = false;
        private bool _waitingForScenicTransition = false;
        private Coroutine _transitionCoroutine;

        public int CurrentDecisionIndex => _currentDecisionIndex;
        public bool IsSimulationCompleted => _simulationCompleted;
        public bool IsWaitingForScenicTransition => _waitingForScenicTransition;
        public DecisionData CurrentDecision =>
            (decisionSet != null && _currentDecisionIndex >= 0 && _currentDecisionIndex < decisionSet.decisions.Count)
                ? decisionSet.decisions[_currentDecisionIndex]
                : null;

        public event Action<DecisionData> OnDecisionPresented;
        public event Action<DecisionData, DecisionResult> OnDecisionResolved;
        public event Action<EvaluationManager.FinalEvaluationReport> OnSimulationFinished;

        private void Awake()
        {
            Instance = this;
            EnsureSubComponents();
        }

        public void EnsureSubComponents()
        {
            if (simulationState == null) simulationState = GetComponent<SimulationState>();
            if (simulationState == null) simulationState = gameObject.AddComponent<SimulationState>();

            if (evaluator == null) evaluator = GetComponent<DecisionEvaluator>();
            if (evaluator == null) evaluator = gameObject.AddComponent<DecisionEvaluator>();

            if (branchManager == null) branchManager = GetComponent<DecisionBranchManager>();
            if (branchManager == null) branchManager = gameObject.AddComponent<DecisionBranchManager>();

            if (feedbackManager == null) feedbackManager = GetComponent<DecisionFeedbackManager>();
            if (feedbackManager == null) feedbackManager = gameObject.AddComponent<DecisionFeedbackManager>();

            if (history == null) history = GetComponent<DecisionHistory>();
            if (history == null) history = gameObject.AddComponent<DecisionHistory>();

            if (scoreManager == null) scoreManager = GetComponent<ScoreManager>();
            if (scoreManager == null) scoreManager = gameObject.AddComponent<ScoreManager>();

            if (evaluationManager == null) evaluationManager = GetComponent<EvaluationManager>();
            if (evaluationManager == null) evaluationManager = gameObject.AddComponent<EvaluationManager>();

            if (scenarioDirector == null) scenarioDirector = FindAnyObjectByType<ScenarioDirector>();
            if (scenarioDirector == null) scenarioDirector = gameObject.AddComponent<ScenarioDirector>();

            if (decisionSet == null)
            {
                decisionSet = Resources.Load<DecisionSet>("Decisions/DecisionSet");
            }
        }

        private void Start()
        {
            EnsureCursorUnlockedForUI();
            if (autoStartOnPlay)
            {
                Invoke(nameof(StartSimulationFlow), initialDelaySeconds);
            }
        }

        private void Update()
        {
            // Mientras cualquier ventana de decisión o resultado esté activa, mantener el cursor 100% libre y visible
            // para que todos los botones respondan al primer clic sin tener que pulsar ESC.
            if (IsAnyDecisionUIOpen)
            {
                EnsureCursorUnlockedForUI();
            }

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                if (_waitingForScenicTransition)
                {
                    if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
                    _waitingForScenicTransition = false;
                    AdvanceToNextDecision();
                    return;
                }

                if (decisionPanelUI != null && !_simulationCompleted)
                {
                    decisionPanelUI.ToggleMinimized();
                    if (IsAnyDecisionUIOpen)
                        EnsureCursorUnlockedForUI();
                }
            }
#endif
        }

        public void StartSimulationFlow()
        {
            EnsureSubComponents();
            simulationState.ResetState();
            scoreManager.ResetScores();
            history.ClearHistory();
            _simulationCompleted = false;
            _waitingForScenicTransition = false;
            _currentDecisionIndex = -1;

            if (decisionFeedbackUI != null) decisionFeedbackUI.Hide();
            if (decisionResultUI != null) decisionResultUI.Hide();

            if (scenarioDirector != null)
            {
                scenarioDirector.InitializeScenarioStartState();
            }

            AdvanceToNextDecision();
        }

        public void AdvanceToNextDecision()
        {
            _waitingForScenicTransition = false;
            if (decisionSet == null || decisionSet.decisions == null || decisionSet.decisions.Count == 0)
            {
                Debug.LogWarning("[DecisionManager] No se encontró DecisionSet configurado.");
                return;
            }

            int nextIdx = branchManager.GetNextDecisionIndex(decisionSet, _currentDecisionIndex, simulationState);
            if (nextIdx < 0 || nextIdx >= decisionSet.decisions.Count)
            {
                FinishSimulation();
                return;
            }

            PresentDecisionAtIndex(nextIdx);
        }

        public void PresentDecisionAtIndex(int index)
        {
            _currentDecisionIndex = index;
            DecisionData data = decisionSet.decisions[index];
            if (data == null) return;

            if (!data.Validate(out string validationError))
            {
                Debug.LogWarning("[DecisionManager] " + validationError);
            }

            string dynamicCtx = branchManager.BuildDynamicContext(data, simulationState, out _activeTopScoreDelta);

            if (scenarioDirector != null)
            {
                scenarioDirector.TriggerPreDecisionEvent(data, simulationState);
            }

            EnsureCursorUnlockedForUI();

            if (decisionPanelUI != null)
            {
                decisionPanelUI.ShowDecision(data, dynamicCtx, simulationState, HandlePlayerConfirmedOption);
            }

            OnDecisionPresented?.Invoke(data);
        }

        /// <summary>
        /// Método público para resolver la decisión actual (desde la UI o pruebas automatizadas).
        /// </summary>
        public DecisionResult SubmitOptionChoice(
            DecisionOption chosenOption,
            bool useCustomDist = false,
            int prot = 0,
            int obs = 0,
            int comm = 0)
        {
            DecisionData data = CurrentDecision;
            if (data == null || chosenOption == null) return null;

            DecisionResult result = evaluator.EvaluateChoice(
                data, chosenOption, simulationState, scoreManager, _activeTopScoreDelta,
                useCustomDist, prot, obs, comm);

            history.Record(result);

            if (scenarioDirector != null)
            {
                scenarioDirector.ExecutePostDecisionReaction(data, result, simulationState);
            }

            OnDecisionResolved?.Invoke(data, result);
            return result;
        }

        private void HandlePlayerConfirmedOption(
            DecisionOption chosenOption,
            bool useCustomDist,
            int prot,
            int obs,
            int comm)
        {
            DecisionData data = CurrentDecision;
            DecisionResult result = SubmitOptionChoice(chosenOption, useCustomDist, prot, obs, comm);
            if (result == null) return;

            EnsureCursorUnlockedForUI();

            // Verificar si corresponde mostrar el modal correctivo exclusivo de la Decisión 2 (Cantidad de efectivos)
            var correctivePayload = feedbackManager.BuildFeedbackForResult(data, result);
            if (correctivePayload != null && correctivePayload.shouldShowModal && decisionFeedbackUI != null)
            {
                decisionFeedbackUI.ShowCorrectiveModal(correctivePayload, () =>
                {
                    ShowStepResultPanel(result);
                });
            }
            else
            {
                ShowStepResultPanel(result);
            }
        }

        private void ShowStepResultPanel(DecisionResult result)
        {
            EnsureCursorUnlockedForUI();
            if (decisionResultUI != null)
            {
                decisionResultUI.ShowStepResult(result, simulationState, () =>
                {
                    if (result != null && result.order < 13 && scenicActionObservationSeconds > 0.1f)
                    {
                        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
                        _transitionCoroutine = StartCoroutine(ScenicObservationBeforeNextDecisionCoroutine(scenicActionObservationSeconds));
                    }
                    else
                    {
                        AdvanceToNextDecision();
                    }
                });
            }
            else
            {
                AdvanceToNextDecision();
            }
        }

        private IEnumerator ScenicObservationBeforeNextDecisionCoroutine(float seconds)
        {
            _waitingForScenicTransition = true;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            _waitingForScenicTransition = false;
            AdvanceToNextDecision();
        }

        public EvaluationManager.FinalEvaluationReport FinishSimulation()
        {
            _simulationCompleted = true;
            _waitingForScenicTransition = false;
            if (decisionPanelUI != null) decisionPanelUI.Hide();
            if (decisionFeedbackUI != null) decisionFeedbackUI.Hide();

            var finalReport = evaluationManager.EvaluateFinalPerformance(simulationState, scoreManager, history);

            EnsureCursorUnlockedForUI();
            if (decisionResultUI != null)
            {
                decisionResultUI.ShowFinalEvaluationReport(finalReport, history, () =>
                {
                    // Al cerrar el informe final el usuario puede seguir explorando libremente la escena
                });
            }

            OnSimulationFinished?.Invoke(finalReport);
            return finalReport;
        }

        public static void EnsureCursorUnlockedForUI()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
