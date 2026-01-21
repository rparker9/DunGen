using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Handles camera/viewport state and transformations for the graph editor.
    /// Manages panning, zooming, and coordinate conversions.
    /// </summary>
    public sealed class CameraController
    {
        // Camera state
        private Vector2 _center = Vector2.zero;
        private float _zoom = 1.0f;

        // Zoom limits
        private const float MinZoom = 0.75f;
        private const float MaxZoom = 4.0f;

        // Properties
        public Vector2 Center => _center;
        public float Zoom => _zoom;

        // =========================================================
        // CAMERA CONTROL
        // =========================================================

        public void Pan(Vector2 delta)
        {
            Vector2 adjustedDelta = delta;
            adjustedDelta.y = -adjustedDelta.y; // Invert Y for world coordinates
            _center -= adjustedDelta / _zoom;
        }

        public void ZoomToward(Vector2 screenPos, Rect canvasRect, float zoomDelta)
        {
            float oldZoom = _zoom;
            _zoom = Mathf.Clamp(_zoom * (1f + zoomDelta), MinZoom, MaxZoom);

            // Zoom toward screen position
            Vector2 worldPosBeforeZoom = CoordinateConverter.ScreenToWorld(screenPos, canvasRect, _center, oldZoom);
            Vector2 worldPosAfterZoom = CoordinateConverter.ScreenToWorld(screenPos, canvasRect, _center, _zoom);
            _center += worldPosBeforeZoom - worldPosAfterZoom;
        }

        public void Reset()
        {
            _center = Vector2.zero;
            _zoom = 1.0f;
        }

        public void CenterOn(Vector2 worldPosition)
        {
            _center = worldPosition;
        }

        public void FitToBounds(Rect worldBounds, Vector2 canvasSize, float padding = 0f)
        {
            if (worldBounds.width <= 0.0001f || worldBounds.height <= 0.0001f)
                return;

            if (canvasSize.x <= 1f || canvasSize.y <= 1f)
                return;

            float w = worldBounds.width + padding * 2f;
            float h = worldBounds.height + padding * 2f;

            float zoomX = canvasSize.x / Mathf.Max(0.0001f, w);
            float zoomY = canvasSize.y / Mathf.Max(0.0001f, h);

            _zoom = Mathf.Clamp(Mathf.Min(zoomX, zoomY), MinZoom, MaxZoom);
            _center = worldBounds.center;
        }

        // =========================================================
        // COORDINATE CONVERSION
        // =========================================================

        public Vector2 WorldToScreen(Vector2 worldPos, Rect canvasRect)
        {
            return CoordinateConverter.WorldToScreen(worldPos, canvasRect, _center, _zoom);
        }

        public Vector2 ScreenToWorld(Vector2 screenPos, Rect canvasRect)
        {
            return CoordinateConverter.ScreenToWorld(screenPos, canvasRect, _center, _zoom);
        }

        // =========================================================
        // BOUNDS CALCULATION
        // =========================================================

        public static Rect CalculateWorldBounds(Dictionary<CycleNode, Vector2> positions, float nodeRadius)
        {
            if (positions == null || positions.Count == 0)
                return new Rect(0, 0, 0, 0);

            return CoordinateConverter.CalculateBounds(positions.Values, nodeRadius);
        }

        public static Vector2 CalculateCentroid(Dictionary<CycleNode, Vector2> positions)
        {
            if (positions == null || positions.Count == 0)
                return Vector2.zero;

            return CoordinateConverter.CalculateCentroid(positions.Values);
        }
    }
}