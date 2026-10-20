using System.Collections.Generic;
using System.Linq;
using DungeonDice.Combat;
using DungeonDice.Dice;
using DungeonDice.Run;
using UnityEngine;

namespace DungeonDice.Tests
{
    /// <summary>Shared fake dice for EditMode tests. Callers add the results to their own cleanup list.</summary>
    public static class TestDice
    {
        /// <summary>The party's resource die as authored: faces 1–2 Red, 3–4 Blue, 5–6 Yellow.</summary>
        public static DieDefinition Resource(List<ScriptableObject> cleanup)
        {
            var die = ScriptableObject.CreateInstance<DieDefinition>();
            die.displayName = "Resource";
            die.SetFaces(new[]
            {
                DieFace.Mana(DieColor.Red), DieFace.Mana(DieColor.Red),
                DieFace.Mana(DieColor.Blue), DieFace.Mana(DieColor.Blue),
                DieFace.Mana(DieColor.Yellow), DieFace.Mana(DieColor.Yellow),
            });
            cleanup.Add(die);
            return die;
        }

        public static DieDefinition Numbers(List<ScriptableObject> cleanup, string name, params int[] pips)
        {
            var die = ScriptableObject.CreateInstance<DieDefinition>();
            die.displayName = name;
            die.SetFaces(pips.Select(DieFace.Number).ToArray());
            cleanup.Add(die);
            return die;
        }
    }

    /// <summary>Shared fake heroes mirroring the authored assets (numbers are the same placeholders).</summary>
    public static class TestHeroes
    {
        public static AbilityDefinition Ability(string name, DieColor color, int cost, AbilityKind kind, int value = 0, StatusKind status = StatusKind.Burn) =>
            new() { name = name, costColor = color, cost = cost, kind = kind, value = value, status = status };

        public static HeroDefinition Hero(List<ScriptableObject> cleanup, string name, DieColor color, int hp, bool playable = true, params AbilityDefinition[] abilities)
        {
            var hero = ScriptableObject.CreateInstance<HeroDefinition>();
            hero.displayName = name;
            hero.color = color;
            hero.maxHealth = hp;
            hero.playableInPrototype = playable;
            hero.abilities = abilities.ToList();
            cleanup.Add(hero);
            return hero;
        }

        /// <summary>A playable hero with one cheap Strike, for tests that only need a valid party.</summary>
        public static HeroDefinition Simple(List<ScriptableObject> cleanup, string name, int hp = 20, bool playable = true) =>
            Hero(cleanup, name, DieColor.Red, hp, playable, playable ? new[] { Ability("Strike", DieColor.Red, 1, AbilityKind.Damage, 3) } : System.Array.Empty<AbilityDefinition>());

        public static HeroDefinition Knight(List<ScriptableObject> cleanup) => Hero(cleanup, "Knight", DieColor.Red, 30, true,
            Ability("Strike", DieColor.Red, 1, AbilityKind.Damage, 3),
            Ability("Shield", DieColor.Red, 2, AbilityKind.Shield, 4),
            Ability("Bulwark", DieColor.Red, 3, AbilityKind.ShieldAll, 6));

        public static HeroDefinition Arcanist(List<ScriptableObject> cleanup) => Hero(cleanup, "Arcanist", DieColor.Blue, 20, true,
            Ability("Burn", DieColor.Blue, 1, AbilityKind.Status, 2, StatusKind.Burn),
            Ability("Poison", DieColor.Blue, 2, AbilityKind.Status, 3, StatusKind.Poison),
            Ability("Detonate", DieColor.Blue, 3, AbilityKind.Detonate));

        public static HeroDefinition Trickster(List<ScriptableObject> cleanup) => Hero(cleanup, "Trickster", DieColor.Yellow, 24, true,
            Ability("Jab", DieColor.Yellow, 1, AbilityKind.Damage, 2),
            Ability("Reroll", DieColor.Yellow, 1, AbilityKind.RerollDie),
            Ability("Recolour", DieColor.Yellow, 2, AbilityKind.Recolor));
    }
}
