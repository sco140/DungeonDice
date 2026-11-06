using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BurnEffect", menuName = "Scriptable Objects/BurnEffect")]
public class BurnEffect : BaseSkillEffect
{
    public override void Execute(
        UnitController caster,
        UnitController target,
        Dictionary<string, int> skillData)
    {
        //target.ApplyStatusEffect(StatusEffect.Burn, skillData["BurnCount"]);
    }
}
