using System.Collections.Generic;
using UnityEngine;

namespace HighStakes.AI
{
    /// <summary>A named location an NPC can visit. Connections are directional.</summary>
    public sealed class WaypointNode : MonoBehaviour
    {
        [Tooltip("Other nodes this node can lead to. Leave empty to let the manager connect nearby nodes.")]
        [SerializeField] private List<WaypointNode> connectedNodes = new List<WaypointNode>();

        [Tooltip("Optional semantic label such as Bench, Shop, Corner, or Door.")]
        [SerializeField] private string areaTag = "General";

        public IReadOnlyList<WaypointNode> ConnectedNodes => connectedNodes;
        public string AreaTag => areaTag;

        public void AddConnection(WaypointNode node)
        {
            if (node != null && node != this && !connectedNodes.Contains(node))
                connectedNodes.Add(node);
        }

        public void RemoveConnection(WaypointNode node)
        {
            connectedNodes.Remove(node);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            foreach (WaypointNode node in connectedNodes)
            {
                if (node == null) continue;
                Gizmos.color = Color.Lerp(Color.cyan, Color.white, 0.35f);
                Gizmos.DrawLine(transform.position, node.transform.position);
            }
        }
    }
}
