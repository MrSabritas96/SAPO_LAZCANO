#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using SimuladorBloqueos;
using SimuladorBloqueos.Environment;
using SimuladorBloqueos.Environment.Editor;

namespace SimuladorBloqueos.EditorTools
{
    /// <summary>
    /// Constructor integral del interior funcional de 2 pisos del edificio UTOP,
    /// con distribución arquitectónica limpia (cero colisiones de capas / Z-fighting),
    /// caja de gradas amplia con descansos y barandas continuas, puertas interactivas con bisagra ([E]),
    /// Oficina del Capitán (Imagen 2), Sala de Operaciones, Secretaría,
    /// Baños reales con paredes opacas, piso de azulejo, cubículos con puertas e inodoros/lavamanos esculpidos,
    /// Garaje plano con Armería Antidisturbios (Imagen 3), Patio de Honor cerrado (Imágenes 4 y 5)
    /// y Cuadra de Tropa con literas dobles y roperos.
    /// </summary>
    public static class UTOPInteriorAndCompoundBuilder
    {
        public const float GarageFloorY = -0.30f; // Nivel plano de Planta Baja (Garaje, Armería, Patio de Honor y Cuadras)
        public const float FirstFloorY  =  3.70f; // Nivel plano del Primer Piso (Oficina del Capitán, Oficinas, Baños)
        public const float SecondFloorY =  7.50f; // Techo del Primer Piso / Piso 2
        public const float RoofTopY     = 11.20f; // Azotea superior del bloque principal

        public static void BuildCompleteUTOPCompound(Transform root, Dictionary<string, Material> mats)
        {
            var compound = new GameObject("UTOP_Functional_Headquarters_And_Compound");
            compound.transform.SetParent(root, false);

            EnsureInteriorMaterials(mats);

            // 1. Cimentación y Pisos Nivelados sin superposición (Cero Z-Fighting)
            BuildLevelGroundAndGarage(compound.transform, mats);

            // 2. Cerramiento Exterior del Bloque Principal UTOP y Muro Perimetral hacia Calle Junín
            BuildStreetFacadeAndPerimeterWall(compound.transform, mats);

            // 3. Escalera Interna ("Las Gradas") con Descansos Amplios y Barandas Continuas
            BuildInternalStaircaseAndGuardrails(compound.transform, mats);

            // 4. Cuarto Grande de Armamento Antidisturbios / Armería UTOP en el Garaje (Imagen 3)
            BuildRiotArmoryRoom(compound.transform, mats);

            // 5. Primer Piso Bien Distribuido: Pasillo Central, Oficina del Capitán, Secretaría, Operaciones y Baños Reales Opacos
            BuildFirstFloorWellDistributedLayout(compound.transform, mats);

            // 6. Patio de Honor Techado y Abierto (Imagen 5) + Pabellón de Cuadras con Literas y Roperos
            BuildPatioDeHonorAndTroopBarracks(compound.transform, mats);
        }

        #region 0. Materiales Específicos de Interior (Madera Caoba, Azulejos Cerámicos, Porcelana, Cromo, Espejo)
        private static void EnsureInteriorMaterials(Dictionary<string, Material> mats)
        {
            mats["Mat_CJ_Road_Asphalt"]      = mats["Mat_CJ_Asphalt"];
            mats["Mat_CJ_Sidewalk_Curb"]     = mats["Mat_CJ_Curb_Stone"];
            mats["Mat_CJ_UTOP_BannerSign"]   = mats["Mat_CJ_UTOP_Banner"];
            mats["Mat_CJ_Tinglado_RoofDark"] = mats["Mat_CJ_Corrugated_Roof"];
            mats["Mat_CJ_Tinglado_Skylight"] = mats["Mat_CJ_Translucent_Roof"];

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");

            string texDir = "Assets/_Project/Art/Textures/CalleJunin";
            var texWood         = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Tex_Fine_Wood.png");
            var texPortrait     = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Tex_President_Portrait.png");
            var texFlagBol      = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Tex_Flag_Bolivia.png");
            var texWiphala      = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Tex_Flag_Wiphala.png");
            var texArmoryShield = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Tex_UTOP_Armory_Shield.png");
            var texFloorEmblem  = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Tex_UTOP_Floor_Emblem.png");
            var texAmmoCrate    = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Tex_Ammo_Crate.png");
            var texStucco       = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Tex_Stucco_Detail.png");
            var texBathTile     = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Tex_Bathroom_Tile.png");

            mats["Mat_Int_Mahogany"]          = MakeMat(lit, new Color(0.45f, 0.25f, 0.13f), 0.58f, 0.05f, texWood, new Vector2(2f, 2f));
            mats["Mat_Int_DarkWood"]          = MakeMat(lit, new Color(0.26f, 0.14f, 0.07f), 0.64f, 0.08f, texWood, new Vector2(1.5f, 1.5f));
            mats["Mat_Int_ParquetFloor"]      = MakeMat(lit, new Color(0.54f, 0.36f, 0.21f), 0.62f, 0.02f, texWood, new Vector2(8f, 8f));
            mats["Mat_Int_OfficeWallGrey"]    = MakeMat(lit, new Color(0.48f, 0.49f, 0.47f), 0.25f, 0f, texStucco, new Vector2(3f, 3f));
            mats["Mat_Int_CreamWall"]         = MakeMat(lit, new Color(0.88f, 0.87f, 0.82f), 0.25f, 0f, texStucco, new Vector2(4f, 4f));
            mats["Mat_Int_LeatherBlack"]      = MakeMat(lit, new Color(0.11f, 0.11f, 0.13f), 0.68f, 0.12f, null, Vector2.one);
            mats["Mat_Int_LeatherBurgundy"]   = MakeMat(lit, new Color(0.28f, 0.10f, 0.09f), 0.65f, 0.08f, null, Vector2.one);
            mats["Mat_Int_Gold"]              = MakeMat(lit, new Color(0.88f, 0.72f, 0.22f), 0.80f, 0.82f, null, Vector2.one);
            mats["Mat_Int_Chrome"]            = MakeMat(lit, new Color(0.86f, 0.89f, 0.92f), 0.92f, 0.90f, null, Vector2.one);
            mats["Mat_Int_Porcelain"]         = MakeMat(lit, new Color(0.96f, 0.97f, 0.98f), 0.88f, 0.04f, null, Vector2.one);
            mats["Mat_Int_WaterBlue"]         = MakeMat(lit, new Color(0.45f, 0.75f, 0.88f), 0.95f, 0.10f, null, Vector2.one);
            mats["Mat_Int_MirrorGlass"]       = MakeMat(lit, new Color(0.88f, 0.93f, 0.96f), 0.98f, 0.95f, null, Vector2.one);
            mats["Mat_Int_UniformOlive"]      = MakeMat(lit, new Color(0.22f, 0.28f, 0.14f), 0.28f, 0.02f, null, Vector2.one);
            mats["Mat_Int_Skin"]              = MakeMat(lit, new Color(0.80f, 0.62f, 0.48f), 0.30f, 0f, null, Vector2.one);

            // Azulejos cerámicos con junta para el suelo y las paredes del baño
            mats["Mat_Int_BathroomTileFloor"] = MakeMat(lit, new Color(0.92f, 0.95f, 0.97f), 0.78f, 0.05f, texBathTile, new Vector2(6f, 10f));
            mats["Mat_Int_BathroomWallTile"]  = MakeMat(lit, new Color(0.95f, 0.97f, 0.98f), 0.82f, 0.04f, texBathTile, new Vector2(8f, 5f));
            mats["Mat_Int_GraniteCounter"]    = MakeMat(lit, new Color(0.24f, 0.26f, 0.28f), 0.84f, 0.15f, texStucco, new Vector2(4f, 2f));

            mats["Mat_Int_Portrait"]          = MakeMat(lit, Color.white, 0.45f, 0f, texPortrait, Vector2.one);
            mats["Mat_Int_FlagBolivia"]       = MakeMat(lit, Color.white, 0.25f, 0f, texFlagBol, Vector2.one);
            mats["Mat_Int_FlagWiphala"]       = MakeMat(lit, Color.white, 0.25f, 0f, texWiphala, Vector2.one);
            mats["Mat_Int_ArmoryShield"]      = MakeMat(lit, Color.white, 0.55f, 0.35f, texArmoryShield, Vector2.one);
            mats["Mat_Int_PatioEmblem"]       = MakeMat(lit, Color.white, 0.35f, 0f, texFloorEmblem, Vector2.one);
            mats["Mat_Int_AmmoCrate"]         = MakeMat(lit, Color.white, 0.25f, 0f, texAmmoCrate, Vector2.one);

            mats["Mat_Int_ArmoryDarkWall"]    = MakeMat(lit, new Color(0.18f, 0.20f, 0.22f), 0.25f, 0.05f, texStucco, new Vector2(4f, 4f));
            mats["Mat_Int_SteelRack"]         = MakeMat(lit, new Color(0.12f, 0.13f, 0.15f), 0.58f, 0.65f, null, Vector2.one);
            mats["Mat_Int_GunMetal"]          = MakeMat(lit, new Color(0.15f, 0.16f, 0.18f), 0.68f, 0.78f, null, Vector2.one);
            mats["Mat_Int_RiotHelmet"]        = MakeMat(lit, new Color(0.08f, 0.09f, 0.11f), 0.85f, 0.25f, null, Vector2.one);
            mats["Mat_Int_BathroomTile"]      = mats["Mat_Int_BathroomTileFloor"];

            var glassMat = new Material(lit);
            glassMat.color = new Color(0.72f, 0.84f, 0.92f, 0.32f);
            glassMat.SetFloat("_Surface", 1f);
            glassMat.SetFloat("_Blend", 0f);
            glassMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glassMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glassMat.SetInt("_ZWrite", 0);
            glassMat.renderQueue = 3000;
            glassMat.SetFloat("_Smoothness", 0.90f);
            mats["Mat_Int_ClearGlass"] = glassMat;
        }

        private static Material MakeMat(Shader s, Color col, float smooth, float metallic, Texture2D tex, Vector2 scale)
        {
            var m = new Material(s);
            m.color = col;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = scale;
                if (m.HasProperty("_BaseMap"))
                {
                    m.SetTexture("_BaseMap", tex);
                    m.SetTextureScale("_BaseMap", scale);
                }
            }
            return m;
        }
        #endregion

        #region 1. Garaje Plano Nivelado y Cimentación (Cero Z-Fighting)
        private static void BuildLevelGroundAndGarage(Transform parent, Dictionary<string, Material> mats)
        {
            var garage = new GameObject("01_Flat_Garage_And_Foundation");
            garage.transform.SetParent(parent, false);

            // Cimentación profunda colocada 0.35m DEBAJO de GarageFloorY para que JAMÁS compita ni titile con los pisos
            Box("Compound_Deep_Foundation", garage.transform,
                new Vector3(21.0f, GarageFloorY - 10.35f, -13.0f),
                new Vector3(30.4f, 20.0f, 46.0f),
                mats["Mat_CJ_Sidewalk_Stone"]);

            // Piso plano del Garaje y Armería (X = +6.0m a +24.0m, Z = -2.35m a +10.0m, Tope exacto = GarageFloorY)
            Box("Garage_Level_Floor", garage.transform,
                new Vector3(15.0f, GarageFloorY - 0.12f, 3.825f),
                new Vector3(18.0f, 0.24f, 12.35f),
                mats["Mat_CJ_Road_Asphalt"]);

            // Rampa suave en el umbral del portón vehicular hacia la Calle Junín
            var ramp = Box("Entrance_Threshold_Ramp", garage.transform,
                new Vector3(6.15f, GarageFloorY - 0.06f, 2.75f),
                new Vector3(1.2f, 0.16f, 6.5f),
                mats["Mat_CJ_Sidewalk_Stone"]);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, -2.5f);

            // Vigas casetonadas del techo del Garaje: ubicadas SOLO en Z = +0.3m a +5.5m (X = 6.6m a 18.2m)
            // para no invadir jamás el hueco de las gradas (Z = -2.2m a -0.1m) ni el piso superior
            for (int gx = 0; gx < 4; gx++)
            {
                Box($"Garage_Ceiling_Beam_X_{gx}", garage.transform,
                    new Vector3(7.4f + gx * 3.4f, FirstFloorY - 0.30f, 2.9f),
                    new Vector3(0.22f, 0.22f, 5.2f),
                    mats["Mat_CJ_Classical_Ochre"]);
            }
            for (int gz = 0; gz < 4; gz++)
            {
                Box($"Garage_Ceiling_Beam_Z_{gz}", garage.transform,
                    new Vector3(12.5f, FirstFloorY - 0.30f, 0.5f + gz * 1.55f),
                    new Vector3(11.4f, 0.22f, 0.20f),
                    mats["Mat_CJ_Classical_Ochre"]);
            }

            // Patrulla Policial UTOP Doble Cabina estacionada en el Garaje (dejando amplio paso hacia las gradas y la armería)
            ProceduralMeshBuilder.BuildUTOPPatrolPickup(
                garage.transform,
                new Vector3(14.0f, GarageFloorY, 2.4f),
                yaw: -90f,
                bodyMat: mats["Mat_CJ_Car_White"],
                mats: mats);

