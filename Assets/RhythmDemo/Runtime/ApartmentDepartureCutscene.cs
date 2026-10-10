using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    // Shutdown snap, black intertitle and descending footsteps, then the existing exterior entrance.
    public sealed class ApartmentDepartureCutscene : MonoBehaviour
    {
        public const float Duration=7f;
        readonly List<AudioListener> previousListeners=new List<AudioListener>();
        AudioSource source;
        AudioClip snap,step;
        RectTransform glow;
        Image glowImage;
        Text title;
        Action completed;
        float elapsed;
        int steps;
        bool snapped,finished;
        public int CueCount=>steps+(snapped?1:0);
        public float Elapsed=>elapsed;
        public void Initialize(Action onComplete)
        {
            completed=onComplete;
            glow=Rect("CRT screen collapse",0,0,1600,900);
            glowImage=glow.gameObject.AddComponent<Image>();glowImage.raycastTarget=false;
            title=Rect("Downstairs intertitle",400,400,800,100).gameObject.AddComponent<Text>();
            title.font=CyberTheme.Chinese;title.fontSize=32;title.alignment=TextAnchor.MiddleCenter;
            title.text="下楼中......";title.color=Color.clear;title.raycastTarget=false;
            source=gameObject.AddComponent<AudioSource>();source.spatialBlend=0;source.volume=.7f;
            foreach(var listener in FindObjectsOfType<AudioListener>())if(listener.enabled){previousListeners.Add(listener);listener.enabled=false;}
            gameObject.AddComponent<AudioListener>();
            snap=DepartureFoley.Create("pia");step=DepartureFoley.Create("step");
            Draw();
        }
        RectTransform Rect(string name,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(transform,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        void Update(){Tick(Time.unscaledDeltaTime);}
        public void Tick(float dt)
        {
            if(finished)return;elapsed+=dt;
            if(!snapped&&elapsed>=.12f){snapped=true;source.PlayOneShot(snap);}
            while(steps<9&&elapsed>=1.4f+steps*.43f)
            {
                source.pitch=steps%2==0?.88f:1.02f;
                source.PlayOneShot(step,Mathf.Lerp(.85f,.42f,steps/8f));steps++;
            }
            Draw();
            if(elapsed>=Duration){finished=true;ReleaseAudio();completed?.Invoke();}
        }
        void Draw()
        {
            float collapse=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.28f));
            float h=Mathf.Lerp(900,2,collapse),w=1600*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-.28f)/.3f)));
            glow.sizeDelta=new Vector2(w,h);glow.anchoredPosition=new Vector2((1600-w)/2,-(900-h)/2);
            glowImage.color=new Color(.72f,.83f,.93f,1-Mathf.Clamp01((elapsed-.48f)/.25f));
            float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-.95f)/.9f))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-5.1f)/1.4f)));
            title.color=new Color(.87f,.84f,.91f,alpha);
        }
        void ReleaseAudio()
        {
            if(source!=null)source.Stop();var own=GetComponent<AudioListener>();if(own!=null)own.enabled=false;
            foreach(var listener in previousListeners)if(listener!=null)listener.enabled=true;previousListeners.Clear();
        }
        void OnDestroy(){ReleaseAudio();if(snap!=null)Destroy(snap);if(step!=null)Destroy(step);}
    }
}
