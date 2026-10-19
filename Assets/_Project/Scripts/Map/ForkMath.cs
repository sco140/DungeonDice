using System.Collections.Generic;
using System.Linq;
using DungeonDice.Dice;

namespace DungeonDice.Map
{
    /// <summary>
    /// Nominal (fair-die) probabilities for a fork roll, by convolving the pip multisets of the
    /// committed dice. Physics decides the real result, so these are what the UI shows on hover,
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

        /// <summary>Probability of each path of <paramref name="fork"/>, in path order, for the given dice.</summary>
        public static double[] PathProbabilities(Fork fork, IReadOnlyList<DieDefinition> dice)
        {
            var distribution = Distribution(dice);
            var result = new double[fork.paths.Count];
            for (int i = 0; i < fork.paths.Count; i++)
            {
                var bracket = fork.paths[i];
                result[i] = distribution.Where(kv => bracket.Contains(kv.Key)).Sum(kv => kv.Value);
            }
            return result;
        }

        public static double Mean(DieDefinition die) =>
            die == null ? 0 : Enumerable.Range(1, DieDefinition.FaceCount).Average(die.Pips);
    }

    /// <summary>Picks the path for a rolled total.</summary>
    public static class ForkResolver
    {
        /// <summary>The bracket containing <paramref name="total"/>, or null if the fork is malformed.</summary>
        public static PathBracket Resolve(Fork fork, int total)
        {
            if (fork == null || !fork.HasPaths) return null;
            foreach (var path in fork.paths)
            {
                if (path.Contains(total)) return path;
            }
            // A validated fork never gets here; clamp so a stray total still lands somewhere.
            return total < fork.MinTotal
                ? fork.paths.OrderBy(p => p.minTotal).First()
                : fork.paths.OrderByDescending(p => p.maxTotal).First();
        }
    }

    /// <summary>
    /// Design rule from the proposal: brackets cover every possible total with no gaps or overlaps,
    /// and every target exists. Run from <see cref="ActDefinition.OnValidate"/> and the authoring tool.
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

            var sorted = fork.paths.OrderBy(p => p.minTotal).ToList();
            if (sorted[0].minTotal > fork.MinTotal)
                errors.Add($"{context}: gap below {sorted[0].minTotal} (lowest possible total is {fork.MinTotal}).");
            for (int i = 0; i < sorted.Count; i++)
            {
                var p = sorted[i];
                if (p.maxTotal < p.minTotal) errors.Add($"{context}: bracket {p.minTotal}–{p.maxTotal} is inverted.");
                if (p.targetNode < 0 || p.targetNode >= nodeCount) errors.Add($"{context}: bracket {p.minTotal}–{p.maxTotal} targets missing node {p.targetNode}.");
                if (i == 0) continue;
                var prev = sorted[i - 1];
                if (p.minTotal <= prev.maxTotal) errors.Add($"{context}: brackets {prev.minTotal}–{prev.maxTotal} and {p.minTotal}–{p.maxTotal} overlap.");
                else if (p.minTotal > prev.maxTotal + 1) errors.Add($"{context}: gap between {prev.maxTotal} and {p.minTotal}.");
            }
            if (sorted[^1].maxTotal < fork.MaxTotal)
                errors.Add($"{context}: gap above {sorted[^1].maxTotal} (highest possible total is {fork.MaxTotal}).");
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