            CreateCeilingLight(garage.transform, new Vector3(10.5f, FirstFloorY - 0.35f, 2.5f), new Color(0.95f, 0.96f, 1.0f), 2.8f, 12f);
            CreateCeilingLight(garage.transform, new Vector3(18.5f, FirstFloorY - 0.35f, 2.5f), new Color(0.95f, 0.96f, 1.0f), 2.8f, 12f);
        }
        #endregion

        #region 2. Fachada Exterior y Cerramiento Estructural del Edificio UTOP
        private static void BuildStreetFacadeAndPerimeterWall(Transform parent, Dictionary<string, Material> mats)
        {
            var ext = new GameObject("02_Street_Facade_And_Perimeter");
            ext.transform.SetParent(parent, false);

            float facadeX = 5.8f;

            // A. Muro verde exterior de la UTOP en la subida (Z = -28.0m a -2.4m) — Réplica exacta de la 4ta Imagen (Google Street View):
            // 1) Tramo Alto de Guardia pegado al edificio UTOP (Z = -9.0m a -2.4m, largo 6.6m):
            //    Muro verde alto (hasta Y = FirstFloorY + 1.85m = 5.55m) con cumbrera roja superior, zócalo de piedra y ventanilla de guardia.
            float tallZStart = -9.0f;
            float tallZEnd = -2.4f;
            float tallZLen = tallZEnd - tallZStart; // 6.6m
            float tallZMid = (tallZStart + tallZEnd) * 0.5f; // -5.7m
            float tallRoadY = CalleJuninSceneBuilder.GetRoadY(tallZMid);
            float tallPlinthTopY = tallRoadY + 0.90f;
            float tallWallTopY = FirstFloorY + 1.85f; // 5.55m

            Box("UTOP_Street_TallGuard_Plinth", ext.transform,
                new Vector3(facadeX + 0.16f, (tallPlinthTopY - 6f) * 0.5f, tallZMid),
                new Vector3(0.44f, tallPlinthTopY + 6f, tallZLen + 0.04f),
                mats["Mat_CJ_Sidewalk_Curb"]);

            float tallGreenH = tallWallTopY - tallPlinthTopY;
            Box("UTOP_Street_TallGuard_GreenWall", ext.transform,
                new Vector3(facadeX + 0.20f, tallPlinthTopY + tallGreenH * 0.5f, tallZMid),
                new Vector3(0.38f, tallGreenH, tallZLen + 0.04f),
                mats["Mat_CJ_UTOP_GreenWall"]);

            Box("UTOP_Street_TallGuard_RoofCoping", ext.transform,
                new Vector3(facadeX + 0.20f, tallWallTopY + 0.07f, tallZMid),
                new Vector3(0.48f, 0.14f, tallZLen + 0.12f),
                mats["Mat_CJ_UTOP_GateRust"]);

            // 2) Tramo Escalonado más Bajo cuesta arriba (Z = -28.0m a -9.0m, largo 19.0m en 5 segmentos):
            //    Deja libre y abierta la parte superior para que desde la calle se vean las columnas reticuladas rojas y las cerchas triangulares del Tinglado (4ta Imagen).
            int lowSegments = 5;
            float lowSegLen = 19.0f / lowSegments; // 3.8m por tramo
            for (int s = 0; s < lowSegments; s++)
            {
                float zStart = -28.0f + (s * lowSegLen);
                float zMid = zStart + lowSegLen * 0.5f;
                float roadY = CalleJuninSceneBuilder.GetRoadY(zMid);
                float plinthTopY = roadY + 0.82f;
                float wallTopY = roadY + 2.95f; // Altura baja que permite ver las cerchas rojas del tinglado por encima

                Box($"UTOP_Street_Plinth_{s}", ext.transform,
                    new Vector3(facadeX + 0.16f, (plinthTopY - 6f) * 0.5f, zMid),
                    new Vector3(0.42f, plinthTopY + 6f, lowSegLen + 0.05f),
                    mats["Mat_CJ_Sidewalk_Curb"]);

                float greenH = wallTopY - plinthTopY;
                Box($"UTOP_Street_GreenWall_{s}", ext.transform,
                    new Vector3(facadeX + 0.20f, plinthTopY + greenH * 0.5f, zMid),
                    new Vector3(0.36f, greenH, lowSegLen + 0.05f),
                    mats["Mat_CJ_UTOP_GreenWall"]);

                // Cumbrera y pilastras escalonadas con remate blanco/rojo en cada cambio de nivel (visibles en la 4ta Imagen)
                Box($"UTOP_Street_WallCap_{s}", ext.transform,
                    new Vector3(facadeX + 0.20f, wallTopY + 0.05f, zMid),
                    new Vector3(0.44f, 0.10f, lowSegLen + 0.06f),
                    mats["Mat_CJ_UTOP_GateRust"]);

                Box($"UTOP_Street_StepPillar_{s}", ext.transform,
                    new Vector3(facadeX + 0.18f, wallTopY + 0.18f, zStart + lowSegLen - 0.18f),
                    new Vector3(0.44f, 0.28f, 0.38f),
                    mats["Mat_CJ_UTOP_GreenWall"]);
                Box($"UTOP_Street_StepPillarCap_{s}", ext.transform,
                    new Vector3(facadeX + 0.18f, wallTopY + 0.34f, zStart + lowSegLen - 0.18f),
                    new Vector3(0.48f, 0.06f, 0.44f),
                    mats["Mat_CJ_UTOP_WhiteTrim"]);

                // Gradas / escalones horizontales de piedra en la acera junto al muro verde (4ta Imagen)
                Box($"UTOP_Sidewalk_StepLanding_{s}", ext.transform,
                    new Vector3(facadeX - 0.85f, roadY + 0.16f, zMid),
                    new Vector3(1.75f, 0.28f, lowSegLen - 0.04f),
                    mats["Mat_CJ_Sidewalk_Stone"]);
            }

            // Escalón de acera frente a la ventanilla de guardia
            for (int gs = 0; gs < 2; gs++)
            {
                float gz = -8.0f + gs * 3.2f;
                float gy = CalleJuninSceneBuilder.GetRoadY(gz);
                Box($"UTOP_Guard_SidewalkStep_{gs}", ext.transform,
                    new Vector3(facadeX - 0.85f, gy + 0.15f, gz),
                    new Vector3(1.75f, 0.26f, 3.15f),
                    mats["Mat_CJ_Sidewalk_Stone"]);
            }

            // Ventanilla de guardia en la calle (Z = -5.5m) con alféizar blanco sobresaliente y reja
            float guardY = CalleJuninSceneBuilder.GetRoadY(-5.5f);
            Box("Guard_Window_Recess", ext.transform, new Vector3(facadeX + 0.02f, guardY + 1.75f, -5.5f), new Vector3(0.14f, 1.15f, 2.2f), mats["Mat_CJ_Window_FrameDark"]);
            Box("Guard_Window_Glass", ext.transform, new Vector3(facadeX + 0.04f, guardY + 1.75f, -5.5f), new Vector3(0.10f, 1.05f, 2.08f), mats["Mat_CJ_Window_Glass"]);
            Box("Guard_Window_WhiteSill", ext.transform, new Vector3(facadeX - 0.08f, guardY + 1.14f, -5.5f), new Vector3(0.32f, 0.15f, 2.45f), mats["Mat_CJ_UTOP_WhiteTrim"]);
            for (int b = -2; b <= 2; b++)
                Box($"Guard_Bar_{b}", ext.transform, new Vector3(facadeX - 0.02f, guardY + 1.75f, -5.5f + b * 0.42f), new Vector3(0.035f, 1.08f, 0.035f), mats["Mat_CJ_Iron_Grille"]);

            // B. Pilares de Planta Baja del Edificio UTOP flanqueando el Portal Vehicular
            Box("UTOP_Front_Pillar_North", ext.transform,
                new Vector3(facadeX + 0.25f, (FirstFloorY + GarageFloorY) * 0.5f, -1.5f),
                new Vector3(0.50f, FirstFloorY - GarageFloorY, 2.0f),
                mats["Mat_CJ_UTOP_Green"]);

            Box("UTOP_Front_Pillar_South", ext.transform,
                new Vector3(facadeX + 0.25f, (FirstFloorY + GarageFloorY) * 0.5f, 8.0f),
                new Vector3(0.50f, FirstFloorY - GarageFloorY, 4.0f),
                mats["Mat_CJ_UTOP_Green"]);

            // Hojas del portón metálico rojo abiertas en la entrada vehicular
            BuildOpenGateLeaf(ext.transform, new Vector3(facadeX + 0.2f, GarageFloorY + 1.6f, -0.4f), mats["Mat_CJ_UTOP_GateRust"]);
            BuildOpenGateLeaf(ext.transform, new Vector3(facadeX + 0.2f, GarageFloorY + 1.6f,  5.9f), mats["Mat_CJ_UTOP_GateRust"]);

            // C. Letrero Institucional UTOP sobre el portal
            Box("UTOP_Banner_Backing", ext.transform,
                new Vector3(facadeX - 0.28f, FirstFloorY + 0.65f, 3.75f),
                new Vector3(0.45f, 1.65f, 12.1f),
                mats["Mat_CJ_UTOP_WhiteTrim"]);
            Box("UTOP_Banner_SignFace", ext.transform,
                new Vector3(facadeX - 0.52f, FirstFloorY + 0.65f, 3.75f),
                new Vector3(0.06f, 1.45f, 11.8f),
                mats["Mat_CJ_UTOP_BannerSign"]);

            // D. Cerramiento Exterior Completo de 2 Pisos (Sin huecos ni paredes flotantes)
            // Muro Oeste (Fachada hacia Calle Junín, X = +5.8m)
            Box("UTOP_Upper_Facade_Wall", ext.transform,
                new Vector3(facadeX + 0.20f, (RoofTopY + FirstFloorY) * 0.5f, 3.75f),
                new Vector3(0.40f, RoofTopY - FirstFloorY, 12.5f),
                mats["Mat_CJ_UTOP_Green"]);

            for (int f = 0; f < 2; f++)
            {
                float wy = FirstFloorY + 2.15f + f * 3.6f;
                for (int i = 0; i < 4; i++)
                {
                    float wz = -0.6f + i * 2.9f;
                    BuildWindowFrameWithClearGlass(ext.transform, new Vector3(facadeX - 0.04f, wy, wz), 2.35f, 1.75f, mats);
                }
            }

            // Muro Exterior Sur Completo (Z = +9.95m, desde GarageFloorY hasta RoofTopY)
            Box("UTOP_South_PartyWall", ext.transform,
                new Vector3(14.9f, (RoofTopY + GarageFloorY) * 0.5f, 9.95f),
                new Vector3(18.2f, RoofTopY - GarageFloorY, 0.35f),
                mats["Mat_CJ_UTOP_Green"]);

            // Muro Exterior Este Completo (Fondo del edificio en X = +23.85m, desde GarageFloorY hasta RoofTopY)
            Box("UTOP_East_FullBackWall", ext.transform,
                new Vector3(23.85f, (RoofTopY + GarageFloorY) * 0.5f, 3.75f),
                new Vector3(0.35f, RoofTopY - GarageFloorY, 12.5f),
                mats["Mat_CJ_UTOP_Green"]);

            // Muro Exterior Norte (Z = -2.40m):
            // 1) Parte superior continua en Piso 1 y Piso 2 (X = +5.8m a +24.0m)
            Box("UTOP_North_UpperWall", ext.transform,
                new Vector3(14.9f, (RoofTopY + FirstFloorY) * 0.5f, -2.40f),
                new Vector3(18.2f, RoofTopY - FirstFloorY, 0.35f),
                mats["Mat_CJ_UTOP_Green"]);

            // 2) Parte inferior en Planta Baja (Z = -2.40m): Cierra la caja de las gradas (X = +13.0m a +19.5m)
            // dejando un gran paso vehicular a la izquierda (X = 6.0m a 13.0m) y un pórtico peatonal a la derecha (X = 19.5m a 22.8m)
            Box("UTOP_North_GroundWall_StairEnclosure", ext.transform,
                new Vector3(16.25f, (FirstFloorY + GarageFloorY) * 0.5f, -2.40f),
                new Vector3(6.5f, FirstFloorY - GarageFloorY, 0.35f),
                mats["Mat_CJ_UTOP_Green"]);
            Box("UTOP_North_GroundWall_EastPillar", ext.transform,
                new Vector3(23.3f, (FirstFloorY + GarageFloorY) * 0.5f, -2.40f),
                new Vector3(1.4f, FirstFloorY - GarageFloorY, 0.35f),
                mats["Mat_CJ_UTOP_Green"]);
            Box("UTOP_North_GroundWall_DoorHeader", ext.transform,
                new Vector3(21.05f, FirstFloorY - 0.55f, -2.40f),
                new Vector3(3.1f, 1.1f, 0.35f),
                mats["Mat_CJ_UTOP_Green"]);

            // Pares de bajantes blancas verticales en la fachada lateral Norte (Foto 4)
            float[] pipeXs = { facadeX + 0.4f, facadeX + 0.7f, facadeX + 4.4f, facadeX + 4.7f };
            foreach (float px in pipeXs)
            {
                Box("UTOP_Side_WhitePipe", ext.transform,
                    new Vector3(px, (RoofTopY + FirstFloorY) * 0.5f, -2.62f),
                    new Vector3(0.16f, RoofTopY - FirstFloorY, 0.16f),
                    mats["Mat_CJ_UTOP_WhiteTrim"]);
            }

            // Losas superiores (Entreplanta Piso 2 y Azotea)
            Box("UTOP_SecondFloor_CeilingSlab", ext.transform,
                new Vector3(14.9f, SecondFloorY, 3.75f),
                new Vector3(18.2f, 0.25f, 12.5f),
                mats["Mat_Int_CreamWall"]);
            Box("UTOP_Roof_Slab", ext.transform,
                new Vector3(14.9f, RoofTopY, 3.75f),
                new Vector3(18.4f, 0.30f, 12.7f),
                mats["Mat_CJ_UTOP_GreenWall"]);
        }

        private static void BuildOpenGateLeaf(Transform parent, Vector3 pos, Material mat)
        {
            var g = new GameObject("UTOP_IronGate_Leaf");
            g.transform.SetParent(parent, false);
            Box("LowerPanel", g.transform, pos + new Vector3(0, -0.7f, 0), new Vector3(1.2f, 1.4f, 0.08f), mat);
            Box("TopBar", g.transform, pos + new Vector3(0, 1.5f, 0), new Vector3(1.2f, 0.08f, 0.08f), mat);
            for (int i = 0; i < 6; i++)
                Box($"Bar_{i}", g.transform, pos + new Vector3(-0.5f + i * 0.2f, 0.75f, 0), new Vector3(0.04f, 1.5f, 0.04f), mat);
        }

        private static void BuildWindowFrameWithClearGlass(Transform parent, Vector3 pos, float width, float height, Dictionary<string, Material> mats)
        {
            var w = new GameObject("Facade_Window");
            w.transform.SetParent(parent, false);
            Box("Glass", w.transform, pos, new Vector3(0.06f, height, width), mats["Mat_CJ_Window_Glass"]);
            Box("Top", w.transform, pos + new Vector3(-0.04f, height * 0.5f, 0), new Vector3(0.14f, 0.10f, width + 0.1f), mats["Mat_CJ_Window_FrameDark"]);
            Box("Sill", w.transform, pos + new Vector3(-0.08f, -height * 0.5f, 0), new Vector3(0.22f, 0.12f, width + 0.2f), mats["Mat_CJ_UTOP_WhiteTrim"]);
            for (int c = 0; c <= 3; c++)
            {
                float dz = -width * 0.5f + c * (width / 3f);
                Box($"Mullion_V_{c}", w.transform, pos + new Vector3(-0.04f, 0, dz), new Vector3(0.10f, height, 0.06f), mats["Mat_CJ_Window_FrameDark"]);
            }
            Box("Mullion_H", w.transform, pos + new Vector3(-0.04f, 0, 0), new Vector3(0.10f, 0.05f, width), mats["Mat_CJ_Window_FrameDark"]);
        }
        #endregion

        #region 3. Escalera Interna ("Las Gradas") con Descansos Amplios y Barandas Continuas
        private static void BuildInternalStaircaseAndGuardrails(Transform parent, Dictionary<string, Material> mats)
        {
            var stairs = new GameObject("03_Internal_Staircase_Gradas");
            stairs.transform.SetParent(parent, false);

            // Caja de gradas bien distribuida:
            // - Descanso superior amplio en X = +10.4m a +13.4m (3.0m libres para entrar cómodamente a las gradas!)
            // - Tramo de 16 escalones desde X = +13.4m (Y = +3.70m) bajando hacia X = +19.0m (Y = -0.30m)
            // - Descanso inferior amplio en el Garaje de X = +19.0m a +23.6m (4.6m libres al pie de las gradas!)
            // - Ancho de las gradas: Z = -2.20m a -0.15m (2.05m de ancho)
            int stepCount = 16;
            float totalRise = FirstFloorY - GarageFloorY; // 4.00m
            float totalRun = 5.60f;                       // de X = 13.40m a X = 19.00m
            float stepH = totalRise / stepCount;          // 0.25m
            float stepD = totalRun / stepCount;           // 0.35m
            float stairW = 2.02f;
            float stairCenterZ = -1.18f;

            for (int i = 0; i < stepCount; i++)
            {
                // i = 0 es el peldaño inferior en el Garaje (X = 18.825m), i = 15 es el peldaño superior junto al 1er Piso (X = 13.575m)
                float sx = 19.00f - (i + 0.5f) * stepD;
                float stepTopY = GarageFloorY + (i + 1) * stepH;
                float colH = stepTopY - GarageFloorY;

                // Cuerpo del peldaño sin colisionador individual (usamos la rampa continua suave para no trabar al jugador)
                var stepBody = Box($"Stair_Step_{i}", stairs.transform,
                    new Vector3(sx, GarageFloorY + colH * 0.5f, stairCenterZ),
                    new Vector3(stepD + 0.02f, colH, stairW),
                    mats["Mat_CJ_Sidewalk_Stone"]);
                Object.DestroyImmediate(stepBody.GetComponent<Collider>());

                var tread = Box($"Stair_WoodTread_{i}", stairs.transform,
                    new Vector3(sx, stepTopY - 0.015f, stairCenterZ),
                    new Vector3(stepD + 0.05f, 0.035f, stairW),
                    mats["Mat_Int_Mahogany"]);
                Object.DestroyImmediate(tread.GetComponent<Collider>());
            }

            // Rampa invisible perfectamente alineada con la nariz de los peldaños de (X=13.4, Y=+3.70) bajando hacia (X=19.0, Y=-0.30)
            var smoothColliderGo = new GameObject("Stair_Smooth_Collider");
            smoothColliderGo.transform.SetParent(stairs.transform, false);
            smoothColliderGo.transform.localPosition = new Vector3(16.20f, (FirstFloorY + GarageFloorY) * 0.5f - 0.04f, stairCenterZ);
            float rampAngle = Mathf.Atan2(totalRise, totalRun) * Mathf.Rad2Deg; // 35.53°
            smoothColliderGo.transform.localRotation = Quaternion.Euler(0f, 0f, -rampAngle);
            var col = smoothColliderGo.AddComponent<BoxCollider>();
            col.size = new Vector3(Mathf.Sqrt(totalRun * totalRun + totalRise * totalRise), 0.12f, stairW);

            // BARANDAS INCLINADAS EN AMBOS LADOS DE LA ESCALERA (Con pasamanos de caoba y balaustres verticales)
            float[] railZs = { -0.16f, -2.18f };
            foreach (float rz in railZs)
            {
                var handrail = Box("Stair_Sloped_Handrail", stairs.transform,
                    new Vector3(16.20f, (FirstFloorY + GarageFloorY) * 0.5f + 0.92f, rz),
                    new Vector3(6.85f, 0.07f, 0.07f),
                    mats["Mat_Int_Mahogany"]);
                handrail.transform.localRotation = Quaternion.Euler(0f, 0f, -rampAngle);

                for (int p = 0; p <= 10; p++)
                {
                    float t = p / 10f;
                    float px = Mathf.Lerp(19.00f, 13.40f, t);
                    float py = Mathf.Lerp(GarageFloorY, FirstFloorY, t);
                    Box($"Stair_Baluster_{p}", stairs.transform,
                        new Vector3(px, py + 0.46f, rz),
                        new Vector3(0.045f, 0.92f, 0.045f),
                        mats["Mat_CJ_Iron_Grille"]);
                }
            }

            // BARANDA HORIZONTAL CONTINUA DE PROTECCIÓN EN EL PRIMER PISO BORDEANDO EL HUECO DE LAS GRADAS (Z = -0.12m, X = 13.4m a 19.8m)
            BuildHorizontalBalustradeAlongX(stairs.transform, startX: 13.40f, endX: 19.80f, z: -0.12f, y0: FirstFloorY, mats);

            // Cartel indicador luminoso junto al descanso superior de las gradas (mirando hacia el pasillo -X)
            CreateWallSign(stairs.transform, new Vector3(13.40f, FirstFloorY + 2.45f, -1.18f), Quaternion.Euler(0, 90, 0),
                "▼ GRADAS A PLANTA BAJA\nGARAJE • ARMERÍA • PATIO DE HONOR", new Vector2(2.0f, 0.45f), mats);
        }

        private static void BuildHorizontalBalustradeAlongX(Transform parent, float startX, float endX, float z, float y0, Dictionary<string, Material> mats)
        {
            var bal = new GameObject("FirstFloor_Stairwell_Guardrail");
            bal.transform.SetParent(parent, false);
            float len = Mathf.Abs(endX - startX);
            float midX = (startX + endX) * 0.5f;

            // Pasamanos superior de madera caoba a 1.00m de altura y riel inferior
            Box("Top_Mahogany_Rail", bal.transform, new Vector3(midX, y0 + 0.98f, z), new Vector3(len + 0.08f, 0.08f, 0.10f), mats["Mat_Int_Mahogany"]);
            Box("Bot_Mahogany_Rail", bal.transform, new Vector3(midX, y0 + 0.12f, z), new Vector3(len, 0.06f, 0.08f), mats["Mat_Int_DarkWood"]);

            // Pilares principales en los extremos y balaustres cada 0.28m
            Box("Newel_Post_Start", bal.transform, new Vector3(startX, y0 + 0.52f, z), new Vector3(0.12f, 1.05f, 0.12f), mats["Mat_Int_Mahogany"]);
            Box("Newel_Post_End",   bal.transform, new Vector3(endX,   y0 + 0.52f, z), new Vector3(0.12f, 1.05f, 0.12f), mats["Mat_Int_Mahogany"]);

            int count = Mathf.Max(4, Mathf.RoundToInt(len / 0.28f));
            for (int i = 1; i < count; i++)
            {
                float bx = Mathf.Lerp(startX, endX, i / (float)count);
                Box($"Baluster_{i}", bal.transform, new Vector3(bx, y0 + 0.54f, z), new Vector3(0.04f, 0.84f, 0.04f), mats["Mat_CJ_Iron_Grille"]);
            }
        }
        #endregion

        #region 4. Cuarto de Armamento Antidisturbios / Armería UTOP en el Garaje (Imagen 3)
        private static void BuildRiotArmoryRoom(Transform parent, Dictionary<string, Material> mats)
        {
            var armory = new GameObject("04_Riot_Armory_Room_Armeria");
            armory.transform.SetParent(parent, false);

            float y0 = GarageFloorY;
            float roomH = FirstFloorY - GarageFloorY - 0.15f; // 3.85m
            float yMid = y0 + roomH * 0.5f;

            // Muro divisorio entre el Garaje y la Armería (Z = +5.8m) con Puerta Doble Táctica en X = +11.2m a +13.6m
            Box("Armory_NorthWall_Left", armory.transform,
                new Vector3(8.65f, yMid, 5.8f),
                new Vector3(5.1f, roomH, 0.22f),
                mats["Mat_Int_ArmoryDarkWall"]);
            Box("Armory_NorthWall_Right", armory.transform,
                new Vector3(16.1f, yMid, 5.8f),
                new Vector3(5.0f, roomH, 0.22f),
                mats["Mat_Int_ArmoryDarkWall"]);
            Box("Armory_Door_Header", armory.transform,
                new Vector3(12.4f, y0 + 3.25f, 5.8f),
                new Vector3(2.4f, 1.2f, 0.24f),
                mats["Mat_Int_ArmoryDarkWall"]);

            CreateWallSign(armory.transform, new Vector3(12.4f, y0 + 2.95f, 5.66f), Quaternion.Euler(0, 180, 0),
                "ARMERÍA Y EQUIPO ANTIDISTURBIOS — UTOP", new Vector2(2.3f, 0.38f), mats);

            // Muro de Fondo de la Armería (X = +18.5m — Imagen 3)
            Box("Armory_East_FocalWall", armory.transform,
                new Vector3(18.5f, yMid, 7.8f),
                new Vector3(0.25f, roomH, 4.0f),
                mats["Mat_Int_ArmoryDarkWall"]);

            Box("Armory_SouthWall_Inner", armory.transform,
                new Vector3(12.3f, yMid, 9.72f),
                new Vector3(12.4f, roomH, 0.15f),
                mats["Mat_Int_ArmoryDarkWall"]);

            // 1. GRAN ESCUDO INSTITUCIONAL DORADO Y NEGRO "UTOP - POLICÍA BOLIVIANA" EN EL MURO CENTRAL (Imagen 3)
            Box("Armory_Shield_BackingPanel", armory.transform,
                new Vector3(18.34f, y0 + 2.15f, 7.8f),
                new Vector3(0.08f, 2.25f, 1.85f),
                mats["Mat_Int_SteelRack"]);
            Box("Armory_Shield_Emblem", armory.transform,
                new Vector3(18.29f, y0 + 2.22f, 7.8f),
                new Vector3(0.04f, 1.85f, 1.48f),
                mats["Mat_Int_ArmoryShield"]);

            // 2. RACKS CENTRALES DE ESCOPETAS LANZAPERDIGONES Y LANZAGRANADAS DE GAS
            BuildSteelRackUnit(armory.transform, new Vector3(17.95f, y0, 7.8f), widthZ: 3.6f, depthX: 0.70f, heightY: 0.95f, shelves: 2, mats);

            for (int g = 0; g < 10; g++)
            {
                float gz = 6.25f + g * 0.34f;
                if (g == 4 || g == 5)
                {
                    if (g == 4)
                    {
                        BuildRiotHelmet(armory.transform, new Vector3(17.95f, y0 + 1.02f, 7.8f), -90f, mats);
                        BuildTacticalVest(armory.transform, new Vector3(17.92f, y0 + 0.98f, 7.15f), mats);
                        BuildTacticalVest(armory.transform, new Vector3(17.92f, y0 + 0.98f, 8.45f), mats);
                    }
                    continue;
                }
                bool isGasLauncher = (g % 2 == 1);
                BuildRiotShotgunOrGasLauncher(armory.transform, new Vector3(18.18f, y0 + 1.02f, gz), isGasLauncher, mats);
            }

            for (int c = 0; c < 3; c++)
            {
                float cz = 6.55f + c * 1.15f;
                Box($"Ammo_Crate_Bot_{c}", armory.transform, new Vector3(17.95f, y0 + 0.22f, cz), new Vector3(0.55f, 0.26f, 0.85f), mats["Mat_Int_AmmoCrate"]);
                Box($"Ammo_Crate_Top_{c}", armory.transform, new Vector3(17.95f, y0 + 0.50f, cz), new Vector3(0.55f, 0.26f, 0.85f), mats["Mat_Int_AmmoCrate"]);
            }

            // 3. ESTANTERÍAS LATERALES DE 4 NIVELES
            float[] rackXs = { 15.6f, 9.2f };
            foreach (float rx in rackXs)
            {
                BuildFullRiotGearRackWall(armory.transform, new Vector3(rx, y0, 9.25f), facingSignZ: -1f, mats);
            }
            BuildFullRiotGearRackWall(armory.transform, new Vector3(15.6f, y0, 6.30f), facingSignZ: 1f, mats);

            CreatePendantLamp(armory.transform, new Vector3(16.8f, y0 + 3.35f, 6.8f), new Color(1.0f, 0.95f, 0.85f), 3.5f, 9f, mats);
            CreatePendantLamp(armory.transform, new Vector3(16.8f, y0 + 3.35f, 8.8f), new Color(1.0f, 0.95f, 0.85f), 3.5f, 9f, mats);
            CreatePendantLamp(armory.transform, new Vector3(11.5f, y0 + 3.35f, 7.8f), new Color(0.95f, 0.96f, 1.0f), 3.0f, 10f, mats);
        }

        private static void BuildFullRiotGearRackWall(Transform parent, Vector3 basePos, float facingSignZ, Dictionary<string, Material> mats)
        {
            var rack = new GameObject("Riot_Gear_MultiTier_Rack");
            rack.transform.SetParent(parent, false);
            rack.transform.localPosition = basePos;

            float rackLenX = 3.8f;
            float rackDepthZ = 0.68f;
            Material steel = mats["Mat_Int_SteelRack"];

            float[] shelfYs = { 0.08f, 0.92f, 1.55f, 2.15f, 2.68f };
            foreach (float sy in shelfYs)
            {
                Box($"Shelf_{sy:F1}", rack.transform, new Vector3(0, sy, 0), new Vector3(rackLenX, 0.05f, rackDepthZ), steel);
            }
            for (int p = -1; p <= 1; p++)
            {
                float px = p * (rackLenX * 0.48f);
                Box($"Post_F_{p}", rack.transform, new Vector3(px, 1.40f,  rackDepthZ * 0.46f), new Vector3(0.06f, 2.80f, 0.06f), steel);
                Box($"Post_B_{p}", rack.transform, new Vector3(px, 1.40f, -rackDepthZ * 0.46f), new Vector3(0.06f, 2.80f, 0.06f), steel);
            }

            Mesh shieldMesh = ProceduralMeshBuilder.GetCurvedRiotShieldMesh();
            for (int s = 0; s < 6; s++)
            {
                float sx = -1.50f + s * 0.60f;
                var sh = ProceduralMeshBuilder.CreateMeshObject($"Riot_Shield_{s}", rack.transform,
                    new Vector3(sx, 3.18f, facingSignZ * 0.12f), shieldMesh, mats["Mat_Int_ClearGlass"], addCollider: false);
                sh.transform.localRotation = Quaternion.Euler(0f, facingSignZ > 0 ? 15f : 165f, 0f);
                Box("Shield_Band", sh.transform, new Vector3(0, 0.28f, -0.02f), new Vector3(0.46f, 0.10f, 0.02f), mats["Mat_Int_SteelRack"]);
            }

            for (int h = 0; h < 7; h++)
            {
                float hx = -1.56f + h * 0.52f;
                BuildRiotHelmet(rack.transform, new Vector3(hx, 2.30f, 0f), facingSignZ > 0 ? 0f : 180f, mats);
            }

            for (int t = 0; t < 8; t++)
            {
                float tx = -1.60f + t * 0.45f;
                if (t < 4)
                {
                    Box($"Tonfa_Baton_{t}", rack.transform, new Vector3(tx, 1.82f, 0f), new Vector3(0.04f, 0.52f, 0.04f), mats["Mat_Int_RiotHelmet"]);
                    Box($"Tonfa_Handle_{t}", rack.transform, new Vector3(tx, 1.92f, facingSignZ * 0.08f), new Vector3(0.04f, 0.04f, 0.15f), mats["Mat_Int_RiotHelmet"]);
                }
                else
                {
                    var can = Cylinder($"Gas_Canister_{t}", rack.transform, new Vector3(tx, 1.72f, 0f), new Vector3(0.09f, 0.14f, 0.09f), mats["Mat_Int_UniformOlive"]);
                    can.transform.localRotation = Quaternion.Euler(25f * facingSignZ, 0f, 15f);
                }
            }

            BuildTacticalVest(rack.transform, new Vector3(-1.25f, 1.00f, 0f), mats);
            BuildTacticalVest(rack.transform, new Vector3(-0.45f, 1.00f, 0f), mats);
            for (int m = 0; m < 4; m++)
            {
                BuildGasMask(rack.transform, new Vector3(0.35f + m * 0.38f, 1.06f, 0f), facingSignZ, mats);
            }

            Box("AmmoCrate_1", rack.transform, new Vector3(-1.15f, 0.30f, 0f), new Vector3(1.15f, 0.34f, 0.52f), mats["Mat_Int_AmmoCrate"]);
            Box("AmmoCrate_2", rack.transform, new Vector3(-1.15f, 0.64f, 0f), new Vector3(1.10f, 0.30f, 0.50f), mats["Mat_Int_AmmoCrate"]);
            for (int b = 0; b < 4; b++)
            {
                float bx = 0.25f + (b % 2) * 0.72f;
                float by = 0.28f + (b / 2) * 0.34f;
                Box($"Cartridge_Box_{b}", rack.transform, new Vector3(bx, by, 0f), new Vector3(0.64f, 0.30f, 0.48f), mats["Mat_CJ_Classical_Ochre"]);
            }
        }

        private static void BuildRiotShotgunOrGasLauncher(Transform parent, Vector3 pos, bool isGasLauncher, Dictionary<string, Material> mats)
        {
            var gun = new GameObject(isGasLauncher ? "TearGas_Launcher_37mm" : "Riot_Shotgun_12GA");
            gun.transform.SetParent(parent, false);
            gun.transform.localPosition = pos;

            Material metal = mats["Mat_Int_GunMetal"];
            Material stockMat = mats["Mat_Int_RiotHelmet"];

            Box("Buttstock", gun.transform, new Vector3(0, 0.16f, 0), new Vector3(0.12f, 0.32f, 0.05f), stockMat);
            Box("Receiver", gun.transform, new Vector3(0, 0.44f, 0), new Vector3(0.10f, 0.26f, 0.06f), metal);
            Cylinder("Barrel", gun.transform, new Vector3(0, 0.82f, 0), new Vector3(0.038f, 0.28f, 0.038f), metal);

            if (isGasLauncher)
                Cylinder("Rotary_Drum", gun.transform, new Vector3(0, 0.48f, 0), new Vector3(0.14f, 0.09f, 0.14f), metal);
            else
                Cylinder("Pump_Forend", gun.transform, new Vector3(0.02f, 0.72f, 0), new Vector3(0.06f, 0.11f, 0.06f), stockMat);
        }

        private static void BuildRiotHelmet(Transform parent, Vector3 localPos, float yaw, Dictionary<string, Material> mats)
        {
            var h = new GameObject("Riot_Helmet");
            h.transform.SetParent(parent, false);
            h.transform.localPosition = localPos;
            h.transform.localRotation = Quaternion.Euler(0, yaw, 0);

            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Helmet_Shell";
            dome.transform.SetParent(h.transform, false);
            dome.transform.localPosition = new Vector3(0, 0.10f, 0);
            dome.transform.localScale = new Vector3(0.26f, 0.24f, 0.28f);
            dome.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_Int_RiotHelmet"];
            Object.DestroyImmediate(dome.GetComponent<Collider>());

            Box("Helmet_Visor", h.transform, new Vector3(0, 0.08f, 0.13f), new Vector3(0.24f, 0.13f, 0.04f), mats["Mat_Int_ClearGlass"]);
        }

        private static void BuildTacticalVest(Transform parent, Vector3 localPos, Dictionary<string, Material> mats)
        {
            var v = new GameObject("Tactical_Riot_Vest");
            v.transform.SetParent(parent, false);
            v.transform.localPosition = localPos;
            Box("Vest_Body", v.transform, new Vector3(0, 0.04f, 0), new Vector3(0.46f, 0.08f, 0.48f), mats["Mat_Int_RiotHelmet"]);
            Box("Vest_UTOP_Patch", v.transform, new Vector3(0, 0.085f, 0.05f), new Vector3(0.22f, 0.01f, 0.08f), mats["Mat_CJ_UTOP_WhiteTrim"]);
        }

        private static void BuildGasMask(Transform parent, Vector3 localPos, float facingSignZ, Dictionary<string, Material> mats)
        {
            var m = new GameObject("Gas_Mask");
            m.transform.SetParent(parent, false);
            m.transform.localPosition = localPos;
            var face = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            face.transform.SetParent(m.transform, false);
            face.transform.localScale = new Vector3(0.18f, 0.20f, 0.18f);
            face.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_Int_RiotHelmet"];
            Object.DestroyImmediate(face.GetComponent<Collider>());
            Cylinder("Filter_L", m.transform, new Vector3(-0.07f, -0.03f, facingSignZ * 0.07f), new Vector3(0.07f, 0.04f, 0.07f), mats["Mat_Int_GunMetal"]);
            Cylinder("Filter_R", m.transform, new Vector3( 0.07f, -0.03f, facingSignZ * 0.07f), new Vector3(0.07f, 0.04f, 0.07f), mats["Mat_Int_GunMetal"]);
        }

        private static void BuildSteelRackUnit(Transform parent, Vector3 pos, float widthZ, float depthX, float heightY, int shelves, Dictionary<string, Material> mats)
        {
            var r = new GameObject("Center_Bench_Rack");
            r.transform.SetParent(parent, false);
            r.transform.localPosition = pos;
            for (int i = 0; i < shelves; i++)
            {
                float sy = 0.06f + i * (heightY - 0.08f) / Mathf.Max(1, shelves - 1);
                Box($"Bench_Shelf_{i}", r.transform, new Vector3(0, sy, 0), new Vector3(depthX, 0.05f, widthZ), mats["Mat_Int_SteelRack"]);
            }
        }
        #endregion

        #region 5. Primer Piso Bien Distribuido: Pasillo Central, Oficina del Capitán, Secretaría, Operaciones y Baños Reales Opacos
        private static void BuildFirstFloorWellDistributedLayout(Transform parent, Dictionary<string, Material> mats)
        {
            var floor1 = new GameObject("05_First_Floor_CaptainOffice_And_Admin");
            floor1.transform.SetParent(parent, false);

            float y0 = FirstFloorY; // +3.70m
            float wallH = SecondFloorY - FirstFloorY - 0.10f; // 3.70m

            // =========================================================================================
            // 1. LOSAS DE PISO DEL PRIMER PISO SIN SUPERPOSICIÓN (Con hueco exacto para las gradas: X = 13.4m a 19.8m, Z = -2.22m a -0.10m)
            // =========================================================================================
            // A. Losa Principal de Parquet para Pasillo, Oficina del Capitán y Sala de Operaciones (X = +6.0m a +19.78m, Z = -0.10m a +9.85m)
            Box("FirstFloor_MainParquet_South", floor1.transform,
                new Vector3(12.89f, y0 - 0.08f, 4.875f),
                new Vector3(13.78f, 0.16f, 9.95f),
                mats["Mat_Int_ParquetFloor"]);

            // B. Losa de Parquet para Secretaría y el Amplio Descanso Superior de las Gradas (X = +6.0m a +13.40m, Z = -2.22m a -0.12m)
            Box("FirstFloor_LandingParquet_NorthWest", floor1.transform,
                new Vector3(9.70f, y0 - 0.08f, -1.17f),
                new Vector3(7.40f, 0.16f, 2.10f),
                mats["Mat_Int_ParquetFloor"]);

            // C. Losa Completa de Azulejo Cerámico para el Ala de Baños / Servicios Higiénicos (X = +19.80m a +23.70m, Z = -2.22m a +9.85m)
            Box("FirstFloor_Bathroom_CeramicTileFloor", floor1.transform,
                new Vector3(21.75f, y0 - 0.075f, 3.815f),
                new Vector3(3.90f, 0.16f, 12.07f),
                mats["Mat_Int_BathroomTileFloor"]);

            // =========================================================================================
            // 2. DISTRIBUCIÓN ARQUITECTÓNICA DE MAMPARAS Y MUROS DEL PRIMER PISO (VANOS DE PUERTA DE 1.08m SIN HUECOS LATERALES)
            // =========================================================================================
            // A. OFICINA DEL CAPITÁN DE LA UTOP (X = +6.0m a +14.0m, Z = +3.5m a +9.8m)
            // Mampara frontal en Z = +3.5m con Puerta Interactiva en X = +9.8m (vano exacto de 9.26m a 10.34m = 1.08m)
            BuildWoodAndGlassPartitionAlongX(floor1.transform, startX: 6.0f, endX: 9.26f, z: 3.5f, y0, wallH, mats);
            BuildWoodAndGlassPartitionAlongX(floor1.transform, startX: 10.34f, endX: 14.0f, z: 3.5f, y0, wallH, mats);
            BuildInteractiveDoorwayAlongX(floor1.transform, centerX: 9.8f, z: 3.5f, y0, wallH,
                signText: "DESPACHO COMANDANTE / CAPITÁN UTOP", doorLabel: "Despacho del Capitán UTOP",
                startOpen: true, openAngle: -82f, mats: mats);

            // Mampara divisoria entre Oficina del Capitán y Sala de Operaciones (X = +14.0m, Z = +3.5m a +9.8m)
            BuildWoodAndGlassPartitionAlongZ(floor1.transform, x: 14.0f, startZ: 3.5f, endZ: 9.8f, y0, wallH, mats);

            // B. SALA DE OPERACIONES Y TÁCTICA (X = +14.0m a +19.8m, Z = +3.5m a +9.8m)
            // Mampara frontal en Z = +3.5m con Puerta Interactiva en X = +16.9m (vano exacto de 16.36m a 17.44m = 1.08m)
            BuildWoodAndGlassPartitionAlongX(floor1.transform, startX: 14.0f, endX: 16.36f, z: 3.5f, y0, wallH, mats);
            BuildWoodAndGlassPartitionAlongX(floor1.transform, startX: 17.44f, endX: 19.8f, z: 3.5f, y0, wallH, mats);
            BuildInteractiveDoorwayAlongX(floor1.transform, centerX: 16.9f, z: 3.5f, y0, wallH,
                signText: "SALA DE OPERACIONES Y TÁCTICA", doorLabel: "Sala de Operaciones",
                startOpen: true, openAngle: -80f, mats: mats);

            // C. OFICINA DE SECRETARÍA Y GUARDIA (X = +6.0m a +10.2m, Z = -2.2m a +0.6m)
            // Deja totalmente despejado X = +10.2m a +13.4m (3.2 metros de ancho!) para entrar al descanso de las gradas
            // Vano de puerta exacto de 8.71m a 9.79m (centro en 9.25m, ancho 1.08m) + tramo derecho de 9.79m a 10.20m
            BuildWoodAndGlassPartitionAlongX(floor1.transform, startX: 6.0f, endX: 8.71f, z: 0.6f, y0, wallH, mats);
            BuildWoodAndGlassPartitionAlongX(floor1.transform, startX: 9.79f, endX: 10.20f, z: 0.6f, y0, wallH, mats);
            BuildInteractiveDoorwayAlongX(floor1.transform, centerX: 9.25f, z: 0.6f, y0, wallH,
                signText: "SECRETARÍA Y PARTES", doorLabel: "Secretaría UTOP",
                startOpen: true, openAngle: 80f, mats: mats);
            BuildWoodAndGlassPartitionAlongZ(floor1.transform, x: 10.2f, startZ: -2.2f, endZ: 0.6f, y0, wallH, mats);

            // D. ALA DE BAÑOS / SERVICIOS HIGIÉNICOS CON PAREDES SÓLIDAS OPACAS (CERO CRISTAL!) EN X = +19.8m (Z = -2.2m a +9.8m)
            BuildOpaqueBathroomEnclosureAndInterior(floor1.transform, y0, wallH, mats);

            // 3. MOBILIARIO E INTERIORES DETALLADOS DE LAS OFICINAS
            BuildCaptainOfficeInterior(floor1.transform, y0, wallH, mats);
            BuildSecretaryOfficeInterior(floor1.transform, y0, mats);
            BuildOperationsBriefingRoom(floor1.transform, y0, mats);

            // Iluminación del Pasillo Central y Descanso de las Gradas
            CreateCeilingLight(floor1.transform, new Vector3(11.8f, y0 + wallH - 0.15f, 1.8f), new Color(0.98f, 0.96f, 0.90f), 2.8f, 11f);
            CreateCeilingLight(floor1.transform, new Vector3(17.5f, y0 + wallH - 0.15f, 1.8f), new Color(0.98f, 0.96f, 0.90f), 2.6f, 10f);
        }

        private static void BuildCaptainOfficeInterior(Transform parent, float y0, float wallH, Dictionary<string, Material> mats)
        {
            var office = new GameObject("Captain_Office_Detailed_Replica");
            office.transform.SetParent(parent, false);

            float centerX = 10.0f;
            float backWallZ = 9.68f;

            // A. PARED DE FONDO (Imagen 2): Panel central gris institucional + Pilares y Estanterías de Madera Caoba
            Box("Captain_BackWall_CenterPanel", office.transform,
                new Vector3(centerX, y0 + wallH * 0.5f, backWallZ),
                new Vector3(2.8f, wallH, 0.12f),
                mats["Mat_Int_OfficeWallGrey"]);

            float[] woodPillarXs = { centerX - 3.3f, centerX - 1.45f, centerX + 1.45f, centerX + 3.3f };
            foreach (float px in woodPillarXs)
            {
                Box($"Mahogany_Column_{px:F1}", office.transform,
                    new Vector3(px, y0 + wallH * 0.5f, backWallZ - 0.18f),
                    new Vector3(0.42f, wallH, 0.45f),
                    mats["Mat_Int_Mahogany"]);
            }

            // B. CUADRO ENMARCADO DEL PRESIDENTE EN EL CENTRO SUPERIOR (Imagen 2)
            Box("President_Frame_Outer", office.transform,
                new Vector3(centerX, y0 + 2.38f, backWallZ - 0.08f),
                new Vector3(1.18f, 1.42f, 0.08f),
                mats["Mat_Int_DarkWood"]);
            var portraitCanvas = Box("President_Portrait_Canvas", office.transform,
                new Vector3(centerX, y0 + 2.38f, backWallZ - 0.125f),
                new Vector3(1.02f, 1.26f, 0.02f),
                mats["Mat_Int_Portrait"]);
            portraitCanvas.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            // C. ESTANTERÍAS EMPOTRADAS DE MADERA CON LIBROS, CARPETAS Y PLACA POLICIAL (Imagen 2)
            Box("Rear_Credenza_Base", office.transform,
                new Vector3(centerX, y0 + 0.52f, backWallZ - 0.22f),
                new Vector3(6.2f, 1.04f, 0.44f),
                mats["Mat_Int_Mahogany"]);

            float[] shelfHeights = { 1.05f, 1.58f, 2.08f };
            foreach (float sh in shelfHeights)
            {
                Box($"Bookshelf_Plank_{sh:F2}", office.transform,
                    new Vector3(centerX, y0 + sh, backWallZ - 0.22f),
                    new Vector3(6.2f, 0.06f, 0.42f),
                    mats["Mat_Int_Mahogany"]);
            }

            Material[] bookMats = {
                mats["Mat_Int_DarkWood"],
                mats["Mat_CJ_UTOP_GateRust"],
                mats["Mat_CJ_Window_FrameDark"],
                mats["Mat_Int_UniformOlive"],
                mats["Mat_CJ_Classical_Ochre"]
            };
            for (int b = 0; b < 28; b++)
            {
                float bx = (b < 14) ? (centerX - 3.0f + b * 0.11f) : (centerX + 0.65f + (b - 14) * 0.11f);
                float bh = 0.34f + (b % 4) * 0.03f;
                Material bMat = bookMats[b % bookMats.Length];
                var book = Box($"Book_{b}", office.transform,
                    new Vector3(bx, y0 + 1.08f + bh * 0.5f, backWallZ - 0.24f),
                    new Vector3(0.085f, bh, 0.26f),
                    bMat);
                if (b == 13 || b == 27) book.transform.localRotation = Quaternion.Euler(0, 0, 12f);
            }

            var plaque = Box("Police_Crest_Plaque", office.transform,
                new Vector3(centerX - 1.05f, y0 + 1.82f, backWallZ - 0.24f),
                new Vector3(0.32f, 0.40f, 0.04f),
                mats["Mat_Int_DarkWood"]);
            plaque.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Box("Police_Crest_GoldShield", plaque.transform,
                new Vector3(0f, 0f, -0.6f),
                new Vector3(0.72f, 0.72f, 0.3f),
                mats["Mat_Int_ArmoryShield"]);

            Box("Folder_Stack_L", office.transform, new Vector3(centerX - 0.55f, y0 + 1.65f, backWallZ - 0.24f), new Vector3(0.42f, 0.09f, 0.30f), mats["Mat_Int_CreamWall"]);
            Box("Folder_Stack_R", office.transform, new Vector3(centerX + 0.95f, y0 + 1.66f, backWallZ - 0.24f), new Vector3(0.46f, 0.11f, 0.30f), mats["Mat_Int_CreamWall"]);

            // D. BANDERAS INSTITUCIONALES CON PLIEGUES 3D ORGÁNICOS
            Mesh drapedFlagMesh = ProceduralMeshBuilder.GetDrapedStandingFlagMesh();
            BuildStandingOfficeFlag(office.transform, new Vector3(centerX - 1.65f, y0, backWallZ - 0.65f),
                drapedFlagMesh, mats["Mat_Int_FlagBolivia"], mats);
            BuildStandingOfficeFlag(office.transform, new Vector3(centerX + 1.65f, y0, backWallZ - 0.65f),
                drapedFlagMesh, mats["Mat_Int_FlagWiphala"], mats);

            // E. ESCRITORIO EJECUTIVO DE CAOBA CON MOLDURAS Y OBJETOS DETALLADOS
            float deskZ = 7.45f;
            var desk = new GameObject("Captain_Executive_Desk");
            desk.transform.SetParent(office.transform, false);
            desk.transform.localPosition = new Vector3(centerX, y0, deskZ);

            Box("Desk_Top_Main", desk.transform, new Vector3(0f, 0.76f, 0f), new Vector3(2.38f, 0.055f, 1.10f), mats["Mat_Int_Mahogany"]);
            Box("Desk_Top_BevelRim", desk.transform, new Vector3(0f, 0.745f, 0f), new Vector3(2.42f, 0.03f, 1.14f), mats["Mat_Int_DarkWood"]);
            Box("Desk_Leather_Inlay", desk.transform, new Vector3(0f, 0.79f, -0.08f), new Vector3(1.45f, 0.008f, 0.68f), mats["Mat_Int_LeatherBlack"]);
            Box("Desk_Front_ModestyPanel", desk.transform, new Vector3(0f, 0.38f, -0.46f), new Vector3(2.22f, 0.72f, 0.06f), mats["Mat_Int_DarkWood"]);
            Box("Desk_Pedestal_L", desk.transform, new Vector3(-0.84f, 0.37f, 0f), new Vector3(0.56f, 0.72f, 0.98f), mats["Mat_Int_Mahogany"]);
            Box("Desk_Pedestal_R", desk.transform, new Vector3( 0.84f, 0.37f, 0f), new Vector3(0.56f, 0.72f, 0.98f), mats["Mat_Int_Mahogany"]);
            Box("Desk_Plinth_L", desk.transform, new Vector3(-0.84f, 0.04f, 0f), new Vector3(0.58f, 0.08f, 1.00f), mats["Mat_Int_DarkWood"]);
            Box("Desk_Plinth_R", desk.transform, new Vector3( 0.84f, 0.04f, 0f), new Vector3(0.58f, 0.08f, 1.00f), mats["Mat_Int_DarkWood"]);

            // Objetos sobre el escritorio (Imagen 2):
            Box("Open_Binder_Cover", desk.transform, new Vector3(0.05f, 0.80f, -0.12f), new Vector3(0.68f, 0.015f, 0.44f), mats["Mat_Int_LeatherBlack"]);
            Box("Doc_Page_Left", desk.transform, new Vector3(-0.11f, 0.81f, -0.12f), new Vector3(0.30f, 0.01f, 0.40f), mats["Mat_Int_CreamWall"]);
            Box("Doc_Page_Right", desk.transform, new Vector3( 0.21f, 0.81f, -0.12f), new Vector3(0.30f, 0.01f, 0.40f), mats["Mat_Int_CreamWall"]);

            Box("Black_Notebook_Agenda", desk.transform, new Vector3(-0.48f, 0.815f, -0.10f), new Vector3(0.24f, 0.035f, 0.32f), mats["Mat_Int_LeatherBlack"]);
            Box("Notebook_Pages_Edge", desk.transform, new Vector3(-0.48f, 0.815f, -0.255f), new Vector3(0.22f, 0.025f, 0.02f), mats["Mat_Int_CreamWall"]);
            Box("Reading_Glasses_Frame", desk.transform, new Vector3(-0.48f, 0.805f, -0.32f), new Vector3(0.14f, 0.02f, 0.05f), mats["Mat_Int_Gold"]);

            Box("Pen_Holder_Base", desk.transform, new Vector3(-0.88f, 0.81f, -0.22f), new Vector3(0.14f, 0.02f, 0.14f), mats["Mat_Int_LeatherBlack"]);
            Box("Pen_Holder_Cup", desk.transform, new Vector3(-0.88f, 0.87f, -0.22f), new Vector3(0.10f, 0.12f, 0.10f), mats["Mat_Int_LeatherBlack"]);
            var pen1 = Cylinder("Pen_1", desk.transform, new Vector3(-0.90f, 0.95f, -0.22f), new Vector3(0.015f, 0.08f, 0.015f), mats["Mat_Int_Gold"]);
            pen1.transform.localRotation = Quaternion.Euler(12f, 0f, 18f);
            var pen2 = Cylinder("Pen_2", desk.transform, new Vector3(-0.86f, 0.95f, -0.22f), new Vector3(0.015f, 0.08f, 0.015f), mats["Mat_CJ_UTOP_WhiteTrim"]);
            pen2.transform.localRotation = Quaternion.Euler(10f, 0f, -16f);

            var namePlate = Box("Desk_Nameplate_UTOP", desk.transform, new Vector3(0.82f, 0.85f, -0.22f), new Vector3(0.32f, 0.12f, 0.10f), mats["Mat_Int_DarkWood"]);
            namePlate.transform.localRotation = Quaternion.Euler(-18f, -12f, 0f);
            Box("Nameplate_GoldFace", namePlate.transform, new Vector3(0f, 0f, -0.52f), new Vector3(0.92f, 0.86f, 0.1f), mats["Mat_CJ_UTOP_BannerSign"]);

            // F. SILLÓN EJECUTIVO ESCULPIDO Y CAPITÁN DE LA UTOP SENTADO (Imagen 2)
            BuildSeatedCaptainHumanoid(office.transform, new Vector3(centerX, y0, 8.28f), yaw: 180f, isCaptain: true, mats);

            // Dos Sillas de Visita Ergonómicas Esculpidas frente al escritorio del Capitán
            ProceduralMeshBuilder.BuildSculptedOfficeVisitorChair(office.transform, new Vector3(centerX - 0.68f, y0, 6.20f), yaw: 8f, mats["Mat_Int_LeatherBlack"], mats["Mat_Int_Chrome"]);
            ProceduralMeshBuilder.BuildSculptedOfficeVisitorChair(office.transform, new Vector3(centerX + 0.68f, y0, 6.20f), yaw: -8f, mats["Mat_Int_LeatherBlack"], mats["Mat_Int_Chrome"]);

            // Sofá lateral de cuero para visitas en la pared derecha del despacho
            BuildExecutiveLeatherSofa(office.transform, new Vector3(13.25f, y0, 5.4f), yaw: -90f, mats);

            CreateCeilingLight(office.transform, new Vector3(centerX, y0 + wallH - 0.15f, 7.6f), new Color(1.0f, 0.96f, 0.88f), 3.2f, 10f);
        }

        private static void BuildExecutiveLeatherSofa(Transform parent, Vector3 pos, float yaw, Dictionary<string, Material> mats)
        {
            var sofa = new GameObject("Executive_Leather_Sofa_2Seater");
            sofa.transform.SetParent(parent, false);
            sofa.transform.localPosition = pos;
            sofa.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Material leather = mats["Mat_Int_LeatherBlack"];
            Material wood = mats["Mat_Int_DarkWood"];

            Box("Sofa_Base", sofa.transform, new Vector3(0f, 0.18f, 0f), new Vector3(1.55f, 0.24f, 0.76f), leather);
            Box("Sofa_Cushion_L", sofa.transform, new Vector3(-0.34f, 0.36f, 0.04f), new Vector3(0.66f, 0.14f, 0.64f), leather);
            Box("Sofa_Cushion_R", sofa.transform, new Vector3( 0.34f, 0.36f, 0.04f), new Vector3(0.66f, 0.14f, 0.64f), leather);
            var back = Box("Sofa_Backrest", sofa.transform, new Vector3(0f, 0.64f, -0.26f), new Vector3(1.38f, 0.48f, 0.20f), leather);
            back.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Box("Sofa_Arm_L", sofa.transform, new Vector3(-0.74f, 0.44f, 0f), new Vector3(0.16f, 0.42f, 0.76f), leather);
            Box("Sofa_Arm_R", sofa.transform, new Vector3( 0.74f, 0.44f, 0f), new Vector3(0.16f, 0.42f, 0.76f), leather);
            Box("Sofa_WoodTrim", sofa.transform, new Vector3(0f, 0.04f, 0f), new Vector3(1.58f, 0.06f, 0.78f), wood);
        }

        private static void BuildStandingOfficeFlag(Transform parent, Vector3 basePos, Mesh drapedMesh, Material flagMat, Dictionary<string, Material> mats)
        {
            var flagGroup = new GameObject("Institutional_Standing_Flag");
            flagGroup.transform.SetParent(parent, false);
            flagGroup.transform.localPosition = basePos;

            Cylinder("Flag_Pedestal", flagGroup.transform, new Vector3(0, 0.06f, 0), new Vector3(0.36f, 0.06f, 0.36f), mats["Mat_Int_DarkWood"]);
            Cylinder("Flag_Pole", flagGroup.transform, new Vector3(0, 1.15f, 0), new Vector3(0.035f, 1.10f, 0.035f), mats["Mat_Int_DarkWood"]);
            Cylinder("Flag_GoldFinial", flagGroup.transform, new Vector3(0, 2.30f, 0), new Vector3(0.06f, 0.08f, 0.06f), mats["Mat_Int_Gold"]);

            var cloth = ProceduralMeshBuilder.CreateMeshObject("Flag_Draped_Cloth", flagGroup.transform,
                new Vector3(0, 0.52f, 0.02f), drapedMesh, flagMat, addCollider: false);
            cloth.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        }

        private static void BuildSeatedCaptainHumanoid(Transform parent, Vector3 chairFloorPos, float yaw, bool isCaptain, Dictionary<string, Material> mats)
        {
            var group = new GameObject(isCaptain ? "Captain_UTOP_Seated_With_Chair" : "Officer_Seated_With_Chair");
            group.transform.SetParent(parent, false);
            group.transform.localPosition = chairFloorPos;
            group.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // 1. Sillón Ejecutivo Giratorio Esculpido con Ruedas y componente InteractiveChair
            var execChairGo = ProceduralMeshBuilder.BuildSculptedExecutiveChair(
                group.transform,
                Vector3.zero,
                yaw: 0f,
                upholsteryMat: mats["Mat_Int_LeatherBlack"],
                metalMat: mats["Mat_Int_Chrome"],
                darkTrimMat: mats["Mat_Int_SteelRack"]);

            var interactiveChair = execChairGo.GetComponent<InteractiveChair>();

            if (isCaptain)
            {
                if (interactiveChair != null)
                {
                    interactiveChair.chairLabel = "Sillón Ejecutivo del Capitán UTOP";
                    interactiveChair.allowPlayerSit = false;
                }

                // 2. Modelo 3D Esquelético del Capitán de la UTOP (skin capitan + movimientos capitan)
                Vector3 seatedWorldPos = group.transform.TransformPoint(new Vector3(0f, 0f, 0.08f));
                Quaternion seatedWorldRot = Quaternion.Euler(0f, yaw, 0f);
                // Posición despejada al costado del escritorio para demostraciones de pie, caída y Kip-Up
                Vector3 clearStandingWorldPos = group.transform.TransformPoint(new Vector3(-1.45f, 0f, 1.35f));

                CharacterAssetsAndClipsProcessor.SpawnRiggedCaptainInOffice(
                    parent,
                    seatedWorldPos,
                    seatedWorldRot,
                    clearStandingWorldPos,
                    interactiveChair);
                return;
            }

            if (interactiveChair != null)
            {
                interactiveChair.chairLabel = "Silla de Guardia / Secretaría";
                interactiveChair.allowPlayerSit = true;
            }
        }

        private static void BuildSecretaryOfficeInterior(Transform parent, float y0, Dictionary<string, Material> mats)
        {
            var sec = new GameObject("Secretary_And_Duty_Office");
            sec.transform.SetParent(parent, false);

            float dx = 7.9f, dz = -0.9f;
            // Escritorio administrativo con faldón, cajonera y computadora completa
            Box("Admin_Desk_Top", sec.transform, new Vector3(dx, y0 + 0.76f, dz), new Vector3(1.75f, 0.055f, 0.85f), mats["Mat_Int_Mahogany"]);
            Box("Admin_Desk_Rim", sec.transform, new Vector3(dx, y0 + 0.74f, dz), new Vector3(1.79f, 0.03f, 0.89f), mats["Mat_Int_DarkWood"]);
            Box("Admin_Desk_Modesty", sec.transform, new Vector3(dx, y0 + 0.42f, dz + 0.36f), new Vector3(1.65f, 0.62f, 0.04f), mats["Mat_Int_DarkWood"]);
            Box("Admin_Desk_PedestalL", sec.transform, new Vector3(dx - 0.62f, y0 + 0.37f, dz), new Vector3(0.45f, 0.72f, 0.78f), mats["Mat_Int_Mahogany"]);
            Box("Admin_Desk_LegR", sec.transform, new Vector3(dx + 0.78f, y0 + 0.37f, dz), new Vector3(0.08f, 0.72f, 0.78f), mats["Mat_Int_DarkWood"]);

            // Monitor de PC con base, pantalla, teclado y mouse
            Box("PC_Stand_Base", sec.transform, new Vector3(dx, y0 + 0.79f, dz - 0.15f), new Vector3(0.22f, 0.015f, 0.16f), mats["Mat_Int_SteelRack"]);
            Box("PC_Stand_Neck", sec.transform, new Vector3(dx, y0 + 0.88f, dz - 0.18f), new Vector3(0.05f, 0.18f, 0.04f), mats["Mat_Int_SteelRack"]);
            Box("PC_Monitor_Bezel", sec.transform, new Vector3(dx, y0 + 1.04f, dz - 0.15f), new Vector3(0.56f, 0.35f, 0.035f), mats["Mat_Int_LeatherBlack"]);
            Box("PC_Keyboard", sec.transform, new Vector3(dx, y0 + 0.795f, dz + 0.08f), new Vector3(0.42f, 0.018f, 0.14f), mats["Mat_Int_SteelRack"]);

            BuildSeatedCaptainHumanoid(sec.transform, new Vector3(dx, y0, dz - 0.68f), yaw: 0f, isCaptain: false, mats);

            // Silla de visita esculpida frente al escritorio de secretaría
            ProceduralMeshBuilder.BuildSculptedOfficeVisitorChair(sec.transform, new Vector3(dx, y0, dz + 0.85f), yaw: 180f, mats["Mat_Int_LeatherBlack"], mats["Mat_Int_Chrome"]);

            // Archivadores metálicos con cajones y manijas cromadas
            for (int f = 0; f < 3; f++)
            {
                float fz = -1.75f + f * 0.82f;
                Box($"Filing_Cabinet_{f}", sec.transform, new Vector3(6.42f, y0 + 0.70f, fz), new Vector3(0.52f, 1.40f, 0.72f), mats["Mat_Int_OfficeWallGrey"]);
                for (int d = 0; d < 4; d++)
                {
                    float dy = y0 + 0.22f + d * 0.32f;
                    Box($"Drawer_Front_{f}_{d}", sec.transform, new Vector3(6.69f, dy, fz), new Vector3(0.02f, 0.28f, 0.64f), mats["Mat_CJ_Sidewalk_Curb"]);
                    Box($"Drawer_Handle_{f}_{d}", sec.transform, new Vector3(6.71f, dy + 0.04f, fz), new Vector3(0.02f, 0.025f, 0.14f), mats["Mat_Int_Chrome"]);
                }
            }
        }

        private static void BuildOperationsBriefingRoom(Transform parent, float y0, Dictionary<string, Material> mats)
        {
            var ops = new GameObject("Operations_Briefing_Room");
            ops.transform.SetParent(parent, false);

            float tableX = 16.9f, tableZ = 6.7f;
            // Mesa de Conferencias Tácticas con canto biselado y pedestales cilindrados/caoba
            Box("Briefing_Table_Top", ops.transform, new Vector3(tableX, y0 + 0.76f, tableZ), new Vector3(1.95f, 0.055f, 3.50f), mats["Mat_Int_Mahogany"]);
            Box("Briefing_Table_Bevel", ops.transform, new Vector3(tableX, y0 + 0.74f, tableZ), new Vector3(2.00f, 0.03f, 3.55f), mats["Mat_Int_DarkWood"]);
            Box("Briefing_Table_CenterInlay", ops.transform, new Vector3(tableX, y0 + 0.79f, tableZ), new Vector3(0.65f, 0.01f, 2.40f), mats["Mat_Int_LeatherBlack"]);
            Box("Briefing_Table_Pedestal1", ops.transform, new Vector3(tableX, y0 + 0.37f, tableZ - 1.05f), new Vector3(0.95f, 0.72f, 0.32f), mats["Mat_Int_DarkWood"]);
            Box("Briefing_Table_Pedestal2", ops.transform, new Vector3(tableX, y0 + 0.37f, tableZ + 1.05f), new Vector3(0.95f, 0.72f, 0.32f), mats["Mat_Int_DarkWood"]);

            // 6 Sillas Ergonómicas de Oficina Esculpidas alrededor de la Mesa de Operaciones
            for (int r = 0; r < 3; r++)
            {
                float cz = tableZ - 1.05f + r * 1.05f;
                // 3 sillas lado izquierdo mirando hacia +X
                ProceduralMeshBuilder.BuildSculptedOfficeVisitorChair(ops.transform, new Vector3(tableX - 1.32f, y0, cz), yaw: 90f, mats["Mat_Int_LeatherBlack"], mats["Mat_Int_Chrome"]);
                // 3 sillas lado derecho mirando hacia -X
                ProceduralMeshBuilder.BuildSculptedOfficeVisitorChair(ops.transform, new Vector3(tableX + 1.32f, y0, cz), yaw: -90f, mats["Mat_Int_LeatherBlack"], mats["Mat_Int_Chrome"]);

                // Carpetas tácticas sobre la mesa frente a cada puesto
                Box($"Briefing_Folder_L_{r}", ops.transform, new Vector3(tableX - 0.62f, y0 + 0.795f, cz), new Vector3(0.28f, 0.012f, 0.36f), mats["Mat_Int_CreamWall"]);
                Box($"Briefing_Folder_R_{r}", ops.transform, new Vector3(tableX + 0.62f, y0 + 0.795f, cz), new Vector3(0.28f, 0.012f, 0.36f), mats["Mat_Int_CreamWall"]);
            }

            // Pizarra táctica en el muro de fondo (Z = +9.65m)
            Box("Tactical_Whiteboard_Frame", ops.transform, new Vector3(tableX, y0 + 2.0f, 9.66f), new Vector3(3.2f, 1.4f, 0.06f), mats["Mat_Int_DarkWood"]);
            Box("Tactical_Whiteboard_Surface", ops.transform, new Vector3(tableX, y0 + 2.0f, 9.62f), new Vector3(3.05f, 1.25f, 0.02f), mats["Mat_CJ_UTOP_WhiteTrim"]);

            CreateCeilingLight(ops.transform, new Vector3(tableX, y0 + 3.55f, tableZ), new Color(0.96f, 0.98f, 1.0f), 2.8f, 10f);
        }

        /// <summary>
        /// Construye el Ala de Baños / Servicios Higiénicos con PAREDES SÓLIDAS OPACAS (Cero cristal),
        /// revestimiento de azulejo cerámico, puerta principal interactiva, 3 lavamanos reales de porcelana con espejo y grifería cromada,
        /// y 3 cubículos privados con puertas que se abren/cierran e inodoros esculpidos de porcelana.
        /// </summary>
        private static void BuildOpaqueBathroomEnclosureAndInterior(Transform parent, float y0, float wallH, Dictionary<string, Material> mats)
        {
            var bath = new GameObject("FirstFloor_Restrooms_Banos_OpaqueTiled");
            bath.transform.SetParent(parent, false);

            // 1. PAREDES PERIMETRALES SÓLIDAS OPACAS EN X = +19.80m (Z = -2.25m a +9.80m) — CERO CRISTAL!
            // Tramo Norte (junto al hueco de las gradas: Z = -2.25m a +1.26m)
            BuildSolidOpaqueBathroomWallAlongZ(bath.transform, x: 19.80f, startZ: -2.25f, endZ: 1.26f, y0, wallH, mats);
            // Tramo Sur (Z = +2.34m a +9.80m)
            BuildSolidOpaqueBathroomWallAlongZ(bath.transform, x: 19.80f, startZ: 2.34f, endZ: 9.80f, y0, wallH, mats);
            // Puerta Principal Interactiva del Baño en X = +19.80m, Z = +1.80m (vano exacto de 1.26m a 2.34m = 1.08m)
            BuildInteractiveDoorwayAlongZ(bath.transform, x: 19.80f, centerZ: 1.80f, y0, wallH,
                signText: "BAÑOS / SERVICIOS HIGIÉNICOS", doorLabel: "Puerta Principal de Baños",
                startOpen: false, openAngle: 84f, mats: mats, isOpaqueBathroomWall: true);

            // Revestimiento interior de azulejo cerámico en los muros Norte, Este y Sur del baño
            Box("Bath_EastWall_CeramicTile", bath.transform, new Vector3(23.62f, y0 + wallH * 0.5f, 3.80f), new Vector3(0.06f, wallH, 12.0f), mats["Mat_Int_BathroomWallTile"]);
            Box("Bath_SouthWall_CeramicTile", bath.transform, new Vector3(21.75f, y0 + wallH * 0.5f, 9.72f), new Vector3(3.80f, wallH, 0.06f), mats["Mat_Int_BathroomWallTile"]);
            Box("Bath_NorthWall_CeramicTile", bath.transform, new Vector3(21.75f, y0 + wallH * 0.5f, -2.20f), new Vector3(3.80f, wallH, 0.06f), mats["Mat_Int_BathroomWallTile"]);

            // 2. GRAN MESÓN DE GRANITO CON 3 LAVAMANOS ESCULPIDOS DE PORCELANA, GRIFERÍA CROMADA Y ESPEJO MURAL (X = +20.32m, Z = +4.2m a +8.2m)
            var vanityGroup = new GameObject("Bathroom_Washbasin_Vanity_And_Mirror");
            vanityGroup.transform.SetParent(bath.transform, false);

            float counterX = 20.30f;
            float counterCenterZ = 6.20f;
            float counterLenZ = 3.80f;

            // Mueble inferior de madera hidrófuga con puertas y zócalo
            Box("Vanity_Cabinet_Base", vanityGroup.transform, new Vector3(counterX - 0.04f, y0 + 0.40f, counterCenterZ), new Vector3(0.54f, 0.78f, counterLenZ - 0.08f), mats["Mat_Int_DarkWood"]);
            // Encimera de granito oscuro con faldón frontal y zócalo contra salpicaduras (backsplash)
            Box("Vanity_Granite_Top", vanityGroup.transform, new Vector3(counterX, y0 + 0.81f, counterCenterZ), new Vector3(0.62f, 0.06f, counterLenZ), mats["Mat_Int_GraniteCounter"]);
            Box("Vanity_Granite_Backsplash", vanityGroup.transform, new Vector3(counterX - 0.28f, y0 + 0.90f, counterCenterZ), new Vector3(0.04f, 0.14f, counterLenZ), mats["Mat_Int_GraniteCounter"]);

            // Gran Espejo Mural Reflectante con marco metálico cromado e iluminación LED superior
            Box("Mirror_Chrome_Frame", vanityGroup.transform, new Vector3(19.92f, y0 + 1.56f, counterCenterZ), new Vector3(0.03f, 1.12f, counterLenZ - 0.20f), mats["Mat_Int_Chrome"]);
            Box("Mirror_Reflective_Surface", vanityGroup.transform, new Vector3(19.94f, y0 + 1.56f, counterCenterZ), new Vector3(0.02f, 1.04f, counterLenZ - 0.28f), mats["Mat_Int_MirrorGlass"]);
            Box("Mirror_LED_LightBar", vanityGroup.transform, new Vector3(19.98f, y0 + 2.16f, counterCenterZ), new Vector3(0.08f, 0.05f, counterLenZ - 0.40f), mats["Mat_CJ_UTOP_WhiteTrim"]);

            // 3 Lavamanos Reales de Porcelana Esculpida con poza cóncava, grifo cuello de cisne cromado y dispensador de jabón
            for (int s = 0; s < 3; s++)
            {
                float sz = 4.95f + s * 1.25f;
                var sink = ProceduralMeshBuilder.BuildSculptedWashbasinUnit(
                    vanityGroup.transform,
                    new Vector3(counterX + 0.03f, y0 + 0.83f, sz),
                    porcelainMat: mats["Mat_Int_Porcelain"],
                    chromeMat: mats["Mat_Int_Chrome"],
                    drainDarkMat: mats["Mat_Int_SteelRack"]);
                sink.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                // Dispensador de jabón líquido cromado entre cada lavamanos
                Cylinder($"Soap_Dispenser_{s}", vanityGroup.transform, new Vector3(19.98f, y0 + 1.12f, sz + 0.48f), new Vector3(0.07f, 0.09f, 0.07f), mats["Mat_Int_Chrome"]);
            }

            // Secador de manos eléctrico y basurero cromado junto a la entrada del baño
            Box("Electric_Hand_Dryer", vanityGroup.transform, new Vector3(19.98f, y0 + 1.18f, 3.45f), new Vector3(0.16f, 0.26f, 0.24f), mats["Mat_Int_Porcelain"]);
            Cylinder("Chrome_Trash_Bin", vanityGroup.transform, new Vector3(20.22f, y0 + 0.28f, 3.45f), new Vector3(0.28f, 0.28f, 0.28f), mats["Mat_Int_Chrome"]);

            // 3. TRES CUBÍCULOS SANITARIOS PRIVADOS CON PUERTAS INTERACTIVAS QUE SE ABREN Y CIERRAN + INODOROS DE PORCELANA ESCULPIDOS
            // Ubicados a lo largo de la pared Este (X = +21.95m a +23.58m, Z = +3.60m a +9.60m)
            int stallCount = 3;
            float stallStartZ = 3.60f;
            float stallWidthZ = 1.95f;
            float stallFrontX = 21.95f;
            float stallHeight = 2.15f;

            for (int i = 0; i <= stallCount; i++)
            {
                float divZ = stallStartZ + i * stallWidthZ;
                // Tabique divisorio opaco entre cubículos
                Box($"Stall_SidePartition_{i}", bath.transform,
                    new Vector3(22.78f, y0 + stallHeight * 0.5f, divZ),
                    new Vector3(1.68f, stallHeight, 0.07f),
                    mats["Mat_Int_CreamWall"]);
            }

            // Riel superior estructural de aluminio sobre el frente de los cubículos
            Box("Stalls_Top_Headrail", bath.transform,
                new Vector3(stallFrontX, y0 + stallHeight + 0.03f, stallStartZ + stallCount * stallWidthZ * 0.5f),
                new Vector3(0.08f, 0.06f, stallCount * stallWidthZ + 0.1f),
                mats["Mat_Int_Chrome"]);

            for (int s = 0; s < stallCount; s++)
            {
                float szMin = stallStartZ + s * stallWidthZ;
                float szMax = szMin + stallWidthZ;
                float szCenter = (szMin + szMax) * 0.5f;

                // Pilastras frontales opacas a ambos lados de la puerta del cubículo (dejan un vano de puerta de 0.86m)
                float doorW = 0.86f;
                float pilasterW = (stallWidthZ - doorW) * 0.5f;
                Box($"Stall_FrontPilaster_L_{s}", bath.transform,
                    new Vector3(stallFrontX, y0 + stallHeight * 0.5f, szMin + pilasterW * 0.5f),
                    new Vector3(0.07f, stallHeight, pilasterW),
                    mats["Mat_Int_CreamWall"]);
                Box($"Stall_FrontPilaster_R_{s}", bath.transform,
                    new Vector3(stallFrontX, y0 + stallHeight * 0.5f, szMax - pilasterW * 0.5f),
                    new Vector3(0.07f, stallHeight, pilasterW),
                    mats["Mat_Int_CreamWall"]);

                // PUERTA REAL INTERACTIVA DEL CUBÍCULO SANITARIO (Con bisagra, manija cromada y componente InteractiveDoor)
                var hingeGo = new GameObject($"Stall_Door_Hinge_{s + 1}");
                hingeGo.transform.SetParent(bath.transform, false);
                hingeGo.transform.localPosition = new Vector3(stallFrontX, y0 + 0.12f, szCenter - doorW * 0.5f);

                float doorH = 1.92f;
                var leaf = Box($"Stall_Door_Leaf_{s + 1}", hingeGo.transform,
                    new Vector3(0f, doorH * 0.5f, doorW * 0.5f),
                    new Vector3(0.045f, doorH, doorW - 0.02f),
                    mats["Mat_Int_Mahogany"]);

                // Manija y cerrojo cromado en ambos lados de la puerta del cubículo
                Box("Handle_Out", leaf.transform, new Vector3(-0.65f, 0f, 0.36f), new Vector3(0.6f, 0.06f, 0.04f), mats["Mat_Int_Chrome"]);
                Box("Handle_In",  leaf.transform, new Vector3( 0.65f, 0f, 0.36f), new Vector3(0.6f, 0.06f, 0.04f), mats["Mat_Int_Chrome"]);

                var interactive = hingeGo.AddComponent<InteractiveDoor>();
                interactive.doorLabel = $"Cubículo Sanitario N° {s + 1}";
                interactive.closedLocalYaw = 0f;
                interactive.openLocalYaw = 82f;
                interactive.isOpen = (s <= 1); // Los primeros dos cubículos inician abiertos hacia adentro para mostrar los inodoros reales de porcelana
                hingeGo.transform.localRotation = Quaternion.Euler(0f, interactive.isOpen ? interactive.openLocalYaw : 0f, 0f);

                // INODORO DE PORCELANA ESCULPIDO REALISTA DENTRO DEL CUBÍCULO (Mirando hacia la puerta en -X)
                ProceduralMeshBuilder.BuildSculptedPorcelainToilet(
                    bath.transform,
                    new Vector3(23.12f, y0, szCenter),
                    yaw: -90f,
                    porcelainMat: mats["Mat_Int_Porcelain"],
                    chromeMat: mats["Mat_Int_Chrome"],
                    waterMat: mats["Mat_Int_WaterBlue"]);

                // Portarrollos cromado con rollo de papel higiénico en el tabique lateral
                Box($"Paper_Holder_Bracket_{s}", bath.transform, new Vector3(22.90f, y0 + 0.68f, szMin + 0.06f), new Vector3(0.14f, 0.04f, 0.04f), mats["Mat_Int_Chrome"]);
                var roll = Cylinder($"Toilet_Paper_Roll_{s}", bath.transform, new Vector3(22.90f, y0 + 0.64f, szMin + 0.10f), new Vector3(0.11f, 0.06f, 0.11f), mats["Mat_Int_Porcelain"]);
                roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // Iluminación blanca nítida para el baño
            CreateCeilingLight(bath.transform, new Vector3(21.4f, y0 + wallH - 0.15f, 2.0f), new Color(0.96f, 0.98f, 1.0f), 2.5f, 9f);
            CreateCeilingLight(bath.transform, new Vector3(21.4f, y0 + wallH - 0.15f, 6.8f), new Color(0.96f, 0.98f, 1.0f), 3.0f, 10f);
        }

        private static void BuildSolidOpaqueBathroomWallAlongZ(Transform parent, float x, float startZ, float endZ, float y0, float totalH, Dictionary<string, Material> mats)
        {
            float len = Mathf.Abs(endZ - startZ);
            float midZ = (startZ + endZ) * 0.5f;
            var wall = new GameObject("Opaque_Bathroom_Wall_Z");
            wall.transform.SetParent(parent, false);

            // Cara hacia el pasillo: zócalo de caoba y muro crema opaco arriba
            Box("Hallway_Lower_Wainscot", wall.transform, new Vector3(x - 0.05f, y0 + 0.60f, midZ), new Vector3(0.08f, 1.20f, len), mats["Mat_Int_Mahogany"]);
            Box("Hallway_Upper_OpaqueWall", wall.transform, new Vector3(x - 0.05f, y0 + 1.20f + (totalH - 1.20f) * 0.5f, midZ), new Vector3(0.08f, totalH - 1.20f, len), mats["Mat_Int_CreamWall"]);

            // Cara interior hacia el baño: pared 100% revestida en azulejo cerámico blanco/gris
            Box("Bathroom_Inner_TiledWall", wall.transform, new Vector3(x + 0.04f, y0 + totalH * 0.5f, midZ), new Vector3(0.08f, totalH, len), mats["Mat_Int_BathroomWallTile"]);
        }

        private static void BuildWoodAndGlassPartitionAlongX(Transform parent, float startX, float endX, float z, float y0, float totalH, Dictionary<string, Material> mats)
        {
            float len = Mathf.Abs(endX - startX);
            float midX = (startX + endX) * 0.5f;
            var part = new GameObject("WoodGlass_Partition_X");
            part.transform.SetParent(parent, false);

            Box("Lower_Wood_Wainscot", part.transform, new Vector3(midX, y0 + 0.60f, z), new Vector3(len, 1.20f, 0.14f), mats["Mat_Int_Mahogany"]);
            Box("Mid_Wood_Rail", part.transform, new Vector3(midX, y0 + 1.22f, z), new Vector3(len + 0.04f, 0.07f, 0.18f), mats["Mat_Int_DarkWood"]);
            Box("Upper_Glass_Pane", part.transform, new Vector3(midX, y0 + 2.02f, z), new Vector3(len, 1.55f, 0.04f), mats["Mat_Int_ClearGlass"]);
            Box("Top_Wood_Header", part.transform, new Vector3(midX, y0 + 3.25f, z), new Vector3(len, 0.90f, 0.14f), mats["Mat_Int_Mahogany"]);

            int mullions = Mathf.Max(2, Mathf.RoundToInt(len / 1.1f));
            for (int m = 0; m <= mullions; m++)
            {
                float mx = Mathf.Lerp(startX, endX, m / (float)mullions);
                Box($"Wood_Mullion_{m}", part.transform, new Vector3(mx, y0 + totalH * 0.5f, z), new Vector3(0.10f, totalH, 0.16f), mats["Mat_Int_DarkWood"]);
            }
        }

        private static void BuildWoodAndGlassPartitionAlongZ(Transform parent, float x, float startZ, float endZ, float y0, float totalH, Dictionary<string, Material> mats)
        {
            float len = Mathf.Abs(endZ - startZ);
            float midZ = (startZ + endZ) * 0.5f;
            var part = new GameObject("WoodGlass_Partition_Z");
            part.transform.SetParent(parent, false);

            Box("Lower_Wood_Wainscot", part.transform, new Vector3(x, y0 + 0.60f, midZ), new Vector3(0.14f, 1.20f, len), mats["Mat_Int_Mahogany"]);
            Box("Mid_Wood_Rail", part.transform, new Vector3(x, y0 + 1.22f, midZ), new Vector3(0.18f, 0.07f, len + 0.04f), mats["Mat_Int_DarkWood"]);
            Box("Upper_Glass_Pane", part.transform, new Vector3(x, y0 + 2.02f, midZ), new Vector3(0.04f, 1.55f, len), mats["Mat_Int_ClearGlass"]);
            Box("Top_Wood_Header", part.transform, new Vector3(x, y0 + 3.25f, midZ), new Vector3(0.14f, 0.90f, len), mats["Mat_Int_Mahogany"]);

            int mullions = Mathf.Max(2, Mathf.RoundToInt(len / 1.1f));
            for (int m = 0; m <= mullions; m++)
            {
                float mz = Mathf.Lerp(startZ, endZ, m / (float)mullions);
                Box($"Wood_Mullion_{m}", part.transform, new Vector3(x, y0 + totalH * 0.5f, mz), new Vector3(0.16f, totalH, 0.10f), mats["Mat_Int_DarkWood"]);
            }
        }

        private static void BuildInteractiveDoorwayAlongX(Transform parent, float centerX, float z, float y0, float totalH,
            string signText, string doorLabel, bool startOpen, float openAngle, Dictionary<string, Material> mats)
        {
            var doorFrame = new GameObject($"Doorway_{doorLabel}");
            doorFrame.transform.SetParent(parent, false);
            float doorW = 1.08f; // Vano exacto de 1.08m acorde a la hoja de puerta de 1.00m (Cero huecos laterales)
            float doorH = 2.26f;

            // Jambas laterales de madera oscura, travesaño de dintel y sobrepuerta superior
            Box("Door_Jamb_L", doorFrame.transform, new Vector3(centerX - doorW * 0.5f, y0 + totalH * 0.5f, z), new Vector3(0.11f, totalH, 0.18f), mats["Mat_Int_DarkWood"]);
            Box("Door_Jamb_R", doorFrame.transform, new Vector3(centerX + doorW * 0.5f, y0 + totalH * 0.5f, z), new Vector3(0.11f, totalH, 0.18f), mats["Mat_Int_DarkWood"]);
            Box("Door_Lintel_Bar", doorFrame.transform, new Vector3(centerX, y0 + doorH + 0.04f, z), new Vector3(doorW + 0.08f, 0.09f, 0.19f), mats["Mat_Int_DarkWood"]);
            Box("Door_Header", doorFrame.transform, new Vector3(centerX, y0 + doorH + (totalH - doorH) * 0.5f, z), new Vector3(doorW, totalH - doorH, 0.14f), mats["Mat_Int_Mahogany"]);

            // Pivote de bisagra real en el borde izquierdo interior del marco
            var hingeGo = new GameObject($"Interactive_Door_Hinge_{doorLabel}");
            hingeGo.transform.SetParent(doorFrame.transform, false);
            hingeGo.transform.localPosition = new Vector3(centerX - 0.50f, y0, z);

            float leafW = 1.00f;
            var leaf = Box("Door_Leaf_Panel", hingeGo.transform, new Vector3(leafW * 0.5f, doorH * 0.5f, 0f), new Vector3(leafW, doorH - 0.02f, 0.055f), mats["Mat_Int_Mahogany"]);

            // Molduras / tableros en relieve en ambas caras de la hoja de puerta
            Box("Panel_Upper", hingeGo.transform, new Vector3(leafW * 0.5f, doorH * 0.68f, 0f), new Vector3(leafW * 0.72f, doorH * 0.36f, 0.070f), mats["Mat_Int_DarkWood"]);
            Box("Panel_Lower", hingeGo.transform, new Vector3(leafW * 0.5f, doorH * 0.24f, 0f), new Vector3(leafW * 0.72f, doorH * 0.32f, 0.070f), mats["Mat_Int_DarkWood"]);

            // Manijas de palanca doradas proporcionadas en ambas caras
            Box("Handle_Plate_Front", hingeGo.transform, new Vector3(leafW - 0.11f, 1.02f, -0.035f), new Vector3(0.04f, 0.16f, 0.015f), mats["Mat_Int_Gold"]);
            Box("Handle_Lever_Front", hingeGo.transform, new Vector3(leafW - 0.15f, 1.02f, -0.055f), new Vector3(0.12f, 0.024f, 0.035f), mats["Mat_Int_Gold"]);
            Box("Handle_Plate_Back",  hingeGo.transform, new Vector3(leafW - 0.11f, 1.02f,  0.035f), new Vector3(0.04f, 0.16f, 0.015f), mats["Mat_Int_Gold"]);
            Box("Handle_Lever_Back",  hingeGo.transform, new Vector3(leafW - 0.15f, 1.02f,  0.055f), new Vector3(0.12f, 0.024f, 0.035f), mats["Mat_Int_Gold"]);

            var interactive = hingeGo.AddComponent<InteractiveDoor>();
            interactive.doorLabel = doorLabel;
            interactive.closedLocalYaw = 0f;
            interactive.openLocalYaw = openAngle;
            interactive.isOpen = startOpen;
            hingeGo.transform.localRotation = Quaternion.Euler(0f, startOpen ? openAngle : 0f, 0f);

            CreateWallSign(doorFrame.transform, new Vector3(centerX, y0 + doorH + 0.34f, z - 0.10f), Quaternion.identity, signText, new Vector2(1.55f, 0.32f), mats);
        }

        private static void BuildInteractiveDoorwayAlongZ(Transform parent, float x, float centerZ, float y0, float totalH,
            string signText, string doorLabel, bool startOpen, float openAngle, Dictionary<string, Material> mats, bool isOpaqueBathroomWall = false)
        {
            var doorFrame = new GameObject($"Doorway_{doorLabel}");
            doorFrame.transform.SetParent(parent, false);
            float doorW = 1.08f; // Vano exacto de 1.08m sin huecos laterales
            float doorH = 2.26f;

            Box("Door_Jamb_N", doorFrame.transform, new Vector3(x, y0 + totalH * 0.5f, centerZ - doorW * 0.5f), new Vector3(0.18f, totalH, 0.11f), mats["Mat_Int_DarkWood"]);
            Box("Door_Jamb_S", doorFrame.transform, new Vector3(x, y0 + totalH * 0.5f, centerZ + doorW * 0.5f), new Vector3(0.18f, totalH, 0.11f), mats["Mat_Int_DarkWood"]);
            Box("Door_Lintel_Bar", doorFrame.transform, new Vector3(x, y0 + doorH + 0.04f, centerZ), new Vector3(0.19f, 0.09f, doorW + 0.08f), mats["Mat_Int_DarkWood"]);
            Box("Door_Header", doorFrame.transform, new Vector3(x, y0 + doorH + (totalH - doorH) * 0.5f, centerZ), new Vector3(0.16f, totalH - doorH, doorW), isOpaqueBathroomWall ? mats["Mat_Int_CreamWall"] : mats["Mat_Int_Mahogany"]);

            var hingeGo = new GameObject($"Interactive_Door_Hinge_{doorLabel}");
            hingeGo.transform.SetParent(doorFrame.transform, false);
            hingeGo.transform.localPosition = new Vector3(x, y0, centerZ - 0.50f);

            float leafW = 1.00f;
            var leaf = Box("Door_Leaf_Panel", hingeGo.transform, new Vector3(0f, doorH * 0.5f, leafW * 0.5f), new Vector3(0.055f, doorH - 0.02f, leafW), mats["Mat_Int_Mahogany"]);
            Box("Panel_Upper", hingeGo.transform, new Vector3(0f, doorH * 0.68f, leafW * 0.5f), new Vector3(0.070f, doorH * 0.36f, leafW * 0.72f), mats["Mat_Int_DarkWood"]);
            Box("Panel_Lower", hingeGo.transform, new Vector3(0f, doorH * 0.24f, leafW * 0.5f), new Vector3(0.070f, doorH * 0.32f, leafW * 0.72f), mats["Mat_Int_DarkWood"]);

            Box("Handle_Plate_Out", hingeGo.transform, new Vector3(-0.035f, 1.02f, leafW - 0.11f), new Vector3(0.015f, 0.16f, 0.04f), mats["Mat_Int_Chrome"]);
            Box("Handle_Lever_Out", hingeGo.transform, new Vector3(-0.055f, 1.02f, leafW - 0.15f), new Vector3(0.035f, 0.024f, 0.12f), mats["Mat_Int_Chrome"]);
            Box("Handle_Plate_In",  hingeGo.transform, new Vector3( 0.035f, 1.02f, leafW - 0.11f), new Vector3(0.015f, 0.16f, 0.04f), mats["Mat_Int_Chrome"]);
            Box("Handle_Lever_In",  hingeGo.transform, new Vector3( 0.055f, 1.02f, leafW - 0.15f), new Vector3(0.035f, 0.024f, 0.12f), mats["Mat_Int_Chrome"]);

            var interactive = hingeGo.AddComponent<InteractiveDoor>();
            interactive.doorLabel = doorLabel;
            interactive.closedLocalYaw = 0f;
            interactive.openLocalYaw = openAngle;
            interactive.isOpen = startOpen;
            hingeGo.transform.localRotation = Quaternion.Euler(0f, startOpen ? openAngle : 0f, 0f);

            CreateWallSign(doorFrame.transform, new Vector3(x - 0.10f, y0 + doorH + 0.34f, centerZ), Quaternion.Euler(0, 90, 0), signText, new Vector2(1.55f, 0.32f), mats);
        }
        #endregion

        #region 6. Patio de Honor sin Z-Fighting, Tinglado con Cerchas Triangulares Rojas (4ta Imagen) y Pabellón de Cuadras
        private static void BuildPatioDeHonorAndTroopBarracks(Transform parent, Dictionary<string, Material> mats)
        {
            var patio = new GameObject("06_Patio_De_Honor_And_Troop_Barracks");
            patio.transform.SetParent(parent, false);

            float y0 = GarageFloorY; // -0.30m

            // 1. PISO DE HORMIGÓN NIVELADO DEL PATIO DE HONOR (X = +6.0m a +35.0m, Z = -34.50m a -2.38m)
            Box("Patio_Concrete_Floor", patio.transform,
                new Vector3(20.5f, y0 - 0.12f, -18.44f),
                new Vector3(29.0f, 0.24f, 32.12f),
                mats["Mat_CJ_Sidewalk_Stone"]);

            // Rejillas de drenaje perimetral elevadas +0.02m sobre el piso del patio
            Box("Patio_Drain_Grate_Right", patio.transform, new Vector3(6.55f, y0 + 0.015f, -15.0f), new Vector3(0.32f, 0.02f, 24.0f), mats["Mat_CJ_Iron_Grille"]);
            Box("Patio_Drain_Grate_Left",  patio.transform, new Vector3(19.35f, y0 + 0.015f, -13.5f), new Vector3(0.32f, 0.02f, 21.0f), mats["Mat_CJ_Iron_Grille"]);

            // 2. GRAN ESCUDO CIRCULAR DE LA UTOP / POLICÍA BOLIVIANA EN EL PISO DEL PATIO (Elevado +0.025m para cero Z-fighting)
            Box("Patio_Floor_Emblem_Decal", patio.transform,
                new Vector3(13.0f, y0 + 0.02f, -10.5f),
                new Vector3(4.8f, 0.02f, 4.8f),
                mats["Mat_Int_PatioEmblem"]);

            // 3. ESTRUCTURA METÁLICA DEL TINGLADO Y MUROS LATERALES (Fiel a la 4ta Imagen de la Calle Junín y al interior UTOP):
            // - En el lado interno izquierdo (X = +19.8m): muro verde de cierre del pabellón lateral.
            // - En el lado derecho hacia la Calle Junín (X = +6.0m): se usa directamente el muro verde escalonado de la calle
            //   (alto en Z = -9.0m a -2.4m donde lleva las letras doradas "- UTOP -", y bajo en Z = -28m a -9.0m deixando ver
            //   las columnas reticuladas rojas y las cerchas triangulares desde la calle tal como en la 4ta Imagen!).
            Box("Tinglado_Left_GreenWall", patio.transform,
                new Vector3(19.8f, y0 + 3.7f, -9.25f),
                new Vector3(0.42f, 7.4f, 13.5f),
                mats["Mat_CJ_UTOP_GreenWall"]);

            // Letras institucionales doradas "- UTOP -" sobre la cara interior del tramo alto del muro verde (Z = -5.8m)
            CreatePatioGoldWallLettering(patio.transform, new Vector3(6.42f, y0 + 3.25f, -5.8f), mats);

            // 6 Pórticos Metálicos con Columnas Reticuladas y Cerchas Triangulares Rojas (Z = -3.8m a -23.8m)
            int totalPorticos = 6;
            float porticoStartZ = -3.8f;
            float porticoSpacingZ = 4.0f;
            for (int p = 0; p < totalPorticos; p++)
            {
                float pz = porticoStartZ - p * porticoSpacingZ;
                BuildPatioSteelPortico(patio.transform, pz, y0, covered: true, mats);
            }

            // Viga reticulada longitudinal roja a lo largo del alero de la Calle Junín (X = +6.35m, Z = -24.2m a -2.5m) — visible en la 4ta Imagen
            Material rust = mats["Mat_CJ_UTOP_GateRust"];
            float eaveLenZ = 22.0f;
            float eaveMidZ = -13.5f;
            Box("Tinglado_StreetEave_TopChord", patio.transform, new Vector3(6.35f, y0 + 7.55f, eaveMidZ), new Vector3(0.12f, 0.12f, eaveLenZ), rust);
            Box("Tinglado_StreetEave_BotChord", patio.transform, new Vector3(6.35f, y0 + 6.95f, eaveMidZ), new Vector3(0.12f, 0.12f, eaveLenZ), rust);
            int eaveDiags = 28;
            for (int ed = 0; ed < eaveDiags; ed++)
            {
                float dz = -24.2f + (ed + 0.5f) * (eaveLenZ / eaveDiags);
                var diag = Box($"Tinglado_StreetEave_Diag_{ed}", patio.transform, new Vector3(6.35f, y0 + 7.25f, dz), new Vector3(0.06f, 0.06f, 0.92f), rust);
                diag.transform.localRotation = Quaternion.Euler((ed % 2 == 0) ? 38f : -38f, 0f, 0f);
            }

            // Correas metálicas longitudinales (Purlins) que soportan las planchas del techo inclinado a dos aguas
            float[] purlinOffsets = { -6.4f, -4.3f, -2.2f, 0f, 2.2f, 4.3f, 6.4f };
            foreach (float pxOff in purlinOffsets)
            {
                float px = 13.0f + pxOff;
                float tSlope = 1f - (Mathf.Abs(pxOff) / 7.0f);
                float py = y0 + Mathf.Lerp(7.55f, 9.05f, tSlope);
                Box($"Tinglado_Longitudinal_Purlin_{pxOff:F1}", patio.transform,
                    new Vector3(px, py, eaveMidZ),
                    new Vector3(0.08f, 0.10f, eaveLenZ + 0.6f),
                    rust);
            }

            // Cubierta a dos aguas de calamina oscura con franjas translúcidas amarillas integradas en la pendiente (1ra y 4ta Imagen)
            int roofStrips = 10;
            float stripLenZ = eaveLenZ / roofStrips; // 2.2m por franja
            float halfSpanWidth = 7.35f; // Cubre desde X = 5.8m hasta X = 20.2m con alero sobre la calle
            float roofSlopeDeg = 12.2f;  // Pendiente de las dos aguas coincidiendo con el cordón superior de las cerchas

            for (int s = 0; s < roofStrips; s++)
            {
                float sz = -2.6f - (s + 0.5f) * stripLenZ;
                bool isSkylight = (s == 1 || s == 4 || s == 7);
                Material roofMat = isSkylight ? mats["Mat_CJ_Tinglado_Skylight"] : mats["Mat_CJ_Tinglado_RoofDark"];

                // Faldón derecho (hacia la Calle Junín, X = 5.85m a 13.0m, sube hacia el centro en +X)
                var rightSlope = Box($"Tinglado_Roof_StreetSlope_{s}", patio.transform,
                    new Vector3(9.45f, y0 + 8.35f, sz),
                    new Vector3(halfSpanWidth, 0.05f, stripLenZ + 0.04f),
                    roofMat);
                rightSlope.transform.localRotation = Quaternion.Euler(0f, 0f, roofSlopeDeg);

                // Faldón izquierdo (hacia el interior del patio, X = 13.0m a 20.15m, baja hacia +X)
                var leftSlope = Box($"Tinglado_Roof_InnerSlope_{s}", patio.transform,
                    new Vector3(16.55f, y0 + 8.35f, sz),
                    new Vector3(halfSpanWidth, 0.05f, stripLenZ + 0.04f),
                    roofMat);
                leftSlope.transform.localRotation = Quaternion.Euler(0f, 0f, -roofSlopeDeg);
            }

            // Cumbrera central metálica (Ridge cap) en X = 13.0m
            Box("Tinglado_Roof_RidgeCap", patio.transform,
                new Vector3(13.0f, y0 + 9.15f, eaveMidZ),
                new Vector3(0.45f, 0.08f, eaveLenZ + 0.6f),
                mats["Mat_CJ_Tinglado_RoofDark"]);

            // 4. MÁSTIL CENTRAL CON LA BANDERA DE BOLIVIA FLAMEANDO (En el sector abierto del patio al fondo del tinglado)
            BuildPatioFlagpoleWithBoliviaFlag(patio.transform, new Vector3(13.0f, y0, -26.2f), mats);

            // 5. PABELLÓN DE DORMITORIOS / CUADRAS DE TROPA
            BuildTwoStoryGreenBarracksFacade(patio.transform,
                centerPos: new Vector3(20.5f, y0, -31.5f),
                size: new Vector3(29.0f, 7.6f, 5.5f),
                facingZ: 1f, bays: 7, mats: mats);

            BuildNavigableTroopBarracksWithBunks(patio.transform, y0, mats);
        }

        private static void BuildPatioSteelPortico(Transform parent, float z, float y0, bool covered, Dictionary<string, Material> mats)
        {
            var portico = new GameObject($"Patio_Portico_{z:F1}");
            portico.transform.SetParent(parent, false);
            Material rust = mats["Mat_CJ_UTOP_GateRust"];

            float colH = 7.45f;
            float rightX = 6.35f; // Junto al muro verde de la Calle Junín (visible por encima del muro bajo en la 4ta Imagen)
            float leftX = 19.65f;
            float midX = (leftX + rightX) * 0.5f; // 13.0f
            float span = leftX - rightX;          // 13.3f
            float ridgeH = 1.55f;                 // Altura de la cercha triangular en la cumbrera

            // Columnas reticuladas rojas a ambos lados
            BuildLatticeColumn(portico.transform, new Vector3(rightX, y0, z), colH, rust);
            BuildLatticeColumn(portico.transform, new Vector3(leftX,  y0, z), colH, rust);

            // CERCHA TRIANGULAR RETICULADA 3D (Fiel a la 4ta Imagen de la Calle Junín):
            // 1) Cordón horizontal inferior (Bottom Chord)
            Box("Truss_Bottom_Chord", portico.transform,
                new Vector3(midX, y0 + colH, z),
                new Vector3(span + 0.5f, 0.14f, 0.14f),
                rust);

            // 2) Dos cordones superiores inclinados (Top Chords) formando el triángulo hasta la cumbrera
            float halfChordLen = Mathf.Sqrt((span * 0.5f) * (span * 0.5f) + ridgeH * ridgeH) + 0.45f;
            float chordAngle = Mathf.Atan2(ridgeH, span * 0.5f) * Mathf.Rad2Deg; // ~13.1°

            var topRightChord = Box("Truss_TopChord_StreetSide", portico.transform,
                new Vector3((rightX + midX) * 0.5f - 0.15f, y0 + colH + ridgeH * 0.5f, z),
                new Vector3(halfChordLen, 0.14f, 0.14f),
                rust);
            topRightChord.transform.localRotation = Quaternion.Euler(0f, 0f, chordAngle);

            var topLeftChord = Box("Truss_TopChord_InnerSide", portico.transform,
                new Vector3((leftX + midX) * 0.5f + 0.15f, y0 + colH + ridgeH * 0.5f, z),
                new Vector3(halfChordLen, 0.14f, 0.14f),
                rust);
            topLeftChord.transform.localRotation = Quaternion.Euler(0f, 0f, -chordAngle);

            // 3) Montantes verticales y diagonales en V/X dentro de la cercha triangular
            int webPanels = 6;
            for (int w = 0; w <= webPanels; w++)
            {
                float t = w / (float)webPanels; // 0..1 a lo largo del vano
                float wx = Mathf.Lerp(rightX + 0.4f, leftX - 0.4f, t);
                float distFromCenter = Mathf.Abs(wx - midX) / (span * 0.5f);
                float wyH = Mathf.Max(0.18f, ridgeH * (1f - distFromCenter));

                // Montante vertical
                Box($"Truss_Vertical_{w}", portico.transform,
                    new Vector3(wx, y0 + colH + wyH * 0.5f, z),
                    new Vector3(0.08f, wyH, 0.08f),
                    rust);

                // Diagonal reticulada hacia el siguiente panel
                if (w < webPanels)
                {
                    float nextT = (w + 1) / (float)webPanels;
                    float nextWx = Mathf.Lerp(rightX + 0.4f, leftX - 0.4f, nextT);
                    float nextDist = Mathf.Abs(nextWx - midX) / (span * 0.5f);
                    float nextWyH = Mathf.Max(0.18f, ridgeH * (1f - nextDist));

                    Vector3 p0 = new Vector3(wx, y0 + colH, z);
                    Vector3 p1 = new Vector3(nextWx, y0 + colH + nextWyH, z);
                    if (w >= webPanels / 2)
                    {
                        p0 = new Vector3(wx, y0 + colH + wyH, z);
                        p1 = new Vector3(nextWx, y0 + colH, z);
                    }
                    Vector3 dMid = (p0 + p1) * 0.5f;
                    float dLen = Vector3.Distance(p0, p1);
                    float dAng = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;

                    var diag = Box($"Truss_WebDiag_{w}", portico.transform, dMid, new Vector3(dLen, 0.07f, 0.07f), rust);
                    diag.transform.localRotation = Quaternion.Euler(0f, 0f, dAng);
                }
            }
        }

        private static void BuildLatticeColumn(Transform parent, Vector3 basePos, float height, Material mat)
        {
            Box("Post_1", parent, basePos + new Vector3(0, height * 0.5f, -0.18f), new Vector3(0.10f, height, 0.10f), mat);
            Box("Post_2", parent, basePos + new Vector3(0, height * 0.5f,  0.18f), new Vector3(0.10f, height, 0.10f), mat);
            int rungs = Mathf.RoundToInt(height * 1.5f);
            for (int r = 0; r < rungs; r++)
            {
                var d = Box($"Diag_{r}", parent, basePos + new Vector3(0, 0.4f + r * 0.62f, 0), new Vector3(0.06f, 0.06f, 0.48f), mat);
                d.transform.rotation = Quaternion.Euler((r % 2 == 0) ? 36f : -36f, 0, 0);
            }
        }

        private static void BuildPatioFlagpoleWithBoliviaFlag(Transform parent, Vector3 pos, Dictionary<string, Material> mats)
        {
            var fp = new GameObject("Patio_De_Honor_Bolivia_Flagpole");
            fp.transform.SetParent(parent, false);
            fp.transform.localPosition = pos;

            Box("Pedestal_Step1", fp.transform, new Vector3(0, 0.15f, 0), new Vector3(1.8f, 0.30f, 1.8f), mats["Mat_CJ_Sidewalk_Curb"]);
            Box("Pedestal_Step2", fp.transform, new Vector3(0, 0.42f, 0), new Vector3(1.2f, 0.28f, 1.2f), mats["Mat_CJ_UTOP_GreenWall"]);

            Cylinder("Flagpole_Mast", fp.transform, new Vector3(0, 5.2f, 0), new Vector3(0.11f, 4.8f, 0.11f), mats["Mat_CJ_UTOP_WhiteTrim"]);
            Cylinder("Flagpole_GoldTop", fp.transform, new Vector3(0, 10.1f, 0), new Vector3(0.18f, 0.15f, 0.18f), mats["Mat_Int_Gold"]);

            Mesh waveMesh = ProceduralMeshBuilder.GetWavingPatioFlagMesh();
            var flagGo = ProceduralMeshBuilder.CreateMeshObject("Bolivia_Waving_Flag", fp.transform,
                new Vector3(0.06f, 8.8f, 0f), waveMesh, mats["Mat_Int_FlagBolivia"], addCollider: false);
            flagGo.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        }

        private static void BuildTwoStoryGreenBarracksFacade(Transform parent, Vector3 centerPos, Vector3 size, float facingZ, int bays, Dictionary<string, Material> mats)
        {
            var b = new GameObject("Barracks_TwoStory_Block");
            b.transform.SetParent(parent, false);

            Box("Main_Mass", b.transform, centerPos + new Vector3(0, size.y * 0.5f, 0), size, mats["Mat_CJ_UTOP_Green"]);
            Box("Gabled_Roof", b.transform, centerPos + new Vector3(0, size.y + 0.35f, 0), new Vector3(size.x + 0.6f, 0.70f, size.z + 0.6f), mats["Mat_CJ_Tinglado_RoofDark"]);

            float faceZ = centerPos.z + facingZ * (size.z * 0.5f + 0.05f);
            float spacing = (size.x - 3.0f) / Mathf.Max(1, bays - 1);
            float startX = centerPos.x - (size.x - 3.0f) * 0.5f;

            for (int i = 0; i < bays; i++)
            {
                float bx = startX + i * spacing;
                if (i % 2 == 1)
                {
                    Box($"Door_Frame_{i}", b.transform, new Vector3(bx, centerPos.y + 1.15f, faceZ), new Vector3(1.25f, 2.30f, 0.12f), mats["Mat_CJ_UTOP_WhiteTrim"]);
                    Box($"Door_Wood_{i}", b.transform, new Vector3(bx, centerPos.y + 1.12f, faceZ + facingZ * 0.03f), new Vector3(1.08f, 2.18f, 0.10f), mats["Mat_Int_Mahogany"]);
                }
                else
                {
                    Box($"Win_F1_Frame_{i}", b.transform, new Vector3(bx, centerPos.y + 1.55f, faceZ), new Vector3(1.55f, 1.25f, 0.12f), mats["Mat_CJ_UTOP_WhiteTrim"]);
                    Box($"Win_F1_Glass_{i}", b.transform, new Vector3(bx, centerPos.y + 1.55f, faceZ + facingZ * 0.03f), new Vector3(1.38f, 1.08f, 0.08f), mats["Mat_CJ_Window_Glass"]);
                }

                Box($"Win_F2_Frame_{i}", b.transform, new Vector3(bx, centerPos.y + 5.15f, faceZ), new Vector3(1.55f, 1.25f, 0.12f), mats["Mat_CJ_UTOP_WhiteTrim"]);
                Box($"Win_F2_Glass_{i}", b.transform, new Vector3(bx, centerPos.y + 5.15f, faceZ + facingZ * 0.03f), new Vector3(1.38f, 1.08f, 0.08f), mats["Mat_CJ_Window_Glass"]);
            }
        }

        private static void BuildNavigableTroopBarracksWithBunks(Transform parent, float y0, Dictionary<string, Material> mats)
        {
            var squadRoom = new GameObject("Troop_Barracks_Cuadra_Literas_Y_Roperos");
            squadRoom.transform.SetParent(parent, false);

            float xMin = 19.8f, xMax = 34.5f;
            float zMin = -28.8f, zMax = -16.0f;
            float midX = (xMin + xMax) * 0.5f;
            float midZ = (zMin + zMax) * 0.5f;
            float roomW = xMax - xMin;
            float roomL = zMax - zMin;
            float f1H = 3.8f;

            // Piso interior de azulejo elevado +0.03m sobre el patio para que jamás titile con Patio_Concrete_Floor
            Box("Barracks_Floor", squadRoom.transform, new Vector3(midX, y0 + 0.02f, midZ), new Vector3(roomW - 0.1f, 0.04f, roomL - 0.1f), mats["Mat_Int_BathroomTileFloor"]);

            Box("Barracks_UpperFloor_Mass", squadRoom.transform, new Vector3(midX, y0 + f1H + 1.9f, midZ), new Vector3(roomW, 3.8f, roomL), mats["Mat_CJ_UTOP_Green"]);
            Box("Barracks_Roof", squadRoom.transform, new Vector3(midX, y0 + f1H + 4.0f, midZ), new Vector3(roomW + 0.6f, 0.5f, roomL + 0.6f), mats["Mat_CJ_Tinglado_RoofDark"]);

            Box("Barracks_BackWall_East", squadRoom.transform, new Vector3(xMax - 0.18f, y0 + f1H * 0.5f, midZ), new Vector3(0.35f, f1H, roomL), mats["Mat_Int_CreamWall"]);
            Box("Barracks_SideWall_North", squadRoom.transform, new Vector3(midX, y0 + f1H * 0.5f, zMax - 0.18f), new Vector3(roomW, f1H, 0.35f), mats["Mat_Int_CreamWall"]);
            Box("Barracks_SideWall_South", squadRoom.transform, new Vector3(midX, y0 + f1H * 0.5f, zMin + 0.18f), new Vector3(roomW, f1H, 0.35f), mats["Mat_Int_CreamWall"]);

            Box("Barracks_FrontWall_Right", squadRoom.transform, new Vector3(xMin + 0.18f, y0 + f1H * 0.5f, -18.5f), new Vector3(0.35f, f1H, 5.0f), mats["Mat_CJ_UTOP_Green"]);
            Box("Barracks_FrontWall_Left", squadRoom.transform, new Vector3(xMin + 0.18f, y0 + f1H * 0.5f, -26.3f), new Vector3(0.35f, f1H, 5.0f), mats["Mat_CJ_UTOP_Green"]);
            Box("Barracks_Door_Header", squadRoom.transform, new Vector3(xMin + 0.18f, y0 + 3.1f, -22.4f), new Vector3(0.38f, 1.4f, 2.8f), mats["Mat_CJ_UTOP_Green"]);

            CreateWallSign(squadRoom.transform, new Vector3(xMin - 0.05f, y0 + 2.85f, -22.4f), Quaternion.Euler(0, 90, 0),
                "CUADRA DE TROPA N° 1 — CONTINGENTE ANTIDISTURBIOS UTOP", new Vector2(2.7f, 0.40f), mats);

            for (int i = 0; i < 4; i++)
            {
                float bx = 22.2f + i * 3.1f;
                BuildDoubleBunkBedWithLocker(squadRoom.transform, new Vector3(bx, y0 + 0.04f, -17.5f), yaw: 0f, mats);
                BuildDoubleBunkBedWithLocker(squadRoom.transform, new Vector3(bx, y0 + 0.04f, -27.3f), yaw: 180f, mats);
            }

            // 6 Sillas Interactivas con Ruedas dentro de la Cuadra de Tropa 1 para que los efectivos descansen sentados
            var barracksChairs = new List<InteractiveChair>();
            for (int c = 0; c < 6; c++)
            {
                float cx = 22.4f + (c % 3) * 3.8f;
                float cz = (c < 3) ? -20.0f : -24.8f;
                float cyaw = (c < 3) ? 180f : 0f;
                GameObject chairGo = ProceduralMeshBuilder.BuildSculptedOfficeVisitorChair(
                    squadRoom.transform,
                    new Vector3(cx, y0 + 0.04f, cz),
                    cyaw,
                    mats["Mat_Int_LeatherBlack"],
                    mats["Mat_Int_Chrome"]);
                var ic = chairGo.GetComponent<InteractiveChair>();
                if (ic != null)
                {
                    ic.chairLabel = $"Silla de Cuadra de Tropa N° {c + 1}";
                    barracksChairs.Add(ic);
                }
            }

            // Desplegar el contingente de 30 Policías UTOP en la Cuadra de Tropa 1 y vincularlos al sistema de Formación 3x10 en el Patio de Honor
            CharacterAssetsAndClipsProcessor.Spawn30UTOPTroopOfficers(parent, barracksChairs);

            CreateCeilingLight(squadRoom.transform, new Vector3(24.5f, y0 + f1H - 0.2f, midZ), new Color(0.95f, 0.97f, 1.0f), 3.0f, 12f);
            CreateCeilingLight(squadRoom.transform, new Vector3(30.5f, y0 + f1H - 0.2f, midZ), new Color(0.95f, 0.97f, 1.0f), 3.0f, 12f);
        }

        private static void BuildDoubleBunkBedWithLocker(Transform parent, Vector3 pos, float yaw, Dictionary<string, Material> mats)
        {
            var unit = new GameObject("BunkBed_And_Locker_Set");
            unit.transform.SetParent(parent, false);
            unit.transform.localPosition = pos;
            unit.transform.localRotation = Quaternion.Euler(0, yaw, 0);

            Material metalFrame = mats["Mat_Int_SteelRack"];
            Material mattressMat = mats["Mat_CJ_UTOP_WhiteTrim"];
            Material blanketMat = mats["Mat_Int_UniformOlive"];
            Material lockerMat = mats["Mat_Int_OfficeWallGrey"];

            float bedW = 0.96f;
            float bedL = 2.00f;
            float postH = 1.85f;

            float[] px = { -bedW * 0.48f, bedW * 0.48f };
            float[] pz = { -bedL * 0.48f, bedL * 0.48f };
            foreach (float x in px)
                foreach (float z in pz)
                    Cylinder("Bed_Post", unit.transform, new Vector3(x, postH * 0.5f, z), new Vector3(0.05f, postH * 0.5f, 0.05f), metalFrame);

            float[] bunkHeights = { 0.42f, 1.38f };
            foreach (float by in bunkHeights)
            {
                Box("Bed_Frame", unit.transform, new Vector3(0, by, 0), new Vector3(bedW, 0.06f, bedL), metalFrame);
                Box("Mattress", unit.transform, new Vector3(0, by + 0.09f, 0), new Vector3(bedW - 0.06f, 0.12f, bedL - 0.06f), mattressMat);
                Box("UTOP_Olive_Blanket", unit.transform, new Vector3(0, by + 0.105f, -0.25f), new Vector3(bedW - 0.04f, 0.13f, bedL * 0.68f), blanketMat);
                Box("Pillow", unit.transform, new Vector3(0, by + 0.18f, 0.72f), new Vector3(0.62f, 0.08f, 0.32f), mattressMat);
            }

            Box("Upper_Safety_Rail", unit.transform, new Vector3(-bedW * 0.48f, 1.65f, -0.1f), new Vector3(0.04f, 0.25f, 1.2f), metalFrame);
            for (int r = 0; r < 4; r++)
                Box($"Ladder_Rung_{r}", unit.transform, new Vector3(bedW * 0.49f, 0.55f + r * 0.28f, -0.65f), new Vector3(0.04f, 0.03f, 0.38f), metalFrame);

            Vector3 lockerPos = new Vector3(0.92f, 0f, 0.42f);
            Box("Personal_Locker_Body", unit.transform, lockerPos + new Vector3(0, 0.92f, 0), new Vector3(0.68f, 1.84f, 0.56f), lockerMat);
            Box("Locker_Door_Left", unit.transform, lockerPos + new Vector3(-0.16f, 0.92f, -0.29f), new Vector3(0.30f, 1.72f, 0.02f), mats["Mat_CJ_Sidewalk_Curb"]);
            Box("Locker_Door_Right", unit.transform, lockerPos + new Vector3( 0.16f, 0.92f, -0.29f), new Vector3(0.30f, 1.72f, 0.02f), mats["Mat_CJ_Sidewalk_Curb"]);
            Box("Locker_Handle_L", unit.transform, lockerPos + new Vector3(-0.04f, 0.95f, -0.31f), new Vector3(0.02f, 0.12f, 0.03f), metalFrame);
            Box("Locker_Handle_R", unit.transform, lockerPos + new Vector3( 0.04f, 0.95f, -0.31f), new Vector3(0.02f, 0.12f, 0.03f), metalFrame);
        }

        private static void CreatePatioGoldWallLettering(Transform parent, Vector3 pos, Dictionary<string, Material> mats)
        {
            var signGo = new GameObject("Patio_UTOP_Gold_Wall_Text");
            signGo.transform.SetParent(parent, false);
            signGo.transform.localPosition = pos;
            signGo.transform.localRotation = Quaternion.Euler(0, -90f, 0);

            var tmp = signGo.AddComponent<TextMeshPro>();
            tmp.text = "<b>- UTOP -</b>\n<size=52%>UNIDAD TÁCTICA DE\nOPERACIONES POLICIALES</size>";
            tmp.fontSize = 4.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.94f, 0.80f, 0.25f);
            tmp.rectTransform.sizeDelta = new Vector2(6.5f, 2.2f);

            Box("Floodlight_Fixture", parent, pos + new Vector3(0.12f, 1.25f, 0f), new Vector3(0.24f, 0.18f, 0.36f), mats["Mat_Int_SteelRack"]);
        }
        #endregion

        #region Helpers de Iluminación, Señalética y Geometría
        private static void CreateCeilingLight(Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var lGo = new GameObject("Interior_Light");
            lGo.transform.SetParent(parent, false);
            lGo.transform.localPosition = pos;
            var light = lGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void CreatePendantLamp(Transform parent, Vector3 pos, Color color, float intensity, float range, Dictionary<string, Material> mats)
        {
            Cylinder("Lamp_Cord", parent, pos + new Vector3(0, 0.22f, 0), new Vector3(0.02f, 0.22f, 0.02f), mats["Mat_Int_SteelRack"]);
            Cylinder("Lamp_Shade", parent, pos, new Vector3(0.36f, 0.08f, 0.36f), mats["Mat_Int_GunMetal"]);
            CreateCeilingLight(parent, pos + new Vector3(0, -0.12f, 0), color, intensity, range);
        }

        private static void CreateWallSign(Transform parent, Vector3 pos, Quaternion rot, string text, Vector2 size, Dictionary<string, Material> mats)
        {
            var sign = Box("Sign_Board", parent, pos, new Vector3(size.x, size.y, 0.03f), mats["Mat_Int_DarkWood"]);
            sign.transform.localRotation = rot;

            var txtGo = new GameObject("Sign_TMP");
            txtGo.transform.SetParent(sign.transform, false);
            txtGo.transform.localPosition = new Vector3(0, 0, -0.6f);
            txtGo.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f);

            var tmp = txtGo.AddComponent<TextMeshPro>();
            tmp.text = $"<b>{text}</b>";
            tmp.fontSize = 1.15f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.95f, 0.85f, 0.45f);
            tmp.rectTransform.sizeDelta = size;
        }

        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
        #endregion
    }
}
#endif
