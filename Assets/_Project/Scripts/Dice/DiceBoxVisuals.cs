using DungeonDice.Core;
using GameUtility;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDice.Dice
{
    /// <summary>
    /// Applies a <see cref="DiceBoxProfile"/> to the box's tray, camera, light, overlay and dice,
    /// and plays the profile's throw/settle sounds. Lives on the DiceBox prefab root next to
    /// <see cref="DiceManager"/>. Swap profiles with <see cref="ApplyProfile"/> or by firing
    /// <c>GameEvents.SetDiceBoxProfile</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiceBoxVisuals : MonoBehaviour
    {
        [Tooltip("Applied on Awake. Leave empty to keep the prefab's own look.")]
        [SerializeField] private DiceBoxProfile m_defaultProfile;

        [Header("Wired by DiceBoxPrefabAuthoring")]
        [SerializeField] private Camera m_camera;
        [SerializeField] private Renderer m_floor;
        [SerializeField] private Renderer[] m_walls;
        [SerializeField] private Light m_light;
        [SerializeField] private AudioSource m_audio;
        [SerializeField] private Image m_overlayDim;

        /// <summary>The profile currently applied, or null if none has been.</summary>
        public DiceBoxProfile Profile { get; private set; }

        private void Awake()
        {
            EventManager.Register(GameEvents.SetDiceBoxProfile, new EventHandler<DiceBoxProfile>(ApplyProfile));
            EventManager.Register(GameEvents.DiceThrown, new EventHandler<int>(OnDiceThrown));
            EventManager.Register(GameEvents.RollCompleted, new EventHandler<RollCompletedEvent>(OnRollCompleted));

            // Applied in Awake, not Start, so the profile is in place before the first SetUpDice
            // (which can happen in another component's Start the same frame).
            if (m_defaultProfile != null) ApplyProfile(m_defaultProfile);
        }

        private void OnDestroy()
        {
            EventManager.Unregister<DiceBoxProfile>(GameEvents.SetDiceBoxProfile, ApplyProfile);
            EventManager.Unregister<int>(GameEvents.DiceThrown, OnDiceThrown);
            EventManager.Unregister<RollCompletedEvent>(GameEvents.RollCompleted, OnRollCompleted);
        }

        /// <summary>Apply every part of the profile and re-skin any dice already in the box.</summary>
        public void ApplyProfile(DiceBoxProfile profile)
        {
            if (profile == null)
            {
                Debug.LogWarning("DiceBoxVisuals: ignoring null profile.");
                return;
            }
            Profile = profile;

            if (m_floor != null && profile.floorMaterial != null) m_floor.sharedMaterial = profile.floorMaterial;
            if (m_walls != null && profile.wallMaterial != null)
            {
                foreach (var wall in m_walls)
                {
                    if (wall != null) wall.sharedMaterial = profile.wallMaterial;
                }
            }

            if (m_camera != null)
            {
                m_camera.transform.localPosition = profile.cameraLocalPosition;
                m_camera.transform.localEulerAngles = profile.cameraLocalEuler;
                m_camera.fieldOfView = profile.fieldOfView;
            }

            if (m_light != null)
            {
                m_light.color = profile.lightColor;
                m_light.intensity = profile.lightIntensity;
                m_light.range = profile.lightRange;
            }

            if (m_overlayDim != null) m_overlayDim.color = profile.overlayColor;
            if (m_audio != null) m_audio.volume = profile.volume;

            // Dice set up before this profile arrived need the new fallback material.
            var manager = GetComponent<DiceManager>();
            if (manager != null)
            {
                foreach (var die in manager.ActiveDice) die.ApplyVisual(profile.defaultDieMaterial);
            }
        }

        private void OnDiceThrown(int diceCount) => Play(Profile != null ? Profile.throwClip : null);

        private void OnRollCompleted(RollCompletedEvent roll) => Play(Profile != null ? Profile.settleClip : null);

        private void Play(AudioClip clip)
        {
            if (clip == null || m_audio == null) return;
            m_audio.PlayOneShot(clip);
        }
    }
}
