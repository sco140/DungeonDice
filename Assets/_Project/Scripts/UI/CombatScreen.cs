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
    /// Renders a <see cref="CombatState"/> and drives the dice box for it. Unit cards are built once
    /// per fight and updated in place (health bars, status icons, intent), so hit flashes and floating
    /// numbers from <see cref="CombatState.UnitEvent"/> can play on them. Throws and re-throws are real
    /// drags in the box; the overlay closes once the dice settle so the player can act on the cards.
    /// Targeting a die shows what each candidate would become.
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
        private readonly List<int> m_ownerToSlot = new();
        private readonly HashSet<int> m_rerollSelection = new();
        private readonly Dictionary<int, UnitCard> m_heroCards = new();
        private readonly Dictionary<int, UnitCard> m_enemyCards = new();
        private bool m_built;
        private bool m_waitingForThrow;

        // Targeting: which die/signature is waiting for a click, and what kind of click.
        private int m_pendingSlot = -1;
        private TargetKind m_pendingKind = TargetKind.None;
        private bool m_pendingIsSignature;
        private int m_nudgeDelta = 1;
        private int m_loadedFace;

        private TMP_Text m_title, m_hint, m_log;
        private RectTransform m_enemyRow, m_heroRow, m_actions, m_manaRow;

        public CombatState State => m_state;
        public IReadOnlyList<int> OwnerToSlot => m_ownerToSlot;
        public bool WaitingForThrow => m_waitingForThrow;

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
            m_state.DieModified -= OnDieModified;
            m_state.AllyRerollRequested -= OnAllyRerollRequested;
            m_state.UnitEvent -= OnUnitEvent;
            m_state = null;
            m_waitingForThrow = false;
            ClearTargeting();
        }

        // ---------------------------------------------------------------- dice box

        private void BeginThrow()
        {
            var dice = m_state.DiceToThrow(out var slots);
            m_ownerToSlot.Clear();
            m_ownerToSlot.AddRange(slots);
            DiceManager.SetUpDice(dice);
            Roller()?.SetHint($"Turn {m_state.Turn}: drag across the box to throw your {dice.Count} hero {(dice.Count == 1 ? "die" : "dice")}");
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

        private static DiceRollerController Roller() =>
            DiceManager.Instance != null ? DiceManager.Instance.GetComponentInChildren<DiceRollerController>(true) : null;

        public void RerollSelected()
        {
            if (m_state == null || !m_state.CanReroll || m_rerollSelection.Count == 0) return;
            var owners = m_rerollSelection.Select(OwnerFor).Where(o => o >= 0).ToList();
            var roller = Roller();
            roller?.SetRerollSelection(owners);
            roller?.SetHint($"Drag to re-throw {owners.Count} selected {(owners.Count == 1 ? "die" : "dice")}; the others stay put");
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
            var roller = Roller();
            roller?.SetRerollSelection(new[] { owner });
            roller?.SetHint($"Reroll ally: drag to re-throw {m_state.Hero(slot).Name}'s die");
            EventManager.TriggerEvent(GameEvents.OpenDiceBox);
            m_waitingForThrow = true;
        }

        /// <summary>A modifier changed a result; turn the physical die to match (the result object is shared, so no re-apply).</summary>
        private void OnDieModified(int slot, IDieModifier modifier)
        {
            int owner = OwnerFor(slot);
            var die = m_state.Die(slot);
            if (owner < 0 || die == null || DiceManager.Instance == null) return;
            DiceManager.Instance.ActiveDice.FirstOrDefault(d => d.OwnerId == owner)?.ShowFace(die.Result.FaceIndex);
            if (m_heroCards.TryGetValue(slot, out var card)) card.Flash.Play(die.Face.ToString(), UiFactory.Accent, 0.9f);
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

        public void Use(int slot)
        {
            if (m_state == null || !m_state.CanAct(slot)) return;
            var kind = m_state.TargetFor(slot);
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
                    m_nudgeDelta = 1;
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
            if (m_state.Phase == CombatPhase.Won)
            {
                // Unspent mana travels with the party so Yellow can pay for Loaded Dice on a traversal roll.
                RunController.Instance?.State.SetCarriedMana(m_state.Red, m_state.Blue, m_state.Yellow);
                RunController.Instance?.CompleteNode();
            }
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
            int face = m_loadedFace;   // capture before ClearTargeting resets it (this was the Loaded Dice bug)
            int nudge = m_nudgeDelta;
            ClearTargeting();

            if (signature) m_state.CastSignature(slot, target, face);
            else m_state.UseDie(slot, target, nudge);
            Refresh();
        }

        public void ChooseLoadedFace(int faceIndex)
        {
            m_loadedFace = faceIndex;
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

            m_heroRow = UiFactory.Group(background, "Heroes");
            UiFactory.Horizontal(m_heroRow, spacing: 16f, padding: 0);
            UiFactory.FixedHeight(m_heroRow, 330f);

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
                var card = BuildCard(m_enemyRow, $"Enemy{index}", enemy.Name, new Color(0.3f, 0.12f, 0.12f, 1f), () => ChooseTarget(index));
                card.Bar.SetColor(new Color(0.8f, 0.25f, 0.2f));
                m_enemyCards[index] = card;
            }
            foreach (var hero in m_state.Heroes)
            {
                int slot = hero.Slot;
                var card = BuildCard(m_heroRow, $"Hero{slot}", hero.Name, HeroTint(hero.Color), () => ChooseTarget(slot));
                card.Bar.SetColor(new Color(0.35f, 0.8f, 0.4f));
                m_heroCards[slot] = card;
            }
        }

        private UnitCard BuildCard(Transform row, string name, string title, Color tint, System.Action onClick)
        {
            var button = UiFactory.Button(row, name, "", onClick, tint, 18f, 200f);
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
            var die = state.Die(slot);
            bool targetingDie = m_pendingSlot >= 0 && m_pendingKind == TargetKind.Die;
            bool canBeTarget = targetingDie && die != null && !die.Spent && hero.IsAlive && (m_pendingIsSignature || slot != m_pendingSlot);
            var tint = !hero.IsAlive ? UiFactory.PanelLocked : canBeTarget ? UiFactory.Accent : card.BaseColor;
            card.Flash.SetBaseColor(tint);
            card.Button.interactable = canBeTarget;
            card.Bar.Set(hero.Health, hero.MaxHealth, hero.Shield);
            RefreshStatuses(card.StatusRow, hero.Statuses);

            var body = card.Body;
            UiFactory.DestroyChildren(body);
            if (!hero.IsAlive)
            {
                UiFactory.Label(body, "Dead", "Fallen", 22f, color: UiFactory.TextDim);
                return;
            }

            // The die: icon + face, greyed once spent.
            string dieText = die == null ? "No die yet" : die.Spent ? $"{die.Face}  ({(die.Used ? "used" : "banked")})" : die.Face.ToString();
            var dieLabel = UiFactory.IconLabel(body, "Die", die != null ? Icons?.ForFace(die.Face) : null, dieText, 20f, 30f,
                die != null && die.Spent ? UiFactory.TextDim : UiFactory.TextLight, die != null && die.Spent ? UiFactory.TextDim : Color.white, TextAlignmentOptions.Midline);
            if (die != null && die.MirroredFace != null) dieLabel.text += " (mirrored)";

            if (die == null || die.Spent) return;

            // Targeting preview: what this die would become.
            if (canBeTarget)
            {
                string preview = m_pendingIsSignature ? state.PreviewLoadedDice(slot, m_loadedFace) : state.PreviewDieAction(m_pendingSlot, slot, m_nudgeDelta);
                UiFactory.Label(body, "Preview", preview ?? "Click to target", 18f, color: Color.black);
                return;
            }

            if (state.Phase == CombatPhase.Reroll && !m_waitingForThrow)
            {
                bool selected = m_rerollSelection.Contains(slot);
                UiFactory.Button(body, "RerollToggle", selected ? "Re-throw: yes" : "Re-throw: no", () => ToggleRerollDie(slot), selected ? UiFactory.Accent : UiFactory.PanelDark, 16f, 36f);
            }
            else if (state.Phase == CombatPhase.Act && m_pendingSlot < 0 && state.PendingAllyReroll < 0)
            {
                var buttons = UiFactory.Group(body, "Buttons");
                UiFactory.Horizontal(buttons, spacing: 6f, padding: 0);
                UiFactory.FixedHeight(buttons, 36f);
                if (die.Face.Has(FaceEffectKind.Nudge))
                {
                    UiFactory.Button(buttons, "NudgeUp", "Nudge +", () => UseNudge(slot, 1), UiFactory.PanelDark, 16f, 36f);
                    UiFactory.Button(buttons, "NudgeDown", "Nudge −", () => UseNudge(slot, -1), UiFactory.PanelDark, 16f, 36f);
                }
                else
                {
                    UiFactory.Button(buttons, "Use", $"Use {die.Face}", () => Use(slot), UiFactory.PanelDark, 16f, 36f);
                }
                UiFactory.Button(buttons, "Bank", $"Bank (+1 {hero.Color})", () => Bank(slot), UiFactory.PanelDark, 16f, 36f);

                var definition = hero.Run.Definition;
                if (definition.signatureKind != SignatureKind.None)
                {
                    bool affordable = state.Mana(hero.Color) >= CombatState.SignatureCost;
                    string why = !affordable ? "" : hero.SignatureUsedThisTurn ? "  (cast this turn)" : !state.SignatureWindowOpen ? "  (before using dice)" : "";
                    var sig = UiFactory.Button(body, "Signature", $"{definition.signatureName} (3 {hero.Color}){why}", () => CastSignature(slot), UiFactory.PanelDark, 16f, 34f);
                    sig.interactable = state.CanCastSignature(slot);
                }
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
                if (m_pendingKind == TargetKind.Die && m_pendingIsSignature && !DieDefinition.IsValidFaceIndex(m_loadedFace)) return "Loaded Dice: choose the face below, then click a hero's die.";
                return m_pendingKind == TargetKind.Enemy ? "Click an enemy." : "Click a hero's die. Each card shows what it would become.";
            }
            if (state.PendingAllyReroll >= 0) return $"Drag in the box to re-throw {state.Hero(state.PendingAllyReroll).Name}'s die.";
            return state.Phase switch
            {
                CombatPhase.Throw => "Drag across the box to throw your dice.",
                CombatPhase.Reroll => m_waitingForThrow ? "Drag to re-throw the selected dice." : "Pick dice to re-throw (once per turn), or keep them all.",
                CombatPhase.Act => state.SignatureWindowOpen && state.Heroes.Any(h => state.CanCastSignature(h.Slot))
                    ? "Signatures go first: cast now (3 mana of the hero's colour) or wait for next turn. Then use or bank each die."
                    : "Use each die's face, or bank it for 1 mana. Signatures must be cast before any die is used or banked.",
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
                if (m_pendingKind == TargetKind.Die && m_pendingIsSignature)
                {
                    for (int f = 1; f <= DieDefinition.FaceCount; f++)
                    {
                        int face = f;
                        var fb = UiFactory.Button(m_actions, $"Face{f}", "", () => ChooseLoadedFace(face), m_loadedFace == f ? UiFactory.Accent : UiFactory.PanelMid, 20f, 48f);
                        fb.gameObject.GetComponent<LayoutElement>().preferredWidth = 56f;
                        var old = fb.GetComponentInChildren<TMP_Text>();
                        if (old != null) Destroy(old.gameObject);
                        var icon = UiFactory.Icon(fb.transform, "Pips", Icons?.ForPips(f), 36f);
                        if (Icons?.ForPips(f) == null) UiFactory.Label(fb.transform, "Text", f.ToString(), 22f);
                        UiFactory.Stretch(icon.rectTransform, 6f);
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
