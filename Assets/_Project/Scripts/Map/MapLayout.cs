using System.Collections.Generic;
using System.Linq;

namespace DungeonDice.Map
{
    /// <summary>
    /// Places an act's nodes on a grid for the map graph: column = distance from the start node
    /// (longest path, so a node after a fork sits right of every node that can lead to it), row =
    /// order within the column. Pure, so the layout is unit-tested without UI.
    /// </summary>
    public static class MapLayout
    {
        public readonly struct Cell
        {
            public int Node { get; }
            public int Column { get; }
            public int Row { get; }
            public Cell(int node, int column, int row) { Node = node; Column = column; Row = row; }
        }

        public sealed class Result
        {
            public IReadOnlyList<Cell> Cells { get; }
            public int Columns { get; }
            public int Rows { get; }
            public Result(IReadOnlyList<Cell> cells, int columns, int rows) { Cells = cells; Columns = columns; Rows = rows; }
            public Cell For(int node) => Cells.First(c => c.Node == node);
        }

        /// <summary>Every (from, to) edge of the act, with the fork bracket when the edge comes from a fork.</summary>
        public static IEnumerable<(int from, int to, PathBracket bracket)> Edges(ActDefinition act)
        {
            for (int i = 0; i < act.nodes.Count; i++)
            {
                var node = act.nodes[i];
                if (node.HasFork)
                {
                    foreach (var path in node.fork.paths) yield return (i, path.targetNode, path);
                }
                else if (node.nextNode >= 0)
                {
                    yield return (i, node.nextNode, null);
                }
            }
        }

        public static Result Compute(ActDefinition act)
        {
            int count = act.nodes.Count;
            var depth = new int[count];
            for (int i = 0; i < count; i++) depth[i] = -1;
            depth[act.startNode] = 0;

            // Longest-path depth over a DAG via relaxation (acts are small; no cycle handling needed).
            var edges = Edges(act).ToList();
            bool changed = true;
            for (int pass = 0; pass < count + 1 && changed; pass++)
            {
                changed = false;
                foreach (var (from, to, _) in edges)
                {
                    if (depth[from] < 0) continue;
                    if (depth[to] < depth[from] + 1)
                    {
                        depth[to] = depth[from] + 1;
                        changed = true;
                    }
                }
            }

            var cells = new List<Cell>();
            int columns = depth.Max() + 1;
            int rows = 0;
            for (int column = 0; column < columns; column++)
            {
                var inColumn = Enumerable.Range(0, count).Where(n => depth[n] == column).ToList();
                rows = System.Math.Max(rows, inColumn.Count);
                for (int row = 0; row < inColumn.Count; row++) cells.Add(new Cell(inColumn[row], column, row));
            }
            // Unreachable nodes (never in a valid act) go in a trailing column so nothing is lost.
            var unreachable = Enumerable.Range(0, count).Where(n => depth[n] < 0).ToList();
            if (unreachable.Count > 0)
            {
                for (int row = 0; row < unreachable.Count; row++) cells.Add(new Cell(unreachable[row], columns, row));
                columns++;
                rows = System.Math.Max(rows, unreachable.Count);
            }
            return new Result(cells, columns, rows);
        }
    }
}
