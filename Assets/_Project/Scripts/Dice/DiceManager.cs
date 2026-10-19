using System.Collections.Generic;
using System.Linq;
using DungeonDice.Core;
using GameUtility;
using UnityEngine;

namespace DungeonDice.Dice
{
    /// <summary>Lifecycle of the dice box between two <see cref="DiceManager.SetUpDice(int)"/> calls.</summary>
    public enum DiceBoxState
    {
        /// <summary>No active dice. Nothing can be thrown.</summary>
        Empty,
        /// <summary>Active dice tumble weightlessly, waiting for the player's throw.</summary>
        Idle,
        /// <summary>At least one die is in flight; results are pending.</summary>
        Rolling,
        /// <summary>Every active die has reported. Results can be modified or a subset re-thrown.</summary>
        Settled,
    }

    /// <summary>
    /// Owns the dice in the box: pools die prefabs, idles them in the air, throws the active set,
    /// collects each die's settled face into a <see cref="RollResult"/> and fires
    /// <see cref="GameEvents.RollCompleted"/>. After a roll it can apply modifiers (re-orienting the
    /// physical die to match) and re-throw a chosen subset. Knows nothing about mana or combat;
    /// consumers decide what a roll means.
    /// </summary>
    public class DiceManager : MonoBehaviour
    {
        public static DiceManager Instance { get; private set; }

        // Public fields keep their legacy names: SampleScene serializes them by name.
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
        /// <summary>Dice taking part in the current roll, indexed by owner id.</summary>
        private readonly List<DiceHandler> m_active = new();
        /// <summary>Settled results of the current roll, keyed by owner id.</summary>
        private readonly Dictionary<int, RollResult> m_results = new();
        /// <summary>Owner ids still in flight. The roll completes when this empties.</summary>
        private readonly HashSet<int> m_pending = new();

        private Vector3 m_defaultGravity;
        private bool m_gravityOverridden;

        public DiceBoxState State { get; private set; } = DiceBoxState.Empty;
        /// <summary>The most recent completed roll, or null before the first completion of this setup.</summary>
        public RollCompletedEvent LastRoll { get; private set; }
        public IReadOnlyList<DiceHandler> ActiveDice => m_active;
        /// <summary>Kept for callers written against the pre-state-machine API.</summary>
        public bool IsIdle => State == DiceBoxState.Idle;

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
                Debug.LogError("DiceManager: die prefab is not assigned in the inspector.");
                enabled = false;
                return;
            }
            if (diceSpawnPoint == null)
            {
                Debug.LogError("DiceManager: dice spawn point is not assigned in the inspector.");
                enabled = false;
                return;
            }

