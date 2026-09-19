using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        bool RunStaticNoteChecks()
        {
            string snapshot = JsonUtility.ToJson(chart), savedBaseline = savedChartJson;
            bool savedNeedsSaveAs = needsSaveAs;
            var report = new StringBuilder(); int checks = 0; bool passed = false;
            Action<bool, string> check = (value, name) =>
            {
                if (!value) throw new InvalidOperationException(name);
                checks++; report.AppendLine("PASS " + name);
            };
            try
            {
                NewChart(); flyMode = false; showSettings = showGuide = showFilesMenu = false;
                chart.tempos = new[] { new TempoData { tick = 0, bpm = 120 }, new TempoData { tick = 32 * 480, bpm = 180 } };
                chart.stagePath.points[2].roll = 23;
                chart.paths[0].roll = 30;
                chart.paths[0].offsetKeys = new[]
                {
                    new PathOffsetKey { tick = 0, x = -4 }, new PathOffsetKey { tick = 32 * 480, x = 4, y = 3 },
                    new PathOffsetKey { tick = 96 * 480, x = -3, y = -2 }
                };
                chart.sections = new[] { chart.sections[0], new SectionData { startBeat = 32, name = "LATER",
                    placements = new[] { Placement("p0", -4), new PathPlacement { pathId = "p1", x = 5, y = 6 }, Placement("p2", 4) } } };
                chart.notes = new[]
                {
                    new NoteData { id = "static-0", tick = 0, pathId = "p0", action = "tap" },
                    new NoteData { id = "static-4", tick = 4 * 480, pathId = "p1", action = "drag" },
                    new NoteData { id = "static-16", tick = 16 * 480, pathId = "p0", action = "tap" },
                    new NoteData { id = "static-64", tick = 64 * 480, pathId = "p1", action = "tap" },
                    new NoteData { id = "static-120", tick = 120 * 480, pathId = "p0", action = "tap", protectedNote = true }
                };
                Rebuild();
                var positions = new Vector3[chart.notes.Length]; var rotations = new Quaternion[chart.notes.Length];
                for (int i = 0; i < chart.notes.Length; i++)
                {
                    var note = chart.notes[i]; EditorNotePose(note, out positions[i], out rotations[i]);
                    Vector3 expected = spatial.Point(note.pathId, SpatialDirector.NearDepth, NoteHitTime(note));
                    check(Vector3.Distance(positions[i], expected) < .0001f, "Fixed note matches its own hit position " + note.id);
                }
                foreach (ChartEditMode view in Enum.GetValues(typeof(ChartEditMode)))
                {
                    SetMode(view);
                    foreach (double time in new[] { 0, 12, Duration })
                    {
                        Seek(time);
                        bool allFixed = true;
                        for (int i = 0; i < chart.notes.Length; i++)
                        {
                            Transform note = visualRoot.Find(chart.notes[i].id);
                            allFixed &= note != null && note.gameObject.activeInHierarchy && note.GetComponent<Renderer>().enabled &&
                                Vector3.Distance(note.position, positions[i]) < .0001f &&
                                Quaternion.Angle(note.rotation, rotations[i] * Quaternion.Euler(90, 0, 0)) < .05f;
                        }
                        check(allFixed, "All notes remain visible and fixed in " + view + " at " + time.ToString("0.##") + "s");
                    }
                }
                SetMode(ChartEditMode.Paths); Seek(0);
                var pathLine = visualRoot.Find("Note path p0").GetComponent<LineRenderer>();
                Vector3 first = pathLine.GetPosition(0), last = pathLine.GetPosition(pathLine.positionCount - 1);
                check(Vector3.Distance(first, spatial.Point("p0", SpatialDirector.NearDepth, 0)) < .001f &&
                    Vector3.Distance(last, spatial.Point("p0", SpatialDirector.NearDepth, Duration)) < .001f,
                    "Edit path spans the entire chart, including later note positions");
                Transform staticNote = visualRoot.Find("static-120"), staticRoot = visualRoot;
                Vector3 crossSection = pathPlacementHandles[0].position;
                SetPlaying(true); songTime = 12; RefreshEditorPlayheadVisuals(); SetPlaying(false);
                check(visualRoot == staticRoot && visualRoot.Find("static-120") == staticNote && Vector3.Distance(staticNote.position, positions[4]) < .001f,
                    "Edit audio playback keeps note objects and positions intact");
                check(Vector3.Distance(pathPlacementHandles[0].position, crossSection) > 1, "Path edit cross-section still follows playhead");
                pathLine = visualRoot.Find("Note path p0").GetComponent<LineRenderer>();
                check(pathLine.GetPosition(0) == first && pathLine.GetPosition(pathLine.positionCount - 1) == last, "Edit path does not scroll with playhead");
                SetMode(ChartEditMode.Notes); selectedNote = 4; FocusSelection();
                check(Vector3.Distance(orbitPivot, positions[4]) < .001f, "F focuses the fixed selected note, independent of playhead");
                var noteHandle = visualRoot.Find("static-4").GetComponent<ChartEditorHandle>(); SelectHandle(noteHandle);
                check(selectedNote == 1 && selectedPath == 1 && mode == ChartEditMode.Notes, "3D note selection resolves note and path");
                Seek(0); SetPreviewMode(true); SetPlaying(false);
                check(previewMotionRoot.Find("static-4") != null && previewMotionRoot.Find("static-120") == null,
                    "Preview alone uses the approach visibility window");
                Vector3 previewStart = previewMotionRoot.Find("static-4").position;
                Seek(1);
                check(Vector3.Distance(previewMotionRoot.Find("static-4").position, previewStart) > 1, "Notes still move with time in Preview");
                Seek(2);
                check(Vector3.Distance(previewMotionRoot.Find("static-4").position, positions[1]) < .001f,
                    "Preview reaches the exact fixed edit position at hit time");
                Seek(3); check(previewMotionRoot.Find("static-4") == null, "Preview hides notes after hit time");
                SetPreviewMode(false);
                check(visualRoot.Find("static-4") != null && visualRoot.Find("static-120") != null &&
                    Vector3.Distance(visualRoot.Find("static-120").position, positions[4]) < .001f,
                    "Leaving Preview restores past and future notes at fixed locations");
                Change(() => chart.notes[4].tick = 124 * 480, "Test note retime");
                check(Vector3.Distance(visualRoot.Find("static-120").position, positions[4]) > 1, "Retiming intentionally updates static position");
                Undo(); check(Vector3.Distance(visualRoot.Find("static-120").position, positions[4]) < .001f, "Undo restores static note location");
                chart.notes = new NoteData[0]; Rebuild();
                check(Array.FindAll(visualRoot.GetComponentsInChildren<ChartEditorHandle>(), h => h.mode == ChartEditMode.Notes).Length == 0,
                    "Empty chart remains editable without stale notes");
                passed = true;
            }
            catch (Exception error) { report.AppendLine("FAIL " + error); Debug.LogException(error); }
            finally
            {
                if (chartCameraPreview) SetPreviewMode(false);
                SetPlaying(false); chart = JsonUtility.FromJson<ChartData>(snapshot); songTime = 0;
                selectedNote = -1; savedChartJson = savedBaseline; needsSaveAs = savedNeedsSaveAs;
                undo.Clear(); redo.Clear(); Rebuild(); SetupAudio();
            }
            report.AppendLine("RESULT=" + (passed ? "PASS" : "FAIL") + " CHECKS=" + checks);
            File.WriteAllText(Path.Combine(smokeDirectory, "static-note-checks.txt"), report.ToString());
            Debug.Log("CHART_STUDIO_STATIC_NOTES " + (passed ? "PASS" : "FAIL") + " " + checks); return passed;
        }
    }
}
