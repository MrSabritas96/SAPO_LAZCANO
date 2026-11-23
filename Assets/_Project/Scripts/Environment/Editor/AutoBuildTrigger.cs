#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimuladorBloqueos.EditorTools
{
    [InitializeOnLoad]
    public static class AutoBuildTrigger
    {
        private const string TriggerPath = "Temp/run_build.txt";
        private const string DonePath = "Temp/build_done.txt";

        static AutoBuildTrigger()
        {
            EditorApplication.delayCall += CheckAndRun;
            EditorApplication.update += CheckAndRun;
        }

        public static void CheckAndRun()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (File.Exists(TriggerPath))
            {
                try
                {
                    File.Delete(TriggerPath);
                    Debug.Log("<color=yellow>[AutoBuildTrigger] Trigger detectado (v2). Ejecutando reconstrucción detallada de Calle Junín...</color>");

                    CalleJuninSceneBuilder.BuildCalleJuninScene();
                    BaseUTOPCaptureAndTester.CaptureAllViews();

                    File.WriteAllText(DonePath, "SUCCESS_V2\n" + System.DateTime.Now.ToString("O"));
                    Debug.Log("<color=green>[AutoBuildTrigger] Reconstrucción V2 y capturas finalizadas con éxito.</color>");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[AutoBuildTrigger] Error durante la construcción: " + ex);
                    File.WriteAllText(DonePath, "ERROR: " + ex.ToString());
                }
            }
        }
    }
}
#endif
