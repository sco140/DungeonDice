using GameUtility;
using System.Collections.Generic;
using UnityEngine;

public sealed class DiceRollEntry
{
    public BaseDice Dice { get; }
    public UnitController Owner { get; }

    public DiceRollEntry(BaseDice dice, UnitController owner)
    {
        Dice = dice;
        Owner = owner;
    }
}

public sealed class DiceRollRequest
{
    public IReadOnlyList<DiceRollEntry> DiceEntries { get; }

    public DiceRollRequest(IReadOnlyList<DiceRollEntry> diceEntries)
    {
        DiceEntries = diceEntries;
    }
}

public class DiceManager : MonoBehaviour
{
    // Singleton instance of the DiceManager.
    public static DiceManager Instance { get; private set; }

    public int m_idleTorqueStrength = 10;
    public int m_launchTorqueStrength = 20;
    public float m_spawnPadding = 100;
    public float m_downwardForce = 0.5f;
    /// <summary>
    /// A list to keep track of the retrieved resources from the dice rolls.
    /// </summary>
    private List<BaseDice.DieFace<ResourceFaceResult>> m_lstRetrievedResource = new List<BaseDice.DieFace<ResourceFaceResult>>();
    private readonly List<(UnitController owner, BaseDice.DieFace<BurnDamage> face)> m_lstRetrievedBurn =
        new List<(UnitController owner, BaseDice.DieFace<BurnDamage> face)>();
    private int m_settledDiceCount;
    // The diePrefab should be assigned in the Unity Inspector.
    // This is the prefab that will be instantiated when spawning dice.
    [SerializeField]
    private GameObject diePrefab;
    // The diceSpawnPoint should be assigned in the Unity Inspector.
    // This is the point where the dice will be spawned in the scene.
    [SerializeField]
    private GameObject diceSpawnPoint;
    [SerializeField]
    private float m_maxForce = 50f;
    [SerializeField]
    private float m_minForce = 5f;

    // A list to keep track of the spawned dice.
    private List<GameObject> m_spawnedDiceList = new List<GameObject>();
    private int m_activeDiceCount;
    // A flag to indicate whether the dice are currently idle.
    private bool m_isIdle = true;
    private void OnDestroy()
    {
        EventManager.Unregister<DiceRollRequest>(EventNames.SetUpDice, SetUpDice);
        EventManager.Unregister<Vector3>("LaunchDice", LaunchDice);
    }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Physics.defaultSolverIterations = 12;
        Physics.defaultSolverVelocityIterations = 12;

        // Validate the diePrefab and diceSpawnPoint before proceeding.
        if (Instance.diePrefab == null)
        {
            Debug.LogError("Die prefab is not assigned in the inspector.");
            Instance.enabled = false;
            return;
        }

        if (Instance.diceSpawnPoint == null)
        {
            Debug.LogError("Dice spawn point is not assigned in the inspector.");
            Instance.enabled = false;
            return;
        }

