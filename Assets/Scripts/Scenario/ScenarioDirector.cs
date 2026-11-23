using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimuladorBloqueos.Decisions;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Director escénico 3D que conecta las 13 decisiones tácticas con acciones automáticas y visibles
    /// de los NPCs (Capitán, 30 Policías UTOP, Mineros, Cholitas y Ponchos Rojos), la cinemática
    /// funcional de la Sala de Armas (Armería) y el despliegue de barreras (BARRERAGRANDE / BARRICADA).
    /// </summary>
    public class ScenarioDirector : MonoBehaviour
    {
        [Header("Referencias de Escena")]
        public UTOPCaptainNPCController captain;
        public UTOPTroopFormationManager troopFormationManager;
        public List<MinerBlockerNPCController> allProtesters = new List<MinerBlockerNPCController>();

        [Header("Plantillas 3D de Equipamiento Táctico (Armería)")]
        public GameObject shieldTemplate;
        public GameObject tonfaTemplate;
        public GameObject gasLauncherTemplate;
        public GameObject shotgunTemplate;
        public GameObject gasSprayTemplate;

        [Header("Barreras Desplegables en Acceso UTOP (Decisión 9)")]
        public List<GameObject> deployableEntranceBarriers = new List<GameObject>();

        [Header("Estado Escénico")]
        public string currentActionBanner = "";
        private float _bannerTimer = 0f;
        private readonly List<GameObject> _spawnedEquippedProps = new List<GameObject>();

        private void Awake()
        {
            RefreshSceneReferences();
        }

        public void RefreshSceneReferences()
        {
            if (captain == null)
                captain = FindAnyObjectByType<UTOPCaptainNPCController>();
            if (troopFormationManager == null)
                troopFormationManager = FindAnyObjectByType<UTOPTroopFormationManager>();

            if (allProtesters == null || allProtesters.Count == 0)
            {
                allProtesters = new List<MinerBlockerNPCController>(
                    FindObjectsByType<MinerBlockerNPCController>());
            }
        }

        private void Update()
        {
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.deltaTime;
                if (_bannerTimer <= 0f)
                    currentActionBanner = "";
            }
        }

        public void ShowWorldBanner(string text, float duration = 8f)
        {
            currentActionBanner = text;
            _bannerTimer = duration;
        }

        /// <summary>
        /// Escenifica el evento previo en el mundo 3D al abrirse una etapa de decisión.
        /// </summary>
        public void TriggerPreDecisionEvent(DecisionData decision, SimulationState state)
        {
            if (decision == null) return;
            RefreshSceneReferences();

            switch (decision.order)
            {
                case 1:
                    if (captain != null) captain.TriggerTacticalBriefingOrder();
                    ShowWorldBanner("ETAPA 1: El Capitán Comandante UTOP imparte la orden inicial en su despacho.", 7f);
                    break;

                case 7:
                    TriggerMinerDynamiteEvent();
                    ShowWorldBanner("¡ALERTA EN CALLE JUNÍN!: Mineros bloqueadores detonan dinamita frente al recinto UTOP.", 8f);
                    break;

                case 8:
                    TriggerStoneVolleyEvent();
                    ShowWorldBanner("¡ATAQUE CON OBJETOS CONTUNDENTES!: Cholitas, Ponchos Rojos y Mineros lanzan piedras hacia el acceso.", 8f);
                    break;

                case 11:
                    TriggerInjuredProtesterEvent();
                    ShowWorldBanner("INCIDENTE EN VÍA PÚBLICA: Una persona cae herida cerca de la línea de bloqueo en Calle Junín.", 8f);
                    break;
            }
        }

        /// <summary>
        /// Ejecuta las reacciones físicas de la tropa UTOP, la Sala de Armas y los manifestantes tras confirmar una decisión.
        /// </summary>
        public void ExecutePostDecisionReaction(DecisionData decision, DecisionResult result, SimulationState state)
        {
            if (decision == null || result == null || state == null) return;
            RefreshSceneReferences();

            switch (decision.order)
            {
                case 1: // Orden inicial del superior
                    if (captain != null) captain.TriggerTacticalBriefingOrder();
                    ShowWorldBanner("Orden inicial recibida. Dirígete al Patio de Honor para conformar el contingente UTOP.", 7f);
                    break;

                case 2: // Cantidad de efectivos (5, 15, 20 o 30)
                    DeploySelectedContingentToPatio(state.selectedContingentCount);
                    ShowWorldBanner(string.Format(
                        "CONTINGENTE CONFORMADO: {0} efectivos UTOP salen de la Cuadra 1 y forman en el centro del Patio de Honor.",
                        state.selectedContingentCount), 8f);
                    break;

                case 3: // Distribución de efectivos (Protección, Observación, Comunicaciones)
                    DistributeContingentToRoles(state.selectedContingentCount, state.distProtection, state.distObservation, state.distCommunications);
                    // Una vez distribuidos los efectivos en sus puestos, los manifestantes avanzan a la altura del coche rojo
                    SetProtestersSpawnAndActivity(false);
                    ShowWorldBanner(string.Format(
                        "DISTRIBUCIÓN EJECUTADA: Protección={0} | Observación={1} | Comunicaciones={2} se desplazan a sus puestos.",
                        state.distProtection, state.distObservation, state.distCommunications), 8f);
                    break;

                case 4: // Selección de equipamiento -> Cinemática funcional en la Sala de Armas (Armería)
                    StopAllCoroutines();
                    StartCoroutine(ArmoryEquippingSequenceCoroutine(result.classification, state.selectedContingentCount));
                    break;

                case 5: // Verificación previa a la salida
                    if (troopFormationManager != null)
                    {
                        DeploySelectedContingentToPatio(state.selectedContingentCount);
                        troopFormationManager.OnPlayerSalutedNearPatio();
                    }
                    ShowWorldBanner("VERIFICACIÓN PREVIA: El contingente equipado adopta posición de firmes y ejecuta saludo reglamentario.", 7f);
                    break;

                case 6: // Coordinación por radio
                    ShowWorldBanner("COORDINACIÓN POR RADIO: Enlace verificado entre Sala de Operaciones, Observación y Jefe Operativo.", 7f);
                    break;

                case 7: // Detonación de dinamita
                    SetProtestersSpawnAndActivity(true);
                    TriggerMinerDynamiteEvent();
                    if (result.classification == OptionClass.SeriousWrong)
                    {
                        // Lesión con causa identificable: exposición directa a onda expansiva por orden imprudente
                        state.attributableInjuriesCount++;
                        var playerAnim = FindAnyObjectByType<UTOPCharacterAnimationController>();
                        if (playerAnim != null) playerAnim.ApplyDamage(22f);
                        ShowWorldBanner("¡IMPACTO POR ONDA EXPANSIVA!: La orden de avance sin cobertura provocó daños por detonación.", 8f);
                    }
                    else
                    {
                        MoveProtectionLineToGate(state.distProtection, true);
                        ShowWorldBanner("CONTENCIÓN ANTE DINAMITA: El contingente mantiene distancia de seguridad y cobertura tras muros.", 8f);
                    }
                    break;

                case 8: // Lanzamiento de objetos contundentes (piedras por Cholitas, Ponchos Rojos y Mineros)
                    TriggerStoneVolleyEvent();
                    if (result.classification == OptionClass.SeriousWrong)
                    {
                        state.attributableInjuriesCount++;
                        var playerAnim = FindAnyObjectByType<UTOPCharacterAnimationController>();
                        if (playerAnim != null) playerAnim.ApplyDamage(15f);
                    }
                    MoveProtectionLineToGate(state.distProtection, true);
                    ShowWorldBanner("LÍNEA DE ESCUDOS ACTIVA: La guardia contiene el lanzamiento de piedras sin romper la formación.", 8f);
                    break;

                case 9: // Protección de instalaciones (despliegue físico de BARRERAGRANDE y BARRICADA)
                    ActivateEntranceBarriers(true);
                    MoveProtectionLineToGate(state.distProtection, true);
                    ShowWorldBanner("PROTECCIÓN DE INSTALACIONES: Barreras metálicas y barricadas desplegadas en el pórtico de la UTOP.", 8f);
                    break;

                case 10: // Evaluación de agentes químicos (solo evaluación protocolar y ambiental)
                    ShowWorldBanner("EVALUACIÓN DE AGENTES QUÍMICOS: Condiciones de viento, entorno cerrado y proporcionalidad registradas.", 7f);
                    break;

                case 11: // Atención de persona herida
                    TriggerInjuredProtesterEvent();
                    SendMedicalEscortPair();
                    ShowWorldBanner("ASISTENCIA HUMANITARIA: Efectivos aseguran perímetro sanitario para evacuar a la persona herida.", 8f);
                    break;

                case 12: // Fallo o saturación de comunicaciones
                    ShowWorldBanner("CONTINGENCIA DE COMUNICACIONES: Enlace alterno y mensajeros tácticos activados entre sectores.", 7f);
                    break;

                case 13: // Desescalamiento y cierre
                    TriggerDeescalationAndWithdrawal(state.selectedContingentCount);
                    ShowWorldBanner("DESESCALAMIENTO Y CIERRE: Los manifestantes se repliegan por Calle Junín y la tropa UTOP forma en el Patio.", 10f);
                    break;
            }
        }

        private void DeploySelectedContingentToPatio(int contingentCount)
        {
            if (troopFormationManager == null || troopFormationManager.troops == null) return;
            var troops = troopFormationManager.troops;
            Vector3 center = troopFormationManager.patioFormationCenter;
            float rowSpacing = troopFormationManager.rowSpacing;
            float colSpacing = troopFormationManager.colSpacing;
            Quaternion lookEmblem = Quaternion.Euler(0f, 0f, 0f);

            troopFormationManager.isFormedInPatio = true;

            for (int i = 0; i < troops.Count; i++)
            {
                var t = troops[i];
                if (t == null) continue;
                bool activeInContingent = (i < contingentCount);
                t.inContingent = activeInContingent;

                if (activeInContingent)
                {
                    int r = i / 10;
                    int c = i % 10;
                    float x = center.x + (c - 4.5f) * colSpacing;
                    float z = center.z - (r * rowSpacing);
                    t.CommandMoveToFormation(new Vector3(x, center.y, z), lookEmblem);
                }
                else
                {
                    t.CommandBreakFormation();
                }
            }
        }

        private void DistributeContingentToRoles(int total, int prot, int obs, int comm)
        {
            if (troopFormationManager == null || troopFormationManager.troops == null) return;
            var troops = troopFormationManager.troops;

            for (int i = 0; i < troops.Count; i++)
            {
                var t = troops[i];
                if (t == null) continue;
                if (i >= total)
                {
                    t.inContingent = false;
                    t.CommandBreakFormation();
                    continue;
                }

                t.inContingent = true;
                if (i < prot)
                {
                    // Grupo 1: Protección frente al acceso del Patio / Pórtico (Z = -11.8 a -13.2)
                    float colOffset = (i - (prot - 1) * 0.5f) * 1.05f;
                    Vector3 pos = new Vector3(Mathf.Clamp(11.5f + colOffset, 7.2f, 17.5f), -0.30f, -12.2f - (i % 2) * 0.9f);
                    t.CommandMoveToStation(pos, Quaternion.Euler(0f, 0f, 0f), true, null, "Protección");
                }
                else if (i < prot + obs)
                {
                    // Grupo 2: Observación y reporte hacia el pórtico y flancos (X = 7.2..9.5, Z = -9.5..-11.0)
                    int obsIdx = i - prot;
                    Vector3 pos = new Vector3(7.5f + (obsIdx % 4) * 1.1f, -0.30f, -9.8f - (obsIdx / 4) * 1.0f);
                    t.CommandMoveToStation(pos, Quaternion.Euler(0f, -35f, 0f), false, null, "Observación y Reporte");
                }
                else
                {
                    // Grupo 3: Coordinación y comunicaciones junto a Sala de Operaciones (X = 16.5..18.8, Z = -14.5..-17.0)
                    int commIdx = i - (prot + obs);
                    Vector3 pos = new Vector3(17.2f + (commIdx % 3) * 0.85f, -0.30f, -14.8f - (commIdx / 3) * 1.05f);
                    t.CommandMoveToStation(pos, Quaternion.Euler(0f, -90f, 0f), false, null, "Coordinación y Comunicaciones");
                }
            }
        }

        private IEnumerator ArmoryEquippingSequenceCoroutine(OptionClass optionClass, int contingentCount)
        {
            ShowWorldBanner("SALA DE ARMAS (ARMERÍA): Escuadra UTOP ingresa a la armería para retirar escudos, tonfas y equipo reglamentario...", 9f);

            if (troopFormationManager == null || troopFormationManager.troops == null)
                yield break;

            var troops = troopFormationManager.troops;
            int activeCount = Mathf.Min(contingentCount, troops.Count);
            int armorySquadCount = Mathf.Min(8, activeCount);

            // 1. Enviar a los primeros efectivos hacia el interior de la Sala de Armas / Armería (X = 10.5..17.0, Z = 7.0..8.6)
            for (int i = 0; i < armorySquadCount; i++)
            {
                var t = troops[i];
                if (t == null) continue;
                Vector3 rackPos = new Vector3(11.0f + (i % 4) * 1.65f, -0.30f, 7.1f + (i / 4) * 1.25f);
                Quaternion rackRot = Quaternion.Euler(0f, (i < 4) ? 180f : 0f, 0f);
                t.CommandMoveToStation(rackPos, rackRot, false, null, "Equipándose en Armería");
            }

            yield return new WaitForSeconds(3.2f);

            // 2. Equipar visualmente los modelos 3D importados (ESCUDO, TONFA, LANZAGASLACRI / ESCOPETA) en los huesos de las manos
            EquipContingentProps(optionClass, activeCount);

            ShowWorldBanner("EQUIPAMIENTO COMPLETADO: Los efectivos salen de la Sala de Armas con escudos y bastones policiales hacia el Patio.", 8f);

            // 3. Regresar equipados al Patio de Honor en formación lista para verificación (Decisión 5)
            DeploySelectedContingentToPatio(contingentCount);
        }

        public void EquipContingentProps(OptionClass equipmentChoice, int activeCount)
        {
            // Limpiar props instanciados previamente en manos
            for (int i = 0; i < _spawnedEquippedProps.Count; i++)
            {
                if (_spawnedEquippedProps[i] != null)
                    Destroy(_spawnedEquippedProps[i]);
            }
            _spawnedEquippedProps.Clear();

            if (troopFormationManager == null || troopFormationManager.troops == null) return;
            var troops = troopFormationManager.troops;
            int limit = Mathf.Min(activeCount, troops.Count);

            for (int i = 0; i < limit; i++)
            {
                var t = troops[i];
                if (t == null) continue;

                t.hasShieldEquipped = false;
                t.hasTwoHandedWeaponEquipped = false;

                // En opciones Excelente/Buena la primera línea lleva Escudo antimotines (LeftHand) y Tonfa (RightHand)
                bool giveShield = (equipmentChoice != OptionClass.ModerateWrong) && (i < Mathf.Max(4, limit / 2));
                bool giveTonfa = (equipmentChoice != OptionClass.ModerateWrong);
                bool giveLauncher = (equipmentChoice == OptionClass.Excellent || equipmentChoice == OptionClass.Good) && (i == limit - 1);
                bool giveShotgun = (equipmentChoice == OptionClass.SeriousWrong) && (i % 3 == 0);

                if (giveShield && shieldTemplate != null)
                {
                    GameObject sh = Instantiate(shieldTemplate);
                    sh.name = "Equipped_ESCUDO";
                    sh.SetActive(true);
                    // Escudo colocado al frente protegiéndose
                    t.Equip(sh, "LeftForeArm", new Vector3(0.08f, 0.16f, 0.18f), new Vector3(0f, 95f, -10f), 0.80f);
                    t.hasShieldEquipped = true;
                    _spawnedEquippedProps.Add(sh);
                }

                if (giveShotgun && shotgunTemplate != null)
                {
                    GameObject sg = Instantiate(shotgunTemplate);
                    sg.name = "Equipped_ESCOPETA";
                    sg.SetActive(true);
                    // Agarrado con orientación de arma táctica al frente a 2 manos
                    t.Equip(sg, "RightHand", new Vector3(-0.04f, 0.14f, 0.24f), new Vector3(-85f, 0f, 0f), 0.44f);
                    t.hasTwoHandedWeaponEquipped = true;
                    _spawnedEquippedProps.Add(sg);
                }
                else if (giveLauncher && gasLauncherTemplate != null)
                {
                    GameObject gl = Instantiate(gasLauncherTemplate);
                    gl.name = "Equipped_LANZAGAS";
                    gl.SetActive(true);
                    // Agarrado con orientación de lanzagas al frente a 2 manos
                    t.Equip(gl, "RightHand", new Vector3(-0.04f, 0.12f, 0.22f), new Vector3(-85f, 0f, 0f), 0.44f);
                    t.hasTwoHandedWeaponEquipped = true;
                    _spawnedEquippedProps.Add(gl);
                }
                else if (giveTonfa && tonfaTemplate != null)
                {
                    GameObject tf = Instantiate(tonfaTemplate);
                    tf.name = "Equipped_TONFA";
                    tf.SetActive(true);
                    t.Equip(tf, "RightHand", new Vector3(0.03f, 0.05f, 0.06f), new Vector3(90f, 0f, 0f), 0.85f);
                    _spawnedEquippedProps.Add(tf);
                }
            }
        }

        private void MoveProtectionLineToGate(int protCount, bool holdLine)
        {
            if (troopFormationManager == null || troopFormationManager.troops == null) return;
            var troops = troopFormationManager.troops;
            int count = Mathf.Clamp(protCount, 3, Mathf.Min(12, troops.Count));

            for (int i = 0; i < count; i++)
            {
                var t = troops[i];
                if (t == null) continue;
                float x = 5.4f + (i % 4) * 0.95f;
                float z = -8.2f - (i / 4) * 1.15f;
                t.CommandMoveToStation(new Vector3(x, -0.30f, z), Quaternion.Euler(0f, -65f, 0f), holdLine, null, "Línea de Contención en Pórtico");
            }
        }

        public void ActivateEntranceBarriers(bool active)
        {
            if (deployableEntranceBarriers == null) return;
            for (int i = 0; i < deployableEntranceBarriers.Count; i++)
            {
                if (deployableEntranceBarriers[i] != null)
                    deployableEntranceBarriers[i].SetActive(active);
            }
        }

        private void TriggerMinerDynamiteEvent()
        {
            for (int i = 0; i < allProtesters.Count; i++)
            {
                var p = allProtesters[i];
                if (p != null && p.protesterType == ProtesterType.Minero)
                {
                    p.ScriptedThrow(true); // Solo los mineros pueden lanzar dinamita
                }
            }
        }

        private void TriggerStoneVolleyEvent()
        {
            for (int i = 0; i < allProtesters.Count; i++)
            {
                var p = allProtesters[i];
                if (p != null)
                {
                    // Cholitas, Ponchos Rojos y Mineros lanzan piedras (useDynamite = false)
                    p.ScriptedThrow(false);
                }
            }
        }

        private void TriggerInjuredProtesterEvent()
        {
            for (int i = 0; i < allProtesters.Count; i++)
            {
                var p = allProtesters[i];
                if (p != null && p.protesterType != ProtesterType.Minero)
                {
                    p.ScriptedInjury("commotion");
                    break;
                }
            }
        }

        private void SendMedicalEscortPair()
        {
            if (troopFormationManager == null || troopFormationManager.troops == null) return;
            var troops = troopFormationManager.troops;
            if (troops.Count >= 2)
            {
                if (troops[0] != null)
                    troops[0].CommandMoveToStation(new Vector3(5.6f, -0.30f, -7.2f), Quaternion.Euler(0f, -90f, 0f), false, null, "Asistencia Sanitaria");
                if (troops[1] != null)
                    troops[1].CommandMoveToStation(new Vector3(6.8f, -0.30f, -7.2f), Quaternion.Euler(0f, -90f, 0f), true, null, "Cobertura Sanitaria");
            }
        }

        private void TriggerDeescalationAndWithdrawal(int contingentCount)
        {
            // Los manifestantes se repliegan ordenadamente por Calle Junín
            for (int i = 0; i < allProtesters.Count; i++)
            {
                var p = allProtesters[i];
                if (p != null)
                {
                    Vector3 retreatTarget = new Vector3(-1.5f + (i % 3) * 1.4f, 0f, -38f - i * 2.2f);
                    p.ScriptedFlee(retreatTarget);
                }
            }

            // La tropa UTOP se reagrupa en formación 3x10 en el Patio de Honor
            DeploySelectedContingentToPatio(contingentCount);
        }

        public void InitializeScenarioStartState()
        {
            RefreshSceneReferences();

            // Los manifestantes inician ocultos o agrupados a la altura del coche rojo de Calle Junín (Z = 24m..28m)
            // y no frente al recinto de la UTOP, con lanzamientos pausados cada 45 a 60 segundos
            SetProtestersSpawnAndActivity(false);
        }

        public void SetProtestersSpawnAndActivity(bool activeNearGate)
        {
            RefreshSceneReferences();
            for (int i = 0; i < allProtesters.Count; i++)
            {
                var p = allProtesters[i];
                if (p == null) continue;

                if (!activeNearGate)
                {
                    // Agrupados a la altura del coche rojo (Z = 24m..27m) mirando hacia la subida de la UTOP
                    float roadY = -25.5f * 0.105f; // GetRoadY
                    Vector3 redCarArea = new Vector3(-1.8f + (i % 3) * 1.5f, roadY, 24.5f + (i / 3) * 2.2f);
                    p.SetHomePosition(redCarArea);
                    p.transform.position = redCarArea;
                    p.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Mirando hacia la UTOP
                    p.SetMood(ProtesterMood.Calm);
                    p.autoThrowIntervalMin = 45f;
                    p.autoThrowIntervalMax = 60f;
                    p.SetScenarioTarget(new Vector3(4.8f, 0.2f, -6.2f));
                }
                else
                {
                    p.SetMood(ProtesterMood.Tense);
                    p.autoThrowIntervalMin = 35f;
                    p.autoThrowIntervalMax = 50f;
                }
            }
        }

        public void OrderTroopFireTearGasAtProtesters()
        {
            RefreshSceneReferences();
            if (troopFormationManager == null || troopFormationManager.troops == null) return;

            UTOPTroopNPCController gasTroop = null;
            for (int i = 0; i < troopFormationManager.troops.Count; i++)
            {
                var t = troopFormationManager.troops[i];
                if (t != null && t.inContingent)
                {
                    gasTroop = t;
                    break;
                }
            }

            if (gasTroop != null)
            {
                Vector3 origin = gasTroop.transform.position + Vector3.up * 1.5f;
                Vector3 target = new Vector3(0.5f, 0.2f, 12f); // Hacia la línea de manifestantes en Calle Junín
                Vector3 vel = (target - origin).normalized * 18f + Vector3.up * 4.5f;
                PlayerInventorySystem.SpawnTearGasGrenadeProjectile(origin, vel, gasLauncherTemplate);
                ShowWorldBanner("ORDEN EJECUTADA: La tropa UTOP efectúa disparo de gas lacrimógeno con LANZAGASLACRI hacia la calzada.", 7f);
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(currentActionBanner)) return;

            float w = Mathf.Min(820f, Screen.width - 40f);
            float h = 42f;
            float x = (Screen.width - w) * 0.5f;
            float y = 14f;

            GUI.color = new Color(0.05f, 0.11f, 0.09f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            GUI.color = new Color(0.25f, 0.85f, 0.48f, 1f);
            GUI.DrawTexture(new Rect(x, y, 5f, h), Texture2D.whiteTexture);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.95f, 0.98f, 0.92f) }
            };

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 2f, w - 24f, h - 4f), currentActionBanner, style);
        }
    }
}
