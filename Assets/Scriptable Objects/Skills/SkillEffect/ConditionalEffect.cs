using GameUtility;
using System;
using System.Collections.Generic;
using UnityEngine;
public enum ConditionType
{
    EventOccurred,
    ValueComparison,
    StatusCheck,
    MAX
}
public enum ComparisonType
{
    Equal,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    MAX
}
public enum TargetType
{
    Caster,
    Target,
    AllAllies,
    AllEnemies,
    Everyone,
    MAX
}
[Serializable]
public class ConditionData
{
    public string EventName;

    public TargetType Target;

    public ComparisonType Comparison;

    public int Value;
}
[CreateAssetMenu(fileName = "ConditionalEffect", menuName = "Scriptable Objects/Effect/ConditionalEffect")]
public class ConditionalEffect : BaseSkillEffect
{
    SerializableDictionary<string, int> m_conditionalEffects = new();
    List<BaseSkillEffect> m_trueCondition;
    List<BaseSkillEffect> m_falseCondition;
    public override void Execute(
        UnitController caster, 
        UnitController target, 
        Dictionary<string, int> skillData)
    {

    }
}
