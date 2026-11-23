using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos.UI
{
    /// <summary>
    /// Gestiona la navegación y transiciones suaves entre paneles del menú principal.
    /// Utiliza CanvasGroup para desvanecimientos fluidos sin operaciones pesadas por frame.
    /// </summary>
    public class MenuNavigationController : MonoBehaviour
    {
        [Header("Paneles del Menú")]
        public CanvasGroup mainMenuPanel;
        public CanvasGroup registrationPanel;
        public CanvasGroup historyPanel;
        public CanvasGroup statisticsPanel;
        public CanvasGroup settingsPanel;

        [Header("Configuración de Transición")]
        public float transitionDuration = 0.22f;

        private CanvasGroup _currentActivePanel;
        private Coroutine _transitionCoroutine;

        private void Awake()
        {
            // Ocultar todos los subpaneles al inicio
            SetPanelInstant(registrationPanel, false);
            SetPanelInstant(historyPanel, false);
            SetPanelInstant(statisticsPanel, false);
            SetPanelInstant(settingsPanel, false);

            // Mostrar el panel principal
            SetPanelInstant(mainMenuPanel, true);
            _currentActivePanel = mainMenuPanel;
        }

        public void OpenMainMenu()
        {
            SwitchToPanel(mainMenuPanel);
        }

        public void OpenRegistration()
        {
            SwitchToPanel(registrationPanel);
        }

        public void OpenHistory()
        {
            SwitchToPanel(historyPanel);
        }

        public void OpenStatistics()
        {
            SwitchToPanel(statisticsPanel);
        }

        public void OpenSettings()
        {
            SwitchToPanel(settingsPanel);
        }

        public void SwitchToPanel(CanvasGroup targetPanel)
        {
            if (targetPanel == null || targetPanel == _currentActivePanel) return;

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                if (_currentActivePanel != null)
                {
                    SetPanelInstant(_currentActivePanel, false);
                }
            }

            _transitionCoroutine = StartCoroutine(TransitionRoutine(_currentActivePanel, targetPanel));
        }

        private IEnumerator TransitionRoutine(CanvasGroup fromPanel, CanvasGroup toPanel)
        {
            float elapsed = 0f;

            // Desactivar interacción inmediatamente para evitar doble clic
            if (fromPanel != null)
            {
                fromPanel.interactable = false;
                fromPanel.blocksRaycasts = false;
            }
            if (toPanel != null)
            {
                toPanel.gameObject.SetActive(true);
                toPanel.interactable = false;
                toPanel.blocksRaycasts = false;
            }

            // Desvanecimiento cruzado
            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);

                if (fromPanel != null) fromPanel.alpha = 1f - t;
                if (toPanel != null) toPanel.alpha = t;

                yield return null;
            }

            if (fromPanel != null)
            {
                fromPanel.alpha = 0f;
                fromPanel.gameObject.SetActive(false);
            }

            if (toPanel != null)
            {
                toPanel.alpha = 1f;
                toPanel.interactable = true;
                toPanel.blocksRaycasts = true;
                _currentActivePanel = toPanel;
            }

            _transitionCoroutine = null;
        }

        private void SetPanelInstant(CanvasGroup panel, bool visible)
        {
            if (panel == null) return;
            panel.alpha = visible ? 1f : 0f;
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
            panel.gameObject.SetActive(visible);
        }
    }
}
