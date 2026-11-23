using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>
    /// Historial completo de decisiones tomadas en la partida.
    /// Permite reconstruir toda la secuencia al finalizar la simulación.
    /// </summary>
    public class DecisionHistory : MonoBehaviour
    {
        [SerializeField] private List<DecisionResult> _entries = new List<DecisionResult>();

        public IReadOnlyList<DecisionResult> Entries => _entries;
        public int Count => _entries.Count;

        public void ClearHistory()
        {
            _entries.Clear();
        }

        public void Record(DecisionResult result)
        {
            if (result == null) return;
            _entries.Add(result);
        }

        public DecisionResult GetByDecisionId(string decisionId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].decisionId == decisionId)
                    return _entries[i];
            }
            return null;
        }

        public string BuildDetailedReportText()
        {
            StringBuilder sb = new StringBuilder(2048);
            for (int i = 0; i < _entries.Count; i++)
            {
                var r = _entries[i];
                int mins = Mathf.FloorToInt(r.gameTimeSeconds / 60f);
                int secs = Mathf.FloorToInt(r.gameTimeSeconds % 60f);
                sb.AppendFormat("{0}. [{1:00}:{2:00}] {3} ({4})\n",
                    i + 1, mins, secs, r.stageName, r.decisionId);
                sb.AppendFormat("   • Elección: {0}\n", r.selectedOptionText);
                if (r.hasCustomDistribution)
                {
                    sb.AppendFormat("   • Distribución: Protección={0} | Observación={1} | Comunicaciones={2}\n",
                        r.distProtection, r.distObservation, r.distCommunications);
                }
                sb.AppendFormat("   • Clasificación: {0}  |  Puntaje: {1:0.#} pts\n",
                    DecisionResult.GetClassificationLabel(r.classification), r.scoreObtained);
                sb.AppendFormat("   • Consecuencia: {0}\n", r.immediateConsequence);
                if (r.triggeredDeferredConsequences != null && r.triggeredDeferredConsequences.Count > 0)
                {
                    for (int d = 0; d < r.triggeredDeferredConsequences.Count; d++)
                    {
                        sb.AppendFormat("   • Efecto acumulativo activado: {0}\n", r.triggeredDeferredConsequences[d]);
                    }
                }
            }
            return sb.ToString();
        }
    }
}
