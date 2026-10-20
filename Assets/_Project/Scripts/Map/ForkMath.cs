using System.Collections.Generic;
using System.Linq;
using DungeonDice.Dice;

namespace DungeonDice.Map
{
    /// <summary>
    /// Nominal (fair-die) probabilities for a fork roll, by convolving the pip multisets of the
    /// committed dice. Physics decides the real result, so these are what the UI shows on the edges,
    /// not a guarantee.
    /// </summary>
    public static class ForkOdds
    {
        /// <summary>P(total) for the given dice. Each face of each die is equally likely.</summary>
        public static Dictionary<int, double> Distribution(IReadOnlyList<DieDefinition> dice)
        {
            var current = new Dictionary<int, double> { [0] = 1.0 };
            if (dice == null) return current;

            foreach (var die in dice)
            {
                if (die == null) continue;
                var next = new Dictionary<int, double>();
                double perFace = 1.0 / DieDefinition.FaceCount;
                foreach (var (total, p) in current)
                {
                    for (int face = 1; face <= DieDefinition.FaceCount; face++)
                    {
                        int sum = total + die.Pips(face);
                        next[sum] = next.GetValueOrDefault(sum) + p * perFace;
                    }
                }
                current = next;
            }
            return current;
        }

        /// <summary>
        /// Probability that each path of <paramref name="fork"/> is unlocked (total ≥ threshold), in path
        /// order, for the given dice. Paths are not exclusive, so these do not sum to 1.
        /// </summary>
        public static double[] PathProbabilities(Fork fork, IReadOnlyList<DieDefinition> dice)
        {
            var distribution = Distribution(dice);
            var result = new double[fork.paths.Count];
            for (int i = 0; i < fork.paths.Count; i++)
            {
                var path = fork.paths[i];
                result[i] = distribution.Where(kv => path.IsUnlockedBy(kv.Key)).Sum(kv => kv.Value);
            }
            return result;
        }

        public static double Mean(DieDefinition die) =>
            die == null ? 0 : Enumerable.Range(1, DieDefinition.FaceCount).Average(die.Pips);
    }

    /// <summary>Which paths a rolled total opens.</summary>
    public static class ForkResolver
    {
        /// <summary>Every path unlocked by <paramref name="total"/>, in path order. Empty only for a malformed fork.</summary>
        public static List<ForkPath> Unlocked(Fork fork, int total)
        {
            if (fork == null || !fork.HasPaths) return new List<ForkPath>();
            var unlocked = fork.paths.Where(p => p.IsUnlockedBy(total)).ToList();
            // A validated fork always unlocks something; clamp so a stray total still opens the easiest path.
            if (unlocked.Count == 0) unlocked.Add(fork.paths.OrderBy(p => p.threshold).First());
            return unlocked;
        }

        /// <summary>Target node indices unlocked by <paramref name="total"/>.</summary>
        public static List<int> UnlockedTargets(Fork fork, int total) => Unlocked(fork, total).Select(p => p.targetNode).ToList();
    }

    /// <summary>
    /// Design rules: a fork has at least two paths, one of them unlocks on the lowest possible total,
    /// every threshold is reachable, and every target exists. Run from <see cref="ActDefinition.OnValidate"/>
    /// and the authoring tool.
    /// </summary>
    public static class ForkValidator
    {
        public static List<string> ValidateFork(Fork fork, string context = "fork", int nodeCount = int.MaxValue)
        {
            var errors = new List<string>();
            if (fork == null) return errors;
            if (fork.minDice < 1 || fork.maxDice < fork.minDice)
                errors.Add($"{context}: dice range {fork.minDice}–{fork.maxDice} is invalid.");
            if (!fork.HasPaths)
            {
                errors.Add($"{context}: has no paths.");
                return errors;
            }
            if (fork.paths.Count < 2) errors.Add($"{context}: a fork needs at least two paths.");
            if (fork.paths.Min(p => p.threshold) > fork.MinTotal)
                errors.Add($"{context}: no path unlocks on the lowest total {fork.MinTotal} (lowest threshold is {fork.paths.Min(p => p.threshold)}).");
            foreach (var p in fork.paths)
            {
                if (p.threshold > fork.MaxTotal) errors.Add($"{context}: threshold {p.threshold}+ is unreachable (highest possible total is {fork.MaxTotal}).");
                if (p.targetNode < 0 || p.targetNode >= nodeCount) errors.Add($"{context}: path {p.threshold}+ targets missing node {p.targetNode}.");
            }
            return errors;
        }

        public static List<string> ValidateAct(ActDefinition act)
        {
            var errors = new List<string>();
            if (act == null || act.nodes == null || act.nodes.Count == 0)
            {
                errors.Add("act has no nodes.");
                return errors;
            }
            if (act.startNode < 0 || act.startNode >= act.nodes.Count) errors.Add($"start node {act.startNode} does not exist.");

            int bosses = 0;
            for (int i = 0; i < act.nodes.Count; i++)
            {
                var node = act.nodes[i];
                string context = $"node {i} ({node.name})";
                if (node.type == NodeType.Boss) bosses++;
                if (node.HasFork && node.nextNode >= 0) errors.Add($"{context}: has both a fork and a single exit.");
                if (node.HasFork) errors.AddRange(ValidateFork(node.fork, context, act.nodes.Count));
                else if (node.nextNode >= act.nodes.Count) errors.Add($"{context}: next node {node.nextNode} does not exist.");
                else if (node.nextNode < 0 && node.type != NodeType.Boss) errors.Add($"{context}: dead end that is not the Boss.");
            }
            if (bosses != 1) errors.Add($"act has {bosses} Boss nodes; expected exactly one.");
            return errors;
        }
    }
}
