using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DamageEffect", menuName = "Scriptable Objects/Effect/DamageEffect")]
public class DamageEffect : BaseSkillEffect
{
    public override void Execute(
        UnitController caster, 
        UnitController target, 
        Dictionary<string, int> skillData)
    {
        //target.TakeDamage(skillData["Damage"]);
    }
}