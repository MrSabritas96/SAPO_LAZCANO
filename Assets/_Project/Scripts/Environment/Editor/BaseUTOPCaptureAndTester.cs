#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimuladorBloqueos.EditorTools
{
    public static class BaseUTOPCaptureAndTester
    {
        private const string ScreenshotDir = "Assets/Screenshots";

        [MenuItem("Simulador/Capturar Vistas Base UTOP")]
        public static void CaptureAllViews()
        {
            CalleJuninSceneBuilder.BuildCalleJuninScene();

            if (!Directory.Exists(ScreenshotDir))
            {
                Directory.CreateDirectory(ScreenshotDir);
            }

            if (EditorSceneManager.GetActiveScene().path != "Assets/_Project/Scenes/BaseUTOP/BaseUTOP.unity")
            {
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/BaseUTOP/BaseUTOP.unity");
            }

            // Ocultar temporalmente la cabeza del jugador para las vistas en 1ra persona
            var player = GameObject.Find("Player_Oficial");
            Renderer[] headRenderers = null;
            if (player != null)
            {
                var anim = player.GetComponent<SimuladorBloqueos.ProceduralHumanoidAnimator>();
                if (anim != null && anim.humanoid != null && anim.humanoid.head != null)
                {
                    headRenderers = anim.humanoid.head.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in headRenderers) r.enabled = false;
                }
            }

            float f1Y = UTOPInteriorAndCompoundBuilder.FirstFloorY;
            float gY  = UTOPInteriorAndCompoundBuilder.GarageFloorY;

            // 1. Oficina del Capitán de la UTOP en el Primer Piso (Réplica exacta de Imagen 2: Capitán sentado, escritorio, cuadro y banderas)
            CaptureView(
                "utop_oficina_capitan.png",
                new Vector3(10.0f, f1Y + 1.48f, 5.75f),
                Quaternion.Euler(6f, 0f, 0f),
                50f
            );

            // 2. Primer Piso: Pasillo Central, Descanso Amplio de Gradas, Barandas Continuas y Puertas Interactivas
            CaptureView(
                "utop_primer_piso_mamparas_y_gradas.png",
                new Vector3(11.4f, f1Y + 1.85f, 1.8f),
                Quaternion.Euler(18f, 118f, 0f),
                66f
            );

            // 2B. Baños Policiales Reales: Paredes Sólidas Opacas, Suelo de Azulejo Cerámico, Lavamanos con Espejo y Cubículos con Puertas e Inodoros Esculpidos
            CaptureView(
                "utop_banos_azulejos_e_inodoros.png",
                new Vector3(20.62f, f1Y + 1.62f, 5.15f),
                Quaternion.Euler(16f, 48f, 0f),
                72f
            );

            // 2C. Sala de Operaciones y Táctica: Mesa Biselada y 6 Sillas Ergonómicas Esculpidas
            CaptureView(
                "utop_sala_operaciones_sillas.png",
                new Vector3(16.9f, f1Y + 1.75f, 3.9f),
                Quaternion.Euler(14f, 0f, 0f),
                64f
            );

            // 3. Cuarto de Armamento Antidisturbios / Armería UTOP en Planta Baja (Réplica de Imagen 3: Escudo dorado, escopetas, lanzagás, escudos y cascos)
            CaptureView(
                "utop_armeria_antidisturbios.png",
                new Vector3(13.8f, gY + 1.65f, 7.65f),
                Quaternion.Euler(2f, 92f, 0f),
                62f
            );

            // 4. Patio de Honor Techado y Abierto (Réplica de Imagen 5: Escudo en el piso, letras doradas - UTOP -, mástil con bandera de Bolivia y pabellones verdes)
            CaptureView(
                "utop_patio_de_honor.png",
                new Vector3(13.0f, gY + 1.75f, -4.2f),
                Quaternion.Euler(2f, 180f, 0f),
                62f
            );

            // 5. Gran Cuadra de Tropa con Literas Dobles y Roperos Individuales (De donde saldrá el contingente UTOP)
            CaptureView(
                "utop_cuadra_literas_y_roperos.png",
                new Vector3(20.6f, gY + 1.75f, -22.4f),
                Quaternion.Euler(4f, 90f, 0f),
                64f
            );

            // 6. Calle Junín con Autos Paceños Esculpidos (Toyota Caldina Wagon, VW Peta, Minibús HiAce) estacionados a un costado y Entrada al Garaje Plano
            CaptureView(
                "junin_autos_laterales_y_garaje.png",
                new Vector3(-1.2f, CalleJuninSceneBuilder.GetRoadY(6f) + 2.1f, 6f),
                Quaternion.Euler(4f, 22f, 0f),
                60f
            );

            AssetDatabase.Refresh();
            Debug.Log("<color=green><b>[CalleJunin] 6 capturas del interior funcional UTOP y Calle Junín generadas en Assets/Screenshots/</b></color>");
        }

        private static void CaptureView(string filename, Vector3 pos, Quaternion rot, float fov)
        {
            var tempCamGo = new GameObject("Temp_Capture_Cam");
            var cam = tempCamGo.AddComponent<Camera>();
            tempCamGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            tempCamGo.transform.position = pos;
            tempCamGo.transform.rotation = rot;

            var rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            string fullPath = Path.Combine(ScreenshotDir, filename);
            File.WriteAllBytes(fullPath, bytes);

            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(tempCamGo);

            Debug.Log($"[Screenshot] Guardada vista: {filename}");
        }
    }
}
#endif
