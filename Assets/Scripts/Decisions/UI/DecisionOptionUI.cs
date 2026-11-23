using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SimuladorBloqueos.Decisions.UI
{
    /// <summary>
    /// Botón individual dentro de la cuadrícula 2x2 de opciones.
    /// Nunca muestra la clasificación (Excelente, Buena, Incorrecta) antes de responder.
    /// </summary>
    public class DecisionOptionUI : MonoBehaviour
    {
        public Button button;
        public Image backgroundImage;
        public TextMeshProUGUI badgeText;
        public TextMeshProUGUI optionBodyText;

        private int _optionIndex;
        private DecisionOption _boundOption;
        private Action<int, DecisionOption> _onSelected;

        private static readonly Color NormalBg = new Color(0.10f, 0.16f, 0.20f, 0.96f);
        private static readonly Color SelectedBg = new Color(0.16f, 0.36f, 0.28f, 0.98f);

        public void Bind(int displayIndex, DecisionOption option, Action<int, DecisionOption> onSelected)
        {
            _optionIndex = displayIndex;
            _boundOption = option;
            _onSelected = onSelected;

            if (badgeText != null)
            {
                char letter = (char)('A' + Mathf.Clamp(displayIndex, 0, 3));
                badgeText.text = string.Format("OPCIÓN {0}", letter);
            }

            if (optionBodyText != null)
            {
                optionBodyText.text = option != null ? option.text : "";
            }

            SetHighlighted(false);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(HandleClick);
                button.interactable = true;
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = highlighted ? SelectedBg : NormalBg;
            }
        }

        private void HandleClick()
        {
            _onSelected?.Invoke(_optionIndex, _boundOption);
        }
    }
}
