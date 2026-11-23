using System;
using System.Text;
using UnityEngine;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>
    /// Evaluación final del desempeño operativo al concluir las 13 decisiones.
    /// Aplica los pesos oficiales, verifica incumplimientos graves que limitan la calificación máxima
    /// y genera el desglose completo por criterios.
    /// </summary>
    public class EvaluationManager : MonoBehaviour
    {
        [Serializable]
        public class FinalEvaluationReport
        {
            public float rawWeightedScore;
            public float finalScore;
            public string classificationLabel;
            public string capReason;
            public float physicalIntegrityScore;
            public float legalityScore;
            public float personnelAdminScore;
            public float equipmentScore;
            public float coordinationScore;
            public int totalDecisionsTaken;
            public int criticalViolations;
            public int attributableInjuries;
            public string fullSummaryText;
        }

        public FinalEvaluationReport EvaluateFinalPerformance(
            SimulationState state,
            ScoreManager scoreManager,
            DecisionHistory history)
        {
            var report = new FinalEvaluationReport();
            if (scoreManager == null || state == null) return report;

            report.physicalIntegrityScore = scoreManager.GetCriterionScore(ScoreCriterion.PhysicalIntegrity);
            report.legalityScore = scoreManager.GetCriterionScore(ScoreCriterion.Legality);
            report.personnelAdminScore = scoreManager.GetCriterionScore(ScoreCriterion.PersonnelAdmin);
            report.equipmentScore = scoreManager.GetCriterionScore(ScoreCriterion.Equipment);
            report.coordinationScore = scoreManager.GetCriterionScore(ScoreCriterion.Coordination);

            report.rawWeightedScore = scoreManager.ComputeWeightedTotalScore();
            report.criticalViolations = state.criticalViolationCount;
            report.attributableInjuries = state.attributableInjuriesCount;
            report.totalDecisionsTaken = history != null ? history.Count : 0;

            float cappedScore = report.rawWeightedScore;
            string capNote = "";

            // Los incumplimientos graves de protocolo/legalidad limitan la clasificación máxima final
            if (report.criticalViolations >= 2)
            {
                if (cappedScore > 69f)
                {
                    cappedScore = 69f;
                    capNote = "Clasificación máxima limitada a DEFICIENTE por múltiples incumplimientos graves de protocolo y derechos humanos.";
                }
            }
            else if (report.criticalViolations == 1)
            {
                if (cappedScore > 79f)
                {
                    cappedScore = 79f;
                    capNote = "Clasificación máxima limitada a ACEPTABLE por registrar 1 incumplimiento grave de protocolo.";
                }
            }

            report.finalScore = Mathf.Round(cappedScore * 10f) / 10f;
            report.capReason = capNote;
            report.classificationLabel = GetClassificationForScore(report.finalScore);
            state.Set(SimVar.currentScore, report.finalScore);

            StringBuilder sb = new StringBuilder(1024);
            sb.AppendFormat("PUNTAJE FINAL PONDERADO: {0:0.0} / 100  —  CLASIFICACIÓN: {1}\n",
                report.finalScore, report.classificationLabel.ToUpper());
            if (!string.IsNullOrEmpty(report.capReason))
            {
                sb.AppendFormat("⚠ NOTA DE PROTOCOLO: {0}\n", report.capReason);
            }
            sb.AppendLine("\nDESGLOSE POR CRITERIOS DE EVALUACIÓN:");
            sb.AppendFormat(" • Integridad física de las personas (30%): {0:0.0} pts\n", report.physicalIntegrityScore);
            sb.AppendFormat(" • Legalidad, protocolos y DD.HH. (25%): {0:0.0} pts\n", report.legalityScore);
            sb.AppendFormat(" • Administración de efectivos (20%): {0:0.0} pts\n", report.personnelAdminScore);
            sb.AppendFormat(" • Selección de equipamiento (15%): {0:0.0} pts\n", report.equipmentScore);
            sb.AppendFormat(" • Coordinación y respuesta (10%): {0:0.0} pts\n", report.coordinationScore);
            sb.AppendFormat("\nIncumplimientos graves: {0}  |  Efectivos desplegados: {1} (Prot:{2}, Obs:{3}, Com:{4})",
                report.criticalViolations, state.selectedContingentCount,
                state.distProtection, state.distObservation, state.distCommunications);

            report.fullSummaryText = sb.ToString();
            return report;
        }

        /// <summary>
        /// Escala oficial de clasificación final:
        /// - 90–100: Excelente
        /// - 80–89: Muy bueno
        /// - 70–79: Aceptable
        /// - 60–69: Deficiente
        /// - Menos de 60: Insuficiente
        /// </summary>
        public static string GetClassificationForScore(float score)
        {
            if (score >= 90f) return "Excelente";
            if (score >= 80f) return "Muy bueno";
            if (score >= 70f) return "Aceptable";
            if (score >= 60f) return "Deficiente";
            return "Insuficiente";
        }
    }
}
