using System.Collections.Generic;
using UnityEngine;

namespace DungeonDice.Map
{
    /// <summary>
    /// One act of the run: a hand-authored node graph, start to Boss.
    /// Lives in its own file because Unity binds a ScriptableObject asset to its script by file
    /// name; declared elsewhere, the asset saves but loads as null.
    /// </summary>
    [CreateAssetMenu(fileName = "Act", menuName = "DungeonDice/Act Definition")]
    public class ActDefinition : ScriptableObject
    {
        public string displayName;
        public int startNode = 0;
        public List<MapNode> nodes = new();

        public MapNode Node(int index) => index >= 0 && index < nodes.Count ? nodes[index] : null;

        private void OnValidate()
        {
            foreach (var error in ForkValidator.ValidateAct(this))
                Debug.LogWarning($"{name}: {error}", this);
        }
    }
}
