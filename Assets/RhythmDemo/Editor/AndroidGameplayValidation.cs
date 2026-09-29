using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GeometryRhythm.Editor
{
    /// <summary>Replays single presses against the controller's real spatial hit test.</summary>
    public static class AndroidGameplayValidation
    {
        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch(Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            var chart=ChartLoader.Parse(Resources.Load<TextAsset>("Charts/only-my-railgun-jack-4k").text);
            var tempo=new TempoMap(chart.tempos,chart.ticksPerBeat);
            var note=Array.Find(chart.notes,n=>n.action=="tap"&&!n.protectedNote&&n.tick>1920);
            chart.notes=new[]{note};
            var root=new GameObject("Single finger regression");
            var cameraObject=new GameObject("Test camera",typeof(Camera));
            var camera=cameraObject.GetComponent<Camera>();
            // Batch-mode Game view is 640x480 and clamps pixelRect. A target texture
            // provides the real device viewport even without a graphics device.
            var viewportTexture=new RenderTexture(3048,2032,0);
            camera.targetTexture=viewportTexture;camera.rect=new Rect(0,0,1,1);
            var controller=root.AddComponent<RhythmDemoController>();controller.enabled=false;
            controller.demoCamera=camera;
            typeof(RhythmDemoController).GetProperty("Chart").SetValue(controller,chart);
            var engine=new JudgementEngine(chart,tempo);
            typeof(RhythmDemoController).GetProperty("Engine").SetValue(controller,engine);
            var inside=typeof(RhythmDemoController).GetMethod("Inside",BindingFlags.Instance|BindingFlags.NonPublic);
            var library=new VisualLibrary();
            var view=new NoteVisual(root.transform,library);view.Bind(note);
            int checks=0,failures=0;
            try
            {
                foreach(float speed in new[]{8f,24f,47f})
                {
                    var spatial=new SpatialDirector(chart,tempo,speed);
                    typeof(RhythmDemoController).GetProperty("Spatial").SetValue(controller,spatial);
                    foreach(double error in new[]{-.100,-.060,0,.060,.100})
                    {
                        var n=engine.Notes[0];engine.Reset(0);n.View=view;
                        double time=n.HitTime+error;
                        typeof(RhythmDemoController).GetField("visualTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,time);
                        spatial.EvaluateCamera(camera,time);
                        spatial.NotePose(n,time,out var position,out var rotation);
                        view.Transform.SetPositionAndRotation(position,rotation);
                        Vector2 point=camera.WorldToScreenPoint(spatial.Point(note.pathId,SpatialDirector.NearDepth,time));
                        bool hit=engine.Tap(time,candidate=>(bool)inside.Invoke(controller,new object[]{candidate,point}))!=null;
                        checks++;
                        if(!hit) { failures++;Debug.LogError("ANDROID_TOUCH_REPRO speed="+speed+" errorMs="+(error*1000)+" expected=hit actual=miss"); }
                    }
                }
                // No renderer dependency: a high-speed note can already be behind the camera.
                var target=engine.Notes[0];engine.Reset(0);target.View=null;
                double hitTime=target.HitTime;
                var hitSpatial=new SpatialDirector(chart,tempo,47);
                typeof(RhythmDemoController).GetProperty("Spatial").SetValue(controller,hitSpatial);
                hitSpatial.EvaluateCamera(camera,hitTime);
                typeof(RhythmDemoController).GetField("visualTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,hitTime);
                Vector2 centre=camera.WorldToScreenPoint(hitSpatial.Point(note.pathId,SpatialDirector.NearDepth,hitTime));
                Func<RuntimeNote,bool> atCentre=n=>(bool)inside.Invoke(controller,new object[]{n,centre});
                Check(engine.Tap(hitTime+.1,atCentre)!=null,"late target survives a released visual",ref checks);
                engine.Reset(0);
                Check(engine.Tap(hitTime+.121,atCentre)==null,"late time outside Good still rejects",ref checks);
                Check(engine.Tap(hitTime-.121,atCentre)==null,"early time outside Good still rejects",ref checks);
                Check(engine.Tap(hitTime,n=>(bool)inside.Invoke(controller,new object[]{n,new Vector2(-500,-500)}))==null,"local note still rejects empty screen",ref checks);
                // Replay consecutive controller frames. Previously the first frame expired
                // the note on raw song time before the corrected press on the next frame.
                var downs=(List<Vector2>)typeof(RhythmDemoController).GetField("downs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
                var judge=typeof(RhythmDemoController).GetMethod("JudgePointers",BindingFlags.Instance|BindingFlags.NonPublic);
                controller.autoPlay=false;controller.inputOffsetMilliseconds=-80;engine.Reset(0);
                judge.Invoke(controller,new object[]{hitTime+.16});
                Check(target.Result==NoteResult.Pending,"negative offset keeps late window open between presses",ref checks);
                downs.Add(centre);judge.Invoke(controller,new object[]{hitTime+.18});downs.Clear();
                Check(target.Result==NoteResult.Good,"corrected single press at +100 ms is Good",ref checks);
                engine.Reset(0);judge.Invoke(controller,new object[]{hitTime+.201});
                Check(target.Result==NoteResult.Miss,"corrected timeout still expires",ref checks);
                controller.autoPlay=true;engine.Reset(0);judge.Invoke(controller,new object[]{hitTime});
                Check(target.Result==NoteResult.Perfect,"autoplay ignores personal input offset",ref checks);
                // A real tablet press 150 px below the centre at 3048x2032 was
                // delivered on time but rejected by the very tight spatial target.
                // Exercise the controller at different viewport sizes and speeds;
                // the usable margin must follow the viewport, not editor Screen size.
                foreach(var viewport in new[]{new Vector2(3048,2032),new Vector2(1920,1080),new Vector2(1280,720)})
                foreach(float speed in new[]{8f,24f,47f})
                foreach(double error in new[]{-.100,0,.100})
                {
                    if(viewportTexture.width!=(int)viewport.x || viewportTexture.height!=(int)viewport.y)
                    {
                        camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(viewportTexture);
                        viewportTexture=new RenderTexture((int)viewport.x,(int)viewport.y,0);
                        camera.targetTexture=viewportTexture;
                    }
                    Check(camera.pixelWidth==(int)viewport.x&&camera.pixelHeight==(int)viewport.y,
                        "fixture uses the requested device viewport",ref checks);
                    var spatial=new SpatialDirector(chart,tempo,speed);
                    typeof(RhythmDemoController).GetProperty("Spatial").SetValue(controller,spatial);
                    double time=hitTime+error;engine.Reset(0);target.View=null;
                    spatial.EvaluateCamera(camera,time);
                    typeof(RhythmDemoController).GetField("visualTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,time);
                    Vector2 point=camera.WorldToScreenPoint(spatial.Point(note.pathId,SpatialDirector.NearDepth,time));
                    point+=Vector2.down*(150f/2032*Mathf.Min(viewport.x,viewport.y));
                    Check(engine.Tap(time,n=>(bool)inside.Invoke(controller,new object[]{n,point}))!=null,
                        "finger margin at "+viewport+" speed="+speed+" error="+error,ref checks);
                    engine.Reset(0);
                    var other=Array.Find(chart.paths,p=>p.id!=note.pathId);
                    Vector2 neighbour=camera.WorldToScreenPoint(spatial.Point(other.id,SpatialDirector.NearDepth,time));
                    Check(engine.Tap(time,n=>(bool)inside.Invoke(controller,new object[]{n,neighbour}))==null,
                        "finger margin cannot hit the neighbouring lane",ref checks);
                }
                // Real on-device presses below the visible targets. Each has an
                // eligible note on the intended lane; these were spatial rejects.
                var capturedChart=ChartLoader.Parse(Resources.Load<TextAsset>("Charts/only-my-railgun-jack-4k").text);
                // These absolute screen coordinates were recorded before the imported
                // lanes were narrowed. Replay with that recording's original layout;
                // the viewport/speed checks above exercise the current shipped layout.
                var recordedLaneX=new[]{-11f,-4f,4f,11f};
                foreach(var placement in capturedChart.sections[0].placements)
                    placement.x=recordedLaneX[Array.FindIndex(capturedChart.paths,p=>p.id==placement.pathId)];
                var capturedEngine=new JudgementEngine(capturedChart,tempo);
                var capturedSpatial=new SpatialDirector(capturedChart,tempo,45);
                typeof(RhythmDemoController).GetProperty("Chart").SetValue(controller,capturedChart);
                typeof(RhythmDemoController).GetProperty("Engine").SetValue(controller,capturedEngine);
                typeof(RhythmDemoController).GetProperty("Spatial").SetValue(controller,capturedSpatial);
                camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(viewportTexture);
                viewportTexture=new RenderTexture(3048,2032,0);camera.targetTexture=viewportTexture;
                var recorded=new[]{new Vector3(1114.8f,431.5996f,18.3653f),new Vector3(1150.5f,434.9004f,18.5573f),new Vector3(2033.8f,375.2002f,18.3973f),
                    new Vector3(2059.8f,315.0996f,17.0640f),new Vector3(2042.6f,315.7002f,17.2027f)};
                var expected=new[]{"n000127","n000131","n000129","n000107","n000107"};
                for(int i=0;i<recorded.Length;i++)
                {
                    var sample=recorded[i];capturedEngine.Reset(0);capturedSpatial.EvaluateCamera(camera,sample.z);
                    typeof(RhythmDemoController).GetField("visualTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,(double)sample.z);
                    var hit=capturedEngine.Tap(sample.z,n=>(bool)inside.Invoke(controller,new object[]{n,new Vector2(sample.x,sample.y)}));
                    Check(hit!=null&&hit.Data.id==expected[i],"recorded finger press "+expected[i]+" reaches the intended note",ref checks);
                    capturedEngine.Reset(0);
                    Check(capturedEngine.Tap(sample.z,n=>(bool)inside.Invoke(controller,new object[]{n,new Vector2(sample.x,30)}))==null,
                        "touch apron stays bounded above the bottom edge",ref checks);
                }
                if(failures>0) throw new Exception("Single-finger judgement-plane presses missed "+failures+"/"+checks+" within the published Good window");
                Debug.Log("ANDROID_GAMEPLAY_VALIDATION_PASS "+checks+" checks");
            }
            finally
            {
                // These native assets belong only to this edit-mode fixture.
                foreach(var field in typeof(VisualLibrary).GetFields())
                    if(field.GetValue(library) is UnityEngine.Object asset) UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(viewportTexture);
            }
        }
        static void Check(bool value,string message,ref int checks)
        { checks++;if(!value) throw new Exception("Android gameplay validation failed: "+message); }
    }
}
