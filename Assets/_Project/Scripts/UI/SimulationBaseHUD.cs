using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SimuladorBloqueos.Data;

namespace SimuladorBloqueos.UI
{
    /// <summary>
    /// HUD principal de la escena BaseUTOP:
    /// - Datos del oficial activo (Grado, Nombre, Código y Unidad).
    /// - Indicador de ubicación táctica.
    /// - Controles y botón de retorno al Menú Principal.
    /// </summary>
    public class SimulationBaseHUD : MonoBehaviour
    {
        [Header("Datos del Participante")]
        public TextMeshProUGUI rankAndNameText;
        public TextMeshProUGUI codeAndUnitText;
        public TextMeshProUGUI locationText;

        [Header("Navegación")]
        public Button returnMenuButton;

        private void Start()
        {
            UpdateParticipantDisplay();

            if (returnMenuButton != null)
            {
                returnMenuButton.onClick.AddListener(OnReturnMenuClicked);
            }
        }

        public void UpdateParticipantDisplay()
        {
            string rankName = "Subt. Oficial en Prácticas";
            string codeUnit = "POL-2026-UTOP | Sección Instrucción";

            if (GameSessionManager.Instance != null && GameSessionManager.Instance.hasActiveSession && GameSessionManager.Instance.activeParticipant != null)
            {
                var p = GameSessionManager.Instance.activeParticipant;
                rankName = $"{p.rank} {p.fullName}";
                codeUnit = $"{p.participantCode} | {p.unitSection}";
            }

            if (rankAndNameText != null) rankAndNameText.text = rankName;
            if (codeAndUnitText != null) codeAndUnitText.text = codeUnit;
            if (locationText != null) locationText.text = "BASE DE OPERACIONES UTOP — SECTOR CENTRAL";
        }

        private void OnReturnMenuClicked()
        {
            // Liberar cursor al salir
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
