using System.Collections.Generic;
using System.Linq;
using DungeonDice.Core;
using GameUtility;
using UnityEngine;

namespace DungeonDice.Dice
{
    /// <summary>
    /// Owns the dice in the box: pools die prefabs, idles them in the air, throws the active set
    /// on <see cref="GameEvents.LaunchDice"/>, collects each die's settled face and fires
    /// <see cref="GameEvents.RollCompleted"/> with one <see cref="RollResult"/> per active die.
    /// Knows nothing about mana or combat.
    /// </summary>
    public class DiceManager : MonoBehaviour
    {
        public static DiceManager Instance { get; private set; }

        public int m_idleTorqueStrength = 10;
        public int m_launchTorqueStrength = 20;
        public float m_spawnPadding = 100;
        public float m_downwardForce = 0.5f;

        [SerializeField] private GameObject diePrefab;
        [SerializeField] private GameObject diceSpawnPoint;
        [SerializeField] private float m_maxForce = 50f;
        [SerializeField] private float m_minForce = 5f;

        [Tooltip("Gravity while dice are in flight. Restored to the project default when the roll completes.")]
        [SerializeField] private Vector3 m_throwGravity = new(0f, -12f, 0f);
        [Tooltip("Per-rigidbody solver iterations for dice, so the global Physics defaults are untouched.")]
        [SerializeField] private int m_dieSolverIterations = 12;

        /// <summary>Every die ever spawned. Inactive entries are pooled for later rolls.</summary>
        private readonly List<GameObject> m_pool = new();
        /// <summary>The dice taking part in the current roll, in owner order.</summary>
        private readonly List<DiceHandler> m_active = new();
        private readonly List<RollResult> m_results = new();

        private bool m_isIdle = true;
        private Vector3 m_defaultGravity;
        private bool m_gravityOverridden;

        public bool IsIdle => m_isIdle;
        public IReadOnlyList<DiceHandler> ActiveDice => m_active;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (diePrefab == null)
            {
                Debug.LogError("Die prefab is not assigned in the inspector.");
                enabled = false;
                return;
            }
            if (diceSpawnPoint == null)
            {
                Debug.LogError("Dice spawn point is not assigned in the inspector.");
                enabled = false;
                return;
            }

            m_defaultGravity = Physics.gravity;
            DontDestroyOnLoad(gameObject);
            EventManager.Register(GameEvents.SetUpDice, new EventHandler<int>(SetUpDice));
            EventManager.Register(GameEvents.LaunchDice, new EventHandler<Vector3>(LaunchDice));
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            EventManager.Unregister<int>(GameEvents.SetUpDice, SetUpDice);
            EventManager.Unregister<Vector3>(GameEvents.LaunchDice, LaunchDice);
            RestoreGravity();
        }

        private void Update()
        {
            if (!m_isIdle) return;
            // Keep the idle dice tumbling in the air.
            foreach (var die in m_active)
            {
                if (die.TryGetComponent(out Rigidbody rb))
                {
                    rb.AddTorque(Random.onUnitSphere * m_idleTorqueStrength, ForceMode.Acceleration);
                }
            }
        }

        /// <summary>Set up <paramref name="diceCount"/> dice, each keeping whatever definition its prefab carries.</summary>
        public static void SetUpDice(int diceCount) => Instance.SetUp(diceCount, null);

        /// <summary>Set up one die per definition, in order. Owner ids are the list indices.</summary>
        public static void SetUpDice(IReadOnlyList<DieDefinition> definitions) =>
            Instance.SetUp(definitions.Count, definitions);

