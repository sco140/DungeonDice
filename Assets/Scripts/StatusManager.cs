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

        public int StackCount => m_stackCount;

        public bool IsTarget(GameObject target)
        {
            return m_target == target;
        }

        public void AddStacks(int stackCount)
        {
            m_stackCount += Mathf.Max(0, stackCount);
        }

        public void RemoveStack()
        {
            m_stackCount = Mathf.Max(0, m_stackCount - 1);
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
            if (eventMessage is not DamageEventMessage damageEventMessage)
            {
                Debug.LogError("Invalid event message for armor effect");
                return;
            }

            if (damageEventMessage.GetDamageTarget() != m_target)
            {
                return;
            }

            int damage = damageEventMessage.GetDamageValue();
            int absorbed = Mathf.Min(damage, m_stackCount);
            damage -= absorbed;
            m_stackCount -= absorbed;
            damageEventMessage.SetDamageValue(damage);

            if (m_stackCount <= 0)
            {
                RemoveEffect();
            }
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
            EventManager.Unregister<EventMessage>(
                EventNames.BeforeDamageApplied,
                TriggerEffect);

            if (StatusManager.Instance != null)
            {
                StatusManager.Instance.RemoveStatusEffect(StatusTypes.Armor, this);
            }
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
        }

        protected override void RegisterEffect()
        {
        }

        public void ApplyBurnDamage(int damage)
        {
            if (m_target == null || damage <= 0)
            {
                return;
            }

            if (m_target.TryGetComponent(out UnitController unit))
            {
                unit.TakeDamage(damage, DamageType.Burn, m_target);
            }
        }

        public override void RemoveEffect()
        {
            base.RemoveEffect();
        }
    }
    public class StatusManager : MonoBehaviour
    {
        public static StatusManager Instance { get; private set; }

        Dictionary<StatusTypes, List<BaseStatusEffect>> m_statusEffects = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public int GetStackCount(StatusTypes effect, GameObject target)
        {
            if (target == null || !m_statusEffects.TryGetValue(effect, out var effects))
            {
                return 0;
            }

            int count = 0;
            foreach (var statusEffect in effects)
            {
                if (statusEffect.IsTarget(target))
                {
                    count += statusEffect.StackCount;
                }
            }

            return count;
        }

        public void ApplyBurnDamage(GameObject target, int damage)
        {
            if (target == null || !m_statusEffects.TryGetValue(StatusTypes.Burn, out var effects))
            {
                return;
            }

            foreach (var statusEffect in effects)
            {
                if (statusEffect is BurnStatusEffect burnEffect && statusEffect.IsTarget(target))
                {
                    burnEffect.ApplyBurnDamage(damage);
                    return;
                }
            }
        }

        public void RemoveStatusEffect(StatusTypes effect, BaseStatusEffect statusEffect)
        {
            if (!m_statusEffects.TryGetValue(effect, out var effects))
            {
                return;
            }

            effects.Remove(statusEffect);
            if (effects.Count == 0)
            {
                m_statusEffects.Remove(effect);
            }
        }

        public bool RemoveStack(StatusTypes effect, GameObject target)
        {
            if (target == null || !m_statusEffects.TryGetValue(effect, out var effects))
            {
                return false;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                var statusEffect = effects[i];
                if (!statusEffect.IsTarget(target))
                {
                    continue;
                }

                statusEffect.RemoveStack();
                if (statusEffect.StackCount == 0)
                {
                    statusEffect.RemoveEffect();
                    effects.RemoveAt(i);
                }

                if (effects.Count == 0)
                {
                    m_statusEffects.Remove(effect);
                }

                return true;
            }

            return false;
        }

        public void ApplyStatusEffect(StatusTypes effect, GameObject target, int stackCount)
        {
            BaseStatusEffect statusEffect = null;
            bool existingStatusEffect = false;

            StatusPackage statusEvent = new()
            {
                target = target,
                statusEffect = effect,
                statusParams = new Dictionary<string, int> { { "stackCount", stackCount } }
            };

            switch (effect)
            {
                case StatusTypes.Armor:
                    {
                        if (m_statusEffects.TryGetValue(effect, out var armorEffects))
                        {
                            foreach (var existingEffect in armorEffects)
                            {
                                if (existingEffect.IsTarget(target))
                                {
                                    existingEffect.AddStacks(stackCount);
                                    statusEffect = existingEffect;
                                    existingStatusEffect = true;
                                    break;
                                }
                            }
                        }

                        if (!existingStatusEffect)
                        {
                            statusEffect = new ArmorStatusEffect(target, stackCount);
                        }
                    }
                    break;
                case StatusTypes.Burn:
                    {
                        if (m_statusEffects.TryGetValue(effect, out var burnEffects))
                        {
                            foreach (var existingEffect in burnEffects)
                            {
                                if (existingEffect.IsTarget(target))
                                {
                                    existingEffect.AddStacks(stackCount);
                                    statusEffect = existingEffect;
                                    existingStatusEffect = true;
                                    break;
                                }
                            }
                        }

                        if (!existingStatusEffect)
                        {
                            statusEffect = new BurnStatusEffect(target, stackCount);
                        }
                    }
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

                if (!existingStatusEffect)
                {
                    if (!m_statusEffects.ContainsKey(effect))
                    {
                        m_statusEffects.Add(effect, new List<BaseStatusEffect>());
                    }

                    m_statusEffects[effect].Add(statusEffect);
                }

                EventManager.TriggerEvent(
                    EventNames.AfterStatusApplied,
                    statusEvent);
            }

        }
    }
}
