using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace DungeonDice.Dice
{
    /// <summary>
    /// One physical die in the box. Throws itself, waits to settle, reads the face that points up
    /// and hands that face index to <see cref="DiceManager"/>. The face table is fixed by the
    /// D6 mesh: +Z=1, +Y=2, −X=3, +X=4, −Y=5, −Z=6 (opposite faces sum to 7).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DiceHandler : MonoBehaviour
    {
        [System.Serializable]
        private struct FaceNormal
        {
            public int value;
            public Vector3 localNormal;
        }

        private static readonly FaceNormal[] s_faces =
        {
            new() { value = 1, localNormal = Vector3.forward },
            new() { value = 2, localNormal = Vector3.up },
            new() { value = 3, localNormal = Vector3.left },
            new() { value = 4, localNormal = Vector3.right },
            new() { value = 5, localNormal = Vector3.down },
            new() { value = 6, localNormal = Vector3.back },
        };

        [SerializeField, FormerlySerializedAs("m_diceFaceInfo")]
        private DieDefinition m_definition;

        private Rigidbody m_cmpRigidBody;
        private MeshRenderer m_renderer;
        /// <summary>The prefab's material, restored when neither the die nor the profile supplies one.</summary>
        private Material m_originalMaterial;

        public DieDefinition Definition
        {
            get => m_definition;
            set => m_definition = value;
        }

        /// <summary>Slot of the owner (hero index or traversal slot) for this throw. -1 when unset.</summary>
        public int OwnerId { get; set; } = -1;

        /// <summary>True from launch until the die has settled and reported its face.</summary>
        public bool IsSettling { get; private set; }

        public void Awake()
        {
            m_cmpRigidBody = GetComponent<Rigidbody>();
            if (!m_cmpRigidBody)
            {
                Debug.LogError("DiceHandler::Awake - Rigid body component is not attached to the DiceHandler.");
                enabled = false;
            }

            m_renderer = GetComponentInChildren<MeshRenderer>();
            if (m_renderer != null) m_originalMaterial = m_renderer.sharedMaterial;
        }

        /// <summary>
        /// Skin the die: its definition's material if set, else <paramref name="fallback"/> (the box
        /// profile's default die material), else the prefab's original material.
        /// </summary>
        public void ApplyVisual(Material fallback)
        {
            if (m_renderer == null) return;
            var definitionMaterial = m_definition != null ? m_definition.visualMaterial : null;
            m_renderer.sharedMaterial = ResolveMaterial(definitionMaterial, fallback, m_originalMaterial);
        }

        internal static Material ResolveMaterial(Material definitionMaterial, Material fallback, Material original)
        {
            if (definitionMaterial != null) return definitionMaterial;
            if (fallback != null) return fallback;
            return original;
        }

        public void LaunchDice(Vector3 launchForce, Vector3 torqueForce)
        {
            if (!m_cmpRigidBody)
            {
                Debug.LogError("DiceHandler::LaunchDice - Rigid body component is not attached to the DiceHandler.");
                enabled = false;
                return;
            }

            m_cmpRigidBody.useGravity = true;
            m_cmpRigidBody.AddForce(launchForce, ForceMode.Impulse);
            m_cmpRigidBody.AddTorque(torqueForce, ForceMode.Impulse);
            // Push it down so hard throws still land inside the box.
            m_cmpRigidBody.AddForce(Vector3.down * 12f, ForceMode.Impulse);

            IsSettling = true;
            StartCoroutine(WaitForDiceToSettle());
        }

        /// <summary>Face index 1–6 whose normal points closest to world up.</summary>
        internal int GetFaceUp() => FaceUpFor(transform);

        internal static int FaceUpFor(Transform t)
        {
            float bestDot = float.NegativeInfinity;
            int bestValue = -1;
            foreach (var face in s_faces)
            {
                float dot = Vector3.Dot(t.TransformDirection(face.localNormal), Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestValue = face.value;
                }
            }
            return bestValue;
        }

        /// <summary>
        /// Re-orients the die so <paramref name="faceIndex"/> points up, keeping its position.
        /// Used after a modifier changes the logical result so the shown face matches it.
        /// </summary>
        public void ShowFace(int faceIndex)
        {
            if (!DieDefinition.IsValidFaceIndex(faceIndex))
            {
                Debug.LogError($"DiceHandler::ShowFace - invalid face index {faceIndex}.");
                return;
            }

            var localNormal = s_faces[faceIndex - 1].localNormal;
            var worldNormal = transform.TransformDirection(localNormal);
            var correction = Quaternion.FromToRotation(worldNormal, Vector3.up);
            var rotation = correction * transform.rotation;

            if (m_cmpRigidBody && !m_cmpRigidBody.isKinematic)
            {
                m_cmpRigidBody.angularVelocity = Vector3.zero;
                m_cmpRigidBody.rotation = rotation;
            }
            transform.rotation = rotation;
        }

        private IEnumerator WaitForDiceToSettle()
        {
            while (!m_cmpRigidBody.IsSleeping())
            {
                yield return new WaitForFixedUpdate();
            }

            IsSettling = false;
            DiceManager.ProcessDiceValue(this, GetFaceUp());
        }
    }
}
