using UnityEngine;
using UnityEngine.Events;

using SkillDataTable = System.Collections.Generic.Dictionary<string, uint>;

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
    public SkillDataTable m_skillData;
    /// <summary>
    /// Events triggered when the skill is used.
    ///  <param name="UnitController">The unit that is the target of the skill.</param>
    ///  <param name="SkillDataTable">The skill data table.</param>
    /// </summary>
    public UnityEvent<UnitController, SkillDataTable> m_skillEffects;
}
