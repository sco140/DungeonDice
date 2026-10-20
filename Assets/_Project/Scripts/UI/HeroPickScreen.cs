using System.Collections.Generic;
using System.Linq;
using DungeonDice.Dice;
using DungeonDice.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDice.UI
{
    /// <summary>
    /// Pick 3 of 6 heroes. Six cards from <see cref="RunController.Roster"/> (locked ones greyed and
    /// non-interactive) listing each hero's three abilities and costs, three party slots (click to
    /// remove) and a Start Run button. Layout is final for the prototype; art is placeholder panels and text.
    /// </summary>
    public class HeroPickScreen : MonoBehaviour
    {
        private readonly HeroPickSelection m_selection = new();
        private readonly List<Button> m_cardButtons = new();
        private readonly List<TMP_Text> m_partySlotLabels = new();
        private Button m_startButton;
        private TMP_Text m_hint;
        private bool m_built;

        public HeroPickSelection Selection => m_selection;
        public IReadOnlyList<Button> CardButtons => m_cardButtons;
        public Button StartButton => m_startButton;

        private void Awake()
        {
            m_selection.Changed += Refresh;
        }

        private void OnEnable()
        {
            if (!m_built) Build();
            m_selection.Clear();
            Refresh();
        }

        private void OnDestroy()
        {
            m_selection.Changed -= Refresh;
        }

        private IReadOnlyList<HeroDefinition> Roster =>
            RunController.Instance != null ? RunController.Instance.Roster : System.Array.Empty<HeroDefinition>();

        private static IconLibrary Icons => RunController.CurrentIcons;

        private void Build()
        {
            m_built = true;
            var rt = (RectTransform)transform;
            UiFactory.Stretch(rt);

            var background = UiFactory.Panel(transform, "Background", UiFactory.PanelDark);
            var column = UiFactory.Vertical(background, spacing: 16f, padding: 32);
            column.childForceExpandHeight = false;

            UiFactory.Label(background, "Title", "Choose your party", 48f, color: UiFactory.Accent, preferredHeight: 64f);
            UiFactory.Label(background, "Explain", "In combat the party throws shared resource dice; each die adds one mana of its colour. Heroes spend that mana on their abilities.", 20f, color: UiFactory.TextDim, preferredHeight: 32f);
            m_hint = UiFactory.Label(background, "Hint", "", 24f, color: UiFactory.TextDim, preferredHeight: 36f);

            // Six hero cards in a row.
            var cardRow = UiFactory.Group(background, "Cards");
            UiFactory.Horizontal(cardRow, spacing: 16f, padding: 0);
            UiFactory.Flexible(cardRow, 1f);
            m_cardButtons.Clear();
            foreach (var hero in Roster) m_cardButtons.Add(BuildCard(cardRow, hero));

            // Party slots.
            var partyRow = UiFactory.Group(background, "Party");
            UiFactory.Horizontal(partyRow, spacing: 16f, padding: 0);
            UiFactory.FixedHeight(partyRow, 96f);
            m_partySlotLabels.Clear();
            for (int i = 0; i < m_selection.Capacity; i++)
            {
                int slot = i;
                var slotButton = UiFactory.Button(partyRow, $"Slot{i}", "Empty", () => m_selection.RemoveAt(slot), UiFactory.PanelMid, 26f, 96f);
                m_partySlotLabels.Add(slotButton.GetComponentInChildren<TMP_Text>());
            }

            m_startButton = UiFactory.Button(background, "StartRun", "Start Run", OnStartRun, UiFactory.Accent, 30f, 72f);
            var startLabel = m_startButton.GetComponentInChildren<TMP_Text>();
            startLabel.color = Color.black;
        }

        private Button BuildCard(Transform parent, HeroDefinition hero)
        {
            bool pickable = HeroPickSelection.IsPickable(hero);
            var tint = pickable ? ColorFor(hero.color) : UiFactory.PanelLocked;
            var card = UiFactory.Button(parent, hero != null ? hero.displayName : "Empty", "", () => m_selection.TryAdd(hero), tint, 20f, 400f);
            card.interactable = pickable;

            // Replace the button's single label with a column of details.
            var oldLabel = card.GetComponentInChildren<TMP_Text>();
            Destroy(oldLabel.gameObject);
            var column = UiFactory.Group(card.transform, "Column");
            UiFactory.Stretch(column, 10f);
            UiFactory.Vertical(column, spacing: 6f, padding: 8, controlHeight: true);

            if (hero == null)
            {
                UiFactory.Label(column, "Name", "Empty", 28f);
                return card;
            }

            UiFactory.Label(column, "Name", hero.displayName, 30f, color: pickable ? UiFactory.TextLight : UiFactory.TextDim);
            UiFactory.Label(column, "Color", hero.ColorLabel, 20f, color: pickable ? UiFactory.TextLight : UiFactory.TextDim);
            UiFactory.Label(column, "Health", $"HP {hero.maxHealth}", 20f, color: UiFactory.TextDim);

            if (pickable)
            {
                foreach (var ability in hero.abilities)
                {
                    UiFactory.IconLabel(column, ability.name, Icons?.ForAbility(ability), $"{ability.name}  <size=80%>{ability.CostLabel}</size>", 18f, 24f,
                        UiFactory.TextLight, IconLibrary.ManaColor(ability.costColor), TextAlignmentOptions.MidlineLeft);
                    UiFactory.Label(column, $"{ability.name}Desc", ability.description, 14f, TextAlignmentOptions.TopLeft, UiFactory.TextDim);
                }
            }
            else
            {
                UiFactory.Label(column, "Locked", "LOCKED", 34f, color: UiFactory.TextDim);
                UiFactory.Label(column, "Description", hero.description, 16f, color: UiFactory.TextDim);
            }
            return card;
        }

        private void Refresh()
        {
            if (!m_built) return;
            var party = m_selection.Party;
            for (int i = 0; i < m_partySlotLabels.Count; i++)
            {
                m_partySlotLabels[i].text = i < party.Count ? party[i].displayName : "Empty";
            }

            var roster = Roster;
            for (int i = 0; i < m_cardButtons.Count && i < roster.Count; i++)
            {
                m_cardButtons[i].interactable = HeroPickSelection.IsPickable(roster[i]) && !m_selection.IsFull;
            }

            m_startButton.interactable = m_selection.CanConfirm;
            m_hint.text = m_selection.CanConfirm
                ? "Party ready. Start the run."
                : $"Pick {m_selection.Capacity - party.Count} more. Duplicates are allowed; click a party slot to remove.";
        }

        private void OnStartRun()
        {
            if (!m_selection.CanConfirm || RunController.Instance == null) return;
            RunController.Instance.StartRun(m_selection.Party.ToList());
        }

        private static Color ColorFor(DieColor color) => color switch
        {
            DieColor.Red => new Color(0.55f, 0.16f, 0.16f, 1f),
            DieColor.Blue => new Color(0.16f, 0.26f, 0.55f, 1f),
            DieColor.Yellow => new Color(0.55f, 0.45f, 0.12f, 1f),
            _ => UiFactory.PanelMid,
        };
    }
}
