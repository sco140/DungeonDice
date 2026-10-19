using DungeonDice.Dice;
using GameUtility;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
	fileName = "Characters",
	menuName = "Scriptable Objects/Characters")]
public abstract class BaseCharacters : ScriptableObject
{
	[Range(0, 100)]
	public uint m_nHealth = 50;
	[Range(0, 100)]
	public uint m_nAttack = 50;

	public List<DieDefinition> m_lstDiceValues;
	// TODO: Create a class to handle skills and add it here.
	public BaseSkill[] m_lstSkills = new BaseSkill[4];
    // TODO: Create a way to handle passive skills and add it here.
    /// <summary>
    /// @Key - The Event name that will be used to trigger the skill.
    /// </summary>
    private Dictionary<string, List<BaseSkill>> m_dPassiveEffects;
	public virtual void OnValidate()
	{
		if (m_nHealth > 100)
		{
			m_nHealth = 100;
		}

		if (m_nAttack > 100)
		{
			m_nAttack = 100;
		}
	}
}
