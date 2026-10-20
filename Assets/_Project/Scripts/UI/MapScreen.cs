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
    /// drawn edge, fork edges carry their bracket and (once dice are committed) the nominal odds.
    /// The current node is highlighted; the next node is clickable when travel is possible. Below the
    /// graph, a detail panel offers the current node's action: Enter, or the fork's dice toggles and
    /// Roll, which opens the dice box with the Traversal profile. The settled total picks the path.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        private const float NodeWidth = 170f;
        private const float NodeHeight = 70f;

        private readonly List<int> m_committed = new();   // indices into RunState.TraversalDice
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

        // Loaded Dice on the map: pick one rolled die, then a face; once per fork.
        private bool m_loadedDiceMode;
        private int m_loadedDieOwner = -1;
        private bool m_loadedDiceUsedThisFork;
        private string m_loadedDiceNote;

        /// <summary>Node the last fork roll chose; -1 when no roll is pending travel.</summary>
        public int PendingTarget { get; private set; } = -1;
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
                           + $"    |    traversal dice: {string.Join(", ", run.TraversalDice.Select(d => d.displayName))}";

            var current = run.CurrentNode;
            m_nodeTitle.text = $"{current.name}  <size=60%>({current.type})</size>";
            m_nodeDescription.text = current.description;

            BuildGraph(run);

            UiFactory.DestroyChildren(m_actions);
            if (PendingTarget >= 0)
            {
                var target = run.Act.Node(PendingTarget);
                m_result.text = $"Rolled {LastTotal} → {target.name} ({target.type}){(m_loadedDiceNote != null ? $"    {m_loadedDiceNote}" : "")}";
                m_graphHint.text = $"Click {target.name} or Continue to travel.";
                if (m_loadedDiceMode)
                {
                    BuildLoadedDiceActions(run);
                    return;
                }
                UiFactory.Button(m_actions, "Continue", $"Continue to {target.name}", OnContinueAfterRoll, UiFactory.Accent, 22f, 50f)
                    .GetComponentInChildren<TMP_Text>().color = Color.black;
                if (CanOfferLoadedDice())
                {
                    var caster = RunController.Instance.LoadedDiceCaster;
                    UiFactory.Button(m_actions, "LoadedDice", $"{caster.Definition.displayName}: Loaded Dice (3 Yellow, {run.CarriedYellow} carried) — change one rolled die to any face",
                        BeginLoadedDice, new Color(0.4f, 0.33f, 0.1f, 1f), 20f, 46f);
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
                m_graphHint.text = "A fork: commit dice and roll. The total picks the path.";
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
            var chosenEdge = PendingTarget >= 0 ? (run.CurrentNodeIndex, PendingTarget) : (-1, -1);
            var committed = CommittedDice(run);
            double[] odds = run.CurrentNode.HasFork && run.CurrentNode.fork.AllowsDiceCount(committed.Count)
                ? ForkOdds.PathProbabilities(run.CurrentNode.fork, committed) : null;

            // Edges first so nodes draw on top.
            foreach (var (from, to, bracket) in MapLayout.Edges(run.Act))
            {
                var a = Position(m_layout.For(from));
                var b = Position(m_layout.For(to));
                bool chosen = (from, to) == chosenEdge;
                bool fromCurrent = from == run.CurrentNodeIndex && run.CurrentNodeResolved;
                var color = chosen ? UiFactory.Accent : fromCurrent ? new Color(0.85f, 0.75f, 0.45f, 0.9f) : run.VisitedNodes.Contains(from) && run.VisitedNodes.Contains(to) ? new Color(0.6f, 0.6f, 0.6f, 0.8f) : new Color(0.35f, 0.35f, 0.4f, 0.8f);
                DrawEdge(a, b, chosen ? 6f : 3f, color);

                if (bracket != null)
                {
                    string label = bracket.RangeLabel(run.CurrentNode.HasFork && from == run.CurrentNodeIndex ? run.CurrentNode.fork : run.Act.nodes[from].fork);
                    if (odds != null && from == run.CurrentNodeIndex)
                    {
                        int pathIndex = run.CurrentNode.fork.paths.IndexOf(bracket);
                        if (pathIndex >= 0) label += $"  {odds[pathIndex] * 100:0}%";
                    }
                    DrawEdgeLabel(Vector2.Lerp(a, b, 0.45f), label, fromCurrent || chosen ? UiFactory.Accent : UiFactory.TextDim);
                }
            }

            foreach (var cell in m_layout.Cells)
            {
                int index = cell.Node;
                var node = run.Act.nodes[index];
                bool isCurrent = index == run.CurrentNodeIndex;
                bool visited = run.VisitedNodes.Contains(index);
                bool clickable = index == next || index == PendingTarget;
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
            panel.sizeDelta = new Vector2(96f, 26f);
            panel.anchoredPosition = at;
            var label = UiFactory.Label(panel, "Text", text, 15f, TextAlignmentOptions.Center, color);
            UiFactory.Stretch(label.rectTransform, 2f);
        }

        private void OnNodeClicked(int index)
        {
            var run = CurrentRun;
            if (run == null) return;
            if (PendingTarget >= 0 && index == PendingTarget) OnContinueAfterRoll();
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
            UiFactory.Label(m_actions, "ForkTitle", $"{fork.DiceCountLabel} dice. Click dice to commit; the edges show each path's odds.", 19f, TextAlignmentOptions.Left);

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
            UiFactory.Label(m_actions, "OddsNote", countOk ? "Nominal odds for fair dice; the throw decides." : $"Commit {fork.minDice}{(fork.maxDice != fork.minDice ? $"–{fork.maxDice}" : "")} dice.", 15f, TextAlignmentOptions.Left, UiFactory.TextDim, 20f);

            var roll = UiFactory.Button(m_actions, "Roll", m_awaitingRoll ? "Throwing…" : $"Roll {committed.Count} dice", StartForkRoll, UiFactory.Accent, 22f, 50f);
            roll.interactable = countOk && !m_awaitingRoll;
            roll.GetComponentInChildren<TMP_Text>().color = Color.black;
        }

        // ---------------------------------------------------------------- Loaded Dice on the map

        private bool CanOfferLoadedDice() =>
            !m_loadedDiceUsedThisFork && RunController.Instance != null && RunController.Instance.CanCastLoadedDiceOnMap
            && DiceManager.Instance != null && DiceManager.Instance.State == DiceBoxState.Settled && DiceManager.Instance.LastRoll != null;

        public void BeginLoadedDice()
        {
            if (!CanOfferLoadedDice()) return;
            m_loadedDiceMode = true;
            m_loadedDieOwner = -1;
            Refresh();
        }

        public void CancelLoadedDice()
        {
            m_loadedDiceMode = false;
            m_loadedDieOwner = -1;
            Refresh();
        }

        public void SelectLoadedDie(int ownerId)
        {
            m_loadedDieOwner = ownerId;
            Refresh();
        }

        /// <summary>What the total and path would be if the selected die showed <paramref name="faceIndex"/>.</summary>
        private (int total, MapNode target) PreviewLoaded(RunState run, RollResult result, int faceIndex)
        {
            int total = LastTotal - result.Pips + result.Die.Pips(faceIndex);
            var path = ForkResolver.Resolve(run.CurrentNode.fork, total);
            return (total, path != null ? run.Act.Node(path.targetNode) : null);
        }

        private void BuildLoadedDiceActions(RunState run)
        {
            var roll = DiceManager.Instance.LastRoll;
            UiFactory.Label(m_actions, "LoadedTitle", m_loadedDieOwner < 0 ? "Loaded Dice: pick the die to change." : "Loaded Dice: pick its new face.", 20f, TextAlignmentOptions.Left, UiFactory.Accent);

            var diceRow = UiFactory.Group(m_actions, "LoadedDice");
            UiFactory.Horizontal(diceRow, spacing: 8f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
            UiFactory.FixedHeight(diceRow, 46f);
            foreach (var result in roll.Results)
            {
                int owner = result.OwnerId;
                bool selected = owner == m_loadedDieOwner;
                var b = UiFactory.Button(diceRow, $"Loaded{owner}", $"{result.Die.displayName}: {result.Pips}", () => SelectLoadedDie(owner), selected ? UiFactory.Accent : UiFactory.PanelDark, 18f, 42f);
                b.gameObject.GetComponent<LayoutElement>().preferredWidth = 220f;
                if (selected) b.GetComponentInChildren<TMP_Text>().color = Color.black;
            }

            if (m_loadedDieOwner >= 0)
            {
                var result = roll.Results.First(r => r.OwnerId == m_loadedDieOwner);
                var faceRow = UiFactory.Group(m_actions, "LoadedFaces");
                UiFactory.Horizontal(faceRow, spacing: 8f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
                UiFactory.FixedHeight(faceRow, 46f);
                for (int f = 1; f <= DieDefinition.FaceCount; f++)
                {
                    int face = f;
                    var (total, target) = PreviewLoaded(run, result, face);
                    var b = UiFactory.Button(faceRow, $"Face{f}", $"{result.Die.Pips(face)}: total {total} → {(target != null ? target.name : "?")}", () => ApplyLoadedDice(m_loadedDieOwner, face),
                        face == result.FaceIndex ? UiFactory.PanelMid : UiFactory.PanelDark, 16f, 42f);
                    b.gameObject.GetComponent<LayoutElement>().preferredWidth = 290f;
                    b.interactable = face != result.FaceIndex;
                }
            }
            UiFactory.Button(m_actions, "CancelLoaded", "Cancel", CancelLoadedDice, UiFactory.PanelMid, 18f, 42f);
        }

        /// <summary>
        /// Cast Loaded Dice on the settled fork roll: pay 3 carried Yellow, set one die to a face (the box
        /// turns the physical die), then re-resolve the path from the new total. Once per fork.
        /// </summary>
        public bool ApplyLoadedDice(int ownerId, int faceIndex)
        {
            var run = CurrentRun;
            var node = run?.CurrentNode;
            var box = DiceManager.Instance;
            if (PendingTarget < 0 || node == null || !node.HasFork || !CanOfferLoadedDice()) return false;
            if (box.GetResult(ownerId) == null || !DieDefinition.IsValidFaceIndex(faceIndex)) return false;
            if (!RunController.Instance.PayLoadedDiceOnMap()) return false;

            var result = DiceManager.ApplyModifier(ownerId, new ChooseResultModifier(faceIndex));
            LastTotal = box.LastRoll.TotalPips;
            var path = ForkResolver.Resolve(node.fork, LastTotal);
            if (path != null) PendingTarget = path.targetNode;
            m_loadedDiceUsedThisFork = true;
            m_loadedDiceMode = false;
            m_loadedDieOwner = -1;
            m_loadedDiceNote = result != null ? $"Loaded Dice: {result.Die.displayName} set to {result.Pips}." : "Loaded Dice cast.";
            Debug.Log($"MapScreen: {m_loadedDiceNote} New total {LastTotal} → node {PendingTarget}.");
            Refresh();
            return true;
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
            var roller = DiceManager.Instance.GetComponentInChildren<DiceRollerController>(true);
            if (roller != null) roller.SetHint($"Fork at {node.name}: drag across the box to throw {dice.Count} {(dice.Count == 1 ? "die" : "dice")}");
            EventManager.TriggerEvent(GameEvents.OpenDiceBox);
            m_awaitingRoll = true;
            m_loadedDiceUsedThisFork = false;
            m_loadedDiceMode = false;
            m_loadedDieOwner = -1;
            m_loadedDiceNote = null;
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
            m_loadedDiceNote = null;
            m_loadedDiceMode = false;
            Travel(target);
        }

        private void Travel(int nodeIndex)
        {
            RunController.Instance?.TravelTo(nodeIndex);
            Refresh();
        }
    }
}
