#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using SimuladorBloqueos.CameraSystem;
using SimuladorBloqueos.UI;
using SimuladorBloqueos.Environment.Editor;

namespace SimuladorBloqueos.EditorTools
{
    /// <summary>
    /// Reconstructor 3D de alta fidelidad arquitectónica de la Calle Junín y Base UTOP (La Paz, Bolivia).
    /// Reproduce al detalle las fotografías reales proporcionadas:
    /// - Eje +Z = Bajada (hacia la Catedral), Eje -Z = Subida.
    /// - Acera Derecha (+X bajando):
    ///   1. Muro verde escalonado de la UTOP con zócalo de piedra, ventanilla de guardia con repisa blanca y garita superior.
    ///   2. Tinglado metálico con columnas reticuladas en zig-zag color óxido y techo de calamina.
    ///   3. Edificio Verde UTOP de 3 pisos con portal vehicular casetonado, portón de reja roja, letrero institucional texturizado,
    ///      ventanales con carpintería 3D y pilastras/bajantes blancas.
    ///   4. Edificio Patrimonial Clásico (Ocre/Piedra) con portal monumental de arco, zócalo almohadillado con ventanas arqueadas enrejadas,
    ///      piano nobile con pilastras almohadilladas, arcos con clave y balaustradas 3D, y cornisa con modillones.
    ///   5. Edificio moderno blanco en la esquina inferior.
    /// - Acera Izquierda (-X bajando):
    ///   1. Edificio Neoclásico Rosa Viejo/Malva con zócalo gris, pilastras almohadilladas en relieve blanco, puerta doble blanca con escalones,
    ///      y ventanas profundas con cuadrícula de madera blanca y frontones.
    ///   2. Casonas Republicanas tradicionales con balcones de hierro forjado y madera, puertas comerciales y aleros.
    /// - Fondo Urbano y Cimentación:
    ///   Terreno continuo sin huecos, cruce inferior con árboles, torres y cúpulas de la Catedral y laderas de ladrillo de La Paz.
    /// </summary>
    public static class CalleJuninSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/BaseUTOP/BaseUTOP.unity";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials/CalleJunin";
        private const string TexFolder = "Assets/_Project/Art/Textures/CalleJunin";

        // Pendiente descendente hacia +Z (10.5% de inclinación continua)
        public const float SlopeGrade = 0.105f;
        public static float SlopeAngleDeg => Mathf.Atan(SlopeGrade) * Mathf.Rad2Deg; // +6.0 grados

        public static float GetRoadY(float z)
        {
            return -z * SlopeGrade;
        }

        [MenuItem("Simulador/Reconstruir Calle Junín y Base UTOP")]
        public static void BuildCalleJuninScene()
        {
            Debug.Log("<color=cyan><b>[CalleJunin] Generando texturas y reconstruyendo Calle Junín con alto nivel de detalle...</b></color>");

            EnsureDirectories();
            ProceduralMeshBuilder.ClearCache();
            CalleJuninTextureGenerator.EnsureTexturesGenerated();
            var mats = CreateOrLoadMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("[CALLE_JUNIN_LA_PAZ]");

            // 1. Iluminación y Cielo Nublado Andino
            SetupLightingAndEnvironment(root.transform);

            // 2. Terreno Base Continuo, Calzada Asfáltica, Bordillos y Aceras con Escalones
            BuildRoadAndSidewalks(root.transform, mats);

            // 3. Complejo UTOP Funcional de 2 Pisos (Oficina del Capitán, Mamparas Madera/Vidrio, Gradas, Garaje Plano, Armería, Patio de Honor y Cuadras con Literas)
            UTOPInteriorAndCompoundBuilder.BuildCompleteUTOPCompound(root.transform, mats);

            // 4. Edificio Patrimonial Clásico Ocre y Piedra (Acera Derecha, Z = +10 a +48)
            BuildClassicalMonumentalBuilding(root.transform, mats);

            // 5. Edificio Neoclásico Rosado y Casonas Republicanas (Acera Izquierda, X < 0)
            BuildLeftSideBuildings(root.transform, mats);

            // 6. Cierre Urbano Superior, Esquina Inferior, Catedral y Laderas de La Paz
            BuildSurroundingCityAndVista(root.transform, mats);

            // 7. Detalles Urbanos (Cables aéreos, conos policiales, vehículos estacionados, luminarias, señalética)
            BuildUrbanDetailsAndVehicles(root.transform, mats);

            // 8. Jugador y Cámara Dual (1ra Persona inicial + Tecla V para 3ra Persona)
            var player = BuildPlayerAndCamera(root.transform, mats);

            // 9. HUD Táctico Corregido
            BuildHUD(player);

            // 10. Sistema de 13 Decisiones Tácticas, Armería 3D Funcional, NavMesh y UI Interactiva
            DecisionAssetsAndUISceneBuilder.IntegrateDecisionSystemIntoScene(root.transform, root.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateBuildSettings();

            Selection.activeGameObject = player;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("<color=green><b>[CalleJunin] Reconstrucción detallada completada y guardada en BaseUTOP.unity</b></color>");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes/BaseUTOP"))
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "BaseUTOP");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Materials"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "CalleJunin");
        }

        #region Materiales URP con Texturas
        private static Dictionary<string, Material> CreateOrLoadMaterials()
        {
            var dict = new Dictionary<string, Material>();
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) urpShader = Shader.Find("Standard");

            var texAsphalt = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/Tex_Asphalt_Road.png");
            var texSidewalk = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/Tex_Sidewalk_Tiles.png");
            var texRusticated = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/Tex_Rusticated_Stone.png");
            var texStucco = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/Tex_Stucco_Detail.png");
            var texBanner = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/Tex_UTOP_Banner.png");
            var texBrick = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/Tex_Brick_LaPaz.png");

            Material MakeMat(string name, Color color, float smoothness = 0.2f, float metallic = 0.0f, Texture2D mainTex = null, Vector2? tiling = null)
            {
                string path = $"{MaterialsFolder}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(urpShader) { name = name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

                if (mainTex != null)
                {
                    mat.mainTexture = mainTex;
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                    Vector2 tile = tiling ?? Vector2.one;
                    mat.mainTextureScale = tile;
                    if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", tile);
                }
                else
                {
                    mat.mainTexture = null;
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
                }

                EditorUtility.SetDirty(mat);
                dict[name] = mat;
                return mat;
            }

            // Calle y Aceras
            MakeMat("Mat_CJ_Asphalt", new Color(0.85f, 0.85f, 0.88f), 0.18f, 0.0f, texAsphalt, new Vector2(2f, 20f));
            MakeMat("Mat_CJ_Sidewalk_Stone", new Color(0.78f, 0.79f, 0.82f), 0.28f, 0.0f, texSidewalk, new Vector2(2f, 18f));
            MakeMat("Mat_CJ_Curb_Stone", new Color(0.46f, 0.47f, 0.49f), 0.22f, 0.0f, texStucco, new Vector2(2f, 10f));
            MakeMat("Mat_CJ_Curb_Yellow", new Color(0.82f, 0.68f, 0.16f), 0.22f, 0.0f, texStucco, new Vector2(1f, 4f));
            MakeMat("Mat_CJ_Underground_Earth", new Color(0.28f, 0.26f, 0.25f), 0.1f, 0.0f);

            // Complejo UTOP
            MakeMat("Mat_CJ_UTOP_Green", new Color(0.43f, 0.56f, 0.45f), 0.16f, 0.0f, texStucco, new Vector2(4f, 4f));
            MakeMat("Mat_CJ_UTOP_GreenWall", new Color(0.39f, 0.52f, 0.41f), 0.14f, 0.0f, texStucco, new Vector2(5f, 2f));
            MakeMat("Mat_CJ_UTOP_Banner", Color.white, 0.35f, 0.05f, texBanner, Vector2.one);
            MakeMat("Mat_CJ_UTOP_Plinth", new Color(0.68f, 0.70f, 0.71f), 0.15f, 0.0f, texStucco, new Vector2(4f, 1f));
            MakeMat("Mat_CJ_UTOP_WhiteTrim", new Color(0.90f, 0.91f, 0.92f), 0.22f, 0.0f, texStucco, new Vector2(2f, 2f));
            MakeMat("Mat_CJ_UTOP_GateRust", new Color(0.48f, 0.21f, 0.16f), 0.35f, 0.55f);
            MakeMat("Mat_CJ_Corrugated_Roof", new Color(0.34f, 0.36f, 0.38f), 0.42f, 0.45f);
            MakeMat("Mat_CJ_Translucent_Roof", new Color(0.88f, 0.78f, 0.45f), 0.6f, 0.1f);

            // Ventanas y Carpintería
            MakeMat("Mat_CJ_Window_Glass", new Color(0.14f, 0.19f, 0.23f), 0.92f, 0.25f);
            MakeMat("Mat_CJ_Window_Curtain", new Color(0.68f, 0.66f, 0.60f), 0.2f, 0.0f);
            MakeMat("Mat_CJ_Window_FrameDark", new Color(0.16f, 0.17f, 0.18f), 0.45f, 0.40f);
            MakeMat("Mat_CJ_Wood_Mullion", new Color(0.36f, 0.22f, 0.15f), 0.32f, 0.0f);

            // Edificio Clásico Ocre y Piedra (Foto 3)
            MakeMat("Mat_CJ_Classical_Ochre", new Color(0.84f, 0.74f, 0.56f), 0.18f, 0.0f, texStucco, new Vector2(4f, 4f));
            MakeMat("Mat_CJ_Classical_WhiteStone", new Color(0.86f, 0.85f, 0.83f), 0.22f, 0.0f, texStucco, new Vector2(2f, 4f));
            MakeMat("Mat_CJ_Classical_Rusticated", new Color(0.88f, 0.85f, 0.82f), 0.20f, 0.0f, texRusticated, new Vector2(6f, 2f));
            MakeMat("Mat_CJ_Wood_Door", new Color(0.34f, 0.19f, 0.13f), 0.38f, 0.0f);
            MakeMat("Mat_CJ_Iron_Grille", new Color(0.11f, 0.11f, 0.12f), 0.55f, 0.80f);

            // Edificio Neoclásico Rosado y Casonas (Acera Izquierda)
            MakeMat("Mat_CJ_Pink_Facade", new Color(0.68f, 0.52f, 0.55f), 0.16f, 0.0f, texStucco, new Vector2(5f, 4f));
            MakeMat("Mat_CJ_BlueGrey_Plinth", new Color(0.50f, 0.54f, 0.58f), 0.20f, 0.0f, texRusticated, new Vector2(5f, 1.5f));
            MakeMat("Mat_CJ_Teal_Facade", new Color(0.36f, 0.58f, 0.55f), 0.16f, 0.0f, texStucco, new Vector2(3f, 3f));
            MakeMat("Mat_CJ_Cream_Colonial", new Color(0.76f, 0.68f, 0.55f), 0.16f, 0.0f, texStucco, new Vector2(3f, 3f));
            MakeMat("Mat_CJ_Balcony_Wood", new Color(0.30f, 0.20f, 0.14f), 0.28f, 0.0f);

            // Fondo Urbano y Detalles
            MakeMat("Mat_CJ_Traffic_Cone", new Color(0.95f, 0.36f, 0.08f), 0.45f, 0.0f);
            MakeMat("Mat_CJ_White_Stripe", new Color(0.94f, 0.94f, 0.94f), 0.35f, 0.0f);
            MakeMat("Mat_CJ_Black_Cable", new Color(0.08f, 0.08f, 0.09f), 0.4f, 0.3f);
            MakeMat("Mat_CJ_Mountain_Brick", Color.white, 0.15f, 0.0f, texBrick, new Vector2(4f, 3f));
            MakeMat("Mat_CJ_Dome_Slate", new Color(0.26f, 0.28f, 0.31f), 0.35f, 0.2f);
            MakeMat("Mat_CJ_Tree_Foliage", new Color(0.32f, 0.42f, 0.22f), 0.15f, 0.0f);
            MakeMat("Mat_CJ_Car_White", new Color(0.92f, 0.93f, 0.94f), 0.82f, 0.15f);
            MakeMat("Mat_CJ_Car_Black", new Color(0.12f, 0.13f, 0.15f), 0.85f, 0.25f);
            MakeMat("Mat_CJ_Car_Red", new Color(0.72f, 0.14f, 0.14f), 0.82f, 0.20f);

