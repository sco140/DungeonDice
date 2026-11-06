using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUtility
{
    public enum DiceFaceValue
    {
        RED,
        BLUE,
        YELLOW,
        BLANK,
    }

    public enum ManaColor
    {
        Generic,
        Red,
        Yellow,
        Blue,
    }

    [Serializable]
    public struct Mana
    {
        private Dictionary<ManaColor, uint> m_dicMana;

        public Mana(Dictionary<ManaColor, uint> dicMana)
        {
            m_dicMana = dicMana;
        }

        public Mana(uint generic, uint red, uint yellow, uint blue)
        {
            m_dicMana = new Dictionary<ManaColor, uint>
            {
                { ManaColor.Generic, blue },
                { ManaColor.Red, red },
                { ManaColor.Yellow, yellow },
                { ManaColor.Blue, blue },
            };
        }
    }

    [Serializable]
    public struct EffectInfo
    {
        UnitController owner;
        UnitController target;
        Dictionary<string, int> effectParams;
    }
    public enum StatusEffect
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

    [Serializable]
    public struct StatusPackage
    {
        UnitController target;
        StatusEffect statusEffect;
        Dictionary<string, int> statusParams;
    }
    public enum TriggerType
    {
        BeforeTurnStart,
        AfterTurnStart,
        BeforeTurnEnd,
        AfterTurnEnd,
        BeforeSkillActivate,
        AfterSkillActivate,
        BeforeResourceGain,
        AfterResourceGain,
        BeforeRollDice,
        AfterRollDice,
        BeforeDamageTaken,
        AfterDamageTaken,
        MAX
    }

    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new List<TKey>();
        [SerializeField] private List<TValue> values = new List<TValue>();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var kvp in this)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            for (int i = 0; i < keys.Count; i++)
            {
                this[keys[i]] = values[i];
            }
        }
    }
}
