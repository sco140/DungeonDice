using UnityEngine;

public class DieHandler : MonoBehaviour
{
    public int m_idleTorqueStrength = 10;
    public int m_launchTorqueStrength = 20;

    [SerializeField]
    private GameObject diePrefab;

    private bool isRolling = true;
    void Start()
    {
        if (diePrefab == null)
        {
            // If the diePrefab is not assigned, disable this script to prevent errors.
            enabled = false;
            return;
        }

        var dieRigidBody = diePrefab.GetComponent<Rigidbody>();
        if (dieRigidBody)
        {
            // Disable gravity and apply a random torque to the die to make it spin in the air.
            dieRigidBody.useGravity = false;
            dieRigidBody.angularVelocity = Random.insideUnitSphere * m_idleTorqueStrength;
        }

    }

    void Update()
    {
        if (isRolling)
        {
            // Continuously apply torque to keep it rolling
            diePrefab.GetComponent<Rigidbody>().AddTorque(
                Random.onUnitSphere * m_idleTorqueStrength, 
                ForceMode.Acceleration);
        }
    }
}
