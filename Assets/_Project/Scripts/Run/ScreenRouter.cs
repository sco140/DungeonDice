using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDice.Run
{
    /// <summary>
    /// Shows exactly one screen root per <see cref="RunPhase"/>. The prototype is a single scene,
    /// so "routing" is activating one child of the UI canvas and deactivating the rest. If a run
    /// ever spans several scenes, this is the component to swap for a scene loader.
    /// </summary>
    public class ScreenRouter : MonoBehaviour
    {
        [Serializable]
        public struct ScreenEntry
        {
            public RunPhase phase;
            public GameObject root;
        }

        [SerializeField] private List<ScreenEntry> m_screens = new();

        public RunPhase? Current { get; private set; }

        public void Show(RunPhase phase)
        {
            bool found = false;
            foreach (var entry in m_screens)
            {
                if (entry.root == null) continue;
                bool active = entry.phase == phase;
                found |= active;
                if (entry.root.activeSelf != active) entry.root.SetActive(active);
            }

            if (!found) Debug.LogWarning($"ScreenRouter: no screen registered for {phase}.");
            Current = phase;
        }

        public GameObject RootFor(RunPhase phase)
        {
            foreach (var entry in m_screens)
            {
                if (entry.phase == phase) return entry.root;
            }
            return null;
        }

        /// <summary>Authoring-time wiring; the scene tool calls this.</summary>
        public void SetScreens(IEnumerable<ScreenEntry> screens)
        {
            m_screens.Clear();
            m_screens.AddRange(screens);
        }
    }
}
