using System.Collections.Generic;
using UnityEngine;

namespace HighStakes.AI
{
    /// <summary>Scene-level registry and optional proximity-based network builder.</summary>
    public sealed class WaypointManager : MonoBehaviour
    {
        [SerializeField] private List<WaypointNode> nodes = new List<WaypointNode>();
        [SerializeField] private bool autoDiscoverChildren = true;
        [SerializeField] private bool connectNearbyNodes;
        [SerializeField, Min(0.1f)] private float connectionRadius = 12f;
        [SerializeField] private bool bidirectionalConnections = true;

        public IReadOnlyList<WaypointNode> Nodes => nodes;

        private void Awake()
        {
            Refresh();
            if (connectNearbyNodes) BuildProximityConnections();
        }

        [ContextMenu("Refresh Nodes")]
        public void Refresh()
        {
            if (!autoDiscoverChildren) return;
            nodes.Clear();
            nodes.AddRange(GetComponentsInChildren<WaypointNode>(true));
        }

        [ContextMenu("Build Proximity Connections")]
        public void BuildProximityConnections()
        {
            Refresh();
            float radiusSquared = connectionRadius * connectionRadius;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null) continue;
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    if (nodes[j] == null) continue;
                    if ((nodes[i].transform.position - nodes[j].transform.position).sqrMagnitude > radiusSquared) continue;
                    nodes[i].AddConnection(nodes[j]);
                    if (bidirectionalConnections) nodes[j].AddConnection(nodes[i]);
                }
            }
        }

        public WaypointNode GetRandomNode(WaypointNode excluding = null)
        {
            List<WaypointNode> candidates = new List<WaypointNode>();
            foreach (WaypointNode node in nodes)
                if (node != null && node != excluding) candidates.Add(node);
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }
    }
}
