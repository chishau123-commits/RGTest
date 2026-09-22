using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Title-screen-only artwork. It deliberately shares no background texture,
    /// character layer, or lobby layout with the main interface.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberTitleGraphic : MaskableGraphic
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
            int count=Mathf.Max(2,Mathf.CeilToInt((end-start)/5f));
            Vector2 center=P(centerX,centerY);
            float scale=rectTransform.rect.height/900f;
            for(int i=0;i<count;i++)
            {
                float a=Mathf.Lerp(start,end,i/(float)count);
                float b=Mathf.Lerp(start,end,(i+1f)/count);
                Vector2 pa=center+new Vector2(Mathf.Cos(a*Mathf.Deg2Rad),-Mathf.Sin(a*Mathf.Deg2Rad))*radius*scale;
                Vector2 pb=center+new Vector2(Mathf.Cos(b*Mathf.Deg2Rad),-Mathf.Sin(b*Mathf.Deg2Rad))*radius*scale;
                CyberTheme.Line(v,pa,pb,width,color);
            }
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();

            // A restrained vertical print field replaces the character-photo lobby backdrop.
            int start=v.currentVertCount;
            v.AddVert(P(0,0),new Color(.018f,.024f,.038f),Vector2.zero);
            v.AddVert(P(1600,0),new Color(.032f,.016f,.028f),Vector2.zero);
            v.AddVert(P(1600,900),new Color(.010f,.012f,.022f),Vector2.zero);
            v.AddVert(P(0,900),new Color(.008f,.010f,.018f),Vector2.zero);
            v.AddTriangle(start,start+1,start+2);v.AddTriangle(start,start+2,start+3);

            // Broad oversized slashes create a title identity unrelated to the lobby stage.
            Poly(v,new Color(.05f,.48f,.54f,.20f),770,900,1010,900,545,0,305,0);
            Poly(v,new Color(.72f,.12f,.34f,.15f),1025,900,1105,900,640,0,560,0);
            Poly(v,new Color(.55f,.40f,.98f,.10f),1325,900,1600,900,1600,238,1425,314);
            Poly(v,new Color(.015f,.025f,.047f,.94f),1110,0,1600,0,1600,900,1200,900);

            // Fine registration lines and staggered type blocks.
            Color faint=CyberTheme.Alpha(CyberTheme.Text,.055f);
            for(int i=0;i<18;i++)Line(v,0,88+i*47,1600,88+i*47,faint,1);
            for(int i=0;i<12;i++)Line(v,1090+i*44,0,1090+i*44,900,CyberTheme.Alpha(CyberTheme.Cyan,.035f),1);
            Line(v,1100,0,1100,900,CyberTheme.Alpha(CyberTheme.Cyan,.28f),2);
            Line(v,1106,0,1106,900,CyberTheme.Alpha(CyberTheme.Pink,.16f),1);

            // Broken rings suggest a signal being traced without reusing the main-menu halo.
            Arc(v,1305,406,315,204,356,3,CyberTheme.Alpha(CyberTheme.Cyan,.28f));
            Arc(v,1305,406,342,192,331,1,CyberTheme.Alpha(CyberTheme.Text,.14f));
            Arc(v,1305,406,381,222,326,7,CyberTheme.Alpha(CyberTheme.Pink,.18f));
            Arc(v,1305,406,410,202,275,2,CyberTheme.Alpha(CyberTheme.Purple,.24f));
            for(int i=0;i<15;i++)
            {
                float a=204+i*10.7f;
                float inner=i%4==0?349:361;
                float outer=i%4==0?373:382;
                Line(v,1305+Mathf.Cos(a*Mathf.Deg2Rad)*inner,406-Mathf.Sin(a*Mathf.Deg2Rad)*inner,
                    1305+Mathf.Cos(a*Mathf.Deg2Rad)*outer,406-Mathf.Sin(a*Mathf.Deg2Rad)*outer,
                    i%4==0?CyberTheme.Alpha(CyberTheme.Cyan,.46f):CyberTheme.Alpha(CyberTheme.Purple,.28f),2);
            }
            Poly(v,CyberTheme.Alpha(CyberTheme.Pink,.12f),1128,343,1240,343,1174,468,1088,468);
            Poly(v,CyberTheme.Alpha(CyberTheme.Cyan,.12f),1176,365,1312,365,1232,485,1110,485);

            // Barcode, hard cuts, and terminal-like status rails.
            for(int i=0;i<28;i++)
            {
                float x=1190+i*13;
                Line(v,x,626,x,674,i%3==0?CyberTheme.Alpha(CyberTheme.Text,.65f):CyberTheme.Alpha(CyberTheme.Cyan,.34f),i%5==0?5:2);
            }
            Line(v,1128,716,1544,716,CyberTheme.Alpha(CyberTheme.Cyan,.28f),2);
            for(int i=0;i<9;i++)Line(v,1128+i*52,716,1152+i*52,742,CyberTheme.Alpha(CyberTheme.Pink,.38f),3);
            Poly(v,CyberTheme.Alpha(CyberTheme.Cyan,.16f),1430,760,1588,760,1544,786,1386,786);
            Poly(v,CyberTheme.Alpha(CyberTheme.Pink,.18f),1380,792,1515,792,1488,812,1352,812);
            Line(v,42,806,1014,806,CyberTheme.Alpha(CyberTheme.Cyan,.13f),1);
            Line(v,42,812,720,812,CyberTheme.Alpha(CyberTheme.Pink,.10f),1);

            // Sparse corner calibration marks keep the frame readable at different aspect ratios.
            Line(v,38,38,142,38,CyberTheme.Alpha(CyberTheme.Cyan,.42f),2);
            Line(v,38,38,38,118,CyberTheme.Alpha(CyberTheme.Cyan,.42f),2);
            Line(v,1562,862,1458,862,CyberTheme.Alpha(CyberTheme.Pink,.42f),2);
            Line(v,1562,862,1562,782,CyberTheme.Alpha(CyberTheme.Pink,.42f),2);
        }
    }
}
