using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SimuladorBloqueos
{
    /// <summary>
    /// Construye y gestiona todo el escenario de la simulación:
    /// - Suelo y bordes estilizados de la arena.
    /// - 1 Jugador humanoide articulado con cámara en 3ra persona y controles WASD + Shift.
    /// - 4 Personajes humanoides articulados autónomos con IA.
    /// - Materiales y estética visual.
    /// </summary>
    public class SimulationArenaManager : MonoBehaviour
    {
        [Header("Configuración")]
        public bool buildOnStart = true;
        public float floorSize = 48f;

        private void Start()
        {
            if (buildOnStart)
            {
                BuildCompleteSimulation();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Reconstruir Escenario")]
        public void RebuildFromEditor()
        {
            BuildCompleteSimulation();
        }

        [MenuItem("Simulador/Construir Escenario Completo")]
        public static void BuildMenuAction()
        {
            var manager = Object.FindAnyObjectByType<SimulationArenaManager>();
            if (manager == null)
            {
                var go = new GameObject("SimulationArenaManager");
                manager = go.AddComponent<SimulationArenaManager>();
            }
            manager.BuildCompleteSimulation();
        }
#endif

        public void BuildCompleteSimulation()
        {
            // Limpiar elementos previos si existen
            CleanPreviousElements();

            // 1. Crear Materiales URP
            var materials = CreateMaterials();

            // 2. Construir Suelo y Arena
            BuildArenaFloor(materials["Floor"], materials["Border"], materials["Accent"]);

            // 3. Crear Jugador (Persona 1)
            var playerObj = CreateCharacter(
                "Player_1",
                new Vector3(0, 0, 0),
                Quaternion.identity,
                materials["PlayerBody"],
                materials["Joints"],
                materials["Accent"],
                materials["PlayerVisor"],
                isPlayer: true
            );

            // 4. Configurar Cámara en 3ra Persona
            SetupThirdPersonCamera(playerObj.transform);

            // 5. Crear 4 Personajes con IA (Personas 2 a 5)
            Vector3[] aiPositions = new Vector3[]
            {
                new Vector3(-7f, 0, 6f),
                new Vector3(7f, 0, 6f),
                new Vector3(-7f, 0, -6f),
                new Vector3(7f, 0, -6f)
            };

            string[] aiNames = new string[] { "AI_Persona_1_Esmeralda", "AI_Persona_2_Rubi", "AI_Persona_3_Amatista", "AI_Persona_4_Ambar" };
            string[] aiMatKeys = new string[] { "AI_Emerald", "AI_Crimson", "AI_Amethyst", "AI_Amber" };

            for (int i = 0; i < 4; i++)
            {
                float rotY = Random.Range(0f, 360f);
                CreateCharacter(
                    aiNames[i],
                    aiPositions[i],
                    Quaternion.Euler(0, rotY, 0),
                    materials[aiMatKeys[i]],
                    materials["Joints"],
                    materials["Accent"],
                    materials["AIVisor"],
                    isPlayer: false
                );
            }

            // 6. Añadir interfaz HUD
            if (GetComponent<SimulationHUD>() == null)
            {
                gameObject.AddComponent<SimulationHUD>();
            }

            Debug.Log("<color=cyan><b>[SimuladorBloqueos]</b> Escenario construido con éxito: Suelo + 1 Jugador + 4 Personas con IA articuladas.</color>");
        }

        private void CleanPreviousElements()
        {
            string[] namesToClean = new string[] { "ArenaRoot", "Player_1", "AI_Persona_1_Esmeralda", "AI_Persona_2_Rubi", "AI_Persona_3_Amatista", "AI_Persona_4_Ambar" };
            foreach (var name in namesToClean)
            {
                var existing = GameObject.Find(name);
                if (existing != null)
                {
                    DestroyImmediate(existing);
                }
            }
        }

        private Dictionary<string, Material> CreateMaterials()
        {
            var dict = new Dictionary<string, Material>();
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            // Suelo (Gris oscuro moderno y limpio)
            var floorMat = new Material(urpLit);
            floorMat.name = "Mat_ArenaFloor";
            floorMat.color = new Color(0.18f, 0.20f, 0.23f);
            if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", 0.45f);
            dict["Floor"] = floorMat;

            // Bordes y pedestales
            var borderMat = new Material(urpLit);
            borderMat.name = "Mat_ArenaBorder";
            borderMat.color = new Color(0.28f, 0.31f, 0.36f);
            if (borderMat.HasProperty("_Smoothness")) borderMat.SetFloat("_Smoothness", 0.6f);
            dict["Border"] = borderMat;

            // Articulaciones metálicas (Grafito / cromo oscuro)
            var jointMat = new Material(urpLit);
            jointMat.name = "Mat_Joints";
            jointMat.color = new Color(0.12f, 0.12f, 0.14f);
            if (jointMat.HasProperty("_Metallic")) jointMat.SetFloat("_Metallic", 0.85f);
            if (jointMat.HasProperty("_Smoothness")) jointMat.SetFloat("_Smoothness", 0.75f);
            dict["Joints"] = jointMat;

            // Acento metálico
            var accentMat = new Material(urpLit);
            accentMat.name = "Mat_Accent";
            accentMat.color = new Color(0.35f, 0.38f, 0.42f);
            if (accentMat.HasProperty("_Metallic")) accentMat.SetFloat("_Metallic", 0.7f);
            if (accentMat.HasProperty("_Smoothness")) accentMat.SetFloat("_Smoothness", 0.65f);
            dict["Accent"] = accentMat;

            // Jugador (Azul Cian Eléctrico)
            var playerMat = new Material(urpLit);
            playerMat.name = "Mat_Player";
            playerMat.color = new Color(0.08f, 0.58f, 0.98f);
            if (playerMat.HasProperty("_Smoothness")) playerMat.SetFloat("_Smoothness", 0.55f);
            dict["PlayerBody"] = playerMat;

            // Visor Jugador (Cian brillante)
            var playerVisor = new Material(urpLit);
            playerVisor.name = "Mat_PlayerVisor";
            playerVisor.color = new Color(0.2f, 0.95f, 1f);
            if (playerVisor.HasProperty("_EmissionColor"))
            {
                playerVisor.EnableKeyword("_EMISSION");
                playerVisor.SetColor("_EmissionColor", new Color(0.2f, 0.95f, 1f) * 1.5f);
            }
            dict["PlayerVisor"] = playerVisor;

            // IA 1 (Esmeralda)
            var aiEmerald = new Material(urpLit);
            aiEmerald.name = "Mat_AI_Emerald";
            aiEmerald.color = new Color(0.12f, 0.78f, 0.44f);
            if (aiEmerald.HasProperty("_Smoothness")) aiEmerald.SetFloat("_Smoothness", 0.55f);
            dict["AI_Emerald"] = aiEmerald;

            // IA 2 (Rubí / Carmesí)
            var aiCrimson = new Material(urpLit);
            aiCrimson.name = "Mat_AI_Crimson";
            aiCrimson.color = new Color(0.92f, 0.24f, 0.28f);
            if (aiCrimson.HasProperty("_Smoothness")) aiCrimson.SetFloat("_Smoothness", 0.55f);
            dict["AI_Crimson"] = aiCrimson;

            // IA 3 (Amatista / Púrpura)
            var aiAmethyst = new Material(urpLit);
            aiAmethyst.name = "Mat_AI_Amethyst";
            aiAmethyst.color = new Color(0.68f, 0.30f, 0.92f);
            if (aiAmethyst.HasProperty("_Smoothness")) aiAmethyst.SetFloat("_Smoothness", 0.55f);
            dict["AI_Amethyst"] = aiAmethyst;

            // IA 4 (Ámbar / Naranja dorado)
            var aiAmber = new Material(urpLit);
            aiAmber.name = "Mat_AI_Amber";
            aiAmber.color = new Color(0.98f, 0.62f, 0.12f);
            if (aiAmber.HasProperty("_Smoothness")) aiAmber.SetFloat("_Smoothness", 0.55f);
            dict["AI_Amber"] = aiAmber;

            // Visor IA (Blanco/Dorado)
            var aiVisor = new Material(urpLit);
            aiVisor.name = "Mat_AIVisor";
            aiVisor.color = new Color(1f, 0.92f, 0.65f);
            if (aiVisor.HasProperty("_EmissionColor"))
            {
                aiVisor.EnableKeyword("_EMISSION");
                aiVisor.SetColor("_EmissionColor", new Color(1f, 0.92f, 0.65f) * 1.2f);
            }
            dict["AIVisor"] = aiVisor;

            return dict;
        }

        private void BuildArenaFloor(Material floorMat, Material borderMat, Material accentMat)
        {
            var arenaRoot = new GameObject("ArenaRoot");

            // Suelo principal
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Ground";
            floor.transform.SetParent(arenaRoot.transform, false);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.transform.localScale = new Vector3(floorSize, 1.0f, floorSize);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // Bordes perimetrales de seguridad
            float half = floorSize * 0.5f;
            float borderThickness = 1.0f;
            float borderHeight = 0.8f;

            CreateBorderWall("Border_North", arenaRoot.transform, new Vector3(0, borderHeight * 0.5f, half), new Vector3(floorSize, borderHeight, borderThickness), borderMat);
            CreateBorderWall("Border_South", arenaRoot.transform, new Vector3(0, borderHeight * 0.5f, -half), new Vector3(floorSize, borderHeight, borderThickness), borderMat);
            CreateBorderWall("Border_East", arenaRoot.transform, new Vector3(half, borderHeight * 0.5f, 0), new Vector3(borderThickness, borderHeight, floorSize), borderMat);
            CreateBorderWall("Border_West", arenaRoot.transform, new Vector3(-half, borderHeight * 0.5f, 0), new Vector3(borderThickness, borderHeight, floorSize), borderMat);

            // Columnas en las cuatro esquinas
            Vector3[] cornerPositions = new Vector3[]
            {
                new Vector3(half, 1.5f, half),
                new Vector3(-half, 1.5f, half),
                new Vector3(half, 1.5f, -half),
                new Vector3(-half, 1.5f, -half)
            };

            for (int i = 0; i < cornerPositions.Length; i++)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Pillar_{i}";
                pillar.transform.SetParent(arenaRoot.transform, false);
                pillar.transform.position = cornerPositions[i];
                pillar.transform.localScale = new Vector3(1.2f, 3f, 1.2f);
                pillar.GetComponent<MeshRenderer>().sharedMaterial = accentMat;
            }
        }

        private void CreateBorderWall(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private GameObject CreateCharacter(
            string name,
            Vector3 position,
            Quaternion rotation,
            Material bodyMat,
            Material jointMat,
            Material accentMat,
            Material visorMat,
            bool isPlayer)
        {
            var charGo = new GameObject(name);
            charGo.transform.position = position;
            charGo.transform.rotation = rotation;

            // CharacterController para movimiento con colisiones y suelo
            var cc = charGo.AddComponent<CharacterController>();
            cc.height = 1.85f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0, 0.925f, 0);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;

            // Construir modelo anatómico articulado completo
            var humanoid = HumanoidModelBuilder.BuildHumanoid(charGo, bodyMat, jointMat, accentMat, visorMat);

            // Animador procedural biomecánico
            var animator = charGo.AddComponent<ProceduralHumanoidAnimator>();
            animator.humanoid = humanoid;
            animator.CaptureInitialPoses();

            if (isPlayer)
            {
                charGo.tag = "Player";
                var pc = charGo.AddComponent<PlayerController>();
                if (Camera.main != null)
                {
                    pc.cameraTransform = Camera.main.transform;
                }
            }
            else
            {
                var ai = charGo.AddComponent<AIController>();
                ai.wanderRadius = (floorSize * 0.5f) - 4f;
            }

            return charGo;
        }

        private void SetupThirdPersonCamera(Transform playerTransform)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var tpCam = cam.GetComponent<ThirdPersonCamera>();
            if (tpCam == null)
            {
                tpCam = cam.gameObject.AddComponent<ThirdPersonCamera>();
            }

            tpCam.target = playerTransform;
            tpCam.distance = 4.0f;
            tpCam.targetOffset = new Vector3(0, 1.4f, 0);

            // Posicionar la cámara directamente detrás del jugador
            cam.transform.position = playerTransform.position + new Vector3(0, 2.3f, -4.0f);
            cam.transform.LookAt(playerTransform.position + new Vector3(0, 1.4f, 0));
            cam.nearClipPlane = 0.1f;
        }
    }
}
