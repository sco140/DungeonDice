using System.Collections.Generic;
using UnityEngine;

namespace GameUtility
{
    public class EventMessage
    {
        public Dictionary<string, int> m_parameters = new();
    }
    public class DamageEventMessage : EventMessage
    {
        public enum DamageOwnerType
        {
            PLAYER = 0,
            ENEMY = 1,
            STATUS_EFFECT = 2,
        }
        const string m_damageKey = "damage";
        const string m_damageOwnerTypeKey = "damageOwnerType";
        const string m_damageOwnerIdKey = "damageOwnerId";
        const string m_damageTargetTypeKey = "damageTargetType";
        const string m_damageTargetIdKey = "damageTargetId";

        public int GetDamageValue()
        {
            return m_parameters[m_damageKey];
        }

        public GameObject GetDamageOwner()
        {
            GameManager gameManager = GameManager.Instance;
            gameManager.TryGetUnit(
                m_parameters[m_damageOwnerTypeKey],
                m_parameters[m_damageOwnerIdKey],
                out GameObject damageOwner);
            return damageOwner;
        }

        public GameObject GetDamageTarget()
        {
            GameManager gameManager = GameManager.Instance;
            gameManager.TryGetUnit(
                m_parameters[m_damageTargetTypeKey],
                m_parameters[m_damageTargetIdKey],
                out GameObject damageTarget);
            return damageTarget;
        }

    }
    
    public class HealingEventMessage : EventMessage
    {
        const string m_healingKey = "healing";
        const string m_healingOwnerTypeKey = "healingOwnerType";
        const string m_healingOwnerIdKey = "healingOwnerId";
        const string m_healingTargetTypeKey = "healingTargetType";
        const string m_healingTargetIdKey = "healingTargetId";
        public int GetHealingValue()
        {
            return m_parameters[m_healingKey];
        }
        public GameObject GetHealingOwner()
        {
            GameManager gameManager = GameManager.Instance;
            gameManager.TryGetUnit(
                m_parameters[m_healingOwnerTypeKey],
                m_parameters[m_healingOwnerIdKey],
                out GameObject healingOwner);
            return healingOwner;
        }
        public GameObject GetHealingTarget()
        {
            GameManager gameManager = GameManager.Instance;
            gameManager.TryGetUnit(
                m_parameters[m_healingTargetTypeKey],
                m_parameters[m_healingTargetIdKey],
                out GameObject healingTarget);
            return healingTarget;
        }
    }

    public class StatusAppliedEventMessage : EventMessage
    {
        const string m_statusKey = "status";
        const string m_statusOwnerTypeKey = "statusOwnerType";
        const string m_statusOwnerIdKey = "statusOwnerId";
        const string m_statusTargetTypeKey = "statusTargetType";
        const string m_statusTargetIdKey = "statusTargetId";
        public int GetStatusValue()
        {
            return m_parameters[m_statusKey];
        }
        public GameObject GetStatusOwner()
        {
            GameManager gameManager = GameManager.Instance;
            gameManager.TryGetUnit(
                m_parameters[m_statusOwnerTypeKey],
                m_parameters[m_statusOwnerIdKey],
                out GameObject statusOwner);
            return statusOwner;
        }
        public GameObject GetStatusTarget()
        {
            GameManager gameManager = GameManager.Instance;
            gameManager.TryGetUnit(
                m_parameters[m_statusTargetTypeKey],
                m_parameters[m_statusTargetIdKey],
                out GameObject statusTarget);
            return statusTarget;
        }
    }

    public class DiceRolledEventMessage : EventMessage
    {
        const string m_diceFaceValueKey = "diceFaceValue";
        const string m_diceTypeKey = "diceType";
        const string m_diceOwnerTypeKey = "diceOwnerType";
        const string m_diceOwnerIdKey = "diceOwnerId";
        public ResourceDiceFaceValue GetDiceFaceValue()
        {
            return (ResourceDiceFaceValue)m_parameters[m_diceFaceValueKey];
        }
        public GameObject GetDiceOwner()
        {
            GameManager gameManager = GameManager.Instance;
            gameManager.TryGetUnit(
                m_parameters[m_diceOwnerTypeKey],
                m_parameters[m_diceOwnerIdKey],
                out GameObject diceOwner);
            return diceOwner;
        }
        public DiceType GetDiceType()
        {
            return (DiceType)m_parameters[m_diceTypeKey];
        }
    }
}
