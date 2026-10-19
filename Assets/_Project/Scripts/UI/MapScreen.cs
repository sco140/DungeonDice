using System.Collections.Generic;
using System.Linq;
using DungeonDice.Core;
using DungeonDice.Dice;
using DungeonDice.Map;
using DungeonDice.Run;
using GameUtility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDice.UI
{
    /// <summary>
    /// The act as a node list plus a detail panel for the current node. Unresolved nodes offer an
    /// Enter button; resolved nodes offer their exit: a single Travel button, or a fork with dice
    /// toggles, per-path nominal odds and a Roll button that opens the dice box with the Traversal
    /// profile. The settled roll's total picks the path; Continue travels there.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        private readonly List<int> m_committed = new();   // indices into RunState.TraversalDice
        private TMP_Text m_party;
        private TMP_Text m_nodeTitle;
        private TMP_Text m_nodeDescription;
        private TMP_Text m_result;
        private RectTransform m_nodeList;
        private RectTransform m_actions;
        private bool m_built;
        private bool m_awaitingRoll;

        /// <summary>Node the last fork roll chose; -1 when no roll is pending travel.</summary>
        public int PendingTarget { get; private set; } = -1;
        public int LastTotal { get; private set; }
        public bool AwaitingRoll => m_awaitingRoll;
        public IReadOnlyList<int> CommittedIndices => m_committed;

        private static RunState CurrentRun => RunController.Instance != null ? RunController.Instance.State : null;

        private void OnEnable()
        {
            EventManager.Register(GameEvents.RollCompleted, new EventHandler<RollCompletedEvent>(OnRollCompleted));
            if (!m_built) Build();
            m_committed.Clear();
            Refresh();
        }

        private void OnDisable()
        {
            EventManager.Unregister<RollCompletedEvent>(GameEvents.RollCompleted, OnRollCompleted);
            m_awaitingRoll = false;
        }

        // ---------------------------------------------------------------- build

        private void Build()
        {
            m_built = true;
            UiFactory.Stretch((RectTransform)transform);
            var background = UiFactory.Panel(transform, "Background", UiFactory.PanelDark);
            UiFactory.Vertical(background, spacing: 12f, padding: 24);

            UiFactory.Label(background, "Title", "The Map", 40f, color: UiFactory.Accent, preferredHeight: 52f);
            m_party = UiFactory.Label(background, "Party", "", 20f, color: UiFactory.TextDim, preferredHeight: 30f);

            var body = UiFactory.Group(background, "Body");
            UiFactory.Horizontal(body, spacing: 24f, padding: 0, expandChildren: false);
            UiFactory.Flexible(body, 1f);

            var listPanel = UiFactory.Panel(body, "NodeListPanel", UiFactory.PanelMid, stretch: false);
            listPanel.gameObject.AddComponent<LayoutElement>().preferredWidth = 520f;
            listPanel.GetComponent<LayoutElement>().flexibleHeight = 1f;
            m_nodeList = UiFactory.Group(listPanel, "Nodes");
            UiFactory.Stretch(m_nodeList, 8f);
            UiFactory.Vertical(m_nodeList, spacing: 2f, padding: 8, controlHeight: true);

            var detail = UiFactory.Panel(body, "Detail", UiFactory.PanelMid, stretch: false);
            var detailLayout = detail.gameObject.AddComponent<LayoutElement>();
            detailLayout.flexibleWidth = 1f;
            detailLayout.flexibleHeight = 1f;
            var detailColumn = UiFactory.Group(detail, "Column");
            UiFactory.Stretch(detailColumn, 16f);
            UiFactory.Vertical(detailColumn, spacing: 10f, padding: 8, align: TextAnchor.UpperLeft, controlHeight: true);

            m_nodeTitle = UiFactory.Label(detailColumn, "NodeTitle", "", 32f, TextAlignmentOptions.Left);
            m_nodeDescription = UiFactory.Label(detailColumn, "NodeDescription", "", 20f, TextAlignmentOptions.Left, UiFactory.TextDim);
            m_result = UiFactory.Label(detailColumn, "Result", "", 24f, TextAlignmentOptions.Left, UiFactory.Accent);
            m_actions = UiFactory.Group(detailColumn, "Actions");
            UiFactory.Vertical(m_actions, spacing: 8f, padding: 0, align: TextAnchor.UpperLeft, controlHeight: true);

            UiFactory.Button(background, "Abandon", "Abandon run", () => RunController.Instance?.AbandonRun(), UiFactory.PanelMid, 20f, 44f);
        }

        // ---------------------------------------------------------------- refresh

        public void Refresh()
        {
            if (!m_built) return;
            var run = CurrentRun;
            if (run == null || !run.IsActive || run.Act == null)
            {
                m_nodeTitle.text = "No active run";
                m_nodeDescription.text = "";
                m_result.text = "";
                UiFactory.DestroyChildren(m_actions);
                UiFactory.DestroyChildren(m_nodeList);
                return;
            }

            m_party.text = string.Join("    ", run.Party.Select(h => $"{h.Definition.displayName} {h.CurrentHealth}/{h.MaxHealth}"))
                           + $"    |    dice: {string.Join(", ", run.TraversalDice.Select(d => d.displayName))}";

            UiFactory.DestroyChildren(m_nodeList);
            for (int i = 0; i < run.Act.nodes.Count; i++)
            {
                var node = run.Act.nodes[i];
                // ASCII markers: the default TMP font (LiberationSans SDF) has no arrow or check glyphs.
                string marker = i == run.CurrentNodeIndex ? "> " : run.VisitedNodes.Contains(i) ? "* " : "   ";
                var color = i == run.CurrentNodeIndex ? UiFactory.Accent : run.VisitedNodes.Contains(i) ? UiFactory.TextLight : UiFactory.TextDim;
                UiFactory.Label(m_nodeList, $"Node{i}", $"{marker}{i}. {node.name}  <size=70%>{node.type}</size>", 20f, TextAlignmentOptions.Left, color, preferredHeight: 28f);
            }

            var current = run.CurrentNode;
            m_nodeTitle.text = $"{current.name}  <size=60%>({current.type})</size>";
            m_nodeDescription.text = current.description;

            UiFactory.DestroyChildren(m_actions);
            if (PendingTarget >= 0)
            {
                var target = run.Act.Node(PendingTarget);
                m_result.text = $"Rolled {LastTotal} → {target.name} ({target.type})";
                UiFactory.Button(m_actions, "Continue", $"Continue to {target.name}", OnContinueAfterRoll, UiFactory.Accent, 24f, 56f)
                    .GetComponentInChildren<TMP_Text>().color = Color.black;
                return;
            }

            m_result.text = m_awaitingRoll ? "Throw the dice…" : "";
            if (!run.CurrentNodeResolved)
            {
                BuildEnterAction(current);
            }
            else if (current.HasFork)
            {
                BuildForkActions(run, current.fork);
            }
            else if (current.nextNode >= 0)
            {
                var next = run.Act.Node(current.nextNode);
                UiFactory.Button(m_actions, "Travel", $"Travel → {next.name} ({next.type})", () => Travel(current.nextNode), UiFactory.Accent, 24f, 56f)
                    .GetComponentInChildren<TMP_Text>().color = Color.black;
            }
            else
            {
                UiFactory.Button(m_actions, "Finish", "Finish run", () => RunController.Instance.CompleteNode(), UiFactory.Accent, 24f, 56f);
            }
        }

        private void BuildEnterAction(MapNode node)
        {
            string text = node.type switch
            {
                NodeType.Combat => "Enter combat",
                NodeType.Elite => "Face the elite",
                NodeType.Boss => "Face the Boss",
                NodeType.Rest => "Rest (heal 30%)",
                NodeType.Treasure => "Open the treasure",
                _ => "Continue",
            };
            UiFactory.Button(m_actions, "Enter", text, EnterNode, UiFactory.Accent, 24f, 56f)
                .GetComponentInChildren<TMP_Text>().color = Color.black;
        }

        private void BuildForkActions(RunState run, Fork fork)
        {
            UiFactory.Label(m_actions, "ForkTitle", $"A fork: {fork.DiceCountLabel} dice. Choose which to commit.", 22f, TextAlignmentOptions.Left);

            // One toggle per owned traversal die.
            var diceRow = UiFactory.Group(m_actions, "DiceRow");
            UiFactory.Horizontal(diceRow, spacing: 8f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
            UiFactory.FixedHeight(diceRow, 48f);
            for (int i = 0; i < run.TraversalDice.Count; i++)
            {
                int index = i;
                var die = run.TraversalDice[i];
                bool on = m_committed.Contains(index);
                var toggle = UiFactory.Button(diceRow, $"Die{i}", $"{die.displayName} [{string.Join(" ", die.Faces.Select(f => f.Pips))}]", () => ToggleDie(index),
                    on ? UiFactory.Accent : UiFactory.PanelDark, 18f, 44f);
                toggle.gameObject.GetComponent<LayoutElement>().preferredWidth = 230f;
                toggle.interactable = !m_awaitingRoll;
            }

            // Paths with nominal odds for the committed dice.
            var committed = CommittedDice(run);
            bool countOk = fork.AllowsDiceCount(committed.Count);
            double[] odds = countOk ? ForkOdds.PathProbabilities(fork, committed) : null;
            for (int i = 0; i < fork.paths.Count; i++)
            {
                var path = fork.paths[i];
                var target = run.Act.Node(path.targetNode);
                string oddsText = odds != null ? $"{odds[i] * 100:0}%" : "—";
                UiFactory.Label(m_actions, $"Path{i}", $"  {path.RangeLabel(fork),-6} → {target.name} ({target.type})   {oddsText}", 20f, TextAlignmentOptions.Left, preferredHeight: 28f);
            }
            UiFactory.Label(m_actions, "OddsNote", "Nominal odds for fair dice; the throw decides.", 16f, TextAlignmentOptions.Left, UiFactory.TextDim, 22f);

            var roll = UiFactory.Button(m_actions, "Roll", m_awaitingRoll ? "Throwing…" : $"Roll {committed.Count} dice", StartForkRoll, UiFactory.Accent, 24f, 56f);
            roll.interactable = countOk && !m_awaitingRoll;
            roll.GetComponentInChildren<TMP_Text>().color = Color.black;
        }

        // ---------------------------------------------------------------- actions (public for tests)

        public void EnterNode()
        {
            RunController.Instance?.EnterCurrentNode();
            Refresh();
        }

        public void ToggleDie(int traversalDieIndex)
        {
            if (m_awaitingRoll) return;
            if (!m_committed.Remove(traversalDieIndex)) m_committed.Add(traversalDieIndex);
            Refresh();
        }

        public List<DieDefinition> CommittedDice(RunState run) =>
            m_committed.Where(i => i >= 0 && i < run.TraversalDice.Count).Select(i => run.TraversalDice[i]).ToList();

        /// <summary>Set up the committed dice in the box with the Traversal profile and open the overlay.</summary>
        public void StartForkRoll()
        {
            var run = CurrentRun;
            var node = run?.CurrentNode;
            if (node == null || !node.HasFork || m_awaitingRoll || DiceManager.Instance == null) return;
            var dice = CommittedDice(run);
            if (!node.fork.AllowsDiceCount(dice.Count)) return;

            var profile = RunController.Instance.TraversalProfile;
            if (profile != null) EventManager.TriggerEvent(GameEvents.SetDiceBoxProfile, profile);
            DiceManager.SetUpDice(dice);
            EventManager.TriggerEvent(GameEvents.OpenDiceBox);
            m_awaitingRoll = true;
            Refresh();
        }

        private void OnRollCompleted(RollCompletedEvent roll)
        {
            if (!m_awaitingRoll) return;
            var run = CurrentRun;
            var node = run?.CurrentNode;
            m_awaitingRoll = false;
            if (node == null || !node.HasFork) return;

            LastTotal = roll.TotalPips;
            var path = ForkResolver.Resolve(node.fork, LastTotal);
            PendingTarget = path?.targetNode ?? -1;
            Debug.Log($"MapScreen: fork roll {LastTotal} → node {PendingTarget}.");
            EventManager.TriggerEvent(GameEvents.DisableDiceLaunch);
            Refresh();
        }

        private void OnContinueAfterRoll()
        {
            int target = PendingTarget;
            PendingTarget = -1;
            m_committed.Clear();
            Travel(target);
        }

        private void Travel(int nodeIndex)
        {
            RunController.Instance?.TravelTo(nodeIndex);
            Refresh();
        }
    }
}
