using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos.Decisions
{
    public enum OptionClass { Excellent = 0, Good = 1, ModerateWrong = 2, SeriousWrong = 3 }

    public enum SimVar
    {
        personnelAvailability, protectionLevel, communicationLevel, observationCapacity, equipmentReadiness,
        publicSafety, incidentSeverity, missionProgress, currentScore, criticalViolationCount
    }

    public enum ScoreCriterion { PhysicalIntegrity = 0, Legality = 1, PersonnelAdmin = 2, Equipment = 3, Coordination = 4 }

    public enum DecisionKind { Standard = 0, PersonnelCount = 1, Distribution = 2 }

    public enum ConditionKind { FlagSet, FlagNotSet, VarBelow, VarAbove }

    [Serializable]
    public class VariableEffect
    {
        public SimVar variable;
        public float delta;
        public VariableEffect() { }
        public VariableEffect(SimVar v, float d) { variable = v; delta = d; }
    }

    /// <summary>Condicion configurable sobre el estado de la simulacion (banderas o variables).</summary>
    [Serializable]
    public class DecisionCondition
    {
        public ConditionKind kind;
        public string flag;
        public SimVar variable;
        public float threshold;

        public bool Evaluate(SimulationState s)
        {
            if (s == null) return false;
            switch (kind)
            {
                case ConditionKind.FlagSet: return s.HasFlag(flag);
                case ConditionKind.FlagNotSet: return !s.HasFlag(flag);
                case ConditionKind.VarBelow: return s.Get(variable) < threshold;
                case ConditionKind.VarAbove: return s.Get(variable) > threshold;
            }
            return false;
        }
    }

    /// <summary>Consecuencia que se activa varias decisiones despues de la eleccion.</summary>
    [Serializable]
    public class DeferredConsequence
    {
        public string id;
        [Tooltip("Cantidad de decisiones posteriores que deben resolverse antes de que se active.")]
        public int afterDecisions = 2;
        [TextArea] public string description;
        public List<VariableEffect> effects = new List<VariableEffect>();
        public string setFlag;
        [Tooltip("Ajuste (puntos) sobre el criterio indicado. Solo se aplica una vez gracias a 'id'.")]
        public float scoreDelta;
        public ScoreCriterion criterion;
    }

    [Serializable]
    public class DecisionOption
    {
        public string id;
        [TextArea] public string text;
        public OptionClass classification;
        public float score;
        [TextArea] public string immediateConsequence;
        public List<VariableEffect> variableEffects = new List<VariableEffect>();
        public List<DeferredConsequence> deferredConsequences = new List<DeferredConsequence>();
        public List<string> setFlags = new List<string>();
        public bool criticalViolation;
        [Tooltip("Etiqueta que lee el ScenarioDirector para reaccionar en el mundo.")]
        public string reactionTag;
        [Tooltip("Valor numerico asociado (por ejemplo cantidad de efectivos).")]
        public int numericValue;
    }

    /// <summary>Modificador de contexto/evaluacion que depende de decisiones previas.</summary>
    [Serializable]
    public class ContextModifier
    {
        public DecisionCondition condition;
        [TextArea] public string addedContext;
        [Tooltip("Cambia la puntuacion de las alternativas Excelente/Buena (dificultad adicional) cuando se cumple la condicion.")]
        public float topOptionsScoreDelta;
        public List<VariableEffect> effectsOnShow = new List<VariableEffect>();
    }

    [Serializable]
    public class BranchRule
    {
        public string fromDecisionId;
        public DecisionCondition condition;
        public string gotoDecisionId;
    }
}
