using System;
using UnityEngine;

namespace SimuladorBloqueos.Decisions
{
    /// <summary>
    /// Construye la retroalimentación posterior a cada decisión.
    /// REGLA ESTRICTA DE DISEÑO:
    /// - ÚNICAMENTE en la Decisión 2 ("Cantidad de efectivos"), si el jugador elige una opción incorrecta
    ///   (5 efectivos o 30 efectivos), se activa la ventana correctiva con TIP formativo e indicación de que
    ///   15 efectivos era la alternativa excelente.
    /// - En ninguna otra decisión del juego se muestra el TIP ni se revela cuál era la alternativa excelente.
    /// </summary>
    public class DecisionFeedbackManager : MonoBehaviour
    {
        [Serializable]
        public class CorrectiveTipPayload
        {
            public bool shouldShowModal;
            public int selectedCount;
            public float scoreObtained;
            public string classificationLabel;
            public string headerLine;
            public string limitationExplanation;
            public string educationalTip;
            public string excellentRevealLine;
        }

        public CorrectiveTipPayload BuildFeedbackForResult(DecisionData decision, DecisionResult result)
        {
            var payload = new CorrectiveTipPayload { shouldShowModal = false };
            if (decision == null || result == null) return payload;

            // REGLA EXCLUSIVA: solo aplica a la Decisión 2 (DecisionKind.PersonnelCount) cuando es incorrecta
            if (decision.kind != DecisionKind.PersonnelCount)
                return payload;

            if (result.classification != OptionClass.ModerateWrong && result.classification != OptionClass.SeriousWrong)
                return payload;

            payload.shouldShowModal = true;
            payload.selectedCount = result.numericValue;
            payload.scoreObtained = result.scoreObtained;
            payload.classificationLabel = DecisionResult.GetClassificationLabel(result.classification);
            payload.headerLine = string.Format("Seleccionaste {0} efectivos — Puntaje obtenido: {1:0} puntos",
                payload.selectedCount, payload.scoreObtained);

            if (payload.selectedCount == 5)
            {
                payload.limitationExplanation =
                    "Esta cantidad es insuficiente para cubrir simultáneamente seguridad del recinto, observación y apoyo operativo.";
                payload.educationalTip =
                    "En una manifestación frente a instalaciones policiales debes garantizar protección perimetral, control de accesos, relevo mínimo y capacidad de reacción sin exponer al personal.";
            }
            else // 30 efectivos
            {
                payload.limitationExplanation =
                    "Movilizar la totalidad de los 30 efectivos desde el inicio compromete la reserva operativa de la unidad y puede percibirse como una sobrerreacción desproporcionada frente al nivel inicial del incidente.";
                payload.educationalTip =
                    "El despliegue policial táctico exige proporcionalidad y mantener una reserva orgánica lista para relevos o contingencias secundarias, evitando saturar el perímetro prematuramente.";
            }

            payload.excellentRevealLine =
                "La alternativa excelente de esta simulación era seleccionar 15 efectivos.";

            return payload;
        }
    }
}
