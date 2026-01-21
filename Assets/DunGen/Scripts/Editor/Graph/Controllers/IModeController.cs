using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Base interface for mode-specific controllers.
    /// Each mode handles input and provides node positions differently.
    /// </summary>
    public interface IModeController
    {
        /// <summary>
        /// Handle input events for this mode.
        /// </summary>
        void HandleInput(Event e, Vector2 mousePos, Rect canvasRect, CameraController camera);

        /// <summary>
        /// Get node positions for rendering.
        /// Author mode: Manual positions set by user.
        /// Preview mode: Auto-computed positions.
        /// </summary>
        Dictionary<CycleNode, Vector2> GetNodePositions();

        /// <summary>
        /// Called when entering this mode.
        /// </summary>
        void OnModeEnter();

        /// <summary>
        /// Called when exiting this mode.
        /// </summary>
        void OnModeExit();

        /// <summary>
        /// Draw mode-specific overlays (e.g., connection preview line).
        /// </summary>
        void DrawOverlays(Rect canvasRect, CameraController camera);

        /// <summary>
        /// Update mode state each frame.
        /// </summary>
        void Update();
    }
}