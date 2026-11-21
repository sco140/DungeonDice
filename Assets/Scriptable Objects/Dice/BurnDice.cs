using UnityEngine;

namespace GameUtility
{
    public enum BurnDamage
    {
        Flame,
        Nothing,
        Extinguish
    }

    [CreateAssetMenu(
        fileName = "BurnDice",
        menuName = "Scriptable Objects/BurnDice")]
    public class BurnDice : BaseDice
    {
        public DieFace<BurnDamage>[] m_lstDieFaces = new DieFace<BurnDamage>[6]
        {
            new() {
                m_diceFaceIndex = 1,
                m_type = BurnDamage.Flame,
                amount = 1 },
            new() {
                m_diceFaceIndex = 2,
                m_type = BurnDamage.Flame,
                amount = 1 },
            new() {
                m_diceFaceIndex = 3,
                m_type = BurnDamage.Flame,
                amount = 3 },
            new() {
                m_diceFaceIndex = 4,
                m_type = BurnDamage.Nothing,
                amount = 1 },
            new() {
                m_diceFaceIndex = 5,
                m_type = BurnDamage.Extinguish,
                amount = 1 },
            new() {
                m_diceFaceIndex = 6,
                m_type = BurnDamage.Flame,
                amount = 5 }
        };
    }
}
