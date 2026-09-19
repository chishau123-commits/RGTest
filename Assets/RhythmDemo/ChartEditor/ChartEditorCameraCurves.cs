using System;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        int openCameraCurveSegment = -1;
        static readonly string[] CameraCurveIds =
        {
            SpatialDirector.CameraEaseLinear,
            SpatialDirector.CameraEaseIn,
            SpatialDirector.CameraEaseOut,
            SpatialDirector.CameraEaseInOut,
            SpatialDirector.CameraEaseSmoother
        };
        static readonly string[] CameraCurveNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out", "Smoother" };
        static readonly string[] CameraCurveShortNames = { "LIN", "IN", "OUT", "S", "SM" };

        bool CameraCurveButtonRect(int segment, out Rect rect)
        {
            rect = default(Rect);
            if (mode != ChartEditMode.Camera || chartCameraPreview || chart == null || spatial == null ||
                segment < 0 || segment + 1 >= chart.cameraKeys.Length) return false;
            var a = chart.cameraKeys[segment]; var b = chart.cameraKeys[segment + 1];
            float middleBeat = (a.beat + b.beat) * .5f;
            spatial.EvaluateCamera(evaluatorCamera, tempo.SecondsAtBeat(middleBeat));
            Vector3 world = evaluatorCamera.transform.position;
            if (sceneCamera.WorldToScreenPoint(world).z <= .1f) return false;
            Vector2 point = WorldGui(world);
            var viewport = new Rect(PanelWidth, ToolbarHeight, ViewportRight - PanelWidth, TimelineTop - ToolbarHeight);
            if (!viewport.Contains(point)) return false;
            rect = new Rect(point.x - 19, point.y - 12, 38, 24);
            return true;
        }

        Rect CameraCurvePopupRect(Rect button)
        {
            const float width = 116, row = 27;
            float x = Mathf.Clamp(button.center.x - width * .5f, PanelWidth + 6, ViewportRight - width - 6);
            float height = CameraCurveNames.Length * row + 8;
            float y = button.yMax + 5;
            if (y + height > TimelineTop - 6) y = button.yMin - height - 5;
            return new Rect(x, y, width, height);
        }

        bool CameraCurvePointerOwns(Event e)
        {
            if (e == null || (e.type != EventType.MouseDown && e.type != EventType.MouseUp && e.type != EventType.MouseDrag)) return false;
            if (mode != ChartEditMode.Camera || chartCameraPreview || WorkspaceInputBlocked) { openCameraCurveSegment = -1; return false; }
            Vector2 mouse = e.mousePosition / uiScale;
            for (int i = 0; i + 1 < chart.cameraKeys.Length; i++)
                if (CameraCurveButtonRect(i, out var button) && button.Contains(mouse)) return true;
            if (openCameraCurveSegment >= 0 && CameraCurveButtonRect(openCameraCurveSegment, out var anchor) &&
                CameraCurvePopupRect(anchor).Contains(mouse)) return true;
            if (e.type == EventType.MouseDown) openCameraCurveSegment = -1;
            return false;
        }

        void DrawCameraCurveControls()
        {
            if (mode != ChartEditMode.Camera || chartCameraPreview || WorkspaceInputBlocked || chart.cameraKeys.Length < 2) return;
            for (int i = 0; i < chart.cameraKeys.Length; i++)
            {
                spatial.EvaluateCamera(evaluatorCamera, tempo.SecondsAtBeat(chart.cameraKeys[i].beat));
                Vector3 world = evaluatorCamera.transform.position;
                if (sceneCamera.WorldToScreenPoint(world).z <= .1f) continue;
                Vector2 point = WorldGui(world);
                if (point.x >= PanelWidth && point.x <= ViewportRight && point.y >= ToolbarHeight && point.y <= TimelineTop)
                    GUI.Label(new Rect(point.x + 9, point.y - 20, 45, 20), "K" + i, smallStyle);
            }
            for (int i = 0; i + 1 < chart.cameraKeys.Length; i++)
            {
                if (!CameraCurveButtonRect(i, out var button)) continue;
                string id = SpatialDirector.CameraEasing(chart.cameraKeys[i]);
                int option = Array.IndexOf(CameraCurveIds, id); if (option < 0) option = 3;
                if (GUI.Button(button, CameraCurveShortNames[option],
                    openCameraCurveSegment == i ? selectedButtonStyle : buttonStyle))
                {
                    openCameraCurveSegment = openCameraCurveSegment == i ? -1 : i;
                    clearGuiFocus = true;
                }
            }
            if (openCameraCurveSegment < 0 || !CameraCurveButtonRect(openCameraCurveSegment, out var anchorRect)) return;
            Rect popup = CameraCurvePopupRect(anchorRect);
            GUI.Box(popup, "", panelStyle);
            string current = SpatialDirector.CameraEasing(chart.cameraKeys[openCameraCurveSegment]);
            for (int i = 0; i < CameraCurveIds.Length; i++)
            {
                Rect option = new Rect(popup.x + 4, popup.y + 4 + i * 27, popup.width - 8, 25);
                if (!GUI.Button(option, CameraCurveNames[i], current == CameraCurveIds[i] ? selectedButtonStyle : buttonStyle)) continue;
                int segment = openCameraCurveSegment; string selected = CameraCurveIds[i];
                openCameraCurveSegment = -1;
                Change(() => chart.cameraKeys[segment].easing = selected,
                    "Camera K" + segment + "→K" + (segment + 1) + " curve: " + CameraCurveNames[i]);
                break;
            }
        }
    }
}
