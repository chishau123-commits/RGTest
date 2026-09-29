using System;
using UnityEngine;

namespace GeometryRhythm.Editor
{
    /// <summary>Spawn distance must change visibility, never note motion or judgement.</summary>
    public static class NoteSpawnValidation
    {
        static int checks;
        static void Check(bool value,string message)
        { checks++;if(!value) throw new Exception("Note spawn validation failed: "+message); }

        public static void Run()
        {
            checks=0;
            foreach(var asset in Resources.LoadAll<TextAsset>("Charts"))
                VerifyChart(ChartLoader.Parse(asset.text));

            // Exercise the inverse distance gate on an accelerating video rail as well
            // as the fixed approach windows of the current song catalogue.
            var video=ChartLoader.Parse(Resources.Load<TextAsset>("Charts/geometry-demo").text);
            int endTick=Mathf.RoundToInt(video.endBeat*video.ticksPerBeat);
            video.videoSpace=new VideoChartSpaceData {
                schemaVersion=2,
                cameraZKeys=new[]{new CameraZKey{tick=0,z=0,easing="easeInOut"},new CameraZKey{tick=endTick,z=2000}}
            };
            for(int i=0;i<video.paths.Length;i++)
                video.paths[i].screenAnchors=new[]{
                    new ScreenAnchorData{tick=0,xPercent=30+i*4,yPercent=70},
                    new ScreenAnchorData{tick=endTick,xPercent=35+i*4,yPercent=65}};
            VerifyChart(video);

            // Do not leave validation's personal settings on the editor machine.
            bool existed=PlayerPrefs.HasKey(NoteSpawnSettings.PreferenceKey);
            float original=PlayerPrefs.GetFloat(NoteSpawnSettings.PreferenceKey);
            try
            {
                NoteSpawnSettings.Save(60);
                Check(NoteSpawnSettings.Load()==60,"position survives preference reload");
                NoteSpawnSettings.Save(-10);
                Check(NoteSpawnSettings.Load()==NoteSpawnSettings.Minimum,"lower limit");
                NoteSpawnSettings.Save(110);
                Check(NoteSpawnSettings.Load()==NoteSpawnSettings.Maximum,"upper limit");
                Check(NoteSpawnSettings.Sanitize(float.NaN)==NoteSpawnSettings.Default&&
                    NoteSpawnSettings.Sanitize(float.PositiveInfinity)==NoteSpawnSettings.Default,"invalid preference falls back");
            }
            finally
            {
                if(existed) PlayerPrefs.SetFloat(NoteSpawnSettings.PreferenceKey,original);
                else PlayerPrefs.DeleteKey(NoteSpawnSettings.PreferenceKey);
                PlayerPrefs.Save();
            }
            Debug.Log("GEOMETRY_NOTE_SPAWN_PASS "+checks+" checks");
        }

        static void VerifyChart(ChartData chart)
        {
            var tempo=new TempoMap(chart.tempos,chart.ticksPerBeat);
            var engine=new JudgementEngine(chart,tempo);
            var note=engine.Notes[engine.Notes.Length/2];
            foreach(float speed in new[]{8f,48f})
            {
                var spatial=new SpatialDirector(chart,tempo,speed);
                Check(spatial.NoteSpawnDepth==SpatialDirector.FarDepth,"default preserves the full path");
                double sampleTime=note.HitTime-.04;
                spatial.NotePose(note,sampleTime,out var originalPosition,out var originalRotation);
                spatial.JudgementPose(note.Data.pathId,note.HitTime,out var originalTarget,out var originalTargetRotation);
                float approach=spatial.ApproachSeconds;
                float depth=spatial.Depth(note.HitTime,sampleTime);
                foreach(float percent in new[]{20f,60f,100f})
                {
                    spatial.SetNoteSpawnPosition(percent);
                    double entry=spatial.VideoSpace==null ? note.HitTime-approach*percent/100 :
                        spatial.VideoSpace.TimeAtCameraZ(spatial.VideoSpace.CameraZ(note.HitTime)-
                            (spatial.NoteSpawnDepth-SpatialDirector.NearDepth)/speed);
                    Check(!spatial.NoteInView(note.HitTime,entry-.002),"hidden before the new endpoint");
                    Check(spatial.NoteInView(note.HitTime,entry+.002),"visible after crossing the new endpoint");
                    spatial.NotePose(note,entry,out var entering,out _);
                    Check(Vector3.Distance(entering,spatial.Point(note.Data.pathId,spatial.VisiblePathFarDepth,entry))<.02f,
                        "note enters at the rendered path endpoint");
                    double lookahead=spatial.NoteLookaheadSeconds(sampleTime);
                    Check(spatial.NoteInView(sampleTime+lookahead-.002,sampleTime)&&
                        !spatial.NoteInView(sampleTime+lookahead+.002,sampleTime),"lookahead matches the actual gate");
                    spatial.NotePose(note,sampleTime,out var position,out var rotation);
                    Check(Vector3.Distance(originalPosition,position)<.0001f&&Quaternion.Angle(originalRotation,rotation)<.01f,
                        "a visible note does not jump when spawn changes");
                    Check(spatial.Depth(note.HitTime,sampleTime)==depth&&spatial.ApproachSeconds==approach&&spatial.NoteSpeedMultiplier==speed,
                        "flight speed and approach clock are unchanged");
                    spatial.JudgementPose(note.Data.pathId,note.HitTime,out var target,out var targetRotation);
                    spatial.NotePose(note,note.HitTime,out var hitPosition,out _);
                    Check(Vector3.Distance(target,originalTarget)<.0001f&&Quaternion.Angle(targetRotation,originalTargetRotation)<.01f&&
                        Vector3.Distance(hitPosition,target)<.0001f,"note still meets its original target at hit time");
                    Check(spatial.NoteInView(note.HitTime,note.HitTime+JudgementEngine.GoodWindow,JudgementEngine.GoodWindow)&&
                        !spatial.NoteInView(note.HitTime,note.HitTime+JudgementEngine.GoodWindow+.002,JudgementEngine.GoodWindow),
                        "late judgement window is preserved");
                }
            }
        }
    }
}
