using System.Collections;
using System.Linq;
using DungeonDice.Core;
using DungeonDice.Dice;
using GameUtility;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DungeonDice.Tests
{
    /// <summary>
    /// The DiceBox prefab applies its Combat profile on Awake, swaps to Traversal on request
    /// (directly or via event), and skins dice from their definition or the profile fallback.
    /// </summary>
    public class DiceBoxVisualsTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/DiceBox.prefab";
        private const string CombatPath = "Assets/_Project/Data/DiceBox/Combat.asset";
        private const string TraversalPath = "Assets/_Project/Data/DiceBox/Traversal.asset";
        private const string ResourcePath = "Assets/_Project/Data/Dice/Resource.asset";

        private GameObject m_eventManager;
        private GameObject m_box;
        private DiceBoxVisuals m_visuals;
        private DiceBoxProfile m_combat;
        private DiceBoxProfile m_traversal;
        private DieDefinition m_resource;
        private DieDefinition m_plainDie;

        private static T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
            T asset = null;
#endif
            Assert.IsNotNull(asset, $"missing asset {path}");
            return asset;
        }

        [SetUp]
        public void SetUp()
        {
            m_eventManager = new GameObject("EventManager", typeof(EventManager));
            m_combat = Load<DiceBoxProfile>(CombatPath);
            m_traversal = Load<DiceBoxProfile>(TraversalPath);
            m_resource = Load<DieDefinition>(ResourcePath);
            Assert.IsNotNull(m_resource.visualMaterial, "the resource die should carry its own skin");

            m_plainDie = ScriptableObject.CreateInstance<DieDefinition>();
            m_plainDie.SetFaces(Enumerable.Range(1, 6).Select(DieFace.Number).ToArray());

            m_box = Object.Instantiate(Load<GameObject>(PrefabPath), new Vector3(0f, -500f, 0f), Quaternion.identity);
            m_visuals = m_box.GetComponent<DiceBoxVisuals>();
            Assert.IsNotNull(m_visuals, "prefab must carry DiceBoxVisuals; re-run DungeonDice ▸ Author DiceBox Prefab");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_box);
            Object.DestroyImmediate(m_eventManager);
            Object.DestroyImmediate(m_plainDie);
        }

        private Renderer Floor() => m_box.GetComponentsInChildren<Renderer>().First(r => r.name == "Floor");
        private Camera BoxCamera() => m_box.GetComponentInChildren<Camera>();

        [Test]
        public void CombatProfile_IsAppliedOnAwake()
        {
            Assert.AreSame(m_combat, m_visuals.Profile);
            Assert.AreEqual(m_combat.floorMaterial, Floor().sharedMaterial);
            Assert.AreEqual(m_combat.fieldOfView, BoxCamera().fieldOfView, 0.001f);
            Assert.AreEqual(m_combat.cameraLocalPosition, BoxCamera().transform.localPosition);
        }

        [Test]
        public void ApplyProfile_SwapsTrayCameraLightAndOverlay()
        {
            m_visuals.ApplyProfile(m_traversal);

            Assert.AreSame(m_traversal, m_visuals.Profile);
            Assert.AreEqual(m_traversal.floorMaterial, Floor().sharedMaterial);
            foreach (var wall in m_box.GetComponentsInChildren<Renderer>().Where(r => r.name != "Floor" && r.GetComponentInParent<DiceHandler>() == null))
                Assert.AreEqual(m_traversal.wallMaterial, wall.sharedMaterial, wall.name);

            var cam = BoxCamera();
            Assert.AreEqual(m_traversal.cameraLocalPosition, cam.transform.localPosition);
            Assert.AreEqual(m_traversal.fieldOfView, cam.fieldOfView, 0.001f);
            Assert.That(Quaternion.Angle(cam.transform.localRotation, Quaternion.Euler(m_traversal.cameraLocalEuler)), Is.LessThan(0.01f));

            var light = m_box.GetComponentInChildren<Light>();
            Assert.AreEqual(m_traversal.lightColor, light.color);
            Assert.AreEqual(m_traversal.lightIntensity, light.intensity, 0.001f);

            var dim = m_box.transform.Find("Overlay/Panel").GetComponent<Image>();
            Assert.AreEqual(m_traversal.overlayColor, dim.color);
        }

        [UnityTest]
        public IEnumerator SetDiceBoxProfileEvent_AppliesProfile()
        {
            EventManager.TriggerEvent(GameEvents.SetDiceBoxProfile, m_traversal);
            yield return null;
            Assert.AreSame(m_traversal, m_visuals.Profile);
        }

        [Test]
        public void Dice_UseTheirOwnSkin_OrTheProfileFallback()
        {
            DiceManager.SetUpDice(new[] { m_resource, m_plainDie });
            var dice = DiceManager.Instance.ActiveDice;
            Material SkinOf(int owner) => dice.First(d => d.OwnerId == owner).GetComponentInChildren<MeshRenderer>().sharedMaterial;

            Assert.AreEqual(m_resource.visualMaterial, SkinOf(0), "resource die keeps its own material");
            Assert.AreEqual(m_combat.defaultDieMaterial, SkinOf(1), "plain die takes the Combat fallback");

            m_visuals.ApplyProfile(m_traversal);
            Assert.AreEqual(m_resource.visualMaterial, SkinOf(0), "resource skin survives a profile swap");
            Assert.AreEqual(m_traversal.defaultDieMaterial, SkinOf(1), "plain die follows the new fallback");
        }
    }
}
