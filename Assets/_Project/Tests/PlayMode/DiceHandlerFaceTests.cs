using DungeonDice.Dice;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDice.Tests
{
    /// <summary>
    /// Verifies the physical face read: which face index <see cref="DiceHandler"/> reports for a
    /// given die orientation, and that <see cref="DiceHandler.ShowFace"/> re-orients a die so the
    /// requested face is read back. Face table: +Z=1, +Y=2, −X=3, +X=4, −Y=5, −Z=6.
    /// </summary>
    public class DiceHandlerFaceTests
    {
        private GameObject m_go;
        private DiceHandler m_handler;

        [SetUp]
        public void SetUp()
        {
            m_go = new GameObject("TestDie");
            var rb = m_go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            m_handler = m_go.AddComponent<DiceHandler>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(m_go);
        }

        [TestCase(0f, 0f, 0f, 2)]      // identity: +Y up
        [TestCase(0f, 0f, 180f, 5)]    // upside down: −Y up
        [TestCase(-90f, 0f, 0f, 1)]    // pitch so +Z points up
        [TestCase(90f, 0f, 0f, 6)]     // pitch so −Z points up
        [TestCase(0f, 0f, 90f, 4)]     // roll so +X points up
        [TestCase(0f, 0f, -90f, 3)]    // roll so −X points up
        [TestCase(0f, 45f, 0f, 2)]     // yaw does not change the face
        public void FaceUp_MatchesOrientation(float x, float y, float z, int expectedFace)
        {
            m_go.transform.rotation = Quaternion.Euler(x, y, z);
            Assert.AreEqual(expectedFace, m_handler.GetFaceUp());
            Assert.AreEqual(expectedFace, DiceHandler.FaceUpFor(m_go.transform));
        }

        [Test]
        public void ShowFace_ReorientsSoThatFaceReadsBack()
        {
            for (int face = 1; face <= 6; face++)
            {
                m_go.transform.rotation = Quaternion.Euler(17f, 123f, -40f); // arbitrary start
                m_handler.ShowFace(face);
                Assert.AreEqual(face, m_handler.GetFaceUp(), $"ShowFace({face})");
            }
        }

        [Test]
        public void OppositeFaces_ReadOppositeWhenFlippedPhysically()
        {
            for (int face = 1; face <= 6; face++)
            {
                m_handler.ShowFace(face);
                // Rotate 180° about an axis perpendicular to up: the opposite face comes up.
                m_go.transform.rotation = Quaternion.AngleAxis(180f, Vector3.forward) * m_go.transform.rotation;
                int read = m_handler.GetFaceUp();
                Assert.AreEqual(DieDefinition.OppositeFace(face), read, $"opposite of {face}");
            }
        }
    }
}
