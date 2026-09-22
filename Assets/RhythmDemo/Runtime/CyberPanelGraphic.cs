using UnityEngine;
using UnityEngine.UI;
namespace GeometryRhythm
{
    /// <summary>Cut-corner panel. Button tint changes its fill, not its border.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberPanelGraphic : MaskableGraphic
    {
        public Color accent=CyberTheme.Cyan;
        public float cut=16;
        public bool outline=true;
        public bool rounded;
        public float cornerRadius=12;
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();var r=rectTransform.rect;
            if(rounded)
            {
                float radius=Mathf.Clamp(cornerRadius,1,Mathf.Min(r.width,r.height)*.5f);
                int steps=6;var points=new System.Collections.Generic.List<Vector2>(steps*4+4);
                AddCorner(points,r.xMax-radius,r.yMax-radius,radius,90,0,steps);
                AddCorner(points,r.xMax-radius,r.yMin+radius,radius,0,-90,steps);
                AddCorner(points,r.xMin+radius,r.yMin+radius,radius,-90,-180,steps);
                AddCorner(points,r.xMin+radius,r.yMax-radius,radius,180,90,steps);
                CyberTheme.Polygon(v,points.ToArray(),color);
                if(!outline)return;
                for(int i=0;i<points.Count;i++)CyberTheme.Line(v,points[i],points[(i+1)%points.Count],1,CyberTheme.Alpha(accent,.38f));
                for(int i=0;i<steps;i++)CyberTheme.Line(v,points[steps*3+i],points[steps*3+i+1],2.5f,accent);
                return;
            }
            float c=Mathf.Min(cut,Mathf.Min(r.width,r.height)*.3f);
            var p=new[]{new Vector2(r.xMin+c,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin+c),
                new Vector2(r.xMax-c,r.yMin),new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax-c)};
            CyberTheme.Polygon(v,p,color);if(!outline)return;
            for(int i=0;i<p.Length;i++) CyberTheme.Line(v,p[i],p[(i+1)%p.Length],1,CyberTheme.Alpha(accent,.32f));
            CyberTheme.Line(v,p[5],p[0],2.5f,accent);CyberTheme.Line(v,p[2],p[3],2.5f,accent);
            CyberTheme.Line(v,p[0],p[0]+Vector2.right*Mathf.Min(48,r.width*.2f),2,accent);
        }
        static void AddCorner(System.Collections.Generic.List<Vector2> points,float centerX,float centerY,float radius,float start,float end,int steps)
        {
            for(int i=0;i<=steps;i++)
            {
                float angle=Mathf.Lerp(start,end,i/(float)steps)*Mathf.Deg2Rad;
                points.Add(new Vector2(centerX+Mathf.Cos(angle)*radius,centerY+Mathf.Sin(angle)*radius));
            }
        }
    }
}
