using UnityEngine;

namespace GameUtility
{
    [CreateAssetMenu(
        fileName = "BaseDice",
        menuName = "Scriptable Objects/BaseDice")]
    public class BaseDice : ScriptableObject
    {
        [System.Serializable]
        public struct DieFace<I>
        {
            // The index of the face on the dice, ranging from 1 - 6.
            public int m_diceFaceIndex;
            // The type of result this face represents.
            public I m_type;
            // The amount of the result this face provides.
            public int amount;
        }
    }
}
