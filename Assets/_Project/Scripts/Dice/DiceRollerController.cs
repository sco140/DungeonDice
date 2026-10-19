using System.Collections.Generic;
using DungeonDice.Core;
using GameUtility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonDice.Dice
{
    /// <summary>
    /// The drag surface of the dice overlay. A press-and-release across the panel becomes a throw
    /// vector: while the box is <see cref="DiceBoxState.Idle"/> it throws every die, while it is
    /// <see cref="DiceBoxState.Settled"/> and a reroll selection is set it re-throws only those dice.
    /// Also shows and hides the overlay on <c>EnableDiceLaunch</c> / <c>DisableDiceLaunch</c>.
    /// </summary>
    public class DiceRollerController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        // Field names are kept for SampleScene serialization.
        [Tooltip("The RawImage showing the dice camera; toggled with the overlay.")]
        public GameObject m_objDiceCameraPanel;
        [Tooltip("Full-screen dim behind the dice; disabling it also stops this panel receiving drags.")]
        public Image m_cBackgroundPanel;

        private readonly List<int> m_rerollSelection = new();
        private Vector2 m_initialMousePosition;

        /// <summary>Owner ids the next drag will re-throw. Set by the combat UI; cleared after use.</summary>
        public IReadOnlyList<int> RerollSelection => m_rerollSelection;

        private void Awake()
        {
            EventManager.Register(GameEvents.EnableDiceLaunch, new EventHandler<int>(EnableDiceLaunch));
            EventManager.Register(GameEvents.DisableDiceLaunch, new EventHandler(DisableDiceLaunch));
        }

        private void Start()
        {
            if (m_objDiceCameraPanel == null)
            {
                Debug.LogError("DiceRollerController: dice camera panel is not assigned.");
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            EventManager.Unregister<int>(GameEvents.EnableDiceLaunch, EnableDiceLaunch);
            EventManager.Unregister(GameEvents.DisableDiceLaunch, DisableDiceLaunch);
        }

        public void SetRerollSelection(IEnumerable<int> ownerIds)
        {
            m_rerollSelection.Clear();
            m_rerollSelection.AddRange(ownerIds);
        }

        public void ClearRerollSelection() => m_rerollSelection.Clear();

        /// <summary>Show the overlay and set up <paramref name="diceAmount"/> dice in the box.</summary>
        public void EnableDiceLaunch(int diceAmount)
        {
            EventManager.TriggerEvent(GameEvents.SetUpDice, diceAmount);
            SetOverlayVisible(true);
        }

        public void DisableDiceLaunch() => SetOverlayVisible(false);

        private void SetOverlayVisible(bool visible)
        {
            if (m_objDiceCameraPanel) m_objDiceCameraPanel.SetActive(visible);
            if (m_cBackgroundPanel) m_cBackgroundPanel.enabled = visible;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            m_initialMousePosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var drag = eventData.position - m_initialMousePosition;
            // Screen-space drag maps to the box's horizontal plane: screen x -> world x, screen y -> world z.
            var throwVector = new Vector3(drag.x, 0f, drag.y);

            var state = DiceManager.Instance ? DiceManager.Instance.State : DiceBoxState.Empty;
            switch (state)
            {
                case DiceBoxState.Idle:
                    EventManager.TriggerEvent(GameEvents.LaunchDice, throwVector);
                    break;
                case DiceBoxState.Settled when m_rerollSelection.Count > 0:
                    EventManager.TriggerEvent(GameEvents.RelaunchDice, new RelaunchRequest(throwVector, m_rerollSelection.ToArray()));
                    m_rerollSelection.Clear();
                    break;
                default:
                    Debug.Log($"DiceRollerController: drag ignored while the box is {state}.");
                    break;
            }
        }
    }
}
