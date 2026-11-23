using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimuladorBloqueos
{
    /// <summary>
    /// Gestor de la tropa UTOP: formación en el Patio de Honor (3 filas), contingente de N efectivos
    /// (decisión 2), puestos de distribución (decisión 3) y saludo sincronizado.
    /// </summary>
    public class UTOPTroopFormationManager : MonoBehaviour
    {
        public static UTOPTroopFormationManager Instance { get; private set; }

        [Header("Punto de Mando (Emblema UTOP en Patio de Honor)")]
        public Vector3 emblemCenter = new Vector3(13.0f, -0.30f, -10.5f);
        public float emblemActivationRadius = 3.25f;

        [Header("Bloque de formación (centro del patio, mirando al emblema)")]
        public Vector3 formationFrontCenter = new Vector3(13.0f, -0.30f, -15.2f);
        public float columnSpacing = 0.88f;
        public float rowSpacing = 1.15f;
        public int rows = 3;

        [Header("Estado")]
        public bool isTroopFormed = false;
        public int contingentSize = 30;
        public List<UTOPTroopNPCController> troopOfficers = new List<UTOPTroopNPCController>();

        // Propiedades de compatibilidad con ScenarioDirector y constructores de escena
        public List<UTOPTroopNPCController> troops => troopOfficers;
        public Vector3 patioFormationCenter { get => formationFrontCenter; set => formationFrontCenter = value; }
        public float colSpacing { get => columnSpacing; set => columnSpacing = value; }
        public bool isFormedInPatio { get => isTroopFormed; set => isTroopFormed = value; }
        public void OnPlayerSalutedNearPatio() => OnPlayerSaluted();

        private Transform _playerTransform;
        private bool _playerOnEmblem;
        private static Texture2D _bgTex;

        private void Awake() { Instance = this; }

        private void Start()
        {
            if (troopOfficers == null || troopOfficers.Count == 0)
                troopOfficers = new List<UTOPTroopNPCController>(FindObjectsByType<UTOPTroopNPCController>());
            troopOfficers.RemoveAll(t => t == null);
            troopOfficers.Sort((a, b) => a.troopIndex.CompareTo(b.troopIndex));
        }

        /// <summary>Posición de la casilla i dentro de un bloque de 'total' efectivos (filas × columnas centradas).</summary>
        public void GetSlot(int i, int total, out Vector3 pos, out Quaternion rot)
        {
            int cols = Mathf.Max(1, Mathf.CeilToInt(total / (float)rows));
            int r = i / cols, c = i % cols;
            float x = formationFrontCenter.x + (c - (cols - 1) * 0.5f) * columnSpacing;
            float z = formationFrontCenter.z - r * rowSpacing;
            pos = new Vector3(x, formationFrontCenter.y, z);
            rot = Quaternion.Euler(0f, 0f, 0f); // mirando a +Z, hacia el emblema/jugador
        }

        public List<UTOPTroopNPCController> GetContingent()
        {
            var list = new List<UTOPTroopNPCController>();
            for (int i = 0; i < troopOfficers.Count && i < contingentSize; i++) if (troopOfficers[i] != null) list.Add(troopOfficers[i]);
            return list;
        }

        public void SetContingentSize(int n)
        {
            contingentSize = Mathf.Clamp(n, 1, troopOfficers.Count);
            for (int i = 0; i < troopOfficers.Count; i++) troopOfficers[i].inContingent = i < contingentSize;
        }

        public void FormContingent()
        {
            isTroopFormed = true;
            var list = GetContingent();
            for (int i = 0; i < list.Count; i++)
            {
                GetSlot(i, list.Count, out Vector3 p, out Quaternion q);
                list[i].formationRow = i / Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)rows));
                list[i].CommandMoveToFormation(p, q);
            }
        }

        public void BreakFormation()
        {
            isTroopFormed = false;
            foreach (var t in GetContingent()) t.CommandBreakFormation();
        }

        public bool AllContingentAtRest()
        {
            foreach (var t in GetContingent()) if (t != null && !t.IsAtRest) return false;
            return true;
        }

        public void ToggleFormation()
        {
            if (!isTroopFormed)
            {
                if (_playerTransform != null) _playerTransform.rotation = Quaternion.Euler(0f, 180f, 0f);
                FormContingent();
            }
            else BreakFormation();
        }

        public void OnPlayerSaluted()
        {
            if (!isTroopFormed) return;
            foreach (var t in GetContingent()) t.CommandSaluteInUnison();
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) _playerTransform = pc.transform;
            }
            Vector3 playerPos = _playerTransform != null ? _playerTransform.position : emblemCenter;
            Vector3 diff = playerPos - emblemCenter;
            _playerOnEmblem = Mathf.Abs(diff.y) < 1.6f && new Vector2(diff.x, diff.z).sqrMagnitude <= emblemActivationRadius * emblemActivationRadius;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            bool uiOpen = SimuladorBloqueos.Decisions.DecisionManager.IsAnyDecisionUIOpen;
            if (!uiOpen && kb != null && _playerOnEmblem && (kb.bKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                ToggleFormation();
#endif
            float dt = Time.deltaTime;
            bool near = playerPos.z < -4.0f && playerPos.x > 1.5f;
            for (int i = 0; i < troopOfficers.Count; i++)
                if (troopOfficers[i] != null) troopOfficers[i].ManagedUpdate(dt, playerPos, near);
        }

        private void OnGUI()
        {
            if (!_playerOnEmblem && !isTroopFormed) return;
            if (!_playerOnEmblem) return;
            if (_bgTex == null) { _bgTex = new Texture2D(1, 1); _bgTex.SetPixel(0, 0, Color.white); _bgTex.Apply(); }
            float w = 640f, h = isTroopFormed ? 58f : 42f;
            float x = (Screen.width - w) * 0.5f, y = Screen.height - 114f;
            GUI.color = new Color(0.04f, 0.09f, 0.07f, 0.92f); GUI.DrawTexture(new Rect(x, y, w, h), _bgTex);
            GUI.color = new Color(0.24f, 0.85f, 0.45f, 1f); GUI.DrawTexture(new Rect(x, y, 5f, h), _bgTex);
            var main = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.48f, 0.98f, 0.65f) } };
            var sub = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.95f, 0.95f, 0.85f) } };
            GUI.color = Color.white;
            if (!isTroopFormed)
                GUI.Label(new Rect(x + 10, y + 8, w - 20, 26), "EMBLEMA DE MANDO UTOP — Pulsa [B] o [Enter] para FORMAR A LA TROPA (" + contingentSize + " Policías: 3 filas)", main);
            else
            {
                GUI.Label(new Rect(x + 10, y + 6, w - 20, 22), "TROPA EN FORMACIÓN (FIRMES) — Pulsa [1] o [G] para SALUDO SINCRONIZADO", main);
                GUI.Label(new Rect(x + 10, y + 29, w - 20, 22), "Pulsa [B] o [Enter] para ROMPER FILAS", sub);
            }
        }
    }
}