        private void SetUp(int diceCount, IReadOnlyList<DieDefinition> definitions)
        {
            RestoreGravity(); // in case a previous roll never completed
            m_results.Clear();
            m_active.Clear();
            m_isIdle = true;

            // Line the dice up, centred on the spawn point.
            var position = diceSpawnPoint.transform.position;
            position.x -= (diceCount - 1) / 2.0f * m_spawnPadding;

            while (m_pool.Count < diceCount)
            {
                var die = Instantiate(diePrefab, position, Quaternion.identity, diceSpawnPoint.transform);
                die.name = $"Dice_{m_pool.Count}";
                m_pool.Add(die);
            }

            for (int i = 0; i < m_pool.Count; i++)
            {
                var die = m_pool[i];
                bool active = i < diceCount;
                die.SetActive(active);
                if (!active) continue;

                die.transform.SetPositionAndRotation(position, Random.rotation);
                position.x += m_spawnPadding;

                if (!die.TryGetComponent(out DiceHandler handler))
                {
                    Debug.LogWarning($"Die prefab [ {die.name} ] does not have a DiceHandler component.");
                    continue;
                }
                if (definitions != null && definitions[i] != null) handler.Definition = definitions[i];
                handler.OwnerId = i;
                m_active.Add(handler);

                if (die.TryGetComponent(out Rigidbody rb))
                {
                    rb.useGravity = false;
                    rb.solverIterations = m_dieSolverIterations;
                    rb.solverVelocityIterations = m_dieSolverIterations;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Random.insideUnitSphere * m_idleTorqueStrength;
                }
                else
                {
                    Debug.LogWarning($"Die prefab [ {die.name} ] does not have a Rigidbody component.");
                }
            }
        }

        /// <param name="toVector">Direction and magnitude of the drag; magnitude is clamped to the force range.</param>
        public static void LaunchDice(Vector3 toVector)
        {
            var self = Instance;
            if (self.m_active.Count == 0)
            {
                Debug.LogWarning("No dice have been set up to launch.");
                return;
            }
            if (!self.m_isIdle)
            {
                Debug.LogWarning("Dice have been launched already.");
                return;
            }

            var direction = toVector.normalized + Vector3.down * self.m_downwardForce;
            var force = Mathf.Clamp(toVector.magnitude, self.m_minForce, self.m_maxForce);
            Debug.Log($"Launch force applied to the dice [ {force} ]");

            self.OverrideGravity();
            self.m_isIdle = false;
            foreach (var handler in self.m_active)
            {
                handler.LaunchDice(direction * force, Random.insideUnitSphere * self.m_launchTorqueStrength);
            }
        }

        /// <summary>Called by each <see cref="DiceHandler"/> once it has settled.</summary>
        public static void ProcessDiceValue(DiceHandler die, int faceIndex)
        {
            var self = Instance;
            if (die.Definition == null)
            {
                Debug.LogError($"Die [ {die.name} ] has no DieDefinition; its result is dropped.");
            }
            else
            {
                var result = new RollResult(die.Definition, faceIndex, die.name, die.OwnerId);
                Debug.Log($"Dice result: {result}");
                self.m_results.Add(result);
            }

            int settled = self.m_results.Count + self.m_active.Count(d => d.Definition == null && !d.IsSettling);
            if (settled < self.m_active.Count) return;

            self.CompleteRoll();
        }

        private void CompleteRoll()
        {
            RestoreGravity();
            var results = new List<RollResult>(m_results);
            results.Sort((a, b) => a.OwnerId.CompareTo(b.OwnerId));
            m_results.Clear();

            var evt = new RollCompletedEvent(results);
            Debug.Log($"Roll completed: {results.Count} dice, {evt.TotalPips} pips.");
            EventManager.TriggerEvent(GameEvents.RollCompleted, evt);
            EventManager.TriggerEvent(GameEvents.DisableDiceLaunch);
        }

        private void OverrideGravity()
        {
            if (m_gravityOverridden) return;
            m_defaultGravity = Physics.gravity;
            Physics.gravity = m_throwGravity;
            m_gravityOverridden = true;
        }

        private void RestoreGravity()
        {
            if (!m_gravityOverridden) return;
            Physics.gravity = m_defaultGravity;
            m_gravityOverridden = false;
        }
    }
}
