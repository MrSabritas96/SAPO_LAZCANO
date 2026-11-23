using System.Text;
using UnityEngine;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>
    /// Gestiona la ramificación, omisión condicional de etapas y modificadores de contexto
    /// basados en el estado acumulado de la simulación (SimulationState) sin usar condicionales rígidos.
    /// </summary>
    public class DecisionBranchManager : MonoBehaviour
    {
        /// <summary>
        /// Determina cuál es la siguiente decisión válida del DecisionSet tras resolver currentIndex.
        /// Evalúa primero las reglas de ramificación (branchRules) y luego las condiciones de omisión (skipConditions).
        /// </summary>
        public int GetNextDecisionIndex(DecisionSet set, int currentIndex, SimulationState state)
        {
            if (set == null || set.decisions == null || set.decisions.Count == 0)
                return -1;

            // 1. Verificar reglas de salto explícito (branchRules) desde la decisión actual
            if (currentIndex >= 0 && currentIndex < set.decisions.Count && set.branchRules != null)
            {
                string currentId = set.decisions[currentIndex].decisionId;
                for (int i = 0; i < set.branchRules.Count; i++)
                {
                    var rule = set.branchRules[i];
                    if (rule != null && rule.fromDecisionId == currentId &&
                        rule.condition != null && rule.condition.Evaluate(state))
                    {
                        int targetIdx = FindDecisionIndexById(set, rule.gotoDecisionId);
                        if (targetIdx >= 0)
                            return AdvancePastSkipped(set, targetIdx, state);
                    }
                }
            }

            // 2. Avance secuencial evaluando skipConditions
            return AdvancePastSkipped(set, currentIndex + 1, state);
        }

        private int AdvancePastSkipped(DecisionSet set, int startIndex, SimulationState state)
        {
            int idx = startIndex;
            while (idx >= 0 && idx < set.decisions.Count)
            {
                var d = set.decisions[idx];
                if (d != null && !ShouldSkipDecision(d, state))
                    return idx;
                idx++;
            }
            return -1; // Fin de las decisiones
        }

        public bool ShouldSkipDecision(DecisionData data, SimulationState state)
        {
            if (data == null || data.skipConditions == null || data.skipConditions.Count == 0)
                return false;

            for (int i = 0; i < data.skipConditions.Count; i++)
            {
                if (data.skipConditions[i] != null && data.skipConditions[i].Evaluate(state))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Construye el texto de contexto enriquecido dinámicamente según las decisiones previas
        /// y devuelve el modificador de puntaje acumulado sobre las opciones superiores.
        /// </summary>
        public string BuildDynamicContext(DecisionData data, SimulationState state, out float topOptionsScoreDelta)
        {
            topOptionsScoreDelta = 0f;
            if (data == null) return "";

            StringBuilder sb = new StringBuilder(512);
            sb.Append(data.context);

            // Si es la Decisión 3 (Distribución), indicar cuántos efectivos fueron asignados en la Decisión 2
            if (data.kind == DecisionKind.Distribution && state != null)
            {
                sb.AppendFormat("\n\n• Contingente asignado en la etapa anterior: {0} efectivos UTOP listos para ser distribuidos en las tres responsabilidades operativas.",
                    state.selectedContingentCount);
            }

            if (data.contextModifiers != null && state != null)
            {
                for (int i = 0; i < data.contextModifiers.Count; i++)
                {
                    var mod = data.contextModifiers[i];
                    if (mod != null && mod.condition != null && mod.condition.Evaluate(state))
                    {
                        if (!string.IsNullOrEmpty(mod.addedContext))
                        {
                            sb.Append("\n\n[ACTUALIZACIÓN OPERATIVA POR DECISIONES PREVIAS]: ");
                            sb.Append(mod.addedContext);
                        }
                        topOptionsScoreDelta += mod.topOptionsScoreDelta;

                        if (mod.effectsOnShow != null)
                        {
                            for (int e = 0; e < mod.effectsOnShow.Count; e++)
                            {
                                state.ApplyDelta(mod.effectsOnShow[e].variable, mod.effectsOnShow[e].delta);
                            }
                        }
                    }
                }
            }

            return sb.ToString();
        }

        private static int FindDecisionIndexById(DecisionSet set, string id)
        {
            if (set == null || set.decisions == null || string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < set.decisions.Count; i++)
            {
                if (set.decisions[i] != null && set.decisions[i].decisionId == id)
                    return i;
            }
            return -1;
        }
    }
}
