namespace DunGen.Editor
{
    /// <summary>
    /// Editor mode for the dungeon graph canvas.
    /// </summary>
    public enum EditorMode
    {
        /// <summary>
        /// Author mode: Create and edit cycle templates manually.
        /// - Place nodes by clicking
        /// - Drag nodes to reposition
        /// - Create edges by connecting nodes
        /// - Edit node/edge properties
        /// - Save as reusable templates
        /// </summary>
        Author,

        /// <summary>
        /// Preview mode: Generate and view expanded cycles.
        /// - Auto-layout with algorithm
        /// - Generate with random seeds
        /// - Read-only inspection
        /// - Expand rewrite sites
        /// </summary>
        Preview
    }
}