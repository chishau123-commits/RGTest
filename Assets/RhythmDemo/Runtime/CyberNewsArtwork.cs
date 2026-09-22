using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Original vector artwork for a signal-feed post. It is not a note skin,
    /// gameplay element, or external image dependency.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberNewsArtwork : MaskableGraphic
    {
        Vector2 P(float x,float y)
        {
            var r=rectTransform.rect;
            return new Vector2(r.xMin+x*r.width/520f,r.yMax-y*r.height/300f);
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

        void City(VertexHelper v,float x,float width,float top,float baseY,Color color)
        {
            Poly(v,color,x,baseY,x,top,x+width*.82f,top-8,x+width,baseY);
            int rows=Mathf.Max(1,Mathf.FloorToInt((baseY-top)/27f));
            int columns=Mathf.Max(1,Mathf.FloorToInt(width/28f));
            for(int row=0;row<rows;row++)
            for(int column=0;column<columns;column++)
            {
                if((row*3+column*5+(int)x)%4==0)continue;
                Color lit=(row+column)%3==0?CyberTheme.Alpha(CyberTheme.Cyan,.55f):CyberTheme.Alpha(CyberTheme.Pink,.34f);
                Poly(v,lit,x+10+column*25,top+12+row*24,x+17+column*25,top+12+row*24,
                    x+17+column*25,top+17+row*24);
            }
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();
            int start=v.currentVertCount;
            v.AddVert(P(0,0),new Color(.01f,.04f,.07f),Vector2.zero);
            v.AddVert(P(520,0),new Color(.03f,.02f,.08f),Vector2.zero);
            v.AddVert(P(520,300),new Color(.005f,.015f,.035f),Vector2.zero);
            v.AddVert(P(0,300),new Color(.01f,.01f,.025f),Vector2.zero);
            v.AddTriangle(start,start+1,start+2);v.AddTriangle(start,start+2,start+3);

            // A rain-soaked lower district, cropped like a social post image.
            Poly(v,new Color(.05f,.55f,.68f,.22f),0,172,520,132,520,300,0,300);
            City(v,0,104,116,220,new Color(.018f,.055f,.1f,.96f));
            City(v,96,128,86,220,new Color(.025f,.07f,.12f,.96f));
            City(v,210,94,132,220,new Color(.014f,.045f,.09f,.96f));
            City(v,410,110,96,220,new Color(.02f,.055f,.105f,.96f));
            Line(v,0,220,520,220,CyberTheme.Alpha(CyberTheme.Cyan,.38f),2);

            // Glitch rain, signs, and a signal trace.
            for(int i=0;i<24;i++)
            {
                float x=14+i*23;
                Line(v,x,18,x-24,184,i%5==0?CyberTheme.Alpha(CyberTheme.Pink,.50f):CyberTheme.Alpha(CyberTheme.Cyan,.18f),1);
            }
            Poly(v,CyberTheme.Alpha(CyberTheme.Pink,.72f),94,42,212,42,198,58,80,58);
            Poly(v,CyberTheme.Alpha(CyberTheme.Cyan,.64f),96,70,188,70,174,84,82,84);
            Poly(v,CyberTheme.Alpha(CyberTheme.Purple,.55f),320,54,432,54,414,72,302,72);
            Line(v,60,128,188,128,CyberTheme.Alpha(CyberTheme.Text,.58f),2);
            Line(v,60,136,136,136,CyberTheme.Alpha(CyberTheme.Cyan,.72f),4);
            Line(v,358,112,474,112,CyberTheme.Alpha(CyberTheme.Pink,.60f),2);
            for(int i=0;i<11;i++)Line(v,338+i*13,88,338+i*13,96,CyberTheme.Alpha(CyberTheme.Text,.62f),3);
            Line(v,276,218,276,262,CyberTheme.Alpha(CyberTheme.Cyan,.58f),2);
            Line(v,260,244,292,244,CyberTheme.Alpha(CyberTheme.Cyan,.58f),2);
        }
    }
}
