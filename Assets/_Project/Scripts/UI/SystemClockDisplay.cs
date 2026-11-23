using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace SimuladorBloqueos.UI
{
    /// <summary>
    /// Muestra la fecha y hora del sistema en tiempo real.
    /// Optimizado: Se actualiza en una corrutina cada 1 segundo para evitar carga en Update().
    /// </summary>
    public class SystemClockDisplay : MonoBehaviour
    {
        [Header("Referencias")]
        public TextMeshProUGUI clockText;

        [Header("Formato")]
        public string prefix = "LPZ | ";

        private void Start()
        {
            if (clockText == null)
            {
                clockText = GetComponent<TextMeshProUGUI>();
            }
            StartCoroutine(UpdateClockRoutine());
        }

        private IEnumerator UpdateClockRoutine()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                if (clockText != null)
                {
                    DateTime now = DateTime.Now;
                    clockText.text = $"{prefix}{now:dd/MM/yyyy}  {now:HH:mm:ss}";
                }
                yield return wait;
            }
        }
    }
}
