using UnityEngine;

using SkillDataTable = System.Collections.Generic.Dictionary<string, int>;

[CreateAssetMenu(fileName = "SkillEffect", menuName = "Scriptable Objects/BaseSkillEffect")]
public abstract class BaseSkillEffect : ScriptableObject
{
    public abstract void Execute(
        UnitController caster,
        UnitController target,
        SkillDataTable skillData);
}
