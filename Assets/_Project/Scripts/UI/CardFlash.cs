using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDice.UI
{
    /// <summary>
    /// Hit feedback on a card: a short colour flash of the card's image and a floating number that
    /// rises and fades. Red for damage, green for heals, blue for shield.
    /// </summary>
    public class CardFlash : MonoBehaviour
    {
        public static readonly Color DamageColor = new(0.95f, 0.25f, 0.2f);
        public static readonly Color HealColor = new(0.3f, 0.9f, 0.4f);
        public static readonly Color ShieldColor = new(0.6f, 0.8f, 1f);

        private Image m_image;
        private Color m_baseColor;
        private Coroutine m_flash;

        private void Awake()
        {
            m_image = GetComponent<Image>();
            if (m_image != null) m_baseColor = m_image.color;
        }

        /// <summary>Remember the card's resting colour (call after re-tinting on refresh).</summary>
        public void SetBaseColor(Color color)
        {
            m_baseColor = color;
            if (m_flash == null && m_image != null) m_image.color = color;
        }

        public void Play(string text, Color color, float duration = 0.6f)
        {
            if (!isActiveAndEnabled) return;
            if (m_flash != null) StopCoroutine(m_flash);
            m_flash = StartCoroutine(Flash(color, 0.25f));
            StartCoroutine(Float(text, color, duration));
        }

        private IEnumerator Flash(Color color, float duration)
        {
            float t = 0f;
            while (t < duration && m_image != null)
            {
                t += Time.deltaTime;
                m_image.color = Color.Lerp(color, m_baseColor, t / duration);
                yield return null;
            }
            if (m_image != null) m_image.color = m_baseColor;
            m_flash = null;
        }

        private IEnumerator Float(string text, Color color, float duration)
        {
            var go = new GameObject("Floater", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.6f);
            rt.sizeDelta = new Vector2(300f, 60f);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 44f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                rt.anchoredPosition = new Vector2(0f, k * 70f);
                label.color = new Color(color.r, color.g, color.b, 1f - k * k);
                yield return null;
            }
            Destroy(go);
        }
    }
}
