using System;
using UnityEngine;
using UnityEditor;

namespace GeometryRhythm.Editor
{
    /// <summary>Import gate for charts converted from other rhythm games (osu!mania, Malody).
    /// It runs the real gameplay loader over every chart in the Resources catalogue, so a
    /// conversion that would silently disappear from the song list fails loudly here instead.
    /// Conversion itself lives in <c>BGA/tools/import_vsrg_chart.py</c>.</summary>
    public static class ChartImportValidation
    {
        static int checks;
        static void Check(bool value,string message)
        { checks++;if(!value) throw new Exception("Chart import validation failed: "+message); }

        /// <summary>Validates and lets an uncaught failure escape (menu item / live editor).</summary>
        public static void Run()
        {
            checks=0;
            var assets=Resources.LoadAll<TextAsset>("Charts");
            Check(assets.Length>0,"the chart catalogue is not empty");
            var cameraObject=new GameObject("Chart import projection test",typeof(Camera));
            var camera=cameraObject.GetComponent<Camera>();
            var viewportTexture=new RenderTexture(1600,900,0);
            camera.targetTexture=viewportTexture;camera.rect=new Rect(0,0,1,1);
            try
            {
                Check(camera.pixelWidth==1600&&camera.pixelHeight==900,"projection uses a real 16:9 viewport");
                foreach(var asset in assets)
                {
                    // The same call the player and the frontend catalogue make. Invalid JSON,
                    // a bad stage route or an out-of-range camera key throws right here.
                    var chart=ChartLoader.Parse(asset.text);
                    Check(chart.notes.Length>0,asset.name+" has notes");
                    var tempo=new TempoMap(chart.tempos,chart.ticksPerBeat);
                    double duration=tempo.SecondsAtBeat(chart.endBeat);
                    Check(duration>1,asset.name+" has a usable duration");
                    if(!string.IsNullOrEmpty(chart.audioResource))
                        Check(Resources.Load<AudioClip>(chart.audioResource)!=null,
                            "missing audio resource '"+chart.audioResource+"' for "+asset.name);
                    // A cover path is addressed without its extension; a typo must fail here
                    // instead of silently falling back to the procedural artwork.
                    if(!string.IsNullOrEmpty(chart.coverResource))
                        Check(Resources.Load<Texture2D>(chart.coverResource)!=null,
                            "missing cover resource '"+chart.coverResource+"' for "+asset.name);
                    // Autoplay crosses every note exactly once: a broken tail, an endBeat that
                    // cuts the chart short or a note after the end all surface as a miss.
                    var engine=new JudgementEngine(chart,tempo);
                    engine.Advance(duration,true);
                    Check(engine.Judged==chart.notes.Length&&engine.Perfects==chart.notes.Length&&
                        engine.Misses==0&&engine.Score==1000000,asset.name+" does not autoplay cleanly");
                    // Every note must reach a readable spot on screen at its own hit time.
                    var spatial=new SpatialDirector(chart,tempo);
                    foreach(var note in engine.Notes)
                    {
                        spatial.EvaluateCamera(camera,note.HitTime);
                        spatial.NotePose(note,note.HitTime,out var position,out _);
                        Vector3 viewport=camera.WorldToViewportPoint(position);
                        Check(viewport.z>camera.nearClipPlane&&viewport.x>.035f&&viewport.x<.965f&&
                            viewport.y>.075f&&viewport.y<.89f,
                            "hit note outside the play area: "+asset.name+" / "+note.Data.id+
                            " viewport="+viewport.ToString("F3"));
                    }
                    // Reading speed is presentation only: the stored chart never changes and
                    // every note must still be judged at the same world point, while a chart
                    // without a video space scales its authored approach window instead.
                    var recommended=new SpatialDirector(chart,tempo,NoteScrollSettings.Default);
                    var faster=new SpatialDirector(chart,tempo,NoteScrollSettings.Default*2);
                    Check(faster.NoteSpeedMultiplier>recommended.NoteSpeedMultiplier,
                        asset.name+" stores the requested reading speed");
                    if(recommended.VideoSpace==null)
                        Check(Math.Abs(recommended.ApproachSeconds-chart.approachSeconds)<.001f &&
                            Math.Abs(recommended.ApproachSeconds-faster.ApproachSeconds*2)<.001f,
                            asset.name+" keeps its authored approach window at the recommended speed and scales from there");
                    foreach(var note in engine.Notes)
                    {
                        recommended.NotePose(note,note.HitTime,out var recommendedHit,out _);
                        faster.NotePose(note,note.HitTime,out var fasterHit,out _);
                        Check(Vector3.Distance(recommendedHit,fasterHit)<.0001f,
                            asset.name+" hit anchor must not move with the reading speed: "+note.Data.id);
                    }
                    // The frontend derives the song list from the same chart.
                    var entry=SongEntry.Parse(asset);
                    Check(entry.Duration>0&&entry.MaxPaths>0&&!string.IsNullOrEmpty(entry.Title),
                        asset.name+" does not produce a song entry");
                    Check(chart.notes.Length==engine.Notes.Length,asset.name+" note count is stable");
                    Debug.Log("Chart import OK: "+asset.name+" · "+chart.title+" · "+chart.notes.Length+
                        " notes · "+entry.Bpm+" BPM · "+duration.ToString("F2")+" s · "+
                        entry.MaxPaths+" paths · "+(string.IsNullOrEmpty(chart.audioResource)?"no audio":chart.audioResource)+
                        " · "+(entry.Cover==null?"procedural cover":chart.coverResource));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(viewportTexture); }
            Debug.Log("GEOMETRY_CHART_IMPORT_PASS "+checks+" checks over "+assets.Length+" charts");
        }

        /// <summary>Batch entry point; a failure must fail the process, not only the log.</summary>
        public static void RunBatch()
        {
            try { Run(); }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
            EditorApplication.Exit(0);
        }
    }
}
