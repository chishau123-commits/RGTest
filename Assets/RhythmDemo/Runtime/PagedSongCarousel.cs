using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Touch-first, horizontally paged real-song carousel. UGUI cancels a Button click
    /// once a drag starts, so a swipe never starts gameplay. Snapping uses canvas units, not DPI.</summary>
    public sealed class PagedSongCarousel : ScrollRect
    {
        public int PageCount=1,Selected;
        public float Stride=588;
        public Action<int> SelectionChanged;
        bool dragging;
        public void SelectPage(int index,bool notify=true)
        {
            index=Mathf.Clamp(index,0,Mathf.Max(0,PageCount-1));
            bool changed=index!=Selected;Selected=index;velocity=Vector2.zero;
            if(content!=null)content.anchoredPosition=new Vector2(-index*Stride,0);
            if(changed && notify)SelectionChanged?.Invoke(index);
        }
        public override void OnBeginDrag(PointerEventData e)
        {
            base.OnBeginDrag(e);dragging=true;
        }
        public override void OnEndDrag(PointerEventData e)
        {
            base.OnEndDrag(e);dragging=false;
            if(content==null)return;
            // A deliberate swipe can advance one card without having to drag half its width.
            float offset=-content.anchoredPosition.x-Selected*Stride;
            int next=Mathf.Abs(offset)>Stride*.15f?Selected+(offset>0?1:-1):Selected;
            SelectPage(next);
        }
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if(!dragging && content!=null)content.anchoredPosition=new Vector2(-Selected*Stride,0);
        }
    }
}
