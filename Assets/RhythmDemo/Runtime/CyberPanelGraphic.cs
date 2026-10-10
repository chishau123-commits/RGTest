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
        // MainUI uses a fine, stepped silhouette while retaining the panel's size and palette.
        public bool pixelStyle;
        public bool pixelChrome;
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();var r=rectTransform.rect;
            if(rounded)
            {
                float radius=Mathf.Clamp(cornerRadius,1,Mathf.Min(r.width,r.height)*.5f);
                int steps=6;var points=new System.Collections.Generic.List<Vector2>(steps*4+4);
                if(pixelStyle)
                {
                    AddPixelCorner(points,r.xMax-radius,r.yMax-radius,radius,90,0,steps);
                    AddPixelCorner(points,r.xMax-radius,r.yMin+radius,radius,0,-90,steps);
                    AddPixelCorner(points,r.xMin+radius,r.yMin+radius,radius,-90,-180,steps);
                    AddPixelCorner(points,r.xMin+radius,r.yMax-radius,radius,180,90,steps);
                }
                else
                {
                    AddCorner(points,r.xMax-radius,r.yMax-radius,radius,90,0,steps);
                    AddCorner(points,r.xMax-radius,r.yMin+radius,radius,0,-90,steps);
                    AddCorner(points,r.xMin+radius,r.yMin+radius,radius,-90,-180,steps);
                    AddCorner(points,r.xMin+radius,r.yMax-radius,radius,180,90,steps);
                }
                CyberTheme.Polygon(v,points.ToArray(),color);
                if(!outline)return;
                for(int i=0;i<points.Count;i++)CyberTheme.Line(v,points[i],points[(i+1)%points.Count],pixelStyle?2:1,
                    pixelStyle?new Color(.29f,.19f,.58f,.72f):CyberTheme.Alpha(accent,.38f));
                if(pixelStyle) { AddPixelTicks(v,r,accent);if(pixelChrome)AddPixelChrome(v,r,accent); }
                else for(int i=0;i<steps;i++)CyberTheme.Line(v,points[steps*3+i],points[steps*3+i+1],2.5f,accent);
                return;
            }
            float c=Mathf.Min(cut,Mathf.Min(r.width,r.height)*.3f);
            if(pixelStyle)
            {
                var points=new System.Collections.Generic.List<Vector2>(20);
                points.Add(new Vector2(r.xMin+c,r.yMax));
                points.Add(new Vector2(r.xMax,r.yMax));
                points.Add(new Vector2(r.xMax,r.yMin+c));
                int stairCount=Mathf.Clamp(Mathf.CeilToInt(c/4f),2,5);
                for(int i=1;i<=stairCount;i++)
                {
                    float step=c*i/stairCount;
                    points.Add(new Vector2(r.xMax-step,r.yMin+c-c*(i-1)/stairCount));
                    points.Add(new Vector2(r.xMax-step,r.yMin+c-step));
                }
                points.Add(new Vector2(r.xMin,r.yMin));
                points.Add(new Vector2(r.xMin,r.yMax-c));
                for(int i=1;i<=stairCount;i++)
                {
                    float step=c*i/stairCount;
                    points.Add(new Vector2(r.xMin+step,r.yMax-c+c*(i-1)/stairCount));
                    points.Add(new Vector2(r.xMin+step,r.yMax-c+step));
                }
                CyberTheme.Polygon(v,points.ToArray(),color);
                if(!outline)return;
                for(int i=0;i<points.Count;i++)CyberTheme.Line(v,points[i],points[(i+1)%points.Count],2,new Color(.29f,.19f,.58f,.72f));
                CyberTheme.Line(v,points[0],points[0]+Vector2.right*Mathf.Min(48,r.width*.2f),3,accent);
                AddPixelTicks(v,r,accent);
                if(pixelChrome)AddPixelChrome(v,r,accent);
                return;
            }
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
        static void AddPixelCorner(System.Collections.Generic.List<Vector2> points,float centerX,float centerY,float radius,float start,float end,int steps)
        {
            for(int i=0;i<=steps;i++)
            {
                float angle=Mathf.Lerp(start,end,i/(float)steps)*Mathf.Deg2Rad;
                var next=new Vector2(centerX+Mathf.Round(Mathf.Cos(angle)*radius/3f)*3f,
                    centerY+Mathf.Round(Mathf.Sin(angle)*radius/3f)*3f);
                if(points.Count>0)
                {
                    Vector2 previous=points[points.Count-1];
                    if(!Mathf.Approximately(previous.x,next.x)&&!Mathf.Approximately(previous.y,next.y))
                        points.Add(new Vector2(next.x,previous.y));
                }
                points.Add(next);
            }
        }
        static void AddPixelTicks(VertexHelper v,Rect r,Color accent)
        {
            if(r.width<38||r.height<24)return;
            Color subtle=CyberTheme.Alpha(accent,.46f);
            float x=r.xMin+Mathf.Min(24,r.width*.16f);
            float y=r.yMax-5;
            for(int i=0;i<3;i++)
            {
                float left=x+i*6;
                CyberTheme.Quad(v,new Vector2(left,y),new Vector2(left+4,y),
                    new Vector2(left+4,y-3),new Vector2(left,y-3),subtle);
            }
        }
        static void AddPixelChrome(VertexHelper v,Rect r,Color accent)
        {
            if(r.width<120||r.height<35)return;
            Color ink=new Color(.24f,.13f,.53f,.78f);
            CyberTheme.Line(v,new Vector2(r.xMin+5,r.yMax-5),new Vector2(r.xMax-5,r.yMax-5),1,ink);
            CyberTheme.Line(v,new Vector2(r.xMin+5,r.yMin+5),new Vector2(r.xMax-5,r.yMin+5),1,CyberTheme.Alpha(accent,.65f));
            for(int i=0;i<3;i++)
            {
                float x=r.xMax-35+i*7;
                CyberTheme.Quad(v,new Vector2(x,r.yMin+9),new Vector2(x+4,r.yMin+9),
                    new Vector2(x+4,r.yMin+13),new Vector2(x,r.yMin+13),ink);
            }
        }
    }
}
