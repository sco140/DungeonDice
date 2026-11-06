using GameUtility;
using UnityEngine;

using SkillDataTable = System.Collections.Generic.Dictionary<string, int>;

public class GenericSkillList : MonoBehaviour
{
    void DealDamage(UnitController target, SkillDataTable skillInfo)
    {
        // Implement damage logic here.
    }

    void ApplyStatusEffect(UnitController target, SkillDataTable skillInfo)
    {
        // Implement status effect logic here.
    }

    void Heal(UnitController target, SkillDataTable skillInfo)
    {
        // Implement healing logic here.
    }

    void ModifyMana(UnitController target, SkillDataTable skillInfo)
    {
        // Implement mana modification logic here.
    }
}
