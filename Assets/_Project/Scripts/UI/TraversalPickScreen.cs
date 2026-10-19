using System.Collections.Generic;
using System.Linq;
using DungeonDice.Dice;
using DungeonDice.Map;
using DungeonDice.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDice.UI
{
    /// <summary>
    /// Start node: pick three distinct traversal dice from the starting pool. Each card lists the
    /// die's faces and mean so the choice reads as a probability curve, which is the whole point of
    /// the prototype. Renders a <see cref="TraversalPickSelection"/>.
    /// </summary>
    public class TraversalPickScreen : MonoBehaviour
    {
        private TraversalPickSelection m_selection;
        private readonly List<Button> m_cards = new();
        private readonly List<Image> m_cardImages = new();
        private Button m_confirm;
        private TMP_Text m_hint;
        private bool m_built;

        public TraversalPickSelection Selection => m_selection;
        public IReadOnlyList<Button> CardButtons => m_cards;
        public Button ConfirmButton => m_confirm;

        private void OnEnable()
        {
            var pool = RunController.Instance != null ? RunController.Instance.StartingTraversalPool : System.Array.Empty<DieDefinition>();
            if (m_selection != null) m_selection.Changed -= Refresh;
            m_selection = new TraversalPickSelection(pool, RunState.TraversalPickCount);
            m_selection.Changed += Refresh;
            if (!m_built) Build();
            Refresh();
        }

        private void OnDisable()
        {
            if (m_selection != null) m_selection.Changed -= Refresh;
        }

        private void Build()
        {
            m_built = true;
            UiFactory.Stretch((RectTransform)transform);
            var background = UiFactory.Panel(transform, "Background", UiFactory.PanelDark);
            UiFactory.Vertical(background, spacing: 16f, padding: 32);

            UiFactory.Label(background, "Title", "Choose your traversal dice", 48f, color: UiFactory.Accent, preferredHeight: 64f);
            UiFactory.Label(background, "Explain", "At every fork you commit dice and roll; the total picks the path. Different faces, different odds.", 22f, color: UiFactory.TextDim, preferredHeight: 40f);
            m_hint = UiFactory.Label(background, "Hint", "", 24f, color: UiFactory.TextDim, preferredHeight: 36f);

            var row = UiFactory.Group(background, "Cards");
            UiFactory.Horizontal(row, spacing: 16f, padding: 0);
            UiFactory.Flexible(row, 1f);
            m_cards.Clear();
            m_cardImages.Clear();
            foreach (var die in m_selection.Pool) BuildCard(row, die);

            m_confirm = UiFactory.Button(background, "Confirm", "Enter the dungeon", OnConfirm, UiFactory.Accent, 30f, 72f);
            m_confirm.GetComponentInChildren<TMP_Text>().color = Color.black;
        }

        private void BuildCard(Transform parent, DieDefinition die)
        {
            var card = UiFactory.Button(parent, die.displayName, "", () => m_selection.Toggle(die), UiFactory.PanelMid, 20f, 360f);
            Destroy(card.GetComponentInChildren<TMP_Text>().gameObject);
            var column = UiFactory.Group(card.transform, "Column");
            UiFactory.Stretch(column, 10f);
            UiFactory.Vertical(column, spacing: 6f, padding: 8, controlHeight: true);

            UiFactory.Label(column, "Name", die.displayName, 30f);
            UiFactory.Label(column, "Faces", string.Join("  ", die.Faces.Select(f => f.Pips)), 26f);
            UiFactory.Label(column, "Mean", $"mean {ForkOdds.Mean(die):0.0}", 20f, color: UiFactory.TextDim);
            UiFactory.Label(column, "Role", RoleText(die), 18f, color: UiFactory.TextDim);

            m_cards.Add(card);
            m_cardImages.Add(card.GetComponent<Image>());
        }

        private static string RoleText(DieDefinition die)
        {
            var pips = Enumerable.Range(1, DieDefinition.FaceCount).Select(die.Pips).ToList();
            int spread = pips.Max() - pips.Min();
            if (spread == 0) return "Always the same";
            if (spread >= 5 && pips.Distinct().Count() == 2) return "All or nothing";
            if (spread <= 2) return pips.Max() <= 3 ? "Chases low totals" : pips.Min() >= 4 ? "Chases high totals" : "Hits the middle";
            return "Baseline d6";
        }

        private void Refresh()
        {
            if (!m_built) return;
            for (int i = 0; i < m_cards.Count && i < m_selection.Pool.Count; i++)
            {
                bool chosen = m_selection.IsChosen(m_selection.Pool[i]);
                m_cardImages[i].color = chosen ? UiFactory.Accent : UiFactory.PanelMid;
                m_cards[i].interactable = chosen || !m_selection.IsFull;
            }
            m_confirm.interactable = m_selection.CanConfirm;
            m_hint.text = m_selection.CanConfirm
                ? "Three dice chosen. Enter the dungeon."
                : $"Pick {m_selection.Capacity - m_selection.Chosen.Count} more. Click a chosen die to put it back.";
        }

        private void OnConfirm()
        {
            if (!m_selection.CanConfirm || RunController.Instance == null) return;
            RunController.Instance.ConfirmTraversalDice(m_selection.Chosen.ToList());
        }
    }
}
