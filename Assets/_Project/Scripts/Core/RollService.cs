using System;
using System.Collections.Generic;

namespace DungeonDice.Core
{
    /// <summary>
    /// Seeded source for every random decision that is <b>not</b> the physical dice throw:
    /// enemy intents, rewards, map layout. Reset the seed at run start so a run is reproducible
    /// apart from the dice themselves.
    /// </summary>
    public sealed class RollService
    {
        private readonly Random m_random;

        public int Seed { get; }

        /// <summary>Process-wide instance. Call <see cref="ResetShared"/> when a run starts.</summary>
        public static RollService Shared { get; private set; } = new RollService(Environment.TickCount);

        public static void ResetShared(int seed)
        {
            Shared = new RollService(seed);
        }

        public RollService(int seed)
        {
            Seed = seed;
            m_random = new Random(seed);
        }

        /// <summary>Integer in [minInclusive, maxExclusive).</summary>
        public int Next(int minInclusive, int maxExclusive) => m_random.Next(minInclusive, maxExclusive);

        /// <summary>A fair d6 face index 1–6, for logical rolls that bypass the physical throw.</summary>
        public int RollFace() => Next(1, 7);

        public double NextDouble() => m_random.NextDouble();

        public bool Chance(double probability) => NextDouble() < probability;

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
            return items[Next(0, items.Count)];
        }

        public void Shuffle<T>(IList<T> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
