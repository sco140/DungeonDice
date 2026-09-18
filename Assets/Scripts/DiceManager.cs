using GameUtility;
using System.Collections.Generic;
using UnityEngine;

public class DiceManager : MonoBehaviour
{
    // Singleton instance of the DiceManager.
    public static DiceManager Instance { get; private set; }

    public int m_idleTorqueStrength = 10;
    public int m_launchTorqueStrength = 20;
    public int m_diceCount = 6;
    public float m_spawnPadding = 100;
    public float m_downwardForce = 0.5f;
    public int m_processDiceCount = 0;
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

    public static void SpawnDice()
    {
        if (Instance.diePrefab == null)
        {
            Debug.LogError("Die prefab is not assigned in the inspector.");
            Instance.enabled = false;
            return;
        }

        // Spawn the dice in a line with some padding between them.
        var currentPosition = Instance.diceSpawnPoint.transform.position;
        // Adjust the starting position to center the dice based on the count and padding.
        currentPosition.x -= (Instance.m_diceCount - 1) / 2.0f * Instance.m_spawnPadding;

        for (int i = 0; i < Instance.m_diceCount; i++)
        {
            var newDice = Instantiate(
                Instance.diePrefab,
                currentPosition,
                Quaternion.identity,
                Instance.diceSpawnPoint.transform);
            
            // Updating padding for next dice.
            currentPosition.x += Instance.m_spawnPadding;

            // Update Dice name
            newDice.name = $"Dice_{i}";
            Instance.m_spawnedDiceList.Add(newDice);
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

    public static void ProcessDiceValue(string dieName, int result)
    {
        Debug.Log($"Dice result for {dieName}: {result}");

        Instance.m_processDiceCount++;

        if (Instance.m_processDiceCount == Instance.m_diceCount)
        {
            Instance.m_processDiceCount = 0;
            EventManager.TriggerEvent("UpdateResources", result);
        }
    }
}
