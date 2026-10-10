using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GeometryRhythm
{
    // Presentation only: callers retain their original input gates and completion callbacks.
    public static class DesktopMotion
    {
        // Unity Editor can return a managed wrapper for a missing native component.
        // Use Unity's component lookup, not C# ?? / ReferenceEquals null semantics.
        public static T GetOrAdd<T>(GameObject owner) where T:Component
        {
            if(owner.TryGetComponent<T>(out var component))return component;
            return owner.AddComponent<T>();
        }
        public static float Enter(float t) {t=Mathf.Clamp01(t);return 1-Mathf.Pow(1-t,4);}
        public static float Smooth(float t) {t=Mathf.Clamp01(t);return t*t*t*(t*(t*6-15)+10);}

        public static void Window(RectTransform root,CanvasGroup group,float started,float raw,bool closing,
            bool fromAnchor,Vector2 position,Vector2 size,Vector2 anchor,Vector2 anchorSize)
        {
            var motion=root.GetComponent<DesktopWindowMotion>();
            if(motion==null)motion=root.gameObject.AddComponent<DesktopWindowMotion>();
            motion.Draw(group,started,raw,closing,fromAnchor,position,size,anchor,anchorSize);
        }
    }

    public sealed class DesktopWindowMotion : MonoBehaviour
    {
        float stamp=float.MinValue,fromScale,fromAlpha;
        Vector2 fromPosition;
        bool initialized;
        public void Draw(CanvasGroup group,float started,float raw,bool closing,bool fromAnchor,
            Vector2 position,Vector2 size,Vector2 anchor,Vector2 anchorSize)
        {
            var rect=(RectTransform)transform;
            float small=fromAnchor?Mathf.Clamp(Mathf.Min(anchorSize.x/size.x,anchorSize.y/size.y),.065f,.28f):.965f;
            Vector2 half=new Vector2(size.x,-size.y)*.5f;
            Vector2 dock=fromAnchor?anchor+new Vector2(anchorSize.x,-anchorSize.y)*.5f-half*small:
                position+half*(1-small)+Vector2.down*18;
            if(stamp!=started)
            {
                if(!initialized&&!closing)
                {
                    fromScale=fromAnchor?small:.965f;
                    fromPosition=fromAnchor?dock:position+half*(1-fromScale)+Vector2.down*18;
                    fromAlpha=0;
                }
                else
                {
                    // Reversing mid-flight begins at the currently displayed pose.
                    fromPosition=rect.anchoredPosition;fromScale=rect.localScale.x;
                    fromAlpha=group!=null?group.alpha:1;
                }
                stamp=started;initialized=true;
            }
            float t=closing?DesktopMotion.Smooth(raw):DesktopMotion.Enter(raw);
            float scale=Mathf.Lerp(fromScale,closing?small:1,t);
            rect.sizeDelta=size; // Keep content layout intact; never animate its clipping rectangle.
            rect.localScale=Vector3.one*scale;
            rect.anchoredPosition=Vector2.Lerp(fromPosition,closing?dock:position,t);
            if(group!=null)
            {
                float fade=closing?DesktopMotion.Smooth(Mathf.Clamp01((raw-.14f)/.86f)):
                    DesktopMotion.Enter(Mathf.Clamp01(raw/.85f));
                group.alpha=Mathf.Lerp(fromAlpha,closing?0:1,fade);
            }
        }
    }

    // Event-driven feedback: icons are completely still when idle.
    public sealed class DesktopControlMotion : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,
        IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler
    {
        Selectable control;
        public System.Action HoverSound;
        bool hovered,pressed,selected;
        float scale=1,velocity;
        void Awake(){control=GetComponent<Selectable>();}
        void Update()
        {
            bool live=control!=null&&control.IsInteractable();
            float target=!live?1:pressed?.974f:(hovered||selected)?1.012f:1;
            if(Mathf.Abs(scale-target)<.0001f&&Mathf.Abs(velocity)<.001f)return;
            scale=Mathf.SmoothDamp(scale,target,ref velocity,pressed?.045f:.10f,10,Time.unscaledDeltaTime);
            if(Mathf.Abs(scale-target)<.0001f){scale=target;velocity=0;}
            transform.localScale=Vector3.one*scale;
        }
        void OnDisable(){hovered=pressed=selected=false;scale=1;velocity=0;transform.localScale=Vector3.one;}
        public void OnPointerEnter(PointerEventData e){hovered=true;if(control!=null&&control.IsInteractable())HoverSound?.Invoke();}
        public void OnPointerExit(PointerEventData e){hovered=false;pressed=false;}
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)pressed=true;}
        public void OnPointerUp(PointerEventData e){pressed=false;}
        public void OnSelect(BaseEventData e){selected=true;}
        public void OnDeselect(BaseEventData e){selected=false;pressed=false;}
    }

    [RequireComponent(typeof(RectTransform),typeof(CanvasGroup))]
    public sealed class DesktopEntranceMotion : MonoBehaviour
    {
        RectTransform rect;CanvasGroup group;
        Vector2 destination,offset;
        float started,duration;
        void Awake(){enabled=false;}
        public void Play(float delay,float seconds,float distance=14)
        {
            if(rect!=null)rect.anchoredPosition=destination;
            rect=(RectTransform)transform;destination=rect.anchoredPosition;offset=Vector2.down*distance;
            group=DesktopMotion.GetOrAdd<CanvasGroup>(gameObject);
            started=Time.unscaledTime+delay;duration=Mathf.Max(.001f,seconds);group.alpha=0;
            rect.anchoredPosition=destination+offset;enabled=true;
        }
        void Update()
        {
            if(rect==null||group==null){enabled=false;return;}
            float raw=Mathf.Clamp01((Time.unscaledTime-started)/duration),t=DesktopMotion.Enter(raw);
            rect.anchoredPosition=destination+offset*(1-t);group.alpha=t;
            if(raw>=1)enabled=false;
        }
    }

    public sealed class DesktopReflowMotion : MonoBehaviour
    {
        RectTransform rect;Vector2 from,target;float started;
        public void MoveTo(Vector2 destination)
        {
            if(rect==null)
            {
                rect=(RectTransform)transform;target=destination;rect.anchoredPosition=target;
                enabled=false;return;
            }
            if((destination-target).sqrMagnitude<.01f)return;
            from=rect.anchoredPosition;target=destination;started=Time.unscaledTime;enabled=true;
        }
        void Update()
        {
            float raw=Mathf.Clamp01((Time.unscaledTime-started)/.24f);
            rect.anchoredPosition=Vector2.Lerp(from,target,DesktopMotion.Smooth(raw));
            if(raw>=1)enabled=false;
        }
    }
}
