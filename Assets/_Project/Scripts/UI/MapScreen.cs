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
    /// The act as a left-to-right graph: every node is a card with its type icon, every exit is a
    /// drawn edge, fork edges carry their threshold ("9+") and, once dice are committed, the chance of
    /// unlocking that path. The current node is highlighted; clickable nodes are where travel is
    /// possible. Below the graph, a detail panel offers the current node's action: Enter, or the
    /// fork's dice toggles and Roll, which opens the dice box with the Traversal profile. The settled
    /// total unlocks every path at or below it and the player picks one of them.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        private const float NodeWidth = 170f;
        private const float NodeHeight = 70f;

        private readonly List<int> m_committed = new();   // indices into RunState.TraversalDice
        private readonly List<int> m_unlocked = new();    // node indices the last fork roll opened
        private readonly Dictionary<int, Button> m_nodeButtons = new();
        private TMP_Text m_party;
        private TMP_Text m_nodeTitle;
        private TMP_Text m_nodeDescription;
        private TMP_Text m_result;
        private TMP_Text m_graphHint;
        private RectTransform m_graph;
        private RectTransform m_actions;
        private ActDefinition m_layoutAct;
        private MapLayout.Result m_layout;
        private bool m_built;
        private bool m_awaitingRoll;

        /// <summary>Nodes the last fork roll unlocked; empty when no roll is pending travel.</summary>
        public IReadOnlyList<int> UnlockedTargets => m_unlocked;
        public bool HasPendingChoice => m_unlocked.Count > 0;
        public int LastTotal { get; private set; }
        public bool AwaitingRoll => m_awaitingRoll;
        public IReadOnlyList<int> CommittedIndices => m_committed;
        public IReadOnlyDictionary<int, Button> NodeButtons => m_nodeButtons;

        private static RunState CurrentRun => RunController.Instance != null ? RunController.Instance.State : null;
        private static IconLibrary Icons => RunController.CurrentIcons;

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
            UiFactory.Vertical(background, spacing: 10f, padding: 20);

            UiFactory.Label(background, "Title", "The Map", 36f, color: UiFactory.Accent, preferredHeight: 46f);
            m_party = UiFactory.Label(background, "Party", "", 20f, color: UiFactory.TextDim, preferredHeight: 30f);

            var graphPanel = UiFactory.Panel(background, "GraphPanel", UiFactory.PanelMid, stretch: false);
            UiFactory.Flexible(graphPanel, 1f);
            m_graph = UiFactory.Group(graphPanel, "Graph");
            UiFactory.Stretch(m_graph, 16f);
            m_graphHint = UiFactory.Label(graphPanel, "GraphHint", "", 18f, TextAlignmentOptions.TopRight, UiFactory.TextDim);
            UiFactory.Stretch(m_graphHint.rectTransform, 10f);

            var detail = UiFactory.Panel(background, "Detail", UiFactory.PanelMid, stretch: false);
            UiFactory.FixedHeight(detail, 300f);
            var detailColumn = UiFactory.Group(detail, "Column");
            UiFactory.Stretch(detailColumn, 14f);
            UiFactory.Vertical(detailColumn, spacing: 8f, padding: 4, align: TextAnchor.UpperLeft, controlHeight: true);

            m_nodeTitle = UiFactory.Label(detailColumn, "NodeTitle", "", 30f, TextAlignmentOptions.Left);
            m_nodeDescription = UiFactory.Label(detailColumn, "NodeDescription", "", 19f, TextAlignmentOptions.Left, UiFactory.TextDim);
            m_result = UiFactory.Label(detailColumn, "Result", "", 22f, TextAlignmentOptions.Left, UiFactory.Accent);
            m_actions = UiFactory.Group(detailColumn, "Actions");
            UiFactory.Vertical(m_actions, spacing: 6f, padding: 0, align: TextAnchor.UpperLeft, controlHeight: true);

            UiFactory.Button(background, "Abandon", "Abandon run", () => RunController.Instance?.AbandonRun(), UiFactory.PanelMid, 18f, 40f);
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
                UiFactory.DestroyChildren(m_graph);
                m_nodeButtons.Clear();
                return;
            }

            m_party.text = string.Join("    ", run.Party.Select(h => $"{h.Definition.displayName} {h.CurrentHealth}/{h.MaxHealth}"))
                           + $"    |    {run.ResourceDiceCount} resource dice    |    traversal dice: {string.Join(", ", run.TraversalDice.Select(d => d.displayName))}";

            var current = run.CurrentNode;
            m_nodeTitle.text = $"{current.name}  <size=60%>({current.type})</size>";
            m_nodeDescription.text = current.description;

            BuildGraph(run);

            UiFactory.DestroyChildren(m_actions);
            if (HasPendingChoice)
            {
                var names = m_unlocked.Select(i => run.Act.Node(i).name).ToList();
                m_result.text = $"Rolled {LastTotal}: {(m_unlocked.Count == 1 ? "one path open" : $"{m_unlocked.Count} paths open")} — {string.Join(", ", names)}.";
                m_graphHint.text = m_unlocked.Count == 1 ? $"Click {names[0]} or Continue to travel." : "Click one of the open nodes, or a button below.";
                foreach (var target in m_unlocked)
                {
                    int index = target;
                    var node = run.Act.Node(index);
                    var path = current.fork.paths.First(p => p.targetNode == index);
                    var b = UiFactory.Button(m_actions, $"Go{index}", $"{(m_unlocked.Count == 1 ? "Continue to" : "Travel to")} {node.name} ({node.type}, {path.Label(current.fork)})", () => ChooseTarget(index), UiFactory.Accent, 22f, 50f);
                    b.GetComponentInChildren<TMP_Text>().color = Color.black;
                }
                return;
            }

            m_result.text = m_awaitingRoll ? "Throw the dice…" : run.LastNodeSummary ?? "";
            if (!run.CurrentNodeResolved)
            {
                m_graphHint.text = "Resolve this node to open its exits.";
                BuildEnterAction(current);
            }
            else if (current.HasFork)
            {
                m_graphHint.text = "A fork: commit dice and roll. The total unlocks every path at or below it; you choose.";
                BuildForkActions(run, current.fork);
            }
            else if (current.nextNode >= 0)
            {
                var next = run.Act.Node(current.nextNode);
                m_graphHint.text = $"Click {next.name} or Travel to move on.";
                UiFactory.Button(m_actions, "Travel", $"Travel → {next.name} ({next.type})", () => Travel(current.nextNode), UiFactory.Accent, 22f, 50f)
                    .GetComponentInChildren<TMP_Text>().color = Color.black;
            }
            else
            {
                m_graphHint.text = "";
                UiFactory.Button(m_actions, "Finish", "Finish run", () => RunController.Instance.CompleteNode(), UiFactory.Accent, 22f, 50f);
            }
        }

        // ---------------------------------------------------------------- graph

        private void BuildGraph(RunState run)
        {
            if (m_layoutAct != run.Act)
            {
                m_layoutAct = run.Act;
                m_layout = MapLayout.Compute(run.Act);
            }

            UiFactory.DestroyChildren(m_graph);
            m_nodeButtons.Clear();
            Canvas.ForceUpdateCanvases();
            var rect = m_graph.rect;
            if (rect.width < 10f || rect.height < 10f) rect = new Rect(0f, 0f, 1800f, 440f); // first frame before layout: use a sane size

            Vector2 Position(MapLayout.Cell cell)
            {
                float xStep = m_layout.Columns > 1 ? (rect.width - NodeWidth) / (m_layout.Columns - 1) : 0f;
                int inColumn = m_layout.Cells.Count(c => c.Column == cell.Column);
                float rowStep = Mathf.Min(NodeHeight + 24f, inColumn > 1 ? (rect.height - NodeHeight) / (inColumn - 1) : 0f);
                float x = -rect.width / 2f + NodeWidth / 2f + cell.Column * xStep;
                float y = (cell.Row - (inColumn - 1) / 2f) * -rowStep;
                return new Vector2(x, y);
            }

            int next = NextNode(run);
            var committed = CommittedDice(run);
            double[] odds = run.CurrentNode.HasFork && run.CurrentNode.fork.AllowsDiceCount(committed.Count)
                ? ForkOdds.PathProbabilities(run.CurrentNode.fork, committed) : null;

            // Edges first so nodes draw on top.
            foreach (var (from, to, path) in MapLayout.Edges(run.Act))
            {
                var a = Position(m_layout.For(from));
                var b = Position(m_layout.For(to));
                bool fromCurrent = from == run.CurrentNodeIndex && run.CurrentNodeResolved;
                bool open = fromCurrent && m_unlocked.Contains(to);
                bool closed = fromCurrent && HasPendingChoice && !open;
                var color = open ? UiFactory.Accent
                    : closed ? new Color(0.3f, 0.3f, 0.34f, 0.8f)
                    : fromCurrent ? new Color(0.85f, 0.75f, 0.45f, 0.9f)
                    : run.VisitedNodes.Contains(from) && run.VisitedNodes.Contains(to) ? new Color(0.6f, 0.6f, 0.6f, 0.8f)
                    : new Color(0.35f, 0.35f, 0.4f, 0.8f);
                DrawEdge(a, b, open ? 6f : 3f, color);

                if (path != null)
                {
                    var fork = run.Act.nodes[from].fork;
                    string label = path.Label(fork);
                    if (odds != null && from == run.CurrentNodeIndex && !HasPendingChoice)
                    {
                        int pathIndex = fork.paths.IndexOf(path);
                        if (pathIndex >= 0) label += $"  {odds[pathIndex] * 100:0}%";
                    }
                    else if (HasPendingChoice && from == run.CurrentNodeIndex)
                    {
                        label += open ? "  open" : "  locked";
                    }
                    // Edges that skip a column pass over other cards; keep their label near the source.
                    bool longEdge = m_layout.For(to).Column - m_layout.For(from).Column > 1;
                    DrawEdgeLabel(Vector2.Lerp(a, b, longEdge ? 0.22f : 0.45f), label, open || (fromCurrent && !closed) ? UiFactory.Accent : UiFactory.TextDim);
                }
            }

            foreach (var cell in m_layout.Cells)
            {
                int index = cell.Node;
                var node = run.Act.nodes[index];
                bool isCurrent = index == run.CurrentNodeIndex;
                bool visited = run.VisitedNodes.Contains(index);
                bool clickable = index == next || m_unlocked.Contains(index);
                var tint = isCurrent ? UiFactory.Accent : clickable ? new Color(0.5f, 0.42f, 0.2f, 1f) : visited ? new Color(0.3f, 0.3f, 0.34f, 1f) : UiFactory.PanelDark;

                var button = UiFactory.Button(m_graph, $"Node{index}", "", () => OnNodeClicked(index), tint, 16f, NodeHeight);
                var rt = button.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(NodeWidth, NodeHeight);
                rt.anchoredPosition = Position(cell);
                button.interactable = clickable;
                var old = button.GetComponentInChildren<TMP_Text>();
                if (old != null) Destroy(old.gameObject);

                var row = UiFactory.Group(button.transform, "Row");
                UiFactory.Stretch(row, 6f);
                UiFactory.Horizontal(row, spacing: 6f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
                UiFactory.Icon(row, "Icon", Icons?.ForNode(node.type), 40f, isCurrent ? Color.black : visited || clickable ? Color.white : UiFactory.TextDim);
                var textColor = isCurrent ? Color.black : visited || clickable ? UiFactory.TextLight : UiFactory.TextDim;
                var label = UiFactory.Label(row, "Name", $"{node.name}\n<size=70%>{node.type}</size>", 16f, TextAlignmentOptions.MidlineLeft, textColor);
                label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                m_nodeButtons[index] = button;
            }
        }

        private int NextNode(RunState run)
        {
            var current = run.CurrentNode;
            if (!run.CurrentNodeResolved || current.HasFork) return -1;
            return current.nextNode;
        }

        private void DrawEdge(Vector2 a, Vector2 b, float thickness, Color color)
        {
            var go = new GameObject("Edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(m_graph, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            var delta = b - a;
            rt.sizeDelta = new Vector2(delta.magnitude, thickness);
            rt.anchoredPosition = a;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void DrawEdgeLabel(Vector2 at, string text, Color color)
        {
            var panel = UiFactory.Panel(m_graph, "EdgeLabel", new Color(0.1f, 0.1f, 0.12f, 0.9f), stretch: false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(104f, 26f);
            panel.anchoredPosition = at;
            var label = UiFactory.Label(panel, "Text", text, 15f, TextAlignmentOptions.Center, color);
            UiFactory.Stretch(label.rectTransform, 2f);
        }

        private void OnNodeClicked(int index)
        {
            var run = CurrentRun;
            if (run == null) return;
            if (m_unlocked.Contains(index)) ChooseTarget(index);
            else if (index == NextNode(run)) Travel(index);
        }

        // ---------------------------------------------------------------- detail actions

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
            UiFactory.Button(m_actions, "Enter", text, EnterNode, UiFactory.Accent, 22f, 50f)
                .GetComponentInChildren<TMP_Text>().color = Color.black;
        }

        private void BuildForkActions(RunState run, Fork fork)
        {
            UiFactory.Label(m_actions, "ForkTitle", $"{fork.DiceCountLabel} dice. Click dice to commit; each edge shows its threshold and the chance of reaching it.", 19f, TextAlignmentOptions.Left);

            var diceRow = UiFactory.Group(m_actions, "DiceRow");
            UiFactory.Horizontal(diceRow, spacing: 8f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
            UiFactory.FixedHeight(diceRow, 48f);
            for (int i = 0; i < run.TraversalDice.Count; i++)
            {
                int index = i;
                var die = run.TraversalDice[i];
                bool on = m_committed.Contains(index);
                var toggle = UiFactory.Button(diceRow, $"Die{i}", "", () => ToggleDie(index), on ? UiFactory.Accent : UiFactory.PanelDark, 16f, 44f);
                toggle.gameObject.GetComponent<LayoutElement>().preferredWidth = 250f;
                toggle.interactable = !m_awaitingRoll;
                var old = toggle.GetComponentInChildren<TMP_Text>();
                if (old != null) Destroy(old.gameObject);
                var row = UiFactory.Group(toggle.transform, "Row");
                UiFactory.Stretch(row, 4f);
                UiFactory.Horizontal(row, spacing: 4f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
                UiFactory.Icon(row, "Icon", Icons?.number, 28f, on ? Color.black : Color.white);
                var label = UiFactory.Label(row, "Text", $"{die.displayName}  <size=80%>[{string.Join(" ", die.Faces.Select(f => f.Pips))}]</size>", 16f, TextAlignmentOptions.MidlineLeft, on ? Color.black : UiFactory.TextLight);
                label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            }

            var committed = CommittedDice(run);
            bool countOk = fork.AllowsDiceCount(committed.Count);
            UiFactory.Label(m_actions, "OddsNote", countOk ? "Nominal odds for fair dice; the throw decides. Higher totals keep the lower paths open too." : $"Commit {fork.minDice}{(fork.maxDice != fork.minDice ? $"–{fork.maxDice}" : "")} dice.", 15f, TextAlignmentOptions.Left, UiFactory.TextDim, 20f);

            var roll = UiFactory.Button(m_actions, "Roll", m_awaitingRoll ? "Throwing…" : $"Roll {committed.Count} dice", StartForkRoll, UiFactory.Accent, 22f, 50f);
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
            if (node == null || !node.HasFork || m_awaitingRoll || HasPendingChoice || DiceManager.Instance == null) return;
            var dice = CommittedDice(run);
            if (!node.fork.AllowsDiceCount(dice.Count)) return;

            var profile = RunController.Instance.TraversalProfile;
            if (profile != null) EventManager.TriggerEvent(GameEvents.SetDiceBoxProfile, profile);
            DiceManager.SetUpDice(dice);
            var roller = DiceManager.Instance.GetComponentInChildren<DiceRollerController>(true);
            if (roller != null) roller.SetHint($"Fork at {node.name}: drag across the box to throw {dice.Count} {(dice.Count == 1 ? "die" : "dice")}");
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
            m_unlocked.Clear();
            m_unlocked.AddRange(ForkResolver.UnlockedTargets(node.fork, LastTotal));
            Debug.Log($"MapScreen: fork roll {LastTotal} unlocks nodes {string.Join(", ", m_unlocked)}.");
            EventManager.TriggerEvent(GameEvents.DisableDiceLaunch);
            Refresh();
        }

        /// <summary>Travel along one of the paths the roll unlocked.</summary>
        public void ChooseTarget(int nodeIndex)
        {
            if (!m_unlocked.Contains(nodeIndex)) return;
            m_unlocked.Clear();
            m_committed.Clear();
            Travel(nodeIndex);
        }

        private void Travel(int nodeIndex)
        {
            RunController.Instance?.TravelTo(nodeIndex);
            Refresh();
        }
    }
}
