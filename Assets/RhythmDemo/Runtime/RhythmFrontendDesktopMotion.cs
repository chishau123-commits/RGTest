using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    public sealed partial class RhythmFrontendView
    {
        bool mainEntryActive;
        float loginExitProgress,loginFocus,loginFocusVelocity;
        Image loginFocusRail;

        void Reveal(Transform parent,string name,float delay,float distance=14)
        {
            var child=parent.Find(name) as RectTransform;
            if(child!=null)DesktopMotion.GetOrAdd<DesktopEntranceMotion>(child.gameObject).Play(delay,.58f,distance);
        }

        void BeginLoginEntrance()
        {
            loginExitProgress=0;loginFocus=0;loginFocusVelocity=0;
            Reveal(page,"Login product",.18f,12);
            Reveal(page,"Login edition",.24f,10);
            Reveal(page,"Login identity rule",.28f,8);
            Reveal(page,"Login prompt",.34f,10);
            Reveal(page,"Login prompt english",.40f,8);
            Reveal(page,"Login bottom dock",.38f,14);
            Reveal(page,"Login dock shadow",.38f,14);
            loginFocusRail=Box(titleLoginFieldRect,"Password focus rail",8,49,384,2,CyberTheme.Purple);
            loginFocusRail.rectTransform.localScale=new Vector3(0,1,1);
        }

        void BeginDesktopEntrance()
        {
            mainEntryActive=true;mainWindowGroup.alpha=0;
            Reveal(page,"Open diary",.10f,12);Reveal(page,"Open friends",.15f,12);
            Reveal(page,"Open news",.20f,12);Reveal(page,"Open music player",.25f,12);
            Reveal(mainWindowRoot,"Main stage viewport",.18f,16);
            Reveal(mainWindowRoot,"Mission overview",.24f,12);
            var overview=mainWindowRoot.Find("Mission overview");
            if(overview!=null)
            {
                Reveal(overview,"当前章节 status card",.28f,10);
                Reveal(overview,"在线好友 status card",.33f,10);
                Reveal(overview,"未读新闻 status card",.38f,10);
                Reveal(overview,"舞台入口 status card",.43f,10);
            }
        }

        void UpdateDesktopPresentation()
        {
            if(mainEntryActive&&mainWindowRoot!=null)
            {
                if(mainWindowMinimizing||mainWindowRestoring)mainEntryActive=false;
                else
                {
                    float raw=Mathf.Clamp01((Time.unscaledTime-pageStarted-.06f)/.68f);
                    DesktopMotion.Window(mainWindowRoot,mainWindowGroup,pageStarted+.06f,raw,false,false,
                        mainWindowPosition,new Vector2(1386,742),Vector2.zero,Vector2.one);
                    if(raw>=1)mainEntryActive=false;
                }
            }
            if(titlePasswordInput!=null&&loginFocusRail!=null)
            {
                loginFocus=Mathf.SmoothDamp(loginFocus,titlePasswordInput.isFocused?1:0,
                    ref loginFocusVelocity,.12f,10,Time.unscaledDeltaTime);
                loginFocusRail.rectTransform.localScale=new Vector3(Mathf.Lerp(.18f,1,loginFocus),1,1);
                loginFocusRail.color=CyberTheme.Alpha(titleLoginErrorUntil>Time.unscaledTime?CyberTheme.Pink:CyberTheme.Purple,loginFocus*.65f);
            }
            float follow=1-Mathf.Exp(-Time.unscaledDeltaTime*16);
            foreach(var task in taskbarTasks)
            {
                if(task.Button==null)continue;
                Color target=task.Minimized?new Color(.85f,.81f,.93f):new Color(.73f,.66f,.87f);
                task.Button.targetGraphic.color=Color.Lerp(task.Button.targetGraphic.color,target,follow);
                if(task.Rail!=null)
                {
                    Color color=task.Rail.color;color.a=Mathf.Lerp(color.a,task.Minimized?.38f:1,follow);
                    task.Rail.color=color;
                }
            }
        }

        void PositionLoginCard(float entered)
        {
            float settle=DesktopMotion.Enter(entered),exit=DesktopMotion.Smooth(loginExitProgress);
            float scale=Mathf.Lerp(.975f,1,settle)*(1-.025f*exit);
            var account=(RectTransform)titleCtaGroup.transform;
            account.localScale=Vector3.one*scale;
            account.anchoredPosition=new Vector2(770,-222)+new Vector2(320,-237)*(1-scale)+Vector2.down*(16*(1-settle)+10*exit);
        }

        public bool DesktopGeometryValid()
        {
            foreach(var target in new[]{FrontendPage.Main,FrontendPage.Diary,FrontendPage.Friends,FrontendPage.News,FrontendPage.MusicPlayer})
            {
                var root=WindowRoot(target);
                if(root==null||!root.gameObject.activeInHierarchy)continue;
                var expected=target==FrontendPage.Main?new Vector2(1386,742):
                    target==FrontendPage.MusicPlayer?new Vector2(840,650):new Vector2(1180,720);
                if((root.sizeDelta-expected).sqrMagnitude>.01f||root.localScale.x<=0||root.localScale.x>1.001f||
                    Mathf.Abs(root.localScale.x-root.localScale.y)>.001f)return false;
            }
            return true;
        }
        public Vector3 DesktopPose(FrontendPage target)
        {
            var root=WindowRoot(target);return root==null?Vector3.zero:
                new Vector3(root.anchoredPosition.x,root.anchoredPosition.y,root.localScale.x);
        }
        public void PreviewControlState(string name,int state)
        {
            if(!buttons.TryGetValue(name,out var button))return;
            var motion=button.GetComponent<DesktopControlMotion>();if(motion==null)return;
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            if(state==1)motion.OnPointerEnter(pointer);
            else if(state==2)motion.OnPointerDown(pointer);
            else {motion.OnPointerUp(pointer);motion.OnPointerExit(pointer);motion.OnDeselect(pointer);}
        }
        public bool PreviewWindowDrag(FrontendPage target,Vector2 delta)
        {
            var root=WindowRoot(target);if(root==null)return false;
            var handle=root.GetComponentInChildren<WindowDragHandler>();if(handle==null)return false;
            var before=root.anchoredPosition;
            Vector3 world=root.TransformPoint(new Vector3(100,-24,0));
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,world);
            handle.OnBeginDrag(pointer);
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,world+root.parent.TransformVector(delta));
            handle.OnDrag(pointer);handle.OnEndDrag(pointer);UpdateLayout();
            return Vector2.Distance(root.anchoredPosition,before+delta)<.1f;
        }
    }
}