        DontDestroyOnLoad(gameObject);
        EventManager.Register(EventNames.SetUpDice, new EventHandler<DiceRollRequest>(SetUpDice));
        EventManager.Register(EventNames.LaunchDice, new EventHandler<Vector3>(LaunchDice));
    }

    // Update is called once per frame
    void Update()
    {
        if (m_isIdle)
        {
            // Continuously apply torque to keep them rolling.
            foreach (var diePrefab in m_spawnedDiceList)
            {
                var dieRigidBody = diePrefab.GetComponent<Rigidbody>();
                if (dieRigidBody)
                {
                    dieRigidBody.AddTorque(
                    Random.onUnitSphere * m_idleTorqueStrength,
                    ForceMode.Acceleration);
                }
                else
                {
                    Debug.LogWarning($"Die prefab [ {diePrefab.name} ] does not have a Rigidbody component.");
                }
            }
        }
    }

    public static void SetUpDice(DiceRollRequest rollRequest)
    {
        if (rollRequest == null || rollRequest.DiceEntries == null || rollRequest.DiceEntries.Count == 0)
        {
            Debug.LogError("Dice setup requires at least one die.");
            return;
        }

        var diceList = Instance.m_spawnedDiceList;
        var diceCount = rollRequest.DiceEntries.Count;
        Instance.m_activeDiceCount = diceCount;
        Instance.m_lstRetrievedResource.Clear();
        Instance.m_lstRetrievedBurn.Clear();
        Instance.m_settledDiceCount = 0;
        Instance.m_isIdle = true;

        var currentPosition = Instance.diceSpawnPoint.transform.position;
        currentPosition.x -= (diceCount - 1) / 2.0f * Instance.m_spawnPadding;

        for (int i = 0; i < diceCount; i++)
        {
            GameObject die;
            if (i < diceList.Count)
            {
                die = diceList[i];
                die.transform.position = currentPosition;
                die.SetActive(true);
            }
            else
            {
                die = Instantiate(
                    Instance.diePrefab,
                    currentPosition,
                    Quaternion.identity,
                    Instance.diceSpawnPoint.transform);
                die.name = $"Dice_{i}";
                diceList.Add(die);
            }

            if (!die.TryGetComponent(out DiceHandler handler))
            {
                Debug.LogError($"Die prefab [ {die.name} ] does not have a DiceHandler component.");
            }
            else
            {
                var entry = rollRequest.DiceEntries[i];
                handler.SetDiceInfo(entry.Dice, entry.Owner);
            }

            var dieRigidBody = die.GetComponent<Rigidbody>();
            if (dieRigidBody)
            {
                dieRigidBody.useGravity = false;
                dieRigidBody.angularVelocity = Random.insideUnitSphere * Instance.m_idleTorqueStrength;
            }
            else
            {
                Debug.LogWarning($"Die prefab [ {die.name} ] does not have a Rigidbody component.");
            }

            currentPosition.x += Instance.m_spawnPadding;
        }

        for (int i = diceCount; i < diceList.Count; i++)
        {
            diceList[i].SetActive(false);
        }
    }

    /// <summary>
    /// Launch the dice in the specified direction.
    /// </summary>
    /// <param name="toVector">Represents the direction and magnitude of the launch</param>
    public static void LaunchDice(Vector3 toVector)
    {
        if (Instance.m_activeDiceCount == 0)
        {
            Debug.LogWarning("No dice have been set up to launch.");
            return;
        }

        if (!Instance.m_isIdle)
        {
            Debug.LogWarning("Dice have been launched already.");
            return;
        }

        EventManager.TriggerEvent(EventNames.BeforeDiceRoll);

        var direction = toVector.normalized + (Vector3.down * Instance.m_downwardForce);
        var force = Mathf.Clamp(toVector.magnitude, Instance.m_minForce, Instance.m_maxForce);
        Debug.Log($"Launch force applied to the dice [ {force} ]");

        // Change the gravity to be stronger than the default to make the dice fall faster.
        // Note: This is a temporary change and should be reset after the dice have settled.
        // The default gravity in Unity is (0, -9.81, 0). Here we set it to (0, -12, 0) for a stronger effect.
        Physics.gravity = new Vector3(0, -12f, 0);

        // Stop applying continuous torque.
        Instance.m_isIdle = false;
        for (int i = 0; i < Instance.m_activeDiceCount; i++)
        {
            var die = Instance.m_spawnedDiceList[i];
            if (!die.TryGetComponent(out DiceHandler handler))
            {
                Debug.LogWarning($"Die prefab [ {die.name} ] does not have a DiceHandler component.");
                continue;
            }

            // Launch the die with the calculated direction and force,
            // and apply a random torque for realism.
            handler.LaunchDice(
                direction * force,
                Random.insideUnitSphere * Instance.m_launchTorqueStrength);
        }
    }

    private static void CompleteSettledDie()
    {
        Instance.m_settledDiceCount++;
        if (Instance.m_settledDiceCount != Instance.m_activeDiceCount)
        {
            return;
        }

        CompleteDiceRoll();
    }

    private static void CompleteDiceRoll()
    {
        // Processed resource faces are accumulated into the team resource totals.

        int red = 0;
        int blue = 0;
        int yellow = 0;

        // Process the results and update the resources accordingly.
        foreach (var face in Instance.m_lstRetrievedResource)
        {
            switch (face.m_type)
            {
                case ResourceFaceResult.Red:
                    red += face.amount;
                    break;
                case ResourceFaceResult.Blue:
                    blue += face.amount;
                    break;
                case ResourceFaceResult.Yellow:
                    yellow += face.amount;
                    break;
            }
        }

        if (Instance.m_lstRetrievedResource.Count > 0)
        {
            Debug.Log($"Dice result Red:{red} | Yellow:{yellow} | Blue:{blue}");

            EventManager.TriggerEvent(EventNames.ResourceUIUpdate, new Event_ResourceUpdate
            {
                red = red,
                blue = blue,
                yellow = yellow
            });

            EventManager.TriggerEvent(EventNames.AfterResourceDiceRoll);
        }

        foreach (var burnResult in Instance.m_lstRetrievedBurn)
        {
            if (burnResult.owner == null)
            {
                Debug.LogError("BurnDice result has no owning character.");
                continue;
            }

            switch (burnResult.face.m_type)
            {
                case BurnDamage.Flame:
                    if (StatusManager.Instance != null)
                    {
                        StatusManager.Instance.ApplyBurnDamage(burnResult.owner.gameObject, burnResult.face.amount);
                    }
                    break;
                case BurnDamage.Extinguish:
                    if (StatusManager.Instance != null)
                    {
                        StatusManager.Instance.RemoveStack(StatusTypes.Burn, burnResult.owner.gameObject);
                    }
                    break;
                case BurnDamage.Nothing:
                    break;
            }
        }

        EventManager.TriggerEvent(EventNames.AfterDiceRoll);
        EventManager.TriggerEvent("DisableDiceLaunch");

        Instance.m_lstRetrievedResource.Clear();
        Instance.m_lstRetrievedBurn.Clear();
        Instance.m_settledDiceCount = 0;
        Instance.m_isIdle = true;
        Physics.gravity = new Vector3(0, -9.81f, 0);
    }

    public static void ProcessDiceValue(
        string dieName,
        UnitController owner,
        BaseDice.DieFace<ResourceFaceResult> result)
    {
        Debug.Log($"Resource dice result for {dieName}, owner {owner?.name}: face {result.m_diceFaceIndex}");
        Instance.m_lstRetrievedResource.Add(result);
        CompleteSettledDie();
    }

    public static void ProcessDiceValue(
        string dieName,
        UnitController owner,
        BaseDice.DieFace<BurnDamage> result)
    {
        Debug.Log($"Burn dice result for {dieName}, owner {owner?.name}: {result.m_type}, amount {result.amount}");
        Instance.m_lstRetrievedBurn.Add((owner, result));
        CompleteSettledDie();
    }
}
