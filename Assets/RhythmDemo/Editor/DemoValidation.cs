using System;
using UnityEngine;

namespace GeometryRhythm.Editor
{
    /// <summary>Focused integration/rule checks run in the real Unity editor, without a test-package dependency.</summary>
    public static class DemoValidation
    {
        static int checks;
        static void Check(bool value,string message)
        { checks++;if(!value) throw new Exception("Validation failed: "+message); }
        static JudgementEngine Engine(params NoteData[] notes)
        { return new JudgementEngine(new ChartData{notes=notes,ticksPerBeat=480},new TempoMap(new[]{new TempoData{tick=0,bpm=120}},480)); }
        static NoteData Note(string id,string action,bool sleeve=false,int tick=960)
        { return new NoteData{id=id,pathId="p0",action=action,protectedNote=sleeve,tick=tick}; }
        public static void Run()
        {
            checks=0;
            var chart=ChartLoader.Parse(Resources.Load<TextAsset>("Charts/geometry-demo").text);
            var tempo=new TempoMap(chart.tempos,chart.ticksPerBeat);
            Check(chart.notes.Length>150,"nonempty example chart");
            Check(Math.Abs(tempo.SecondsAtBeat(chart.endBeat)-64)<.000001,"song length");
            var change=new TempoMap(new[]{new TempoData{tick=0,bpm=120},new TempoData{tick=1920,bpm=60}},480);
            Check(Math.Abs(change.SecondsAtBeat(8)-6)<.000001,"tempo change");
            Check(Math.Abs(change.BeatAtSeconds(6)-8)<.000001,"tempo inverse");
            var e=Engine(Note("local","tap"));
            Check(e.Tap(1,n=>false)==null,"local tap rejects empty screen");
            Check(e.Tap(.7,n=>true)==null,"tap rejects early time");
            Check(e.Tap(1,n=>true)!=null&&e.Perfects==1,"local tap success");
            Check(e.Tap(1,n=>true)==null&&e.Judged==1,"no double scoring");
            e=Engine(Note("global","tap",true));
            Check(e.Tap(1,n=>false)!=null,"protected tap anywhere");
            e=Engine(Note("global","tap",true),Note("local","tap"));
            Check(e.Tap(1,n=>true).Data.id=="local"&&e.Judged==1,"one press, local priority");
            Check(e.Tap(1,n=>false).Data.id=="global","second press protected");
            e=Engine(Note("drag","drag"));
            e.Drag(.98,n=>true,true);Check(e.Judged==0,"early drag must remain");
            e.Drag(1,n=>false,true);Check(e.Judged==0,"local drag needs overlap");
            e.Drag(1,n=>true,false);Check(e.Judged==0,"drag needs contact");
            e.Drag(1,n=>true,true);Check(e.Perfects==1,"local drag success");
            e=Engine(Note("global","drag",true));e.Drag(1,n=>false,true);
            Check(e.Perfects==1,"protected drag anywhere while held");
            e=Engine(Note("good","tap"));e.Tap(1.10,n=>true);
            Check(e.Goods==1&&Math.Abs(e.Accuracy-65)<.001,"Good weighting");
            e=Engine(Note("miss","tap"));e.Advance(1.2,false);
            Check(e.Misses==1&&e.Combo==0,"late miss");
            e=Engine(Note("a","tap"),Note("b","drag",true,1920));e.Reset(1.5);
            Check(e.Notes[0].Result==NoteResult.Skipped&&e.Eligible==1,"seek excludes old notes");
            e.Advance(5,true);Check(e.Score==1000000&&e.Misses==0,"practice score denominator");
            e=new JudgementEngine(chart,tempo);e.Advance(64,true);
            Check(e.Perfects==chart.notes.Length&&e.Score==1000000,"autoplay catches all crossed events");
            var spatial=new SpatialDirector(chart,tempo);
            Check(spatial.Section(8).placements.Length==4&&spatial.Section(16).placements.Length==2&&
                spatial.Section(23).placements.Length==1&&spatial.Section(44).placements.Length==8,"variable path count");
            var n0=e.Notes[0];spatial.NotePose(n0,n0.HitTime,out var hit,out var rot);
            Check(Vector3.Distance(hit,spatial.Point(n0.Data.pathId,SpatialDirector.NearDepth,n0.HitTime))<.00001f,"arrival equals judgement point");
            var pos=spatial.Point("p0",30,34);spatial.Point("p0",50,2);
            Check(pos==spatial.Point("p0",30,34),"seek-independent spatial evaluation");
            var cameraObject=new GameObject("Projection test",typeof(Camera));var cam=cameraObject.GetComponent<Camera>();
            cam.pixelRect=new Rect(0,0,1600,900);cam.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            // Check hit-time framing for every charted note, including the eight-path section.
            // Scene occlusion still needs visual QA; frustum membership alone cannot prove visibility.
            foreach(var note in e.Notes)
            {
                spatial.EvaluateCamera(cam,note.HitTime);spatial.NotePose(note,note.HitTime,out var p,out var rotation);
                Vector3 viewport=cam.WorldToViewportPoint(p);
                Check(viewport.z>cam.nearClipPlane && viewport.x>.035f && viewport.x<.965f && viewport.y>.075f && viewport.y<.89f,
                    "hit note outside play area: "+note.Data.id+" viewport="+viewport);
            }
            cam.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var disc=new GameObject("Disc");disc.transform.position=new Vector3(0,0,5);disc.transform.rotation=Quaternion.Euler(25,35,0);
            var polygon=new Vector2[24];Vector2 screen=cam.WorldToScreenPoint(disc.transform.position);
            Check(NoteProjection.Contains(cam,disc.transform,1,screen,polygon,10),"tilted disc centre");
            Check(!NoteProjection.Contains(cam,disc.transform,1,new Vector2(-500,-500),polygon,10),"outside projection");
            disc.transform.position=new Vector3(0,0,-5);
            Check(!NoteProjection.Contains(cam,disc.transform,1,screen,polygon,10),"behind camera rejected");
            UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(disc);
            string json=Resources.Load<TextAsset>("Charts/geometry-demo").text;
            bool invalid=false;try{ChartLoader.Parse(json.Replace("\"version\": 1","\"version\": 99"));}catch(FormatException){invalid=true;}
            Check(invalid,"schema version rejected");
            invalid=false;try{ChartLoader.Parse(json.Replace("\"action\": \"tap\"","\"action\": \"flick\""));}catch(FormatException){invalid=true;}
            Check(invalid,"unknown action rejected");
            var entry=SongEntry.Parse(Resources.Load<TextAsset>("Charts/geometry-demo"));
            Check(entry.MaxPaths==8 && Math.Abs(entry.Duration-64)<.001,"song library derives chart metadata");
            Check(entry.ShortTitle=="First Light" && entry.Bpm=="120","song display metadata");
            e=new JudgementEngine(chart,tempo);e.Advance(64,true);
            var autoResult=new PlayResult(e,true,false);
            Check(autoResult.Automatic && autoResult.Score==1000000 && autoResult.Grade=="SSS","autoplay result marked preview");
            e.Reset(0);
            Check(autoResult.Perfect==chart.notes.Length && autoResult.Score==1000000,"results are immutable snapshots");
            e.Advance(65,false);var missedResult=new PlayResult(e,false,false);
            Check(missedResult.Miss==chart.notes.Length && missedResult.Accuracy==0 && missedResult.Grade=="D","manual misses shown honestly");
            Check(new PlayResult(e,false,true).Practice,"practice result explicitly marked");
            Debug.Log("GEOMETRY_VALIDATION_PASS "+checks+" checks");
        }
    }
}