            m_defaultGravity = Physics.gravity;
            DontDestroyOnLoad(gameObject);
            EventManager.Register(GameEvents.SetUpDice, new EventHandler<int>(SetUpDice));
            EventManager.Register(GameEvents.LaunchDice, new EventHandler<Vector3>(LaunchDice));
            EventManager.Register(GameEvents.RelaunchDice, new EventHandler<RelaunchRequest>(RelaunchDice));
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            EventManager.Unregister<int>(GameEvents.SetUpDice, SetUpDice);
            EventManager.Unregister<Vector3>(GameEvents.LaunchDice, LaunchDice);
            EventManager.Unregister<RelaunchRequest>(GameEvents.RelaunchDice, RelaunchDice);
            RestoreGravity();
        }

        private void Update()
        {
            if (State != DiceBoxState.Idle) return;
            // Keep the waiting dice tumbling so the box never looks frozen.
            foreach (var die in m_active)
            {
                if (die.TryGetComponent(out Rigidbody rb))
                {
                    rb.AddTorque(Random.onUnitSphere * m_idleTorqueStrength, ForceMode.Acceleration);
                }
            }
        }

        // ---------------------------------------------------------------- setup

        /// <summary>Set up <paramref name="diceCount"/> dice, each keeping whatever definition its prefab carries.</summary>
        public static void SetUpDice(int diceCount) => Instance.SetUp(diceCount, null);

        /// <summary>Set up one die per definition, in order. Owner ids are the list indices.</summary>
        public static void SetUpDice(IReadOnlyList<DieDefinition> definitions) =>
            Instance.SetUp(definitions.Count, definitions);

        private void SetUp(int diceCount, IReadOnlyList<DieDefinition> definitions)
        {
            RestoreGravity(); // a previous roll may have been abandoned mid-flight
            m_results.Clear();
            m_pending.Clear();
            m_active.Clear();
            LastRoll = null;

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

            State = m_active.Count > 0 ? DiceBoxState.Idle : DiceBoxState.Empty;
        }

        // ---------------------------------------------------------------- throwing

        /// <summary>Throw every active die. Only valid while <see cref="DiceBoxState.Idle"/>.</summary>
        /// <param name="toVector">Drag direction and magnitude; magnitude is clamped to the force range.</param>
        public static void LaunchDice(Vector3 toVector)
        {
            var self = Instance;
            if (self.State == DiceBoxState.Empty)
            {
                Debug.LogWarning("DiceManager: no dice have been set up to launch.");
                return;
            }
            if (self.State != DiceBoxState.Idle)
            {
                Debug.LogWarning($"DiceManager: cannot launch while {self.State}. Use RelaunchDice after a roll settles.");
                return;
            }

            self.Throw(self.m_active, toVector);
        }

        /// <summary>
        /// Re-throw a subset of the settled dice (the reroll rule). Locked results and unknown ids are
        /// skipped; the other dice stay where they lie. Only valid while <see cref="DiceBoxState.Settled"/>.
        /// </summary>
        public static void RelaunchDice(RelaunchRequest request) => RelaunchDice(request.OwnerIds, request.Direction);

        public static void RelaunchDice(IReadOnlyList<int> ownerIds, Vector3 toVector)
        {
            var self = Instance;
            if (self.State != DiceBoxState.Settled)
            {
                Debug.LogWarning($"DiceManager: cannot relaunch while {self.State}.");
                return;
            }

            var dice = new List<DiceHandler>();
            foreach (var id in ownerIds.Distinct())
            {
                var handler = self.m_active.FirstOrDefault(d => d.OwnerId == id);
                if (handler == null)
                {
                    Debug.LogWarning($"DiceManager: relaunch ignores unknown owner id {id}.");
                    continue;
                }
                if (self.m_results.TryGetValue(id, out var result) && result.Locked)
                {
                    Debug.Log($"DiceManager: die {id} is locked and stays put.");
                    continue;
                }
                dice.Add(handler);
            }

            if (dice.Count == 0)
            {
                Debug.LogWarning("DiceManager: nothing to relaunch.");
                return;
            }

            foreach (var die in dice) self.m_results.Remove(die.OwnerId);
            self.Throw(dice, toVector);
        }

        private void Throw(IReadOnlyList<DiceHandler> dice, Vector3 toVector)
        {
            var direction = toVector.normalized + Vector3.down * m_downwardForce;
            var force = Mathf.Clamp(toVector.magnitude, m_minForce, m_maxForce);
            Debug.Log($"DiceManager: throwing {dice.Count} dice with force {force:F1}.");

            OverrideGravity();
            State = DiceBoxState.Rolling;
            foreach (var handler in dice)
            {
                m_pending.Add(handler.OwnerId);
                handler.LaunchDice(direction * force, Random.insideUnitSphere * m_launchTorqueStrength);
            }
        }

        // ---------------------------------------------------------------- results

        /// <summary>Called by each <see cref="DiceHandler"/> once it has settled.</summary>
        public static void ProcessDiceValue(DiceHandler die, int faceIndex)
        {
            var self = Instance;
            if (!self.m_pending.Remove(die.OwnerId))
            {
                // A die that settled outside a throw (e.g. nudged by a relaunched neighbour) is not a result.
                Debug.LogWarning($"DiceManager: ignoring unexpected result from {die.name} (owner {die.OwnerId}).");
                return;
            }

            if (die.Definition == null)
            {
                Debug.LogError($"DiceManager: die [ {die.name} ] has no DieDefinition; its result is dropped.");
            }
            else
            {
                var result = new RollResult(die.Definition, faceIndex, die.name, die.OwnerId);
                Debug.Log($"DiceManager: {result}");
                self.m_results[die.OwnerId] = result;
            }

            if (self.m_pending.Count == 0) self.CompleteRoll();
        }

        private void CompleteRoll()
        {
            RestoreGravity();
            State = DiceBoxState.Settled;

            var results = m_results.Values.OrderBy(r => r.OwnerId).ToList();
            LastRoll = new RollCompletedEvent(results);
            Debug.Log($"DiceManager: roll completed, {results.Count} dice, {LastRoll.TotalPips} pips.");
            EventManager.TriggerEvent(GameEvents.RollCompleted, LastRoll);
        }

        /// <summary>
        /// Apply a modifier to one settled result and turn the physical die to show the new face.
        /// Returns the modified result, or null if the box is not settled or the id is unknown.
        /// </summary>
        public static RollResult ApplyModifier(int ownerId, IDieModifier modifier)
        {
            var self = Instance;
            if (self.State != DiceBoxState.Settled)
            {
                Debug.LogWarning($"DiceManager: cannot modify results while {self.State}.");
                return null;
            }
            if (!self.m_results.TryGetValue(ownerId, out var result))
            {
                Debug.LogWarning($"DiceManager: no result for owner id {ownerId}.");
                return null;
            }

            result.Apply(modifier);
            var handler = self.m_active.FirstOrDefault(d => d.OwnerId == ownerId);
            if (handler != null) handler.ShowFace(result.FaceIndex);

            Debug.Log($"DiceManager: {modifier.Name} on die {ownerId} -> {result}");
            EventManager.TriggerEvent(GameEvents.RollResultModified, result);
            return result;
        }

        /// <summary>The settled result for an owner id, or null.</summary>
        public RollResult GetResult(int ownerId) => m_results.TryGetValue(ownerId, out var r) ? r : null;

        // ---------------------------------------------------------------- physics

        // Physics.gravity is global, so it is only overridden while dice are in flight.
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
