using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SimuladorBloqueos.Environment
{
    /// <summary>
    /// Controlador de puertas interactivas con bisagra real para oficinas, baños, cubículos sanitarios y dependencias UTOP.
    /// Permite abrir y cerrar las puertas suavemente presionando la tecla [E] al acercarse.
    /// </summary>
    public class InteractiveDoor : MonoBehaviour
    {
        private static readonly List<InteractiveDoor> AllDoors = new List<InteractiveDoor>();
        private static InteractiveDoor _closestActiveDoor;
        private static Transform _cachedPlayer;
        private static GUIStyle _promptStyle;
        private static Texture2D _bgTex;

        [Header("Configuración de Bisagra")]
        public string doorLabel = "Puerta";
        public float closedLocalYaw = 0f;
        public float openLocalYaw = 88f;
        public float openSpeed = 220f;
        public float interactionDistance = 2.35f;
        public bool isOpen = false;

        private float _currentYaw;

        private void Awake()
        {
            _currentYaw = isOpen ? openLocalYaw : closedLocalYaw;
            transform.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);
        }

        private void OnEnable()
        {
            if (!AllDoors.Contains(this))
                AllDoors.Add(this);
        }

        private void OnDisable()
        {
            AllDoors.Remove(this);
            if (_closestActiveDoor == this)
                _closestActiveDoor = null;
        }

        private void Update()
        {
            // Animación suave de rotación sobre el eje de bisagra
            float targetYaw = isOpen ? openLocalYaw : closedLocalYaw;
            if (Mathf.Abs(Mathf.DeltaAngle(_currentYaw, targetYaw)) > 0.05f)
            {
                _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, targetYaw, openSpeed * Time.deltaTime);
                transform.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);
            }

            // Solo la primera puerta de la lista evalúa cuál es la más cercana al jugador en este frame
            if (AllDoors.Count > 0 && AllDoors[0] == this)
            {
                UpdateClosestDoorAndInput();
            }
        }

        private static void UpdateClosestDoorAndInput()
        {
            if (_cachedPlayer == null)
            {
                var pc = Object.FindFirstObjectByType<PlayerController>();
                if (pc != null) _cachedPlayer = pc.transform;
                else if (Camera.main != null) _cachedPlayer = Camera.main.transform;
            }

            if (_cachedPlayer == null) return;

            InteractiveDoor best = null;
            float bestDistSq = float.MaxValue;
            Vector3 pPos = _cachedPlayer.position + Vector3.up * 0.9f;

            for (int i = 0; i < AllDoors.Count; i++)
            {
                var d = AllDoors[i];
                if (d == null) continue;
                Vector3 doorPos = d.transform.position + Vector3.up * 0.9f;
                float distSq = (pPos - doorPos).sqrMagnitude;
                float maxSq = d.interactionDistance * d.interactionDistance;
                if (distSq <= maxSq && distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = d;
                }
            }

            _closestActiveDoor = best;

            if (_closestActiveDoor != null && Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame)
                {
                    _closestActiveDoor.ToggleDoor();
                }
            }
        }

        public void ToggleDoor()
        {
            isOpen = !isOpen;
        }

        private void OnGUI()
        {
            if (_closestActiveDoor != this) return;

            if (_promptStyle == null)
            {
                _bgTex = new Texture2D(1, 1);
                _bgTex.SetPixel(0, 0, new Color(0.06f, 0.10f, 0.08f, 0.88f));
                _bgTex.Apply();

                _promptStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.95f, 0.88f, 0.45f), background = _bgTex }
                };
            }

            string action = isOpen ? "Cerrar" : "Abrir";
            string msg = $"Presiona [E] para {action} — {doorLabel}";
            float w = 380f;
            float h = 36f;
            GUI.Box(new Rect((Screen.width - w) * 0.5f, Screen.height - 95f, w, h), msg, _promptStyle);
        }
    }
}
