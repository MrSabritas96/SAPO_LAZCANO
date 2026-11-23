#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimuladorBloqueos.EditorTools
{
    /// <summary>
    /// Generador de mallas 3D de alta densidad poligonal (curvas, perfiles extruidos y superficies suaves)
    /// para vehículos paceños (Toyota Caldina/Corolla, Volkswagen Peta, Minibús HiAce, Patrulla UTOP),
    /// banderas con pliegues orgánicos, armamento antidisturbios y mobiliario ejecutivo.
    /// </summary>
    public static class ProceduralMeshBuilder
    {
        private const string MeshFolder = "Assets/_Project/Art/Environments/Meshes";
        private static Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();

        public static void ClearCache()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Environments"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Environments");
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Environments", "Meshes");
            _meshCache.Clear();
        }

        private static Mesh GetOrCreateMesh(string name, System.Func<Mesh> generator)
        {
            if (_meshCache.TryGetValue(name, out Mesh cached) && cached != null)
                return cached;

            string path = $"{MeshFolder}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            Mesh m = generator();
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            _meshCache[name] = m;
            return m;
        }

        #region 1. Mallas Curvas y Perfiladas (Extrusión longitudinal con bisel lateral)
        /// <summary>
        /// Crea una malla automotriz 3D continua y hermética (watertight) a partir de secciones transversales
        /// en el eje Z, con faldón inferior biselado, puertas abombadas, línea de cintura, pilares inclinados y techo coronado.
        /// </summary>
        public static Mesh CreateContouredBodyMesh(string meshName, Vector4[] profileZ_YBot_YTop_HalfW, int bevelSteps = 5)
        {
            return GetOrCreateMesh(meshName, () =>
            {
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();

                const int ringPoints = 15;
                int slices = profileZ_YBot_YTop_HalfW.Length;

                var rawRings = new Vector3[slices, ringPoints];
                for (int s = 0; s < slices; s++)
                {
                    float z = profileZ_YBot_YTop_HalfW[s].x;
                    float yBot = profileZ_YBot_YTop_HalfW[s].y;
                    float yTop = profileZ_YBot_YTop_HalfW[s].z;
                    float hw = profileZ_YBot_YTop_HalfW[s].w;
                    float h = Mathf.Max(0.06f, yTop - yBot);

                    float yRocker = yBot + h * 0.14f;
                    float yDoorMid = yBot + h * 0.42f;
                    float yBelt = yBot + h * 0.62f;
                    float yShoulder = yBot + h * 0.88f;
                    float yCrown = yTop + Mathf.Min(0.04f, h * 0.06f);

                    float hwBot = hw * 0.90f;
                    float hwBulge = hw * 1.04f;
                    float hwBelt = hw * 0.98f;
                    float hwShoulder = hw * 0.85f;
                    float hwRoof = hw * 0.76f;

                    Vector2[] ringXY = new Vector2[ringPoints]
                    {
                        new Vector2( 0f,          yBot),
                        new Vector2( hwBot,       yBot),
                        new Vector2( hw,          yRocker),
                        new Vector2( hwBulge,     yDoorMid),
                        new Vector2( hwBelt,      yBelt),
                        new Vector2( hwShoulder,  yShoulder),
                        new Vector2( hwRoof,      yTop),
                        new Vector2( 0f,          yCrown),
                        new Vector2(-hwRoof,      yTop),
                        new Vector2(-hwShoulder,  yShoulder),
                        new Vector2(-hwBelt,      yBelt),
                        new Vector2(-hwBulge,     yDoorMid),
                        new Vector2(-hw,          yRocker),
                        new Vector2(-hwBot,       yBot),
                        new Vector2( 0f,          yBot)
                    };

                    for (int p = 0; p < ringPoints; p++)
                    {
                        rawRings[s, p] = new Vector3(ringXY[p].x, ringXY[p].y, z);
                    }
                }

                Vector3 interiorCenter = new Vector3(0f, 0.65f, 0f);

                for (int s = 0; s < slices - 1; s++)
                {
                    for (int p = 0; p < ringPoints - 1; p++)
                    {
                        AddOutwardQuad(verts, uvs, tris,
                            rawRings[s, p],
                            rawRings[s + 1, p],
                            rawRings[s + 1, p + 1],
                            rawRings[s, p + 1],
                            interiorCenter);
                    }
                }

                // Tapas frontal y trasera con orientación exterior garantizada
                Vector3 fCenter = new Vector3(0f, (profileZ_YBot_YTop_HalfW[0].y + profileZ_YBot_YTop_HalfW[0].z) * 0.5f, profileZ_YBot_YTop_HalfW[0].x);
                for (int p = 0; p < ringPoints - 1; p++)
                {
                    AddOutwardTri(verts, uvs, tris, fCenter, rawRings[0, p], rawRings[0, p + 1], interiorCenter);
                }

                int lastS = slices - 1;
                Vector3 bCenter = new Vector3(0f, (profileZ_YBot_YTop_HalfW[lastS].y + profileZ_YBot_YTop_HalfW[lastS].z) * 0.5f, profileZ_YBot_YTop_HalfW[lastS].x);
                for (int p = 0; p < ringPoints - 1; p++)
                {
                    AddOutwardTri(verts, uvs, tris, bCenter, rawRings[lastS, p], rawRings[lastS, p + 1], interiorCenter);
                }

                var mesh = new Mesh();
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        private static void AddOutwardQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 interiorCenter)
        {
            Vector3 qCenter = (a + b + c + d) * 0.25f;
            Vector3 outward = qCenter - interiorCenter;
            Vector3 n = Vector3.Cross(b - a, c - a);
            int idx = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));

            if (Vector3.Dot(n, outward) >= 0f)
            {
                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
            }
            else
            {
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
                tris.Add(idx); tris.Add(idx + 3); tris.Add(idx + 2);
            }
        }

        private static void AddOutwardTri(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 interiorCenter)
        {
            Vector3 tCenter = (a + b + c) / 3f;
            Vector3 outward = tCenter - interiorCenter;
            Vector3 n = Vector3.Cross(b - a, c - a);
            int idx = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(new Vector2(0.5f, 0.5f)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));

            if (Vector3.Dot(n, outward) >= 0f)
            {
                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            }
            else
            {
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
            }
        }
        #endregion

        #region 2. Vehículos Paceños Detallados (Toyota Caldina/Corolla, VW Peta, Minibús, Patrulla UTOP)
        public static GameObject BuildToyotaCaldinaWagon(Transform parent, Vector3 pos, float yaw, float pitchSlope, Material bodyMat, Dictionary<string, Material> mats)
        {
            var car = new GameObject("Vehicle_Toyota_Caldina_Wagon");
            car.transform.SetParent(parent, false);
            car.transform.position = pos;
            car.transform.rotation = Quaternion.Euler(pitchSlope, yaw, 0f);

            // Perfil longitudinal aerodinámico de Toyota Caldina / Corolla Fielder Wagon
            Vector4[] profile = new Vector4[]
            {
                new Vector4(-2.22f, 0.25f, 0.62f, 0.76f), // Parachoques delantero redondeado
                new Vector4(-2.08f, 0.22f, 0.76f, 0.84f), // Punta del capó y faros
                new Vector4(-1.25f, 0.20f, 0.88f, 0.88f), // Base del parabrisas
                new Vector4(-0.55f, 0.20f, 1.42f, 0.85f), // Techo delantero (Pilar A)
                new Vector4( 0.45f, 0.20f, 1.44f, 0.85f), // Techo central (Pilar B)
                new Vector4( 1.65f, 0.22f, 1.40f, 0.83f), // Techo trasero Station Wagon (Pilar D)
                new Vector4( 2.12f, 0.26f, 0.90f, 0.82f), // Portón trasero inclinado
                new Vector4( 2.24f, 0.28f, 0.62f, 0.78f)  // Parachoques trasero
            };

            Mesh bodyMesh = CreateContouredBodyMesh("Mesh_ToyotaCaldina_Body", profile, 6);
            var bodyGo = CreateMeshObject("Body_Chassis", car.transform, Vector3.zero, bodyMesh, bodyMat, addCollider: true);

            // Cabina acristalada envolvente (Parabrisas inclinado y ventanillas laterales)
            Vector4[] glassProfile = new Vector4[]
            {
                new Vector4(-1.20f, 0.84f, 0.90f, 0.78f),
                new Vector4(-0.52f, 0.85f, 1.39f, 0.79f),
                new Vector4( 1.58f, 0.86f, 1.37f, 0.77f),
                new Vector4( 2.04f, 0.88f, 0.92f, 0.74f)
            };
            Mesh glassMesh = CreateContouredBodyMesh("Mesh_ToyotaCaldina_Glass", glassProfile, 4);
            CreateMeshObject("Cabin_Windows", car.transform, Vector3.zero, glassMesh, mats["Mat_CJ_Window_Glass"], addCollider: false);

            // Parrilla frontal, faros delanteros, luces traseras y rieles portaequipaje en el techo
            CreatePartBox("Front_Grille", car.transform, new Vector3(0f, 0.56f, -2.18f), new Vector3(0.82f, 0.16f, 0.08f), mats["Mat_CJ_Iron_Grille"]);
            CreatePartBox("Headlight_L", car.transform, new Vector3(-0.56f, 0.58f, -2.15f), new Vector3(0.32f, 0.14f, 0.10f), mats["Mat_CJ_UTOP_WhiteTrim"]);
            CreatePartBox("Headlight_R", car.transform, new Vector3( 0.56f, 0.58f, -2.15f), new Vector3(0.32f, 0.14f, 0.10f), mats["Mat_CJ_UTOP_WhiteTrim"]);
            CreatePartBox("Taillight_L", car.transform, new Vector3(-0.62f, 0.78f,  2.14f), new Vector3(0.28f, 0.22f, 0.10f), mats["Mat_CJ_Traffic_Cone"]);
            CreatePartBox("Taillight_R", car.transform, new Vector3( 0.62f, 0.78f,  2.14f), new Vector3(0.28f, 0.22f, 0.10f), mats["Mat_CJ_Traffic_Cone"]);

            // Rieles de techo (Roof Rack clásico de Caldina en La Paz)
            CreatePartBox("RoofRail_L", car.transform, new Vector3(-0.52f, 1.46f, 0.55f), new Vector3(0.05f, 0.06f, 1.9f), mats["Mat_CJ_Iron_Grille"]);
            CreatePartBox("RoofRail_R", car.transform, new Vector3( 0.52f, 1.46f, 0.55f), new Vector3(0.05f, 0.06f, 1.9f), mats["Mat_CJ_Iron_Grille"]);
            // Espejos retrovisores
            CreatePartBox("Mirror_L", car.transform, new Vector3(-0.92f, 0.92f, -0.95f), new Vector3(0.18f, 0.11f, 0.12f), bodyMat);
            CreatePartBox("Mirror_R", car.transform, new Vector3( 0.92f, 0.92f, -0.95f), new Vector3(0.18f, 0.11f, 0.12f), bodyMat);

            // 4 Ruedas de alta definición con neumático y aro plateado
            AttachDetailedWheels(car.transform, wheelBaseHalfZ: 1.34f, trackHalfX: 0.82f, wheelRadius: 0.33f, mats);
            return car;
        }

        public static GameObject BuildVolkswagenPeta(Transform parent, Vector3 pos, float yaw, float pitchSlope, Material bodyMat, Dictionary<string, Material> mats)
        {
            var car = new GameObject("Vehicle_VW_Peta_Beetle");
            car.transform.SetParent(parent, false);
            car.transform.position = pos;
            car.transform.rotation = Quaternion.Euler(pitchSlope, yaw, 0f);

            // Silueta curva icónica del Volkswagen Escarabajo ("Peta" paceña)
            Vector4[] beetleProfile = new Vector4[]
            {
                new Vector4(-1.95f, 0.24f, 0.52f, 0.62f), // Frente redondeado del capó
                new Vector4(-1.60f, 0.22f, 0.78f, 0.72f), // Curva del capó delantero
                new Vector4(-0.95f, 0.20f, 0.94f, 0.76f), // Base del parabrisas
                new Vector4(-0.42f, 0.20f, 1.42f, 0.74f), // Curva superior del parabrisas
                new Vector4( 0.15f, 0.20f, 1.48f, 0.75f), // Domo central del techo
                new Vector4( 0.78f, 0.20f, 1.36f, 0.74f), // Caída trasera curva
                new Vector4( 1.45f, 0.22f, 0.96f, 0.72f), // Capó del motor trasero
                new Vector4( 1.92f, 0.26f, 0.54f, 0.62f)  // Cola redondeada
            };

            Mesh beetleMesh = CreateContouredBodyMesh("Mesh_VWPeta_Body", beetleProfile, 7);
            CreateMeshObject("Beetle_Body", car.transform, Vector3.zero, beetleMesh, bodyMat, addCollider: true);

            // Ventanillas curvas del Escarabajo
            Vector4[] beetleGlass = new Vector4[]
            {
                new Vector4(-0.88f, 0.90f, 0.96f, 0.68f),
                new Vector4(-0.38f, 0.92f, 1.38f, 0.69f),
                new Vector4( 0.20f, 0.92f, 1.43f, 0.69f),
                new Vector4( 0.75f, 0.90f, 1.31f, 0.67f),
                new Vector4( 1.18f, 0.88f, 0.98f, 0.64f)
            };
            Mesh glassMesh = CreateContouredBodyMesh("Mesh_VWPeta_Glass", beetleGlass, 5);
            CreateMeshObject("Beetle_Glass", car.transform, Vector3.zero, glassMesh, mats["Mat_CJ_Window_Glass"], addCollider: false);

            // Guardabarros abombados (Fenders clásicos del VW Escarabajo) y estribos laterales
            Mesh fenderMesh = GetOrCreateSphereWheelArchMesh("Mesh_VWPeta_Fender", 0.44f, 0.26f);
            float[] fZ = { -1.22f, 1.20f };
            float[] fX = { -0.74f, 0.74f };
            foreach (float z in fZ)
            {
                foreach (float x in fX)
                {
                    CreateMeshObject("Beetle_Fender", car.transform, new Vector3(x, 0.34f, z), fenderMesh, bodyMat, addCollider: false);
                }
            }

            // Parachoques cromados y faros redondos clásicos
            CreatePartBox("Chrome_Bumper_Front", car.transform, new Vector3(0f, 0.34f, -2.02f), new Vector3(1.48f, 0.09f, 0.10f), mats["Mat_CJ_UTOP_WhiteTrim"]);
            CreatePartBox("Chrome_Bumper_Rear", car.transform, new Vector3(0f, 0.34f,  1.98f), new Vector3(1.48f, 0.09f, 0.10f), mats["Mat_CJ_UTOP_WhiteTrim"]);

            AttachDetailedWheels(car.transform, wheelBaseHalfZ: 1.20f, trackHalfX: 0.78f, wheelRadius: 0.32f, mats);
            return car;
        }

        public static GameObject BuildLaPazMinibus(Transform parent, Vector3 pos, float yaw, float pitchSlope, Material bodyMat, Material stripeMat, Dictionary<string, Material> mats)
        {
            var van = new GameObject("Vehicle_Minibus_HiAce");
            van.transform.SetParent(parent, false);
            van.transform.position = pos;
            van.transform.rotation = Quaternion.Euler(pitchSlope, yaw, 0f);

            // Perfil Toyota HiAce (Minibús tradicional de La Paz con frente inclinado y techo elevado)
            Vector4[] vanProfile = new Vector4[]
            {
                new Vector4(-2.38f, 0.28f, 0.75f, 0.84f), // Parachoques delantero
                new Vector4(-2.28f, 0.26f, 1.08f, 0.88f), // Nariz frontal bajo el parabrisas
                new Vector4(-1.82f, 0.24f, 1.88f, 0.86f), // Borde superior del parabrisas
                new Vector4(-0.80f, 0.24f, 1.98f, 0.87f), // Techo delantero
                new Vector4( 1.95f, 0.24f, 1.98f, 0.87f), // Techo trasero
                new Vector4( 2.32f, 0.28f, 1.82f, 0.85f), // Portón trasero superior
                new Vector4( 2.40f, 0.28f, 0.72f, 0.85f)  // Parachoques trasero
            };

            Mesh vanMesh = CreateContouredBodyMesh("Mesh_Minibus_Body", vanProfile, 5);
            CreateMeshObject("Minibus_Body", van.transform, Vector3.zero, vanMesh, bodyMat, addCollider: true);

            // Franja continua de ventanillas panorámicas
            Vector4[] vanGlass = new Vector4[]
            {
                new Vector4(-2.15f, 1.06f, 1.15f, 0.82f),
                new Vector4(-1.78f, 1.08f, 1.80f, 0.82f),
                new Vector4( 2.25f, 1.08f, 1.78f, 0.81f)
            };
            Mesh glassMesh = CreateContouredBodyMesh("Mesh_Minibus_Glass", vanGlass, 4);
            CreateMeshObject("Minibus_Windows", van.transform, Vector3.zero, glassMesh, mats["Mat_CJ_Window_Glass"], addCollider: false);

            // Franja lateral identificativa y letrero luminoso de ruta en el parabrisas ("JUNÍN - CENTRO - UTOP")
            CreatePartBox("Lateral_Stripe_L", van.transform, new Vector3(-0.89f, 0.88f, 0.1f), new Vector3(0.03f, 0.18f, 4.3f), stripeMat);
            CreatePartBox("Lateral_Stripe_R", van.transform, new Vector3( 0.89f, 0.88f, 0.1f), new Vector3(0.03f, 0.18f, 4.3f), stripeMat);
            CreatePartBox("Route_Sign_Placard", van.transform, new Vector3(-0.35f, 1.22f, -2.08f), new Vector3(0.55f, 0.22f, 0.05f), mats["Mat_CJ_Traffic_Cone"]);

            CreatePartBox("Headlight_L", van.transform, new Vector3(-0.62f, 0.74f, -2.35f), new Vector3(0.34f, 0.18f, 0.08f), mats["Mat_CJ_UTOP_WhiteTrim"]);
            CreatePartBox("Headlight_R", van.transform, new Vector3( 0.62f, 0.74f, -2.35f), new Vector3(0.34f, 0.18f, 0.08f), mats["Mat_CJ_UTOP_WhiteTrim"]);

            AttachDetailedWheels(van.transform, wheelBaseHalfZ: 1.42f, trackHalfX: 0.84f, wheelRadius: 0.35f, mats);
            return van;
        }

        public static GameObject BuildUTOPPatrolPickup(Transform parent, Vector3 pos, float yaw, Material bodyMat, Dictionary<string, Material> mats)
        {
            var truck = new GameObject("Vehicle_UTOP_Patrol_Pickup");
            truck.transform.SetParent(parent, false);
            truck.transform.position = pos;
            truck.transform.rotation = Quaternion.Euler(0f, yaw, 0f); // Siempre horizontal en el garaje plano

            // Perfil camioneta doble cabina policial (Hilux / Ranger)
            Vector4[] pickupProfile = new Vector4[]
            {
                new Vector4(-2.45f, 0.34f, 0.82f, 0.88f), // Parachoques delantero
                new Vector4(-2.30f, 0.32f, 1.06f, 0.92f), // Parrilla alta
                new Vector4(-1.30f, 0.30f, 1.14f, 0.94f), // Capó musculoso
                new Vector4(-0.62f, 0.30f, 1.76f, 0.90f), // Techo cabina delantera
                new Vector4( 0.72f, 0.30f, 1.76f, 0.90f), // Fin de doble cabina
                new Vector4( 0.76f, 0.32f, 1.12f, 0.92f), // Inicio de caja trasera (platón)
                new Vector4( 2.38f, 0.34f, 1.12f, 0.92f), // Compuerta trasera
                new Vector4( 2.46f, 0.36f, 0.68f, 0.88f)
            };

            Mesh truckMesh = CreateContouredBodyMesh("Mesh_UTOPPickup_Body", pickupProfile, 5);
            CreateMeshObject("Pickup_Body", truck.transform, Vector3.zero, truckMesh, bodyMat, addCollider: true);

            // Cristales de la doble cabina
            Vector4[] cabGlass = new Vector4[]
            {
                new Vector4(-1.24f, 1.10f, 1.16f, 0.84f),
                new Vector4(-0.58f, 1.12f, 1.71f, 0.84f),
                new Vector4( 0.68f, 1.12f, 1.71f, 0.84f),
                new Vector4( 0.72f, 1.10f, 1.16f, 0.84f)
            };
            Mesh glassMesh = CreateContouredBodyMesh("Mesh_UTOPPickup_Glass", cabGlass, 4);
            CreateMeshObject("Pickup_Glass", truck.transform, Vector3.zero, glassMesh, mats["Mat_CJ_Window_Glass"], addCollider: false);

            // Franja verde institucional UTOP en los laterales y baliza policial en el techo
            CreatePartBox("UTOP_Stripe_L", truck.transform, new Vector3(-0.95f, 0.88f, 0f), new Vector3(0.03f, 0.22f, 4.4f), mats["Mat_CJ_UTOP_Green"]);
            CreatePartBox("UTOP_Stripe_R", truck.transform, new Vector3( 0.95f, 0.88f, 0f), new Vector3(0.03f, 0.22f, 4.4f), mats["Mat_CJ_UTOP_Green"]);
            CreatePartBox("Police_Lightbar_Base", truck.transform, new Vector3(0f, 1.80f, 0.05f), new Vector3(1.15f, 0.08f, 0.26f), mats["Mat_CJ_UTOP_WhiteTrim"]);
            CreatePartBox("Police_Siren_Red", truck.transform, new Vector3(-0.35f, 1.85f, 0.05f), new Vector3(0.42f, 0.10f, 0.22f), mats["Mat_CJ_Traffic_Cone"]);
            CreatePartBox("Police_Siren_Blue", truck.transform, new Vector3( 0.35f, 1.85f, 0.05f), new Vector3(0.42f, 0.10f, 0.22f), mats["Mat_CJ_Window_Glass"]);

            // Mataburros delantero y barra antivuelco en la caja
            CreatePartBox("Bullbar_Center", truck.transform, new Vector3(0f, 0.72f, -2.50f), new Vector3(1.1f, 0.48f, 0.10f), mats["Mat_CJ_Iron_Grille"]);
            CreatePartBox("Rollbar_Arch", truck.transform, new Vector3(0f, 1.42f, 0.92f), new Vector3(1.56f, 0.65f, 0.10f), mats["Mat_CJ_Iron_Grille"]);

            AttachDetailedWheels(truck.transform, wheelBaseHalfZ: 1.48f, trackHalfX: 0.88f, wheelRadius: 0.38f, mats);
            return truck;
        }

        private static void AttachDetailedWheels(Transform vehicle, float wheelBaseHalfZ, float trackHalfX, float wheelRadius, Dictionary<string, Material> mats)
        {
            Mesh tireMesh = GetOrCreateWheelMesh($"Mesh_Wheel_{Mathf.RoundToInt(wheelRadius * 100)}", wheelRadius, 0.22f, 20);
            Mesh rimMesh = GetOrCreateWheelMesh($"Mesh_Rim_{Mathf.RoundToInt(wheelRadius * 100)}", wheelRadius * 0.62f, 0.24f, 16);

            float[] zs = { -wheelBaseHalfZ, wheelBaseHalfZ };
            float[] xs = { -trackHalfX, trackHalfX };

            foreach (float z in zs)
            {
                foreach (float x in xs)
                {
                    var wPos = new Vector3(x, wheelRadius, z);
                    CreateMeshObject("Wheel_Tire", vehicle, wPos, tireMesh, mats["Mat_CJ_Iron_Grille"], addCollider: false);
                    CreateMeshObject("Wheel_Rim", vehicle, wPos, rimMesh, mats["Mat_CJ_UTOP_WhiteTrim"], addCollider: false);
                }
            }
        }

        private static Mesh GetOrCreateWheelMesh(string name, float radius, float width, int segments)
        {
            return GetOrCreateMesh(name, () =>
            {
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                float hw = width * 0.5f;

                for (int i = 0; i < segments; i++)
                {
                    float a0 = (i / (float)segments) * Mathf.PI * 2f;
                    float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                    Vector3 p0L = new Vector3(-hw, Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius);
                    Vector3 p0R = new Vector3( hw, Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius);
                    Vector3 p1R = new Vector3( hw, Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius);
                    Vector3 p1L = new Vector3(-hw, Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius);

                    AddOutwardQuad(verts, uvs, tris, p0L, p0R, p1R, p1L, Vector3.zero);
                    AddOutwardTri(verts, uvs, tris, new Vector3(-hw, 0, 0), p0L, p1L, Vector3.zero);
                    AddOutwardTri(verts, uvs, tris, new Vector3( hw, 0, 0), p0R, p1R, Vector3.zero);
                }

                var m = new Mesh();
                m.SetVertices(verts);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            });
        }

        private static Mesh GetOrCreateSphereWheelArchMesh(string name, float radiusZ, float widthX)
        {
            return GetOrCreateMesh(name, () =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Mesh src = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(go);
                Vector3[] v = src.vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    v[i] = new Vector3(v[i].x * widthX, Mathf.Max(0f, v[i].y * radiusZ * 1.2f), v[i].z * radiusZ * 1.4f);
                }
                src.vertices = v;
                src.RecalculateNormals();
                src.RecalculateBounds();
                return src;
            });
        }
        #endregion

        #region 3. Banderas con Pliegues Orgánicos 3D y Armamento Antidisturbios Esculpido
        /// <summary>
        /// Crea la malla de una bandera de interior colgada de asta con pliegues cónicos naturales (Imagen 2: Oficina del Capitán).
        /// </summary>
        public static Mesh GetDrapedStandingFlagMesh()
        {
            return GetOrCreateMesh("Mesh_Draped_Standing_Flag", () =>
            {
                int cols = 20, rows = 24;
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();

                for (int r = 0; r <= rows; r++)
                {
                    float v = r / (float)rows; // 0 = punta superior en el asta, 1 = borde inferior amplio
                    float y = Mathf.Lerp(1.65f, 0.25f, v);
                    float spreadW = Mathf.Lerp(0.04f, 0.58f, Mathf.Pow(v, 0.85f));

                    for (int c = 0; c <= cols; c++)
                    {
                        float u = c / (float)cols;
                        float angle = Mathf.Lerp(-Mathf.PI * 0.65f, Mathf.PI * 0.65f, u);
                        float foldWave = Mathf.Sin(u * Mathf.PI * 5f) * 0.045f * v;
                        float rad = spreadW * 0.5f + foldWave;

                        float x = Mathf.Sin(angle) * rad;
                        float z = Mathf.Cos(angle) * rad * 0.55f;
                        // Caída diagonal en las esquinas inferiores como en la foto del Capitán
                        float dropY = y - Mathf.Abs(u - 0.5f) * 0.18f * v;

                        verts.Add(new Vector3(x, dropY, z));
                        uvs.Add(new Vector2(u, 1f - v));
                    }
                }

                int frontCount = verts.Count;
                for (int i = 0; i < frontCount; i++)
                {
                    verts.Add(verts[i]);
                    uvs.Add(uvs[i]);
                }

                int stride = cols + 1;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        int i0 = r * stride + c;
                        int i1 = i0 + 1;
                        int i2 = (r + 1) * stride + c;
                        int i3 = i2 + 1;

                        // Cara frontal
                        tris.Add(i0); tris.Add(i2); tris.Add(i1);
                        tris.Add(i1); tris.Add(i2); tris.Add(i3);
                        // Cara trasera con vértices independientes para no cancelar normales
                        tris.Add(frontCount + i0); tris.Add(frontCount + i1); tris.Add(frontCount + i2);
                        tris.Add(frontCount + i1); tris.Add(frontCount + i3); tris.Add(frontCount + i2);
                    }
                }

                var m = new Mesh();
                m.SetVertices(verts);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>
        /// Crea una bandera flameando al viento para el mástil del Patio de Honor (Imagen 5).
        /// </summary>
        public static Mesh GetWavingPatioFlagMesh()
        {
            return GetOrCreateMesh("Mesh_Waving_Patio_Flag", () =>
            {
                int cols = 24, rows = 14;
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();

                for (int r = 0; r <= rows; r++)
                {
                    float v = r / (float)rows;
                    float y = (v - 0.5f) * 1.45f;
                    for (int c = 0; c <= cols; c++)
                    {
                        float u = c / (float)cols;
                        float x = u * 2.25f;
                        float z = Mathf.Sin(u * Mathf.PI * 2.6f) * 0.18f * u;
                        float sagY = y - u * 0.12f + Mathf.Cos(u * Mathf.PI * 2.0f) * 0.06f * u;
                        verts.Add(new Vector3(x, sagY, z));
                        uvs.Add(new Vector2(u, v));
                    }
                }

                int frontCount = verts.Count;
                for (int i = 0; i < frontCount; i++)
                {
                    verts.Add(verts[i]);
                    uvs.Add(uvs[i]);
                }

                int stride = cols + 1;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        int i0 = r * stride + c;
                        int i1 = i0 + 1;
                        int i2 = (r + 1) * stride + c;
                        int i3 = i2 + 1;
                        tris.Add(i0); tris.Add(i2); tris.Add(i1);
                        tris.Add(i1); tris.Add(i2); tris.Add(i3);
                        tris.Add(frontCount + i0); tris.Add(frontCount + i1); tris.Add(frontCount + i2);
                        tris.Add(frontCount + i1); tris.Add(frontCount + i3); tris.Add(frontCount + i2);
                    }
                }

                var m = new Mesh();
                m.SetVertices(verts);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>
        /// Malla curvada de Escudo Antidisturbios de policarbonato (Imagen 3).
        /// </summary>
        public static Mesh GetCurvedRiotShieldMesh()
        {
            return GetOrCreateMesh("Mesh_Curved_Riot_Shield", () =>
            {
                int segs = 14;
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                float radius = 0.42f;
                float height = 0.96f;

                for (int yStep = 0; yStep <= 1; yStep++)
                {
                    float y = (yStep - 0.5f) * height;
                    for (int s = 0; s <= segs; s++)
                    {
                        float t = s / (float)segs;
                        float ang = Mathf.Lerp(-0.62f, 0.62f, t);
                        verts.Add(new Vector3(Mathf.Sin(ang) * radius, y, Mathf.Cos(ang) * radius - radius));
                        uvs.Add(new Vector2(t, yStep));
                    }
                }

                int frontCount = verts.Count;
                for (int i = 0; i < frontCount; i++)
                {
                    verts.Add(verts[i]);
                    uvs.Add(uvs[i]);
                }

                int stride = segs + 1;
                for (int s = 0; s < segs; s++)
                {
                    int i0 = s, i1 = s + 1, i2 = stride + s, i3 = stride + s + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                    tris.Add(frontCount + i0); tris.Add(frontCount + i1); tris.Add(frontCount + i2);
                    tris.Add(frontCount + i1); tris.Add(frontCount + i3); tris.Add(frontCount + i2);
                }

                var m = new Mesh();
                m.SetVertices(verts);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            });
        }
        #endregion

        #region 4. Mobiliario Ergonómico Esculpido (Sillas, Sillones, Mesas) y Sanitarios Reales (Inodoros y Lavamanos)
        /// <summary>
        /// Cojín de asiento ergonómico esculpido con borde delantero curvo ("waterfall edge") y refuerzos laterales.
        /// </summary>
        public static Mesh GetErgonomicSeatMesh()
        {
            Vector4[] profile = new Vector4[]
            {
                new Vector4(-0.24f, 0.01f, 0.08f, 0.22f), // Borde trasero redondeado
                new Vector4(-0.16f, 0.00f, 0.09f, 0.26f),
                new Vector4( 0.00f, 0.00f, 0.085f, 0.27f), // Centro anatómico
                new Vector4( 0.18f, 0.00f, 0.09f, 0.26f),
                new Vector4( 0.25f, -0.02f, 0.06f, 0.23f)  // Caída frontal curva ("waterfall")
            };
            return CreateContouredBodyMesh("Mesh_Ergonomic_Seat_Cushion", profile, 3);
        }

        /// <summary>
        /// Respaldo anatómico curvo en S con soporte lumbar y cabecero integrado.
        /// </summary>
        public static Mesh GetErgonomicBackrestMesh(bool highBack)
        {
            string key = highBack ? "Mesh_Ergonomic_Backrest_High" : "Mesh_Ergonomic_Backrest_Mid";
            return GetOrCreateMesh(key, () =>
            {
                int rows = highBack ? 14 : 10;
                int cols = 10;
                float totalH = highBack ? 0.88f : 0.50f;
                float baseW = highBack ? 0.52f : 0.46f;
                float thick = 0.075f;

                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();

                Vector3[,] frontGrid = new Vector3[rows + 1, cols + 1];
                Vector3[,] backGrid  = new Vector3[rows + 1, cols + 1];

                for (int r = 0; r <= rows; r++)
                {
                    float v = r / (float)rows;
                    float y = v * totalH;
                    // Curva lumbar en S suave
                    float lumbarZ = Mathf.Sin(v * Mathf.PI * 1.35f) * 0.045f - v * 0.04f;
                    // Estrechamiento suave en hombros y cabecero
                    float wFactor = highBack
                        ? (v < 0.72f ? Mathf.Lerp(0.92f, 1.0f, Mathf.Sin(v * Mathf.PI)) : Mathf.Lerp(0.88f, 0.68f, (v - 0.72f) / 0.28f))
                        : Mathf.Lerp(0.90f, 0.98f, Mathf.Sin(v * Mathf.PI));
                    float halfW = (baseW * 0.5f) * wFactor;

                    for (int c = 0; c <= cols; c++)
                    {
                        float u = c / (float)cols;
                        float x = (u - 0.5f) * 2f * halfW;
                        // Refuerzos laterales envolventes (bolsters)
                        float sideCurveZ = Mathf.Pow(Mathf.Abs(u - 0.5f) * 2f, 2f) * 0.035f;
                        // Cabecero acolchado extra en la parte superior
                        float headrestBump = (highBack && v > 0.76f) ? Mathf.Sin(((v - 0.76f) / 0.24f) * Mathf.PI) * 0.03f : 0f;

                        frontGrid[r, c] = new Vector3(x, y, lumbarZ + sideCurveZ + headrestBump + thick * 0.5f);
                        backGrid[r, c]  = new Vector3(x * 0.95f, y, lumbarZ - thick * 0.5f);
                    }
                }

                Vector3 interiorCenter = new Vector3(0f, totalH * 0.5f, 0f);
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        AddOutwardQuad(verts, uvs, tris, frontGrid[r, c], frontGrid[r + 1, c], frontGrid[r + 1, c + 1], frontGrid[r, c + 1], interiorCenter);
                        AddOutwardQuad(verts, uvs, tris, backGrid[r, c],  backGrid[r, c + 1],  backGrid[r + 1, c + 1], backGrid[r + 1, c],  interiorCenter);
                    }
                    // Costados izquierdo y derecho
                    AddOutwardQuad(verts, uvs, tris, backGrid[r, 0], frontGrid[r, 0], frontGrid[r + 1, 0], backGrid[r + 1, 0], interiorCenter);
                    AddOutwardQuad(verts, uvs, tris, frontGrid[r, cols], backGrid[r, cols], backGrid[r + 1, cols], frontGrid[r + 1, cols], interiorCenter);
                }
                for (int c = 0; c < cols; c++)
                {
                    AddOutwardQuad(verts, uvs, tris, backGrid[rows, c], frontGrid[rows, c], frontGrid[rows, c + 1], backGrid[rows, c + 1], interiorCenter);
                    AddOutwardQuad(verts, uvs, tris, frontGrid[0, c], backGrid[0, c], backGrid[0, c + 1], frontGrid[0, c + 1], interiorCenter);
                }

                var m = new Mesh();
                m.SetVertices(verts);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>
        /// Construye un Sillón Ejecutivo Giratorio de alta gama con base estrella de 5 puntas, 5 ruedas dobles,
        /// pistón neumático, cojín ergonómico esculpido, respaldo anatómico en S con cabecero y apoyabrazos curvos.
        /// </summary>
        public static GameObject BuildSculptedExecutiveChair(Transform parent, Vector3 pos, float yaw, Material upholsteryMat, Material metalMat, Material darkTrimMat)
        {
            var chair = new GameObject("Sculpted_Executive_Swivel_Chair");
            chair.transform.SetParent(parent, false);
            chair.transform.localPosition = pos;
            chair.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // 1. Base estrella de 5 puntas con 5 ruedas dobles giratorias
            CreatePartCylinder("Center_Hub", chair.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.11f, 0.04f, 0.11f), darkTrimMat);
            for (int i = 0; i < 5; i++)
            {
                float ang = i * 72f;
                var armPivot = new GameObject($"Star_Leg_{i}");
                armPivot.transform.SetParent(chair.transform, false);
                armPivot.transform.localRotation = Quaternion.Euler(0f, ang, 0f);

                var leg = CreatePartBox("Leg_Bar", armPivot.transform, new Vector3(0f, 0.075f, 0.17f), new Vector3(0.045f, 0.032f, 0.32f), metalMat);
                leg.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);

                // Rueda doble (caster) en la punta
                var wheel = CreatePartCylinder("Caster_Wheel", armPivot.transform, new Vector3(0f, 0.03f, 0.31f), new Vector3(0.055f, 0.022f, 0.055f), darkTrimMat);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // 2. Pistón neumático cromado y mecanismo basculante inferior
            CreatePartCylinder("Gas_Lift_Cylinder", chair.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.055f, 0.16f, 0.055f), metalMat);
            CreatePartCylinder("Telescopic_Shroud", chair.transform, new Vector3(0f, 0.17f, 0f), new Vector3(0.075f, 0.08f, 0.075f), darkTrimMat);
            CreatePartBox("Tilt_Mechanism_Box", chair.transform, new Vector3(0f, 0.41f, 0f), new Vector3(0.22f, 0.05f, 0.26f), darkTrimMat);

            // 3. Cojín de asiento ergonómico esculpido (sin collider bloqueante para permitir deslizamiento suave de InteractiveChair)
            CreateMeshObject("Ergonomic_Seat", chair.transform, new Vector3(0f, 0.44f, 0.02f), GetErgonomicSeatMesh(), upholsteryMat, addCollider: false);

            // 4. Respaldo alto anatómico con soporte lumbar y cabecero acolchado
            CreateMeshObject("Ergonomic_HighBack", chair.transform, new Vector3(0f, 0.48f, -0.22f), GetErgonomicBackrestMesh(highBack: true), upholsteryMat, addCollider: false);
            // Columna dorsal metálica que une asiento y respaldo
            CreatePartBox("Back_Spine_Bracket", chair.transform, new Vector3(0f, 0.56f, -0.25f), new Vector3(0.08f, 0.32f, 0.03f), metalMat);

            // 5. Apoyabrazos curvos con soportes cromados y almohadillas superiores
            float[] sides = { -0.285f, 0.285f };
            foreach (float sx in sides)
            {
                CreatePartBox("Arm_Support_Front", chair.transform, new Vector3(sx, 0.56f, 0.12f), new Vector3(0.03f, 0.18f, 0.04f), metalMat);
                CreatePartBox("Arm_Support_Rear", chair.transform, new Vector3(sx, 0.56f, -0.14f), new Vector3(0.03f, 0.18f, 0.04f), metalMat);
                CreatePartBox("Arm_Pad_Cushion", chair.transform, new Vector3(sx, 0.66f, -0.01f), new Vector3(0.065f, 0.035f, 0.36f), upholsteryMat);
            }

            var interactive = chair.AddComponent<SimuladorBloqueos.InteractiveChair>();
            interactive.chairLabel = "Sillón Ejecutivo";
            interactive.isWheeled = true;

            return chair;
        }

        /// <summary>
        /// Construye una Silla de Oficina / Visita / Sala de Reuniones con asiento y respaldo anatómicos esculpidos,
        /// estructura tubular metálica con rodachinas y componente InteractiveChair para sentarse con [2] o [Z].
        /// </summary>
        public static GameObject BuildSculptedOfficeVisitorChair(Transform parent, Vector3 pos, float yaw, Material upholsteryMat, Material frameMat)
        {
            var chair = new GameObject("Sculpted_Office_Chair");
            chair.transform.SetParent(parent, false);
            chair.transform.localPosition = pos;
            chair.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Asiento y respaldo esculpidos (sin collider rígido para que ruede al paso del personaje)
            var seat = CreateMeshObject("Ergonomic_Seat", chair.transform, new Vector3(0f, 0.43f, 0.02f), GetErgonomicSeatMesh(), upholsteryMat, addCollider: false);
            seat.transform.localScale = new Vector3(0.92f, 0.90f, 0.92f);

            var back = CreateMeshObject("Ergonomic_MidBack", chair.transform, new Vector3(0f, 0.49f, -0.20f), GetErgonomicBackrestMesh(highBack: false), upholsteryMat, addCollider: false);
            back.transform.localScale = new Vector3(0.92f, 0.92f, 0.92f);

            // Estructura tubular con 4 ruedas giratorias inferiores y apoyabrazos
            float[] lx = { -0.23f, 0.23f };
            foreach (float x in lx)
            {
                // Patas delanteras verticales + rueda
                CreatePartCylinder("Front_Leg_Tube", chair.transform, new Vector3(x, 0.22f, 0.20f), new Vector3(0.032f, 0.20f, 0.032f), frameMat);
                var wF = CreatePartCylinder("Front_Caster_Wheel", chair.transform, new Vector3(x, 0.028f, 0.20f), new Vector3(0.052f, 0.020f, 0.052f), upholsteryMat);
                wF.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                // Patas traseras levemente inclinadas + rueda
                var rLeg = CreatePartCylinder("Rear_Leg_Tube", chair.transform, new Vector3(x, 0.22f, -0.20f), new Vector3(0.032f, 0.20f, 0.032f), frameMat);
                rLeg.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);
                var wR = CreatePartCylinder("Rear_Caster_Wheel", chair.transform, new Vector3(x, 0.028f, -0.20f), new Vector3(0.052f, 0.020f, 0.052f), upholsteryMat);
                wR.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                // Travesaño lateral
                CreatePartBox("Side_Frame_Bar", chair.transform, new Vector3(x, 0.08f, 0.0f), new Vector3(0.028f, 0.025f, 0.40f), frameMat);
                // Apoyabrazos lateral
                CreatePartBox("Arm_Loop_Post", chair.transform, new Vector3(x, 0.53f, 0.14f), new Vector3(0.03f, 0.16f, 0.03f), frameMat);
                CreatePartBox("Armrest_Top", chair.transform, new Vector3(x, 0.61f, -0.02f), new Vector3(0.05f, 0.028f, 0.34f), upholsteryMat);
            }
            // Tubo dorsal que sostiene el respaldo
            CreatePartBox("Back_Support_Spine", chair.transform, new Vector3(0f, 0.56f, -0.23f), new Vector3(0.06f, 0.28f, 0.025f), frameMat);

            var interactive = chair.AddComponent<SimuladorBloqueos.InteractiveChair>();
            interactive.chairLabel = "Silla de Oficina con Ruedas";
            interactive.isWheeled = true;

            return chair;
        }

        /// <summary>
        /// Genera la malla 3D de un Inodoro de Porcelana real con pedestal contorneado, taza ovalada,
        /// cavidad interior cóncava real y anillo de borde.
        /// </summary>
        public static Mesh GetSculptedToiletBowlMesh()
        {
            return GetOrCreateMesh("Mesh_Porcelain_Toilet_Bowl", () =>
            {
                int radialSegs = 20;
                // Perfil vertical exterior desde la base en el piso hasta el borde superior de la taza
                // Vector3(heightY, radiusX, radiusZ) + desplazamiento frontal Z
                float[] heights   = { 0.00f, 0.04f, 0.16f, 0.24f, 0.34f, 0.41f, 0.42f, 0.39f, 0.22f };
                float[] radX      = { 0.14f, 0.13f, 0.11f, 0.15f, 0.20f, 0.21f, 0.16f, 0.15f, 0.07f };
                float[] radZ      = { 0.20f, 0.19f, 0.16f, 0.22f, 0.27f, 0.28f, 0.22f, 0.21f, 0.09f };
                float[] centerZ   = { -0.02f, -0.02f, 0.00f, 0.03f, 0.06f, 0.06f, 0.06f, 0.06f, 0.04f };

                int rings = heights.Length;
                Vector3[,] ringVerts = new Vector3[rings, radialSegs + 1];

                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s <= radialSegs; s++)
                    {
                        float ang = (s / (float)radialSegs) * Mathf.PI * 2f;
                        float x = Mathf.Sin(ang) * radX[r];
                        float z = Mathf.Cos(ang) * radZ[r] + centerZ[r];
                        ringVerts[r, s] = new Vector3(x, heights[r], z);
                    }
                }

                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                Vector3 coreCenter = new Vector3(0f, 0.25f, 0.03f);

                for (int r = 0; r < rings - 1; r++)
                {
                    // Para los anillos internos de la cavidad de la taza (r >= 6), la normal apunta hacia el hueco interior
                    Vector3 refCenter = (r >= 6) ? new Vector3(0f, 0.55f, 0.06f) : coreCenter;
                    for (int s = 0; s < radialSegs; s++)
                    {
                        if (r >= 6)
                        {
                            // Cavidad interior mirando hacia arriba/adentro
                            AddOutwardQuad(verts, uvs, tris,
                                ringVerts[r, s], ringVerts[r, s + 1], ringVerts[r + 1, s + 1], ringVerts[r + 1, s],
                                new Vector3(0f, 0.10f, 0.04f));
                        }
                        else
                        {
                            AddOutwardQuad(verts, uvs, tris,
                                ringVerts[r, s], ringVerts[r + 1, s], ringVerts[r + 1, s + 1], ringVerts[r, s + 1],
                                refCenter);
                        }
                    }
                }

                var m = new Mesh();
                m.SetVertices(verts);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>
        /// Construye un Inodoro de Porcelana Completo y Realista (Taza contorneada con cavidad de agua,
        /// asiento ovalado, tapa levantada, tanque cisterna con tapa biselada, botón cromado de doble descarga y portarrollos).
        /// </summary>
        public static GameObject BuildSculptedPorcelainToilet(Transform parent, Vector3 pos, float yaw, Material porcelainMat, Material chromeMat, Material waterMat)
        {
            var toilet = new GameObject("Realistic_Porcelain_Toilet");
            toilet.transform.SetParent(parent, false);
            toilet.transform.localPosition = pos;
            toilet.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // 1. Taza y pedestal contorneados con cavidad interior
            CreateMeshObject("Porcelain_Bowl_Mesh", toilet.transform, Vector3.zero, GetSculptedToiletBowlMesh(), porcelainMat, addCollider: true);

            // 2. Bloque posterior que une la taza con el tanque cisterna
            CreatePartBox("Rear_Bowl_Bridge", toilet.transform, new Vector3(0f, 0.21f, -0.14f), new Vector3(0.28f, 0.40f, 0.22f), porcelainMat);

            // 3. Espejo de agua dentro del fondo de la taza
            CreatePartCylinder("Bowl_Water_Surface", toilet.transform, new Vector3(0f, 0.24f, 0.05f), new Vector3(0.18f, 0.005f, 0.22f), waterMat);

            // 4. Asiento anatómico blanco sobre el borde de la taza y Tapa ovalada levantada
            CreatePartCylinder("Toilet_Seat_Ring", toilet.transform, new Vector3(0f, 0.425f, 0.06f), new Vector3(0.41f, 0.012f, 0.52f), porcelainMat);
            CreatePartCylinder("Seat_Inner_Hole", toilet.transform, new Vector3(0f, 0.428f, 0.06f), new Vector3(0.26f, 0.013f, 0.34f), waterMat);

            // Tapa ovalada levantada apoyada contra el tanque
            var raisedLid = CreatePartCylinder("Raised_Toilet_Lid", toilet.transform, new Vector3(0f, 0.66f, -0.13f), new Vector3(0.40f, 0.012f, 0.48f), porcelainMat);
            raisedLid.transform.localRotation = Quaternion.Euler(82f, 0f, 0f);

            // Bisagras cromadas del asiento
            CreatePartCylinder("Hinge_L", toilet.transform, new Vector3(-0.10f, 0.43f, -0.14f), new Vector3(0.03f, 0.015f, 0.03f), chromeMat);
            CreatePartCylinder("Hinge_R", toilet.transform, new Vector3( 0.10f, 0.43f, -0.14f), new Vector3(0.03f, 0.015f, 0.03f), chromeMat);

            // 5. Tanque Cisterna de Porcelana con tapa biselada y botón cromado superior
            CreatePartBox("Cistern_Tank_Body", toilet.transform, new Vector3(0f, 0.62f, -0.22f), new Vector3(0.38f, 0.38f, 0.19f), porcelainMat);
            CreatePartBox("Cistern_Tank_Lid", toilet.transform, new Vector3(0f, 0.82f, -0.22f), new Vector3(0.40f, 0.035f, 0.21f), porcelainMat);
            CreatePartCylinder("Dual_Flush_Chrome_Button", toilet.transform, new Vector3(0f, 0.842f, -0.22f), new Vector3(0.055f, 0.01f, 0.055f), chromeMat);
            // Llave angular y tubo de abasto cromado lateral
            CreatePartCylinder("Water_Supply_Valve", toilet.transform, new Vector3(-0.16f, 0.18f, -0.26f), new Vector3(0.025f, 0.16f, 0.025f), chromeMat);

            return toilet;
        }

        /// <summary>
        /// Genera la malla 3D de una taza ovalada de Lavamanos (poza cóncava real de porcelana con reborde superior).
        /// </summary>
        public static Mesh GetSculptedWashbasinBowlMesh()
        {
            return GetOrCreateMesh("Mesh_Porcelain_Washbasin_Bowl", () =>
            {
                int radialSegs = 22;
                // Taza semi-encastrada con pared exterior curva y poza interior cóncava profunda sobre la encimera
                float[] heights = { 0.00f, 0.05f, 0.105f, 0.105f, 0.055f, 0.022f, 0.014f };
                float[] radX    = { 0.22f, 0.26f, 0.26f,  0.22f,  0.18f,  0.13f,  0.03f  };
                float[] radZ    = { 0.16f, 0.20f, 0.20f,  0.16f,  0.13f,  0.09f,  0.02f  };

                int rings = heights.Length;
                Vector3[,] ringVerts = new Vector3[rings, radialSegs + 1];
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s <= radialSegs; s++)
                    {
                        float ang = (s / (float)radialSegs) * Mathf.PI * 2f;
                        ringVerts[r, s] = new Vector3(Mathf.Sin(ang) * radX[r], heights[r], Mathf.Cos(ang) * radZ[r]);
                    }
                }

                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();

                for (int r = 0; r < rings - 1; r++)
                {
                    Vector3 refCenter = (r < 2) ? new Vector3(0f, 0.05f, 0f) : new Vector3(0f, -0.35f, 0f);
                    for (int s = 0; s < radialSegs; s++)
                    {
                        AddOutwardQuad(verts, uvs, tris,
                            ringVerts[r, s], ringVerts[r, s + 1], ringVerts[r + 1, s + 1], ringVerts[r + 1, s],
                            refCenter);
                    }
                }

                var m = new Mesh();
                m.SetVertices(verts);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>
        /// Construye un Lavamanos Individual de Porcelana con poza cóncava real, desagüe cromado,
        /// grifo curvo tipo cuello de cisne y llaves mezcladoras cromadas.
        /// </summary>
        public static GameObject BuildSculptedWashbasinUnit(Transform parent, Vector3 localPos, Material porcelainMat, Material chromeMat, Material drainDarkMat)
        {
            var sink = new GameObject("Sculpted_Porcelain_Washbasin");
            sink.transform.SetParent(parent, false);
            sink.transform.localPosition = localPos;

            // Poza cóncava esculpida sobre la encimera
            CreateMeshObject("Basin_Bowl", sink.transform, Vector3.zero, GetSculptedWashbasinBowlMesh(), porcelainMat, addCollider: false);
            // Desagüe cromado en el fondo cóncavo de la poza
            CreatePartCylinder("Chrome_Drain_Ring", sink.transform, new Vector3(0f, 0.018f, 0f), new Vector3(0.055f, 0.004f, 0.055f), chromeMat);
            CreatePartCylinder("Drain_Hole", sink.transform, new Vector3(0f, 0.020f, 0f), new Vector3(0.035f, 0.005f, 0.035f), drainDarkMat);

            // Grifería cromada: Base mezcladora, caño alto curvo y manijas de agua fría/caliente
            CreatePartBox("Faucet_Mixer_Base", sink.transform, new Vector3(0f, 0.035f, -0.22f), new Vector3(0.14f, 0.025f, 0.045f), chromeMat);
            CreatePartCylinder("Faucet_Riser_Stem", sink.transform, new Vector3(0f, 0.13f, -0.22f), new Vector3(0.024f, 0.095f, 0.024f), chromeMat);
            var spout = CreatePartCylinder("Faucet_Curved_Spout", sink.transform, new Vector3(0f, 0.21f, -0.15f), new Vector3(0.022f, 0.075f, 0.022f), chromeMat);
            spout.transform.localRotation = Quaternion.Euler(68f, 0f, 0f);
            CreatePartCylinder("Faucet_Aerator_Tip", sink.transform, new Vector3(0f, 0.175f, -0.085f), new Vector3(0.024f, 0.018f, 0.024f), chromeMat);

            // Llaves laterales cromadas
            CreatePartCylinder("Tap_Knob_Cold", sink.transform, new Vector3(-0.05f, 0.055f, -0.22f), new Vector3(0.028f, 0.02f, 0.028f), chromeMat);
            CreatePartCylinder("Tap_Knob_Hot",  sink.transform, new Vector3( 0.05f, 0.055f, -0.22f), new Vector3(0.028f, 0.02f, 0.028f), chromeMat);

            return sink;
        }

        private static GameObject CreatePartCylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        #endregion

        #region Helpers Internos
        public static GameObject CreateMeshObject(string name, Transform parent, Vector3 localPos, Mesh mesh, Material mat, bool addCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (addCollider)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = mesh.bounds.center;
                bc.size = mesh.bounds.size;
            }
            return go;
        }

        private static GameObject CreatePartBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        #endregion
    }
}
#endif
