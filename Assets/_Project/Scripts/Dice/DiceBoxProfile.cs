using UnityEngine;

namespace DungeonDice.Dice
{
    /// <summary>
    /// Everything that makes the dice box look and sound like one context (combat, traversal, ...).
    /// One box, many profiles: <see cref="DiceBoxVisuals.ApplyProfile"/> swaps them at runtime.
    /// Hero and traversal dice bring their own material via <see cref="DieDefinition.visualMaterial"/>;
    /// <see cref="defaultDieMaterial"/> covers dice that do not.
    /// </summary>
    [CreateAssetMenu(fileName = "DiceBoxProfile", menuName = "DungeonDice/Dice Box Profile")]
    public class DiceBoxProfile : ScriptableObject
    {
        public string displayName;

        [Header("Tray")]
        public Material floorMaterial;
        [Tooltip("Applied to walls, ceiling and corner wedges.")]
        public Material wallMaterial;

        [Header("Dice")]
        [Tooltip("Used for any die whose DieDefinition has no visualMaterial.")]
        public Material defaultDieMaterial;

        [Header("Camera framing (local to the arena)")]
        public Vector3 cameraLocalPosition = new(1f, 32.9f, -10.1f);
        public Vector3 cameraLocalEuler = new(67.35f, 0f, 0f);
        [Range(10f, 120f)] public float fieldOfView = 64.8f;

        [Header("Light (point light above the tray)")]
        public Color lightColor = Color.white;
        [Min(0f)] public float lightIntensity = 1f;
        [Min(1f)] public float lightRange = 60f;

        [Header("Overlay")]
        [Tooltip("Colour of the full-screen dim behind the dice view.")]
        public Color overlayColor = new(0f, 0f, 0f, 0.6f);

        [Header("Audio (optional)")]
        public AudioClip throwClip;
        public AudioClip settleClip;
        [Range(0f, 1f)] public float volume = 1f;
    }
}
