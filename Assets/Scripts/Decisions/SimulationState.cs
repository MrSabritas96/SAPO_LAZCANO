using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>
    /// Estado centralizado de la simulación táctica UTOP.
    /// Contiene las 10 variables de estado obligatorias, banderas de decisiones previas
    /// y la cola de consecuencias diferidas acumulativas.
    /// </summary>
    public class SimulationState : MonoBehaviour
    {
        [Header("10 Variables Centrales de Estado (0 - 100)")]
        public float personnelAvailability = 70f;
        public float protectionLevel = 55f;
        public float communicationLevel = 65f;
        public float observationCapacity = 60f;
        public float equipmentReadiness = 50f;
        public float publicSafety = 75f;
        public float incidentSeverity = 30f;
        public float missionProgress = 0f;
        public float currentScore = 100f;
        public int criticalViolationCount = 0;

        [Header("Datos Operativos Derivados de Decisiones")]
        public int selectedContingentCount = 15;
        public int distProtection = 7;
        public int distObservation = 4;
        public int distCommunications = 4;
        public int attributableInjuriesCount = 0;

        [Serializable]
        public class PendingDeferred
        {
            public string sourceDecisionId;
            public DeferredConsequence consequence;
            public int remainingDecisions;
        }

        [SerializeField] private List<string> _activeFlags = new List<string>();
        [SerializeField] private List<PendingDeferred> _pendingDeferred = new List<PendingDeferred>();
        private readonly HashSet<string> _triggeredDeferredIds = new HashSet<string>();

        public event Action<SimVar, float, float> OnVariableChanged;
        public event Action<DeferredConsequence, string> OnDeferredTriggered;

        public void ResetState()
        {
            personnelAvailability = 70f;
            protectionLevel = 55f;
            communicationLevel = 65f;
            observationCapacity = 60f;
            equipmentReadiness = 50f;
            publicSafety = 75f;
            incidentSeverity = 30f;
            missionProgress = 0f;
            currentScore = 100f;
            criticalViolationCount = 0;

            selectedContingentCount = 15;
            distProtection = 7;
            distObservation = 4;
            distCommunications = 4;
            attributableInjuriesCount = 0;

            _activeFlags.Clear();
            _pendingDeferred.Clear();
            _triggeredDeferredIds.Clear();
        }

        public float Get(SimVar v)
        {
            switch (v)
            {
                case SimVar.personnelAvailability: return personnelAvailability;
                case SimVar.protectionLevel: return protectionLevel;
                case SimVar.communicationLevel: return communicationLevel;
                case SimVar.observationCapacity: return observationCapacity;
                case SimVar.equipmentReadiness: return equipmentReadiness;
                case SimVar.publicSafety: return publicSafety;
                case SimVar.incidentSeverity: return incidentSeverity;
                case SimVar.missionProgress: return missionProgress;
                case SimVar.currentScore: return currentScore;
                case SimVar.criticalViolationCount: return criticalViolationCount;
            }
            return 0f;
        }

        public void Set(SimVar v, float val)
        {
            float old = Get(v);
            float clamped = (v == SimVar.criticalViolationCount)
                ? Mathf.Max(0f, Mathf.Round(val))
                : Mathf.Clamp(val, 0f, 100f);

            switch (v)
            {
                case SimVar.personnelAvailability: personnelAvailability = clamped; break;
                case SimVar.protectionLevel: protectionLevel = clamped; break;
                case SimVar.communicationLevel: communicationLevel = clamped; break;
                case SimVar.observationCapacity: observationCapacity = clamped; break;
                case SimVar.equipmentReadiness: equipmentReadiness = clamped; break;
                case SimVar.publicSafety: publicSafety = clamped; break;
                case SimVar.incidentSeverity: incidentSeverity = clamped; break;
                case SimVar.missionProgress: missionProgress = clamped; break;
                case SimVar.currentScore: currentScore = clamped; break;
                case SimVar.criticalViolationCount: criticalViolationCount = Mathf.RoundToInt(clamped); break;
            }

            if (Mathf.Abs(old - clamped) > 0.001f)
            {
                OnVariableChanged?.Invoke(v, old, clamped);
            }
        }

        public float ApplyDelta(SimVar v, float delta)
        {
            float old = Get(v);
            Set(v, old + delta);
            return Get(v) - old;
        }

        public bool HasFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return false;
            return _activeFlags.Contains(flag);
        }

        public void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (!_activeFlags.Contains(flag))
                _activeFlags.Add(flag);
        }

        public IReadOnlyList<string> GetActiveFlags() => _activeFlags;

        public void EnqueueDeferred(string sourceDecisionId, DeferredConsequence dc)
        {
            if (dc == null) return;
            string cid = string.IsNullOrEmpty(dc.id) ? (sourceDecisionId + "_def_" + _pendingDeferred.Count) : dc.id;
            if (_triggeredDeferredIds.Contains(cid)) return;
            for (int i = 0; i < _pendingDeferred.Count; i++)
            {
                if (_pendingDeferred[i].consequence != null && _pendingDeferred[i].consequence.id == cid)
                    return;
            }
            _pendingDeferred.Add(new PendingDeferred
            {
                sourceDecisionId = sourceDecisionId,
                consequence = dc,
                remainingDecisions = Mathf.Max(1, dc.afterDecisions)
            });
        }

        /// <summary>
        /// Avanza un turno de decisión y devuelve las consecuencias diferidas que se activan en este momento.
        /// Garantiza que cada consecuencia diferida solo se dispare una única vez.
        /// </summary>
        public List<DeferredConsequence> AdvanceDeferredTurn()
        {
            List<DeferredConsequence> fired = new List<DeferredConsequence>();
            for (int i = _pendingDeferred.Count - 1; i >= 0; i--)
            {
                var item = _pendingDeferred[i];
                item.remainingDecisions--;
                if (item.remainingDecisions <= 0)
                {
                    _pendingDeferred.RemoveAt(i);
                    if (item.consequence != null)
                    {
                        string cid = string.IsNullOrEmpty(item.consequence.id)
                            ? (item.sourceDecisionId + "_fired")
                            : item.consequence.id;

                        if (_triggeredDeferredIds.Add(cid))
                        {
                            if (item.consequence.effects != null)
                            {
                                for (int e = 0; e < item.consequence.effects.Count; e++)
                                {
                                    ApplyDelta(item.consequence.effects[e].variable, item.consequence.effects[e].delta);
                                }
                            }
                            if (!string.IsNullOrEmpty(item.consequence.setFlag))
                            {
                                SetFlag(item.consequence.setFlag);
                            }
                            fired.Add(item.consequence);
                            OnDeferredTriggered?.Invoke(item.consequence, item.sourceDecisionId);
                        }
                    }
                }
            }
            return fired;
        }

        public static string GetVariableDisplayName(SimVar v)
        {
            switch (v)
            {
                case SimVar.personnelAvailability: return "Disponibilidad de efectivos";
                case SimVar.protectionLevel: return "Nivel de protección";
                case SimVar.communicationLevel: return "Nivel de comunicaciones";
                case SimVar.observationCapacity: return "Capacidad de observación";
                case SimVar.equipmentReadiness: return "Alistamiento de equipo";
                case SimVar.publicSafety: return "Seguridad pública";
                case SimVar.incidentSeverity: return "Gravedad del incidente";
                case SimVar.missionProgress: return "Progreso de misión";
                case SimVar.currentScore: return "Puntaje actual";
                case SimVar.criticalViolationCount: return "Incumplimientos graves";
            }
            return v.ToString();
        }
    }
}
