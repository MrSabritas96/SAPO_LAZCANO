using UnityEngine;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Muestra un panel de información y controles en pantalla durante la simulación.
    /// </summary>
    public class SimulationHUD : MonoBehaviour
    {
        private PlayerController _player;
        private ProceduralHumanoidAnimator _playerAnim;

        private void Start()
        {
            _player = Object.FindAnyObjectByType<PlayerController>();
            if (_player != null)
            {
                _playerAnim = _player.GetComponent<ProceduralHumanoidAnimator>();
            }
        }

        private void OnGUI()
        {
            // Panel semitransparente superior izquierdo
            GUI.Box(new Rect(15, 15, 340, 240), "");

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.2f, 0.9f, 1f) }
            };

            var textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            var highlightStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) }
            };

            GUILayout.BeginArea(new Rect(25, 20, 320, 230));
            GUILayout.Label("🎮 SIMULADOR DE BLOQUEOS", titleStyle);
            GUILayout.Space(4);

            if (SimuladorBloqueos.Data.GameSessionManager.Instance != null && SimuladorBloqueos.Data.GameSessionManager.Instance.hasActiveSession)
            {
                var p = SimuladorBloqueos.Data.GameSessionManager.Instance.activeParticipant;
                GUILayout.Label($"<b>Oficial:</b> {p.rank} {p.fullName}", highlightStyle);
                GUILayout.Label($"<b>Cód:</b> {p.participantCode} | <b>Unidad:</b> {p.unitSection}", textStyle);
                GUILayout.Space(4);
            }

            GUILayout.Label("• <b>W, A, S, D:</b> Moverse", textStyle);
            GUILayout.Label("• <b>Shift:</b> Correr (Sprint)", textStyle);
            GUILayout.Label("• <b>Ratón:</b> Rotar cámara orbital 3ra persona", textStyle);
            GUILayout.Label("• <b>Clic:</b> Bloquear ratón | <b>Esc:</b> Liberar", textStyle);
            GUILayout.Space(4);

            string state = "En reposo (Idle)";
            if (_playerAnim != null)
            {
                if (_playerAnim.normalizedSpeed > 0.5f) state = "Corriendo (Sprint)";
                else if (_playerAnim.normalizedSpeed > 0.05f) state = "Caminando (Walk)";
            }

            GUILayout.Label($"Estado: {state}", highlightStyle);
            GUILayout.Label("Entorno: 1 Jugador + 4 IA Autónomas", textStyle);

            if (GUILayout.Button("⮌ Menú Principal", GUILayout.Height(22)))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }

            GUILayout.EndArea();
        }
    }
}
