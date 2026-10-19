using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDice.Combat
{
    public enum IntentKind
    {
        /// <summary>Hit one hero (the taunter if any, else a seeded random living hero).</summary>
        Attack,
        /// <summary>Hit every living hero.</summary>
        AttackAll,
        /// <summary>Gain shield until this enemy's next action.</summary>
        Block,
    }

    /// <summary>One thing an enemy can telegraph and then do.</summary>
    [Serializable]
    public class EnemyIntent
    {
        public IntentKind kind;
        public int value;

        public string Describe() => kind switch
        {
            IntentKind.Attack => $"Attack {value}",
            IntentKind.AttackAll => $"Attack all {value}",
            IntentKind.Block => $"Block {value}",
            _ => kind.ToString(),
        };
    }

    /// <summary>
    /// An enemy as data: health and the intents it picks from each turn (seeded). Regulars, the
    /// elite and the boss are all this type; which ones a node spawns is decided by RunController.
    /// Numbers are tuning placeholders.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "DungeonDice/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        public string displayName;
        [Min(1)] public int maxHealth = 10;
        [TextArea] public string description;
        public List<EnemyIntent> intents = new();
    }
}
