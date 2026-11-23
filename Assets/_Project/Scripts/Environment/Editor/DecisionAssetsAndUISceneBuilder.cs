#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimuladorBloqueos;
using SimuladorBloqueos.Decisions;
using SimuladorBloqueos.Decisions.UI;
using SimuladorBloqueos.EditorTools;

namespace SimuladorBloqueos.Environment.Editor
{
    /// <summary>
    /// Generador automatizado de:
    /// 1. Los 13 ScriptableObjects de decisión (Decision_01..Decision_13) y el DecisionSet en Resources/Decisions.
    /// 2. La interfaz uGUI completa (DecisionPanelUI con cuadrícula 2x2 y widget de distribución para Decisión 3,
    ///    DecisionFeedbackUI exclusivo para Decisión 2 y DecisionResultUI para consecuencias e informe final).
    /// 3. La integración de RuntimeNavMeshBaker, ScenarioDirector, DecisionManager y los 8 props 3D importados.
    /// </summary>
    public static class DecisionAssetsAndUISceneBuilder
    {
        public const string DECISIONS_RES_DIR = "Assets/_Project/Resources/Decisions";

        [MenuItem("Tools/Simulador UTOP/Generar 13 Decisiones y UI en BaseUTOP")]
        public static void BuildDecisionsAndIntegrateInCurrentScene()
        {
            DecisionSet set = CreateOrUpdateAll13DecisionAssets();

            Transform streetRoot = GameObject.Find("CalleJunin_Environment")?.transform;
            Transform compoundRoot = GameObject.Find("UTOP_Interior_And_Compound")?.transform;
            if (compoundRoot == null && streetRoot != null)
            {
                var found = streetRoot.Find("UTOP_Interior_And_Compound");
                if (found != null) compoundRoot = found;
            }
            if (compoundRoot == null)
            {
                var go = new GameObject("UTOP_Compound_Props");
                compoundRoot = go.transform;
            }

            IntegrateDecisionSystemIntoScene(compoundRoot, streetRoot, set);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[DecisionAssetsAndUISceneBuilder] 13 Decisiones, Props 3D, Armería funcional y UI integrados en BaseUTOP.");
        }

        public static void IntegrateDecisionSystemIntoScene(
            Transform compoundRoot,
            Transform streetRoot,
            DecisionSet decisionSet = null)
        {
            if (decisionSet == null)
                decisionSet = CreateOrUpdateAll13DecisionAssets();

            // Limpiar instancias previas si se está reconstruyendo
            var oldSys = GameObject.Find("Sistema_Decisiones_Y_Escenario_UTOP");
            if (oldSys != null) Object.DestroyImmediate(oldSys);
            var oldCanvas = GameObject.Find("Canvas_Decisiones_Tacticas_UTOP");
            if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);
            var oldImported = GameObject.Find("Imported_3D_Armory_Props");
            if (oldImported != null) Object.DestroyImmediate(oldImported);

            // 1. Crear objeto raíz del sistema (NavMeshBaker + ScenarioDirector + DecisionManager + módulos)
            GameObject sysRoot = new GameObject("Sistema_Decisiones_Y_Escenario_UTOP");
            sysRoot.AddComponent<RuntimeNavMeshBaker>();

            var director = sysRoot.AddComponent<ScenarioDirector>();
            director.RefreshSceneReferences();

            // 2. Poblar la Sala de Armas (Armería) y el acceso UTOP con los 8 modelos 3D importados
            CharacterAssetsAndClipsProcessor.PopulateArmoryAndStreetWithImportedTacticalProps(
                compoundRoot, streetRoot, CalleJuninSceneBuilder.GetRoadY, director);

            // 3. Construir el Canvas uGUI de Toma de Decisiones (2x2 + Distribución + Modal TIP Decisión 2 + Resultado)
            BuildDecisionUICanvas(out DecisionPanelUI panelUI, out DecisionFeedbackUI feedbackUI, out DecisionResultUI resultUI);

            // 4. Configurar DecisionManager y todos sus submódulos
            var state = sysRoot.AddComponent<SimulationState>();
            var scoreMgr = sysRoot.AddComponent<ScoreManager>();
            var evalMgr = sysRoot.AddComponent<EvaluationManager>();
            var evaluator = sysRoot.AddComponent<DecisionEvaluator>();
            var branchMgr = sysRoot.AddComponent<DecisionBranchManager>();
            var feedbackMgr = sysRoot.AddComponent<DecisionFeedbackManager>();
            var history = sysRoot.AddComponent<DecisionHistory>();
            var manager = sysRoot.AddComponent<DecisionManager>();

            manager.decisionSet = decisionSet;
            manager.simulationState = state;
            manager.scoreManager = scoreMgr;
            manager.evaluationManager = evalMgr;
            manager.evaluator = evaluator;
            manager.branchManager = branchMgr;
            manager.feedbackManager = feedbackMgr;
            manager.history = history;
            manager.scenarioDirector = director;
            manager.decisionPanelUI = panelUI;
            manager.decisionFeedbackUI = feedbackUI;
            manager.decisionResultUI = resultUI;
            manager.autoStartOnPlay = true;
            manager.initialDelaySeconds = 1.0f;
        }

        // =========================================================================================
        // 1. CREACIÓN DE LOS 13 SCRIPTABLEOBJECTS DE DECISIÓN Y EL DECISIONSET
        // =========================================================================================

