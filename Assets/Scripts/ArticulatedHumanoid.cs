using UnityEngine;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Contiene todas las referencias a los huesos/articulaciones del personaje humanoide articulado.
    /// </summary>
    public class ArticulatedHumanoid : MonoBehaviour
    {
        [Header("Pelvis y Columna")]
        public Transform hips;           // Caderas / Pelvis (raíz del cuerpo)
        public Transform waist;          // Cintura / Columna inferior
        public Transform chest;          // Torso / Pecho / Columna superior

        [Header("Cabeza y Cuello")]
        public Transform neck;           // Cuello
        public Transform head;           // Cabeza
        public Transform visor;          // Ojos / Visor

        [Header("Brazo Izquierdo")]
        public Transform leftShoulder;   // Hombro izquierdo (clavícula)
        public Transform leftUpperArm;   // Brazo superior izquierdo
        public Transform leftForearm;    // Antebrazo / Codo izquierdo
        public Transform leftWrist;      // Muñeca izquierda
        public Transform leftHand;       // Mano izquierda
        public Transform leftThumb;      // Dedo pulgar izquierdo
        public Transform[] leftFingers;  // Dedos izquierdos (índice, medio, anular)

        [Header("Brazo Derecho")]
        public Transform rightShoulder;  // Hombro derecho (clavícula)
        public Transform rightUpperArm;  // Brazo superior derecho
        public Transform rightForearm;   // Antebrazo / Codo derecho
        public Transform rightWrist;     // Muñeca derecha
        public Transform rightHand;      // Mano derecha
        public Transform rightThumb;     // Dedo pulgar derecho
        public Transform[] rightFingers; // Dedos derechos (índice, medio, anular)

        [Header("Pierna Izquierda")]
        public Transform leftThigh;      // Muslo / Cadera izquierda
        public Transform leftCalf;       // Pantorrilla / Rodilla izquierda
        public Transform leftFoot;       // Tobillo / Pie izquierdo
        public Transform leftToe;        // Punta del pie izquierdo

        [Header("Pierna Derecha")]
        public Transform rightThigh;     // Muslo / Cadera derecha
        public Transform rightCalf;      // Pantorrilla / Rodilla derecha
        public Transform rightFoot;      // Tobillo / Pie derecho
        public Transform rightToe;       // Punta del pie derecho

        [Header("Materiales")]
        public Renderer[] bodyRenderers;
        public Renderer[] jointRenderers;
    }
}
