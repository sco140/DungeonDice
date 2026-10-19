using DungeonDice.Dice;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDice.Tests
{
    /// <summary>Material precedence for a die: definition, then box profile fallback, then prefab original.</summary>
    public class DiceHandlerMaterialTests
    {
        private Material m_definition, m_fallback, m_original;

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            m_definition = new Material(shader) { name = "definition" };
            m_fallback = new Material(shader) { name = "fallback" };
            m_original = new Material(shader) { name = "original" };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_definition);
            Object.DestroyImmediate(m_fallback);
            Object.DestroyImmediate(m_original);
        }

        [Test]
        public void DefinitionMaterial_WinsOverEverything() =>
            Assert.AreSame(m_definition, DiceHandler.ResolveMaterial(m_definition, m_fallback, m_original));

        [Test]
        public void ProfileFallback_UsedWhenDefinitionHasNone() =>
            Assert.AreSame(m_fallback, DiceHandler.ResolveMaterial(null, m_fallback, m_original));

        [Test]
        public void Original_UsedWhenNothingElseIsSet() =>
            Assert.AreSame(m_original, DiceHandler.ResolveMaterial(null, null, m_original));
    }
}
