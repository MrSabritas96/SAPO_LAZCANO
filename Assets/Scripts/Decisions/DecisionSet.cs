using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>Conjunto ordenado de decisiones y reglas de ramificacion.</summary>
    [CreateAssetMenu(menuName = "Simulador/Conjunto de Decisiones", fileName = "DecisionSet")]
    public class DecisionSet : ScriptableObject
    {
        public List<DecisionData> decisions = new List<DecisionData>();
        public List<BranchRule> branchRules = new List<BranchRule>();
    }
}
