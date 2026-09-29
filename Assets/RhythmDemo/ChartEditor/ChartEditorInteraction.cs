using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        enum SceneTransformTool { Move, Rotate }
        SceneTransformTool sceneTransformTool;
        bool flyMode, navigationInitialized, ignoreLookFrame, textInputFocused, clearGuiFocus;
        Vector2 inspectorScroll, sceneDetailScroll;
        const float FlightSpeed = 14;
        const float MarkerDistance = 12;
        int dragAxis, dragIndex, dragTick;
        ChartEditMode dragMode;
        Vector3 dragOrigin, dragDirection, dragGrab, dragCameraTarget;
        Quaternion dragFrame;
        Plane gizmoDragPlane;
        bool dragPlaneValid;
        Vector2 dragMouse, dragScreenDirection;
        float dragWorldPerPixel;
        Quaternion dragSceneRotation;
        Vector3 dragRotationNormal, dragRotationStartVector;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern short GetKeyState(int key);
#endif
        int PlayheadTick => Mathf.RoundToInt(CurrentBeat * chart.ticksPerBeat);
        // One placement point at the center of the 3D viewport. Time and authored
        // camera keys must never move either this point or the editing viewpoint.
        Vector3 PlacementMarkerPosition => sceneCamera.ViewportToWorldPoint(new Vector3(.5f, .5f, MarkerDistance));

        void UpdateViewportRect()
        {
            float left = chartCameraPreview ? 0 : Mathf.Min(PanelWidth * uiScale, Screen.width - 40);
            float right = chartCameraPreview ? 0 : Mathf.Min(RightPanelWidth * uiScale, Screen.width - left - 40);
            float bottom = Mathf.Min(TimelineHeight * uiScale, Screen.height - 80);
            sceneCamera.pixelRect = new Rect(left, bottom, Mathf.Max(40, Screen.width - left - right),
                Mathf.Max(40, Screen.height - bottom - ToolbarHeight * uiScale));
        }
        void ReadInspectorResize(Event e)
        {
            if (chartCameraPreview || WorkspaceInputBlocked) { inspectorResizing = false; return; }
            Vector2 mouse = e.mousePosition / uiScale;
            if (e.type == EventType.MouseDown && e.button == 0 &&
                new Rect(PanelWidth - 6, ToolbarHeight, 12, TimelineTop - ToolbarHeight).Contains(mouse))
            {
                inspectorResizing = true; inspectorResizeStartX = mouse.x; inspectorResizeStartWidth = PanelWidth;
                CancelTimelineGesture(); draggingHandle = false; e.Use(); return;
            }
            if (inspectorResizing && e.type == EventType.MouseDrag && e.button == 0)
            {
                panelWidth = ClampInspectorPanelWidth(inspectorResizeStartWidth + mouse.x - inspectorResizeStartX);
                UpdateViewportRect(); e.Use(); return;
            }
            if (inspectorResizing && e.type == EventType.MouseUp && e.button == 0)
            {
                inspectorResizing = false; PlayerPrefs.SetFloat("ChartStudio.PanelWidth", PanelWidth); PlayerPrefs.Save(); e.Use();
            }
        }
        float ClampInspectorPanelWidth(float value)
        {
            float maximum = Mathf.Min(MaximumPanelWidth, ViewWidth - RightPanelWidth - 280);
            return Mathf.Clamp(value, MinimumPanelWidth, Mathf.Max(MinimumPanelWidth, maximum));
        }
        void ReadNavigationMode()
        {
            if (chartCameraPreview) { ReleaseMouse(); return; }
            bool enabled = flyMode;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            enabled = (GetKeyState(0x14) & 1) != 0;
#else
            if (Input.GetKeyDown(KeyCode.CapsLock)) enabled = !flyMode;
#endif
            if (!navigationInitialized)
            {
                ApplyOrbitCamera(); navigationInitialized = true;
            }
            if (enabled != flyMode) SetFlightMode(enabled);
            if (Input.GetKeyDown(KeyCode.Escape) || WorkspaceInputBlocked || !Application.isFocused)
                ReleaseMouse();
        }
        void SetFlightMode(bool enabled)
        {
            draggingHandle = false;
            if (!enabled)
            {
                // Keep the exact current view when returning to orbit navigation.
                orbitPivot = sceneCamera.transform.position + sceneCamera.transform.forward * orbitDistance;
                ReadViewAngles();
            }
            else ReadViewAngles();
            flyMode = enabled;
            ReleaseMouse();
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (enabled && PointerInViewport(mouse) && !textInputFocused && !chartCameraPreview) CaptureMouse();
            SetStatus(enabled ? "Caps ON: creative flight. Esc releases mouse; RMB resumes." : "Caps OFF: orbit / pan editing");
        }
        void ReadViewAngles()
        {
            orbitYaw = sceneCamera.transform.eulerAngles.y;
            orbitPitch = Mathf.DeltaAngle(0, sceneCamera.transform.eulerAngles.x);
        }
        void CaptureMouse()
        {
            clearGuiFocus = true; textInputFocused = false; ignoreLookFrame = true;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
        void ReleaseMouse() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        void OnApplicationFocus(bool focused) { if (!focused) { ReleaseMouse(); draggingHandle = inspectorResizing = false; CancelTimelineGesture(); } }
        void ReadFlight()
        {
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (Input.GetMouseButtonDown(1) && PointerInViewport(mouse) && !draggingHandle) CaptureMouse();
            if (Cursor.lockState != CursorLockMode.Locked || WorkspaceInputBlocked || !Application.isFocused) return;
            if (ignoreLookFrame) { ignoreLookFrame = false; return; }
            orbitYaw += Input.GetAxis("Mouse X") * 2;
            orbitPitch = Mathf.Clamp(orbitPitch - Input.GetAxis("Mouse Y") * 2, -89, 89);
            sceneCamera.transform.rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0);
            float right = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float forward = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            float up = FlightVerticalInput(Input.GetKey(KeyCode.Space),
                Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            float speed = FlightSpeed * FlightSpeedMultiplier(
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            sceneCamera.transform.position += FlightMovement(orbitYaw, right, forward, up) * speed * Time.unscaledDeltaTime;
        }
        static float FlightVerticalInput(bool ascend, bool descend) => (ascend ? 1 : 0) - (descend ? 1 : 0);
        static float FlightSpeedMultiplier(bool accelerate) => accelerate ? 3 : 1;
        static Vector3 FlightMovement(float yaw, float right, float forward, float up)
            => Quaternion.Euler(0, yaw, 0) * Vector3.ClampMagnitude(new Vector3(right, up, forward), 1);

        void ToggleCameraPreview()
        {
            SetPreviewMode(!chartCameraPreview);
        }
        void DrawNavigationStatus()
        {
            string state = flyMode ? "CAPS ON  |  CREATIVE FLIGHT" : "CAPS OFF  |  ORBIT / PAN";
            if (chartCameraPreview) state += "  |  CAMERA PREVIEW (navigation paused)";
            string hint = flyMode ? (Cursor.lockState == CursorLockMode.Locked
                ? "WASD move · Space up · Shift down · Ctrl fast · Esc release mouse"
                : "Mouse released · RMB in viewport to fly · Caps OFF returns to orbit")
                : "RMB orbit · MMB pan · F focus selection · Drag colored axes · Wheel zoom disabled";
            GUI.Label(new Rect(PanelWidth + 14, ToolbarHeight + 10, ViewportRight - PanelWidth - 28, 24), state, headingStyle);
            GUI.Label(new Rect(PanelWidth + 14, ToolbarHeight + 38, ViewportRight - PanelWidth - 28, 38), hint, smallStyle);
            DrawEditorPlaybackStatus();
            if (mode == ChartEditMode.Camera && !chartCameraPreview)
            {
                Vector2 marker = WorldGui(PlacementMarkerPosition);
                if (sceneCamera.WorldToScreenPoint(PlacementMarkerPosition).z > 0)
                    GUI.Label(new Rect(marker.x + 12, marker.y + 8, 350, 22), "NEW KEY POSITION / CENTER", smallStyle);
            }
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 center = new Vector2(sceneCamera.pixelRect.center.x, Screen.height - sceneCamera.pixelRect.center.y) / uiScale;
                GuiLine(center - Vector2.right * 6, center + Vector2.right * 6, Color.white, 1);
                GuiLine(center - Vector2.up * 6, center + Vector2.up * 6, Color.white, 1);
            }
        }
        bool GizmoPose(out Vector3 origin, out Quaternion frame)
        {
            frame = Quaternion.identity; origin = Vector3.zero;
            if (mode == ChartEditMode.Stage)
            {
                var p = chart.stagePath.points[selectedStagePoint]; origin = new Vector3(p.x, p.y, p.z); return true;
            }
            if (mode == ChartEditMode.Camera)
            {
                spatial.EvaluateCamera(evaluatorCamera, tempo.SecondsAtBeat(chart.cameraKeys[selectedCameraKey].beat));
                origin = evaluatorCamera.transform.position; return true;
            }
            if (mode == ChartEditMode.Paths)
            {
                float distance = spatial.OffsetDistance(PlayheadTick);
                Vector2 offset = spatial.OffsetAtDistance(chart.paths[selectedPath].id, distance);
                origin = spatial.RoutePoint(distance, offset.x, offset.y); frame = spatial.RouteRotationAt(distance); return true;
            }
            if (mode == ChartEditMode.Scene && selectedSceneObject >= 0 && selectedSceneObject < chart.sceneObjects.Length)
            {
                var sceneObject = chart.sceneObjects[selectedSceneObject]; origin = sceneObject.position;
                frame = sceneTransformTool == SceneTransformTool.Rotate ? Quaternion.Euler(sceneObject.rotation) : Quaternion.identity;
                return true;
            }
            return false;
        }
        void FocusSelection()
        {
            Vector3 origin;
            if (mode == ChartEditMode.Notes && selectedNote >= 0 && selectedNote < chart.notes.Length)
                EditorNotePose(chart.notes[selectedNote], out origin, out _);
            else if (!GizmoPose(out origin, out _)) return;
            ReleaseMouse(); orbitPivot = origin;
            if (flyMode) sceneCamera.transform.position = origin - sceneCamera.transform.forward * MarkerDistance;
            else { orbitDistance = mode == ChartEditMode.Camera ? MarkerDistance : 24; ApplyOrbitCamera(); }
        }
        Vector2 WorldGui(Vector3 world)
        {
            Vector3 p = sceneCamera.WorldToScreenPoint(world);
            return new Vector2(p.x, Screen.height - p.y) / uiScale;
        }
        float GizmoLength(Vector3 origin)
        {
            float depth = Mathf.Max(.2f, sceneCamera.WorldToScreenPoint(origin).z);
            return 90 * uiScale * 2 * depth * Mathf.Tan(sceneCamera.fieldOfView * Mathf.Deg2Rad * .5f) / sceneCamera.pixelHeight;
        }
        static Vector3 Axis(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        Vector2 AxisEnd(Vector3 origin, Quaternion frame, int axis) => WorldGui(origin + frame * Axis(axis) * GizmoLength(origin));
        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            return Vector2.Distance(p, a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.001f, d.sqrMagnitude)));
        }
        int HitGizmo(Vector2 mouse)
        {
            if (!GizmoPose(out var origin, out var frame) || sceneCamera.WorldToScreenPoint(origin).z <= .1f) return -1;
            if (mode == ChartEditMode.Scene && sceneTransformTool == SceneTransformTool.Rotate)
                return HitRotationGizmo(mouse, origin, frame);
            Vector2 start = WorldGui(origin);
            if ((mouse - start).sqrMagnitude <= 100) return 3;
            int count = mode == ChartEditMode.Paths ? 2 : 3, result = -1;
            float best = 11;
            for (int axis = 0; axis < count; axis++)
            {
                Vector2 end = AxisEnd(origin, frame, axis);
                if ((end - start).sqrMagnitude < 100) continue;
                float distance = SegmentDistance(mouse, Vector2.Lerp(start, end, .17f), end);
                if (distance < best) { best = distance; result = axis; }
            }
            return result;
        }
        int HitRotationGizmo(Vector2 mouse, Vector3 origin, Quaternion frame)
        {
            int result = -1; float best = 9, radius = GizmoLength(origin) * .72f;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 a = Axis((axis + 1) % 3), b = Axis((axis + 2) % 3);
                Vector2 previous = WorldGui(origin + frame * a * radius);
                for (int step = 1; step <= 48; step++)
                {
                    float angle = step / 48f * Mathf.PI * 2;
                    Vector2 current = WorldGui(origin + frame * (a * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * radius);
                    float distance = SegmentDistance(mouse, previous, current);
                    if (distance < best) { best = distance; result = axis; }
                    previous = current;
                }
            }
            return result;
        }
        ChartEditorHandle PickHandle(Vector2 mouse)
        {
            ChartEditorHandle result = null; float best = 15 * uiScale;
            foreach (var handle in visualRoot.GetComponentsInChildren<ChartEditorHandle>())
            {
                if (handle.mode != mode) continue;
                var screen = sceneCamera.WorldToScreenPoint(handle.transform.position);
                if (screen.z <= .1f) continue;
                float distance = Vector2.Distance(mouse, new Vector2(screen.x, Screen.height - screen.y));
                if (distance < best) { best = distance; result = handle; }
            }
            return result;
        }
        void BeginGizmoDrag(int axis, Ray ray, Vector2 mouse)
        {
            if (!GizmoPose(out dragOrigin, out dragFrame)) return;
            RecordUndo(); dragAxis = axis; dragMode = mode;
            dragIndex = mode == ChartEditMode.Stage ? selectedStagePoint : mode == ChartEditMode.Camera ? selectedCameraKey :
                mode == ChartEditMode.Scene ? selectedSceneObject : selectedPath;
            dragTick = PlayheadTick; dragMouse = mouse;
            if (mode == ChartEditMode.Scene && sceneTransformTool == SceneTransformTool.Rotate)
            {
                dragSceneRotation = Quaternion.Euler(chart.sceneObjects[dragIndex].rotation);
                dragRotationNormal = dragFrame * Axis(axis);
                gizmoDragPlane = new Plane(dragRotationNormal, dragOrigin);
                dragPlaneValid = gizmoDragPlane.Raycast(ray, out float rotationEnter);
                if (dragPlaneValid) dragRotationStartVector = (ray.GetPoint(rotationEnter) - dragOrigin).normalized;
                draggingHandle = dragPlaneValid; return;
            }
            if (mode == ChartEditMode.Camera)
            {
                var key = chart.cameraKeys[dragIndex];
                if (!key.useWorldPose)
                {
                    key.worldPosition = dragOrigin; key.worldTarget = dragOrigin + evaluatorCamera.transform.forward * 20;
                    key.useWorldPose = true; key.usePathPose = false;
                    spatial = new SpatialDirector(chart, tempo); RebuildVisuals();
                }
                dragCameraTarget = key.worldTarget;
            }
            if (mode == ChartEditMode.Paths) EnsureOffsetKey(dragTick);
            dragDirection = dragFrame * Axis(axis);
            Vector3 normal = axis == 3 ? (mode == ChartEditMode.Paths ? dragFrame * Vector3.forward : sceneCamera.transform.forward)
                : Vector3.ProjectOnPlane(sceneCamera.transform.forward, dragDirection).normalized;
            gizmoDragPlane = new Plane(normal, dragOrigin);
            dragPlaneValid = normal.sqrMagnitude > .5f && gizmoDragPlane.Raycast(ray, out _);
            if (dragPlaneValid && gizmoDragPlane.Raycast(ray, out float enter)) dragGrab = ray.GetPoint(enter);
            dragScreenDirection = (WorldGui(dragOrigin + dragDirection * GizmoLength(dragOrigin)) - WorldGui(dragOrigin)) * uiScale;
            dragWorldPerPixel = GizmoLength(dragOrigin) / Mathf.Max(18 * uiScale, dragScreenDirection.magnitude);
            dragScreenDirection = dragScreenDirection.magnitude > 2 ? dragScreenDirection.normalized : Vector2.up;
            draggingHandle = true;
        }
        void UpdateGizmoDrag(Ray ray, Vector2 mouse)
        {
            if (dragMode == ChartEditMode.Scene && sceneTransformTool == SceneTransformTool.Rotate)
            {
                if (!gizmoDragPlane.Raycast(ray, out float rotationEnter)) return;
                Vector3 current = (ray.GetPoint(rotationEnter) - dragOrigin).normalized;
                float angle = Vector3.SignedAngle(dragRotationStartVector, current, dragRotationNormal);
                ApplySceneObjectRotation(Quaternion.AngleAxis(angle, dragRotationNormal) * dragSceneRotation); return;
            }
            Vector3 delta;
            if (dragPlaneValid && gizmoDragPlane.Raycast(ray, out float enter))
            {
                delta = ray.GetPoint(enter) - dragGrab;
                if (dragAxis != 3) delta = Vector3.Project(delta, dragDirection);
            }
            else
            {
                if (dragAxis == 3) return; // Edge-on plane: rotate the view to expose it.
                delta = dragDirection * Vector2.Dot(mouse - dragMouse, dragScreenDirection) * dragWorldPerPixel;
            }
            ApplyGizmoPosition(dragOrigin + delta);
        }
        void ApplyGizmoPosition(Vector3 position)
        {
            if (dragMode == ChartEditMode.Stage)
            {
                var point = chart.stagePath.points[dragIndex]; point.x = position.x; point.y = position.y; point.z = position.z;
            }
            else if (dragMode == ChartEditMode.Camera)
            {
                var key = chart.cameraKeys[dragIndex]; key.worldPosition = position;
                key.worldTarget = dragCameraTarget + position - dragOrigin;
            }
            else if (dragMode == ChartEditMode.Paths)
            {
                float distance = spatial.OffsetDistance(dragTick);
                Vector3 local = Quaternion.Inverse(dragFrame) * (position - spatial.RouteAt(distance));
                var key = Array.Find(chart.paths[dragIndex].offsetKeys, k => k.tick == dragTick);
                key.x = local.x; key.y = local.y;
            }
            else if (dragMode == ChartEditMode.Scene) chart.sceneObjects[dragIndex].position = position;
            spatial = new SpatialDirector(chart, tempo); RebuildVisuals();
        }
        void ApplySceneObjectRotation(Quaternion rotation)
        {
            if (dragIndex < 0 || dragIndex >= chart.sceneObjects.Length) return;
            chart.sceneObjects[dragIndex].rotation = rotation.eulerAngles; RebuildVisuals();
        }
        void DrawGizmo()
        {
            if (WorkspaceInputBlocked || playing || chartCameraPreview || Cursor.lockState == CursorLockMode.Locked ||
                !GizmoPose(out var origin, out var frame) || sceneCamera.WorldToScreenPoint(origin).z <= .1f) return;
            Vector2 start = WorldGui(origin);
            if (mode == ChartEditMode.Scene && sceneTransformTool == SceneTransformTool.Rotate)
            {
                DrawRotationGizmo(origin, frame); return;
            }
            // Draw in absolute GUI coordinates. RotateAroundPivot inside a scaled GUI
            // group applies its clip offset twice; panels drawn afterwards mask overflow.
            Vector2 shift = Vector2.zero;
            int count = mode == ChartEditMode.Paths ? 2 : 3;
            if (mode == ChartEditMode.Paths)
            {
                float size = GizmoLength(origin) * .32f;
                Vector2 a = WorldGui(origin + frame * new Vector3(size, 0, 0)) - shift;
                Vector2 b = WorldGui(origin + frame * new Vector3(size, size, 0)) - shift;
                Vector2 c = WorldGui(origin + frame * new Vector3(0, size, 0)) - shift;
                GuiLine(a, b, new Color(.65f, .7f, .8f), 1); GuiLine(b, c, new Color(.65f, .7f, .8f), 1);
            }
            for (int axis = 0; axis < count; axis++)
            {
                Color color = axis == 0 ? new Color(1, .3f, .3f) : axis == 1 ? new Color(.4f, 1, .45f) : new Color(.35f, .65f, 1);
                Vector2 end = AxisEnd(origin, frame, axis), direction = (end - start).normalized;
                if ((end - start).sqrMagnitude < 100) continue;
                Vector2 side = new Vector2(-direction.y, direction.x);
                GuiLine(start - shift, end - shift, color, 3);
                GuiLine(end - shift, end - direction * 11 + side * 5 - shift, color, 3);
                GuiLine(end - shift, end - direction * 11 - side * 5 - shift, color, 3);
                Color previous = GUI.color; GUI.color = color;
                GUI.Label(new Rect(end.x - shift.x + 6, end.y - shift.y - 12, 28, 24), axis == 0 ? "X" : axis == 1 ? "Y" : "Z", headingStyle);
                GUI.color = previous;
            }
            Color saved = GUI.color; GUI.color = new Color(1, .8f, .25f);
            GUI.DrawTexture(new Rect(start.x - shift.x - 5, start.y - shift.y - 5, 10, 10), Texture2D.whiteTexture); GUI.color = saved;
        }
        void DrawRotationGizmo(Vector3 origin, Quaternion frame)
        {
            float radius = GizmoLength(origin) * .72f;
            for (int axis = 0; axis < 3; axis++)
            {
                Color color = axis == 0 ? new Color(1, .3f, .3f) : axis == 1 ? new Color(.4f, 1, .45f) : new Color(.35f, .65f, 1);
                Vector3 a = Axis((axis + 1) % 3), b = Axis((axis + 2) % 3);
                Vector2 previous = WorldGui(origin + frame * a * radius);
                for (int step = 1; step <= 48; step++)
                {
                    float angle = step / 48f * Mathf.PI * 2;
                    Vector2 current = WorldGui(origin + frame * (a * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * radius);
                    GuiLine(previous, current, color, 2.5f); previous = current;
                }
            }
            Vector2 center = WorldGui(origin); Color saved = GUI.color; GUI.color = new Color(1, .8f, .25f);
            GUI.DrawTexture(new Rect(center.x - 4, center.y - 4, 8, 8), Texture2D.whiteTexture); GUI.color = saved;
        }
        static void GuiLine(Vector2 start, Vector2 end, Color color, float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            Matrix4x4 matrix = GUI.matrix; Color previous = GUI.color;
            GUI.color = color;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(start.x, start.y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg), Vector3.one);
            GUI.DrawTexture(new Rect(0, -width * .5f, Vector2.Distance(start, end), width), Texture2D.whiteTexture);
            GUI.matrix = matrix; GUI.color = previous;
        }
        PathOffsetKey EnsureOffsetKey(int tick)
        {
            timelineSelection = TimelineKind.Offset;
            var path = chart.paths[selectedPath];
            Vector2 offset = spatial.OffsetAtDistance(path.id, spatial.OffsetDistance(tick));
            var list = new List<PathOffsetKey>(path.offsetKeys ?? new PathOffsetKey[0]);
            if (list.Count == 0)
            {
                // Seed boundaries so editing one beat does not translate the entire path.
                foreach (var section in chart.sections)
                {
                    var p = Array.Find(section.placements, item => item.pathId == path.id);
                    if (p != null) list.Add(new PathOffsetKey { tick = Mathf.RoundToInt(section.startBeat * chart.ticksPerBeat), x = p.x, y = p.y });
                }
                Vector2 end = spatial.OffsetAtDistance(path.id, spatial.OffsetDistance(Mathf.RoundToInt(chart.endBeat * chart.ticksPerBeat)));
                list.Add(new PathOffsetKey { tick = Mathf.RoundToInt(chart.endBeat * chart.ticksPerBeat), x = end.x, y = end.y });
            }
            var key = list.Find(k => k.tick == tick);
            if (key == null) { key = new PathOffsetKey { tick = tick, x = offset.x, y = offset.y }; list.Add(key); }
            list.Sort((a, b) => a.tick.CompareTo(b.tick)); path.offsetKeys = list.ToArray(); return key;
        }
        void DrawOffsetInspector()
        {
            var path = chart.paths[selectedPath]; int tick = PlayheadTick;
            GUILayout.Space(8); GUILayout.Label("OFFSET AT BEAT " + CurrentBeat.ToString("0.###"), headingStyle);
            float beat = CurrentBeat;
            if (FloatField("Beat", ref beat, false)) Seek(tempo.SecondsAtBeat(Mathf.Clamp(beat, 0, chart.endBeat)));
            tick = PlayheadTick;
            var key = path.offsetKeys == null ? null : Array.Find(path.offsetKeys, k => k.tick == tick);
            Vector2 value = spatial.OffsetAtDistance(path.id, spatial.OffsetDistance(tick));
            bool changed = FloatField("Offset X", ref value.x); changed |= FloatField("Offset Y", ref value.y);
            if (changed) { key = EnsureOffsetKey(tick); key.x = value.x; key.y = value.y; RebuildVisuals(); }
            if (GUILayout.Button(key == null ? "Add offset key here" : "Update offset key here"))
                Change(() => EnsureOffsetKey(PlayheadTick), "Offset key stored at this beat");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous key")) SeekOffsetKey(-1);
            if (GUILayout.Button("Next key")) SeekOffsetKey(1);
            GUILayout.EndHorizontal();
            bool previous = GUI.enabled; GUI.enabled = previous && key != null;
            if (GUILayout.Button("Delete offset key")) Change(() =>
            {
                var list = new List<PathOffsetKey>(path.offsetKeys); list.RemoveAll(k => k.tick == tick); path.offsetKeys = list.ToArray();
            }, "Offset key deleted");
            GUI.enabled = previous;
            if (GUILayout.Button("Focus cross-section  [F]")) FocusSelection();
            GUILayout.Label((path.offsetKeys == null ? 0 : path.offsetKeys.Length) +
                " offset keys · X/Y are local to the stage. Drag arrows or center square; edits create a key at this beat. Other keys stay fixed.", smallStyle);
        }
        void SeekOffsetKey(int direction)
        {
            var keys = chart.paths[selectedPath].offsetKeys; if (keys == null) return;
            int tick = PlayheadTick, target = direction > 0 ? int.MaxValue : int.MinValue;
            foreach (var key in keys)
                if (direction > 0 && key.tick > tick) target = Math.Min(target, key.tick);
                else if (direction < 0 && key.tick < tick) target = Math.Max(target, key.tick);
            if (target != int.MaxValue && target != int.MinValue) Seek(tempo.SecondsAtBeat(target / (double)chart.ticksPerBeat));
        }
    }
}
