using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos.Data
{
    /// <summary>
    /// Administrador global de sesión. Persiste entre escenas y almacena los datos
    /// del participante activo y el registro de simulaciones.
    /// </summary>
    public class GameSessionManager : MonoBehaviour
    {
        private static GameSessionManager _instance;
        public static GameSessionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<GameSessionManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Sesión Activa")]
        public ParticipantData activeParticipant;
        public bool hasActiveSession = false;

        [Header("Historial de Evaluaciones")]
        public List<ParticipantData> sessionHistory = new List<ParticipantData>();

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSampleHistory();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void InitializeSampleHistory()
        {
            // Datos de demostración técnica para la visualización del panel de Historial
            sessionHistory.Add(new ParticipantData("Subt. Carlos Mamani Ramos", "POL-2026-014", "Subteniente", "UTOP Sección Alpha", "My. V. Mendoza"));
            sessionHistory.Add(new ParticipantData("Cdt. Andrea Flores Choque", "POL-2026-089", "Cadete", "4to Año - Curso Táctico", "Cap. R. Vargas"));
            sessionHistory.Add(new ParticipantData("Subt. Jorge Gutiérrez Silva", "POL-2026-031", "Subteniente", "Distrito Policial 2", "My. V. Mendoza"));
        }

        /// <summary>
        /// Inicia una nueva sesión de simulación con el participante registrado.
        /// </summary>
        public void StartNewSession(ParticipantData participant)
        {
            activeParticipant = participant;
            hasActiveSession = true;
            sessionHistory.Insert(0, participant);
            Debug.Log($"<color=cyan>[GameSessionManager] Nueva sesión iniciada para: {participant.rank} {participant.fullName} ({participant.participantCode})</color>");
        }

        /// <summary>
        /// Determina si es posible continuar una sesión previa.
        /// </summary>
        public bool CanContinueSession()
        {
            return hasActiveSession && activeParticipant != null && !string.IsNullOrEmpty(activeParticipant.fullName);
        }
    }
}
