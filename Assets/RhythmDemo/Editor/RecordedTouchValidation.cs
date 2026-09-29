using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GeometryRhythm.Editor
{
    /// <summary>Replays real unmatched presses, including the visible note poses and prior results.</summary>
    public static class RecordedTouchValidation
    {
        [Serializable] sealed class Recording { public Sample[] samples; }
        [Serializable] sealed class Sample
        {
            public double time;
            public float x,y,speed,spawn;
            public int width,height;
            public string expected;
            public NoteState[] states;
        }
        [Serializable] sealed class NoteState { public string id,state; }
        const string Fixture="Assets/RhythmDemo/Editor/Fixtures/koi-touch-20260927.json";

        public static void RunBatch()
        {
            try { Run();EditorApplication.Exit(0); }
            catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            var samples=JsonUtility.FromJson<Recording>(File.ReadAllText(Fixture)).samples;
            var chart=ChartLoader.Parse(Resources.Load<TextAsset>("Charts/koi-kou-enishi-reg3-4k").text);
            var tempo=new TempoMap(chart.tempos,chart.ticksPerBeat);
            var engine=new JudgementEngine(chart,tempo);
            var notes=new Dictionary<string,RuntimeNote>();
            foreach(var note in engine.Notes) notes.Add(note.Data.id,note);
            var root=new GameObject("Recorded touch replay");
            var cameraObject=new GameObject("Recorded touch camera",typeof(Camera));
            var camera=cameraObject.GetComponent<Camera>();
            var texture=new RenderTexture(3048,2032,0);camera.targetTexture=texture;
            var controller=root.AddComponent<RhythmDemoController>();controller.enabled=false;controller.demoCamera=camera;
            typeof(RhythmDemoController).GetProperty("Chart").SetValue(controller,chart);
            typeof(RhythmDemoController).GetProperty("Engine").SetValue(controller,engine);
            var inside=typeof(RhythmDemoController).GetMethod("Inside",BindingFlags.Instance|BindingFlags.NonPublic);
            var timeField=typeof(RhythmDemoController).GetField("visualTime",BindingFlags.Instance|BindingFlags.NonPublic);
            var library=new VisualLibrary();var views=new List<NoteVisual>();
            int failed=0,checks=0;
            try
            {
                foreach(var sample in samples)
                {
                    engine.Reset(0);
                    var spatial=new SpatialDirector(chart,tempo,sample.speed);spatial.SetNoteSpawnPosition(sample.spawn);
                    typeof(RhythmDemoController).GetProperty("Spatial").SetValue(controller,spatial);
                    timeField.SetValue(controller,sample.time);spatial.EvaluateCamera(camera,sample.time);
                    foreach(var note in engine.Notes) { note.View=null;note.Result=NoteResult.Skipped; }
                    foreach(var state in sample.states)
                        notes[state.id].Result=state.state=="Resolved"?NoteResult.Perfect:(NoteResult)Enum.Parse(typeof(NoteResult),state.state);
                    int index=0;
                    foreach(var note in engine.Notes)
                    {
                        if(note.Result!=NoteResult.Pending || !spatial.NoteInView(note.HitTime,sample.time,JudgementEngine.GoodWindow)) continue;
                        if(index==views.Count) views.Add(new NoteVisual(root.transform,library));
                        var view=views[index++];view.Bind(note.Data);note.View=view;
                        spatial.NotePose(note,sample.time,out var p,out var r);view.Transform.SetPositionAndRotation(p,r);
                    }
                    var point=new Vector2(sample.x,sample.y);
                    var hit=engine.Tap(sample.time,n=>(bool)inside.Invoke(controller,new object[]{n,point}));
                    checks++;
                    if(hit==null||hit.Data.id!=sample.expected)
                    {
                        failed++;
                        if(failed<=4) Debug.Log("RECORDED_TOUCH_REJECT expected="+sample.expected+" actual="+(hit?.Data.id??"none")+
                            " t="+sample.time.ToString("F4")+" point="+point);
                    }
                    // More forgiving contact must still own only one lane. Leave only
                    // this note pending, then press each other lane's actual target.
                    foreach(var note in engine.Notes) { note.Result=NoteResult.Skipped;note.View=null; }
                    var expected=notes[sample.expected];
                    foreach(var path in chart.paths)
                    {
                        if(path.id==expected.Data.pathId) continue;
                        expected.Result=NoteResult.Pending;
                        Vector2 other=camera.WorldToScreenPoint(spatial.Point(path.id,SpatialDirector.NearDepth,sample.time));
                        checks++;
                        if(engine.Tap(sample.time,n=>(bool)inside.Invoke(controller,new object[]{n,other}))!=null)
                            throw new Exception("Recorded touch target steals neighbouring lane "+path.id);
                    }
                    expected.Result=NoteResult.Pending;checks++;
                    if(engine.Tap(expected.HitTime+JudgementEngine.GoodWindow+.001,n=>(bool)inside.Invoke(controller,new object[]{n,point}))!=null)
                        throw new Exception("Recorded touch outside the timing window was accepted");
                }
                if(failed>0) throw new Exception("RECORDED_TOUCH_REPLAY_FAILED "+failed+"/"+samples.Length+" on-time nearby presses rejected");
                Debug.Log("RECORDED_TOUCH_VALIDATION_PASS "+checks+" checks over "+samples.Length+" real presses");
            }
            finally
            {
                foreach(var field in typeof(VisualLibrary).GetFields())
                    if(field.GetValue(library) is UnityEngine.Object asset) UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
