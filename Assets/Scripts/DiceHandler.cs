using System.Collections;
using UnityEngine;

[System.Serializable]
public struct DieFace
{
    public int value;
    public Vector3 localNormal; // normal in local space
}

[RequireComponent(typeof(Rigidbody))]
public class DiceHandler : MonoBehaviour
{
    public DieFace[] faces = new DieFace[]
    {
        new() {
            value = 1,
            localNormal = Vector3.forward },
        new() {
            value = 2,
            localNormal = Vector3.up },
        new() {
            value = 3,
            localNormal = Vector3.left },
        new() {
            value = 4,
            localNormal = Vector3.right },
        new() {
            value = 5,
            localNormal = Vector3.down },
        new() {
            value = 6,
            localNormal = Vector3.back }
    };
    const float m_settleThreshold = 0.05f;
    public float m_linearDamping = 0.1f;
    public float m_angularDamping = 0.05f;
    private Rigidbody m_cmpRigidBody;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_cmpRigidBody = GetComponent<Rigidbody>();
        if (!m_cmpRigidBody)
        {
            Debug.LogError("DiceHandler::Start - Rigid body component is not " +
                "attached to the DiceHandler.");
            enabled = false;
            return;
        }

        m_cmpRigidBody.useGravity = false;
        m_cmpRigidBody.linearDamping = m_linearDamping;
        m_cmpRigidBody.angularDamping = m_angularDamping;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void LaunchDice(Vector3 launchForce, Vector3 torqueForce)
    {
        if (!m_cmpRigidBody)
        {
            Debug.LogError("DiceHandler::LaunchDice - Rigid body component is not " +
                "attached to the DiceHandler.");
            enabled = false;
            return;
        }

        // Enable gravity for the dice
        m_cmpRigidBody.useGravity = true;
        // Launch force
        m_cmpRigidBody.AddForce(launchForce, ForceMode.Impulse);
        // Random spin
        m_cmpRigidBody.AddTorque(torqueForce, ForceMode.Impulse);
        // Force it down.
        m_cmpRigidBody.AddForce(Vector3.down * 12f, ForceMode.Impulse);

        // Start a coroutine to wait for the dice to settle and then read the face value.
        StartCoroutine(WaitForDiceToSettle());
    }

    bool IsSettled(Rigidbody rb)
    {
        return rb.linearVelocity.sqrMagnitude < m_settleThreshold &&
               rb.angularVelocity.sqrMagnitude < m_settleThreshold;
    }

    int GetFaceUp()
    {
        float bestDot = -1f;
        int bestValue = -1;

        foreach (var face in faces)
        {
            Vector3 worldNormal = transform.TransformDirection(face.localNormal);
            float dot = Vector3.Dot(worldNormal, Vector3.up);

            if (dot > bestDot)
            {
                bestDot = dot;
                bestValue = face.value;
            }
        }
        return bestValue;
    }

    IEnumerator WaitForDiceToSettle()
    {
        // Wait until the die stops moving
        while (!m_cmpRigidBody.IsSleeping())
        {
            yield return new WaitForFixedUpdate();
        }

        // Now read the face
        int result = GetFaceUp();
        Debug.Log($"{name}'s Die result: " + result);
    }
}
