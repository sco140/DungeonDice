using UnityEngine;

namespace GameUtility
{
    public enum ResourceFaceResult
    {
        Red,
        Blue,
        Yellow,
        Nothing,
        Max
    }

    [CreateAssetMenu(
        fileName = "ResourceDice",
        menuName = "Scriptable Objects/ResourceDice")]
    public class ResourceDice : BaseDice
    {
        public DieFace<ResourceFaceResult>[] m_lstDieFaces = new DieFace<ResourceFaceResult>[6]
        {
            new() {
                m_diceFaceIndex = 1,
                m_type = ResourceFaceResult.Red,
                amount = 1 },
            new() {
                m_diceFaceIndex = 2,
                m_type = ResourceFaceResult.Blue,
                amount = 1 },
            new() {
                m_diceFaceIndex = 3,
                m_type = ResourceFaceResult.Yellow,
                amount = 1 },
            new() {
                m_diceFaceIndex = 4,
                m_type = ResourceFaceResult.Yellow,
                amount = 1 },
            new() {
                m_diceFaceIndex = 5,
                m_type = ResourceFaceResult.Blue,
                amount = 1 },
            new() {
                m_diceFaceIndex = 6,
                m_type = ResourceFaceResult.Red,
                amount = 1 }
        };
    }
}
