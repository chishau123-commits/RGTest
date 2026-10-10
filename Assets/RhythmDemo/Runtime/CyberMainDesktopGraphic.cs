using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Light lavender desktop artwork shared with the title screen palette.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberMainDesktopGraphic : MaskableGraphic
    {
        Vector2 P(float x,float y)
        {
            var r=rectTransform.rect;
            return new Vector2(r.xMin+x*r.width/1600f,r.yMax-y*r.height/900f);
        }

        void Poly(VertexHelper v,Color color,params float[] xy)
        {
            var points=new Vector2[xy.Length/2];
            for(int i=0;i<points.Length;i++)points[i]=P(xy[i*2],xy[i*2+1]);
            CyberTheme.Polygon(v,points,color);
        }

        void Line(VertexHelper v,float x1,float y1,float x2,float y2,Color color,float width=1)
        {
            CyberTheme.Line(v,P(x1,y1),P(x2,y2),width,color);
        }

        void Ellipse(VertexHelper v,float centerX,float centerY,float radiusX,float radiusY,Color color,int segments=48)
        {
            int start=v.currentVertCount;
            v.AddVert(P(centerX,centerY),color,Vector2.zero);
            for(int i=0;i<=segments;i++)
            {
                float angle=i/(float)segments*Mathf.PI*2f;
                v.AddVert(P(centerX+Mathf.Cos(angle)*radiusX,centerY+Mathf.Sin(angle)*radiusY),color,Vector2.zero);
            }
            for(int i=0;i<segments;i++)v.AddTriangle(start,start+i+1,start+i+2);
        }

        void Pixel(VertexHelper v,float x,float y,float size,Color color)
        {
            CyberTheme.Quad(v,P(x,y),P(x+size,y),P(x+size,y+size),P(x,y+size),color);
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();

            int start=v.currentVertCount;
            v.AddVert(P(0,0),new Color(.67f,.64f,.91f),Vector2.zero);
            v.AddVert(P(1600,0),new Color(.55f,.50f,.82f),Vector2.zero);
            v.AddVert(P(1600,900),new Color(.78f,.74f,.96f),Vector2.zero);
            v.AddVert(P(0,900),new Color(.88f,.85f,.99f),Vector2.zero);
            v.AddTriangle(start,start+1,start+2);v.AddTriangle(start,start+2,start+3);

            // Soft orbital colour fields keep the desktop alive without competing with windows.
            Ellipse(v,104,220,256,188,new Color(.33f,.80f,.90f,.24f),56);
            Ellipse(v,1486,238,275,204,new Color(.97f,.46f,.80f,.20f),56);
            Ellipse(v,800,820,690,248,new Color(.42f,.32f,.78f,.18f),64);
            // Quiet checker motifs make the wallpaper read as a pixel desktop at full resolution.
            for(int row=0;row<23;row++)
            for(int col=0;col<41;col++)
            {
                float x=col*40+(row%2)*20;
                float y=row*40+8;
                Color cyan=new Color(.35f,.79f,.92f,.16f);
                Color violet=new Color(.60f,.43f,.87f,.13f);
                Pixel(v,x,y,5,cyan);Pixel(v,x+5,y+5,5,cyan);
                Pixel(v,x+10,y,5,violet);
            }
            Poly(v,new Color(1f,1f,1f,.20f),0,0,1600,0,1600,92,0,142);
            Poly(v,new Color(.20f,.10f,.46f,.12f),0,900,1600,900,1600,754,0,812);

            // Low-contrast signal grid and scan traces echo the title login artwork.
            for(int i=0;i<25;i++)Line(v,0,174+i*28,1600,174+i*28,new Color(.31f,.22f,.63f,.045f),1);
            for(int i=0;i<29;i++)Line(v,58+i*58,112,58+i*58,824,new Color(1f,1f,1f,.055f),1);
            for(int i=0;i<9;i++)
            {
                float x=70+i*98;
                Line(v,x,688,x+62,672,new Color(.43f,.28f,.82f,.11f),2);
                Line(v,x+62,672,x+122,690,new Color(.28f,.77f,.88f,.09f),1);
            }
            for(int i=0;i<7;i++)
            {
                float x=1080+i*72;
                Line(v,x,152,x+48,142,new Color(1f,1f,1f,.16f),2);
            }
            for(int i=0;i<16;i++)
                Line(v,30+i*18,846,43+i*18,838,new Color(.33f,.22f,.65f,.14f),2);
        }
    }
}
