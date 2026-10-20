using System.Collections.Generic;
using DungeonDice.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDice.UI
{
    /// <summary>
    /// Treasure node: one card per <see cref="RewardOption"/> in <see cref="RunState.PendingReward"/>.
    /// Clicking a card takes that reward and returns to the map.
    /// </summary>
    public class RewardScreen : MonoBehaviour
    {
        private readonly List<Button> m_optionButtons = new();
        private TMP_Text m_subtitle;
        private RectTransform m_row;
        private bool m_built;

        public IReadOnlyList<Button> OptionButtons => m_optionButtons;

        private void OnEnable()
        {
            if (!m_built) Build();
            Refresh();
        }

        private void Build()
        {
            m_built = true;
            UiFactory.Stretch((RectTransform)transform);
            var background = UiFactory.Panel(transform, "Background", UiFactory.PanelDark);
            UiFactory.Vertical(background, spacing: 16f, padding: 32);

            UiFactory.Label(background, "Title", "Treasure", 48f, color: UiFactory.Accent, preferredHeight: 64f);
            m_subtitle = UiFactory.Label(background, "Subtitle", "Choose one.", 24f, color: UiFactory.TextDim, preferredHeight: 36f);

            m_row = UiFactory.Group(background, "Options");
            UiFactory.Horizontal(m_row, spacing: 16f, padding: 0);
            UiFactory.Flexible(m_row, 1f);
        }

        private void Refresh()
        {
            var run = RunController.Instance != null ? RunController.Instance.State : null;
            UiFactory.DestroyChildren(m_row);
            m_optionButtons.Clear();

            var offer = run?.PendingReward;
            if (offer == null)
            {
                m_subtitle.text = "Nothing here.";
                return;
            }

            var node = run.CurrentNode;
            m_subtitle.text = $"{(node != null ? node.name : "Treasure")}: choose one.";
            for (int i = 0; i < offer.Options.Count; i++)
            {
                int index = i;
                var option = offer.Options[i];
                var tint = option.Kind == RewardKind.TraversalDie ? new Color(0.16f, 0.3f, 0.3f, 1f) : new Color(0.3f, 0.22f, 0.12f, 1f);
                var card = UiFactory.Button(m_row, $"Option{i}", "", () => RunController.Instance?.ChooseReward(index), tint, 20f, 300f);
                var oldLabel = card.GetComponentInChildren<TMP_Text>();
                if (oldLabel != null) Destroy(oldLabel.gameObject);
                var column = UiFactory.Group(card.transform, "Column");
                UiFactory.Stretch(column, 12f);
                UiFactory.Vertical(column, spacing: 8f, padding: 8, controlHeight: true);
                UiFactory.Label(column, "Title", option.Title, 28f);
                UiFactory.Label(column, "Description", option.Description, 20f, color: UiFactory.TextDim);
                m_optionButtons.Add(card);
            }
        }
    }
}
