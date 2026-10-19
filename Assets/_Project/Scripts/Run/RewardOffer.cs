using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Core;
using DungeonDice.Dice;

namespace DungeonDice.Run
{
    public enum RewardKind
    {
        /// <summary>Add a traversal die to the party's pool.</summary>
        TraversalDie,
        /// <summary>Replace one face of a hero's die (<see cref="FaceUpgrade"/>).</summary>
        FaceUpgrade,
    }

    /// <summary>One thing the player may take from a Treasure node.</summary>
    public sealed class RewardOption
    {
        public RewardKind Kind { get; }
        public DieDefinition Die { get; }
        public int HeroSlot { get; }
        public int UpgradeIndex { get; }
        public string Title { get; }
        public string Description { get; }

        private RewardOption(RewardKind kind, DieDefinition die, int heroSlot, int upgradeIndex, string title, string description)
        {
            Kind = kind;
            Die = die;
            HeroSlot = heroSlot;
            UpgradeIndex = upgradeIndex;
            Title = title;
            Description = description;
        }

        public static RewardOption ForDie(DieDefinition die) =>
            new(RewardKind.TraversalDie, die, -1, -1, $"Traversal die: {die.displayName}",
                $"Faces {string.Join(" ", die.Faces.Select(f => f.Pips))}. Joins your traversal pool.");

        public static RewardOption ForUpgrade(HeroRunState hero, int upgradeIndex) =>
            new(RewardKind.FaceUpgrade, null, hero.Slot, upgradeIndex, $"{hero.Definition.displayName}: face upgrade",
                hero.Definition.faceUpgrades[upgradeIndex].Describe(hero.Die));
    }

    /// <summary>
    /// What a Treasure node offers: one traversal die plus face upgrades for up to two living heroes,
    /// all drawn with the run's seeded RNG. Pure C#; <see cref="RunController"/> generates it on entering
    /// the node and applies the chosen option.
    /// </summary>
    public sealed class RewardOffer
    {
        public const int UpgradeOptions = 2;

        public IReadOnlyList<RewardOption> Options { get; }

        private RewardOffer(List<RewardOption> options)
        {
            Options = options;
        }

        public static RewardOffer Generate(RunState run, IReadOnlyList<DieDefinition> rewardDice, RollService rng)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var options = new List<RewardOption>();

            var dice = rewardDice?.Where(d => d != null).ToList();
            if (dice != null && dice.Count > 0) options.Add(RewardOption.ForDie(rng.Pick(dice)));

            // Heroes with at least one upgrade not yet taken, in a seeded shuffle; one upgrade each.
            var candidates = run.LivingHeroes.Where(h => h.AvailableUpgrades().Any()).ToList();
            rng.Shuffle(candidates);
            foreach (var hero in candidates.Take(UpgradeOptions))
            {
                var available = hero.AvailableUpgrades().ToList();
                options.Add(RewardOption.ForUpgrade(hero, rng.Pick(available)));
            }

            return new RewardOffer(options);
        }

        /// <summary>Apply one option to the run. Returns a one-line summary for the map.</summary>
        public string Apply(int optionIndex, RunState run)
        {
            if (optionIndex < 0 || optionIndex >= Options.Count) throw new ArgumentOutOfRangeException(nameof(optionIndex));
            var option = Options[optionIndex];
            switch (option.Kind)
            {
                case RewardKind.TraversalDie:
                    run.AddTraversalDie(option.Die);
                    return $"Treasure: gained the {option.Die.displayName} die.";

                case RewardKind.FaceUpgrade:
                {
                    var hero = run.Party.First(h => h.Slot == option.HeroSlot);
                    string change = hero.Definition.faceUpgrades[option.UpgradeIndex].Describe(hero.Die);
                    hero.ApplyUpgrade(option.UpgradeIndex);
                    return $"Treasure: {hero.Definition.displayName}'s die upgraded ({change}).";
                }
                default:
                    return "Treasure: nothing happened.";
            }
        }
    }
}
