using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimuladorBloqueos.UI
{
    /// <summary>
    /// Controlador para paneles informativos (Historial y Estadísticas de Evaluación).
    /// </summary>
    public class InfoPanelController : MonoBehaviour
    {
        public Button backButton;
        public MenuNavigationController navigationController;

        private void Start()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackClicked);
            }
        }

        private void OnBackClicked()
        {
            if (navigationController != null)
                navigationController.OpenMainMenu();
        }
    }
}
