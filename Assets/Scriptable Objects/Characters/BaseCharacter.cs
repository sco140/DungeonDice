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

    //TODO: Create a class to handle dice and add it here.
    //public List<List<DiceFaceValue>> m_lstDiceValues;
    // TODO: Create a class to handle skills and add it here.
    public BaseSkill[] m_lstSkills = new BaseSkill[4];
    // TODO: Create a way to handle passive skills and add it here.
    private Dictionary<string, BaseSkill> m_dActions;
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

	public abstract void OnStartOfTurn();
	public abstract void OnBeforeRoll();
	public abstract void OnAfterRoll();
	public abstract void OnBeforeDamage();
	public abstract void OnAfterDamage();
	public abstract void OnEndOfTurn();
}
