#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SimuladorBloqueos.Editor
{
    public static class TMPImporterHelper
    {
        [MenuItem("Simulador/Importar Recursos TMP")]
        public static void ImportTMPEssentials()
        {
            // Importar el paquete TMP Essential Resources oficial desde com.unity.ugui
            string packagePath = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
            AssetDatabase.ImportPackage(packagePath, false);
            Debug.Log("[TMPImporterHelper] TMP Essential Resources importado correctamente.");
        }
    }
}
#endif
