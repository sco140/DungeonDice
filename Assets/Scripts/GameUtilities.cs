using System;
using System.Collections;
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
}