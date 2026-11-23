using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using SimuladorBloqueos.Decisions;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Sistema de Inventario Táctico de 8 Ítems para el Oficial UTOP (Jugador), Recolección en la Sala de Armas,
    /// Posturas de 1 y 2 manos, Escudo con 2 estados (Reposo / Defensa al frente), Gas en Spray con porcentaje (%),
    /// Lanzagases con proyectiles contados (BALAGASLACRI) y recarga, Escopeta con gravedad, y Colocación estilo Fortnite
    /// para Barricada (BARRICADA) y Barrera Grande (BARRERAGRANDE).
    /// </summary>
    public class PlayerInventorySystem : MonoBehaviour
    {
        public static PlayerInventorySystem Instance { get; private set; }

        public enum TacticalItemType
        {
            None = 0,
            Escudo = 1,          // 1: ESCUDO.fbx (2 estados: en reposo abajo en una mano / defensa al frente)
            Tonfa = 2,           // 2: TONFA.fbx (en mano derecha, golpe de bastón)
            GasLacriSprite = 3,  // 3: GASLACRISPRITE.fbx (levanta la mano para rociar gas + % restante)
            LanzaGasLacri = 4,   // 4: LANZAGASLACRI (2 manos, gravedad parabólica, usa BALAGASLACRI y recarga [R])
            BalaGasLacri = 5,    // 5: BALAGASLACRI.fbx (munición contada, ej. 10 granadas de 37mm)
            Escopeta = 6,        // 6: ESCOPETA (2 manos, perdigones con gravedad y recarga [R])
            Barricada = 7,       // 7: BARRICADA.fbx (previsualización holográfica estilo Fortnite para colocar en calle)
            BarreraGrande = 8    // 8: BARRERAGRANDE.fbx (previsualización holográfica estilo Fortnite para colocar en calle)
        }

        [Header("Plantillas 3D (Asignadas desde ScenarioDirector / Armería)")]
        public GameObject shieldTemplate;
        public GameObject tonfaTemplate;
        public GameObject gasSprayTemplate;
        public GameObject gasLauncherTemplate;
        public GameObject gasAmmoTemplate;
        public GameObject shotgunTemplate;
        public GameObject barricadaTemplate;
        public GameObject barreraGrandeTemplate;

        [Header("Estado del Inventario (8 Ranuras)")]
        public bool hasPickedUpArmoryKit = false;
        public bool[] unlockedSlots = new bool[9]; // Índices 1..8
        public TacticalItemType currentItem = TacticalItemType.None;

        [Header("Contadores de Munición, Gas y Barreras")]
        public bool shieldDefensiveFront = false; // false = en reposo abajo, true = escudo al frente protegiéndose
        [Range(0f, 100f)]
        public float gasSprayPercentage = 100f;   // 100% de gas pimienta/lacrimógeno de mano
        public int launcherChamberLoaded = 1;     // 1 granada en recámara del LANZAGASLACRI
        public int balaGasLacriCount = 10;        // 10 proyectiles BALAGASLACRI contados
        public int shotgunLoadedShells = 6;       // 6 cartuchos en tubo
        public int shotgunReserveShells = 18;     // 18 cartuchos de reserva
        public int barricadaCount = 4;            // Barricadas disponibles para colocar
        public int barreraGrandeCount = 4;        // Barreras grandes disponibles para colocar

        [Header("Estado de Acciones de Arma")]
        public bool isReloading = false;
        public bool isSprayingHandGas = false;
        private float _reloadTimer = 0f;
        private float _fireCooldown = 0f;
        private float _tonfaSwingTimer = 0f;
        private float _placementYawOffset = 0f;

        // Huesos del esqueleto del jugador para sostener armas con 1 o 2 manos
        private Transform _hips, _spine1, _spine2;
        private Transform _lShoulder, _lArm, _lForeArm, _lHand;
        private Transform _rShoulder, _rArm, _rForeArm, _rHand;

        // Instancias 3D adjuntas a las manos del jugador
        private GameObject _equippedShieldGO;
        private GameObject _equippedTonfaGO;
        private GameObject _equippedGasSprayGO;
        private GameObject _equippedLauncherGO;
        private GameObject _equippedAmmoGO;
        private GameObject _equippedShotgunGO;

        // Previsualización holográfica estilo Fortnite para BARRICADA y BARRERAGRANDE
        private GameObject _previewBarricadaGO;
        private GameObject _previewBarreraGrandeGO;
        private Material _hologramMat;
        private Vector3 _currentPlacementPos;
        private Quaternion _currentPlacementRot;
        private bool _validPlacementPose;

        // Efecto visual de gas en spray en mano
        private GameObject _handSprayEffectGO;
        private float _sprayDamageTickTimer = 0f;

        private PlayerController _playerController;
        private UTOPCharacterAnimationController _utopAnim;
        private static Texture2D _whiteTex;

        public bool IsShieldProtecting => currentItem == TacticalItemType.Escudo && shieldDefensiveFront;

        private void Awake()
        {
            Instance = this;
            _playerController = GetComponent<PlayerController>();
            _utopAnim = GetComponent<UTOPCharacterAnimationController>();
            FindPlayerBones();
        }

        private void Start()
        {
            SyncTemplatesFromScene();
            CreateHologramMaterial();
        }

        private void FindPlayerBones()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Hips": if (_hips == null) _hips = t; break;
                    case "Spine1": if (_spine1 == null) _spine1 = t; break;
                    case "Spine2": if (_spine2 == null) _spine2 = t; break;
                    case "LeftShoulder": if (_lShoulder == null) _lShoulder = t; break;
                    case "LeftArm": if (_lArm == null) _lArm = t; break;
                    case "LeftForeArm": if (_lForeArm == null) _lForeArm = t; break;
                    case "LeftHand": if (_lHand == null) _lHand = t; break;
                    case "RightShoulder": if (_rShoulder == null) _rShoulder = t; break;
                    case "RightArm": if (_rArm == null) _rArm = t; break;
                    case "RightForeArm": if (_rForeArm == null) _rForeArm = t; break;
                    case "RightHand": if (_rHand == null) _rHand = t; break;
                }
            }
        }

        public void SyncTemplatesFromScene()
        {
            var dir = FindAnyObjectByType<ScenarioDirector>();
            if (dir != null)
            {
                if (shieldTemplate == null) shieldTemplate = dir.shieldTemplate;
                if (tonfaTemplate == null) tonfaTemplate = dir.tonfaTemplate;
                if (gasSprayTemplate == null) gasSprayTemplate = dir.gasSprayTemplate;
                if (gasLauncherTemplate == null) gasLauncherTemplate = dir.gasLauncherTemplate;
                if (shotgunTemplate == null) shotgunTemplate = dir.shotgunTemplate;
            }

            if (shieldTemplate == null) shieldTemplate = GameObject.Find("Armory_3D_ESCUDO_1");
            if (tonfaTemplate == null) tonfaTemplate = GameObject.Find("Armory_3D_TONFA_1");
            if (gasSprayTemplate == null) gasSprayTemplate = GameObject.Find("Armory_3D_GASLACRISPRITE_1");
            if (gasLauncherTemplate == null) gasLauncherTemplate = GameObject.Find("Armory_3D_LANZAGAS_2");
            if (gasAmmoTemplate == null) gasAmmoTemplate = GameObject.Find("Armory_3D_BALAGASLACRI_1");
            if (shotgunTemplate == null) shotgunTemplate = GameObject.Find("Armory_3D_ESCOPETA_1");
            if (barricadaTemplate == null) barricadaTemplate = GameObject.Find("Street_3D_BARRICADA_1");
            if (barreraGrandeTemplate == null) barreraGrandeTemplate = GameObject.Find("Compound_3D_BARRERAGRANDE_Side");
        }

        private void CreateHologramMaterial()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _hologramMat = new Material(urpLit) { name = "Mat_Fortnite_Placement_Hologram" };
            Color holoCol = new Color(0.15f, 0.85f, 1.0f, 0.48f);
            _hologramMat.color = holoCol;
            if (_hologramMat.HasProperty("_BaseColor")) _hologramMat.SetColor("_BaseColor", holoCol);
            if (_hologramMat.HasProperty("_Surface"))
            {
                _hologramMat.SetFloat("_Surface", 1f);
                _hologramMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _hologramMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _hologramMat.SetInt("_ZWrite", 0);
                _hologramMat.renderQueue = 3050;
            }
        }

        public bool IsPlayerInsideOrNearArmory()
        {
            Vector3 p = transform.position;
            // Sala de Armas / Armería en Planta Baja: X = 8.5..18.8, Z = 4.6..9.9, Y cerca de -0.30
            return Mathf.Abs(p.y - (-0.30f)) < 1.8f && p.x >= 8.2f && p.x <= 18.8f && p.z >= 4.5f && p.z <= 9.9f;
        }

        public void PickupFullArmoryInventory()
        {
            SyncTemplatesFromScene();
            hasPickedUpArmoryKit = true;
            for (int i = 1; i <= 8; i++) unlockedSlots[i] = true;

            gasSprayPercentage = 100f;
            launcherChamberLoaded = 1;
            balaGasLacriCount = 10;
            shotgunLoadedShells = 6;
            shotgunReserveShells = 18;
            barricadaCount = 4;
            barreraGrandeCount = 4;

            EnsureHandPropsInstantiated();

            if (currentItem == TacticalItemType.None)
            {
                EquipSlot(TacticalItemType.Escudo);
            }
            else
            {
                EquipSlot(currentItem);
            }

            var dir = FindAnyObjectByType<ScenarioDirector>();
            if (dir != null)
            {
                dir.ShowWorldBanner("INVENTARIO TÁCTICO COMPLETO (8 ÍTEMS): Escudo, Tonfa, Gas Spray (100%), Lanzagases, 10 BALAGASLACRI, Escopeta, Barricada y Barrera Grande.", 7f);
            }
        }

        private void EnsureHandPropsInstantiated()
        {
            FindPlayerBones();
            SyncTemplatesFromScene();

            if (_equippedShieldGO == null && shieldTemplate != null && _lForeArm != null)
            {
                _equippedShieldGO = InstantiateCleanProp(shieldTemplate, "Player_Held_ESCUDO", _lForeArm,
                    new Vector3(0.06f, 0.14f, 0.12f), new Vector3(0f, 90f, 0f), 0.82f);
            }
            if (_equippedTonfaGO == null && tonfaTemplate != null && _rHand != null)
            {
                _equippedTonfaGO = InstantiateCleanProp(tonfaTemplate, "Player_Held_TONFA", _rHand,
                    new Vector3(0.03f, 0.06f, 0.06f), new Vector3(90f, 0f, 0f), 0.90f);
            }
            if (_equippedGasSprayGO == null && gasSprayTemplate != null && _rHand != null)
            {
                _equippedGasSprayGO = InstantiateCleanProp(gasSprayTemplate, "Player_Held_GASLACRISPRITE", _rHand,
                    new Vector3(0.03f, 0.08f, 0.04f), new Vector3(0f, 0f, -90f), 1.05f);
                CreateHandSprayParticles(_equippedGasSprayGO.transform);
            }
            if (_equippedLauncherGO == null && gasLauncherTemplate != null && _rHand != null)
            {
                _equippedLauncherGO = InstantiateCleanProp(gasLauncherTemplate, "Player_Held_LANZAGAS", _rHand,
                    new Vector3(-0.04f, 0.12f, 0.22f), new Vector3(-85f, 0f, 0f), 0.46f);
            }
            if (_equippedAmmoGO == null && gasAmmoTemplate != null && _rHand != null)
            {
                _equippedAmmoGO = InstantiateCleanProp(gasAmmoTemplate, "Player_Held_BALAGASLACRI", _rHand,
                    new Vector3(0.02f, 0.07f, 0.04f), new Vector3(0f, 0f, 0f), 1.0f);
            }
            if (_equippedShotgunGO == null && shotgunTemplate != null && _rHand != null)
            {
                _equippedShotgunGO = InstantiateCleanProp(shotgunTemplate, "Player_Held_ESCOPETA", _rHand,
                    new Vector3(-0.04f, 0.14f, 0.24f), new Vector3(-85f, 0f, 0f), 0.46f);
            }

            if (_previewBarricadaGO == null && barricadaTemplate != null)
            {
                _previewBarricadaGO = CreateHologramPreviewInstance(barricadaTemplate, "Player_FortnitePreview_BARRICADA", 1.0f);
            }
            if (_previewBarreraGrandeGO == null && barreraGrandeTemplate != null)
            {
                _previewBarreraGrandeGO = CreateHologramPreviewInstance(barreraGrandeTemplate, "Player_FortnitePreview_BARRERAGRANDE", 0.95f);
            }
        }

        private GameObject InstantiateCleanProp(GameObject template, string name, Transform parentBone, Vector3 localPos, Vector3 localEuler, float scale)
        {
            GameObject go = Instantiate(template);
            go.name = name;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
            go.transform.SetParent(parentBone, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = Vector3.one * scale;
            go.SetActive(false);
            return go;
        }

        private GameObject CreateHologramPreviewInstance(GameObject template, string name, float scale)
        {
            GameObject go = Instantiate(template);
            go.name = name;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = _hologramMat;
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            go.transform.localScale = Vector3.one * scale;
            go.SetActive(false);
            return go;
        }

        private void CreateHandSprayParticles(Transform sprayRoot)
        {
            GameObject pGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pGo.name = "HandGasSpray_FX";
            pGo.transform.SetParent(sprayRoot, false);
            pGo.transform.localPosition = new Vector3(0f, 0.16f, 0.22f);
            pGo.transform.localScale = new Vector3(0.24f, 0.24f, 0.55f);
            Destroy(pGo.GetComponent<Collider>());

            var mr = pGo.GetComponent<MeshRenderer>();
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (s != null && mr != null)
            {
                Material m = new Material(s);
                Color c = new Color(0.92f, 0.95f, 0.88f, 0.45f);
                m.color = c;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                mr.sharedMaterial = m;
            }
            pGo.SetActive(false);
            _handSprayEffectGO = pGo;
        }

        public void EquipSlot(TacticalItemType item)
        {
            if (item != TacticalItemType.None && !hasPickedUpArmoryKit)
            {
                // Auto-habilitar si el jugador pulsa la ranura o está probando el inventario
                PickupFullArmoryInventory();
            }

            EnsureHandPropsInstantiated();
            currentItem = item;
            isReloading = false;
            isSprayingHandGas = false;

            if (_equippedShieldGO != null) _equippedShieldGO.SetActive(item == TacticalItemType.Escudo);
            if (_equippedTonfaGO != null) _equippedTonfaGO.SetActive(item == TacticalItemType.Tonfa);
            if (_equippedGasSprayGO != null) _equippedGasSprayGO.SetActive(item == TacticalItemType.GasLacriSprite);
            if (_equippedLauncherGO != null) _equippedLauncherGO.SetActive(item == TacticalItemType.LanzaGasLacri);
            if (_equippedAmmoGO != null) _equippedAmmoGO.SetActive(item == TacticalItemType.BalaGasLacri);
            if (_equippedShotgunGO != null) _equippedShotgunGO.SetActive(item == TacticalItemType.Escopeta);

            if (_previewBarricadaGO != null) _previewBarricadaGO.SetActive(item == TacticalItemType.Barricada && barricadaCount > 0);
            if (_previewBarreraGrandeGO != null) _previewBarreraGrandeGO.SetActive(item == TacticalItemType.BarreraGrande && barreraGrandeCount > 0);

            if (_handSprayEffectGO != null && item != TacticalItemType.GasLacriSprite)
                _handSprayEffectGO.SetActive(false);
        }

        private void Update()
        {
            if (_fireCooldown > 0f) _fireCooldown -= Time.deltaTime;
            if (_tonfaSwingTimer > 0f) _tonfaSwingTimer -= Time.deltaTime;

            if (isReloading)
            {
                _reloadTimer -= Time.deltaTime;
                if (_reloadTimer <= 0f)
                {
                    FinishReload();
                }
            }

            UpdatePlacementPreviewTransform();

            // Si cualquier ventana de decisión está abierta o el puntero está sobre un botón UI, no disparar ni cambiar armas con clicks
            bool uiBlocked = DecisionManager.IsAnyDecisionUIOpen ||
                             (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());

            ReadInventoryInputs(uiBlocked);
            UpdateHandGasSprayLogic(uiBlocked);
        }

        private void ReadInventoryInputs(bool uiBlocked)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // 1. Recoger / Reabastecer los 8 suministros en la Sala de Armas con [E]
            if (kb.eKey.wasPressedThisFrame && IsPlayerInsideOrNearArmory())
            {
                PickupFullArmoryInventory();
            }

            // 2. Ordenar a la tropa UTOP que dispare gas lacrimógeno con su LANZAGASLACRI hacia los manifestantes ([H])
            if (kb.hKey.wasPressedThisFrame && !uiBlocked)
            {
                var dir = FindAnyObjectByType<ScenarioDirector>();
                if (dir != null)
                {
                    dir.OrderTroopFireTearGasAtProtesters();
                }
            }

            // 3. Selección de ranuras 1..8 con rueda del ratón o teclas numéricas [1]..[8] / [0] (manos libres)
            if (!uiBlocked && mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.05f)
                {
                    if (!hasPickedUpArmoryKit) PickupFullArmoryInventory();
                    int dir = scroll > 0f ? 1 : -1;
                    int next = ((int)currentItem + dir + 9) % 9;
                    EquipSlot((TacticalItemType)next);
                }
            }

            if (kb.digit0Key.wasPressedThisFrame) EquipSlot(TacticalItemType.None);
            if (kb.digit3Key.wasPressedThisFrame) EquipSlot(TacticalItemType.GasLacriSprite);
            if (kb.digit4Key.wasPressedThisFrame) EquipSlot(TacticalItemType.LanzaGasLacri);
            if (kb.digit5Key.wasPressedThisFrame) EquipSlot(TacticalItemType.BalaGasLacri);
            if (kb.digit6Key.wasPressedThisFrame) EquipSlot(TacticalItemType.Escopeta);
            if (kb.digit7Key.wasPressedThisFrame) EquipSlot(TacticalItemType.Barricada);
            if (kb.digit8Key.wasPressedThisFrame) EquipSlot(TacticalItemType.BarreraGrande);

            // Si ya recogió el kit de la armería, [1] selecciona Escudo y [2] selecciona Tonfa (mientras [G] sigue siendo Saludo y [Z] Sentarse)
            if (hasPickedUpArmoryKit)
            {
                if (kb.digit1Key.wasPressedThisFrame) EquipSlot(TacticalItemType.Escudo);
                if (kb.digit2Key.wasPressedThisFrame && (_utopAnim == null || _utopAnim.nearestAvailableChair == null))
                    EquipSlot(TacticalItemType.Tonfa);
            }

            if (uiBlocked || currentItem == TacticalItemType.None) return;

            bool leftClick = (mouse != null && Cursor.lockState == CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame);
            bool rightClick = (mouse != null && Cursor.lockState == CursorLockMode.Locked && mouse.rightButton.wasPressedThisFrame);

            // 4. Acciones específicas por ítem seleccionado
            switch (currentItem)
            {
                case TacticalItemType.Escudo:
                    // Estado 1 = En reposo abajo en una mano | Estado 2 = Defensa al frente protegiéndose (Click Derecho o [C])
                    if (rightClick || kb.cKey.wasPressedThisFrame || leftClick)
                    {
                        shieldDefensiveFront = !shieldDefensiveFront;
                    }
                    break;

                case TacticalItemType.Tonfa:
                    if (leftClick && _fireCooldown <= 0f)
                    {
                        _fireCooldown = 0.35f;
                        _tonfaSwingTimer = 0.32f;
                        if (_utopAnim != null) _utopAnim.TriggerFastPunchOrBatonStrike();
                    }
                    break;

                case TacticalItemType.BalaGasLacri:
                    // Si tiene la munición en mano y pulsa Click o [R], cambia al Lanzagases y lo recarga
                    if (leftClick || kb.rKey.wasPressedThisFrame)
                    {
                        EquipSlot(TacticalItemType.LanzaGasLacri);
                        StartReload();
                    }
                    break;

                case TacticalItemType.LanzaGasLacri:
                    if (kb.rKey.wasPressedThisFrame)
                    {
                        StartReload();
                    }
                    else if (leftClick && !isReloading && _fireCooldown <= 0f)
                    {
                        if (launcherChamberLoaded > 0)
                        {
                            FireGasLauncherProjectile();
                        }
                        else if (balaGasLacriCount > 0)
                        {
                            StartReload();
                        }
                    }
                    break;

                case TacticalItemType.Escopeta:
                    if (kb.rKey.wasPressedThisFrame)
                    {
                        StartReload();
                    }
                    else if (leftClick && !isReloading && _fireCooldown <= 0f)
                    {
                        if (shotgunLoadedShells > 0)
                        {
                            FireShotgunPelletsWithGravity();
                        }
                        else if (shotgunReserveShells > 0)
                        {
                            StartReload();
                        }
                    }
                    break;

                case TacticalItemType.Barricada:
                    if (kb.rKey.wasPressedThisFrame) _placementYawOffset = (_placementYawOffset + 45f) % 360f;
                    if (leftClick && barricadaCount > 0 && _validPlacementPose)
                    {
                        PlaceStructureInWorld(barricadaTemplate, "Placed_3D_BARRICADA", 1.0f);
                        barricadaCount--;
                        if (barricadaCount <= 0 && _previewBarricadaGO != null) _previewBarricadaGO.SetActive(false);
                    }
                    break;

                case TacticalItemType.BarreraGrande:
                    if (kb.rKey.wasPressedThisFrame) _placementYawOffset = (_placementYawOffset + 45f) % 360f;
                    if (leftClick && barreraGrandeCount > 0 && _validPlacementPose)
                    {
                        PlaceStructureInWorld(barreraGrandeTemplate, "Placed_3D_BARRERAGRANDE", 0.95f);
                        barreraGrandeCount--;
                        if (barreraGrandeCount <= 0 && _previewBarreraGrandeGO != null) _previewBarreraGrandeGO.SetActive(false);
                    }
                    break;
            }
