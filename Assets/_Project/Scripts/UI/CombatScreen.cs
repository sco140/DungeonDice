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
    /// Renders a <see cref="CombatState"/> and drives the dice box for it. Each turn the party's
    /// resource dice are thrown in the box (a real drag); once they settle the overlay closes and the
    /// dice row shows each die's colour while the pool shows the mana. Hero cards carry their three
    /// ability buttons; abilities that need a target put the screen in targeting mode (click an enemy
    /// card or a die chip; Recolour then asks for the new colour). Unit cards are built once per
    /// fight and updated in place so hit flashes and floating numbers from
    /// <see cref="CombatState.UnitEvent"/> can play on them.
    /// </summary>
    public class CombatScreen : MonoBehaviour
    {
        /// <summary>One hero or enemy card: fixed parts updated in place, a Body rebuilt on refresh.</summary>
        private sealed class UnitCard
        {
            public Button Button;
            public Image Image;
            public CardFlash Flash;
            public TMP_Text Name;
            public BarView Bar;
            public RectTransform StatusRow;
            public RectTransform Body;
            public Color BaseColor;
        }

        private CombatState m_state;
        private readonly HashSet<int> m_rerollSelection = new();
        private readonly Dictionary<int, UnitCard> m_heroCards = new();
        private readonly Dictionary<int, UnitCard> m_enemyCards = new();
        private bool m_built;
        private bool m_waitingForThrow;

        // Targeting: which ability is waiting for a click, and on what.
        private int m_pendingSlot = -1;
        private int m_pendingAbility = -1;
        private TargetKind m_pendingKind = TargetKind.None;
        /// <summary>Recolour: the die chosen, waiting for a colour. -1 until a die is clicked.</summary>
        private int m_pendingDie = -1;

        private TMP_Text m_title, m_hint, m_log;
        private RectTransform m_enemyRow, m_heroRow, m_actions, m_manaRow, m_diceRow;

        public CombatState State => m_state;
        public bool WaitingForThrow => m_waitingForThrow;
        public bool IsTargeting => m_pendingSlot >= 0;

        private static IconLibrary Icons => RunController.CurrentIcons;

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
            if (enemies.Count == 0 || run.ResourceDie == null)
            {
                Debug.LogError($"CombatScreen: {(enemies.Count == 0 ? "no enemies configured" : "no resource die configured")}; completing the node.");
                run.CompleteNode();
                return;
            }

            Detach();
            var rules = run.Rules.Clone();
            rules.resourceDiceCount = run.State.ResourceDiceCount; // Treasure may have added dice this run
            m_state = new CombatState(run.State.Party, enemies, run.ResourceDie, rules, run.State.Rng);
            m_state.Changed += Refresh;
            m_state.DieRecolored += OnDieRecolored;
            m_state.DiceRerollRequested += OnDiceRerollRequested;
            m_state.UnitEvent += OnUnitEvent;

            if (run.CombatProfile != null) EventManager.TriggerEvent(GameEvents.SetDiceBoxProfile, run.CombatProfile);
            m_title.text = $"{node.name}  <size=60%>({node.type})</size>";
            BuildCards();
            BeginThrow();
        }

        private void Detach()
        {
            if (m_state == null) return;
            m_state.Changed -= Refresh;
            m_state.DieRecolored -= OnDieRecolored;
            m_state.DiceRerollRequested -= OnDiceRerollRequested;
            m_state.UnitEvent -= OnUnitEvent;
            m_state = null;
            m_waitingForThrow = false;
            ClearTargeting();
        }

        // ---------------------------------------------------------------- dice box

        private void BeginThrow()
        {
            var dice = m_state.DiceToThrow(m_state.Rules.resourceDiceCount);
            DiceManager.SetUpDice(dice);
            Roller()?.SetHint($"Turn {m_state.Turn}: drag across the box to throw the party's {dice.Count} resource dice (1–2 Red, 3–4 Blue, 5–6 Yellow)");
            EventManager.TriggerEvent(GameEvents.OpenDiceBox);
            m_waitingForThrow = true;
            Refresh();
        }

        /// <summary>Box owner ids are the die indices, so results map straight through.</summary>
        private void OnRollCompleted(RollCompletedEvent roll)
        {
            if (m_state == null) return;
            EventManager.TriggerEvent(GameEvents.DisableDiceLaunch);
            m_waitingForThrow = false;
            m_rerollSelection.Clear();
            m_state.ReceiveRoll(roll.Results.Where(r => r.OwnerId >= 0).Select(r => (r.OwnerId, r)));
        }

        private static DiceRollerController Roller() =>
            DiceManager.Instance != null ? DiceManager.Instance.GetComponentInChildren<DiceRollerController>(true) : null;

        /// <summary>The state wants these dice re-thrown (reroll step or the Trickster's Reroll): open the box for a drag.</summary>
        private void OnDiceRerollRequested(IReadOnlyList<int> indices)
        {
            var roller = Roller();
            roller?.SetRerollSelection(indices);
            roller?.SetHint($"Drag to re-throw {indices.Count} {(indices.Count == 1 ? "die" : "dice")}; the others stay put");
            EventManager.TriggerEvent(GameEvents.OpenDiceBox);
            m_waitingForThrow = true;
        }

        /// <summary>Recolour changed a die's colour: set the physical die to a face of that colour so the box agrees with the pool.</summary>
        private void OnDieRecolored(int index, DieColor color)
        {
            int face = m_state.ResourceDie.FaceIndexOfColor(color);
            if (DiceManager.Instance != null && DieDefinition.IsValidFaceIndex(face)) DiceManager.ApplyModifier(index, new ChooseResultModifier(face));
        }

        /// <summary>Flash the card and float a number for damage, shield, status and death.</summary>
        private void OnUnitEvent(CombatEvent evt)
        {
            var cards = evt.TargetIsEnemy ? m_enemyCards : m_heroCards;
            if (!cards.TryGetValue(evt.Target, out var card)) return;
            switch (evt.Kind)
            {
                case CombatEventKind.Damage:
                    card.Flash.Play(evt.Amount > 0 ? $"-{evt.Amount}" : "Blocked", evt.Amount > 0 ? CardFlash.DamageColor : CardFlash.ShieldColor);
                    break;
                case CombatEventKind.Heal:
                    card.Flash.Play($"+{evt.Amount}", CardFlash.HealColor);
                    break;
                case CombatEventKind.Shield:
                    card.Flash.Play($"+{evt.Amount} shield", CardFlash.ShieldColor);
                    break;
                case CombatEventKind.Status:
                    card.Flash.Play(evt.Detail ?? "Status", UiFactory.Accent);
                    break;
                case CombatEventKind.Death:
                    card.Flash.Play(evt.TargetIsEnemy ? "Defeated" : "Fallen", CardFlash.DamageColor, 1.2f);
                    break;
            }
        }

        // ---------------------------------------------------------------- player actions (public for tests)

        /// <summary>Reroll step (only when the rules allow it): re-throw the toggled dice.</summary>
        public void RerollSelected()
        {
            if (m_state == null || !m_state.CanReroll || m_rerollSelection.Count == 0) return;
            m_state.RequestReroll(m_rerollSelection);
        }

        public void ToggleRerollDie(int index)
        {
            if (!m_rerollSelection.Remove(index)) m_rerollSelection.Add(index);
            Refresh();
        }

        public void KeepAll() => m_state?.KeepAll();

        /// <summary>Click on an ability button. Casts at once when no target is needed, else enters targeting.</summary>
        public void Cast(int slot, int abilityIndex)
        {
            if (m_state == null || !m_state.CanCast(slot, abilityIndex)) return;
            var ability = m_state.Hero(slot).Abilities[abilityIndex];
            switch (ability.Target)
            {
                case TargetKind.None:
                    m_state.Cast(slot, abilityIndex);
                    break;
                case TargetKind.Enemy:
                    var living = m_state.LivingEnemies.ToList();
                    if (living.Count == 1) m_state.Cast(slot, abilityIndex, living[0].Index);
                    else BeginTargeting(slot, abilityIndex, TargetKind.Enemy);
                    break;
                case TargetKind.Die:
                    BeginTargeting(slot, abilityIndex, TargetKind.Die);
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

        private void BeginTargeting(int slot, int abilityIndex, TargetKind kind)
        {
            m_pendingSlot = slot;
            m_pendingAbility = abilityIndex;
            m_pendingKind = kind;
            m_pendingDie = -1;
            Refresh();
        }

        public void ClearTargeting()
        {
            m_pendingSlot = -1;
            m_pendingAbility = -1;
            m_pendingKind = TargetKind.None;
            m_pendingDie = -1;
        }

        public void CancelTargeting()
        {
            ClearTargeting();
            Refresh();
        }

        /// <summary>Click on an enemy card while an enemy-targeted ability is pending.</summary>
        public void ChooseEnemy(int enemyIndex)
        {
            if (m_state == null || m_pendingSlot < 0 || m_pendingKind != TargetKind.Enemy) return;
            int slot = m_pendingSlot, ability = m_pendingAbility; // capture before clearing
            ClearTargeting();
            m_state.Cast(slot, ability, enemyIndex);
            Refresh();
        }

        /// <summary>Click on a die chip while a die-targeted ability is pending. Reroll casts at once; Recolour waits for a colour.</summary>
        public void ChooseDie(int dieIndex)
        {
            if (m_state == null || m_pendingSlot < 0 || m_pendingKind != TargetKind.Die) return;
            var ability = m_state.Hero(m_pendingSlot).Abilities[m_pendingAbility];
            if (!m_state.CanTargetDie(dieIndex, ability)) return;
            if (ability.kind == AbilityKind.Recolor)
            {
                m_pendingDie = dieIndex;
                Refresh();
                return;
            }
            int slot = m_pendingSlot, index = m_pendingAbility;
            ClearTargeting();
            m_state.Cast(slot, index, dieIndex);
            Refresh();
        }

        /// <summary>Recolour's second click: the new colour for the chosen die.</summary>
        public void ChooseColor(DieColor color)
        {
            if (m_state == null || m_pendingSlot < 0 || m_pendingDie < 0) return;
            int slot = m_pendingSlot, index = m_pendingAbility, die = m_pendingDie;
            ClearTargeting();
            m_state.Cast(slot, index, die, color);
            Refresh();
        }

        // ---------------------------------------------------------------- build

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
            UiFactory.FixedHeight(m_enemyRow, 190f);

            m_manaRow = UiFactory.Group(background, "Mana");
            UiFactory.Horizontal(m_manaRow, spacing: 24f, padding: 0, align: TextAnchor.MiddleCenter, expandChildren: false);
            UiFactory.FixedHeight(m_manaRow, 40f);

            m_diceRow = UiFactory.Group(background, "Dice");
            UiFactory.Horizontal(m_diceRow, spacing: 10f, padding: 0, align: TextAnchor.MiddleCenter, expandChildren: false);
            UiFactory.FixedHeight(m_diceRow, 64f);

            m_heroRow = UiFactory.Group(background, "Heroes");
            UiFactory.Horizontal(m_heroRow, spacing: 16f, padding: 0);
            UiFactory.FixedHeight(m_heroRow, 300f);

            m_actions = UiFactory.Group(background, "Actions");
            UiFactory.Horizontal(m_actions, spacing: 12f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
            UiFactory.FixedHeight(m_actions, 56f);

            var logPanel = UiFactory.Panel(background, "LogPanel", UiFactory.PanelMid, stretch: false);
            UiFactory.Flexible(logPanel, 1f);
            m_log = UiFactory.Label(logPanel, "Log", "", 17f, TextAlignmentOptions.BottomLeft, UiFactory.TextLight);
            UiFactory.Stretch(m_log.rectTransform, 10f);
        }

        /// <summary>Cards are per fight: enemies differ per node and heroes may have died.</summary>
        private void BuildCards()
        {
            UiFactory.DestroyChildren(m_enemyRow);
            UiFactory.DestroyChildren(m_heroRow);
            m_enemyCards.Clear();
            m_heroCards.Clear();

            foreach (var enemy in m_state.Enemies)
            {
                int index = enemy.Index;
                var card = BuildCard(m_enemyRow, $"Enemy{index}", enemy.Name, new Color(0.3f, 0.12f, 0.12f, 1f), () => ChooseEnemy(index));
                card.Bar.SetColor(new Color(0.8f, 0.25f, 0.2f));
                m_enemyCards[index] = card;
            }
            foreach (var hero in m_state.Heroes)
            {
                int slot = hero.Slot;
                var card = BuildCard(m_heroRow, $"Hero{slot}", hero.Name, HeroTint(hero.Color), null);
                card.Bar.SetColor(new Color(0.35f, 0.8f, 0.4f));
                m_heroCards[slot] = card;
            }
        }

        private UnitCard BuildCard(Transform row, string name, string title, Color tint, System.Action onClick)
        {
            var button = UiFactory.Button(row, name, "", onClick ?? (() => { }), tint, 18f, 200f);
            var old = button.GetComponentInChildren<TMP_Text>();
            if (old != null) Destroy(old.gameObject);
            var column = UiFactory.Group(button.transform, "Column");
            UiFactory.Stretch(column, 8f);
            UiFactory.Vertical(column, spacing: 4f, padding: 6, controlHeight: true);

            var card = new UnitCard
            {
                Button = button,
                Image = button.GetComponent<Image>(),
                Flash = button.gameObject.AddComponent<CardFlash>(),
                BaseColor = tint,
            };
            card.Name = UiFactory.Label(column, "Name", title, 24f);
            card.Bar = UiFactory.Bar(column, "Health", Color.green, 20f);
            card.StatusRow = UiFactory.Group(column, "Statuses");
            UiFactory.Horizontal(card.StatusRow, spacing: 4f, padding: 0, align: TextAnchor.MiddleCenter, expandChildren: false);
            UiFactory.FixedHeight(card.StatusRow, 26f);
            card.Body = UiFactory.Group(column, "Body");
            UiFactory.Vertical(card.Body, spacing: 4f, padding: 0, controlHeight: true);
            card.Flash.SetBaseColor(tint);
            return card;
        }

        // ---------------------------------------------------------------- refresh

        private void Refresh()
        {
            if (!m_built || m_state == null) return;
            var state = m_state;

            RefreshMana(state);
            RefreshDice(state);
            m_log.text = string.Join("\n", state.Log.Skip(Mathf.Max(0, state.Log.Count - 8)));
            foreach (var enemy in state.Enemies) RefreshEnemyCard(state, enemy);
            foreach (var hero in state.Heroes) RefreshHeroCard(state, hero);
            BuildActions(state);
            m_hint.text = HintText(state);
        }

        private void RefreshMana(CombatState state)
        {
            UiFactory.DestroyChildren(m_manaRow);
            UiFactory.Label(m_manaRow, "Label", "Mana", 22f, TextAlignmentOptions.MidlineRight, UiFactory.TextDim).gameObject.AddComponent<LayoutElement>().preferredWidth = 80f;
            foreach (var color in new[] { DieColor.Red, DieColor.Blue, DieColor.Yellow })
            {
                var cell = UiFactory.Group(m_manaRow, color.ToString());
                UiFactory.Horizontal(cell, spacing: 4f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
                cell.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;
                UiFactory.Icon(cell, "Icon", Icons?.mana, 26f, IconLibrary.ManaColor(color));
                UiFactory.Label(cell, "Count", $"{state.Mana(color)}", 24f, TextAlignmentOptions.MidlineLeft, IconLibrary.ManaColor(color));
            }
            UiFactory.Label(m_manaRow, "Turn", $"Turn {state.Turn}", 22f, TextAlignmentOptions.MidlineLeft, UiFactory.TextDim).gameObject.AddComponent<LayoutElement>().preferredWidth = 120f;
        }

        /// <summary>One chip per resource die: its pips and colour. Clickable while a die-targeted ability is pending, or as reroll toggles.</summary>
        private void RefreshDice(CombatState state)
        {
            UiFactory.DestroyChildren(m_diceRow);
            if (state.Dice.Count == 0)
            {
                UiFactory.Label(m_diceRow, "None", m_waitingForThrow ? "Dice in the box…" : "", 18f, color: UiFactory.TextDim).gameObject.AddComponent<LayoutElement>().preferredWidth = 300f;
                return;
            }

            bool targetingDie = m_pendingSlot >= 0 && m_pendingKind == TargetKind.Die;
            var pendingAbility = targetingDie ? state.Hero(m_pendingSlot)?.Abilities[m_pendingAbility] : null;
            bool rerollStep = state.Phase == CombatPhase.Reroll && !m_waitingForThrow;
            foreach (var die in state.Dice)
            {
                int index = die.Index;
                bool canTarget = targetingDie && state.CanTargetDie(index, pendingAbility);
                bool selected = index == m_pendingDie || (rerollStep && m_rerollSelection.Contains(index));
                var tint = selected ? UiFactory.Accent : die.Pending ? UiFactory.PanelLocked : IconLibrary.ManaColor(die.Color) * (canTarget || !targetingDie ? 1f : 0.5f);
                tint.a = 1f;
                System.Action onClick = rerollStep ? () => ToggleRerollDie(index) : () => ChooseDie(index);
                var chip = UiFactory.Button(m_diceRow, $"Die{index}", "", onClick, tint, 16f, 60f);
                chip.gameObject.GetComponent<LayoutElement>().preferredWidth = 110f;
                chip.interactable = rerollStep || canTarget;
                var old = chip.GetComponentInChildren<TMP_Text>();
                if (old != null) Destroy(old.gameObject);
                var row = UiFactory.Group(chip.transform, "Row");
                UiFactory.Stretch(row, 4f);
                UiFactory.Horizontal(row, spacing: 4f, padding: 0, align: TextAnchor.MiddleCenter, expandChildren: false);
                UiFactory.Icon(row, "Pips", Icons?.ForPips(die.Result.FaceIndex), 34f, Color.black);
                UiFactory.Label(row, "Color", die.Pending ? "..." : die.Color.ToString()[..1], 22f, TextAlignmentOptions.Midline, Color.black);
            }
        }

        private void RefreshEnemyCard(CombatState state, CombatEnemy enemy)
        {
            var card = m_enemyCards[enemy.Index];
            bool targeting = m_pendingSlot >= 0 && m_pendingKind == TargetKind.Enemy;
            var tint = !enemy.IsAlive ? UiFactory.PanelLocked : targeting ? UiFactory.Accent : card.BaseColor;
            card.Flash.SetBaseColor(tint);
            card.Button.interactable = targeting && enemy.IsAlive;
            card.Bar.Set(enemy.Health, enemy.MaxHealth, enemy.Shield);
            RefreshStatuses(card.StatusRow, enemy.Statuses);

            UiFactory.DestroyChildren(card.Body);
            if (!enemy.IsAlive)
            {
                UiFactory.Label(card.Body, "Dead", "Defeated", 22f, color: UiFactory.TextDim);
                return;
            }
            if (enemy.Intent != null)
                UiFactory.IconLabel(card.Body, "Intent", Icons?.ForIntent(enemy.Intent.kind), $"Next: {enemy.Intent.Describe()}", 20f, 28f, UiFactory.Accent, UiFactory.Accent, TextAlignmentOptions.Midline);
            if (targeting)
                UiFactory.Label(card.Body, "Target", "Click to target", 18f, color: Color.black);
        }

        private void RefreshHeroCard(CombatState state, CombatHero hero)
        {
            int slot = hero.Slot;
            var card = m_heroCards[slot];
            bool casting = m_pendingSlot == slot;
            var tint = !hero.IsAlive ? UiFactory.PanelLocked : casting ? UiFactory.Accent : card.BaseColor;
            card.Flash.SetBaseColor(tint);
            card.Button.interactable = false;
            card.Bar.Set(hero.Health, hero.MaxHealth, hero.Shield);
            RefreshStatuses(card.StatusRow, hero.Statuses);

            var body = card.Body;
            UiFactory.DestroyChildren(body);
            if (!hero.IsAlive)
            {
                UiFactory.Label(body, "Dead", "Fallen (abilities lost)", 20f, color: UiFactory.TextDim);
                return;
            }

            bool canUseButtons = state.CanAct && m_pendingSlot < 0 && !m_waitingForThrow;
            if (state.Phase == CombatPhase.Act && !state.HasActionLeft(slot))
                UiFactory.Label(body, "Acted", "Acted this turn", 18f, color: UiFactory.Accent);
            for (int i = 0; i < hero.Abilities.Count; i++)
            {
                int abilityIndex = i;
                var ability = hero.Abilities[i];
                var buttonRow = UiFactory.Group(body, $"Ability{i}");
                UiFactory.Horizontal(buttonRow, spacing: 4f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
                UiFactory.FixedHeight(buttonRow, 34f);
                UiFactory.Icon(buttonRow, "Icon", Icons?.ForAbility(ability), 26f, IconLibrary.ManaColor(ability.costColor));
                var b = UiFactory.Button(buttonRow, "Cast", $"{ability.name}  <size=75%>{ability.cost}{ability.costColor.ToString()[..1]}</size>", () => Cast(slot, abilityIndex), UiFactory.PanelDark, 16f, 34f);
                b.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
                b.interactable = canUseButtons && state.CanCast(slot, abilityIndex);
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = b.interactable ? UiFactory.TextLight : UiFactory.TextDim;
            }
        }

        private void RefreshStatuses(RectTransform row, StatusSet statuses)
        {
            UiFactory.DestroyChildren(row);
            foreach (var (kind, stacks) in statuses.All)
            {
                var cell = UiFactory.Group(row, kind.ToString());
                UiFactory.Horizontal(cell, spacing: 2f, padding: 0, align: TextAnchor.MiddleLeft, expandChildren: false);
                cell.gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;
                UiFactory.Icon(cell, "Icon", Icons?.ForStatus(kind), 22f, UiFactory.Accent);
                UiFactory.Label(cell, "Stacks", stacks > 1 || kind is StatusKind.Burn or StatusKind.Poison ? stacks.ToString() : "", 16f, TextAlignmentOptions.MidlineLeft);
            }
        }

        private string HintText(CombatState state)
        {
            if (m_pendingSlot >= 0)
            {
                var ability = state.Hero(m_pendingSlot)?.Abilities[m_pendingAbility];
                if (m_pendingKind == TargetKind.Enemy) return $"{ability?.name}: click an enemy.";
                if (m_pendingDie >= 0) return $"{ability?.name}: pick the new colour below.";
                return $"{ability?.name}: click a rolled die whose mana is still in the pool.";
            }
            if (m_waitingForThrow) return state.Dice.Count == 0 ? "Drag across the box to throw the party's resource dice." : "Drag in the box to re-throw the selected dice.";
            return state.Phase switch
            {
                CombatPhase.Reroll => "Pick dice to re-throw, or keep them all.",
                CombatPhase.Act => "Each die added one mana of its colour. Spend the pool on abilities, then end the turn."
                                   + (state.Rules.actionsPerHeroPerTurn > 0 ? $" Each hero acts {(state.Rules.actionsPerHeroPerTurn == 1 ? "once" : $"{state.Rules.actionsPerHeroPerTurn} times")} per turn." : "")
                                   + (state.Rules.manaCarriesBetweenTurns ? " Unspent mana carries over." : " Unspent mana is lost at end of turn."),
                CombatPhase.Won => "Victory. Continue to the map.",
                CombatPhase.Lost => "The party has fallen.",
                _ => "",
            };
        }

        private void BuildActions(CombatState state)
        {
            UiFactory.DestroyChildren(m_actions);
            void Add(string name, string text, System.Action onClick, Color? tint = null, bool interactable = true, float width = 240f)
            {
                var b = UiFactory.Button(m_actions, name, text, onClick, tint ?? UiFactory.PanelMid, 20f, 48f);
                b.gameObject.GetComponent<LayoutElement>().preferredWidth = width;
                b.interactable = interactable;
            }

            if (m_pendingSlot >= 0)
            {
                if (m_pendingDie >= 0)
                {
                    var current = state.Die(m_pendingDie)?.Color ?? DieColor.None;
                    foreach (var color in new[] { DieColor.Red, DieColor.Blue, DieColor.Yellow })
                    {
                        var c = color;
                        var tint = IconLibrary.ManaColor(color);
                        var b = UiFactory.Button(m_actions, $"To{color}", color.ToString(), () => ChooseColor(c), tint, 20f, 48f);
                        b.gameObject.GetComponent<LayoutElement>().preferredWidth = 140f;
                        b.interactable = color != current;
                        b.GetComponentInChildren<TMP_Text>().color = Color.black;
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
                case CombatPhase.Act when !m_waitingForThrow:
                    Add("EndTurn", "End turn", EndTurn, UiFactory.Accent, state.CanAct);
                    break;
                case CombatPhase.Won:
                case CombatPhase.Lost:
                    Add("Continue", state.Phase == CombatPhase.Won ? "Continue" : "Accept defeat", Continue, UiFactory.Accent);
                    break;
            }
            Add("Abandon", "Abandon run", () => RunController.Instance?.AbandonRun(), width: 180f);
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
