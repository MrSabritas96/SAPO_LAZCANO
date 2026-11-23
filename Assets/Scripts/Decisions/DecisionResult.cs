using System;
using System.Collections.Generic;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>
    /// Registro inmutable de una decisión tomada por el jugador durante la simulación.
    /// Contiene los 8 campos obligatorios del historial más información de distribución si aplica.
    /// </summary>
    [Serializable]
    public class DecisionResult
    {
        public string decisionId;
        public int order;
        public string stageName;
        public string questionText;
        public string selectedOptionId;
        public string selectedOptionText;
        public OptionClass classification;
        public float scoreObtained;
        public List<string> modifiedVariablesSummary = new List<string>();
        public string immediateConsequence;
        public List<string> triggeredDeferredConsequences = new List<string>();
        public float gameTimeSeconds;
        public bool causedCriticalViolation;
        public string reactionTag;
        public int numericValue;

        // Datos adicionales si fue Decisión 3 (Distribución de efectivos)
        public bool hasCustomDistribution;
        public int distProtection;
        public int distObservation;
        public int distCommunications;

        public static string GetClassificationLabel(OptionClass c)
        {
            switch (c)
            {
                case OptionClass.Excellent: return "EXCELENTE";
                case OptionClass.Good: return "BUENA";
                case OptionClass.ModerateWrong: return "INCORRECTA MODERADA";
                case OptionClass.SeriousWrong: return "INCORRECTA GRAVE";
            }
            return c.ToString();
        }
    }
}
