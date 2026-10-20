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
        /// <summary>One more resource die in every combat throw for the rest of the run (capped by <see cref="RunState.MaxResourceDice"/>).</summary>
        ResourceDie,
    }

    /// <summary>One thing the player may take from a Treasure node.</summary>
    public sealed class RewardOption
    {
        public RewardKind Kind { get; }
        public DieDefinition Die { get; }
        public string Title { get; }
        public string Description { get; }

        private RewardOption(RewardKind kind, DieDefinition die, string title, string description)
        {
            Kind = kind;
            Die = die;
            Title = title;
            Description = description;
        }

        public static RewardOption ForTraversalDie(DieDefinition die) =>
            new(RewardKind.TraversalDie, die, $"Traversal die: {die.displayName}",
                $"Faces {string.Join(" ", die.Faces.Select(f => f.Pips))}. Joins your traversal pool.");

        public static RewardOption ForResourceDie(int currentCount) =>
            new(RewardKind.ResourceDie, null, $"Resource die #{currentCount + 1}",
                $"Throw {currentCount + 1} resource dice in every combat for the rest of the run (max {RunState.MaxResourceDice}).");
    }

    /// <summary>
    /// What a Treasure node offers: one traversal die (drawn with the run's seeded RNG) and, while the
    /// party is under the cap, one more resource die. Pure C#; <see cref="RunController"/> generates
    /// it on entering the node and applies the chosen option.
    /// </summary>
    public sealed class RewardOffer
    {
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
            if (dice != null && dice.Count > 0) options.Add(RewardOption.ForTraversalDie(rng.Pick(dice)));
            if (run.CanAddResourceDie) options.Add(RewardOption.ForResourceDie(run.ResourceDiceCount));

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

                case RewardKind.ResourceDie:
                    return run.AddResourceDie()
                        ? $"Treasure: a {Ordinal(run.ResourceDiceCount)} resource die joins every combat throw."
                        : "Treasure: the party already carries the maximum resource dice.";

                default:
                    return "Treasure: nothing happened.";
            }
        }

        private static string Ordinal(int n) => n switch
        {
            1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth",
            6 => "sixth", 7 => "seventh", 8 => "eighth", _ => $"{n}th",
        };
    }
}
