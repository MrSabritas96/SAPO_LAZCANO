#if UNITY_EDITOR
using UnityEditor;

namespace SimuladorBloqueos.EditorTools
{
    /// <summary>
    /// Redirige la construcción de Base UTOP hacia el reconstructor real de Calle Junín.
    /// </summary>
    public static class BaseUTOPSceneBuilder
    {
        [MenuItem("Simulador/Construir Base UTOP Completa")]
        public static void BuildBaseUTOPScene()
        {
            CalleJuninSceneBuilder.BuildCalleJuninScene();
        }
    }
}
#endif