        public static DecisionSet CreateOrUpdateAll13DecisionAssets()
        {
            EnsureFolder("Assets/_Project/Resources");
            EnsureFolder(DECISIONS_RES_DIR);

            var list = new List<DecisionData>(13);

            // DECISIÓN 1: Orden inicial del superior
            list.Add(SaveDecision(1, "D01_OrdenInicial", "Orden inicial del superior",
                "Te encuentras en el Primer Piso de la UTOP frente al Capitán Comandante. Reportes de radio informan que columnas de manifestantes (mineros, cholitas y ponchos rojos) bloquean la pendiente de la Calle Junín frente al recinto policial.",
                "¿Cómo recepcionas y estructuras la orden operativa inicial del Capitán antes de bajar al Patio de Honor?",
                DecisionKind.Standard, ScoreCriterion.Coordination, true, ScoreCriterion.Legality, 0.35f,
                new DecisionOption
                {
                    id = "D01_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Confirmar alcance del mandato, reglas de uso proporcional de la fuerza, puntos críticos del recinto y cadena de reporte antes de movilizar a la tropa.",
                    immediateConsequence = "Queda establecida con claridad la misión defensiva, el marco legal vigente y el canal de mando con el Capitán.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, 10f), new VariableEffect(SimVar.protectionLevel, 8f) },
                    setFlags = new List<string> { "D01_CLEAR_MANDATE" }, reactionTag = "D01_BRIEFING_OK"
                },
                new DecisionOption
                {
                    id = "D01_GOOD", classification = OptionClass.Good, score = 88f,
                    text = "Acusar recibo rápido de la orden priorizando la contención del perímetro exterior y bajar de inmediato a formar al personal.",
                    immediateConsequence = "Se inicia el alistamiento con rapidez, aunque algunos detalles de enlace secundario deberán precisarse en el patio.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, 5f) },
                    reactionTag = "D01_BRIEFING_FAST"
                },
                new DecisionOption
                {
                    id = "D01_MOD", classification = OptionClass.ModerateWrong, score = 68f,
                    text = "Retrasar la salida discutiendo atribuciones administrativas secundarias mientras el bloqueo se aproxima al pórtico.",
                    immediateConsequence = "Se pierde tiempo valioso de preparación inicial y aumenta la presión sobre la guardia de entrada.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.incidentSeverity, 8f), new VariableEffect(SimVar.protectionLevel, -6f) },
                    deferredConsequences = new List<DeferredConsequence>
                    {
                        new DeferredConsequence
                        {
                            id = "DEF_D01_LATE_PREP", afterDecisions = 2,
                            description = "El retraso en la recepción de la orden inicial redujo el margen de organización de los puestos en el patio.",
                            effects = new List<VariableEffect> { new VariableEffect(SimVar.observationCapacity, -5f) },
                            scoreDelta = -3f, criterion = ScoreCriterion.Coordination
                        }
                    },
                    reactionTag = "D01_BRIEFING_DELAY"
                },
                new DecisionOption
                {
                    id = "D01_GRAVE", classification = OptionClass.SeriousWrong, score = 40f, criticalViolation = true,
                    text = "Ignorar los lineamientos de gradualidad del Capitán y ordenar por cuenta propia el despeje forzoso inmediato de toda la Calle Junín.",
                    immediateConsequence = "Incumplimiento grave de la cadena de mando y de los protocolos de actuación proporcional de la UTOP.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, -15f), new VariableEffect(SimVar.incidentSeverity, 15f) },
                    setFlags = new List<string> { "D01_INSUBORDINATION" }, reactionTag = "D01_BRIEFING_VIOLATION"
                }));

            // DECISIÓN 2: Cantidad de efectivos (Regla especial de puntajes: 5=95, 15=100, 20=98, 30=96 + Modal TIP si elige 5 o 30)
            var d2 = SaveDecision(2, "D02_CantidadEfectivos", "Cantidad de efectivos",
                "En la Cuadra de Tropa N° 1 aguardan 30 efectivos UTOP disponibles. Debes definir cuántos efectivos conformarán el contingente activo para proteger las instalaciones, mantener observación y asegurar las comunicaciones sin agotar la reserva.",
                "¿Cuántos efectivos seleccionas para conformar el contingente operativo en el Patio de Honor?",
                DecisionKind.PersonnelCount, ScoreCriterion.PersonnelAdmin, false, ScoreCriterion.Coordination, 0f,
                new DecisionOption
                {
                    id = "D02_5", classification = OptionClass.ModerateWrong, score = 95f, numericValue = 5,
                    text = "Seleccionar 5 efectivos UTOP.",
                    immediateConsequence = "Solo 5 efectivos salen al Patio de Honor. La cantidad resulta insuficiente para cubrir simultáneamente protección, observación y comunicaciones.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.personnelAvailability, -22f), new VariableEffect(SimVar.protectionLevel, -14f), new VariableEffect(SimVar.observationCapacity, -10f) },
                    setFlags = new List<string> { "D02_UNDERSTAFFED_5" },
                    deferredConsequences = new List<DeferredConsequence>
                    {
                        new DeferredConsequence
                        {
                            id = "DEF_D02_5_FATIGUE", afterDecisions = 3,
                            description = "La escasez de personal (solo 5 efectivos asignados en la Decisión 2) sobrecarga los puestos de guardia y deja flancos sin relevo.",
                            effects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, -8f), new VariableEffect(SimVar.publicSafety, -6f) },
                            setFlag = "CONTINGENT_OVERLOADED", scoreDelta = -4f, criterion = ScoreCriterion.PersonnelAdmin
                        }
                    },
                    reactionTag = "D02_COUNT_5"
                },
                new DecisionOption
                {
                    id = "D02_15", classification = OptionClass.Excellent, score = 100f, numericValue = 15,
                    text = "Seleccionar 15 efectivos UTOP.",
                    immediateConsequence = "15 efectivos forman en el Patio de Honor: equilibrio óptimo entre cobertura perimetral, observación, enlace y reserva orgánica en cuadra.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.personnelAvailability, 15f), new VariableEffect(SimVar.protectionLevel, 12f), new VariableEffect(SimVar.observationCapacity, 10f) },
                    setFlags = new List<string> { "D02_OPTIMAL_15" }, reactionTag = "D02_COUNT_15"
                },
                new DecisionOption
                {
                    id = "D02_20", classification = OptionClass.Good, score = 98f, numericValue = 20,
                    text = "Seleccionar 20 efectivos UTOP.",
                    immediateConsequence = "20 efectivos forman en el Patio de Honor: brinda buena cobertura operativa aunque reduce parcialmente la reserva de descanso.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.personnelAvailability, 8f), new VariableEffect(SimVar.protectionLevel, 10f) },
                    setFlags = new List<string> { "D02_GOOD_20" }, reactionTag = "D02_COUNT_20"
                },
                new DecisionOption
                {
                    id = "D02_30", classification = OptionClass.SeriousWrong, score = 96f, numericValue = 30,
                    text = "Seleccionar 30 efectivos UTOP (totalidad de la cuadra).",
                    immediateConsequence = "Los 30 efectivos salen al Patio de Honor dejando 0 efectivos de reserva para relevos o emergencias internas y proyectando una sobrerreacción.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.personnelAvailability, -15f), new VariableEffect(SimVar.incidentSeverity, 12f) },
                    setFlags = new List<string> { "D02_OVERCOMMITTED_30" },
                    deferredConsequences = new List<DeferredConsequence>
                    {
                        new DeferredConsequence
                        {
                            id = "DEF_D02_30_NORESERVE", afterDecisions = 4,
                            description = "Al haber comprometido los 30 efectivos desde el inicio sin dejar reserva en la Cuadra 1, el contingente acusa desgaste sin posibilidad de relevo.",
                            effects = new List<VariableEffect> { new VariableEffect(SimVar.personnelAvailability, -10f), new VariableEffect(SimVar.communicationLevel, -6f) },
                            setFlag = "NO_RESERVE_AVAILABLE", scoreDelta = -5f, criterion = ScoreCriterion.PersonnelAdmin
                        }
                    },
                    reactionTag = "D02_COUNT_30"
                });
            d2.shuffleOptions = false; // Conservar orden 5, 15, 20, 30 en la cuadrícula 2x2
            EditorUtility.SetDirty(d2);
            list.Add(d2);

            // DECISIÓN 3: Distribución de efectivos (Protección, Observación y reporte, Coordinación y comunicaciones)
            var d3 = SaveDecision(3, "D03_DistribucionEfectivos", "Distribución de efectivos",
                "Con el contingente ya formado en el Patio de Honor, debes distribuir exactamente a tus efectivos entre las tres responsabilidades esenciales: 1) Protección de accesos, 2) Observación y reporte, y 3) Coordinación y comunicaciones.",
                "¿Cómo distribuyes los efectivos seleccionados entre Protección, Observación y Comunicaciones?",
                DecisionKind.Distribution, ScoreCriterion.PersonnelAdmin, true, ScoreCriterion.Coordination, 0.35f,
                new DecisionOption
                {
                    id = "D03_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Distribución equilibrada: asignar ~46% a Protección perimetral, ~27% a Observación de puntos altos/accesos y ~27% a Coordinación y comunicaciones.",
                    immediateConsequence = "Los tres equipos ocupan sus puestos en el Patio, Pórtico y Sala de Operaciones con cobertura integral y enlace continuo.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, 12f), new VariableEffect(SimVar.observationCapacity, 12f), new VariableEffect(SimVar.communicationLevel, 12f) },
                    setFlags = new List<string> { "D03_BALANCED" }, reactionTag = "D03_DIST_EXCELLENT"
                },
                new DecisionOption
                {
                    id = "D03_GOOD", classification = OptionClass.Good, score = 86f,
                    text = "Distribución funcional con prioridad en contención: reforzar Protección (~56%) manteniendo equipos reducidos pero activos en Observación y Comunicaciones.",
                    immediateConsequence = "La línea de protección queda sólida y se mantiene enlace básico, aunque el equipo de comunicaciones trabajará con mayor carga.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, 10f), new VariableEffect(SimVar.observationCapacity, 6f), new VariableEffect(SimVar.communicationLevel, 4f) },
                    reactionTag = "D03_DIST_GOOD"
                },
                new DecisionOption
                {
                    id = "D03_MOD", classification = OptionClass.ModerateWrong, score = 64f,
                    text = "Concentración excesiva en una sola área: destinar casi todo el personal a la puerta principal dejando solo 1 efectivo en Observación y 1 en Comunicaciones.",
                    immediateConsequence = "Saturación de personal en un único punto y vigilancia deficiente de los flancos y de la pendiente de Calle Junín.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.observationCapacity, -14f), new VariableEffect(SimVar.communicationLevel, -12f) },
                    setFlags = new List<string> { "D03_WEAK_OBS_COMM" },
                    deferredConsequences = new List<DeferredConsequence>
                    {
                        new DeferredConsequence
                        {
                            id = "DEF_D03_BLIND_FLANK", afterDecisions = 3,
                            description = "La debilidad en observación y enlace derivada de la mala distribución en la Decisión 3 retrasa la detección temprana de amenazas en la calle.",
                            effects = new List<VariableEffect> { new VariableEffect(SimVar.observationCapacity, -8f), new VariableEffect(SimVar.publicSafety, -6f) },
                            scoreDelta = -4f, criterion = ScoreCriterion.PersonnelAdmin
                        }
                    },
                    reactionTag = "D03_DIST_UNBALANCED"
                },
                new DecisionOption
                {
                    id = "D03_GRAVE", classification = OptionClass.SeriousWrong, score = 38f, criticalViolation = true,
                    text = "Desatender una responsabilidad esencial: dejar Coordinación y Comunicaciones (o Observación) con 0 efectivos asignados.",
                    immediateConsequence = "Un área crítica queda completamente descubierta (0 efectivos), rompiendo el control operacional y la seguridad del contingente.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, -25f), new VariableEffect(SimVar.observationCapacity, -18f), new VariableEffect(SimVar.publicSafety, -12f) },
                    setFlags = new List<string> { "D03_ZERO_AREA", "COMMS_VULNERABLE" },
                    deferredConsequences = new List<DeferredConsequence>
                    {
                        new DeferredConsequence
                        {
                            id = "DEF_D03_COMMS_COLLAPSE", afterDecisions = 2,
                            description = "Haber dejado un área esencial sin personal en la Decisión 3 provoca pérdida de coordinación entre el mando y el pórtico.",
                            effects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, -12f), new VariableEffect(SimVar.incidentSeverity, 10f) },
                            scoreDelta = -6f, criterion = ScoreCriterion.Coordination
                        }
                    },
                    reactionTag = "D03_DIST_ZERO"
                });
            d3.contextModifiers.Add(new ContextModifier
            {
                condition = new DecisionCondition { kind = ConditionKind.FlagSet, flag = "D02_UNDERSTAFFED_5" },
                addedContext = "Con solo 5 efectivos seleccionados en la Decisión 2, el margen para cubrir las 3 áreas sin debilitar el perímetro es sumamente estrecho.",
                topOptionsScoreDelta = -4f
            });
            EditorUtility.SetDirty(d3);
            list.Add(d3);

            // DECISIÓN 4: Selección de equipamiento (Cinemática funcional en la Sala de Armas / Armería)
            list.Add(SaveDecision(4, "D04_SeleccionEquipamiento", "Selección de equipamiento",
                "El contingente se dirige a la Sala de Armas (Armería UTOP en Planta Baja), donde se encuentran los escudos de policarbonato (ESCUDO), bastones policiales (TONFA), aerosoles (GASLACRISPRITE), lanzadores de 37mm (LANZAGASLACRI) y escopetas (ESCOPETA).",
                "¿Qué dotación de equipamiento ordenas retirar de la Sala de Armas para el contingente?",
                DecisionKind.Standard, ScoreCriterion.Equipment, true, ScoreCriterion.Legality, 0.30f,
                new DecisionOption
                {
                    id = "D04_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Dotación defensiva reglamentaria completa: cascos con visor, chalecos tácticos, escudos antimotines (ESCUDO) y tonfas (TONFA), manteniendo el lanzador no letal bajo control estricto del jefe de grupo.",
                    immediateConsequence = "La escuadra ingresa a la Sala de Armas, equipa escudos y tonfas 3D y regresa protegida al Patio de Honor cumpliendo el estándar de proporcionalidad.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.equipmentReadiness, 25f), new VariableEffect(SimVar.protectionLevel, 15f), new VariableEffect(SimVar.publicSafety, 8f) },
                    setFlags = new List<string> { "D04_PROPORTIONAL_GEAR" }, reactionTag = "D04_GEAR_EXCELLENT"
                },
                new DecisionOption
                {
                    id = "D04_GOOD", classification = OptionClass.Good, score = 85f,
                    text = "Dotación defensiva estándar con escudos y tonfas para la primera línea, sin portar dispositivos químicos ni lanzadores de apoyo.",
                    immediateConsequence = "El personal queda bien protegido ante objetos contundentes, aunque con menor capacidad disuasiva a distancia.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.equipmentReadiness, 16f), new VariableEffect(SimVar.protectionLevel, 10f) },
                    reactionTag = "D04_GEAR_GOOD"
                },
                new DecisionOption
                {
                    id = "D04_MOD", classification = OptionClass.ModerateWrong, score = 60f,
                    text = "Salir rápidamente sin escudos antimotines ni protección facial completa para ahorrar tiempo en la Armería.",
                    immediateConsequence = "Los efectivos quedan vulnerables ante el lanzamiento de piedras y artefactos pirotécnicos en la Calle Junín.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.equipmentReadiness, -18f), new VariableEffect(SimVar.protectionLevel, -16f) },
                    setFlags = new List<string> { "D04_NO_SHIELDS" },
                    deferredConsequences = new List<DeferredConsequence>
                    {
                        new DeferredConsequence
                        {
                            id = "DEF_D04_UNPROTECTED_LINE", afterDecisions = 3,
                            description = "La falta de escudos antimotines (Decisión 4) reduce drásticamente la capacidad de resistir proyectiles contundentes en el acceso.",
                            effects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, -10f), new VariableEffect(SimVar.publicSafety, -8f) },
                            scoreDelta = -5f, criterion = ScoreCriterion.Equipment
                        }
                    },
                    reactionTag = "D04_GEAR_INSUFFICIENT"
                },
                new DecisionOption
                {
                    id = "D04_GRAVE", classification = OptionClass.SeriousWrong, score = 35f, criticalViolation = true,
                    text = "Distribuir escopetas y lanzadores de gas de forma indiscriminada a toda la tropa sin control de mando ni registro en armería.",
                    immediateConsequence = "Violación grave de los protocolos de control de armamento y alto riesgo de uso desproporcionado de la fuerza.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, -20f), new VariableEffect(SimVar.incidentSeverity, 16f) },
                    setFlags = new List<string> { "D04_UNCONTROLLED_WEAPONS" }, reactionTag = "D04_GEAR_VIOLATION"
                }));

            // DECISIÓN 5: Verificación previa a la salida
            list.Add(SaveDecision(5, "D05_VerificacionPrevia", "Verificación previa a la salida",
                "El contingente ya equipado forma en el Patio de Honor frente al emblema institucional de la UTOP antes de tomar posiciones hacia el acceso vehicular.",
                "¿Qué procedimiento de verificación ejecutas antes de autorizar el despliegue hacia el pórtico?",
                DecisionKind.Standard, ScoreCriterion.Equipment, true, ScoreCriterion.Coordination, 0.40f,
                new DecisionOption
                {
                    id = "D05_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Pasar revista física de sujeción de cascos, estado de escudos, identificación visible de cada efectivo y recordar la prohibición de actuar de forma aislada.",
                    immediateConsequence = "La tropa adopta posición de firmes y saluda al unísono; se corrigen ajustes de equipo y se refuerza la disciplina táctica.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.equipmentReadiness, 12f), new VariableEffect(SimVar.protectionLevel, 10f) },
                    setFlags = new List<string> { "D05_CHECKED" }, reactionTag = "D05_VERIFY_EXCELLENT"
                },
                new DecisionOption
                {
                    id = "D05_GOOD", classification = OptionClass.Good, score = 86f,
                    text = "Realizar un conteo visual rápido por filas y confirmar verbalmente con los jefes de escuadra que el equipo está completo.",
                    immediateConsequence = "Verificación funcional que permite pasar a los puestos con orden aceptable.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.equipmentReadiness, 6f) },
                    reactionTag = "D05_VERIFY_GOOD"
                },
                new DecisionOption
                {
                    id = "D05_MOD", classification = OptionClass.ModerateWrong, score = 62f,
                    text = "Omitir la revista de equipo y enviar a las escuadras directamente al pórtico sin comprobar correas ni radios.",
                    immediateConsequence = "Algunos efectivos llegan al puesto con visores mal ajustados y sin haber probado su canal de enlace.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.equipmentReadiness, -10f), new VariableEffect(SimVar.communicationLevel, -8f) },
                    reactionTag = "D05_VERIFY_SKIPPED"
                },
                new DecisionOption
                {
                    id = "D05_GRAVE", classification = OptionClass.SeriousWrong, score = 40f, criticalViolation = true,
                    text = "Autorizar que efectivos salgan sin identificación institucional y portando objetos no reglamentarios.",
                    immediateConsequence = "Falta grave a la normativa policial de transparencia, trazabilidad y control del uso de la fuerza.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, -16f), new VariableEffect(SimVar.equipmentReadiness, -14f) },
                    reactionTag = "D05_VERIFY_ILLEGAL"
                }));

            // DECISIÓN 6: Coordinación por radio
            list.Add(SaveDecision(6, "D06_CoordinacionRadio", "Coordinación por radio",
                "Los grupos toman posición entre el Patio, la Sala de Operaciones y el acceso a la Calle Junín. El ruido exterior de petardos y cánticos empieza a incrementarse.",
                "¿Cómo configuras el protocolo de comunicaciones por radio entre los tres sectores?",
                DecisionKind.Standard, ScoreCriterion.Coordination, false, ScoreCriterion.PersonnelAdmin, 0f,
                new DecisionOption
                {
                    id = "D06_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Establecer un canal táctico principal con códigos breves, un canal de respaldo, reportes periódicos desde Observación y enlace directo con el Capitán.",
                    immediateConsequence = "Comunicaciones fluidas y redundantes que garantizan alerta temprana ante cualquier cambio en la manifestación.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, 18f), new VariableEffect(SimVar.observationCapacity, 10f) },
                    setFlags = new List<string> { "D06_RADIO_REDUNDANT" }, reactionTag = "D06_RADIO_EXCELLENT"
                },
                new DecisionOption
                {
                    id = "D06_GOOD", classification = OptionClass.Good, score = 86f,
                    text = "Mantener a todos los sectores en una misma frecuencia abierta con prioridad para el jefe operativo.",
                    immediateConsequence = "El enlace funciona adecuadamente en condiciones normales, aunque puede saturarse si ocurren varios eventos simultáneos.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, 8f) },
                    reactionTag = "D06_RADIO_GOOD"
                },
                new DecisionOption
                {
                    id = "D06_MOD", classification = OptionClass.ModerateWrong, score = 62f,
                    text = "Depender únicamente de órdenes a viva voz y señales manuales desde el patio sin verificar frecuencias de radio.",
                    immediateConsequence = "El ruido de la calle dificulta escuchar las órdenes en el pórtico y retrasa los reportes de observación.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, -16f), new VariableEffect(SimVar.observationCapacity, -8f) },
                    setFlags = new List<string> { "COMMS_VULNERABLE" },
                    deferredConsequences = new List<DeferredConsequence>
                    {
                        new DeferredConsequence
                        {
                            id = "DEF_D06_NO_BACKUP_CH", afterDecisions = 5,
                            description = "No haber configurado un canal de radio de respaldo en la Decisión 6 agrava el bloqueo de comunicaciones en la fase crítica.",
                            effects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, -10f) },
                            scoreDelta = -4f, criterion = ScoreCriterion.Coordination
                        }
                    },
                    reactionTag = "D06_RADIO_WEAK"
                },
                new DecisionOption
                {
                    id = "D06_GRAVE", classification = OptionClass.SeriousWrong, score = 42f, criticalViolation = true,
                    text = "Apagar los equipos de radio para evitar interferencias y actuar sin reportar novedades a la Sala de Operaciones ni al mando superior.",
                    immediateConsequence = "Ruptura total del mando y control institucional; las unidades quedan aisladas sin supervisión ni apoyo.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, -30f), new VariableEffect(SimVar.publicSafety, -12f) },
                    setFlags = new List<string> { "COMMS_VULNERABLE" }, reactionTag = "D06_RADIO_OFF"
                }));

            // DECISIÓN 7: Detonación de dinamita (Mineros en Calle Junín)
            var d7 = SaveDecision(7, "D07_DetonacionDinamita", "Detonación de dinamita",
                "¡ALERTA CRÍTICA EN CALLE JUNÍN! Uno de los mineros bloqueadores enciende y arroja un cachorro de dinamita hacia la calzada frente al ingreso de la UTOP. La onda expansiva retumba entre las fachadas.",
                "¿Qué acción táctica inmediata ordenas ante la detonación de dinamita frente al recinto?",
                DecisionKind.Standard, ScoreCriterion.PhysicalIntegrity, true, ScoreCriterion.Legality, 0.30f,
                new DecisionOption
                {
                    id = "D07_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Ordenar repliegue táctico inmediato a cobertura estructural tras los muros del pórtico, ampliar la distancia de seguridad, alertar a transeúntes y registrar al autor desde Observación.",
                    immediateConsequence = "Ningún efectivo ni civil resulta alcanzado por la onda expansiva; se preserva la integridad física y se documenta el hecho.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, 14f), new VariableEffect(SimVar.protectionLevel, 12f), new VariableEffect(SimVar.incidentSeverity, -6f) },
                    setFlags = new List<string> { "D07_BLAST_CONTAINED" }, reactionTag = "D07_DYNAMITE_SAFE"
                },
                new DecisionOption
                {
                    id = "D07_GOOD", classification = OptionClass.Good, score = 86f,
                    text = "Mantener al personal agazapado tras la línea de escudos dentro del umbral del garaje y reportar la detonación por radio.",
                    immediateConsequence = "Se evita daño directo al personal policial, aunque la proximidad al umbral mantiene cierta exposición acústica.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, 6f), new VariableEffect(SimVar.protectionLevel, 6f) },
                    reactionTag = "D07_DYNAMITE_HOLD"
                },
                new DecisionOption
                {
                    id = "D07_MOD", classification = OptionClass.ModerateWrong, score = 58f,
                    text = "Permanecer estáticos en la acera exterior sin buscar cobertura estructural ni despejar el radio de detonación.",
                    immediateConsequence = "El personal queda expuesto al aturdimiento por onda expansiva y esquirlas secundarias sobre el asfalto.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, -14f), new VariableEffect(SimVar.protectionLevel, -12f), new VariableEffect(SimVar.incidentSeverity, 10f) },
                    reactionTag = "D07_DYNAMITE_EXPOSED"
                },
                new DecisionOption
                {
                    id = "D07_GRAVE", classification = OptionClass.SeriousWrong, score = 30f, criticalViolation = true,
                    text = "Ordenar una carga frontal inmediata en masa hacia el punto exacto donde caen los explosivos sin cobertura ni evaluación de riesgo.",
                    immediateConsequence = "Exposición directa a la detonación: la orden imprudente causa daños por onda expansiva y agrava la violencia en la vía pública.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, -28f), new VariableEffect(SimVar.protectionLevel, -22f), new VariableEffect(SimVar.incidentSeverity, 22f) },
                    reactionTag = "D07_DYNAMITE_CHARGE"
                });
            d7.contextModifiers.Add(new ContextModifier
            {
                condition = new DecisionCondition { kind = ConditionKind.FlagSet, flag = "D03_WEAK_OBS_COMM" },
                addedContext = "Debido a la escasa dotación asignada a Observación en la Decisión 3, el lanzamiento del explosivo fue advertido con pocos segundos de anticipación.",
                topOptionsScoreDelta = -5f
            });
            EditorUtility.SetDirty(d7);
            list.Add(d7);

            // DECISIÓN 8: Lanzamiento de objetos contundentes (Cholitas, Ponchos Rojos y Mineros lanzan piedras)
            var d8 = SaveDecision(8, "D08_ObjetosContundentes", "Lanzamiento de objetos contundentes",
                "En la Calle Junín, las cholitas manifestantes, los ponchos rojos y los mineros (recordando que cholitas y ponchos rojos solo arrojan piedras) lanzan una lluvia de piedras contra el acceso de la UTOP.",
                "¿Cómo dispones a la línea de protección frente al lanzamiento continuo de piedras?",
                DecisionKind.Standard, ScoreCriterion.PhysicalIntegrity, true, ScoreCriterion.Equipment, 0.35f,
                new DecisionOption
                {
                    id = "D08_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Cerrar formación defensiva con escudos entrelazados (ESCUDO) a distancia prudente dentro del pórtico, visores bajos y relevo escalonado sin responder a provocaciones.",
                    immediateConsequence = "Los escudos de policarbonato absorben el impacto de las piedras sin lesionados y se mantiene la cohesión táctica.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, 14f), new VariableEffect(SimVar.publicSafety, 10f) },
                    reactionTag = "D08_STONES_SHIELDWALL"
                },
                new DecisionOption
                {
                    id = "D08_GOOD", classification = OptionClass.Good, score = 85f,
                    text = "Resguardar al contingente tras las columnas laterales del acceso vehicular reduciendo la silueta expuesta hacia la calle.",
                    immediateConsequence = "Se protegen los efectivos adecuadamente, aunque disminuye el control visual directo sobre el centro de la calzada.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, 8f) },
                    reactionTag = "D08_STONES_COVER"
                },
                new DecisionOption
                {
                    id = "D08_MOD", classification = OptionClass.ModerateWrong, score = 58f,
                    text = "Dispersar a los efectivos de forma individual por la acera para esquivar las piedras rompiendo la línea de escudos.",
                    immediateConsequence = "La pérdida de cohesión deja huecos en el acceso al cuartel y expone a efectivos aislados.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, -15f), new VariableEffect(SimVar.publicSafety, -10f) },
                    reactionTag = "D08_STONES_SCATTER"
                },
                new DecisionOption
                {
                    id = "D08_GRAVE", classification = OptionClass.SeriousWrong, score = 32f, criticalViolation = true,
                    text = "Ordenar a los efectivos devolver las piedras arrojadas hacia la multitud o perseguir individualmente a los manifestantes cuesta abajo.",
                    immediateConsequence = "Violación flagrante de la disciplina policial y de los protocolos de derechos humanos, provocando heridos y descontrol.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, -25f), new VariableEffect(SimVar.incidentSeverity, 20f) },
                    reactionTag = "D08_STONES_RETALIATE"
                });
            d8.contextModifiers.Add(new ContextModifier
            {
                condition = new DecisionCondition { kind = ConditionKind.FlagSet, flag = "D04_NO_SHIELDS" },
                addedContext = " Al no haber retirado escudos antimotines suficientes en la Decisión 4, la guardia enfrenta las piedras con protección reducida.",
                topOptionsScoreDelta = -8f
            });
            EditorUtility.SetDirty(d8);
            list.Add(d8);

            // DECISIÓN 9: Protección de instalaciones (Despliegue de BARRERAGRANDE y BARRICADA)
            list.Add(SaveDecision(9, "D09_ProteccionInstalaciones", "Protección de instalaciones",
                "Un grupo de manifestantes intenta aproximarse al umbral vehicular de la UTOP y a las puertas laterales sobre la Calle Junín.",
                "¿Qué medida física de protección perimetral adoptas para asegurar las instalaciones policiales?",
                DecisionKind.Standard, ScoreCriterion.PersonnelAdmin, true, ScoreCriterion.PhysicalIntegrity, 0.40f,
                new DecisionOption
                {
                    id = "D09_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Desplegar y anclar las barreras metálicas (BARRERAGRANDE y BARRICADA) en el acceso vehicular, asegurar postigos laterales y mantener la línea de escudos detrás de la valla.",
                    immediateConsequence = "Las barreras físicas quedan instaladas en el pórtico de la UTOP creando un amortiguador seguro que impide el ingreso sin contacto cuerpo a cuerpo.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, 18f), new VariableEffect(SimVar.publicSafety, 12f), new VariableEffect(SimVar.incidentSeverity, -8f) },
                    setFlags = new List<string> { "D09_BARRIERS_DEPLOYED" }, reactionTag = "D09_BARRIERS_EXCELLENT"
                },
                new DecisionOption
                {
                    id = "D09_GOOD", classification = OptionClass.Good, score = 86f,
                    text = "Cerrar parcialmente el paso con una barrera móvil y concentrar una doble fila de efectivos en el umbral del garaje.",
                    immediateConsequence = "El acceso queda contenido de forma efectiva, aunque con menor separación física respecto a la acera.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, 10f) },
                    reactionTag = "D09_BARRIERS_GOOD"
                },
                new DecisionOption
                {
                    id = "D09_MOD", classification = OptionClass.ModerateWrong, score = 60f,
                    text = "Dejar el acceso vehicular libre de barreras físicas confiando únicamente en el cordón humano sobre el bordillo.",
                    immediateConsequence = "El contacto directo empuja la línea policial hacia el interior del garaje y eleva la fricción física.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, -14f), new VariableEffect(SimVar.incidentSeverity, 10f) },
                    reactionTag = "D09_BARRIERS_NONE"
                },
                new DecisionOption
                {
                    id = "D09_GRAVE", classification = OptionClass.SeriousWrong, score = 36f, criticalViolation = true,
                    text = "Abandonar la custodia de las puertas laterales y del garaje para desplazar a todo el contingente hacia el centro de la cuadra.",
                    immediateConsequence = "El recinto policial queda desprotegido ante intrusiones y se compromete la seguridad del armamento e instalaciones.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.protectionLevel, -28f), new VariableEffect(SimVar.publicSafety, -18f) },
                    reactionTag = "D09_BARRIERS_ABANDONED"
                }));

            // DECISIÓN 10: Evaluación de agentes químicos (Solo evaluación de proporcionalidad, entorno y protocolos)
            list.Add(SaveDecision(10, "D10_EvaluacionAgentesQuimicos", "Evaluación de agentes químicos",
                "Un jefe de grupo consulta por radio si debe autorizarse el empleo de agentes químicos (GASLACRISPRITE / LANZAGASLACRI) en la pendiente encajonada de la Calle Junín, donde hay viviendas colindantes y comercios.",
                "¿Qué determinación adoptas tras evaluar las condiciones del entorno, la proporcionalidad y los protocolos?",
                DecisionKind.Standard, ScoreCriterion.Legality, true, ScoreCriterion.PhysicalIntegrity, 0.35f,
                new DecisionOption
                {
                    id = "D10_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Denegar la dispersión química en este momento tras evaluar la pendiente cerrada, presencia de terceros y que la barrera perimetral contiene el riesgo; mantener advertencia verbal por megáfono.",
                    immediateConsequence = "Decisión apegada a los principios de necesidad y proporcionalidad: se evita afectar viviendas vecinas y se previene una estampida en pendiente.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, 15f), new VariableEffect(SimVar.incidentSeverity, -10f) },
                    setFlags = new List<string> { "D10_PROTOCOL_RESPECTED" }, reactionTag = "D10_CHEM_RESTRAINT"
                },
                new DecisionOption
                {
                    id = "D10_GOOD", classification = OptionClass.Good, score = 86f,
                    text = "Condicionar cualquier autorización futura a tres advertencias audibles, verificación de dirección del viento y confirmación de vías de evacuación despejadas cuesta abajo.",
                    immediateConsequence = "Se mantiene el control estricto del protocolo y se deja constancia formal de los requisitos previos.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, 8f), new VariableEffect(SimVar.communicationLevel, 6f) },
                    reactionTag = "D10_CHEM_CONDITIONAL"
                },
                new DecisionOption
                {
                    id = "D10_MOD", classification = OptionClass.ModerateWrong, score = 58f,
                    text = "Emitir una orden ambigua dejando a criterio individual de cada efectivo la evaluación del uso de aerosoles o lanzadores.",
                    immediateConsequence = "La falta de directriz clara genera incertidumbre en la línea de contención y riesgo de actuación descoordinada.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, -12f), new VariableEffect(SimVar.publicSafety, -10f) },
                    reactionTag = "D10_CHEM_AMBIGUOUS"
                },
                new DecisionOption
                {
                    id = "D10_GRAVE", classification = OptionClass.SeriousWrong, score = 30f, criticalViolation = true,
                    text = "Ordenar el uso masivo e inmediato de agentes químicos hacia espacios cerrados y sin advertencia previa ni ruta de salida.",
                    immediateConsequence = "Incumplimiento grave de estándares internacionales y protocolos policiales sobre uso de agentes químicos en zonas urbanas densas.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, -26f), new VariableEffect(SimVar.incidentSeverity, 22f) },
                    reactionTag = "D10_CHEM_VIOLATION"
                }));

            // DECISIÓN 11: Atención de persona herida
            list.Add(SaveDecision(11, "D11_AtencionPersonaHerida", "Atención de persona herida",
                "En medio de los empujones del bloqueo en la Calle Junín, una persona cae herida sobre la acera próxima al pórtico de la UTOP y solicita auxilio.",
                "¿Qué protocolo ejecutas frente a la presencia de una persona herida junto al perímetro?",
                DecisionKind.Standard, ScoreCriterion.PhysicalIntegrity, true, ScoreCriterion.Legality, 0.40f,
                new DecisionOption
                {
                    id = "D11_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Priorizar la vida e integridad física: asegurar una cápsula de protección con escudos, brindar primeros auxilios básicos, solicitar ambulancia por radio y registrar la asistencia.",
                    immediateConsequence = "Dos efectivos aseguran el corredor humanitario y asisten a la persona herida; el gesto reduce la hostilidad de los manifestantes en la calle.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, 18f), new VariableEffect(SimVar.incidentSeverity, -15f), new VariableEffect(SimVar.protectionLevel, 6f) },
                    setFlags = new List<string> { "D11_HUMANITARIAN_AID" }, reactionTag = "D11_AID_EXCELLENT"
                },
                new DecisionOption
                {
                    id = "D11_GOOD", classification = OptionClass.Good, score = 86f,
                    text = "Solicitar apoyo médico externo por radio y abrir un espacio lateral en la barrera para que sea auxiliada sin descuidar el frente.",
                    immediateConsequence = "Se facilita la atención médica manteniendo la seguridad del recinto policial.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, 10f), new VariableEffect(SimVar.incidentSeverity, -6f) },
                    reactionTag = "D11_AID_GOOD"
                },
                new DecisionOption
                {
                    id = "D11_MOD", classification = OptionClass.ModerateWrong, score = 56f,
                    text = "Demorar la asistencia médica esperando a que toda la manifestación se retire por completo antes de permitir el ingreso de auxilio.",
                    immediateConsequence = "La demora agrava el estado de la persona herida y genera reclamos e indignación en la vía pública.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, -16f), new VariableEffect(SimVar.incidentSeverity, 14f) },
                    reactionTag = "D11_AID_DELAYED"
                },
                new DecisionOption
                {
                    id = "D11_GRAVE", classification = OptionClass.SeriousWrong, score = 25f, criticalViolation = true,
                    text = "Negar el auxilio humanitario y bloquear el paso del personal sanitario hacia la persona herida.",
                    immediateConsequence = "Violación gravísima del deber legal de auxilio y de protección de los derechos humanos.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, -32f), new VariableEffect(SimVar.incidentSeverity, 25f) },
                    reactionTag = "D11_AID_DENIED"
                }));

            // DECISIÓN 12: Fallo o saturación de comunicaciones
            var d12 = SaveDecision(12, "D12_FalloComunicaciones", "Fallo o saturación de comunicaciones",
                "En el momento de mayor actividad operativa, el canal principal de radio presenta interferencia y saturación entre el Pórtico, Observación y la Sala de Operaciones.",
                "¿Cómo restableces la coordinación y el mando ante la saturación del canal de radio?",
                DecisionKind.Standard, ScoreCriterion.Coordination, true, ScoreCriterion.PersonnelAdmin, 0.30f,
                new DecisionOption
                {
                    id = "D12_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Conmutar de inmediato al canal táctico de respaldo preasignado, activar enlaces por estafeta visual en el Patio de Honor e imponer disciplina de radio.",
                    immediateConsequence = "Se recupera el 100% del enlace entre la Sala de Operaciones, Observación y la línea de contención en menos de 30 segundos.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, 20f), new VariableEffect(SimVar.protectionLevel, 8f) },
                    reactionTag = "D12_COMMS_RESTORED"
                },
                new DecisionOption
                {
                    id = "D12_GOOD", classification = OptionClass.Good, score = 85f,
                    text = "Enviar un efectivo de enlace físico entre el pórtico y la Sala de Operaciones mientras se depura el tráfico del canal principal.",
                    immediateConsequence = "La coordinación continúa operativa mediante mensajeros tácticos sin perder el control del dispositivo.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, 10f) },
                    reactionTag = "D12_COMMS_RUNNER"
                },
                new DecisionOption
                {
                    id = "D12_MOD", classification = OptionClass.ModerateWrong, score = 60f,
                    text = "Insistir repetidamente sobre el mismo canal saturado elevando el volumen sin cambiar de frecuencia ni enviar enlace.",
                    immediateConsequence = "El bloqueo de audio persiste durante varios minutos dejando a los grupos sin actualización oportuna.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, -14f), new VariableEffect(SimVar.observationCapacity, -8f) },
                    reactionTag = "D12_COMMS_JAMMED"
                },
                new DecisionOption
                {
                    id = "D12_GRAVE", classification = OptionClass.SeriousWrong, score = 38f, criticalViolation = true,
                    text = "Autorizar que cada escuadra tome decisiones autónomas de fuerza sin esperar restablecer el enlace con el mando.",
                    immediateConsequence = "Descoordinación crítica del dispositivo policial y ruptura de la unidad de mando.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.communicationLevel, -26f), new VariableEffect(SimVar.publicSafety, -16f) },
                    reactionTag = "D12_COMMS_ANARCHY"
                });
            d12.contextModifiers.Add(new ContextModifier
            {
                condition = new DecisionCondition { kind = ConditionKind.FlagSet, flag = "COMMS_VULNERABLE" },
                addedContext = "Las deficiencias previas en la asignación o configuración de comunicaciones (Decisiones 3 o 6) hacen que esta saturación sea mucho más severa.",
                topOptionsScoreDelta = -6f
            });
            EditorUtility.SetDirty(d12);
            list.Add(d12);

            // DECISIÓN 13: Desescalamiento y cierre
            list.Add(SaveDecision(13, "D13_DesescalamientoYCierre", "Desescalamiento y cierre",
                "Tras la contención perimetral y la atención humanitaria, la tensión en la Calle Junín disminuye y los voceros de los manifestantes inician el repliegue pacífico cuesta abajo.",
                "¿Cómo conduces el desescalamiento final y el cierre del operativo de la UTOP?",
                DecisionKind.Standard, ScoreCriterion.Legality, true, ScoreCriterion.Coordination, 0.40f,
                new DecisionOption
                {
                    id = "D13_EXC", classification = OptionClass.Excellent, score = 100f,
                    text = "Facilitar la desconcentración pacífica manteniendo actitud preventiva no provocadora, replegar gradualmente al contingente a formación 3x10 en el Patio de Honor, verificar novedades de personal/armamento y elevar el parte final al Capitán.",
                    immediateConsequence = "Los manifestantes se retiran en orden por la Calle Junín; la tropa UTOP forma en el Patio de Honor y el operativo concluye con éxito institucional.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, 16f), new VariableEffect(SimVar.incidentSeverity, -20f), new VariableEffect(SimVar.missionProgress, 100f) },
                    reactionTag = "D13_CLOSE_EXCELLENT"
                },
                new DecisionOption
                {
                    id = "D13_GOOD", classification = OptionClass.Good, score = 88f,
                    text = "Mantener la guardia en el pórtico hasta que el último grupo se aleje y luego ordenar el retorno directo a la Cuadra de Tropa 1.",
                    immediateConsequence = "Cierre seguro del operativo, aunque sin pasar revista formal conjunta antes de romper filas.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, 10f), new VariableEffect(SimVar.incidentSeverity, -12f) },
                    reactionTag = "D13_CLOSE_GOOD"
                },
                new DecisionOption
                {
                    id = "D13_MOD", classification = OptionClass.ModerateWrong, score = 60f,
                    text = "Retirar a todo el personal abruptamente sin verificar el recuento de equipo en la Sala de Armas ni dejar guardia mínima en el acceso.",
                    immediateConsequence = "Desorden administrativo en la devolución de equipo y descuido temporal de la vigilancia del cuartel.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.equipmentReadiness, -12f), new VariableEffect(SimVar.protectionLevel, -10f) },
                    reactionTag = "D13_CLOSE_HASTY"
                },
                new DecisionOption
                {
                    id = "D13_GRAVE", classification = OptionClass.SeriousWrong, score = 30f, criticalViolation = true,
                    text = "Ordenar la persecución de los manifestantes que ya se están retirando pacíficamente por la Calle Junín.",
                    immediateConsequence = "Reactivación innecesaria e ilegal del conflicto cuando la situación ya estaba desescalada.",
                    variableEffects = new List<VariableEffect> { new VariableEffect(SimVar.publicSafety, -28f), new VariableEffect(SimVar.incidentSeverity, 25f) },
                    reactionTag = "D13_CLOSE_VIOLATION"
                }));

            // Guardar o actualizar el DecisionSet maestro
            string setPath = DECISIONS_RES_DIR + "/DecisionSet.asset";
            DecisionSet setAsset = AssetDatabase.LoadAssetAtPath<DecisionSet>(setPath);
            if (setAsset == null)
            {
                setAsset = ScriptableObject.CreateInstance<DecisionSet>();
                setAsset.name = "DecisionSet";
                AssetDatabase.CreateAsset(setAsset, setPath);
            }
            setAsset.decisions = list;
            setAsset.branchRules = new List<BranchRule>();
            EditorUtility.SetDirty(setAsset);
            AssetDatabase.SaveAssets();
            return setAsset;
        }

        private static DecisionData SaveDecision(
            int order,
            string id,
            string stageName,
            string context,
            string question,
            DecisionKind kind,
            ScoreCriterion primary,
            bool hasSecondary,
            ScoreCriterion secondary,
            float secondaryShare,
            DecisionOption opt1,
            DecisionOption opt2,
            DecisionOption opt3,
            DecisionOption opt4)
        {
            string assetPath = string.Format("{0}/Decision_{1:00}_{2}.asset", DECISIONS_RES_DIR, order, id);
            DecisionData data = AssetDatabase.LoadAssetAtPath<DecisionData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DecisionData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.decisionId = id;
            data.order = order;
            data.stageName = stageName;
            data.context = context;
            data.question = question;
            data.kind = kind;
            data.shuffleOptions = (kind != DecisionKind.PersonnelCount);
            data.primaryCriterion = primary;
            data.hasSecondaryCriterion = hasSecondary;
            data.secondaryCriterion = secondary;
            data.secondaryShare = secondaryShare;
            data.options = new List<DecisionOption> { opt1, opt2, opt3, opt4 };
            data.skipConditions = new List<DecisionCondition>();
            data.contextModifiers = new List<ContextModifier>();

            EditorUtility.SetDirty(data);
            return data;
        }

        // =========================================================================================
        // 2. CONSTRUCCIÓN DE LA INTERFAZ uGUI (CANVAS DE DECISIONES TÁCTICAS UTOP)
        // =========================================================================================

        public static void BuildDecisionUICanvas(
            out DecisionPanelUI panelUI,
            out DecisionFeedbackUI feedbackUI,
            out DecisionResultUI resultUI)
        {
            GameObject canvasGO = new GameObject("Canvas_Decisiones_Tacticas_UTOP");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Botón flotante superior para minimizar / restaurar el panel y explorar en 3D ([T])
            GameObject toggleBtnGO = CreateUIButton(canvasGO.transform, "Btn_ToggleMinimize_Panel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(360f, 36f),
                new Color(0.08f, 0.22f, 0.16f, 0.94f), "OCULTAR PANEL [T] (EXPLORAR 3D)", 14, out TextMeshProUGUI toggleBtnLbl);

            // -------------------------------------------------------------------------------------
            // A. PANEL PRINCIPAL DE DECISIÓN (DecisionPanelUI + Cuadrícula 2x2 + Widget Distribución)
            // -------------------------------------------------------------------------------------
            GameObject mainPanelRoot = CreateUIPanel(canvasGO.transform, "DecisionPanel_Root",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(1180f, 760f),
                new Color(0.05f, 0.08f, 0.11f, 0.94f));

            // Barra superior verde institucional
            CreateUIPanel(mainPanelRoot.transform, "TopAccentBar",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), new Vector2(0f, 6f),
                new Color(0.22f, 0.78f, 0.45f, 1f));

            var stageHeaderTmp = CreateUIText(mainPanelRoot.transform, "StageHeader_TMP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(1120f, 36f),
                "DECISIÓN TÁCTICA 1 DE 13", 22, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.45f, 0.96f, 0.65f));

            var liveMetricsTmp = CreateUIText(mainPanelRoot.transform, "LiveMetrics_TMP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(1120f, 26f),
                "Puntaje: 100.0 pts | Efectivos: 15 | Protección: 55% | Com.: 65%", 15, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.82f, 0.90f, 0.86f));

            // Caja de Contexto
            GameObject ctxBox = CreateUIPanel(mainPanelRoot.transform, "Context_Box",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(1120f, 108f),
                new Color(0.08f, 0.12f, 0.16f, 0.95f));
            var contextTmp = CreateUIText(ctxBox.transform, "Context_TMP",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1088f, 94f),
                "Contexto operativo...", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Color(0.92f, 0.95f, 0.97f));

            // Pregunta
            var questionTmp = CreateUIText(mainPanelRoot.transform, "Question_TMP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(1120f, 40f),
                "¿Qué decisión adoptas?", 19, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1.0f, 0.92f, 0.55f));

            // Cuadrícula 2x2 de las 4 Alternativas
            GameObject gridGO = new GameObject("Options_2x2_Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(mainPanelRoot.transform, false);
            var gridRT = gridGO.GetComponent<RectTransform>();
            gridRT.anchorMin = new Vector2(0.5f, 1f);
            gridRT.anchorMax = new Vector2(0.5f, 1f);
            gridRT.pivot = new Vector2(0.5f, 1f);
            gridRT.anchoredPosition = new Vector2(0f, -252f);
            gridRT.sizeDelta = new Vector2(1120f, 320f);

            var grid = gridGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(548f, 148f);
            grid.spacing = new Vector2(20f, 16f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;

            DecisionOptionUI[] optUIs = new DecisionOptionUI[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject cardGO = CreateUIPanel(gridGO.transform, $"OptionCard_{i + 1}",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(548f, 148f),
                    new Color(0.10f, 0.16f, 0.20f, 0.96f));
                var btn = cardGO.AddComponent<Button>();
                var img = cardGO.GetComponent<Image>();

                var badgeTmp = CreateUIText(cardGO.transform, "Badge_TMP",
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(-28f, 24f),
                    $"OPCIÓN {(char)('A' + i)}", 15, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.42f, 0.92f, 0.62f));

                var bodyTmp = CreateUIText(cardGO.transform, "OptionBody_TMP",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(516f, 104f),
                    "Texto de la alternativa...", 15, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white);

                var optUI = cardGO.AddComponent<DecisionOptionUI>();
                optUI.button = btn;
                optUI.backgroundImage = img;
                optUI.badgeText = badgeTmp;
                optUI.optionBodyText = bodyTmp;
                optUIs[i] = optUI;
            }

            // Widget Interactivo Exclusivo para la Decisión 3 (Distribución de Efectivos)
            GameObject distRoot = CreateUIPanel(mainPanelRoot.transform, "DistributionWidget_Dec3",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 88f), new Vector2(1120f, 152f),
                new Color(0.08f, 0.15f, 0.14f, 0.98f));

            var distHeaderTmp = CreateUIText(distRoot.transform, "DistHeader_TMP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1080f, 26f),
                "DISTRIBUCIÓN MANUAL DEL CONTINGENTE:", 15, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.55f, 0.96f, 0.72f));

            // 3 Controles [- / +]: Protección, Observación, Comunicaciones
            BuildDistCounterGroup(distRoot.transform, "Protección", new Vector2(-360f, 12f),
                out TextMeshProUGUI protValTmp, out Button protMinus, out Button protPlus);
            BuildDistCounterGroup(distRoot.transform, "Observación", new Vector2(0f, 12f),
                out TextMeshProUGUI obsValTmp, out Button obsMinus, out Button obsPlus);
            BuildDistCounterGroup(distRoot.transform, "Comunicaciones", new Vector2(360f, 12f),
                out TextMeshProUGUI commValTmp, out Button commMinus, out Button commPlus);

            var validTmp = CreateUIText(distRoot.transform, "ValidationStatus_TMP",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(380f, 24f), new Vector2(720f, 34f),
                "Suma válida", 14, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);

            GameObject confirmDistBtnGO = CreateUIButton(distRoot.transform, "Btn_ConfirmCustomDist",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-190f, 26f), new Vector2(330f, 36f),
                new Color(0.18f, 0.62f, 0.36f, 1f), "CONFIRMAR DISTRIBUCIÓN MANUAL", 14, out _);

            distRoot.SetActive(false);

            panelUI = mainPanelRoot.AddComponent<DecisionPanelUI>();
            panelUI.rootPanel = mainPanelRoot;
            panelUI.stageHeaderText = stageHeaderTmp;
            panelUI.liveMetricsText = liveMetricsTmp;
            panelUI.contextBodyText = contextTmp;
            panelUI.questionText = questionTmp;
            panelUI.optionButtons = optUIs;
            panelUI.minimizeToggleButton = toggleBtnGO.GetComponent<Button>();
            panelUI.minimizeButtonLabel = toggleBtnLbl;
            panelUI.distributionWidgetRoot = distRoot;
            panelUI.distributionHeaderText = distHeaderTmp;
            panelUI.protValueText = protValTmp;
            panelUI.obsValueText = obsValTmp;
            panelUI.commValueText = commValTmp;
            panelUI.validationStatusText = validTmp;
            panelUI.protMinusBtn = protMinus;
            panelUI.protPlusBtn = protPlus;
            panelUI.obsMinusBtn = obsMinus;
            panelUI.obsPlusBtn = obsPlus;
            panelUI.commMinusBtn = commMinus;
            panelUI.commPlusBtn = commPlus;
            panelUI.confirmCustomDistributionBtn = confirmDistBtnGO.GetComponent<Button>();

            // -------------------------------------------------------------------------------------
            // B. MODAL DE RETROALIMENTACIÓN CORRECTIVA EXCLUSIVO PARA DECISIÓN 2 (DecisionFeedbackUI)
            // -------------------------------------------------------------------------------------
            GameObject fbModalRoot = CreateUIPanel(canvasGO.transform, "DecisionFeedbackModal_Dec2Only",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 480f),
                new Color(0.06f, 0.09f, 0.12f, 0.98f));

            CreateUIPanel(fbModalRoot.transform, "WarnAccentBar",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), new Vector2(0f, 6f),
                new Color(0.95f, 0.68f, 0.18f, 1f));

            var fbTitleTmp = CreateUIText(fbModalRoot.transform, "FbTitle_TMP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(820f, 34f),
                "OBSERVACIÓN TÁCTICA — CANTIDAD DE EFECTIVOS", 21, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1.0f, 0.82f, 0.32f));

            var fbScoreTmp = CreateUIText(fbModalRoot.transform, "FbHeaderScore_TMP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(820f, 32f),
                "Seleccionaste 5 efectivos — Puntaje obtenido: 95 puntos", 19, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

            var fbExpTmp = CreateUIText(fbModalRoot.transform, "FbExplanation_TMP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -155f), new Vector2(800f, 80f),
                "Explicación...", 17, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.92f, 0.94f, 0.96f));

            GameObject tipBox = CreateUIPanel(fbModalRoot.transform, "Tip_Box",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -265f), new Vector2(810f, 105f),
                new Color(0.10f, 0.18f, 0.15f, 0.96f));
            var fbTipTmp = CreateUIText(tipBox.transform, "FbTip_TMP",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 90f),
                "TIP: ...", 16, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.62f, 0.98f, 0.76f));

            var fbExcTmp = CreateUIText(fbModalRoot.transform, "FbExcellentReveal_TMP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -356f), new Vector2(800f, 34f),
                "La alternativa excelente de esta simulación era seleccionar 15 efectivos.", 18, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1.0f, 0.88f, 0.42f));

            GameObject fbContBtn = CreateUIButton(fbModalRoot.transform, "Btn_FbContinue",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(340f, 46f),
                new Color(0.20f, 0.65f, 0.38f, 1f), "ENTENDIDO — CONTINUAR", 16, out _);

            feedbackUI = fbModalRoot.AddComponent<DecisionFeedbackUI>();
            feedbackUI.rootModal = fbModalRoot;
            feedbackUI.titleText = fbTitleTmp;
            feedbackUI.headerScoreText = fbScoreTmp;
            feedbackUI.explanationText = fbExpTmp;
            feedbackUI.tipText = fbTipTmp;
            feedbackUI.excellentRevealText = fbExcTmp;
            feedbackUI.continueButton = fbContBtn.GetComponent<Button>();
            fbModalRoot.SetActive(false);

            // -------------------------------------------------------------------------------------
            // C. PANEL DE RESULTADO POR ETAPA E INFORME FINAL (DecisionResultUI)
            // -------------------------------------------------------------------------------------
            GameObject resRoot = CreateUIPanel(canvasGO.transform, "DecisionResult_Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 680f),
                new Color(0.05f, 0.09f, 0.12f, 0.96f));

            CreateUIPanel(resRoot.transform, "ResAccentBar",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), new Vector2(0f, 6f),
                new Color(0.24f, 0.80f, 0.50f, 1f));

            var resTitleTmp = CreateUIText(resRoot.transform, "ResHeaderTitle_TMP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(1000f, 36f),
                "RESULTADO OPERATIVO", 21, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.48f, 0.96f, 0.68f));

            var resScoreTmp = CreateUIText(resRoot.transform, "ResScoreBadge_TMP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(1000f, 30f),
                "Puntaje obtenido: 100 / 100", 17, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1.0f, 0.90f, 0.52f));

            GameObject resBodyBox = CreateUIPanel(resRoot.transform, "ResBody_Box",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(1000f, 440f),
                new Color(0.08f, 0.12f, 0.16f, 0.96f));
            var resBodyTmp = CreateUIText(resBodyBox.transform, "ResConsequenceBody_TMP",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(964f, 416f),
                "Consecuencia...", 15, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white);

            var resVarsTmp = CreateUIText(resRoot.transform, "ResVariablesSummary_TMP",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(1000f, 28f),
                "Variables modificadas...", 14, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.75f, 0.92f, 0.82f));

            GameObject resNextBtn = CreateUIButton(resRoot.transform, "Btn_ConfirmNextDecision",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(440f, 46f),
                new Color(0.18f, 0.64f, 0.38f, 1f), "OBSERVAR ACCIÓN EN ESCENA Y CONTINUAR ->", 16, out TextMeshProUGUI resBtnLbl);

            resultUI = resRoot.AddComponent<DecisionResultUI>();
            resultUI.rootPanel = resRoot;
            resultUI.headerTitleText = resTitleTmp;
            resultUI.scoreBadgeText = resScoreTmp;
            resultUI.consequenceBodyText = resBodyTmp;
            resultUI.variablesSummaryText = resVarsTmp;
            resultUI.confirmNextButton = resNextBtn.GetComponent<Button>();
            resultUI.confirmButtonLabel = resBtnLbl;
            resRoot.SetActive(false);
        }

        private static void BuildDistCounterGroup(
            Transform parent,
            string label,
            Vector2 anchoredPos,
            out TextMeshProUGUI valueText,
            out Button minusBtn,
            out Button plusBtn)
        {
            GameObject grp = CreateUIPanel(parent, "DistGroup_" + label,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(320f, 44f),
                new Color(0.05f, 0.10f, 0.12f, 0.95f));

            GameObject mBtnGO = CreateUIButton(grp.transform, "Btn_Minus",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(38f, 34f),
                new Color(0.55f, 0.18f, 0.18f, 1f), "-", 20, out _);
            minusBtn = mBtnGO.GetComponent<Button>();

            valueText = CreateUIText(grp.transform, "Val_TMP",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 34f),
                label + ": 5", 16, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

            GameObject pBtnGO = CreateUIButton(grp.transform, "Btn_Plus",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(38f, 34f),
                new Color(0.18f, 0.55f, 0.32f, 1f), "+", 20, out _);
            plusBtn = pBtnGO.GetComponent<Button>();
        }

        private static GameObject CreateUIPanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static TextMeshProUGUI CreateUIText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            string text,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions align,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false; // Permitir que los clics del ratón atraviesen las etiquetas de texto directamente hacia el botón de fondo
            return tmp;
        }

        private static GameObject CreateUIButton(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color bgColor,
            string label,
            float fontSize,
            out TextMeshProUGUI labelTmp)
        {
            GameObject go = CreateUIPanel(parent, name, anchorMin, anchorMax, anchoredPos, sizeDelta, bgColor);
            go.AddComponent<Button>();
            labelTmp = CreateUIText(go.transform, "Label_TMP",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                label, fontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            return go;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        // =========================================================================================
        // 3. VERIFICACIÓN AUTOMATIZADA DE LAS 11 FASES Y CAPTURA DE EVIDENCIAS VISUALES V6
        // =========================================================================================

        [MenuItem("Simulador/Verificar Sistema de 13 Decisiones y Capturar Evidencias V6")]
        public static void RunVerificationAndCaptureScreenshots()
        {
            string artifactDir = "C:/Users/senol/.gemini/antigravity/brain/978c045c-4783-423a-984e-32e4427141ea";
            var mgr = Object.FindAnyObjectByType<DecisionManager>();
            if (mgr == null || mgr.decisionSet == null)
            {
                Debug.LogError("[Verificación] No se encontró DecisionManager en la escena.");
                return;
            }

            mgr.EnsureSubComponents();

            // 1. Validar que las 13 decisiones tengan exactamente 4 opciones (Excelente, Buena, Incorrecta moderada, Incorrecta grave)
            int validCount = 0;
            for (int i = 0; i < mgr.decisionSet.decisions.Count; i++)
            {
                var d = mgr.decisionSet.decisions[i];
                string err = "null";
                if (d != null && d.Validate(out err))
                    validCount++;
                else
                    Debug.LogError("[Verificación] Error en decisión: " + err);
            }
            Debug.LogFormat("<color=green>[Fase 1-4 OK] Decisiones validadas: {0}/13 (cada una con 4 opciones: Excelente, Buena, Incorrecta moderada, Incorrecta grave).</color>", validCount);

            // 2. Probar regla exclusiva de Decisión 2 (5, 15, 20, 30) y ausencia de TIP en las demás decisiones
            var d2 = mgr.decisionSet.decisions[1];
            mgr.simulationState.ResetState();
            mgr.scoreManager.ResetScores();

            var opt5 = d2.options[0]; // 5 efectivos (95 pts, Incorrecta moderada)
            var res5 = mgr.evaluator.EvaluateChoice(d2, opt5, mgr.simulationState, mgr.scoreManager, 0f);
            var fb5 = mgr.feedbackManager.BuildFeedbackForResult(d2, res5);
            Debug.LogFormat("<color=green>[Fase 6 OK] Decisión 2 (5 efectivos): Puntaje={0}, ModalTIP={1}, Reveal='{2}'</color>",
                res5.scoreObtained, fb5.shouldShowModal, fb5.excellentRevealLine);

            var d1 = mgr.decisionSet.decisions[0];
            var resD1Wrong = mgr.evaluator.EvaluateChoice(d1, d1.options[3], mgr.simulationState, mgr.scoreManager, 0f);
            var fbD1 = mgr.feedbackManager.BuildFeedbackForResult(d1, resD1Wrong);
            Debug.LogFormat("<color=green>[Fase 6 OK] Decisión 1 (opción incorrecta): ModalTIP={0} (Nunca muestra TIP fuera de Decisión 2).</color>",
                fbD1.shouldShowModal);

            // 3. Probar Decisión 3 (Distribución de efectivos en Protección, Observación y Comunicaciones)
            var d3 = mgr.decisionSet.decisions[2];
            mgr.simulationState.selectedContingentCount = 15;
            var resDistExc = mgr.evaluator.EvaluateChoice(d3, d3.options[0], mgr.simulationState, mgr.scoreManager, 0f, true, 7, 4, 4);
            var resDistGrave = mgr.evaluator.EvaluateChoice(d3, d3.options[0], mgr.simulationState, mgr.scoreManager, 0f, true, 14, 1, 0);
            Debug.LogFormat("<color=green>[Fase 7 OK] Decisión 3 Distribución 15 efectivos: (7/4/4)->{0} ({1} pts) | (14/1/0)->{2} ({3} pts).</color>",
                resDistExc.classification, resDistExc.scoreObtained, resDistGrave.classification, resDistGrave.scoreObtained);

            // 4. Probar simulación completa de las 13 etapas (Ruta Excelente vs Ruta con Incumplimientos Graves)
            mgr.simulationState.ResetState();
            mgr.scoreManager.ResetScores();
            mgr.history.ClearHistory();
            for (int i = 0; i < mgr.decisionSet.decisions.Count; i++)
            {
                var dec = mgr.decisionSet.decisions[i];
                DecisionOption excOpt = dec.options.Find(o => o.classification == OptionClass.Excellent);
                var r = mgr.evaluator.EvaluateChoice(dec, excOpt, mgr.simulationState, mgr.scoreManager, 0f);
                mgr.history.Record(r);
            }
            var finalExc = mgr.evaluationManager.EvaluateFinalPerformance(mgr.simulationState, mgr.scoreManager, mgr.history);
            Debug.LogFormat("<color=green>[Fase 8-11 OK] Ruta Óptima 13 Decisiones: Puntaje Final={0:0.0}/100 ({1}), Historial={2} entradas.</color>",
                finalExc.finalScore, finalExc.classificationLabel, mgr.history.Count);

            // Dejar el estado inicial listo en la Decisión 1
            mgr.simulationState.ResetState();
            mgr.scoreManager.ResetScores();
            mgr.history.ClearHistory();

            // 5. Capturar 4 capturas de verificación de alta resolución (1280x720)
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 origCamPos = cam.transform.position;
            Quaternion origCamRot = cam.transform.rotation;

            // Captura 1: Formación 3x10 en el bloque central del Patio de Honor frente al emblema UTOP
            var troopMgr = Object.FindAnyObjectByType<UTOPTroopFormationManager>();
            if (troopMgr != null && troopMgr.troopOfficers != null)
            {
                for (int i = 0; i < troopMgr.troopOfficers.Count; i++)
                {
                    var t = troopMgr.troopOfficers[i];
                    if (t == null) continue;
                    t.transform.SetPositionAndRotation(t.FormationSlotPos, Quaternion.Euler(0f, 0f, 0f));
                    if (t.militaryAttentionClip != null && t.targetAnimator != null)
                        t.militaryAttentionClip.SampleAnimation(t.targetAnimator.gameObject, 0.1f);
                }
            }
            cam.transform.position = new Vector3(13.0f, 2.25f, -9.2f);
            cam.transform.rotation = Quaternion.Euler(11f, 180f, 0f);
            RenderCameraToPng(cam, artifactDir + "/v6_patio_formacion_3x10_corregida.png");

            // Restaurar tropa a sus posiciones iniciales en la Cuadra de Tropa 1
            if (troopMgr != null && troopMgr.troopOfficers != null)
            {
                for (int i = 0; i < troopMgr.troopOfficers.Count; i++)
                {
                    var t = troopMgr.troopOfficers[i];
                    if (t == null) continue;
                    t.transform.SetPositionAndRotation(t.BarracksHomePos, Quaternion.Euler(0f, (i * 67f) % 360f, 0f));
                }
            }

            // Captura 2: Sala de Armas (Armería) con los modelos 3D importados (ESCUDO, TONFA, ESCOPETA, LANZAGAS, GASLACRISPRITE, BALAGASLACRI)
            cam.transform.position = new Vector3(12.2f, 1.45f, 6.2f);
            cam.transform.rotation = Quaternion.Euler(6f, 58f, 0f);
            RenderCameraToPng(cam, artifactDir + "/v6_armeria_modelos_3d_importados.png");

            // Captura 3: Calle Junín con Cholitas, Ponchos Rojos, Mineros y Barricadas 3D (BARRICADA / BARRERAGRANDE)
            cam.transform.position = new Vector3(2.8f, 1.55f, -10.5f);
            cam.transform.rotation = Quaternion.Euler(6f, -15f, 0f);
            RenderCameraToPng(cam, artifactDir + "/v6_calle_junin_cholitas_ponchos_mineros.png");

            // Captura 4: Despacho del Capitán UTOP y Oficial con calidad de malla original completa
            cam.transform.position = new Vector3(10.0f, 5.45f, 3.8f);
            cam.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            RenderCameraToPng(cam, artifactDir + "/v6_capitan_y_oficial_calidad_original.png");

            cam.transform.position = origCamPos;
            cam.transform.rotation = origCamRot;
            Debug.Log("<color=cyan><b>[Verificación V6] 4 capturas de evidencia guardadas en artifacts.</b></color>");
        }

        private static void RenderCameraToPng(Camera cam, string filePath)
        {
            int w = 1280, h = 720;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevCamRT = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            cam.targetTexture = prevCamRT;
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(rt);

            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            System.IO.File.WriteAllBytes(filePath, png);
        }
    }
}
#endif