#endif
        }

        private void UpdateHandGasSprayLogic(bool uiBlocked)
        {
            if (currentItem != TacticalItemType.GasLacriSprite || uiBlocked)
            {
                if (isSprayingHandGas)
                {
                    isSprayingHandGas = false;
                    if (_handSprayEffectGO != null) _handSprayEffectGO.SetActive(false);
                }
                return;
            }

            bool holdingSpray = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked && Mouse.current.leftButton.isPressed && gasSprayPercentage > 0.1f)
            {
                holdingSpray = true;
            }
#endif
            if (holdingSpray)
            {
                gasSprayPercentage = Mathf.Max(0f, gasSprayPercentage - Time.deltaTime * 16f);
                if (!isSprayingHandGas)
                {
                    isSprayingHandGas = true;
                    if (_handSprayEffectGO != null) _handSprayEffectGO.SetActive(true);
                }

                _sprayDamageTickTimer += Time.deltaTime;
                if (_sprayDamageTickTimer >= 0.30f)
                {
                    _sprayDamageTickTimer = 0f;
                    Vector3 origin = transform.position + Vector3.up * 1.45f;
                    Vector3 fwd = Camera.main != null ? Camera.main.transform.forward : transform.forward;
                    fwd.y = 0f; if (fwd.sqrMagnitude > 0.01f) fwd.Normalize();

                    var protesters = MinerBlockerNPCController.All;
                    for (int i = 0; i < protesters.Count; i++)
                    {
                        var p = protesters[i];
                        if (p == null || !p.gameObject.activeInHierarchy) continue;
                        Vector3 diff = p.transform.position - origin;
                        diff.y = 0f;
                        if (diff.magnitude <= 4.8f && Vector3.Dot(fwd, diff.normalized) > 0.35f)
                        {
                            p.ScriptedFlee(transform.position);
                        }
                    }
                }
            }
            else if (isSprayingHandGas)
            {
                isSprayingHandGas = false;
                if (_handSprayEffectGO != null) _handSprayEffectGO.SetActive(false);
            }
        }

        private void StartReload()
        {
            if (isReloading) return;
            if (currentItem == TacticalItemType.LanzaGasLacri)
            {
                if (launcherChamberLoaded >= 1 || balaGasLacriCount <= 0) return;
                isReloading = true;
                _reloadTimer = 1.05f;
            }
            else if (currentItem == TacticalItemType.Escopeta)
            {
                if (shotgunLoadedShells >= 6 || shotgunReserveShells <= 0) return;
                isReloading = true;
                _reloadTimer = 1.15f;
            }
        }

        private void FinishReload()
        {
            isReloading = false;
            if (currentItem == TacticalItemType.LanzaGasLacri && balaGasLacriCount > 0 && launcherChamberLoaded < 1)
            {
                balaGasLacriCount--;
                launcherChamberLoaded = 1;
            }
            else if (currentItem == TacticalItemType.Escopeta && shotgunReserveShells > 0)
            {
                int needed = 6 - shotgunLoadedShells;
                int take = Mathf.Min(needed, shotgunReserveShells);
                shotgunLoadedShells += take;
                shotgunReserveShells -= take;
            }
        }

        private void FireGasLauncherProjectile()
        {
            launcherChamberLoaded = 0;
            _fireCooldown = 0.65f;

            Transform camT = Camera.main != null ? Camera.main.transform : transform;
            Vector3 muzzlePos = (_rHand != null ? _rHand.position : transform.position + Vector3.up * 1.45f) + camT.forward * 0.45f;
            Vector3 launchVel = camT.forward * 19.5f + Vector3.up * 4.2f;

            SpawnTearGasGrenadeProjectile(muzzlePos, launchVel, gasAmmoTemplate);
        }

        public static void SpawnTearGasGrenadeProjectile(Vector3 startPos, Vector3 initialVelocity, GameObject ammoVisualTemplate)
        {
            GameObject proj;
            if (ammoVisualTemplate != null)
            {
                proj = Instantiate(ammoVisualTemplate);
                foreach (var c in proj.GetComponentsInChildren<Collider>(true)) Destroy(c);
                proj.transform.localScale = Vector3.one * 1.1f;
            }
            else
            {
                proj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                proj.transform.localScale = new Vector3(0.09f, 0.16f, 0.09f);
                Destroy(proj.GetComponent<Collider>());
            }

            proj.name = "Active_BALAGASLACRI_Projectile";
            proj.transform.position = startPos;
            proj.SetActive(true);

            var mover = proj.AddComponent<TearGasGrenadeBallistics>();
            mover.Initialize(initialVelocity);
        }

        private void FireShotgunPelletsWithGravity()
        {
            shotgunLoadedShells--;
            _fireCooldown = 0.55f;

            Transform camT = Camera.main != null ? Camera.main.transform : transform;
            Vector3 muzzlePos = (_rHand != null ? _rHand.position : transform.position + Vector3.up * 1.45f) + camT.forward * 0.50f;

            // Disparar 6 perdigones de goma con caída por gravedad real
            for (int i = 0; i < 6; i++)
            {
                Vector3 spread = new Vector3(
                    Random.Range(-0.035f, 0.035f),
                    Random.Range(-0.025f, 0.025f),
                    Random.Range(-0.035f, 0.035f));
                Vector3 dir = (camT.forward + spread).normalized;

                GameObject pellet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pellet.name = "Shotgun_RubberPellet";
                pellet.transform.position = muzzlePos;
                pellet.transform.localScale = Vector3.one * 0.055f;
                Destroy(pellet.GetComponent<Collider>());

                var mr = pellet.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    Material m = new Material(s);
                    m.color = new Color(0.15f, 0.15f, 0.16f);
                    mr.sharedMaterial = m;
                }

                var bal = pellet.AddComponent<RubberPelletBallistics>();
                bal.Initialize(dir * 38f);
            }
        }

        private void UpdatePlacementPreviewTransform()
        {
            bool showBarricada = (currentItem == TacticalItemType.Barricada && barricadaCount > 0);
            bool showBarrera = (currentItem == TacticalItemType.BarreraGrande && barreraGrandeCount > 0);

            if (_previewBarricadaGO != null && _previewBarricadaGO.activeSelf != showBarricada)
                _previewBarricadaGO.SetActive(showBarricada);
            if (_previewBarreraGrandeGO != null && _previewBarreraGrandeGO.activeSelf != showBarrera)
                _previewBarreraGrandeGO.SetActive(showBarrera);

            if (!showBarricada && !showBarrera)
            {
                _validPlacementPose = false;
                return;
            }

            Transform camT = Camera.main != null ? Camera.main.transform : transform;
            Vector3 flatFwd = transform.forward;
            if (camT != null)
            {
                flatFwd = camT.forward;
                flatFwd.y = 0f;
                if (flatFwd.sqrMagnitude > 0.001f) flatFwd.Normalize();
                else flatFwd = transform.forward;
            }

            Vector3 probeOrigin = transform.position + flatFwd * 3.1f + Vector3.up * 2.5f;
            float groundY = transform.position.y;
            Vector3 groundNormal = Vector3.up;

            if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, 8.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                groundNormal = hit.normal;
            }

            float heightOffset = showBarrera ? 0.95f : 0.36f;
            _currentPlacementPos = new Vector3(probeOrigin.x, groundY + heightOffset, probeOrigin.z);
            float baseYaw = Mathf.Atan2(flatFwd.x, flatFwd.z) * Mathf.Rad2Deg + _placementYawOffset;
            if (showBarrera) baseYaw += 90f;
            _currentPlacementRot = Quaternion.FromToRotation(Vector3.up, groundNormal) * Quaternion.Euler(0f, baseYaw, 0f);
            _validPlacementPose = true;

            GameObject activePreview = showBarricada ? _previewBarricadaGO : _previewBarreraGrandeGO;
            if (activePreview != null)
            {
                activePreview.transform.SetPositionAndRotation(_currentPlacementPos, _currentPlacementRot);
            }
        }

        private void PlaceStructureInWorld(GameObject template, string name, float scale)
        {
            if (template == null) return;
            GameObject placed = Instantiate(template, _currentPlacementPos, _currentPlacementRot);
            placed.name = name;
            placed.transform.localScale = Vector3.one * scale;
            placed.SetActive(true);

            foreach (var mr in placed.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.GetComponent<Collider>() == null)
                    mr.gameObject.AddComponent<BoxCollider>();
            }
        }

        // =========================================================================================
        // POSES ESQUELÉTICAS EN LATEUPDATE PARA SOSTENER ESCUDO, SPRAY, TONFA, ESCOPETA Y LANZAGAS
        // =========================================================================================

        private void LateUpdate()
        {
            if (currentItem == TacticalItemType.None) return;
            if (_utopAnim != null && (_utopAnim.IsDead || _utopAnim.IsSeatedOrSitting || _utopAnim.IsRolling)) return;

            FindPlayerBones();

            switch (currentItem)
            {
                case TacticalItemType.Escudo:
                    if (shieldDefensiveFront)
                    {
                        // Estado 2: Escudo levantado de frente protegiendo torso y rostro
                        if (_lArm != null) _lArm.localRotation = new Quaternion(0.22f, -0.45f, -0.48f, 0.72f).normalized;
                        if (_lForeArm != null) _lForeArm.localRotation = new Quaternion(0.28f, -0.35f, -0.68f, 0.58f).normalized;
                        if (_equippedShieldGO != null)
                        {
                            _equippedShieldGO.transform.localPosition = new Vector3(0.08f, 0.16f, 0.18f);
                            _equippedShieldGO.transform.localRotation = Quaternion.Euler(0f, 95f, -10f);
                        }
                    }
                    else
                    {
                        // Estado 1: Escudo en reposo abajo en una mano al costado
                        if (_lArm != null) _lArm.localRotation = new Quaternion(0.48f, -0.16f, -0.18f, 0.84f).normalized;
                        if (_lForeArm != null) _lForeArm.localRotation = new Quaternion(0.04f, -0.08f, -0.22f, 0.97f).normalized;
                        if (_equippedShieldGO != null)
                        {
                            _equippedShieldGO.transform.localPosition = new Vector3(0.06f, 0.12f, 0.12f);
                            _equippedShieldGO.transform.localRotation = Quaternion.Euler(0f, 85f, 0f);
                        }
                    }
                    break;

                case TacticalItemType.Tonfa:
                    if (_tonfaSwingTimer <= 0.01f)
                    {
                        if (_rArm != null) _rArm.localRotation = new Quaternion(0.38f, 0.15f, 0.28f, 0.87f).normalized;
                        if (_rForeArm != null) _rForeArm.localRotation = new Quaternion(0.12f, 0.22f, 0.52f, 0.82f).normalized;
                    }
                    break;

                case TacticalItemType.GasLacriSprite:
                    if (isSprayingHandGas)
                    {
                        // Levantar la mano derecha al frente para rociar el gas lacrimógeno en spray
                        if (_rArm != null) _rArm.localRotation = new Quaternion(0.08f, -0.42f, 0.54f, 0.72f).normalized;
                        if (_rForeArm != null) _rForeArm.localRotation = new Quaternion(-0.02f, 0.06f, 0.12f, 0.99f).normalized;
                    }
                    else
                    {
                        if (_rArm != null) _rArm.localRotation = new Quaternion(0.36f, 0.12f, 0.25f, 0.89f).normalized;
                        if (_rForeArm != null) _rForeArm.localRotation = new Quaternion(0.10f, 0.18f, 0.48f, 0.85f).normalized;
                    }
                    break;

                case TacticalItemType.LanzaGasLacri:
                case TacticalItemType.Escopeta:
                    // Sujeción realista con LAS DOS MANOS apuntando al frente (mano derecha en empuñadura, mano izquierda en guardamanos)
                    if (_rArm != null) _rArm.localRotation = new Quaternion(0.24f, -0.26f, 0.44f, 0.82f).normalized;
                    if (_rForeArm != null) _rForeArm.localRotation = new Quaternion(0.18f, 0.28f, 0.62f, 0.71f).normalized;
                    if (_lArm != null) _lArm.localRotation = new Quaternion(0.16f, -0.48f, -0.44f, 0.74f).normalized;
                    if (_lForeArm != null) _lForeArm.localRotation = new Quaternion(0.22f, -0.20f, -0.58f, 0.75f).normalized;
                    break;
            }
        }

        // =========================================================================================
        // HUD DE INVENTARIO DE 8 RANURAS Y AVISO EN ARMERÍA
        // =========================================================================================

        private void OnGUI()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }

            // Aviso contextual en la Sala de Armas (Armería)
            if (IsPlayerInsideOrNearArmory())
            {
                float pw = 660f, ph = 38f;
                float px = (Screen.width - pw) * 0.5f, py = Screen.height - 160f;
                GUI.color = new Color(0.05f, 0.12f, 0.09f, 0.94f);
                GUI.DrawTexture(new Rect(px, py, pw, ph), _whiteTex);
                GUI.color = new Color(0.25f, 0.92f, 0.52f, 1f);
                GUI.DrawTexture(new Rect(px, py, 5f, ph), _whiteTex);

                GUIStyle st = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.65f, 0.98f, 0.78f) }
                };
                GUI.color = Color.white;
                string msg = hasPickedUpArmoryKit
                    ? "SALA DE ARMAS UTOP — Pulsa [E] para Reabastecer Munición (10 BALAGASLACRI, Spray 100% y Barreras)"
                    : "SALA DE ARMAS UTOP — Pulsa [E] para Tomar los 8 Ítems Tácticos en tu Inventario";
                GUI.Label(new Rect(px + 8f, py, pw - 16f, ph), msg, st);
            }

            // Barra inferior de Inventario de 8 Ranuras (siempre visible en la parte inferior izquierda/central)
            float slotW = 86f;
            float slotH = 54f;
            float gap = 5f;
            float totalW = 8 * slotW + 7 * gap + 16f;
            float barX = 16f;
            float barY = Screen.height - slotH - 16f;

            GUI.color = new Color(0.04f, 0.07f, 0.09f, 0.90f);
            GUI.DrawTexture(new Rect(barX, barY - 22f, totalW, slotH + 28f), _whiteTex);

            GUIStyle headerSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.52f, 0.94f, 0.68f) }
            };
            GUI.color = Color.white;
            string activeInfo = GetActiveItemStatusText();
            GUI.Label(new Rect(barX + 8f, barY - 20f, totalW - 16f, 18f),
                "INVENTARIO TÁCTICO UTOP (Rueda Ratón / [1-8] / [0] Libre | [H] Ordenar a Tropa Disparar Gas):  " + activeInfo,
                headerSt);

            string[] names =
            {
                "",
                "1.ESCUDO",
                "2.TONFA",
                "3.GAS SPRAY",
                "4.LANZAGAS",
                "5.BALA GAS",
                "6.ESCOPETA",
                "7.BARRICADA",
                "8.BARRERA"
            };

            string[] subLabels =
            {
                "",
                shieldDefensiveFront ? "[FRENTE]" : "[REPOSO]",
                "1 Mano",
                string.Format("{0:0}%", gasSprayPercentage),
                isReloading && currentItem == TacticalItemType.LanzaGasLacri ? "RECARG..." : string.Format("{0}/1 ({1})", launcherChamberLoaded, balaGasLacriCount),
                string.Format("x{0} unid.", balaGasLacriCount),
                isReloading && currentItem == TacticalItemType.Escopeta ? "RECARG..." : string.Format("{0}/{1}", shotgunLoadedShells, shotgunReserveShells),
                string.Format("x{0} [Colocar]", barricadaCount),
                string.Format("x{0} [Colocar]", barreraGrandeCount)
            };

            GUIStyle slotTitleSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };
            GUIStyle slotSubSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.LowerCenter,
                normal = { textColor = new Color(0.95f, 0.88f, 0.48f) }
            };

            for (int i = 1; i <= 8; i++)
            {
                float sx = barX + 8f + (i - 1) * (slotW + gap);
                bool selected = ((int)currentItem == i);

                GUI.color = selected
                    ? new Color(0.18f, 0.56f, 0.36f, 0.98f)
                    : new Color(0.10f, 0.14f, 0.18f, 0.94f);
                Rect r = new Rect(sx, barY, slotW, slotH);
                GUI.DrawTexture(r, _whiteTex);

                if (selected)
                {
                    GUI.color = new Color(0.45f, 0.98f, 0.65f, 1f);
                    GUI.DrawTexture(new Rect(sx, barY, slotW, 3f), _whiteTex);
                }

                GUI.color = Color.white;
                GUI.Label(new Rect(sx + 2f, barY + 5f, slotW - 4f, 20f), names[i], slotTitleSt);
                GUI.Label(new Rect(sx + 2f, barY + 26f, slotW - 4f, 22f), subLabels[i], slotSubSt);

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    EquipSlot((TacticalItemType)i);
                }
            }
        }

        private string GetActiveItemStatusText()
        {
            switch (currentItem)
            {
                case TacticalItemType.None:
                    return "Manos Libres";
                case TacticalItemType.Escudo:
                    return shieldDefensiveFront
                        ? "ESCUDO EN DEFENSA AL FRENTE (Click Der / [C] cambiar a Reposo)"
                        : "ESCUDO EN REPOSO ABAJO (Click Der / [C] poner Escudo al Frente)";
                case TacticalItemType.Tonfa:
                    return "TONFA EN MANO DERECHA (Click Izq: Golpe de Bastón)";
                case TacticalItemType.GasLacriSprite:
                    return string.Format("GAS LACRIMÓGENO DE MANO: {0:0}% (Mantén Click Izq para levantar mano y rociar)", gasSprayPercentage);
                case TacticalItemType.LanzaGasLacri:
                    return isReloading
                        ? "RECARGANDO LANZAGASES CON BALAGASLACRI..."
                        : string.Format("LANZAGASES (2 Manos) — Recámara: {0}/1 | Munición BALAGASLACRI: {1} (Click Izq: Disparar | [R]: Recargar)", launcherChamberLoaded, balaGasLacriCount);
                case TacticalItemType.BalaGasLacri:
                    return string.Format("MUNICIÓN BALAGASLACRI: {0} proyectiles restantes (Click Izq o [R] para cargar en Lanzagases)", balaGasLacriCount);
                case TacticalItemType.Escopeta:
                    return isReloading
                        ? "RECARGANDO ESCOPETA..."
                        : string.Format("ESCOPETA DE PERDIGONES (2 Manos) — Cartuchos: {0}/6 | Reserva: {1} (Click Izq: Disparar | [R]: Recargar)", shotgunLoadedShells, shotgunReserveShells);
                case TacticalItemType.Barricada:
                    return string.Format("MODO COLOCACIÓN FORTNITE (BARRICADA x{0}) — Click Izq: Colocar en calle | [R]: Girar", barricadaCount);
                case TacticalItemType.BarreraGrande:
                    return string.Format("MODO COLOCACIÓN FORTNITE (BARRERA GRANDE x{0}) — Click Izq: Colocar en calle | [R]: Girar", barreraGrandeCount);
                default:
                    return "";
            }
        }
    }

    /// <summary>
    /// Proyectil físico parabólico de 37mm (BALAGASLACRI) disparado por el LANZAGASLACRI (del jugador o de la tropa).
    /// Al impactar en la calzada genera humo lacrimógeno moderado y hace que los manifestantes en un radio corto escapen.
    /// </summary>
    public class TearGasGrenadeBallistics : MonoBehaviour
    {
        private Vector3 _velocity;
        private float _lifeTimer = 0f;
        private bool _detonated = false;

        public void Initialize(Vector3 initialVel)
        {
            _velocity = initialVel;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _lifeTimer += dt;

            // Gravedad real de granada de 37mm
            _velocity += new Vector3(0f, -15.5f, 0f) * dt;
            Vector3 nextPos = transform.position + _velocity * dt;

            if (Physics.Linecast(transform.position, nextPos, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + Vector3.up * 0.08f;
                _velocity = new Vector3(_velocity.x * 0.25f, 0f, _velocity.z * 0.25f);
                if (!_detonated)
                {
                    _detonated = true;
                    SpawnModerateTearGasCloud(transform.position);
                }
            }
            else
            {
                transform.position = nextPos;
                transform.Rotate(260f * dt, 140f * dt, 0f);
            }

            if (!_detonated && _lifeTimer >= 1.8f)
            {
                _detonated = true;
                SpawnModerateTearGasCloud(transform.position);
            }

            if (_lifeTimer >= 10.5f)
            {
                Destroy(gameObject);
            }
        }

        public static void SpawnModerateTearGasCloud(Vector3 center)
        {
            GameObject cloudGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cloudGO.name = "TearGas_ModerateCloud_FX";
            cloudGO.transform.position = center + Vector3.up * 0.95f;
            cloudGO.transform.localScale = new Vector3(3.5f, 2.2f, 3.5f);
            Destroy(cloudGO.GetComponent<Collider>());

            var mr = cloudGO.GetComponent<MeshRenderer>();
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (s != null && mr != null)
            {
                Material m = new Material(s);
                Color c = new Color(0.88f, 0.92f, 0.86f, 0.38f);
                m.color = c;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                mr.sharedMaterial = m;
            }
            Destroy(cloudGO, 7.5f);

            // Hacer que los manifestantes dentro de un radio moderado (8.5m) escapen hasta una distancia corta
            var protesters = MinerBlockerNPCController.All;
            for (int i = 0; i < protesters.Count; i++)
            {
                var p = protesters[i];
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(p.transform.position, center);
                if (d <= 8.5f)
                {
                    p.ScriptedFlee(center);
                }
            }
        }
    }

    /// <summary>
    /// Perdigón de goma con caída por gravedad real disparado por la ESCOPETA antidisturbios.
    /// </summary>
    public class RubberPelletBallistics : MonoBehaviour
    {
        private Vector3 _vel;
        private float _timer = 0f;

        public void Initialize(Vector3 initialVelocity)
        {
            _vel = initialVelocity;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _timer += dt;
            _vel += new Vector3(0f, -9.81f, 0f) * dt;
            Vector3 next = transform.position + _vel * dt;

            // Verificar impacto con manifestantes cercanos
            var protesters = MinerBlockerNPCController.All;
            for (int i = 0; i < protesters.Count; i++)
            {
                var p = protesters[i];
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                Vector3 center = p.transform.position + Vector3.up * 1.0f;
                if ((next - center).sqrMagnitude <= 0.45f)
                {
                    p.OnHitByPlayerPunch(null);
                    Destroy(gameObject);
                    return;
                }
            }

            if (Physics.Linecast(transform.position, next, out _, ~0, QueryTriggerInteraction.Ignore) || _timer >= 2.2f)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = next;
        }
    }
}
