using System.Collections.Generic;
using UnityEngine;

public class DieHandler : MonoBehaviour
{
    public int m_idleTorqueStrength = 10;
    public int m_launchTorqueStrength = 20;
    public int m_launchForceStrength = 10;
    public int m_diceCount = 6;
    public float m_spawnPadding = 100;
    // The diePrefab should be assigned in the Unity Inspector.
    [SerializeField]
    private GameObject diePrefab;
    // A list to keep track of the spawned dice.
    private List<GameObject> m_spawnedDiceList = new List<GameObject>();
    // A flag to indicate whether the dice are currently rolling.
    private bool isRolling = true;

    private void Awake()
    {
        // Ensure that the diePrefab is assigned in the inspector.
        if (diePrefab == null)
        {
            Debug.LogError("Die prefab is not assigned in the inspector.");
            // Disable this script to prevent further errors.
            enabled = false;
        }
    }

    private void Start()
    {
        if (diePrefab == null)
        {
            Debug.LogError("Die prefab is not assigned in the inspector.");
            enabled = false;
            return;
        }

        // Spawn the dice in a line with some padding between them.
        var currentPosition = transform.position;
        // Adjust the starting position to center the dice based on the count and padding.
        currentPosition.x -= (m_diceCount - 1) / 2.0f * m_spawnPadding;

        for (int i = 0; i < m_diceCount; i++)
        {
            m_spawnedDiceList.Add(Instantiate(diePrefab, currentPosition, Quaternion.identity));
            currentPosition.x += m_spawnPadding;
        }

        foreach (var die in m_spawnedDiceList)
        {
            var dieRigidBody = die.GetComponent<Rigidbody>();
            if (dieRigidBody)
            {
                // Disable gravity and apply a random torque to the die to make it spin in the air.
                dieRigidBody.useGravity = false;
                dieRigidBody.angularVelocity = Random.insideUnitSphere * m_idleTorqueStrength;
            }
            else
            {
                Debug.LogWarning($"Die prefab [ {die.name} ] does not have a Rigidbody component.");
            }
        }
    }

    private void Update()
    {
        if (isRolling)
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
    public void LaunchDice(Vector3 direction)
    {
        isRolling = false; // Stop applying continuous torque.
        foreach (var die in m_spawnedDiceList)
        {
            var dieRigidBody = die.GetComponent<Rigidbody>();
            if (dieRigidBody)
            {
                // Enable gravity and apply a random force to roll the die in the specified direction.
                dieRigidBody.useGravity = true;
                dieRigidBody.AddRelativeForce(direction * m_launchForceStrength, ForceMode.Impulse);
                // Add random spin for natural tumbling
                Vector3 randomTorque = Random.insideUnitSphere * m_launchTorqueStrength;
                dieRigidBody.AddTorque(randomTorque, ForceMode.Impulse);
            }
            else
            {
                Debug.LogWarning($"Die prefab [ {die.name} ] does not have a Rigidbody component.");
            }
        }
    }
}
