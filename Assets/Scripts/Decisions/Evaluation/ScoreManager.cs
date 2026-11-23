using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>
    /// Gestor de puntuación ponderada por criterio de evaluación (total 100%):
    /// - Integridad física de las personas: 30%
    /// - Legalidad, protocolos y derechos humanos: 25%
    /// - Administración de efectivos: 20%
    /// - Selección de equipamiento: 15%
    /// - Coordinación y respuesta: 10%
    /// Impide penalizaciones duplicadas por una misma consecuencia mediante registro único de IDs.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static readonly float[] CriterionWeights = new float[5]
        {
            0.30f, // PhysicalIntegrity (30%)
            0.25f, // Legality (25%)
            0.20f, // PersonnelAdmin (20%)
            0.15f, // Equipment (15%)
            0.10f  // Coordination (10%)
        };

        private readonly float[] _criterionSumScores = new float[5];
        private readonly float[] _criterionSampleWeights = new float[5];
        private readonly float[] _criterionDirectAdjustments = new float[5];
        private readonly HashSet<string> _appliedConsequenceIds = new HashSet<string>();

        public void ResetScores()
        {
            for (int i = 0; i < 5; i++)
            {
                _criterionSumScores[i] = 0f;
                _criterionSampleWeights[i] = 0f;
                _criterionDirectAdjustments[i] = 0f;
            }
            _appliedConsequenceIds.Clear();
        }

        public void RecordDecisionScore(DecisionData data, float obtainedScore)
        {
            if (data == null) return;
            int pIdx = (int)data.primaryCriterion;
            float primaryWeight = data.hasSecondaryCriterion ? (1f - data.secondaryShare) : 1f;

            _criterionSumScores[pIdx] += obtainedScore * primaryWeight;
            _criterionSampleWeights[pIdx] += primaryWeight;

            if (data.hasSecondaryCriterion)
            {
                int sIdx = (int)data.secondaryCriterion;
                float secWeight = data.secondaryShare;
                _criterionSumScores[sIdx] += obtainedScore * secWeight;
                _criterionSampleWeights[sIdx] += secWeight;
            }
        }

        /// <summary>
        /// Aplica un ajuste por consecuencia diferida o lesión atribuible una única vez por ID.
        /// Nunca aplica penalizaciones duplicadas por una misma consecuencia.
        /// </summary>
        public bool ApplyUniqueConsequenceAdjustment(string consequenceId, ScoreCriterion criterion, float deltaPoints)
        {
            if (string.IsNullOrEmpty(consequenceId)) return false;
            if (!_appliedConsequenceIds.Add(consequenceId))
                return false; // Ya fue aplicado anteriormente

            int idx = (int)criterion;
            _criterionDirectAdjustments[idx] += deltaPoints;
            return true;
        }

        public float GetCriterionScore(ScoreCriterion criterion)
        {
            int idx = (int)criterion;
            float baseAvg = (_criterionSampleWeights[idx] > 0.001f)
                ? (_criterionSumScores[idx] / _criterionSampleWeights[idx])
                : 100f;
            return Mathf.Clamp(baseAvg + _criterionDirectAdjustments[idx], 0f, 100f);
        }

        public float ComputeWeightedTotalScore()
        {
            float total = 0f;
            for (int i = 0; i < 5; i++)
            {
                float cScore = GetCriterionScore((ScoreCriterion)i);
                total += cScore * CriterionWeights[i];
            }
            return Mathf.Clamp(total, 0f, 100f);
        }

        public static string GetCriterionDisplayName(ScoreCriterion criterion)
        {
            switch (criterion)
            {
                case ScoreCriterion.PhysicalIntegrity: return "Integridad física de las personas (30%)";
                case ScoreCriterion.Legality: return "Legalidad, protocolos y DD.HH. (25%)";
                case ScoreCriterion.PersonnelAdmin: return "Administración de efectivos (20%)";
                case ScoreCriterion.Equipment: return "Selección de equipamiento (15%)";
                case ScoreCriterion.Coordination: return "Coordinación y respuesta (10%)";
            }
            return criterion.ToString();
        }
    }
}
