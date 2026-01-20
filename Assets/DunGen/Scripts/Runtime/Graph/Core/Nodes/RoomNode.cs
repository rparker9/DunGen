#nullable enable
using System.Collections.Generic;

namespace DunGen.Graph.Core 
{
    /// <summary>
    /// Structural role of a node in the dungeon (not cycle-specific).
    /// </summary>
    public enum NodeKind
    {
        Normal,
        Entrance,  // Dungeon entrance (replaces "Start" at dungeon level)
        Exit       // Dungeon exit (replaces "Goal" at dungeon level)
    }

    /// <summary>
    /// A node is a "room" in the *flowchart graph*, not necessarily a spatial room yet.
    ///
    /// Later, when you do spatial layout, one RoomNode could become:
    /// - a single room on a grid
    /// - a bigger room prefab
    /// - or even a small cluster of rooms
    /// </summary>
    public sealed class RoomNode
    {
        // Unique identifier for this node inside the graph.
        public NodeId Id { get; }

        // Structural role (Start / Goal / Normal).
        public NodeKind Kind { get; set; }

        // A label purely for debugging / editor display.
        public string? DebugLabel { get; set; }

        // Semantic tags (key, reward, danger, etc.)
        public List<NodeTag> Tags { get; } = new();

        public RoomNode(NodeId id, NodeKind kind = NodeKind.Normal, string? debugLabel = null)
        {
            Id = id;
            Kind = kind;
            DebugLabel = debugLabel;
        }
    }
}
#nullable disable
