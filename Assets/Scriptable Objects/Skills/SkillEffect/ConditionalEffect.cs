using GameUtility;
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
public class ConditionData
{
    public string eventName;

    public TargetType target;

    public ComparisonType comparison;
    public string parameterName;
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
    List<BaseSkillEffect> m_conditionMetEffect;
    List<BaseSkillEffect> m_baseEffect;
    bool m_conditionMet = false;
    void CheckCondition(EventMessage message)
    {
        if (m_conditionMet)
        {
            return;
        }

        m_conditionMet = false;

        foreach (var condition in m_conditionalEffects)
        {
            message.m_parameters.TryGetValue(condition.parameterName, out int eventValue);

            if (condition.CheckCondition(eventValue))
            {
                m_conditionMet = true;
                return;
            }
        }
    }

    public void AddConditionalEffect(
        ConditionData condition)
    {
        EventManager.Register(
            condition.eventName, 
            new EventHandler<EventMessage>(CheckCondition));

        m_conditionalEffects.Add(condition);
    }

    public override void Execute(
        UnitController caster,
        UnitController target,
        Dictionary<string, int> skillData)
    {
        if (m_conditionMet)
        {
            foreach (var effect in m_conditionMetEffect)
            {
                effect.Execute(caster, target, skillData);
            }
        }
        else
        {
            foreach (var effect in m_baseEffect)
            {
                effect.Execute(caster, target, skillData);
            }
        }
    }

    ~ConditionalEffect()
    {
        foreach (var condition in m_conditionalEffects)
        {
            EventManager.Unregister<EventMessage>(
                condition.eventName,
                CheckCondition);
        }
    }
}
