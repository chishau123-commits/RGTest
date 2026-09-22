using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Original full-screen lobby backdrop. Geometry is rebuilt only when the
    /// RectTransform changes, so the animated frontend does not allocate a mesh per frame.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberHomeBackdropGraphic : MaskableGraphic
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

        void Arc(VertexHelper v,float centerX,float centerY,float radius,float start,float end,float width,Color color)
        {
            int count=Mathf.Max(2,Mathf.CeilToInt((end-start)/4f));
            for(int i=0;i<count;i++)
            {
                float a=Mathf.Lerp(start,end,i/(float)count);
                float b=Mathf.Lerp(start,end,(i+1f)/count);
                float scale=rectTransform.rect.height/900f;
                Vector2 center=P(centerX,centerY);
                Vector2 pa=center+new Vector2(Mathf.Cos(a*Mathf.Deg2Rad),-Mathf.Sin(a*Mathf.Deg2Rad))*radius*scale;
                Vector2 pb=center+new Vector2(Mathf.Cos(b*Mathf.Deg2Rad),-Mathf.Sin(b*Mathf.Deg2Rad))*radius*scale;
                CyberTheme.Line(v,pa,pb,width,color);
            }
        }

        void Ellipse(VertexHelper v,float centerX,float centerY,float radiusX,float radiusY,Color color,int segments=48)
        {
            int start=v.currentVertCount;Vector2 center=P(centerX,centerY);
            v.AddVert(center,color,Vector2.zero);
            for(int i=0;i<=segments;i++)
            {
                float angle=i/(float)segments*Mathf.PI*2;
                v.AddVert(P(centerX+Mathf.Cos(angle)*radiusX,centerY+Mathf.Sin(angle)*radiusY),color,Vector2.zero);
            }
            for(int i=0;i<segments;i++)v.AddTriangle(start,start+i+1,start+i+2);
        }

        void CityBlock(VertexHelper v,float x,float width,float top,float baseY,Color color)
        {
            Poly(v,color,x,baseY,x,top,x+width*.82f,top-10,x+width,baseY);
            Poly(v,CyberTheme.Alpha(CyberTheme.Purple,.22f),x,baseY,x,top,x+2,top,x+2,baseY);
            int floors=Mathf.FloorToInt((baseY-top)/31f);
            int columns=Mathf.Max(1,Mathf.FloorToInt(width/31f));
            for(int row=0;row<floors;row++)
            for(int column=0;column<columns;column++)
            {
                if((row*7+column*3+x)%5==0)continue;
                var lit=(row+column)%4==0?CyberTheme.Alpha(CyberTheme.Cyan,.20f):CyberTheme.Alpha(CyberTheme.Purple,.12f);
                Poly(v,lit,x+13+column*28,top+17+row*29,x+20+column*28,top+17+row*29,x+20+column*28,top+22+row*29);
            }
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();

            // Night atmosphere: navy in the center, electric violet at the edges.
            int start=v.currentVertCount;
            v.AddVert(P(0,0),new Color(.045f,.035f,.13f),Vector2.zero);
            v.AddVert(P(1600,0),new Color(.055f,.025f,.12f),Vector2.zero);
            v.AddVert(P(1600,900),new Color(.005f,.012f,.035f),Vector2.zero);
            v.AddVert(P(0,900),new Color(.008f,.018f,.05f),Vector2.zero);
            v.AddTriangle(start,start+1,start+2);v.AddTriangle(start,start+2,start+3);
            Ellipse(v,760,445,820,470,new Color(.03f,.55f,.76f,.43f));
            Ellipse(v,760,250,510,275,new Color(.08f,.76f,.91f,.18f));

            // Angular color fields echo a stage canopy without obscuring the character.
            Poly(v,new Color(.45f,.08f,.54f,.33f),0,0,430,0,255,112,0,174);
            Poly(v,new Color(.08f,.60f,.70f,.18f),1170,0,1600,0,1600,135,1390,92);
            Poly(v,new Color(.30f,.12f,.63f,.22f),0,900,545,900,440,760,0,805);
            Poly(v,new Color(.05f,.62f,.75f,.15f),1070,900,1600,900,1600,750,1270,820);
            Poly(v,CyberTheme.Alpha(CyberTheme.Cyan,.08f),0,118,312,40,118,0,0,0);
            Poly(v,CyberTheme.Alpha(CyberTheme.Pink,.08f),1600,105,1268,39,1512,0,1600,0);
            Line(v,210,105,610,680,new Color(.42f,.92f,1f,.09f),48);
            Line(v,740,90,940,660,new Color(.75f,.95f,1f,.07f),34);

            // Sparse metropolis skyline gives the stage depth while preserving UI contrast.
            CityBlock(v,0,136,505,704,new Color(.025f,.035f,.095f,.95f));
            CityBlock(v,102,170,554,720,new Color(.03f,.04f,.11f,.94f));
            CityBlock(v,245,126,472,704,new Color(.022f,.03f,.085f,.88f));
            CityBlock(v,1118,138,496,704,new Color(.024f,.03f,.09f,.9f));
            CityBlock(v,1238,160,535,720,new Color(.027f,.035f,.105f,.94f));
            CityBlock(v,1370,230,455,714,new Color(.018f,.028f,.08f,.9f));

            // Concentric stage halo behind the idol.
            Arc(v,650,390,305,8,172,1,CyberTheme.Alpha(CyberTheme.Cyan,.20f));
            Arc(v,650,390,329,24,150,3,CyberTheme.Alpha(CyberTheme.Purple,.26f));
            Arc(v,650,390,371,205,338,1,CyberTheme.Alpha(CyberTheme.Cyan,.17f));
            Arc(v,650,390,420,192,352,5,new Color(.38f,.18f,.75f,.18f));
            for(int i=0;i<18;i++)
            {
                float a=i*20f;
                float r1=i%3==0?367:352,r2=i%3==0?385:365;
                Line(v,650+Mathf.Cos(a*Mathf.Deg2Rad)*r1,390-Mathf.Sin(a*Mathf.Deg2Rad)*r1,
                    650+Mathf.Cos(a*Mathf.Deg2Rad)*r2,390-Mathf.Sin(a*Mathf.Deg2Rad)*r2,
                    i%3==0?CyberTheme.Alpha(CyberTheme.Cyan,.35f):CyberTheme.Alpha(CyberTheme.Purple,.25f),i%3==0?2:1);
            }

            // Perspective grid and a glowing runway.
            Color grid=CyberTheme.Alpha(CyberTheme.Cyan,.12f);
            for(int i=-6;i<=6;i++)Line(v,800,583,800+i*126,900,grid,i==0?2:1);
            for(int i=0;i<8;i++)
            {
                float t=i/7f;
                float y=Mathf.Lerp(585,860,t*t);
                Line(v,0,y,1600,y,CyberTheme.Alpha(CyberTheme.Purple,.08f+t*.08f),1);
            }
            Poly(v,CyberTheme.Alpha(CyberTheme.Cyan,.08f),620,628,780,628,990,900,420,900);
            Poly(v,CyberTheme.Alpha(CyberTheme.Purple,.10f),690,628,810,628,1085,900,690,900);
            Line(v,620,628,420,900,CyberTheme.Alpha(CyberTheme.Cyan,.45f),3);
            Line(v,780,628,990,900,CyberTheme.Alpha(CyberTheme.Cyan,.35f),2);
            Line(v,810,628,1085,900,CyberTheme.Alpha(CyberTheme.Purple,.42f),4);

            // Thin scanning rails keep the screen lively without a full-screen post effect.
            for(int i=0;i<9;i++)Line(v,106+i*21,84,126+i*21,84,CyberTheme.Alpha(CyberTheme.Cyan,.24f),4);
            for(int i=0;i<5;i++)Line(v,1230+i*58,786,1254+i*58,802,CyberTheme.Alpha(CyberTheme.Purple,.32f),3);
            Line(v,44,844,1114,844,CyberTheme.Alpha(CyberTheme.Cyan,.14f));
            Line(v,1180,844,1558,844,CyberTheme.Alpha(CyberTheme.Purple,.18f));
        }
    }
}
