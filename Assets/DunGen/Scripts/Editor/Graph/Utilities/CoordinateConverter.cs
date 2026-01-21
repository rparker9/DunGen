using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Utility class for converting between world space and screen space coordinates.
    /// Handles orthographic camera-style transformations with zoom and pan.
    /// </summary>
    public static class CoordinateConverter
    {
        /// <summary>
        /// Convert world position to screen position.
        /// </summary>
        /// <param name="worldPos">Position in world space</param>
        /// <param name="canvasRect">Canvas rectangle in screen space</param>
        /// <param name="cameraCenter">Camera center in world space</param>
        /// <param name="zoom">Zoom level (pixels per world unit)</param>
        /// <returns>Position in screen space</returns>
        public static Vector2 WorldToScreen(
            Vector2 worldPos,
            Rect canvasRect,
            Vector2 cameraCenter,
            float zoom)
        {
            Vector2 centered = (worldPos - cameraCenter) * zoom;
            return new Vector2(
                canvasRect.x + canvasRect.width * 0.5f + centered.x,
                canvasRect.y + canvasRect.height * 0.5f - centered.y
            );
        }

        /// <summary>
        /// Convert screen position to world position.
        /// </summary>
        /// <param name="screenPos">Position in screen space</param>
        /// <param name="canvasRect">Canvas rectangle in screen space</param>
        /// <param name="cameraCenter">Camera center in world space</param>
        /// <param name="zoom">Zoom level (pixels per world unit)</param>
        /// <returns>Position in world space</returns>
        public static Vector2 ScreenToWorld(
            Vector2 screenPos,
            Rect canvasRect,
            Vector2 cameraCenter,
            float zoom)
        {
            Vector2 centered = new Vector2(
                screenPos.x - canvasRect.x - canvasRect.width * 0.5f,
                -(screenPos.y - canvasRect.y - canvasRect.height * 0.5f)
            );
            return cameraCenter + centered / zoom;
        }

        /// <summary>
        /// Calculate the world-space bounding box that contains all given positions.
        /// </summary>
        /// <param name="worldPositions">Collection of world positions</param>
        /// <param name="padding">Optional padding to add around bounds</param>
        /// <returns>Bounding rectangle in world space</returns>
        public static Rect CalculateBounds(System.Collections.Generic.IEnumerable<Vector2> worldPositions, float padding = 0f)
        {
            bool first = true;
            float minX = 0, minY = 0, maxX = 0, maxY = 0;

            foreach (var pos in worldPositions)
            {
                float x0 = pos.x - padding;
                float x1 = pos.x + padding;
                float y0 = pos.y - padding;
                float y1 = pos.y + padding;

                if (first)
                {
                    minX = x0; minY = y0; maxX = x1; maxY = y1;
                    first = false;
                }
                else
                {
                    if (x0 < minX) minX = x0;
                    if (y0 < minY) minY = y0;
                    if (x1 > maxX) maxX = x1;
                    if (y1 > maxY) maxY = y1;
                }
            }

            if (first)
                return new Rect(0, 0, 0, 0);

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Calculate the centroid (center point) of a collection of world positions.
        /// </summary>
        /// <param name="worldPositions">Collection of world positions</param>
        /// <returns>Centroid in world space</returns>
        public static Vector2 CalculateCentroid(System.Collections.Generic.IEnumerable<Vector2> worldPositions)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;

            foreach (var pos in worldPositions)
            {
                sum += pos;
                count++;
            }

            return count > 0 ? sum / count : Vector2.zero;
        }

        /// <summary>
        /// Calculate the zoom level needed to fit a world-space rectangle into a screen-space canvas.
        /// </summary>
        /// <param name="worldBounds">Bounds in world space</param>
        /// <param name="canvasSize">Canvas size in screen space</param>
        /// <param name="minZoom">Minimum allowed zoom level</param>
        /// <param name="maxZoom">Maximum allowed zoom level</param>
        /// <returns>Optimal zoom level</returns>
        public static float CalculateFitZoom(
            Rect worldBounds,
            Vector2 canvasSize,
            float minZoom = 0.1f,
            float maxZoom = 10f)
        {
            if (worldBounds.width <= 0.0001f || worldBounds.height <= 0.0001f)
                return 1f;

            if (canvasSize.x <= 1f || canvasSize.y <= 1f)
                return 1f;

            float zoomX = canvasSize.x / Mathf.Max(0.0001f, worldBounds.width);
            float zoomY = canvasSize.y / Mathf.Max(0.0001f, worldBounds.height);

            return Mathf.Clamp(Mathf.Min(zoomX, zoomY), minZoom, maxZoom);
        }

        /// <summary>
        /// Check if a screen position is within a world-space circle.
        /// Useful for hit testing circular nodes.
        /// </summary>
        /// <param name="screenPos">Position in screen space</param>
        /// <param name="worldCenter">Circle center in world space</param>
        /// <param name="worldRadius">Circle radius in world space</param>
        /// <param name="canvasRect">Canvas rectangle</param>
        /// <param name="cameraCenter">Camera center</param>
        /// <param name="zoom">Zoom level</param>
        /// <returns>True if screen position is within the circle</returns>
        public static bool IsPointInCircle(
            Vector2 screenPos,
            Vector2 worldCenter,
            float worldRadius,
            Rect canvasRect,
            Vector2 cameraCenter,
            float zoom)
        {
            Vector2 worldPos = ScreenToWorld(screenPos, canvasRect, cameraCenter, zoom);
            return Vector2.Distance(worldPos, worldCenter) <= worldRadius;
        }
    }
}