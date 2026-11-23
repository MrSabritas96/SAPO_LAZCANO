using System;
using UnityEngine;

namespace SimuladorBloqueos.Data
{
    /// <summary>
    /// Modelo de datos que representa a un participante registrado para una sesión de simulación y evaluación.
    /// </summary>
    [Serializable]
    public class ParticipantData
    {
        public string fullName;
        public string participantCode;
        public string rank; // Cadete, Subteniente, Otro
        public string unitSection;
        public string instructorName;
        public string registrationDate;

        public ParticipantData()
        {
            fullName = string.Empty;
            participantCode = string.Empty;
            rank = "Subteniente";
            unitSection = string.Empty;
            instructorName = string.Empty;
            registrationDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }

        public ParticipantData(string name, string code, string rank, string unit, string instructor)
        {
            this.fullName = name?.Trim() ?? string.Empty;
            this.participantCode = code?.Trim().ToUpper() ?? string.Empty;
            this.rank = string.IsNullOrEmpty(rank) ? "Subteniente" : rank;
            this.unitSection = unit?.Trim() ?? string.Empty;
            this.instructorName = instructor?.Trim() ?? string.Empty;
            this.registrationDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }

        /// <summary>
        /// Valida que los campos obligatorios cumplan con los requisitos formales.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 3)
            {
                errorMessage = "El nombre completo del participante es obligatorio (mínimo 3 caracteres).";
                return false;
            }

            if (string.IsNullOrWhiteSpace(participantCode) || participantCode.Length < 2)
            {
                errorMessage = "El código de participante es obligatorio.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(unitSection))
            {
                errorMessage = "La sección o unidad de formación es obligatoria.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
