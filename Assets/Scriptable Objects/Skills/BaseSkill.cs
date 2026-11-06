using UnityEngine;
using System.Collections.Generic;
using GameUtility;

using SkillDataTable = GameUtility.SerializableDictionary<string, int>;

[CreateAssetMenu(
    fileName = "BaseSkill", 
    menuName = "Scriptable Objects/BaseSkill")]
public abstract class BaseSkill : ScriptableObject
{
    public string m_name;
    public string m_description;
    /// <summary>
    /// TKey - Skill data name.
    /// TValue - Value of the data associated with the key.
    /// </summary>
    [SerializeField]
    public SkillDataTable m_skillData;
    public List<BaseSkillEffect> m_skillEffectList;
    public virtual void Execute(
     UnitController caster,
     UnitController target)
    {
        foreach (var effect in m_skillEffectList)
        {
            effect.Execute(caster, target, m_skillData);
        }
    }
}
