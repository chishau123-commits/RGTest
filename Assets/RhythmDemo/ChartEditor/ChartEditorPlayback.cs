using System;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        Material editorLivePathMaterial;
        LineRenderer[] editorLivePaths;
        Vector3[][] editorLivePathPoints;
        LineRenderer editorPlayheadLine;
        LineRenderer editorPlayheadVerticalLine;
        readonly Vector3[] editorPlayheadPoints = new Vector3[2];
        readonly Vector3[] editorPlayheadVerticalPoints = new Vector3[2];
        static readonly Color EditorLivePathColor = new Color(.78f, .40f, 1f);

        // The 3D playhead is the center of the current judgement cross-section,
        // shared with the near ends of the live paths and the authored note hit poses.
        Vector3 EditorPlayheadPosition(double time)
            => spatial.RouteAt(spatial.DistanceAtTime(time) + SpatialDirector.NearDepth);
        void BuildEditorPlaybackVisuals()
        {
            if (chartCameraPreview) return;
            if (editorLivePathMaterial == null) editorLivePathMaterial = MakeMaterial("Editor live paths", EditorLivePathColor, true);
            editorLivePaths = new LineRenderer[chart.paths.Length];
            editorLivePathPoints = new Vector3[chart.paths.Length][];
            for (int i = 0; i < chart.paths.Length; i++)
            {
                editorLivePathPoints[i] = new Vector3[90];
                editorLivePaths[i] = Line("Live note path " + chart.paths[i].id, editorLivePathMaterial, .16f, editorLivePathPoints[i]);
                editorLivePaths[i].sortingOrder = 10;
            }
            editorPlayheadLine = Line("Editor playhead", editorLivePathMaterial, .13f, editorPlayheadPoints);
            editorPlayheadLine.sortingOrder = 11;
            editorPlayheadVerticalLine = Line("Editor playhead vertical", editorLivePathMaterial, .13f, editorPlayheadVerticalPoints);
            editorPlayheadVerticalLine.sortingOrder = 11;
            RefreshEditorPlaybackVisuals();
        }
        void RefreshEditorPlaybackVisuals()
        {
            if (chartCameraPreview || editorLivePaths == null) return;
            for (int i = 0; i < editorLivePaths.Length; i++)
            {
                var line = editorLivePaths[i]; if (line == null) continue;
                bool visible = spatial.Visibility(chart.paths[i].id, songTime) > .01f;
                line.gameObject.SetActive(visible);
                if (!visible) continue;
                var points = editorLivePathPoints[i];
                for (int p = 0; p < points.Length; p++)
                    points[p] = spatial.Point(chart.paths[i].id,
                        Mathf.Lerp(SpatialDirector.NearDepth - 2, SpatialDirector.FarDepth, p / (points.Length - 1f)), songTime);
                line.SetPositions(points);
            }
            if (editorPlayheadLine != null)
            {
                Vector3 center = EditorPlayheadPosition(songTime);
                // Keep the cross camera-facing and constant in screen size. Two separate
                // strokes avoid the folded-polyline artifact that made the old plus jitter.
                float depth = Mathf.Max(.2f, sceneCamera.WorldToScreenPoint(center).z);
                float halfSize = 7 * uiScale * 2 * depth * Mathf.Tan(sceneCamera.fieldOfView * Mathf.Deg2Rad * .5f) /
                    Mathf.Max(1, sceneCamera.pixelHeight);
                float stroke = Mathf.Max(.004f, halfSize * .28f);
                editorPlayheadLine.widthMultiplier = stroke;
                editorPlayheadVerticalLine.widthMultiplier = stroke;
                editorPlayheadPoints[0] = center - sceneCamera.transform.right * halfSize;
                editorPlayheadPoints[1] = center + sceneCamera.transform.right * halfSize;
                editorPlayheadVerticalPoints[0] = center - sceneCamera.transform.up * halfSize;
                editorPlayheadVerticalPoints[1] = center + sceneCamera.transform.up * halfSize;
                editorPlayheadLine.SetPositions(editorPlayheadPoints);
                editorPlayheadVerticalLine.SetPositions(editorPlayheadVerticalPoints);
            }
        }
        void DrawEditorPlaybackStatus()
        {
            Color previous = GUI.color; GUI.color = EditorLivePathColor;
            GUI.Label(new Rect(PanelWidth + 14, ToolbarHeight + 70, ViewportRight - PanelWidth - 28, 24),
                "LIVE PATHS / violet   ·   Static paths + notes stay fixed", smallStyle);
            Vector3 head = EditorPlayheadPosition(songTime);
            Vector2 screen = WorldGui(head);
            if (sceneCamera.WorldToScreenPoint(head).z > 0 && new Rect(PanelWidth, ToolbarHeight, ViewportRight - PanelWidth, TimelineTop - ToolbarHeight).Contains(screen))
                GUI.Label(new Rect(screen.x + 12, screen.y + 8, 200, 22), "PLAYHEAD " + FormatTimelineTime(songTime), smallStyle);
            GUI.color = previous;
        }
    }
}
