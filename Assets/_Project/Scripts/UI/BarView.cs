using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDice.UI
{
    /// <summary>
    /// A health bar: fill = current/max, a lighter overlay for shield (shown as extra, capped at the
    /// bar), and a centred "12/20 +3" label. Created by <see cref="UiFactory.Bar"/>.
    /// </summary>
    public class BarView : MonoBehaviour
    {
        private Image m_fill;
        private Image m_shield;
        private TMP_Text m_text;

        public void Bind(Image fill, Image shield, TMP_Text text)
        {
            m_fill = fill;
            m_shield = shield;
            m_text = text;
        }

        /// <summary>Shield is drawn over the bar from the left, as a fraction of max, so a 6-shield on a 30-HP hero reads as 20%.</summary>
        public void Set(int current, int max, int shield = 0)
        {
            float frac = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            if (m_fill != null) m_fill.fillAmount = frac;
            if (m_shield != null)
            {
                m_shield.fillAmount = max > 0 ? Mathf.Clamp01((float)shield / max) : 0f;
                m_shield.enabled = shield > 0;
            }
            if (m_text != null) m_text.text = shield > 0 ? $"{current}/{max}  +{shield}" : $"{current}/{max}";
        }

        public void SetColor(Color color)
        {
            if (m_fill != null) m_fill.color = color;
        }
    }
}
