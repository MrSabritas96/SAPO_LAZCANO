using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>
    /// Evalúa la alternativa elegida por el jugador (y la distribución numérica de efectivos en la Decisión 3),
    /// aplica efectos sobre SimulationState, encola consecuencias diferidas y actualiza ScoreManager.
    /// </summary>
    public class DecisionEvaluator : MonoBehaviour
    {
        public DecisionResult EvaluateChoice(
            DecisionData decision,
            DecisionOption chosenOption,
            SimulationState state,
            ScoreManager scoreManager,
            float activeTopScoreModifier,
            bool useCustomDistribution = false,
            int customProt = 0,
            int customObs = 0,
            int customComm = 0)
        {
            if (decision == null || chosenOption == null || state == null) return null;

            DecisionOption effectiveOption = chosenOption;
            var result = new DecisionResult
            {
                decisionId = decision.decisionId,
                order = decision.order,
                stageName = decision.stageName,
                questionText = decision.question,
                selectedOptionId = chosenOption.id,
                selectedOptionText = chosenOption.text,
                classification = chosenOption.classification,
                immediateConsequence = chosenOption.immediateConsequence,
                gameTimeSeconds = Time.timeSinceLevelLoad,
                causedCriticalViolation = chosenOption.criticalViolation,
                reactionTag = chosenOption.reactionTag,
                numericValue = chosenOption.numericValue
            };

            // Caso especial Decisión 2: guardar cantidad de efectivos seleccionada (5, 15, 20 o 30)
            if (decision.kind == DecisionKind.PersonnelCount && chosenOption.numericValue > 0)
            {
                state.selectedContingentCount = chosenOption.numericValue;
                GetPresetDistribution(state.selectedContingentCount, OptionClass.Excellent,
                    out state.distProtection, out state.distObservation, out state.distCommunications);
            }

            // Caso especial Decisión 3: Distribución de efectivos en 3 responsabilidades
            if (decision.kind == DecisionKind.Distribution)
            {
                int totalRequired = Mathf.Max(1, state.selectedContingentCount);
                int p = customProt;
                int o = customObs;
                int c = customComm;

                if (!useCustomDistribution || (p + o + c) != totalRequired)
                {
                    GetPresetDistribution(totalRequired, chosenOption.classification, out p, out o, out c);
                }
                else
                {
                    // Clasificar automáticamente según la distribución personalizada ingresada por el jugador
                    OptionClass autoClass = ClassifyDistribution(totalRequired, p, o, c);
                    DecisionOption matched = FindOptionByClass(decision, autoClass);
                    if (matched != null)
                    {
                        effectiveOption = matched;
                        result.classification = matched.classification;
                        result.immediateConsequence = matched.immediateConsequence;
                        result.causedCriticalViolation = matched.criticalViolation;
                        result.reactionTag = matched.reactionTag;
                    }
                }

                state.distProtection = p;
                state.distObservation = o;
                state.distCommunications = c;

                result.hasCustomDistribution = true;
                result.distProtection = p;
                result.distObservation = o;
                result.distCommunications = c;
                result.selectedOptionText = string.Format(
                    "{0} [Total {1}: Protección={2}, Observación={3}, Comunicaciones={4}]",
                    effectiveOption.text, totalRequired, p, o, c);
            }

            // Calcular puntaje obtenido (con modificador contextual si aplica a opciones altas)
            float rawScore = effectiveOption.score;
            if ((effectiveOption.classification == OptionClass.Excellent || effectiveOption.classification == OptionClass.Good)
                && Mathf.Abs(activeTopScoreModifier) > 0.01f)
            {
                rawScore = Mathf.Clamp(rawScore + activeTopScoreModifier, 0f, 100f);
            }
            result.scoreObtained = rawScore;

            // Aplicar efectos inmediatos sobre las variables centrales
            if (effectiveOption.variableEffects != null)
            {
                for (int i = 0; i < effectiveOption.variableEffects.Count; i++)
                {
                    var eff = effectiveOption.variableEffects[i];
                    float applied = state.ApplyDelta(eff.variable, eff.delta);
                    if (Mathf.Abs(applied) > 0.01f)
                    {
                        string sign = applied >= 0f ? "+" : "";
                        result.modifiedVariablesSummary.Add(string.Format("{0}: {1}{2:0}",
                            SimulationState.GetVariableDisplayName(eff.variable), sign, applied));
                    }
                }
            }

            // Activar banderas de estado
            if (effectiveOption.setFlags != null)
            {
                for (int i = 0; i < effectiveOption.setFlags.Count; i++)
                {
                    state.SetFlag(effectiveOption.setFlags[i]);
                }
            }

            // Registrar incumplimiento grave si corresponde
            if (effectiveOption.criticalViolation)
            {
                state.ApplyDelta(SimVar.criticalViolationCount, 1f);
                result.modifiedVariablesSummary.Add("Incumplimiento grave de protocolo: +1");
            }

            // Encolar consecuencias diferidas
            if (effectiveOption.deferredConsequences != null)
            {
                for (int i = 0; i < effectiveOption.deferredConsequences.Count; i++)
                {
                    state.EnqueueDeferred(decision.decisionId, effectiveOption.deferredConsequences[i]);
                }
            }

            // Avanzar turno de consecuencias diferidas previas y aplicar sus ajustes únicos
            List<DeferredConsequence> firedDeferred = state.AdvanceDeferredTurn();
            for (int i = 0; i < firedDeferred.Count; i++)
            {
                var dc = firedDeferred[i];
                if (!string.IsNullOrEmpty(dc.description))
                {
                    result.triggeredDeferredConsequences.Add(dc.description);
                }
                if (scoreManager != null && Mathf.Abs(dc.scoreDelta) > 0.01f)
                {
                    scoreManager.ApplyUniqueConsequenceAdjustment(dc.id, dc.criterion, dc.scoreDelta);
                }
            }

            // Registrar puntaje en el ScoreManager y actualizar progreso y puntaje global
            if (scoreManager != null)
            {
                scoreManager.RecordDecisionScore(decision, result.scoreObtained);
                state.Set(SimVar.currentScore, scoreManager.ComputeWeightedTotalScore());
            }

            float progressPct = Mathf.Clamp((decision.order / 13f) * 100f, 0f, 100f);
            state.Set(SimVar.missionProgress, progressPct);

            return result;
        }

        public static OptionClass ClassifyDistribution(int total, int prot, int obs, int comm)
        {
            if (prot <= 0 || obs <= 0 || comm <= 0)
                return OptionClass.SeriousWrong; // Deja un área esencial en 0

            float protRatio = (float)prot / total;
            float obsRatio = (float)obs / total;
            float commRatio = (float)comm / total;

            if (protRatio > 0.72f || obsRatio > 0.65f || commRatio > 0.65f || protRatio < 0.22f)
                return OptionClass.ModerateWrong; // Concentración excesiva en una sola área

            if (protRatio >= 0.38f && protRatio <= 0.56f && obsRatio >= 0.18f && commRatio >= 0.18f)
                return OptionClass.Excellent; // Distribución equilibrada entre las 3 responsabilidades

            return OptionClass.Good; // Funcional con desbalance menor
        }

        public static void GetPresetDistribution(int total, OptionClass cls, out int prot, out int obs, out int comm)
        {
            total = Mathf.Max(3, total);
            switch (cls)
            {
                case OptionClass.Excellent:
                    // Equilibrada (~46% protección, ~27% observación, ~27% comunicaciones)
                    prot = Mathf.Max(1, Mathf.RoundToInt(total * 0.46f));
                    obs = Mathf.Max(1, Mathf.RoundToInt((total - prot) * 0.5f));
                    comm = Mathf.Max(1, total - prot - obs);
                    break;

                case OptionClass.Good:
                    // Funcional con ligero desbalance (~55% protección, ~30% observación, ~15% comunicaciones)
                    prot = Mathf.Max(1, Mathf.RoundToInt(total * 0.56f));
                    obs = Mathf.Max(1, Mathf.RoundToInt((total - prot) * 0.65f));
                    comm = Mathf.Max(1, total - prot - obs);
                    break;

                case OptionClass.ModerateWrong:
                    // Concentración excesiva en protección (~80%) dejando observación y comunicaciones al mínimo
                    comm = 1;
                    obs = 1;
                    prot = Mathf.Max(1, total - 2);
                    break;

                default: // SeriousWrong: deja al menos un área esencial en 0
                    prot = total - 1;
                    obs = 1;
                    comm = 0;
                    break;
            }
        }

        private static DecisionOption FindOptionByClass(DecisionData data, OptionClass cls)
        {
            if (data == null || data.options == null) return null;
            for (int i = 0; i < data.options.Count; i++)
            {
                if (data.options[i].classification == cls)
                    return data.options[i];
            }
            return data.options.Count > 0 ? data.options[0] : null;
        }
    }
}
