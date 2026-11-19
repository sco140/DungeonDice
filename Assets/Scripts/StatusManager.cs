using GameUtility;
using System.Collections.Generic;
using UnityEngine;

namespace GameUtility
{
    public abstract class BaseStatusEffect
    {
        GameObject m_target;
        public int m_stackCount;

        public virtual void ApplyEffect(GameObject target, int stackCount)
        {
            m_target = target;
            m_stackCount += stackCount;
            RegisterEffect();
        }
        public virtual void RemoveEffect()
        {
            m_target = null;
            m_stackCount = 0;
        }
        public abstract void TriggerEffect(EventMessage eventMessage);
        protected abstract void RegisterEffect();
    }

    public class ArmorStatusEffect : BaseStatusEffect
    {
        public override void TriggerEffect(EventMessage eventMessage)
        {
            DamageEventMessage damageEventMessage = eventMessage as DamageEventMessage;
            if (damageEventMessage != null)
            {
                // log error if damageEventMessage is null
                Debug.LogError($"Invalid event message for armor effect");
            }
            // Implementation for armor effect
        }
        protected override void RegisterEffect()
        {
            // Implementation for registering armor effect
            EventManager.Register(
                EventNames.BeforeDamageApplied, 
                new EventHandler<EventMessage>(TriggerEffect));
        }

        public override void RemoveEffect()
        {
            base.RemoveEffect();
            // Implementation for unregistering armor effect
            EventManager.Unregister<EventMessage>(
                EventNames.BeforeDamageApplied,
                TriggerEffect);
        }
    }

    public class StatusManager
    {
        Dictionary<StatusTypes, List<BaseStatusEffect>> m_statusEffects = new();
    }
}
