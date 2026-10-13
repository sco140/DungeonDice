using UnityEngine;


namespace GameUtility
{
    public enum DiceResultType
    {
        Red,
        Blue,
        Yellow,
        Nothing,
        Attack,
        Armor,
        Max
    }

    [CreateAssetMenu(
        fileName = "BaseDice",
        menuName = "Scriptable Objects/BaseDice")]
    public class BaseDice : ScriptableObject
    {
        [System.Serializable]
        public struct ResourceFace
        {
            // The index of the face on the dice, ranging from 1 - 6.
            public int m_diceFaceIndex;
            // The type of result this face represents.
            public DiceResultType m_type;
            // The amount of the result this face provides.
            public int amount;

        }

        public ResourceFace[] m_lstDieFaces = new ResourceFace[6] {
        new() {
            m_diceFaceIndex = 1,
            m_type = DiceResultType.Red,
            amount = 1 },
        new() {
            m_diceFaceIndex = 2,
            m_type = DiceResultType.Blue,
            amount = 1 },
        new() {
            m_diceFaceIndex = 3,
            m_type = DiceResultType.Yellow,
            amount = 1 },
        new() {
            m_diceFaceIndex = 4,
            m_type = DiceResultType.Yellow,
            amount = 1 },
        new() {
            m_diceFaceIndex = 5,
            m_type = DiceResultType.Blue,
            amount = 1 },
        new() {
            m_diceFaceIndex = 6,
            m_type = DiceResultType.Red,
            amount = 1 }
    };

    }
}
