using UnityEngine;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Inicializador del entorno de navegación física y colisiones de mobiliario/muros
    /// para el recinto UTOP y la Calle Junín.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        public static bool Ready { get; private set; }

        private void Awake()
        {
            Ready = true;
        }

        private void OnDestroy()
        {
            Ready = false;
        }
    }
}
