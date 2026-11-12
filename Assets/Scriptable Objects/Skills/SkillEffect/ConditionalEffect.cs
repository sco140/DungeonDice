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
    public string eventName;

    public TargetType target;

    public ComparisonType comparison;

    public int value;

    public bool CheckCondition(int eventValue)
    {
        switch (comparison)
        {
            case ComparisonType.Equal:
                return eventValue == value;
            case ComparisonType.GreaterThan:
                return eventValue > value;
            case ComparisonType.GreaterThanOrEqual:
                return eventValue >= value;
            case ComparisonType.LessThan:
                return eventValue < value;
            case ComparisonType.LessThanOrEqual:
                return eventValue <= value;
            default:
                Debug.LogError("Invalid comparison type");
                return false;
        }
    }
}
[CreateAssetMenu(fileName = "ConditionalEffect", menuName = "Scriptable Objects/Effect/ConditionalEffect")]
public class ConditionalEffect : BaseSkillEffect
{
    List<ConditionData> m_conditionalEffects = new();
    List<BaseSkillEffect> m_trueCondition;
    List<BaseSkillEffect> m_falseCondition;
    bool m_conditionMet = false;
    void CheckCondition(EventMessage eventValue)
    {
        if (m_conditionMet)
        {
            return;
        }
        //foreach (var condition in m_conditionalEffects)
        //{
        //    if (!condition.CheckCondition(eventValue))
        //    {
        //        m_conditionMet = false;
        //        return;
        //    }
        //}
        m_conditionMet = true;
    }

    public void AddConditionalEffect(
        ConditionData condition)
    {
        m_conditionalEffects.Add(condition);
    }

    public override void Execute(
        UnitController caster,
        UnitController target,
        Dictionary<string, int> skillData)
    {

    }
}
