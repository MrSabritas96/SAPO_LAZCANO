using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimuladorBloqueos.UI
{
    /// <summary>
    /// Controlador del panel de configuración gráfica, audio y controles.
    /// Aplica ajustes reales en el motor Unity.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        [Header("Controles de UI")]
        public TMP_Dropdown resolutionDropdown;
        public Toggle fullscreenToggle;
        public TMP_Dropdown qualityDropdown;
        public Slider masterVolumeSlider;
        public TextMeshProUGUI volumePercentText;
        public Button backButton;

        [Header("Navegación")]
        public MenuNavigationController navigationController;

        private Resolution[] _availableResolutions;
        private List<Resolution> _filteredResolutions;

        private void Start()
        {
            InitializeResolutions();
            InitializeQuality();
            InitializeAudio();

            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackClicked);
            }
        }

        private void InitializeResolutions()
        {
            if (resolutionDropdown == null) return;

            _availableResolutions = Screen.resolutions;
            _filteredResolutions = new List<Resolution>();
            resolutionDropdown.ClearOptions();

            var options = new List<string>();
            int currentResolutionIndex = 0;

            for (int i = 0; i < _availableResolutions.Length; i++)
            {
                var res = _availableResolutions[i];
                // Filtrar duplicados o resoluciones estándar
                string option = $"{res.width} x {res.height} @ {res.refreshRateRatio.value:F0}Hz";
                if (!options.Contains(option))
                {
                    options.Add(option);
                    _filteredResolutions.Add(res);

                    if (res.width == Screen.currentResolution.width && res.height == Screen.currentResolution.height)
                    {
                        currentResolutionIndex = options.Count - 1;
                    }
                }
            }

            if (options.Count == 0)
            {
                options.Add("1920 x 1080 @ 60Hz");
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        private void InitializeQuality()
        {
            if (qualityDropdown == null) return;

            qualityDropdown.ClearOptions();
            var names = QualitySettings.names;
            var options = new List<string>(names);
            qualityDropdown.AddOptions(options);
            qualityDropdown.value = QualitySettings.GetQualityLevel();
            qualityDropdown.RefreshShownValue();
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }

        private void InitializeAudio()
        {
            if (masterVolumeSlider == null) return;

            float currentVol = AudioListener.volume;
            masterVolumeSlider.value = currentVol;
            UpdateVolumeLabel(currentVol);
            masterVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        private void OnResolutionChanged(int index)
        {
            if (_filteredResolutions != null && index >= 0 && index < _filteredResolutions.Count)
            {
                var selected = _filteredResolutions[index];
                Screen.SetResolution(selected.width, selected.height, Screen.fullScreenMode, selected.refreshRateRatio);
                Debug.Log($"[Settings] Resolución establecida: {selected.width}x{selected.height}");
            }
        }

        private void OnFullscreenToggled(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
            Debug.Log($"[Settings] Pantalla completa: {isFullscreen}");
        }

        private void OnQualityChanged(int qualityIndex)
        {
            QualitySettings.SetQualityLevel(qualityIndex, true);
            Debug.Log($"[Settings] Calidad gráfica establecida: {QualitySettings.names[qualityIndex]}");
        }

        private void OnVolumeChanged(float volume)
        {
            AudioListener.volume = volume;
            UpdateVolumeLabel(volume);
        }

        private void UpdateVolumeLabel(float volume)
        {
            if (volumePercentText != null)
            {
                volumePercentText.text = $"{Mathf.RoundToInt(volume * 100f)}%";
            }
        }

        private void OnBackClicked()
        {
            if (navigationController != null)
                navigationController.OpenMainMenu();
        }
    }
}
