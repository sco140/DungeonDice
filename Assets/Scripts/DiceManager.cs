using GameUtility;
using System.Collections.Generic;
using UnityEngine;
using static BaseDice;

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
    private List<ResourceFace> m_lstRetrievedResource = new List<ResourceFace>();
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
    // A flag to indicate whether the dice are currently idle.
    private bool m_isIdle = true;

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

        DontDestroyOnLoad(gameObject);
        EventManager.Register("SpawnDice", new EventHandler<int>(SpawnDice));
        EventManager.Register("LaunchDice", new EventHandler<Vector3>(LaunchDice));
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

    public static void SpawnDice(int diceCount)
    {
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

        if (Instance.m_spawnedDiceList.Count < diceCount)
        {
            for (int i = 0; i < diceCount; i++)
            {
                // Create a new die
                var newDice = Instantiate(
                   Instance.diePrefab,
                   Vector3.zero,
                   Quaternion.identity,
                   Instance.diceSpawnPoint.transform);
                
                // Update dice name
                newDice.name = $"Dice_{i}";

                Instance.m_spawnedDiceList.Add(newDice);
            }
        }
        else if (Instance.m_spawnedDiceList.Count > diceCount)
        {
            // Remove extra dice if the count is less than the current list.
            for (int i = diceCount, count = Instance.m_spawnedDiceList.Count; i < count; i++)
            {
                Instance.m_spawnedDiceList[i].SetActive(false);
            }
        }

        ref var diceList = ref Instance.m_spawnedDiceList;
        // Spawn the dice in a line with some padding between them.
        var currentPosition = Instance.diceSpawnPoint.transform.position;
        // Adjust the starting position to center the dice based on the count and padding.
        currentPosition.x -= (diceCount - 1) / 2.0f * Instance.m_spawnPadding;

        // Loop through the dice and set their positions
        // based on the calculated starting position and padding.
        for (int i = 0; i < diceCount; i++)
        {
            diceList[i].transform.position = currentPosition;
            // Updating padding for next dice.
            currentPosition.x += Instance.m_spawnPadding;
        }

        foreach (var die in Instance.m_spawnedDiceList)
        {
            var dieRigidBody = die.GetComponent<Rigidbody>();
            if (dieRigidBody)
            {
                // Disable gravity and apply a random torque to the die to make it spin in the air.
                dieRigidBody.useGravity = false;
                dieRigidBody.angularVelocity = Random.insideUnitSphere * Instance.m_idleTorqueStrength;
            }
            else
            {
                Debug.LogWarning($"Die prefab [ {die.name} ] does not have a Rigidbody component.");
            }
        }
    }

    /// <summary>
    /// Launch the dice in the specified direction.
    /// </summary>
    /// <param name="toVector">Represents the direction and magnitude of the launch</param>
    public static void LaunchDice(Vector3 toVector)
    {
        if (Instance.m_spawnedDiceList.Count == 0)
        {
            Debug.LogWarning("No dice have been spawned to launch.");
            return;
        }

        if (!Instance.m_isIdle)
        {
            Debug.LogWarning("Dice have been launched already.");
            return;
        }

        var direction = toVector.normalized + (Vector3.down * Instance.m_downwardForce);
        var force = Mathf.Clamp(toVector.magnitude, Instance.m_minForce, Instance.m_maxForce);
        Debug.Log($"Launch force applied to the dice [ {force} ]");

        // Change the gravity to be stronger than the default to make the dice fall faster.
        // Note: This is a temporary change and should be reset after the dice have settled.
        // The default gravity in Unity is (0, -9.81, 0). Here we set it to (0, -12, 0) for a stronger effect.
        Physics.gravity = new Vector3(0, -12f, 0);

        // Stop applying continuous torque.
        Instance.m_isIdle = false;
        foreach (var die in Instance.m_spawnedDiceList)
        {
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

    public static void ProcessDiceValue(string dieName, ResourceFace result)
    {
        Debug.Log($"Dice result for {dieName}: {result}");

        Instance.m_lstRetrievedResource.Add(result);
        if (Instance.m_lstRetrievedResource.Count == Instance.m_spawnedDiceList.Count)
        {
            int red = 0;
            int blue = 0;
            int yellow = 0;

            // Process the results and update the resources accordingly.
            foreach (var face in Instance.m_lstRetrievedResource)
            {
                switch (face.m_type)
                {
                    case DiceResultType.Red:
                        red += face.amount;
                        break;
                    case DiceResultType.Blue:
                        blue += face.amount;
                        break;
                    case DiceResultType.Yellow:
                        yellow += face.amount;
                        break;
                }
            }

            EventManager.TriggerEvent("UpdateResources", new Event_ResourceUpdate
            {
                red = red,
                blue = blue,
                yellow = yellow
            });

            // Removing all result values after processing to prepare for the next dice roll.
            Instance.m_lstRetrievedResource.Clear();
        }
    }
}
