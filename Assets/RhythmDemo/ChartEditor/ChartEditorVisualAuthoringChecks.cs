using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        bool RunVisualAuthoringChecks()
        {
            string original = JsonUtility.ToJson(chart); var results = new List<string>(); bool passed = true;
            Action<bool, string> check = (condition, name) => { passed &= condition; results.Add((condition ? "PASS " : "FAIL ") + name); };
            try
            {
                NewChart(); EnsureVisualLibrary();
                check(sceneLibrary.Count >= 6, "Built-in scene pack is available");
                check(effectLibrary.Count >= 10, "Built-in effect pack is available");
                check(motionLibrary.Count >= 7, "Built-in camera-motion pack is available");
                check(SceneThumbnail(sceneLibrary[0]) != null && SceneThumbnail(sceneLibrary[0]).width > 1,
                    "Every scene asset can provide a thumbnail");
                string originalAssetName = sceneLibrary[0].name;
                check(RenameSceneAsset(0, "Renamed asset", false) && sceneLibrary[0].name == "Renamed asset",
                    "Every scene asset, including built-ins, can be renamed");
                RenameSceneAsset(0, originalAssetName, false);
                check(ClampInspectorPanelWidth(-100) == MinimumPanelWidth && ClampInspectorPanelWidth(10000) <= MaximumPanelWidth,
                    "Left inspector resize remains within usable bounds");

                AddSceneObject(sceneLibrary[1]);
                AddEffectClip(effectLibrary[0]);
                AddMotionClip(motionLibrary[0]);
                check(chart.sceneObjects.Length == 1 && chart.sceneObjects[0].assetId == sceneLibrary[1].id, "Scene asset instantiates portable chart data");
                check(chart.effectClips.Length == 1 && chart.effectClips[0].durationTicks > 0, "Effect preset creates a timeline clip");
                check(chart.cameraMotionClips.Length == 1 && chart.cameraMotionClips[0].keys.Length >= 2, "Motion preset copies editable keys");

                SetMode(ChartEditMode.Scene); check(TimelineRows().Count == 1, "Scene is a dedicated untimed construction page");
                var sceneObject = chart.sceneObjects[0]; selectedSceneObject = -1;
                Ray pickRay = new Ray(sceneCamera.transform.position, (sceneObject.position - sceneCamera.transform.position).normalized);
                check(SelectSceneObject(pickRay, false) && selectedSceneObject == 0 && SceneDetailVisible && RightPanelWidth > 0,
                    "Viewport ray selection opens the scene-object inspector");
                sceneTransformTool = SceneTransformTool.Move; check(GizmoPose(out Vector3 objectOrigin, out _), "Selected scene object exposes a move gizmo");
                dragMode = ChartEditMode.Scene; dragIndex = selectedSceneObject; ApplyGizmoPosition(objectOrigin + new Vector3(2, 3, 4));
                check(Vector3.Distance(chart.sceneObjects[0].position, objectOrigin + new Vector3(2, 3, 4)) < .001f,
                    "Scene move gizmo writes object position");
                sceneTransformTool = SceneTransformTool.Rotate; dragIndex = selectedSceneObject; ApplySceneObjectRotation(Quaternion.Euler(15, 25, 35));
                check(Quaternion.Angle(Quaternion.Euler(chart.sceneObjects[0].rotation), Quaternion.Euler(15, 25, 35)) < .01f && GizmoPose(out _, out _),
                    "Scene rotate gizmo writes object rotation");
                SetMode(ChartEditMode.Effects); check(TimelineRows().Count == 2 && new List<TimelineItem>(TimelineItems(new TimelineRow { kind = TimelineKind.Effect })).Count == 1, "Effects page exposes duration clips");
                SetMode(ChartEditMode.CameraMotion); check(TimelineRows().Count == 2 && new List<TimelineItem>(TimelineItems(new TimelineRow { kind = TimelineKind.Motion })).Count == 1, "Motion page exposes reusable clips");

                var motion = chart.cameraMotionClips[0]; motion.kind = "custom";
                motion.keys = new[] { new CameraMotionKeyData { time = 0 }, new CameraMotionKeyData { time = .5f, position = new Vector3(1, 2, 3), rotation = new Vector3(4, 5, 6), fov = 7 }, new CameraMotionKeyData { time = 1 } };
                double middle = tempo.SecondsAtBeat((motion.startTick + motion.durationTicks * .5) / (double)chart.ticksPerBeat);
                spatial.EvaluateCamera(evaluatorCamera, middle); CameraMotionEvaluator.Apply(chart, tempo, evaluatorCamera, middle);
                Vector3 firstPosition = evaluatorCamera.transform.position; Quaternion firstRotation = evaluatorCamera.transform.rotation; float firstFov = evaluatorCamera.fieldOfView;
                spatial.EvaluateCamera(evaluatorCamera, middle); CameraMotionEvaluator.Apply(chart, tempo, evaluatorCamera, middle);
                check(Vector3.Distance(firstPosition, evaluatorCamera.transform.position) < .0001f && Quaternion.Angle(firstRotation, evaluatorCamera.transform.rotation) < .001f && Mathf.Abs(firstFov - evaluatorCamera.fieldOfView) < .001f,
                    "Camera motion is deterministic under seek");

                RebuildVisuals(); authoredVisuals.Evaluate(middle);
                check(visualRoot.Find("Authored visuals/Reusable scene objects") != null, "Authored scene renders in the editor");
                string json = JsonUtility.ToJson(chart); var copy = JsonUtility.FromJson<ChartData>(json);
                check(copy.sceneObjects.Length == 1 && copy.effectClips.Length == 1 && copy.cameraMotionClips.Length == 1 && copy.cameraMotionClips[0].keys.Length == 3,
                    "Scene, effect and motion data survive JSON round-trip");
            }
            catch (Exception e) { passed = false; results.Add("FAIL Exception: " + e); }
            finally
            {
                chart = JsonUtility.FromJson<ChartData>(original); selectedSceneObject = selectedEffectClip = selectedMotionClip = -1;
                Rebuild();
            }
            File.WriteAllLines(Path.Combine(smokeDirectory, "visual-authoring-checks.txt"), results);
            return passed;
        }
    }
}
