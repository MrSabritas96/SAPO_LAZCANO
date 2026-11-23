using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>ScriptableObject con una toma de decision: contexto, pregunta, 4 alternativas y condiciones.</summary>
    [CreateAssetMenu(menuName = "Simulador/Decision", fileName = "Decision")]
    public class DecisionData : ScriptableObject
    {
        public string decisionId;
        public int order;
        public string stageName;
        [TextArea(2, 6)] public string context;
        [TextArea(1, 3)] public string question;
        public DecisionKind kind = DecisionKind.Standard;
        public bool shuffleOptions = true;
        public ScoreCriterion primaryCriterion;
        public bool hasSecondaryCriterion;
        public ScoreCriterion secondaryCriterion;
        [Range(0f, 1f)] public float secondaryShare = 0.3f;
        public List<DecisionOption> options = new List<DecisionOption>();
        public List<DecisionCondition> skipConditions = new List<DecisionCondition>();
        public List<ContextModifier> contextModifiers = new List<ContextModifier>();
        [Tooltip("Etiqueta de evento previo que el ScenarioDirector escenifica antes de mostrar la pregunta.")]
        public string preEventTag;

        public bool Validate(out string error)
        {
            error = null;
            if (options == null || options.Count != 4) { error = decisionId + ": debe tener exactamente 4 alternativas."; return false; }
            int[] c = new int[4];
            foreach (var o in options) c[(int)o.classification]++;
            if (c[0] != 1 || c[1] != 1 || c[2] != 1 || c[3] != 1) { error = decisionId + ": requiere 1 excelente, 1 buena, 1 incorrecta moderada y 1 incorrecta grave."; return false; }
            return true;
        }
    }
}
