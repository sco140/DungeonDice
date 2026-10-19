using System.Collections.Generic;
using System.Linq;
using DungeonDice.Combat;
using DungeonDice.Core;
using DungeonDice.Dice;
using DungeonDice.Run;
using GameUtility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDice.UI
{
    /// <summary>
    /// Renders a <see cref="CombatState"/> and drives the dice box for it. Enemy cards show HP,
    /// shield, statuses and the telegraphed intent; hero cards show HP, shield, the settled die and
    /// Use / Bank / signature buttons. Throws and re-throws are real drags in the box; the overlay
    /// closes once the dice settle so the player can act on the cards.
    /// </summary>
    public class CombatScreen : MonoBehaviour
    {
        private CombatState m_state;
        private readonly List<int> m_ownerToSlot = new();
        private readonly HashSet<int> m_rerollSelection = new();
        private bool m_built;
        private bool m_waitingForThrow;

        // Targeting: which die/signature is waiting for a click, and what kind of click.
        private int m_pendingSlot = -1;
        private TargetKind m_pendingKind = TargetKind.None;
        private bool m_pendingIsSignature;
        private int m_nudgeDelta = 1;
        private int m_loadedFace;

        private TMP_Text m_title, m_hint, m_mana, m_log;
        private RectTransform m_enemyRow, m_heroRow, m_actions;

        public CombatState State => m_state;
        public IReadOnlyList<int> OwnerToSlot => m_ownerToSlot;
        public bool WaitingForThrow => m_waitingForThrow;

        // ---------------------------------------------------------------- lifecycle

        private void OnEnable()
        {
            EventManager.Register(GameEvents.RollCompleted, new EventHandler<RollCompletedEvent>(OnRollCompleted));
            if (!m_built) Build();
            StartCombat();
        }

        private void OnDisable()
        {
            EventManager.Unregister<RollCompletedEvent>(GameEvents.RollCompleted, OnRollCompleted);
            Detach();
        }

        private void StartCombat()
        {
            var run = RunController.Instance;
            var node = run?.State.CurrentNode;
            if (run == null || node == null || !run.State.IsActive)
            {
                m_title.text = "No combat";
                return;
            }

            var enemies = run.EnemiesFor(node.type);
            if (enemies.Count == 0)
            {
                Debug.LogError("CombatScreen: no enemies configured; completing the node.");
                run.CompleteNode();
                return;
            }

            Detach();
            m_state = new CombatState(run.State.Party, enemies, run.State.Rng);
            m_state.Changed += Refresh;
            m_state.DieModified += OnDieModified;
            m_state.AllyRerollRequested += OnAllyRerollRequested;

            if (run.CombatProfile != null) EventManager.TriggerEvent(GameEvents.SetDiceBoxProfile, run.CombatProfile);
            m_title.text = $"{node.name}  <size=60%>({node.type})</size>";
            BeginThrow();
        }

        private void Detach()
        {
            if (m_state == null) return;
            m_state.Changed -= Refresh;
            m_state.DieModified -= OnDieModified;
            m_state.AllyRerollRequested -= OnAllyRerollRequested;
            m_state = null;
            m_waitingForThrow = false;
            ClearTargeting();
        }

        // ---------------------------------------------------------------- dice box

        /// <summary>Put the living heroes' dice in the box and open it for the throw.</summary>
        private void BeginThrow()
        {
            var dice = m_state.DiceToThrow(out var slots);
            m_ownerToSlot.Clear();
            m_ownerToSlot.AddRange(slots);
            DiceManager.SetUpDice(dice);
            EventManager.TriggerEvent(GameEvents.OpenDiceBox);
            m_waitingForThrow = true;
            Refresh();
        }

        private void OnRollCompleted(RollCompletedEvent roll)
        {
            if (m_state == null) return;
            var mapped = roll.Results
                .Where(r => r.OwnerId >= 0 && r.OwnerId < m_ownerToSlot.Count)
                .Select(r => (m_ownerToSlot[r.OwnerId], r));
            EventManager.TriggerEvent(GameEvents.DisableDiceLaunch);
            m_waitingForThrow = false;
            m_rerollSelection.Clear();
            m_state.ReceiveRoll(mapped);
        }

        private int OwnerFor(int slot) => m_ownerToSlot.IndexOf(slot);

        private DiceRollerController Roller() =>
            DiceManager.Instance != null ? DiceManager.Instance.GetComponentInChildren<DiceRollerController>(true) : null;

        /// <summary>Reroll phase: open the box with the selected dice armed for re-throw; the player drags.</summary>
        public void RerollSelected()
        {
            if (m_state == null || !m_state.CanReroll || m_rerollSelection.Count == 0) return;
            var owners = m_rerollSelection.Select(OwnerFor).Where(o => o >= 0).ToList();
            Roller()?.SetRerollSelection(owners);
            EventManager.TriggerEvent(GameEvents.OpenDiceBox);
            m_waitingForThrow = true;
            Refresh();
        }

        public void ToggleRerollDie(int slot)
        {
            if (!m_rerollSelection.Remove(slot)) m_rerollSelection.Add(slot);
            Refresh();
        }

        public void KeepAll() => m_state?.KeepAll();

        private void OnAllyRerollRequested(int slot)
        {
            int owner = OwnerFor(slot);
            if (owner < 0) return;
            Roller()?.SetRerollSelection(new[] { owner });
            EventManager.TriggerEvent(GameEvents.OpenDiceBox);
            m_waitingForThrow = true;
        }

        /// <summary>A modifier changed a result; turn the physical die to match (the result object is shared, so no re-apply).</summary>
        private void OnDieModified(int slot, IDieModifier modifier)
        {
            int owner = OwnerFor(slot);
            var die = m_state.Die(slot);
            if (owner < 0 || die == null || DiceManager.Instance == null) return;
            var handler = DiceManager.Instance.ActiveDice.FirstOrDefault(d => d.OwnerId == owner);
            handler?.ShowFace(die.Result.FaceIndex);
        }

        // ---------------------------------------------------------------- player actions (public for tests)

        public void Use(int slot)
        {
            if (m_state == null || !m_state.CanAct(slot)) return;
            var kind = m_state.TargetFor(slot);
            var face = m_state.Die(slot).Face;
            bool isNudge = face.Has(FaceEffectKind.Nudge);

            switch (kind)
            {
                case TargetKind.None:
                case TargetKind.Hero: // Shield / Taunt default to self
                    m_state.UseDie(slot);
                    break;
                case TargetKind.Enemy:
                    var living = m_state.LivingEnemies.ToList();
                    if (living.Count == 1) m_state.UseDie(slot, living[0].Index);
                    else BeginTargeting(slot, TargetKind.Enemy, false);
                    break;
                case TargetKind.Die:
                    if (isNudge) m_nudgeDelta = 1;
                    BeginTargeting(slot, TargetKind.Die, false);
                    break;
            }
        }

        public void UseNudge(int slot, int delta)
        {
            m_nudgeDelta = delta;
            BeginTargeting(slot, TargetKind.Die, false);
        }

        public void Bank(int slot) => m_state?.BankDie(slot);

        public void CastSignature(int slot)
        {
            if (m_state == null || !m_state.CanCastSignature(slot)) return;
            var kind = CombatState.SignatureTargetFor(m_state.Hero(slot).Run.Definition.signatureKind);
            switch (kind)
            {
                case TargetKind.None:
                    m_state.CastSignature(slot);
                    break;
                case TargetKind.Enemy:
                    var living = m_state.LivingEnemies.ToList();
                    if (living.Count == 1) m_state.CastSignature(slot, living[0].Index);
                    else BeginTargeting(slot, TargetKind.Enemy, true);
                    break;
                case TargetKind.Die:
                    m_loadedFace = 0; // choose a face first, then a die
                    BeginTargeting(slot, TargetKind.Die, true);
                    break;
            }
        }

        public void EndTurn()
        {
            ClearTargeting();
            m_state?.EndHeroTurn();
            if (m_state != null && m_state.Phase == CombatPhase.Throw) BeginThrow();
        }

        public void Continue()
        {
            if (m_state == null) return;
            if (m_state.Phase == CombatPhase.Won) RunController.Instance?.CompleteNode();
            else if (m_state.Phase == CombatPhase.Lost) RunController.Instance?.PartyWiped();
        }

        private void BeginTargeting(int slot, TargetKind kind, bool signature)
        {
            m_pendingSlot = slot;
            m_pendingKind = kind;
            m_pendingIsSignature = signature;
            Refresh();
        }

        public void ClearTargeting()
        {
            m_pendingSlot = -1;
            m_pendingKind = TargetKind.None;
            m_pendingIsSignature = false;
            m_loadedFace = 0;
        }

        public void CancelTargeting()
        {
            ClearTargeting();
            Refresh();
        }

        /// <summary>Click on an enemy card (index) or a hero card (slot, which is also its die).</summary>
        public void ChooseTarget(int target)
        {
            if (m_state == null || m_pendingSlot < 0) return;
            int slot = m_pendingSlot;
            bool signature = m_pendingIsSignature;
            var kind = m_pendingKind;
            if (kind == TargetKind.Die && signature && !DieDefinition.IsValidFaceIndex(m_loadedFace)) return; // pick a face first
            ClearTargeting();

            if (signature) m_state.CastSignature(slot, target, m_loadedFace);
            else m_state.UseDie(slot, target, m_nudgeDelta);
            Refresh();
        }

        public void ChooseLoadedFace(int faceIndex)
        {
            m_loadedFace = faceIndex;
            Refresh();
        }

        // ---------------------------------------------------------------- build & refresh

        private void Build()
        {
            m_built = true;
            UiFactory.Stretch((RectTransform)transform);
            var background = UiFactory.Panel(transform, "Background", UiFactory.PanelDark);
            UiFactory.Vertical(background, spacing: 10f, padding: 20);

            m_title = UiFactory.Label(background, "Title", "Combat", 36f, color: UiFactory.Accent, preferredHeight: 48f);
            m_hint = UiFactory.Label(background, "Hint", "", 22f, color: UiFactory.TextDim, preferredHeight: 32f);

            m_enemyRow = UiFactory.Group(background, "Enemies");
            UiFactory.Horizontal(m_enemyRow, spacing: 16f, padding: 0);
            UiFactory.FixedHeight(m_enemyRow, 150f);

            m_mana = UiFactory.Label(background, "Mana", "", 24f, preferredHeight: 34f);

            m_heroRow = UiFactory.Group(background, "Heroes");
            UiFactory.Horizontal(m_heroRow, spacing: 16f, padding: 0);
            UiFactory.FixedHeight(m_heroRow, 260f);

            m_actions = UiFactory.Group(background, "Actions");
            UiFactory.Horizontal(m_actions, spacing: 12f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
            UiFactory.FixedHeight(m_actions, 56f);

            var logPanel = UiFactory.Panel(background, "LogPanel", UiFactory.PanelMid, stretch: false);
            UiFactory.Flexible(logPanel, 1f);
            m_log = UiFactory.Label(logPanel, "Log", "", 17f, TextAlignmentOptions.BottomLeft, UiFactory.TextLight);
            UiFactory.Stretch(m_log.rectTransform, 10f);
        }

        private void Refresh()
        {
            if (!m_built || m_state == null) return;
            var state = m_state;

            m_mana.text = $"Mana   <color=#d04040>Red {state.Red}</color>   <color=#4070d0>Blue {state.Blue}</color>   <color=#d0b040>Yellow {state.Yellow}</color>     Turn {state.Turn}";
            m_log.text = string.Join("\n", state.Log.Skip(Mathf.Max(0, state.Log.Count - 9)));

            BuildEnemyCards(state);
            BuildHeroCards(state);
            BuildActions(state);
            m_hint.text = HintText(state);
        }

        private string HintText(CombatState state)
        {
            if (m_pendingSlot >= 0)
            {
                if (m_pendingKind == TargetKind.Die && m_pendingIsSignature && !DieDefinition.IsValidFaceIndex(m_loadedFace)) return "Loaded Dice: choose the face, then click a hero's die.";
                return m_pendingKind == TargetKind.Enemy ? "Click an enemy." : "Click a hero's die.";
            }
            if (state.PendingAllyReroll >= 0) return $"Drag in the box to re-throw {state.Hero(state.PendingAllyReroll).Name}'s die.";
            return state.Phase switch
            {
                CombatPhase.Throw => "Drag across the box to throw your dice.",
                CombatPhase.Reroll => m_waitingForThrow ? "Drag to re-throw the selected dice." : "Pick dice to re-throw (once per turn), or keep them all.",
                CombatPhase.Act => "Use each die's face, or bank it for 1 mana. Signatures cost 3 mana of the hero's colour.",
                CombatPhase.Won => "Victory. Continue to the map.",
                CombatPhase.Lost => "The party has fallen.",
                _ => "",
            };
        }

        private void BuildEnemyCards(CombatState state)
        {
            UiFactory.DestroyChildren(m_enemyRow);
            bool targeting = m_pendingSlot >= 0 && m_pendingKind == TargetKind.Enemy;
            foreach (var enemy in state.Enemies)
            {
                int index = enemy.Index;
                var tint = !enemy.IsAlive ? UiFactory.PanelLocked : targeting ? UiFactory.Accent : new Color(0.3f, 0.12f, 0.12f, 1f);
                var card = UiFactory.Button(m_enemyRow, $"Enemy{index}", "", () => ChooseTarget(index), tint, 18f, 150f);
                card.interactable = targeting && enemy.IsAlive;
                ReplaceLabelWithColumn(card, out var column);
                UiFactory.Label(column, "Name", enemy.Name, 24f);
                UiFactory.Label(column, "HP", enemy.IsAlive ? $"HP {enemy.Health}/{enemy.MaxHealth}{(enemy.Shield > 0 ? $"  Shield {enemy.Shield}" : "")}" : "Defeated", 20f);
                UiFactory.Label(column, "Status", enemy.Statuses.ToString(), 16f, color: UiFactory.TextDim);
                UiFactory.Label(column, "Intent", enemy.IsAlive && enemy.Intent != null ? $"Intends: {enemy.Intent.Describe()}" : "", 18f, color: UiFactory.Accent);
            }
        }

        private void BuildHeroCards(CombatState state)
        {
            UiFactory.DestroyChildren(m_heroRow);
            bool targetingDie = m_pendingSlot >= 0 && m_pendingKind == TargetKind.Die;
            foreach (var hero in state.Heroes)
            {
                int slot = hero.Slot;
                var die = state.Die(slot);
                bool canBeTarget = targetingDie && die != null && !die.Spent && hero.IsAlive
                                   && (m_pendingIsSignature || slot != m_pendingSlot);
                var tint = !hero.IsAlive ? UiFactory.PanelLocked : canBeTarget ? UiFactory.Accent : HeroTint(hero.Color);
                var card = UiFactory.Button(m_heroRow, $"Hero{slot}", "", () => ChooseTarget(slot), tint, 18f, 260f);
                card.interactable = canBeTarget;
                ReplaceLabelWithColumn(card, out var column);

                UiFactory.Label(column, "Name", hero.Name, 24f);
                UiFactory.Label(column, "HP", hero.IsAlive ? $"HP {hero.Health}/{hero.MaxHealth}{(hero.Shield > 0 ? $"  Shield {hero.Shield}" : "")}{(hero.Statuses.Has(StatusKind.Taunt) ? "  Taunting" : "")}" : "Fallen", 18f);
                UiFactory.Label(column, "Die", die != null ? $"Die: {die}" : hero.IsAlive ? "Die: —" : "", 18f, color: UiFactory.TextDim);

                if (die == null || die.Spent || !hero.IsAlive) continue;

                if (state.Phase == CombatPhase.Reroll && !m_waitingForThrow)
                {
                    bool selected = m_rerollSelection.Contains(slot);
                    UiFactory.Button(column, "RerollToggle", selected ? "Re-throw: yes" : "Re-throw: no", () => ToggleRerollDie(slot), selected ? UiFactory.Accent : UiFactory.PanelDark, 16f, 36f);
                }
                else if (state.Phase == CombatPhase.Act && m_pendingSlot < 0 && state.PendingAllyReroll < 0)
                {
                    var buttons = UiFactory.Group(column, "Buttons");
                    UiFactory.Horizontal(buttons, spacing: 6f, padding: 0);
                    UiFactory.FixedHeight(buttons, 36f);
                    if (die.Face.Has(FaceEffectKind.Nudge))
                    {
                        UiFactory.Button(buttons, "NudgeUp", "Nudge +1", () => UseNudge(slot, 1), UiFactory.PanelDark, 16f, 36f);
                        UiFactory.Button(buttons, "NudgeDown", "Nudge −1", () => UseNudge(slot, -1), UiFactory.PanelDark, 16f, 36f);
                    }
                    else
                    {
                        UiFactory.Button(buttons, "Use", $"Use {die.Face}", () => Use(slot), UiFactory.PanelDark, 16f, 36f);
                    }
                    UiFactory.Button(buttons, "Bank", $"Bank (+1 {hero.Color})", () => Bank(slot), UiFactory.PanelDark, 16f, 36f);

                    var definition = hero.Run.Definition;
                    if (definition.signatureKind != SignatureKind.None)
                    {
                        var sig = UiFactory.Button(column, "Signature", $"{definition.signatureName} (3 {hero.Color})", () => CastSignature(slot), UiFactory.PanelDark, 16f, 36f);
                        sig.interactable = state.CanCastSignature(slot);
                    }
                }
            }
        }

        private void BuildActions(CombatState state)
        {
            UiFactory.DestroyChildren(m_actions);
            void Add(string name, string text, System.Action onClick, Color? tint = null, bool interactable = true)
            {
                var b = UiFactory.Button(m_actions, name, text, onClick, tint ?? UiFactory.PanelMid, 20f, 48f);
                b.gameObject.GetComponent<LayoutElement>().preferredWidth = 240f;
                b.interactable = interactable;
            }

            if (m_pendingSlot >= 0)
            {
                if (m_pendingKind == TargetKind.Die && m_pendingIsSignature)
                {
                    for (int f = 1; f <= DieDefinition.FaceCount; f++)
                    {
                        int face = f;
                        var fb = UiFactory.Button(m_actions, $"Face{f}", f.ToString(), () => ChooseLoadedFace(face), m_loadedFace == f ? UiFactory.Accent : UiFactory.PanelMid, 20f, 48f);
                        fb.gameObject.GetComponent<LayoutElement>().preferredWidth = 48f;
                    }
                }
                Add("Cancel", "Cancel", CancelTargeting);
                return;
            }

            switch (state.Phase)
            {
                case CombatPhase.Reroll when !m_waitingForThrow:
                    Add("Reroll", $"Re-throw selected ({m_rerollSelection.Count})", RerollSelected, UiFactory.Accent, m_rerollSelection.Count > 0);
                    Add("Keep", "Keep all", KeepAll);
                    break;
                case CombatPhase.Act when state.PendingAllyReroll < 0:
                    Add("EndTurn", "End turn", EndTurn, UiFactory.Accent);
                    break;
                case CombatPhase.Won:
                case CombatPhase.Lost:
                    Add("Continue", state.Phase == CombatPhase.Won ? "Continue" : "Accept defeat", Continue, UiFactory.Accent);
                    break;
            }
            Add("Abandon", "Abandon run", () => RunController.Instance?.AbandonRun());
        }

        private static void ReplaceLabelWithColumn(Button card, out RectTransform column)
        {
            var label = card.GetComponentInChildren<TMP_Text>();
            if (label != null) Destroy(label.gameObject);
            column = UiFactory.Group(card.transform, "Column");
            UiFactory.Stretch(column, 8f);
            UiFactory.Vertical(column, spacing: 4f, padding: 6, controlHeight: true);
        }

        private static Color HeroTint(DieColor color) => color switch
        {
            DieColor.Red => new Color(0.4f, 0.14f, 0.14f, 1f),
            DieColor.Blue => new Color(0.14f, 0.2f, 0.4f, 1f),
            DieColor.Yellow => new Color(0.4f, 0.33f, 0.1f, 1f),
            _ => UiFactory.PanelMid,
        };
    }
}
