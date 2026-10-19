using System;
using System.Collections.Generic;
using UnityEngine;
using GameUtility;
[CreateAssetMenu(
    fileName = "BaseSkill", 
    menuName = "Scriptable Objects/BaseSkill")]
public abstract class BaseSkill : ScriptableObject
{
    public string m_name;
    public uint m_cooldown;
    public Mana m_manaCost;
    public abstract bool CanTarget(GameObject caster, GameObject target);
    public abstract void Resolve(GameObject caster, List<GameObject> targets);
}
