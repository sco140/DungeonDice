using DungeonDice.Run;
using TMPro;
using UnityEngine;

namespace DungeonDice.UI
{
    /// <summary>
    /// Stand-in for a screen that is not built yet. Shows the phase name and run summary, with a
    /// Continue button and an Abandon button back to Hero Pick, so the whole run flow can be
    /// clicked through before the real screen exists. Continue either jumps to a fixed phase or,
    /// for the combat stand-in, tells the run the current node is complete.
    /// </summary>
    public class PlaceholderScreen : MonoBehaviour
    {
        public enum ContinueAction
        {
            /// <summary>Jump straight to <see cref="m_nextPhase"/>.</summary>
            NextPhase,
            /// <summary>Call <see cref="RunController.CompleteNode"/> (map or run end decided by the node).</summary>
            CompleteNode,
        }

        [SerializeField] private RunPhase m_phase;
        [SerializeField] private RunPhase m_nextPhase;
        [SerializeField] private ContinueAction m_continueAction = ContinueAction.NextPhase;
        [SerializeField] private string m_title;

        private TMP_Text m_summary;
        private bool m_built;

        public void Configure(RunPhase phase, RunPhase nextPhase, string title, ContinueAction action = ContinueAction.NextPhase)
        {
            m_phase = phase;
            m_nextPhase = nextPhase;
            m_title = title;
            m_continueAction = action;
        }

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

            UiFactory.Label(background, "Title", string.IsNullOrEmpty(m_title) ? m_phase.ToString() : m_title, 48f, color: UiFactory.Accent, preferredHeight: 64f);
            UiFactory.Label(background, "Subtitle", $"[{m_phase}] placeholder — not built yet", 22f, color: UiFactory.TextDim, preferredHeight: 32f);
            m_summary = UiFactory.Label(background, "Summary", "", 22f, TextAlignmentOptions.TopLeft, preferredHeight: 300f);

            string continueText = m_continueAction == ContinueAction.CompleteNode ? "Victory → continue" : $"Continue → {m_nextPhase}";
            UiFactory.Button(background, "Continue", continueText, OnContinue, UiFactory.Accent, 28f, 64f)
                .GetComponentInChildren<TMP_Text>().color = Color.black;
            UiFactory.Button(background, "Abandon", "Abandon run", () => RunController.Instance?.AbandonRun(), UiFactory.PanelMid, 22f, 48f);
        }

        private void OnContinue()
        {
            var run = RunController.Instance;
            if (run == null) return;
            if (m_continueAction == ContinueAction.CompleteNode) run.CompleteNode();
            else run.SetPhase(m_nextPhase);
        }

        private void Refresh()
        {
            var run = RunController.Instance != null ? RunController.Instance.State : null;
            if (run == null || !run.IsActive)
            {
                m_summary.text = "No active run.";
                return;
            }

            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"Seed: {run.Seed}");
            if (run.Outcome != RunOutcome.None) lines.AppendLine($"Outcome: {run.Outcome}");
            lines.AppendLine("Party:");
            foreach (var hero in run.Party)
                lines.AppendLine($"  {hero.Slot + 1}. {hero.Definition.displayName} ({hero.Definition.ColorLabel})  HP {hero.CurrentHealth}/{hero.MaxHealth}");
            lines.AppendLine($"Traversal dice: {(run.TraversalDice.Count == 0 ? "none yet" : string.Join(", ", System.Linq.Enumerable.Select(run.TraversalDice, d => d.displayName)))}");
            if (run.CurrentNode != null) lines.AppendLine($"Node: {run.CurrentNodeIndex} — {run.CurrentNode.name} ({run.CurrentNode.type})");
            m_summary.text = lines.ToString();
        }
    }
}
