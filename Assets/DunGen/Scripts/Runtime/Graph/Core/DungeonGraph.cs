#nullable enable
using System;
using System.Collections.Generic;

namespace DunGen.Graph.Core
{
    /// <summary>
    /// A flat directed graph used for flowchart-level dungeon generation.
    ///
    /// Important: this is NOT a spatial layout yet.
    /// This graph answers: "What rooms exist and how are they connected?"
    /// Later you can translate it to physical rooms/tiles.
    /// </summary>
    public sealed class DungeonGraph
    {
        // Nodes and edges are stored by ID, which makes lookups fast and avoids reference tangles.
        private readonly Dictionary<NodeId, RoomNode> _nodes = new();
        private readonly Dictionary<EdgeId, RoomEdge> _edges = new();

        // Convenience cache: for each node, keep a list of outgoing edges.
        // This makes traversal algorithms much easier (BFS/DFS/pathfinding).
        private readonly Dictionary<NodeId, List<EdgeId>> _outEdges = new();

        /// <summary>
        /// All nodes in the graph, indexed by ID.
        /// </summary>
        public IReadOnlyDictionary<NodeId, RoomNode> Nodes => _nodes;

        /// <summary>
        /// All edges in the graph, indexed by ID.
        /// </summary>
        public IReadOnlyDictionary<EdgeId, RoomEdge> Edges => _edges;

        /// <summary>
        /// Get a node by ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public RoomNode GetNode(NodeId id) => _nodes[id];

        /// <summary>
        /// Get an edge by ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public RoomEdge GetEdge(EdgeId id) => _edges[id];

        /// <summary>
        /// Returns all outgoing edges from a node (edges where edge.From == from).
        /// If the node has no outgoing edges, returns an empty sequence.
        /// </summary>
        public IEnumerable<EdgeId> OutEdges(NodeId from)
        {
            return _outEdges.TryGetValue(from, out var list) ? list : Array.Empty<EdgeId>();
        }

        /// <summary>
        /// Add a node to the graph.
        /// (No auto-ID allocation here; ID allocation is handled elsewhere.)
        /// </summary>
        public void AddNode(RoomNode node)
        {
            // Validation check.
            if (_nodes.ContainsKey(node.Id))
                throw new ArgumentException($"A node with ID {node.Id} already exists in the graph.");

            _nodes.Add(node.Id, node);
        }

        /// <summary>
        /// Add an edge to the graph and update the outgoing-edge cache.
        /// </summary>
        public void AddEdge(RoomEdge edge)
        {
            // Validation checks.
            if (_edges.ContainsKey(edge.Id))
                throw new ArgumentException($"An edge with ID {edge.Id} already exists in the graph.");

            if (!_nodes.ContainsKey(edge.From))
                throw new ArgumentException($"The 'from' node with ID {edge.From} does not exist in the graph.");

            if (!_nodes.ContainsKey(edge.To))
                throw new ArgumentException($"The 'to' node with ID {edge.To} does not exist in the graph.");

            if (edge.From == edge.To)
                throw new ArgumentException("Self-loops are not allowed in this graph implementation.");

            // Add to main edge list.
            _edges.Add(edge.Id, edge);

            // If there's no existing list for the 'from' node, create one.
            if (!_outEdges.TryGetValue(edge.From, out var list))
            {
                // Create new list and add to cache.
                list = new List<EdgeId>();
                _outEdges.Add(edge.From, list);
            }

            // Add edge to 'from' node's outgoing edge list.
            list.Add(edge.Id);
        }

        /// <summary>
        /// Remove an edge by ID.
        /// Safe to call even if the edge does not exist.
        /// </summary>
        public void RemoveEdge(EdgeId edgeId)
        {
            // If edge doesn't exist, nothing to do.
            if (!_edges.TryGetValue(edgeId, out var edge))
                return;

            _edges.Remove(edgeId);

            // Keep adjacency cache in sync.
            if (_outEdges.TryGetValue(edge.From, out var list))
            {
                list.Remove(edgeId);
            }
        }
    }
}
