using System.Linq;
using DungeonDice.Run;
using TMPro;
using UnityEngine;

namespace DungeonDice.UI
{
    /// <summary>Victory or defeat: the run's outcome and a summary, then back to Hero Pick.</summary>
    public class RunEndScreen : MonoBehaviour
    {
        private TMP_Text m_title;
        private TMP_Text m_summary;
        private bool m_built;

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
            UiFactory.Vertical(background, spacing: 16f, padding: 48);

            m_title = UiFactory.Label(background, "Title", "Run over", 56f, color: UiFactory.Accent, preferredHeight: 72f);
            m_summary = UiFactory.Label(background, "Summary", "", 24f, TextAlignmentOptions.TopLeft, preferredHeight: 360f);

            var again = UiFactory.Button(background, "NewRun", "New run", () => RunController.Instance?.AbandonRun(), UiFactory.Accent, 30f, 72f);
            again.GetComponentInChildren<TMP_Text>().color = Color.black;
        }

        private void Refresh()
        {
            var run = RunController.Instance != null ? RunController.Instance.State : null;
            if (run == null)
            {
                m_title.text = "Run over";
                m_summary.text = "";
                return;
            }

            m_title.text = run.Outcome switch
            {
                RunOutcome.Victory => "Victory!",
                RunOutcome.Defeat => "Defeat",
                _ => "Run over",
            };

            var lines = new System.Text.StringBuilder();
            lines.AppendLine(run.Outcome == RunOutcome.Victory
                ? $"The Vault Heart is destroyed. {run.Act?.displayName} is cleared."
                : "The party fell in the vault.");
            lines.AppendLine();
            lines.AppendLine($"Nodes visited: {run.VisitedNodes.Count}{(run.Act != null ? $" of {run.Act.nodes.Count}" : "")}");
            lines.AppendLine("Party:");
            foreach (var hero in run.Party)
            {
                string status = hero.IsAlive ? $"HP {hero.CurrentHealth}/{hero.MaxHealth}" : "fallen";
                lines.AppendLine($"  {hero.Definition.displayName}: {status}");
            }
            lines.AppendLine($"Resource dice: {run.ResourceDiceCount}");
            lines.AppendLine($"Traversal dice: {string.Join(", ", run.TraversalDice.Select(d => d.displayName))}");
            lines.AppendLine($"Seed: {run.Seed}");
            m_summary.text = lines.ToString();
        }
    }
}