            AssetDatabase.SaveAssets();
            return dict;
        }
        #endregion

        #region 1. Iluminación y Atmósfera de La Paz
        private static void SetupLightingAndEnvironment(Transform root)
        {
            var envGroup = new GameObject("Environment_Lighting");
            envGroup.transform.SetParent(root, false);

            // Luz diurna andina (cielo parcialmente nublado como en las 4 fotografías, sombras suaves pero definidas)
            var sunGo = new GameObject("Sun_Directional");
            sunGo.transform.SetParent(envGroup.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(54f, -35f, 0f);

            var light = sunGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.98f, 0.96f, 0.93f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.72f;

            // Luz de relleno ambiental para que ninguna fachada quede negra
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.72f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.54f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.35f, 0.34f);

            // Neblina atmosférica lejana para fundir las montañas del fondo
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.76f, 0.78f, 0.81f);
            RenderSettings.fogStartDistance = 85f;
            RenderSettings.fogEndDistance = 230f;

            var volGo = new GameObject("Global_Volume");
            volGo.transform.SetParent(envGroup.transform, false);
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Settings/MainMenuVolumeProfile.asset");
            if (profile != null) volume.sharedProfile = profile;
        }
        #endregion

        #region 2. Calzada, Aceras y Cimentación Continua (Cero Flotación)
        private static void BuildRoadAndSidewalks(Transform root, Dictionary<string, Material> mats)
        {
            var streetGroup = new GameObject("Street_CalleJunin");
            streetGroup.transform.SetParent(root, false);

            // Longitud total de la calle: desde Z = -55m (cuesta arriba) hasta Z = +85m (cuesta abajo) = 140m
            float zStart = -55f;
            float zEnd = 85f;
            float totalLen = zEnd - zStart;
            float zMid = (zStart + zEnd) * 0.5f; // +15m
            float yMid = GetRoadY(zMid);
            float pitch = SlopeAngleDeg; // +6.0° inclina +Z hacia abajo (-Y)

            float roadWidth = 6.6f; // X: -3.3m a +3.3m
            float curbW = 0.22f;
            float curbH = 0.18f;
            float leftSidewalkW = 2.4f;  // X: -3.3 a -5.7m
            float rightSidewalkW = 2.6f; // X: +3.3 a +5.9m

            // 1. ZÓCALO SUBTERRÁNEO MACIZO BAJO LA CALLE Y ACERA IZQUIERDA (No invade el predio nivelado de la UTOP en X > +5.8m)
            CreateSlopedBox("Underground_Terrain_Foundation", streetGroup.transform,
                new Vector3(-14.6f, yMid - 12.0f, zMid),
                new Vector3(40.8f, 24.0f, totalLen + 60f),
                pitch, mats["Mat_CJ_Underground_Earth"]);

            // 2. Calzada continua de Asfalto
            CreateSlopedBox("Roadway_Asphalt_Main", streetGroup.transform,
                new Vector3(0f, yMid - 0.25f, zMid),
                new Vector3(roadWidth, 0.5f, totalLen),
                pitch, mats["Mat_CJ_Asphalt"]);

            // 3. Acera Izquierda continua (-X)
            float leftSidewalkX = -(roadWidth * 0.5f + leftSidewalkW * 0.5f);
            CreateSlopedBox("Sidewalk_Left_Main", streetGroup.transform,
                new Vector3(leftSidewalkX, yMid - 0.3f, zMid),
                new Vector3(leftSidewalkW, 0.6f + curbH * 2f, totalLen),
                pitch, mats["Mat_CJ_Sidewalk_Stone"]);

            // Bordillo Izquierdo
            CreateSlopedBox("Curb_Left", streetGroup.transform,
                new Vector3(-(roadWidth * 0.5f + curbW * 0.5f), yMid + curbH * 0.5f, zMid),
                new Vector3(curbW, curbH + 0.04f, totalLen),
                pitch, mats["Mat_CJ_Curb_Stone"]);

            // 4. Acera Derecha continua (+X, lado UTOP)
            float rightSidewalkX = (roadWidth * 0.5f + rightSidewalkW * 0.5f);
            CreateSlopedBox("Sidewalk_Right_Main", streetGroup.transform,
                new Vector3(rightSidewalkX, yMid - 0.3f, zMid),
                new Vector3(rightSidewalkW, 0.6f + curbH * 2f, totalLen),
                pitch, mats["Mat_CJ_Sidewalk_Stone"]);

            // Bordillo Derecho alternando piedra y amarillo (frente a UTOP)
            int curbSegments = 14;
            float cSegLen = totalLen / curbSegments;
            for (int i = 0; i < curbSegments; i++)
            {
                float cz = zStart + (i + 0.5f) * cSegLen;
                float cy = GetRoadY(cz);
                bool isUTOPZone = (cz > -22f && cz < 12f);
                Material cMat = (isUTOPZone && i % 2 == 0) ? mats["Mat_CJ_Curb_Yellow"] : mats["Mat_CJ_Curb_Stone"];

                CreateSlopedBox($"Curb_Right_{i}", streetGroup.transform,
                    new Vector3(roadWidth * 0.5f + curbW * 0.5f, cy + curbH * 0.5f, cz),
                    new Vector3(curbW, curbH + 0.04f, cSegLen * 1.01f),
                    pitch, cMat);
            }

            // 5. Peldaños / Cuñas de nivelación en la acera de la UTOP (visibles en Fotos 1, 2 y 5 junto a la ventanilla)
            for (int s = 0; s < 5; s++)
            {
                float sz = -18f + (s * 2.8f);
                float sy = GetRoadY(sz) + curbH;
                CreateBox($"Sidewalk_Step_Wedge_{s}", streetGroup.transform,
                    new Vector3(4.6f, sy + 0.04f, sz),
                    new Vector3(2.2f, 0.14f, 2.4f),
                    mats["Mat_CJ_Sidewalk_Stone"]);
            }

            // Tapa de alcantarilla circular frente a la entrada UTOP (visible en Foto 2 y Foto 5)
            float mhZ = 2.5f;
            CreateCylinder("Manhole_Cover", streetGroup.transform,
                new Vector3(2.1f, GetRoadY(mhZ) + 0.01f, mhZ),
                new Vector3(0.85f, 0.02f, 0.85f),
                mats["Mat_CJ_Window_FrameDark"]);
        }
        #endregion

        #region 3. Complejo UTOP Detallado (Muro, Tinglado Reticulado, Edificio Verde y Portal)
        private static void BuildUTOPComplex(Transform root, Dictionary<string, Material> mats)
        {
            var utopGroup = new GameObject("Complex_UTOP_Detailed");
            utopGroup.transform.SetParent(root, false);

            // Línea de fachada derecha en la acera: X = +5.8m
            float facadeX = 5.8f;

            // =========================================================================
            // A. MURO PERIMETRAL VERDE ESCALONADO (Z = -28m a -2.5m) — Fotos 1, 2, 4, 5
            // =========================================================================
            // El muro baja escalonado en 3 tramos siguiendo la pendiente
            float[] wallZStarts = { -28f, -19f, -10.5f };
            float[] wallZEnds   = { -19f, -10.5f, -2.5f };
            float[] wallHeights = { 4.2f, 4.6f, 5.2f };

            for (int w = 0; w < 3; w++)
            {
                float z0 = wallZStarts[w];
                float z1 = wallZEnds[w];
                float zLen = z1 - z0;
                float zC = (z0 + z1) * 0.5f;
                float yBase = GetRoadY(z1); // Cota baja del tramo
                float h = wallHeights[w];

                // Cimiento y zócalo gris de piedra/cemento que penetra en la acera
                CreateSlopedBox($"UTOP_Wall_Plinth_{w}", utopGroup.transform,
                    new Vector3(facadeX - 0.04f, GetRoadY(zC) + 0.35f, zC),
                    new Vector3(0.55f, 1.4f, zLen + 0.1f),
                    SlopeAngleDeg, mats["Mat_CJ_UTOP_Plinth"]);

                // Muro verde superior con tope horizontal escalonado (como en Foto 1 y Foto 4)
                float topY = GetRoadY(zC) + h;
                float totalBoxH = topY - (yBase - 2.0f);
                CreateBox($"UTOP_Green_Wall_{w}", utopGroup.transform,
                    new Vector3(facadeX, (yBase - 2.0f) + totalBoxH * 0.5f, zC),
                    new Vector3(0.48f, totalBoxH, zLen + 0.05f),
                    mats["Mat_CJ_UTOP_GreenWall"]);

                // Remate / Albardilla color ladrillo/óxido en la coronación del muro (Foto 1)
                CreateBox($"UTOP_Wall_Cap_{w}", utopGroup.transform,
                    new Vector3(facadeX, topY + 0.06f, zC),
                    new Vector3(0.56f, 0.12f, zLen + 0.1f),
                    mats["Mat_CJ_UTOP_GateRust"]);
            }

            // VENTANILLA DE GUARDIA EN EL MURO (Z = -5.8m, Fotos 1, 2 y 5)
            float guardWinZ = -5.8f;
            float guardWinY = GetRoadY(guardWinZ) + 1.65f;
            BuildDetailedWindow(utopGroup.transform, "UTOP_Guard_Window",
                new Vector3(facadeX - 0.24f, guardWinY, guardWinZ),
                width: 1.85f, height: 1.05f, facingSignX: -1f,
                cols: 3, rows: 1,
                frameMat: mats["Mat_CJ_Window_FrameDark"],
                glassMat: mats["Mat_CJ_Window_Glass"],
                sillMat: mats["Mat_CJ_UTOP_WhiteTrim"],
                hasBars: true, barMat: mats["Mat_CJ_Iron_Grille"]);

            // Repisa blanca gruesa bajo la ventanilla de guardia (muy notoria en Fotos 1 y 5)
            CreateBox("Guard_Window_Thick_Sill", utopGroup.transform,
                new Vector3(facadeX - 0.38f, guardWinY - 0.58f, guardWinZ),
                new Vector3(0.32f, 0.14f, 2.15f),
                mats["Mat_CJ_UTOP_WhiteTrim"]);

            // Garita elevada con ventanuco cuadrado rojo detrás del muro (visible en Foto 4)
            float boothZ = -7.5f;
            float boothY = GetRoadY(boothZ) + 4.4f;
            CreateBox("UTOP_Upper_GuardBooth", utopGroup.transform,
                new Vector3(facadeX + 1.2f, boothY, boothZ),
                new Vector3(2.4f, 2.2f, 4.5f),
                mats["Mat_CJ_UTOP_Green"]);
            CreateBox("UTOP_Booth_RedHatch", utopGroup.transform,
                new Vector3(facadeX + 1.2f, boothY + 0.2f, boothZ - 2.26f),
                new Vector3(0.85f, 0.85f, 0.08f),
                mats["Mat_CJ_UTOP_GateRust"]);

            // =========================================================================
            // B. TINGLADO METÁLICO CON COLUMNAS RETICULADAS (Z = -28m a -3m) — Fotos 2 y 4
            // =========================================================================
            for (int t = 0; t < 5; t++)
            {
                float tz = -25f + (t * 5.2f);
                float ty = GetRoadY(tz);
                BuildLatticeColumnAndTruss(utopGroup.transform, new Vector3(facadeX + 1.2f, ty, tz), mats);
            }

            // Cubierta del tinglado con chapas opacas y franjas translúcidas amarillas (Foto 2)
            float tingladoZ = -14.5f;
            float tingladoY = GetRoadY(tingladoZ) + 8.6f;
            var roofPanel = CreateSlopedBox("Tinglado_Main_Roof", utopGroup.transform,
                new Vector3(facadeX + 7.5f, tingladoY, tingladoZ),
                new Vector3(14.5f, 0.12f, 24f),
                SlopeAngleDeg, mats["Mat_CJ_Corrugated_Roof"]);
            roofPanel.transform.rotation = Quaternion.Euler(SlopeAngleDeg, 0, -14f);

            // Franjas de calamina plástica amarilla translúcida (visibles desde abajo en Foto 2)
            for (int s = 0; s < 3; s++)
            {
                var skyLight = CreateBox($"Tinglado_Skylight_{s}", utopGroup.transform,
                    new Vector3(facadeX + 6.5f, GetRoadY(-20f + s * 6f) + 8.3f, -20f + s * 6f),
                    new Vector3(10f, 0.08f, 1.4f),
                    mats["Mat_CJ_Translucent_Roof"]);
                skyLight.transform.rotation = Quaternion.Euler(SlopeAngleDeg, 0, -14f);
            }

            // =========================================================================
            // C. EDIFICIO VERDE PRINCIPAL DE LA UTOP (Z = -2.5m a +10.0m) — Fotos 1, 2, 4, 5
            // =========================================================================
            // Este bloque de 3 pisos encuadra el gran portal de entrada en Planta Baja
            float bZStart = -2.5f;
            float bZEnd = 10.0f;
            float bZLen = bZEnd - bZStart; // 12.5m de frente sobre Calle Junín
            float bZMid = (bZStart + bZEnd) * 0.5f; // +3.75m
            float bBaseY = GetRoadY(bZMid);

            // 1. Pilares laterales del Portal en Planta Baja (dejan abierto el vano vehicular de Z = -1.2 a +8.2)
            // Pilar derecho del portal (lado subida, Z = -2.5 a -1.0)
            CreateBox("UTOP_Portal_Pillar_Up", utopGroup.transform,
                new Vector3(facadeX + 3.0f, bBaseY + 1.0f, -1.75f),
                new Vector3(6.2f, 8.0f, 1.5f),
                mats["Mat_CJ_UTOP_Green"]);

            // Pilar izquierdo del portal (lado bajada, pegado al edificio clásico, Z = +8.4 a +10.0)
            CreateBox("UTOP_Portal_Pillar_Down", utopGroup.transform,
                new Vector3(facadeX + 3.0f, bBaseY + 1.0f, 9.2f),
                new Vector3(6.2f, 8.0f, 1.6f),
                mats["Mat_CJ_UTOP_Green"]);

            // 2. Volumen Superior de los Pisos 1 y 2 sobre el Portal (Y = bBaseY + 3.8m hasta bBaseY + 12.6m)
            float upperFloorBottom = bBaseY + 3.6f;
            float upperFloorHeight = 9.0f;
            CreateBox("UTOP_Upper_Floors_Block", utopGroup.transform,
                new Vector3(facadeX + 4.5f, upperFloorBottom + upperFloorHeight * 0.5f, bZMid),
                new Vector3(9.2f, upperFloorHeight, bZLen),
                mats["Mat_CJ_UTOP_Green"]);

            // 3. LETRERO INSTITUCIONAL EN VOLADIZO SOBRE EL PORTAL (Fotos 2 y 5)
            // Caja metálica que sobresale hacia la acera con la textura oficial generada
            float bannerY = bBaseY + 4.35f;
            CreateBox("UTOP_Sign_BackingBox", utopGroup.transform,
                new Vector3(facadeX - 0.25f, bannerY, bZMid),
                new Vector3(0.45f, 1.45f, bZLen - 0.6f),
                mats["Mat_CJ_UTOP_WhiteTrim"]);

            // Cara frontal con el arte del cartel UTOP
            CreateBox("UTOP_Sign_FrontFace", utopGroup.transform,
                new Vector3(facadeX - 0.49f, bannerY, bZMid),
                new Vector3(0.04f, 1.35f, bZLen - 0.8f),
                mats["Mat_CJ_UTOP_Banner"]);

            // 4. FILA DE 4 VENTANAS DEL PRIMER PISO (Justo encima del letrero UTOP — Foto 5)
            float floor1WinY = bBaseY + 6.4f;
            // Recuadro blanco continuo que enmarca las 4 ventanas
            CreateBox("UTOP_F1_WhiteSurround", utopGroup.transform,
                new Vector3(facadeX - 0.06f, floor1WinY, bZMid),
                new Vector3(0.14f, 2.15f, bZLen - 1.2f),
                mats["Mat_CJ_UTOP_WhiteTrim"]);

            for (int i = 0; i < 4; i++)
            {
                float wz = -0.5f + (i * 2.8f);
                BuildDetailedWindow(utopGroup.transform, $"UTOP_Front_Win_F1_{i}",
                    new Vector3(facadeX - 0.08f, floor1WinY, wz),
                    width: 2.35f, height: 1.85f, facingSignX: -1f,
                    cols: 3, rows: 2,
                    frameMat: mats["Mat_CJ_Window_FrameDark"],
                    glassMat: mats["Mat_CJ_Window_Glass"],
                    sillMat: mats["Mat_CJ_UTOP_WhiteTrim"],
                    hasBars: false, barMat: null);
            }

            // 5. FILA DE 4 VENTANAS DEL SEGUNDO PISO (Bajo la cornisa — Foto 1 y Foto 5)
            float floor2WinY = bBaseY + 10.4f;
            CreateBox("UTOP_F2_WhiteSurround", utopGroup.transform,
                new Vector3(facadeX - 0.06f, floor2WinY, bZMid),
                new Vector3(0.14f, 2.25f, bZLen - 1.2f),
                mats["Mat_CJ_UTOP_WhiteTrim"]);

            for (int i = 0; i < 4; i++)
            {
                float wz = -0.5f + (i * 2.8f);
                BuildDetailedWindow(utopGroup.transform, $"UTOP_Front_Win_F2_{i}",
                    new Vector3(facadeX - 0.08f, floor2WinY, wz),
                    width: 2.35f, height: 1.95f, facingSignX: -1f,
                    cols: 3, rows: 2,
                    frameMat: mats["Mat_CJ_Window_FrameDark"],
                    glassMat: mats["Mat_CJ_Window_Glass"],
                    sillMat: mats["Mat_CJ_UTOP_WhiteTrim"],
                    hasBars: false, barMat: null);
            }

            // 6. FACHADA LATERAL DEL BLOQUE UTOP (Mira hacia la subida -Z, sobre el muro verde — Foto 4)
            // En la Foto 4 se ven los grandes ventanales apaisados y los pares de bajantes/pilastras blancas verticales
            for (int f = 0; f < 2; f++)
            {
                float sideWinY = bBaseY + 6.4f + (f * 4.0f);
                for (int c = 0; c < 2; c++)
                {
                    float sideWinX = facadeX + 2.4f + (c * 4.2f);
                    BuildDetailedWindowOnZ(utopGroup.transform, $"UTOP_Side_Win_{f}_{c}",
                        new Vector3(sideWinX, sideWinY, bZStart - 0.06f),
                        width: 3.4f, height: 1.9f,
                        cols: 4, rows: 2,
                        frameMat: mats["Mat_CJ_Window_FrameDark"],
                        glassMat: mats["Mat_CJ_Window_Glass"],
                        sillMat: mats["Mat_CJ_UTOP_WhiteTrim"]);
                }
            }

            // Pares de columnas/bajantes blancas verticales en la fachada lateral (muy visibles en Foto 4)
            float[] pipeX = { facadeX + 0.35f, facadeX + 0.65f, facadeX + 4.35f, facadeX + 4.65f };
            foreach (float px in pipeX)
            {
                CreateBox("UTOP_Side_WhitePipe", utopGroup.transform,
                    new Vector3(px, bBaseY + 8.2f, bZStart - 0.08f),
                    new Vector3(0.16f, 8.8f, 0.16f),
                    mats["Mat_CJ_UTOP_WhiteTrim"]);
            }

            // 7. INTERIOR DEL PORTAL VEHICULAR UTOP (Techo casetonado, portones de reja roja, rampa y patrullas — Foto 2 y 5)
            // Suelo interno del pasaje y patio
            CreateSlopedBox("UTOP_Interior_Passage_Floor", utopGroup.transform,
                new Vector3(facadeX + 12f, bBaseY - 0.1f, bZMid - 4f),
                new Vector3(24f, 0.4f, 34f),
                SlopeAngleDeg, mats["Mat_CJ_Sidewalk_Stone"]);

            // Techo casetonado (retícula de vigas bajo el primer piso, visible en Foto 2)
            for (int gx = 0; gx < 5; gx++)
            {
                CreateBox($"Coffered_Beam_X_{gx}", utopGroup.transform,
                    new Vector3(facadeX + 1.0f + gx * 1.8f, bBaseY + 3.55f, bZMid),
                    new Vector3(0.18f, 0.25f, 9.6f),
                    mats["Mat_CJ_Classical_Ochre"]);
            }
            for (int gz = 0; gz < 6; gz++)
            {
                CreateBox($"Coffered_Beam_Z_{gz}", utopGroup.transform,
                    new Vector3(facadeX + 4.5f, bBaseY + 3.55f, -0.5f + gz * 1.7f),
                    new Vector3(9.0f, 0.25f, 0.18f),
                    mats["Mat_CJ_Classical_Ochre"]);
            }

            // Hojas del portón de reja metálica color rojo óxido abiertas hacia los costados (Fotos 2 y 5)
            BuildIronGateLeaf(utopGroup.transform, new Vector3(facadeX + 0.2f, bBaseY + 1.6f, -0.8f), mats["Mat_CJ_UTOP_GateRust"]);
            BuildIronGateLeaf(utopGroup.transform, new Vector3(facadeX + 0.2f, bBaseY + 1.6f, 8.2f), mats["Mat_CJ_UTOP_GateRust"]);

            // Pared del fondo del patio interior con letrero "UTOP" y patrullas estacionadas (Foto 2)
            CreateBox("UTOP_Courtyard_InnerFacade", utopGroup.transform,
                new Vector3(facadeX + 17.5f, bBaseY + 4.5f, -4f),
                new Vector3(2.0f, 16.0f, 38f),
                mats["Mat_CJ_UTOP_Green"]);

            // Muro lateral de cierre del patio interior en Z = +9.8m
            CreateBox("UTOP_Courtyard_SideClosure", utopGroup.transform,
                new Vector3(facadeX + 12.5f, bBaseY + 4.5f, 9.8f),
                new Vector3(12.0f, 16.0f, 1.0f),
                mats["Mat_CJ_UTOP_GreenWall"]);

            // Patrullas en el patio interior (visibles a través del portal en Foto 2)
            BuildDetailedVehicle(utopGroup.transform, new Vector3(facadeX + 11.5f, bBaseY + 0.5f, 1.5f), -90f, mats["Mat_CJ_Car_White"], mats, isPolicePickup: true);
            BuildDetailedVehicle(utopGroup.transform, new Vector3(facadeX + 11.5f, bBaseY + 0.8f, -2.5f), -90f, mats["Mat_CJ_Car_White"], mats, isPolicePickup: true);
            BuildDetailedVehicle(utopGroup.transform, new Vector3(facadeX + 11.5f, bBaseY + 1.1f, -6.5f), -90f, mats["Mat_CJ_Car_Red"], mats, isPolicePickup: false);

            // Antena pararrayos en la azotea del edificio verde (Foto 4)
            CreateCylinder("UTOP_Roof_Antenna", utopGroup.transform,
                new Vector3(facadeX + 4.5f, bBaseY + 15.5f, bZMid),
                new Vector3(0.06f, 3.2f, 0.06f),
                mats["Mat_CJ_Iron_Grille"]);
        }

        private static void BuildLatticeColumnAndTruss(Transform parent, Vector3 basePos, Dictionary<string, Material> mats)
        {
            var group = new GameObject("Tinglado_Lattice_Frame");
            group.transform.SetParent(parent, false);
            Material rustMat = mats["Mat_CJ_UTOP_GateRust"];

            float colH = 8.4f;
            // Dos montantes verticales de la columna reticulada
            CreateBox("Post_A", group.transform, basePos + new Vector3(0f, colH * 0.5f, -0.22f), new Vector3(0.12f, colH, 0.12f), rustMat);
            CreateBox("Post_B", group.transform, basePos + new Vector3(0f, colH * 0.5f, 0.22f), new Vector3(0.12f, colH, 0.12f), rustMat);

            // Celosía en zig-zag de la columna (idéntica a la Foto 4)
            int rungs = 12;
            for (int r = 0; r < rungs; r++)
            {
                float ry = 0.6f + r * 0.62f;
                var diag = CreateBox($"ZigZag_{r}", group.transform, basePos + new Vector3(0f, ry, 0f), new Vector3(0.08f, 0.08f, 0.65f), rustMat);
                diag.transform.rotation = Quaternion.Euler((r % 2 == 0) ? 38f : -38f, 0f, 0f);
            }

            // Viga cercha transversal hacia el patio (+X)
            float trussY = basePos.y + colH;
            CreateBox("Truss_BottomChord", group.transform, new Vector3(basePos.x + 6.5f, trussY, basePos.z), new Vector3(13.5f, 0.14f, 0.14f), rustMat);
            var topChord = CreateBox("Truss_TopChord", group.transform, new Vector3(basePos.x + 6.5f, trussY + 1.4f, basePos.z), new Vector3(14.0f, 0.14f, 0.14f), rustMat);
            topChord.transform.rotation = Quaternion.Euler(0f, 0f, -12f);

            for (int d = 0; d < 8; d++)
            {
                float dx = basePos.x + 1.0f + d * 1.6f;
                CreateBox($"Truss_Web_{d}", group.transform, new Vector3(dx, trussY + 0.6f, basePos.z), new Vector3(0.1f, 1.3f, 0.1f), rustMat);
            }
        }

        private static void BuildIronGateLeaf(Transform parent, Vector3 pos, Material mat)
        {
            var leaf = new GameObject("Gate_Leaf");
            leaf.transform.SetParent(parent, false);
            CreateBox("Gate_Panel_Lower", leaf.transform, pos + new Vector3(0, -0.7f, 0), new Vector3(1.2f, 1.4f, 0.08f), mat);
            CreateBox("Gate_Frame_Top", leaf.transform, pos + new Vector3(0, 1.5f, 0), new Vector3(1.2f, 0.08f, 0.08f), mat);
            for (int b = 0; b < 6; b++)
            {
                CreateBox($"Bar_{b}", leaf.transform, pos + new Vector3(-0.5f + b * 0.2f, 0.75f, 0), new Vector3(0.04f, 1.5f, 0.04f), mat);
            }
        }
        #endregion

        #region 4. Edificio Patrimonial Clásico Monumental (Foto 3 — Z = +10m a +48m)
        private static void BuildClassicalMonumentalBuilding(Transform root, Dictionary<string, Material> mats)
        {
            var classGroup = new GameObject("Building_Classical_Monumental");
            classGroup.transform.SetParent(root, false);

            float facadeX = 5.8f;
            float zStart = 10.0f; // Pegado exactamente al borde izquierdo del edificio verde UTOP
            float zEnd = 48.0f;
            float zLen = zEnd - zStart; // 38m
            float zMid = (zStart + zEnd) * 0.5f; // +29m
            float baseMidY = GetRoadY(zMid);

            // 1. MASA ARQUITECTÓNICA PRINCIPAL CON CIMIENTO PROFUNDO (Nunca flota)
            float roofTopY = GetRoadY(zStart) + 14.5f; // Cornisa horizontal continua
            float foundationBottomY = -20.0f;
            float totalMassH = roofTopY - foundationBottomY;

            CreateBox("Classical_Main_Wall_Ochre", classGroup.transform,
                new Vector3(facadeX + 6.0f, foundationBottomY + totalMassH * 0.5f, zMid),
                new Vector3(12.0f, totalMassH, zLen),
                mats["Mat_CJ_Classical_Ochre"]);

            // 2. ZÓCALO DE PIEDRA ALMOHADILLADA EN PLANTA BAJA (Sigue la línea de la calle)
            float plinthTopOffset = 4.2f;
            CreateSlopedBox("Classical_Rusticated_Plinth", classGroup.transform,
                new Vector3(facadeX - 0.12f, baseMidY + 1.2f, zMid + 2.5f),
                new Vector3(0.35f, 5.2f, zLen - 5.0f),
                SlopeAngleDeg, mats["Mat_CJ_Classical_Rusticated"]);

            // Cornisa blanca divisoria entre el zócalo de piedra y el Piano Nobile
            CreateSlopedBox("Classical_Belt_Cornice", classGroup.transform,
                new Vector3(facadeX - 0.28f, baseMidY + plinthTopOffset, zMid + 2.5f),
                new Vector3(0.55f, 0.38f, zLen - 4.8f),
                SlopeAngleDeg, mats["Mat_CJ_Classical_WhiteStone"]);

            // 3. PORTAL MONUMENTAL DE ARCO EN EL EXTREMO SUPERIOR (Z = +12.5m, pegado a la UTOP — Fotos 3, 4 y 5)
            float portalZ = 12.6f;
            float portalRoadY = GetRoadY(portalZ);

            // Pilastras blancas que enmarcan el gran arco de doble altura
            CreateBox("Portal_Bay_Pilaster_L", classGroup.transform,
                new Vector3(facadeX - 0.18f, portalRoadY + 7.2f, portalZ - 2.1f),
                new Vector3(0.45f, 14.5f, 0.85f),
                mats["Mat_CJ_Classical_WhiteStone"]);
            CreateBox("Portal_Bay_Pilaster_R", classGroup.transform,
                new Vector3(facadeX - 0.18f, portalRoadY + 7.2f, portalZ + 2.1f),
                new Vector3(0.45f, 14.5f, 0.85f),
                mats["Mat_CJ_Classical_WhiteStone"]);

            // Gran Puerta de Madera Café con Arco de Medio Punto y Cuadrícula de Cristales (Foto 3 derecha)
            CreateBox("Monumental_Wood_Door_Base", classGroup.transform,
                new Vector3(facadeX - 0.08f, portalRoadY + 3.2f, portalZ),
                new Vector3(0.22f, 6.4f, 3.1f),
                mats["Mat_CJ_Wood_Door"]);

            // Cristales en cuadrícula de la puerta monumental
            BuildDetailedWindow(classGroup.transform, "Portal_Upper_Transom",
                new Vector3(facadeX - 0.12f, portalRoadY + 4.6f, portalZ),
                width: 2.6f, height: 2.8f, facingSignX: -1f,
                cols: 4, rows: 5,
                frameMat: mats["Mat_CJ_Wood_Door"],
                glassMat: mats["Mat_CJ_Window_Glass"],
                sillMat: mats["Mat_CJ_Wood_Door"],
                hasBars: false, barMat: null);

            // Arco blanco moldeado sobre la gran puerta
            CreateArchHeader(classGroup.transform, new Vector3(facadeX - 0.22f, portalRoadY + 6.2f, portalZ), radius: 1.65f, mats["Mat_CJ_Classical_WhiteStone"]);

            // Balcón con barandilla de hierro forjado en el piso superior sobre el portal (Foto 5)
            CreateBox("Portal_Upper_Balcony_Slab", classGroup.transform,
                new Vector3(facadeX - 0.45f, portalRoadY + 8.8f, portalZ),
                new Vector3(0.8f, 0.22f, 3.4f),
                mats["Mat_CJ_Classical_WhiteStone"]);
            CreateBox("Portal_Upper_Balcony_Rail", classGroup.transform,
                new Vector3(facadeX - 0.78f, portalRoadY + 9.4f, portalZ),
                new Vector3(0.08f, 0.95f, 3.4f),
                mats["Mat_CJ_Iron_Grille"]);

            // 4. LAS 5 CRUJÍAS CLÁSICAS (Z = +18m a +45m) — Foto 3 en primer plano
            int bays = 5;
            float baySpacing = 5.8f;
            float firstBayZ = 18.2f;

            for (int b = 0; b < bays; b++)
            {
                float bz = firstBayZ + (b * baySpacing);
                float by = GetRoadY(bz);

                // Pilastra vertical blanca almohadillada entre cada paño de ventanas
                BuildRusticatedPilaster(classGroup.transform,
                    new Vector3(facadeX - 0.20f, by + 8.6f, bz - baySpacing * 0.5f),
                    height: 9.2f, width: 0.95f, depth: 0.35f,
                    mats["Mat_CJ_Classical_WhiteStone"]);

                // A. Ventana Arqueada de Planta Baja en el Zócalo de Piedra con Reja Negra (Foto 3 abajo)
                BuildArchedGroundWindow(classGroup.transform,
                    new Vector3(facadeX - 0.31f, by + 1.65f, bz),
                    width: 2.2f, height: 1.85f, mats);

                // B. Gran Ventanal de Arco de Medio Punto del Piano Nobile con Balaustrada Blanca 3D (Foto 3 centro)
                float pianoY = by + 6.4f;
                BuildPianoNobileArchedWindow(classGroup.transform,
                    new Vector3(facadeX - 0.10f, pianoY, bz),
                    width: 2.2f, height: 3.4f, mats);

                // C. Ventana Rectangular del Piso Superior con Marco Blanco y Reja (Foto 3 arriba)
                float topFloorY = by + 10.6f;
                BuildDetailedWindow(classGroup.transform, $"Classical_TopWin_{b}",
                    new Vector3(facadeX - 0.12f, topFloorY, bz),
                    width: 1.75f, height: 2.4f, facingSignX: -1f,
                    cols: 2, rows: 3,
                    frameMat: mats["Mat_CJ_Classical_WhiteStone"],
                    glassMat: mats["Mat_CJ_Window_Glass"],
                    sillMat: mats["Mat_CJ_Classical_WhiteStone"],
                    hasBars: true, barMat: mats["Mat_CJ_Iron_Grille"]);
            }

            // Última pilastra de cierre en la esquina inferior
            float lastZ = firstBayZ + bays * baySpacing - baySpacing * 0.5f;
            BuildRusticatedPilaster(classGroup.transform,
                new Vector3(facadeX - 0.20f, GetRoadY(lastZ) + 8.6f, lastZ),
                height: 9.2f, width: 0.95f, depth: 0.35f,
                mats["Mat_CJ_Classical_WhiteStone"]);

            // 5. CORNISA MONUMENTAL SUPERIOR CON MÉNSULAS (MODILLONES)
            CreateSlopedBox("Classical_Top_Cornice", classGroup.transform,
                new Vector3(facadeX - 0.55f, baseMidY + 13.0f, zMid),
                new Vector3(1.1f, 0.55f, zLen + 0.6f),
                SlopeAngleDeg, mats["Mat_CJ_Classical_WhiteStone"]);

            // Muro piñón / parapeto de ladrillo en la medianera con la UTOP (visible arriba en Foto 4)
            CreateBox("Classical_Brick_Gable", classGroup.transform,
                new Vector3(facadeX + 0.5f, GetRoadY(zStart) + 14.8f, zStart + 0.3f),
                new Vector3(2.2f, 2.4f, 0.6f),
                mats["Mat_CJ_UTOP_GateRust"]);
        }

        private static void BuildArchedGroundWindow(Transform parent, Vector3 pos, float width, float height, Dictionary<string, Material> mats)
        {
            var win = new GameObject("Ground_Arched_Window");
            win.transform.SetParent(parent, false);

            // Fondo de vidrio oscuro frente al zócalo de piedra
            CreateBox("Glass", win.transform, pos + new Vector3(0.01f, -0.15f, 0), new Vector3(0.06f, height * 0.85f, width * 0.92f), mats["Mat_CJ_Window_Glass"]);
            // Marco perimetral de madera café oscuro (como en Foto 3 abajo)
            CreateBox("WoodFrame_Bot", win.transform, pos + new Vector3(-0.02f, -height * 0.52f, 0), new Vector3(0.12f, 0.14f, width), mats["Mat_CJ_Wood_Door"]);
            CreateBox("WoodFrame_L", win.transform, pos + new Vector3(-0.02f, -0.15f, -width * 0.45f), new Vector3(0.12f, height * 0.85f, 0.12f), mats["Mat_CJ_Wood_Door"]);
            CreateBox("WoodFrame_R", win.transform, pos + new Vector3(-0.02f, -0.15f, width * 0.45f), new Vector3(0.12f, height * 0.85f, 0.12f), mats["Mat_CJ_Wood_Door"]);
            // Marco arqueado blanco superior con dovelas
            CreateArchHeader(win.transform, pos + new Vector3(-0.03f, height * 0.22f, 0), width * 0.50f, mats["Mat_CJ_Classical_WhiteStone"]);
            // Reja de barrotes verticales y horizontales sobresaliendo claramente
            for (int i = -3; i <= 3; i++)
            {
                CreateBox($"Bar_V_{i}", win.transform, pos + new Vector3(-0.06f, -0.10f, i * (width / 7.5f)), new Vector3(0.05f, height * 0.88f, 0.05f), mats["Mat_CJ_Iron_Grille"]);
            }
            CreateBox("Bar_H1", win.transform, pos + new Vector3(-0.06f, 0.15f, 0), new Vector3(0.05f, 0.05f, width * 0.92f), mats["Mat_CJ_Iron_Grille"]);
            CreateBox("Bar_H2", win.transform, pos + new Vector3(-0.06f, -0.35f, 0), new Vector3(0.05f, 0.05f, width * 0.92f), mats["Mat_CJ_Iron_Grille"]);
        }

        private static void BuildPianoNobileArchedWindow(Transform parent, Vector3 pos, float width, float height, Dictionary<string, Material> mats)
        {
            var win = new GameObject("PianoNobile_Arched_Window");
            win.transform.SetParent(parent, false);

            // Ventana con cuadrícula de madera café
            BuildDetailedWindow(win.transform, "Inner_Wood_Window",
                pos + new Vector3(0.05f, 0.2f, 0),
                width: width * 0.85f, height: height * 0.78f, facingSignX: -1f,
                cols: 3, rows: 5,
                frameMat: mats["Mat_CJ_Wood_Door"],
                glassMat: mats["Mat_CJ_Window_Glass"],
                sillMat: mats["Mat_CJ_Classical_WhiteStone"],
                hasBars: false, barMat: null);

            // Jambas blancas laterales y arco superior con clave (Foto 3)
            CreateBox("Jamb_L", win.transform, pos + new Vector3(-0.08f, 0, -width * 0.48f), new Vector3(0.24f, height * 0.8f, 0.22f), mats["Mat_CJ_Classical_WhiteStone"]);
            CreateBox("Jamb_R", win.transform, pos + new Vector3(-0.08f, 0, width * 0.48f), new Vector3(0.24f, height * 0.8f, 0.22f), mats["Mat_CJ_Classical_WhiteStone"]);
            CreateArchHeader(win.transform, pos + new Vector3(-0.08f, height * 0.38f, 0), width * 0.52f, mats["Mat_CJ_Classical_WhiteStone"]);

            // BALAUSTRADA BLANCA 3D EN LA BASE DEL VENTANAL (Con 7 balaustres individuales como en Foto 3)
            Vector3 balPos = pos + new Vector3(-0.14f, -height * 0.42f, 0);
            CreateBox("Balustrade_TopRail", win.transform, balPos + new Vector3(0, 0.42f, 0), new Vector3(0.28f, 0.12f, width + 0.2f), mats["Mat_CJ_Classical_WhiteStone"]);
            CreateBox("Balustrade_BotRail", win.transform, balPos + new Vector3(0, -0.42f, 0), new Vector3(0.32f, 0.16f, width + 0.3f), mats["Mat_CJ_Classical_WhiteStone"]);

            int balusters = 7;
            for (int i = 0; i < balusters; i++)
            {
                float bz = -width * 0.42f + i * (width * 0.84f / (balusters - 1));
                CreateCylinder($"Baluster_{i}", win.transform, balPos + new Vector3(0, 0f, bz), new Vector3(0.12f, 0.38f, 0.12f), mats["Mat_CJ_Classical_WhiteStone"]);
            }
        }

        private static void CreateArchHeader(Transform parent, Vector3 centerPos, float radius, Material mat)
        {
            int segments = 7;
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * (i / (float)segments);
                float a1 = Mathf.PI * ((i + 1) / (float)segments);
                float aMid = (a0 + a1) * 0.5f;

                float dz = Mathf.Cos(aMid) * radius;
                float dy = Mathf.Sin(aMid) * radius * 0.75f;
                var voussoir = CreateBox($"Arch_Voussoir_{i}", parent,
                    centerPos + new Vector3(0f, dy, dz),
                    new Vector3(0.26f, 0.28f, (radius * 0.52f)),
                    mat);
                voussoir.transform.rotation = Quaternion.Euler(-(aMid * Mathf.Rad2Deg - 90f), 0f, 0f);
            }
            // Clave central sobresaliente (Keystone)
            CreateBox("Arch_Keystone", parent, centerPos + new Vector3(-0.04f, radius * 0.78f, 0f), new Vector3(0.32f, 0.42f, 0.30f), mat);
        }

        private static void BuildRusticatedPilaster(Transform parent, Vector3 pos, float height, float width, float depth, Material mat)
        {
            var pil = new GameObject("Rusticated_Pilaster");
            pil.transform.SetParent(parent, false);

            // Fuste base
            CreateBox("Pilaster_Shaft", pil.transform, pos, new Vector3(depth, height, width), mat);

            // Sillares horizontales en relieve (Almohadillado 3D visible en Foto 2, 3 y 4)
            int blocks = Mathf.RoundToInt(height / 0.55f);
            float startY = pos.y - height * 0.48f;
            for (int i = 0; i < blocks; i++)
            {
                CreateBox($"Quoin_Block_{i}", pil.transform,
                    new Vector3(pos.x - depth * 0.3f, startY + i * 0.55f, pos.z),
                    new Vector3(depth * 0.65f, 0.40f, width * 1.04f),
                    mat);
            }

            // Capitel superior
            CreateBox("Pilaster_Capital", pil.transform,
                pos + new Vector3(-depth * 0.35f, height * 0.5f, 0f),
                new Vector3(depth * 1.4f, 0.32f, width * 1.2f),
                mat);
        }
        #endregion

        #region 5. Edificios de la Acera Izquierda (Edificio Rosado Neoclásico y Casonas Republicanas)
        private static void BuildLeftSideBuildings(Transform root, Dictionary<string, Material> mats)
        {
            var leftGroup = new GameObject("Buildings_Left_Sidewalk_Detailed");
            leftGroup.transform.SetParent(root, false);

            // Línea de fachada izquierda sobre la acera: X = -5.6m (mira hacia +X)
            float facadeX = -5.6f;

            // =========================================================================
            // A. EDIFICIO NEOCLÁSICO ROSADO / MALVA (Z = -26m a +14m) — Fotos 2 y 4
            // =========================================================================
            float pZStart = -26.0f;
            float pZEnd = 14.0f;
            float pZLen = pZEnd - pZStart; // 40m
            float pZMid = (pZStart + pZEnd) * 0.5f; // -6.0m
            float pBaseY = GetRoadY(pZMid);

            // Masa principal con cimiento hasta Y = -20m (Jamás flota)
            float pTopY = GetRoadY(pZStart) + 11.2f;
            float pBottomY = -20.0f;
            CreateBox("Pink_Building_MainMass", leftGroup.transform,
                new Vector3(facadeX - 5.5f, pBottomY + (pTopY - pBottomY) * 0.5f, pZMid),
                new Vector3(11.0f, pTopY - pBottomY, pZLen),
                mats["Mat_CJ_Pink_Facade"]);

            // Zócalo inferior de piedra gris azulada siguiendo la pendiente (Foto 4 izquierda)
            CreateSlopedBox("Pink_BlueGrey_Plinth", leftGroup.transform,
                new Vector3(facadeX + 0.08f, pBaseY + 0.7f, pZMid),
                new Vector3(0.28f, 2.2f, pZLen + 0.1f),
                SlopeAngleDeg, mats["Mat_CJ_BlueGrey_Plinth"]);

            // Moldura blanca de cintura sobre el zócalo y entre pisos
            CreateSlopedBox("Pink_Plinth_Molding", leftGroup.transform,
                new Vector3(facadeX + 0.16f, pBaseY + 1.65f, pZMid),
                new Vector3(0.38f, 0.22f, pZLen + 0.2f),
                SlopeAngleDeg, mats["Mat_CJ_UTOP_WhiteTrim"]);

            CreateSlopedBox("Pink_MidFloor_Cornice", leftGroup.transform,
                new Vector3(facadeX + 0.20f, pBaseY + 5.6f, pZMid),
                new Vector3(0.46f, 0.32f, pZLen + 0.2f),
                SlopeAngleDeg, mats["Mat_CJ_UTOP_WhiteTrim"]);

            CreateSlopedBox("Pink_Roof_Cornice", leftGroup.transform,
                new Vector3(facadeX + 0.32f, pBaseY + 9.8f, pZMid),
                new Vector3(0.70f, 0.45f, pZLen + 0.4f),
                SlopeAngleDeg, mats["Mat_CJ_UTOP_WhiteTrim"]);

            // 8 Crujías con Pilastras Almohadilladas Blancas, Puerta Blanca con Escalones y Ventanas con Cuadrícula
            int pinkBays = 8;
            float pinkBaySpan = pZLen / pinkBays; // 5.0m por crujía

            for (int b = 0; b <= pinkBays; b++)
            {
                float pz = pZStart + b * pinkBaySpan;
                float py = GetRoadY(pz);

                // Pilastra blanca almohadillada en relieve (invertida para mirar hacia +X)
                var pil = new GameObject($"Pink_Pilaster_{b}");
                pil.transform.SetParent(leftGroup.transform, false);
                BuildRusticatedPilaster(pil.transform, Vector3.zero, height: 8.4f, width: 0.72f, depth: 0.28f, mats["Mat_CJ_UTOP_WhiteTrim"]);
                pil.transform.position = new Vector3(facadeX + 0.12f, py + 5.7f, pz);
                pil.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                if (b < pinkBays)
                {
                    float wz = pz + pinkBaySpan * 0.5f;
                    float wy = GetRoadY(wz);

                    // En la crujía b == 3 (Z = -8.5m, primer plano izquierdo de Foto 4): Gran Puerta Blanca de 2 hojas con escalones
                    if (b == 3 || b == 6)
                    {
                        BuildPinkBuildingEntranceDoor(leftGroup.transform, new Vector3(facadeX + 0.08f, wy, wz), mats);
                    }
                    else
                    {
                        // Ventana de Planta Baja con marco blanco, vidrio oscuro y carpintería blanca 3x4 (Foto 4)
                        BuildDetailedWindow(leftGroup.transform, $"Pink_Win_PB_{b}",
                            new Vector3(facadeX + 0.08f, wy + 3.4f, wz),
                            width: 1.65f, height: 2.5f, facingSignX: 1f,
                            cols: 3, rows: 4,
                            frameMat: mats["Mat_CJ_UTOP_WhiteTrim"],
                            glassMat: mats["Mat_CJ_Window_Glass"],
                            sillMat: mats["Mat_CJ_UTOP_WhiteTrim"],
                            hasBars: true, barMat: mats["Mat_CJ_UTOP_WhiteTrim"]);
                    }

                    // Ventana de Planta Alta con cuadrícula blanca de cristales (Foto 4 arriba a la izquierda)
                    BuildDetailedWindow(leftGroup.transform, $"Pink_Win_PA_{b}",
                        new Vector3(facadeX + 0.08f, wy + 7.6f, wz),
                        width: 1.65f, height: 2.4f, facingSignX: 1f,
                        cols: 3, rows: 4,
                        frameMat: mats["Mat_CJ_UTOP_WhiteTrim"],
                        glassMat: mats["Mat_CJ_Window_Glass"],
                        sillMat: mats["Mat_CJ_UTOP_WhiteTrim"],
                        hasBars: false, barMat: null);
                }
            }

            // =========================================================================
            // B. CASONAS REPUBLICANAS CON BALCONES (Z = +14m a +52m) — Fotos 4 y 5
            // =========================================================================
            float rZStart = 14.0f;
            float rZEnd = 52.0f;
            float rZLen = rZEnd - rZStart; // 38m
            float rZMid = (rZStart + rZEnd) * 0.5f; // +33m
            float rBaseY = GetRoadY(rZMid);

            // Planta Baja en color verde agua / turquesa desgastado (Foto 5 izquierda)
            CreateSlopedBox("Republican_Lower_Teal", leftGroup.transform,
                new Vector3(facadeX - 4.5f, rBaseY + 1.2f, rZMid),
                new Vector3(9.0f, 6.5f, rZLen),
                SlopeAngleDeg, mats["Mat_CJ_Teal_Facade"]);

            // Planta Alta en tono crema/tierra colonial con alero de calamina
            CreateSlopedBox("Republican_Upper_Cream", leftGroup.transform,
                new Vector3(facadeX - 4.5f, rBaseY + 6.2f, rZMid),
                new Vector3(9.0f, 4.8f, rZLen),
                SlopeAngleDeg, mats["Mat_CJ_Cream_Colonial"]);

            // Alero inclinado del techo
            var eave = CreateSlopedBox("Republican_Roof_Eave", leftGroup.transform,
                new Vector3(facadeX - 1.5f, rBaseY + 8.8f, rZMid),
                new Vector3(4.5f, 0.18f, rZLen + 0.5f),
                SlopeAngleDeg, mats["Mat_CJ_Corrugated_Roof"]);
            eave.transform.rotation = Quaternion.Euler(SlopeAngleDeg, 0f, -15f);

            // Balcones en voladizo y puertas en las 6 crujías republicanas
            for (int d = 0; d < 6; d++)
            {
                float dz = rZStart + 3.2f + d * 6.0f;
                float dy = GetRoadY(dz);

                // Puerta comercial/colonial en planta baja con marco en relieve
                BuildDetailedWindow(leftGroup.transform, $"Republican_Door_{d}",
                    new Vector3(facadeX + 0.08f, dy + 1.6f, dz),
                    width: 1.5f, height: 2.8f, facingSignX: 1f,
                    cols: 2, rows: 3,
                    frameMat: mats["Mat_CJ_UTOP_WhiteTrim"],
                    glassMat: mats["Mat_CJ_Wood_Door"],
                    sillMat: mats["Mat_CJ_Curb_Stone"],
                    hasBars: false, barMat: null);

                // Balcón colonial en el segundo piso con barandilla de hierro y puerta-ventana roja/madera
                CreateBox($"Colonial_Balcony_Floor_{d}", leftGroup.transform,
                    new Vector3(facadeX + 0.55f, dy + 4.4f, dz),
                    new Vector3(1.1f, 0.16f, 2.6f),
                    mats["Mat_CJ_Balcony_Wood"]);

                CreateBox($"Colonial_Balcony_Rail_{d}", leftGroup.transform,
                    new Vector3(facadeX + 1.05f, dy + 4.95f, dz),
                    new Vector3(0.06f, 0.95f, 2.6f),
                    mats["Mat_CJ_UTOP_GateRust"]);

                BuildDetailedWindow(leftGroup.transform, $"Republican_UpperDoor_{d}",
                    new Vector3(facadeX + 0.08f, dy + 5.8f, dz),
                    width: 1.45f, height: 2.5f, facingSignX: 1f,
                    cols: 2, rows: 4,
                    frameMat: mats["Mat_CJ_UTOP_GateRust"],
                    glassMat: mats["Mat_CJ_Window_Glass"],
                    sillMat: mats["Mat_CJ_Balcony_Wood"],
                    hasBars: false, barMat: null);
            }
        }

        private static void BuildPinkBuildingEntranceDoor(Transform parent, Vector3 roadLevelPos, Dictionary<string, Material> mats)
        {
            var doorGrp = new GameObject("Pink_Monumental_White_Door");
            doorGrp.transform.SetParent(parent, false);
            Material whiteMat = mats["Mat_CJ_UTOP_WhiteTrim"];

            // Escalones de piedra que suben desde la acera hacia la puerta (Foto 4 esquina inferior izquierda)
            CreateBox("Door_Step_1", doorGrp.transform, roadLevelPos + new Vector3(0.32f, 0.25f, 0), new Vector3(0.55f, 0.22f, 2.2f), mats["Mat_CJ_Curb_Stone"]);
            CreateBox("Door_Step_2", doorGrp.transform, roadLevelPos + new Vector3(0.18f, 0.45f, 0), new Vector3(0.35f, 0.22f, 2.0f), mats["Mat_CJ_Curb_Stone"]);

            // Jambas laterales y cornisa superior de la puerta
            Vector3 doorCenter = roadLevelPos + new Vector3(0.05f, 2.4f, 0);
            CreateBox("Door_Jamb_L", doorGrp.transform, doorCenter + new Vector3(0.08f, 0, -0.95f), new Vector3(0.24f, 3.8f, 0.24f), whiteMat);
            CreateBox("Door_Jamb_R", doorGrp.transform, doorCenter + new Vector3(0.08f, 0, 0.95f), new Vector3(0.24f, 3.8f, 0.24f), whiteMat);
            CreateBox("Door_Cornice_Top", doorGrp.transform, doorCenter + new Vector3(0.18f, 2.05f, 0), new Vector3(0.42f, 0.32f, 2.35f), whiteMat);

            // Hojas blancas de la puerta con paneles en relieve
            CreateBox("Door_Leaf_Back", doorGrp.transform, doorCenter + new Vector3(-0.02f, -0.45f, 0), new Vector3(0.08f, 2.8f, 1.68f), whiteMat);
            // Ranura central oscura entre las dos hojas y paneles
            CreateBox("Door_Center_Split", doorGrp.transform, doorCenter + new Vector3(0.03f, -0.45f, 0), new Vector3(0.02f, 2.8f, 0.04f), mats["Mat_CJ_Window_FrameDark"]);

            for (int r = 0; r < 3; r++)
            {
                for (int c = -1; c <= 1; c += 2)
                {
                    CreateBox($"Door_Panel_{r}_{c}", doorGrp.transform,
                        doorCenter + new Vector3(0.04f, -1.2f + r * 0.8f, c * 0.42f),
                        new Vector3(0.04f, 0.58f, 0.55f),
                        mats["Mat_CJ_Classical_WhiteStone"]);
                }
            }

            // Montante superior con reja (Transom sobre la puerta — Foto 4)
            BuildDetailedWindow(doorGrp.transform, "Door_Transom",
                doorCenter + new Vector3(0.04f, 1.38f, 0),
                width: 1.68f, height: 0.82f, facingSignX: 1f,
                cols: 4, rows: 2,
                frameMat: whiteMat,
                glassMat: mats["Mat_CJ_Window_Curtain"],
                sillMat: whiteMat,
                hasBars: true, barMat: mats["Mat_CJ_Iron_Grille"]);
        }
        #endregion

        #region 6. Fondo Urbano, Cruce Inferior, Catedral y Laderas (Cierre Visual Completo)
        private static void BuildSurroundingCityAndVista(Transform root, Dictionary<string, Material> mats)
        {
            var cityGroup = new GameObject("City_Surroundings_LaPaz");
            cityGroup.transform.SetParent(root, false);

            // 1. EDIFICIO MODERNO BLANCO DE ESQUINA INFERIOR DERECHA (Z = +48m a +66m — Fotos 3, 4 y 5)
            float wZ = 56f;
            float wY = GetRoadY(wZ);
            CreateBox("White_Corner_Building", cityGroup.transform,
                new Vector3(9.5f, wY + 5.0f, wZ),
                new Vector3(8.5f, 18.0f, 15.0f),
                mats["Mat_CJ_UTOP_WhiteTrim"]);

            for (int f = 0; f < 4; f++)
            {
                for (int w = 0; w < 3; w++)
                {
                    BuildDetailedWindow(cityGroup.transform, $"WhiteBldg_Win_{f}_{w}",
                        new Vector3(5.2f, wY + 2.2f + f * 3.2f, 50.5f + w * 4.5f),
                        width: 2.4f, height: 1.5f, facingSignX: -1f,
                        cols: 3, rows: 1,
                        frameMat: mats["Mat_CJ_Window_FrameDark"],
                        glassMat: mats["Mat_CJ_Window_Glass"],
                        sillMat: mats["Mat_CJ_UTOP_WhiteTrim"],
                        hasBars: false, barMat: null);
                }
            }

            // 2. ÁRBOLES EN LA ESQUINA INFERIOR (Visibles al fondo de la bajada en Fotos 3, 4 y 5)
            BuildUrbanTree(cityGroup.transform, new Vector3(-2.5f, GetRoadY(56f), 56f), mats);
            BuildUrbanTree(cityGroup.transform, new Vector3(-3.2f, GetRoadY(64f), 64f), mats);

            // 3. LAS DOS TORRES Y CÚPULAS DE LA CATEDRAL AL FONDO DE LA BAJADA (Foto 4 y Foto 3)
            float cathZ = 92f;
            float cathY = GetRoadY(68f);

            // Cuerpo de la fachada de piedra de la iglesia
            CreateBox("Cathedral_Base_Body", cityGroup.transform,
                new Vector3(-1.5f, cathY + 8f, cathZ),
                new Vector3(22f, 22f, 12f),
                mats["Mat_CJ_Classical_Ochre"]);

            // Torre Derecha y Torre Izquierda con campanario y cúpula gris pizarra
            float[] towerXs = { -6.5f, 3.5f };
            for (int t = 0; t < 2; t++)
            {
                float tx = towerXs[t];
                CreateBox($"Cathedral_Tower_{t}", cityGroup.transform,
                    new Vector3(tx, cathY + 16f, cathZ - 2f),
                    new Vector3(5.4f, 32f, 5.4f),
                    mats["Mat_CJ_Classical_Ochre"]);

                // Cornisas de la torre
                CreateBox($"Tower_Cornice_1_{t}", cityGroup.transform,
                    new Vector3(tx, cathY + 22f, cathZ - 2f),
                    new Vector3(6.2f, 0.6f, 6.2f),
                    mats["Mat_CJ_Classical_WhiteStone"]);
                CreateBox($"Tower_Cornice_2_{t}", cityGroup.transform,
                    new Vector3(tx, cathY + 28f, cathZ - 2f),
                    new Vector3(6.0f, 0.6f, 6.0f),
                    mats["Mat_CJ_Classical_WhiteStone"]);

                // Arcos del campanario
                CreateBox($"Belfry_Arch_{t}", cityGroup.transform,
                    new Vector3(tx, cathY + 25f, cathZ - 4.72f),
                    new Vector3(1.8f, 3.6f, 0.2f),
                    mats["Mat_CJ_Window_FrameDark"]);

                // Cúpula semiesférica gris oscura con linterna superior
                var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dome.name = $"Cathedral_Dome_{t}";
                dome.transform.SetParent(cityGroup.transform, false);
                dome.transform.position = new Vector3(tx, cathY + 32.2f, cathZ - 2f);
                dome.transform.localScale = new Vector3(5.2f, 6.2f, 5.2f);
                dome.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_CJ_Dome_Slate"];

                CreateCylinder($"Dome_Lantern_{t}", cityGroup.transform,
                    new Vector3(tx, cathY + 36.0f, cathZ - 2f),
                    new Vector3(1.1f, 1.8f, 1.1f),
                    mats["Mat_CJ_Dome_Slate"]);
            }

            // 4. LADERAS DE LA PAZ AL FONDO (Cerro con construcciones de ladrillo — Foto 4)
            for (int row = 0; row < 4; row++)
            {
                float hz = 125f + row * 18f;
                float hyBase = cathY + 12f + row * 11f;
                CreateBox($"Hillside_Terrain_Row_{row}", cityGroup.transform,
                    new Vector3(0f, hyBase - 10f, hz),
                    new Vector3(180f, 28f, 20f),
                    mats["Mat_CJ_Mountain_Brick"]);

                for (int h = -8; h <= 8; h++)
                {
                    float hx = h * 9.5f + (row % 2) * 4.2f;
                    float bh = 7f + Mathf.Abs((h * 3 + row) % 5) * 2.2f;
                    CreateBox($"Ladera_House_{row}_{h}", cityGroup.transform,
                        new Vector3(hx, hyBase + bh * 0.5f, hz - 4f),
                        new Vector3(8.2f, bh, 8.0f),
                        mats["Mat_CJ_Mountain_Brick"]);
                }
            }

            // 5. EDIFICIOS CUESTA ARRIBA (Z = -37m a -55m — Detrás del Patio de Honor UTOP)
            float upZ = -46f;
            float upY = GetRoadY(upZ);
            // Edificio ocre de 4 pisos arriba de la cancha UTOP (Foto 2 centro-fondo)
            CreateBox("Uphill_Ochre_Building", cityGroup.transform,
                new Vector3(9.5f, upY + 6.5f, upZ),
                new Vector3(9.0f, 16.0f, 16.0f),
                mats["Mat_CJ_Classical_Ochre"]);

            for (int f = 0; f < 3; f++)
            {
                for (int c = 0; c < 3; c++)
                {
                    BuildDetailedWindow(cityGroup.transform, $"Uphill_Win_{f}_{c}",
                        new Vector3(4.95f, upY + 3.5f + f * 3.4f, -51f + c * 4.8f),
                        width: 1.8f, height: 1.8f, facingSignX: -1f,
                        cols: 2, rows: 2,
                        frameMat: mats["Mat_CJ_Wood_Door"],
                        glassMat: mats["Mat_CJ_Window_Glass"],
                        sillMat: mats["Mat_CJ_UTOP_WhiteTrim"],
                        hasBars: false, barMat: null);
                }
            }

            // Edificio blanco alto detrás del cuartel UTOP (X = +44m, fuera del Patio de Honor y Cuadras)
            CreateBox("Rear_HighRise_Building", cityGroup.transform,
                new Vector3(44.0f, GetRoadY(-8f) + 12.0f, -8f),
                new Vector3(14.0f, 26.0f, 34.0f),
                mats["Mat_CJ_Classical_WhiteStone"]);

            // Continuación de edificios en la acera izquierda cuesta arriba (Z = -26m a -55m)
            CreateBox("Uphill_Left_Building", cityGroup.transform,
                new Vector3(-10.5f, upY + 5.5f, upZ),
                new Vector3(10.0f, 15.0f, 28.0f),
                mats["Mat_CJ_Cream_Colonial"]);

            // Cierre urbano superior al final de la cuesta arriba (Z = -52m) para que el horizonte superior nunca se vea vacío
            CreateBox("Uphill_Horizon_Closure_Building", cityGroup.transform,
                new Vector3(0f, GetRoadY(-50f) + 6.0f, -52f),
                new Vector3(32.0f, 16.0f, 8.0f),
                mats["Mat_CJ_Classical_Ochre"]);
            CreateBox("Uphill_Horizon_Hillside", cityGroup.transform,
                new Vector3(0f, GetRoadY(-54f) + 14.0f, -64f),
                new Vector3(60.0f, 28.0f, 14.0f),
                mats["Mat_CJ_Mountain_Brick"]);
        }

        private static void BuildUrbanTree(Transform parent, Vector3 pos, Dictionary<string, Material> mats)
        {
            var tree = new GameObject("Urban_Tree");
            tree.transform.SetParent(parent, false);
            CreateCylinder("Trunk", tree.transform, pos + new Vector3(0, 2.0f, 0), new Vector3(0.45f, 2.0f, 0.45f), mats["Mat_CJ_Balcony_Wood"]);
            var crown1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown1.transform.SetParent(tree.transform, false);
            crown1.transform.position = pos + new Vector3(0, 4.8f, 0);
            crown1.transform.localScale = new Vector3(5.2f, 4.2f, 5.2f);
            crown1.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_CJ_Tree_Foliage"];

            var crown2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown2.transform.SetParent(tree.transform, false);
            crown2.transform.position = pos + new Vector3(1.2f, 5.4f, 1.0f);
            crown2.transform.localScale = new Vector3(4.0f, 3.6f, 4.0f);
            crown2.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_CJ_Tree_Foliage"];
        }
        #endregion

        #region 7. Detalles Urbanos, Conos, Cables Aéreos y Vehículos en la Calle
        private static void BuildUrbanDetailsAndVehicles(Transform root, Dictionary<string, Material> mats)
        {
            var details = new GameObject("Urban_Props_And_Vehicles");
            details.transform.SetParent(root, false);

            // 1. Conos de seguridad naranjas frente a la UTOP y el edificio clásico (Fotos 1, 2, 4 y 5)
            float[] coneZs = { -9.5f, -3.5f, 4.5f, 18.0f };
            for (int i = 0; i < coneZs.Length; i++)
            {
                float cz = coneZs[i];
                float cy = GetRoadY(cz);
                BuildTrafficCone(details.transform, new Vector3(3.15f, cy, cz), mats);
            }

            // 2. Vehículos paceños de alta densidad poligonal (Toyota Caldina Wagon, Volkswagen Peta, Minibús HiAce)
            // Estacionados ordenadamente a un costado de la calle (X = +2.65m, junto al bordillo lateral, NO en medio de la vía)
            ProceduralMeshBuilder.BuildToyotaCaldinaWagon(
                details.transform,
                new Vector3(2.65f, GetRoadY(16.5f), 16.5f),
                yaw: 0f,
                pitchSlope: SlopeAngleDeg,
                bodyMat: mats["Mat_CJ_Car_White"],
                mats: mats);

            ProceduralMeshBuilder.BuildVolkswagenPeta(
                details.transform,
                new Vector3(2.65f, GetRoadY(25.5f), 25.5f),
                yaw: 0f,
                pitchSlope: SlopeAngleDeg,
                bodyMat: mats["Mat_CJ_Car_Red"],
                mats: mats);

            ProceduralMeshBuilder.BuildLaPazMinibus(
                details.transform,
                new Vector3(2.62f, GetRoadY(36.0f), 36.0f),
                yaw: 0f,
                pitchSlope: SlopeAngleDeg,
                bodyMat: mats["Mat_CJ_Car_White"],
                stripeMat: mats["Mat_CJ_Traffic_Cone"],
                mats: mats);

            ProceduralMeshBuilder.BuildToyotaCaldinaWagon(
                details.transform,
                new Vector3(2.65f, GetRoadY(-14.5f), -14.5f),
                yaw: 180f,
                pitchSlope: -SlopeAngleDeg,
                bodyMat: mats["Mat_CJ_Car_Black"],
                mats: mats);

            // 3. Cables aéreos longitudinales y transversales (Característicos de las 4 fotos)
            for (int c = 0; c < 8; c++)
            {
                float cz = -25f + c * 9.5f;
                float cy = GetRoadY(cz) + 7.2f;
                var wire = CreateCylinder($"Cross_Cable_{c}", details.transform,
                    new Vector3(0f, cy, cz),
                    new Vector3(0.025f, 5.8f, 0.025f),
                    mats["Mat_CJ_Black_Cable"]);
                wire.transform.rotation = Quaternion.Euler(12f, (c % 2 == 0) ? 15f : -18f, 90f);
            }

            // Haz de cables negros pegados a las fachadas izquierda y derecha
            CreateSlopedBox("Facade_Cable_Bundle_Left", details.transform,
                new Vector3(-5.38f, GetRoadY(0f) + 4.8f, 0f),
                new Vector3(0.05f, 0.08f, 80f),
                SlopeAngleDeg, mats["Mat_CJ_Black_Cable"]);

            CreateSlopedBox("Facade_Cable_Bundle_Right", details.transform,
                new Vector3(5.58f, GetRoadY(20f) + 5.0f, 20f),
                new Vector3(0.05f, 0.08f, 65f),
                SlopeAngleDeg, mats["Mat_CJ_Black_Cable"]);

            // 4. Brazo de alumbrado público entre la UTOP y el edificio clásico (visible en Foto 5)
            float lampZ = 10.2f;
            float lampY = GetRoadY(lampZ) + 8.5f;
            var lampArm = CreateCylinder("StreetLamp_Arm", details.transform,
                new Vector3(4.8f, lampY, lampZ),
                new Vector3(0.06f, 1.2f, 0.06f),
                mats["Mat_CJ_Iron_Grille"]);
            lampArm.transform.rotation = Quaternion.Euler(0f, 0f, 75f);
            CreateBox("StreetLamp_Fixture", details.transform,
                new Vector3(3.7f, lampY + 0.25f, lampZ),
                new Vector3(0.55f, 0.16f, 0.32f),
                mats["Mat_CJ_UTOP_WhiteTrim"]);

            // 5. Desplegar los 2 Mineros Bloqueadores, 2 Cholitas y 2 Ponchos Rojos interactivos sobre la calzada de la Calle Junín
            CharacterAssetsAndClipsProcessor.Spawn2MinerBlockersOnCalleJunin(details.transform, GetRoadY);
            CharacterAssetsAndClipsProcessor.SpawnCholitasAndPonchosRojosOnCalleJunin(details.transform, GetRoadY);
        }

        private static void BuildTrafficCone(Transform parent, Vector3 basePos, Dictionary<string, Material> mats)
        {
            var cone = new GameObject("Traffic_Cone");
            cone.transform.SetParent(parent, false);
            CreateBox("Cone_Base", cone.transform, basePos + new Vector3(0, 0.03f, 0), new Vector3(0.42f, 0.06f, 0.42f), mats["Mat_CJ_Traffic_Cone"]);
            CreateCylinder("Cone_Body_Lower", cone.transform, basePos + new Vector3(0, 0.22f, 0), new Vector3(0.28f, 0.18f, 0.28f), mats["Mat_CJ_Traffic_Cone"]);
            CreateCylinder("Cone_Reflective_Collar", cone.transform, basePos + new Vector3(0, 0.45f, 0), new Vector3(0.20f, 0.08f, 0.20f), mats["Mat_CJ_White_Stripe"]);
            CreateCylinder("Cone_Body_Top", cone.transform, basePos + new Vector3(0, 0.62f, 0), new Vector3(0.13f, 0.12f, 0.13f), mats["Mat_CJ_Traffic_Cone"]);
        }

        private static void BuildDetailedVehicle(Transform parent, Vector3 pos, float yaw, Material bodyMat, Dictionary<string, Material> mats, bool isPolicePickup)
        {
            var car = new GameObject(isPolicePickup ? "Vehicle_UTOP_Patrol" : "Vehicle_Urban_Car");
            car.transform.SetParent(parent, false);
            car.transform.position = pos;
            car.transform.rotation = Quaternion.Euler(SlopeAngleDeg, yaw, 0f);

            // Chasis inferior
            CreateBox("Body_Lower", car.transform, pos, new Vector3(1.88f, 0.68f, 4.4f), bodyMat);
            // Cabina con cristales
            float cabZ = isPolicePickup ? -0.2f : -0.1f;
            float cabLen = isPolicePickup ? 2.1f : 2.4f;
            CreateBox("Cabin_Roof", car.transform, pos + new Vector3(0, 0.62f, cabZ), new Vector3(1.68f, 0.62f, cabLen), bodyMat);
            CreateBox("Cabin_Windows", car.transform, pos + new Vector3(0, 0.60f, cabZ), new Vector3(1.72f, 0.48f, cabLen * 0.92f), mats["Mat_CJ_Window_Glass"]);
            CreateBox("Windshield_Front", car.transform, pos + new Vector3(0, 0.60f, cabZ), new Vector3(1.58f, 0.48f, cabLen * 1.04f), mats["Mat_CJ_Window_Glass"]);

            // Parachoques y faros
            CreateBox("Bumper_Front", car.transform, pos + new Vector3(0, -0.18f, 2.22f), new Vector3(1.92f, 0.26f, 0.16f), mats["Mat_CJ_Window_FrameDark"]);
            CreateBox("Bumper_Rear", car.transform, pos + new Vector3(0, -0.18f, -2.22f), new Vector3(1.92f, 0.26f, 0.16f), mats["Mat_CJ_Window_FrameDark"]);
            CreateBox("Headlight_L", car.transform, pos + new Vector3(-0.68f, 0.1f, 2.21f), new Vector3(0.36f, 0.18f, 0.06f), mats["Mat_CJ_White_Stripe"]);
            CreateBox("Headlight_R", car.transform, pos + new Vector3(0.68f, 0.1f, 2.21f), new Vector3(0.36f, 0.18f, 0.06f), mats["Mat_CJ_White_Stripe"]);
            CreateBox("Taillight_L", car.transform, pos + new Vector3(-0.68f, 0.1f, -2.21f), new Vector3(0.36f, 0.18f, 0.06f), mats["Mat_CJ_Traffic_Cone"]);
            CreateBox("Taillight_R", car.transform, pos + new Vector3(0.68f, 0.1f, -2.21f), new Vector3(0.36f, 0.18f, 0.06f), mats["Mat_CJ_Traffic_Cone"]);

            if (isPolicePickup)
            {
                CreateBox("Police_Lightbar", car.transform, pos + new Vector3(0, 0.98f, cabZ), new Vector3(1.15f, 0.10f, 0.26f), mats["Mat_CJ_Traffic_Cone"]);
            }

            // 4 Ruedas
            Vector3[] wheelOffsets = {
                new Vector3(-0.92f, -0.28f, 1.35f),
                new Vector3(0.92f, -0.28f, 1.35f),
                new Vector3(-0.92f, -0.28f, -1.35f),
                new Vector3(0.92f, -0.28f, -1.35f)
            };
            for (int w = 0; w < 4; w++)
            {
                var wh = CreateCylinder($"Wheel_{w}", car.transform, pos + wheelOffsets[w], new Vector3(0.68f, 0.14f, 0.68f), mats["Mat_CJ_Black_Cable"]);
                wh.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }
        #endregion

        #region Helpers de Carpintería 3D (Ventanas con Profundidad Real)
        /// <summary>
        /// Construye una ventana arquitectónica real en 3D sobre una fachada orientada en X:
        /// - Cristal oscuro retraído (NUNCA tapado por el marco).
        /// - 4 barras de marco independientes (Dintel, Alféizar saliente, Jamba Izquierda y Derecha).
        /// - Parteluces y travesaños 3D (cuadrícula de carpintería).
        /// - Rejas opcionales.
        /// </summary>
        private static void BuildDetailedWindow(
            Transform parent, string name, Vector3 centerPos,
            float width, float height, float facingSignX,
            int cols, int rows,
            Material frameMat, Material glassMat, Material sillMat,
            bool hasBars, Material barMat)
        {
            var win = new GameObject(name);
            win.transform.SetParent(parent, false);

            // 1. Panel de cristal oscuro en el plano de la ventana
            CreateBox("Glass_Pane", win.transform,
                centerPos,
                new Vector3(0.04f, height - 0.12f, width - 0.12f),
                glassMat);

            // 2. Marco perimetral hueco (4 piezas alrededor del cristal, sobresaliendo hacia la calle en facingSignX)
            float frameThickness = 0.11f;
            float frameDepth = 0.16f;
            float outX = centerPos.x + facingSignX * 0.05f;

            // Dintel superior
            CreateBox("Frame_Top", win.transform,
                new Vector3(outX, centerPos.y + height * 0.5f - frameThickness * 0.5f, centerPos.z),
                new Vector3(frameDepth, frameThickness, width + 0.08f),
                frameMat);

            // Alféizar / Repisa inferior (más saliente)
            CreateBox("Frame_Sill", win.transform,
                new Vector3(outX + facingSignX * 0.05f, centerPos.y - height * 0.5f, centerPos.z),
                new Vector3(frameDepth + 0.12f, frameThickness * 1.2f, width + 0.18f),
                sillMat);

            // Jambas laterales
            CreateBox("Frame_Left", win.transform,
                new Vector3(outX, centerPos.y, centerPos.z - width * 0.5f + frameThickness * 0.5f),
                new Vector3(frameDepth, height, frameThickness),
                frameMat);

            CreateBox("Frame_Right", win.transform,
                new Vector3(outX, centerPos.y, centerPos.z + width * 0.5f - frameThickness * 0.5f),
                new Vector3(frameDepth, height, frameThickness),
                frameMat);

            // 3. Parteluces verticales y travesaños horizontales 3D (cuadrícula de la ventana)
            float mullionX = centerPos.x + facingSignX * 0.03f;
            for (int c = 1; c < cols; c++)
            {
                float mz = centerPos.z - width * 0.5f + c * (width / cols);
                CreateBox($"Mullion_V_{c}", win.transform,
                    new Vector3(mullionX, centerPos.y, mz),
                    new Vector3(0.05f, height - 0.14f, 0.045f),
                    frameMat);
            }
            for (int r = 1; r < rows; r++)
            {
                float my = centerPos.y - height * 0.5f + r * (height / rows);
                CreateBox($"Mullion_H_{r}", win.transform,
                    new Vector3(mullionX, my, centerPos.z),
                    new Vector3(0.05f, 0.045f, width - 0.14f),
                    frameMat);
            }

            // 4. Rejas exteriores si corresponde
            if (hasBars && barMat != null)
            {
                float barX = centerPos.x + facingSignX * 0.09f;
                int barCount = Mathf.Max(3, cols * 2);
                for (int b = 1; b < barCount; b++)
                {
                    float bz = centerPos.z - width * 0.5f + b * (width / barCount);
                    CreateBox($"Grille_Bar_{b}", win.transform,
                        new Vector3(barX, centerPos.y, bz),
                        new Vector3(0.025f, height - 0.08f, 0.025f),
                        barMat);
                }
            }
        }

        private static void BuildDetailedWindowOnZ(
            Transform parent, string name, Vector3 centerPos,
            float width, float height, int cols, int rows,
            Material frameMat, Material glassMat, Material sillMat)
        {
            var win = new GameObject(name);
            win.transform.SetParent(parent, false);

            CreateBox("Glass_Pane", win.transform, centerPos, new Vector3(width - 0.12f, height - 0.12f, 0.04f), glassMat);
            CreateBox("Frame_Top", win.transform, centerPos + new Vector3(0, height * 0.5f, -0.05f), new Vector3(width + 0.1f, 0.12f, 0.16f), frameMat);
            CreateBox("Frame_Sill", win.transform, centerPos + new Vector3(0, -height * 0.5f, -0.08f), new Vector3(width + 0.18f, 0.14f, 0.24f), sillMat);
            CreateBox("Frame_L", win.transform, centerPos + new Vector3(-width * 0.5f, 0, -0.05f), new Vector3(0.12f, height, 0.16f), frameMat);
            CreateBox("Frame_R", win.transform, centerPos + new Vector3(width * 0.5f, 0, -0.05f), new Vector3(0.12f, height, 0.16f), frameMat);

            for (int c = 1; c < cols; c++)
            {
                float mx = centerPos.x - width * 0.5f + c * (width / cols);
                CreateBox($"Mullion_V_{c}", win.transform, new Vector3(mx, centerPos.y, centerPos.z - 0.03f), new Vector3(0.045f, height - 0.12f, 0.05f), frameMat);
            }
            for (int r = 1; r < rows; r++)
            {
                float my = centerPos.y - height * 0.5f + r * (height / rows);
                CreateBox($"Mullion_H_{r}", win.transform, new Vector3(centerPos.x, my, centerPos.z - 0.03f), new Vector3(width - 0.12f, 0.045f, 0.05f), frameMat);
            }
        }
        #endregion

        #region 8. Jugador Oficial UTOP (Skin 3D + 10 Animaciones Esqueléticas con Mezcla Suave) y Cámara Dual
        private static GameObject BuildPlayerAndCamera(Transform root, Dictionary<string, Material> mats)
        {
            // Ubicación inicial: En el PRIMER PISO (Planta Alta) del edificio de la UTOP,
            // justo a la entrada de la Oficina del Capitán (X = 10.0m, Y = 3.75m, Z = 4.5m) mirando directamente al Capitán (+Z)
            Vector3 spawnPos = new Vector3(10.0f, UTOPInteriorAndCompoundBuilder.FirstFloorY + 0.06f, 4.5f);

            var playerGo = new GameObject("Player_Oficial");
            playerGo.transform.position = spawnPos;
            playerGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // Mirando hacia el Capitán de la UTOP en su escritorio
            playerGo.tag = "Player";

            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.82f;
            cc.radius = 0.30f;
            cc.center = new Vector3(0, 0.91f, 0);
            cc.stepOffset = 0.38f;
            cc.slopeLimit = 55f;

            // Configurar el modelo 3D con Skin UTOP y los 10 clips de movimiento procesados
            SetupUTOPSkinnedCharacterAndAnimations(playerGo, mats);

            var pc = playerGo.AddComponent<PlayerController>();
            playerGo.AddComponent<PlayerInventorySystem>();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 62f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();

            var dualCam = camGo.AddComponent<DualPerspectiveCamera>();
            dualCam.target = playerGo.transform;
            dualCam.playerController = pc;
            dualCam.isFirstPerson = false; // Inicia en 3ra Persona para apreciar la Skin UTOP y sus 10 movimientos ([V] alterna a 1ra Persona)
            dualCam.eyeHeight = 1.68f;
            dualCam.tpTargetOffset = new Vector3(0f, 1.38f, 0f);
            dualCam.tpDistance = 3.1f;

            camGo.transform.position = spawnPos + new Vector3(0f, 1.85f, -2.8f);
            camGo.transform.rotation = Quaternion.Euler(10f, 0f, 0f);

            pc.cameraTransform = camGo.transform;

            return playerGo;
        }

        private static void SetupUTOPSkinnedCharacterAndAnimations(GameObject playerGo, Dictionary<string, Material> mats)
        {
            string charDir = "Assets/_Project/Art/Characters/UTOP_Officer";
            string fbxPath = $"{charDir}/UTOP_Officer_Animations.fbx";
            string clipsDir = $"{charDir}/Clips";

            if (!AssetDatabase.IsValidFolder(clipsDir))
            {
                AssetDatabase.CreateFolder(charDir, "Clips");
            }

            // 1. Configurar Texturas PBR y Material del Oficial UTOP
            string normalPath = $"{charDir}/texture_normal.png";
            var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            var texDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>($"{charDir}/texture_diffuse.png");
            if (texDiffuse == null)
                texDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>($"{charDir}/texture_pbr.png");
            var texNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var texMetallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{charDir}/texture_metallic.png");

            string matPath = $"{charDir}/Mat_UTOP_Officer_Skin.mat";
            var skinMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            if (skinMat == null)
            {
                skinMat = new Material(urpLit) { name = "Mat_UTOP_Officer_Skin" };
                AssetDatabase.CreateAsset(skinMat, matPath);
            }
            skinMat.color = Color.white;
            if (skinMat.HasProperty("_BaseColor")) skinMat.SetColor("_BaseColor", Color.white);
            if (texDiffuse != null)
            {
                skinMat.mainTexture = texDiffuse;
                if (skinMat.HasProperty("_BaseMap")) skinMat.SetTexture("_BaseMap", texDiffuse);
            }
            if (texNormal != null && skinMat.HasProperty("_BumpMap"))
            {
                skinMat.EnableKeyword("_NORMALMAP");
                skinMat.SetTexture("_BumpMap", texNormal);
                skinMat.SetFloat("_BumpScale", 1.0f);
            }
            if (texMetallic != null && skinMat.HasProperty("_MetallicGlossMap"))
            {
                skinMat.SetTexture("_MetallicGlossMap", texMetallic);
            }
            if (skinMat.HasProperty("_Smoothness")) skinMat.SetFloat("_Smoothness", 0.36f);
            if (skinMat.HasProperty("_Metallic")) skinMat.SetFloat("_Metallic", 0.12f);
            EditorUtility.SetDirty(skinMat);

            // 2. Instanciar el modelo 3D articulado con su esqueleto de 52 huesos
            var fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxPrefab == null)
            {
                Debug.LogWarning("[CalleJunin] No se encontró UTOP_Officer_Animations.fbx, usando respaldo procedural.");
                var humanoid = HumanoidModelBuilder.BuildHumanoid(
                    playerGo,
                    mats["Mat_CJ_UTOP_Green"],
                    mats["Mat_CJ_Window_FrameDark"],
                    mats["Mat_CJ_Traffic_Cone"],
                    mats["Mat_CJ_Window_Glass"]
                );
                var procAnim = playerGo.AddComponent<ProceduralHumanoidAnimator>();
                procAnim.humanoid = humanoid;
                procAnim.CaptureInitialPoses();
                return;
            }

            var visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            visualInstance.name = "UTOP_Officer_Model";
            visualInstance.transform.SetParent(playerGo.transform, false);
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;

            // Conservar la malla original completa del FBX del Oficial UTOP para máxima calidad y detalle
            SkinnedMeshRenderer srcSmr = fbxPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Mesh playerFullMesh = srcSmr != null ? srcSmr.sharedMesh : null;

            foreach (var smr in visualInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (playerFullMesh != null) smr.sharedMesh = playerFullMesh;
                smr.sharedMaterial = skinMat;
                smr.quality = SkinQuality.Bone4;
                smr.updateWhenOffscreen = true;
            }

            var animator = visualInstance.GetComponent<Animator>();
            if (animator == null) animator = visualInstance.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 3. Procesar y cargar los 10 clips de animación In-Place + los 4 clips sintetizados (Erguido, Firmes, Puño, Jalar Silla)
            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            var processedClips = new Dictionary<string, AnimationClip>();

            foreach (var asset in allSubAssets)
            {
                if (asset is AnimationClip srcClip && !srcClip.name.StartsWith("__preview__"))
                {
                    string cleanName = srcClip.name.Replace("Armature|", "");
                    bool isLooping = cleanName.Contains("Idle") ||
                                     cleanName.Contains("Walking") ||
                                     cleanName.Contains("Sprint") ||
                                     cleanName.Contains("RunBackward") ||
                                     cleanName.Contains("Sitting");
                    bool rotateBackward180 = cleanName.Contains("RunBackward");

                    AnimationClip readyClip = CreateOrUpdateInPlaceClip(srcClip, $"{clipsDir}/{cleanName}_InPlace.anim", isLooping, rotateBackward180);
                    processedClips[cleanName] = readyClip;
                }
            }

            AnimationClip baseSalute = null;
            processedClips.TryGetValue("HumanSpSalute", out baseSalute);
            if (baseSalute != null)
            {
                CharacterAssetsAndClipsProcessor.GenerateSynthesizedUTOPClips(baseSalute, clipsDir, processedClips);
            }

            // 4. Vincular UTOPCharacterAnimationController con Marcha Táctica predeterminada, Erguido Natural, Firmes [F], Puño [R] y Silla [2/Z]
            var animCtrl = playerGo.AddComponent<UTOPCharacterAnimationController>();
            animCtrl.targetAnimator = animator;
            animCtrl.tacticalMarchMode = true;
            animCtrl.AutoFindBones();

            processedClips.TryGetValue("HumanSpUprightIdle",           out animCtrl.idleClip);
            processedClips.TryGetValue("HumanSpWalking",               out animCtrl.walkClip);
            processedClips.TryGetValue("HumanSpWalking6",              out animCtrl.tacticalWalkClip);
            processedClips.TryGetValue("HumanSpTwoCycleSprint",        out animCtrl.sprintClip);
            processedClips.TryGetValue("HumanSpRunBackward",           out animCtrl.runBackwardClip);
            processedClips.TryGetValue("HumanSpRunningJump",           out animCtrl.jumpClip);
            processedClips.TryGetValue("HumanSpQuickRollToRun",        out animCtrl.rollClip);
            processedClips.TryGetValue("HumanSpSalute",                out animCtrl.saluteClip);
            processedClips.TryGetValue("HumanSpSitting",               out animCtrl.sitClip);
            processedClips.TryGetValue("HumanSpTwoHandedSwordDeath",   out animCtrl.knockdownClip);
            processedClips.TryGetValue("HumanSpMilitaryAttention",     out animCtrl.militaryAttentionClip);
            processedClips.TryGetValue("HumanSpRightPunch",            out animCtrl.rightPunchClip);
            processedClips.TryGetValue("HumanSpPullChair",             out animCtrl.pullChairClip);

            // Evaluar la postura Erguida Natural en Edit Mode
            if (animCtrl.idleClip != null)
            {
                animCtrl.idleClip.SampleAnimation(visualInstance, 0.1f);
                foreach (var smr in visualInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.enabled = false;
                    smr.enabled = true;
                }
            }
        }

        private static AnimationClip CreateOrUpdateInPlaceClip(AnimationClip sourceClip, string assetPath, bool loopTime, bool fixBackwardYaw180)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            var newClip = new AnimationClip { name = Path.GetFileNameWithoutExtension(assetPath) };
            EditorUtility.CopySerialized(sourceClip, newClip);

            // Configurar Loop Time para ciclos continuos
            var settings = AnimationUtility.GetAnimationClipSettings(newClip);
            settings.loopTime = loopTime;
            settings.loopBlend = loopTime;
            AnimationUtility.SetAnimationClipSettings(newClip, settings);

            // 1) Eliminar desplazamiento horizontal Root Motion en Armature/Hips (m_LocalPosition.x y m_LocalPosition.y -> 0)
            //    conservando intacto m_LocalPosition.z (altura vertical: bote al caminar, salto, sentarse y caída)
            var bindPosX = EditorCurveBinding.FloatCurve("Armature/Hips", typeof(Transform), "m_LocalPosition.x");
            var bindPosY = EditorCurveBinding.FloatCurve("Armature/Hips", typeof(Transform), "m_LocalPosition.y");

            var zeroCurve = AnimationCurve.Linear(0f, 0f, sourceClip.length, 0f);
            AnimationUtility.SetEditorCurve(newClip, bindPosX, zeroCurve);
            AnimationUtility.SetEditorCurve(newClip, bindPosY, zeroCurve);

            // 2) En HumanSpRunBackward, el Hips original mira hacia -Z. Aplicamos rotación exacta de 180° alrededor del eje vertical (Z local del Armature):
            //    -( (0, 0, 1, 0) * (x, y, z, w) ) = (y, -x, -w, z), alineándolo a +Z en el mismo hemisferio cuaterniónico que los otros 9 clips.
            if (fixBackwardYaw180)
            {
                var bindRotX = EditorCurveBinding.FloatCurve("Armature/Hips", typeof(Transform), "m_LocalRotation.x");
                var bindRotY = EditorCurveBinding.FloatCurve("Armature/Hips", typeof(Transform), "m_LocalRotation.y");
                var bindRotZ = EditorCurveBinding.FloatCurve("Armature/Hips", typeof(Transform), "m_LocalRotation.z");
                var bindRotW = EditorCurveBinding.FloatCurve("Armature/Hips", typeof(Transform), "m_LocalRotation.w");

                var cx = AnimationUtility.GetEditorCurve(sourceClip, bindRotX);
                var cy = AnimationUtility.GetEditorCurve(sourceClip, bindRotY);
                var cz = AnimationUtility.GetEditorCurve(sourceClip, bindRotZ);
                var cw = AnimationUtility.GetEditorCurve(sourceClip, bindRotW);

                if (cx != null && cy != null && cz != null && cw != null)
                {
                    var nx = new AnimationCurve();
                    var ny = new AnimationCurve();
                    var nz = new AnimationCurve();
                    var nw = new AnimationCurve();

                    int count = cx.keys.Length;
                    for (int i = 0; i < count; i++)
                    {
                        float t = cx.keys[i].time;
                        float ox = cx.Evaluate(t);
                        float oy = cy.Evaluate(t);
                        float oz = cz.Evaluate(t);
                        float ow = cw.Evaluate(t);

                        nx.AddKey(t,  oy);
                        ny.AddKey(t, -ox);
                        nz.AddKey(t, -ow);
                        nw.AddKey(t,  oz);
                    }

                    AnimationUtility.SetEditorCurve(newClip, bindRotX, nx);
                    AnimationUtility.SetEditorCurve(newClip, bindRotY, ny);
                    AnimationUtility.SetEditorCurve(newClip, bindRotZ, nz);
                    AnimationUtility.SetEditorCurve(newClip, bindRotW, nw);
                }
            }

            if (existing == null)
            {
                AssetDatabase.CreateAsset(newClip, assetPath);
                return newClip;
            }
            else
            {
                EditorUtility.CopySerialized(newClip, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
        }
        #endregion

        #region 9. HUD Táctico (Con Guía Completa de los 10 Movimientos UTOP)
        private static void BuildHUD(GameObject player)
        {
            var canvasGo = new GameObject("CalleJunin_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (fontAsset == null) fontAsset = TMP_Settings.defaultFontAsset;

            // 1. Banner Superior Izquierdo (ajustado para convivir con la Barra de Vida al 100% justo debajo)
            var banner = new GameObject("Banner_Officer", typeof(RectTransform), typeof(Image));
            banner.transform.SetParent(canvasGo.transform, false);
            var bRect = banner.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0, 1);
            bRect.anchorMax = new Vector2(0, 1);
            bRect.pivot = new Vector2(0, 1);
            bRect.anchoredPosition = new Vector2(16, -12);
            bRect.sizeDelta = new Vector2(320, 72);
            banner.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.10f, 0.88f);

            var tTitle = CreateText(banner.transform, "Title", "SIMULADOR DE BLOQUEOS — CALLE JUNÍN (UTOP)", 11, FontStyles.Bold, new Color(0.35f, 0.92f, 0.55f), fontAsset, new Vector2(300, 18));
            tTitle.rectTransform.anchoredPosition = new Vector2(10, -8);

            var tOfficer = CreateText(banner.transform, "OfficerName", "Subt. Oficial en Prácticas", 14, FontStyles.Bold, Color.white, fontAsset, new Vector2(300, 22));
            tOfficer.rectTransform.anchoredPosition = new Vector2(10, -26);

            var tUnit = CreateText(banner.transform, "OfficerCode", "POL-2026-UTOP | Base Central La Paz", 11, FontStyles.Normal, new Color(0.75f, 0.80f, 0.85f), fontAsset, new Vector2(300, 18));
            tUnit.rectTransform.anchoredPosition = new Vector2(10, -48);

            // 2. Botón Volver al Menú
            var btnReturn = new GameObject("Btn_ReturnMenu", typeof(RectTransform), typeof(Image), typeof(Button));
            btnReturn.transform.SetParent(canvasGo.transform, false);
            var rRect = btnReturn.GetComponent<RectTransform>();
            rRect.anchorMin = new Vector2(1, 1);
            rRect.anchorMax = new Vector2(1, 1);
            rRect.pivot = new Vector2(1, 1);
            rRect.anchoredPosition = new Vector2(-20, -20);
            rRect.sizeDelta = new Vector2(170, 40);
            btnReturn.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f, 0.9f);

            var btnText = CreateText(btnReturn.transform, "Text", "Volver al Menú", 14, FontStyles.Bold, Color.white, fontAsset, new Vector2(170, 40));
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.rectTransform.anchorMin = Vector2.zero;
            btnText.rectTransform.anchorMax = Vector2.one;
            btnText.rectTransform.sizeDelta = Vector2.zero;
            btnText.rectTransform.anchoredPosition = Vector2.zero;

            var baseHUD = canvasGo.AddComponent<SimulationBaseHUD>();
            baseHUD.rankAndNameText = tOfficer;
            baseHUD.codeAndUnitText = tUnit;
            baseHUD.returnMenuButton = btnReturn.GetComponent<Button>();

            // 3. Guía de controles (V5.0: Marcha Táctica predeterminada, Salud <15%/0%, Silla contextual, Puño, Firmes y Formación 3x10)
            var guide = new GameObject("Controls_Guide", typeof(RectTransform), typeof(Image));
            guide.transform.SetParent(canvasGo.transform, false);
            var gRect = guide.GetComponent<RectTransform>();
            gRect.anchorMin = new Vector2(1, 0);
            gRect.anchorMax = new Vector2(1, 0);
            gRect.pivot = new Vector2(1, 0);
            gRect.anchoredPosition = new Vector2(-16, 16);
            gRect.sizeDelta = new Vector2(360, 232);
            guide.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.10f, 0.88f);

            var gText = CreateText(guide.transform, "Text",
                "<b>CONTROLES Y SISTEMA TÁCTICO UTOP:</b>\n" +
                "• <b>W/A/D:</b> Marcha Táctica (Predet.) | <b>S:</b> Retroceder\n" +
                "• <b>Shift:</b> Correr (Sprint) | <b>Espacio:</b> Saltar | <b>Q:</b> Rodar\n" +
                "• <b>F:</b> <color=#FFF176>Posición de Firmes Militar</color> (Detenido = Erguido)\n" +
                "• <b>1 o G:</b> <color=#81C784>Saludo Policial</color> (Capitán y Tropa responden)\n" +
                "• <b>2 o Z:</b> <color=#FFD54F>Sentarse</color> (Sólo frente a una silla real)\n" +
                "• <b>R / Click Izq:</b> <color=#FF8A65>Golpe con Puño Derecho</color>\n" +
                "• <b>B / Enter (en Emblema Patio):</b> <color=#4FC3F7>Formar 30 Policías (3×10)</color>\n" +
                "• <b>Vida:</b> <15% Caminado Herido | 0% Caída (<b>-/K</b> Daño, <b>+/L</b> Curar)\n" +
                "• <b>E:</b> Abrir/Cerrar Puertas | <b>V:</b> 1ra / 3ra Persona",
                10.5f, FontStyles.Normal, new Color(0.88f, 0.90f, 0.94f), fontAsset, new Vector2(338, 218));
            gText.textWrappingMode = TextWrappingModes.Normal;
            gText.rectTransform.anchoredPosition = new Vector2(12, -8);
        }
        #endregion

        #region Helpers Base
        private static GameObject CreateBox(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateSlopedBox(string name, Transform parent, Vector3 pos, Vector3 size, float angleX, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(angleX, 0, 0);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string content, float size, FontStyles style, Color color, TMP_FontAsset font, Vector2 boxSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = boxSize;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = content;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.extraPadding = true;
            return tmp;
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene("Assets/_Project/Scenes/MainMenu/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/_Project/Scenes/BaseUTOP/BaseUTOP.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        #endregion
    }
}
#endif
