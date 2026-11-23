using SimuladorBloqueos.Data;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SimuladorBloqueos.UI
{
    /// <summary>
    /// Controlador del panel de registro del participante.
    /// Valida los campos antes de permitir el inicio de la simulación.
    /// </summary>
    public class ParticipantRegistrationController : MonoBehaviour
    {
        [Header("Campos del Formulario")]
        public TMP_InputField nameInput;
        public TMP_InputField codeInput;
        public TMP_Dropdown rankDropdown;
        public TMP_InputField unitInput;
        public TMP_InputField instructorInput;

        [Header("Feedback y Validación")]
        public TextMeshProUGUI validationMessageText;
        public Button startSimulationButton;
        public Button backButton;

        [Header("Navegación")]
        public MenuNavigationController navigationController;
        public string simulationSceneName = "BaseUTOP";

        private void Start()
        {
            if (startSimulationButton != null)
                startSimulationButton.onClick.AddListener(OnStartSimulationClicked);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);

            if (validationMessageText != null)
                validationMessageText.text = string.Empty;

            // Configurar opciones del Dropdown de Rango
            if (rankDropdown != null)
            {
                rankDropdown.ClearOptions();
                rankDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "Subteniente",
                    "Cadete",
                    "Otro"
                });
            }
        }

        private void OnStartSimulationClicked()
        {
            string name = nameInput != null ? nameInput.text : string.Empty;
            string code = codeInput != null ? codeInput.text : string.Empty;
            string rank = rankDropdown != null ? rankDropdown.options[rankDropdown.value].text : "Subteniente";
            string unit = unitInput != null ? unitInput.text : string.Empty;
            string instructor = instructorInput != null ? instructorInput.text : string.Empty;

            var participant = new ParticipantData(name, code, rank, unit, instructor);

            if (!participant.Validate(out string errorMsg))
            {
                if (validationMessageText != null)
                {
                    validationMessageText.text = $"⚠️ {errorMsg}";
                    validationMessageText.color = new Color(1f, 0.4f, 0.4f);
                }
                return;
            }

            if (validationMessageText != null)
            {
                validationMessageText.text = "✓ Registro validado. Iniciando entorno táctico...";
                validationMessageText.color = new Color(0.3f, 0.95f, 0.5f);
            }

            // Registrar en el administrador global de sesión
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.StartNewSession(participant);
            }

            // Cargar la escena de simulación (SampleScene)
            Debug.Log($"<color=green>[Registration] Cargando escena de simulación: {simulationSceneName}</color>");
            SceneManager.LoadScene(simulationSceneName);
        }

        private void OnBackClicked()
        {
            if (validationMessageText != null)
                validationMessageText.text = string.Empty;

            if (navigationController != null)
                navigationController.OpenMainMenu();
        }
    }
}
