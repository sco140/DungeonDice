using System.Collections.Generic;
using UnityEngine;

namespace GameUtility
{
    public enum StatusTypes
    {
        None = 0,
        Armor = 1 << 0,
        Burn = 1 << 1,
        Bleeding = 1 << 2,
        CarrionArmor = 1 << 3,
        Cooldown = 1 << 4,
        Usage_Cooldown = 1 << 5,
        Fear = 1 << 6,
        Freeze = 1 << 7,
        Poison = 1 << 8,
        Stun = 1 << 9,
        MAX,
    }

    public abstract class BaseStatusEffect
    {
        protected GameObject m_target;
        protected int m_stackCount;

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
        public ArmorStatusEffect(GameObject target, int stackCount)
        {
            ApplyEffect(target, stackCount);
        }

        public override void TriggerEffect(EventMessage eventMessage)
        {
            DamageEventMessage damageEventMessage = eventMessage as DamageEventMessage;
            if (damageEventMessage != null)
            {
                // log error if damageEventMessage is null
                Debug.LogError($"Invalid event message for armor effect");
                return;
            }

            // check if the target of the damage event is the same as the target of the armor effect
            if (damageEventMessage.GetDamageTarget() != m_target)
            {
                return;
            }

            // Implementation for armor effect
            // Reduce the incoming damage by the stack count of the armor effect.
            // If the incoming damage is greater than the stack count, reduce the damage by the stack count and remove the armor effect.
            // If the incoming damage is less than or equal to the stack count, reduce the damage to 0 and decrease the stack count by the incoming damage amount.
            // If the stack count reaches 0, remove the armor effect.
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
    public class BurnStatusEffect : BaseStatusEffect
    {
        public BurnStatusEffect(GameObject target, int stackCount)
        {
            ApplyEffect(target, stackCount);
        }
        public override void TriggerEffect(EventMessage eventMessage)
        {
            if (m_stackCount <= 0)
            {
                RemoveEffect();
                return;
            }

            // Implementation for burn effect
            // Trigger an additional roll of a burn damage dice based on the stack count of the burn effect.
            // For example, if the stack count is 2, roll 2 burn damage dice and apply the total damage to the target.
            // Decrease the stack count by 1 after each burn damage dice roll, and remove the burn effect when the stack count reaches 0.
        }
        protected override void RegisterEffect()
        {
            // Implementation for registering burn effect
            EventManager.Register(
                EventNames.AfterDiceRoll,
                new EventHandler<EventMessage>(TriggerEffect));
        }
        public override void RemoveEffect()
        {
            base.RemoveEffect();
            // Implementation for unregistering burn effect
            EventManager.Unregister<EventMessage>(
                EventNames.AfterDiceRoll,
                TriggerEffect);
        }
    }
    public class StatusManager : MonoBehaviour
    {
        public static StatusManager Instance { get; private set; }

        Dictionary<StatusTypes, List<BaseStatusEffect>> m_statusEffects = new();

        public void ApplyStatusEffect(StatusTypes effect, GameObject target, int stackCount)
        {
            BaseStatusEffect statusEffect = null;

            StatusPackage statusEvent = new()
            {
                target = target,
                statusEffect = effect,
                statusParams = new Dictionary<string, int> { { "stackCount", stackCount } }
            };

            switch (effect)
            {
                case StatusTypes.Armor:
                    statusEffect = new ArmorStatusEffect(target, stackCount);
                    break;
                case StatusTypes.Burn:
                    break;
                case StatusTypes.Bleeding:
                    break;
                case StatusTypes.CarrionArmor:
                    break;
                case StatusTypes.Cooldown:
                    break;
                case StatusTypes.Usage_Cooldown:
                    break;
                case StatusTypes.Fear:
                    break;
                case StatusTypes.Freeze:
                    break;
                case StatusTypes.Poison:
                    break;
                case StatusTypes.Stun:
                    break;
                case StatusTypes.None:
                case StatusTypes.MAX:
                default:
                    Debug.LogError($"Invalid status effect type: {effect}");
                    return;
            }

            if (statusEffect != null)
            {
                EventManager.TriggerEvent(
                    EventNames.BeforeStatusApplied,
                    statusEvent);

                if (!m_statusEffects.ContainsKey(effect))
                {
                    m_statusEffects.Add(effect, new List<BaseStatusEffect>());
                }

                m_statusEffects[effect].Add(statusEffect);

                EventManager.TriggerEvent(
                    EventNames.AfterStatusApplied,
                    statusEvent);
            }

        }
    }
}
