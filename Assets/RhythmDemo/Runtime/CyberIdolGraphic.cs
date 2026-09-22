using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Temporary blank character silhouette for the lobby. The visual is intentionally
    /// neutral so a final portrait can be dropped into Resources/Characters/hero later.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberIdolGraphic : MaskableGraphic
    {
        static readonly Color SilhouetteEdge=new Color(.34f,.74f,.88f,.42f);
        static readonly Color SilhouetteFill=new Color(.035f,.055f,.12f,.96f);

        Vector2 P(float x,float y)
        {
            var r=rectTransform.rect;
            return new Vector2(r.xMin+x*r.width/900f,r.yMax-y*r.height/1000f);
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

        void Ellipse(VertexHelper v,float centerX,float centerY,float radiusX,float radiusY,Color color,int segments=32)
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

        void Arc(VertexHelper v,float centerX,float centerY,float radius,float start,float end,float width,Color color)
        {
            int count=Mathf.Max(2,Mathf.CeilToInt((end-start)/5f));
            for(int i=0;i<count;i++)
            {
                float a=Mathf.Lerp(start,end,i/(float)count);
                float b=Mathf.Lerp(start,end,(i+1f)/count);
                Line(v,centerX+Mathf.Cos(a*Mathf.Deg2Rad)*radius,centerY-Mathf.Sin(a*Mathf.Deg2Rad)*radius,
                    centerX+Mathf.Cos(b*Mathf.Deg2Rad)*radius,centerY-Mathf.Sin(b*Mathf.Deg2Rad)*radius,color,width);
            }
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();

            // Keep the stage lighting and frame, but leave the figure itself completely blank.
            Ellipse(v,450,475,322,402,new Color(.10f,.06f,.28f,.11f),40);
            Arc(v,450,455,337,10,174,3,CyberTheme.Alpha(CyberTheme.Cyan,.22f));
            Arc(v,450,455,356,198,352,5,CyberTheme.Alpha(CyberTheme.Purple,.27f));
            for(int i=0;i<12;i++)
            {
                float angle=i*30f;
                Line(v,450+Mathf.Cos(angle*Mathf.Deg2Rad)*365,455-Mathf.Sin(angle*Mathf.Deg2Rad)*365,
                    450+Mathf.Cos(angle*Mathf.Deg2Rad)*391,455-Mathf.Sin(angle*Mathf.Deg2Rad)*391,
                    i%3==0?CyberTheme.Alpha(CyberTheme.Pink,.34f):CyberTheme.Alpha(CyberTheme.Cyan,.20f),i%3==0?3:1);
            }

            // A soft offset creates separation from the city without inventing facial details.
            Ellipse(v,466,293,106,126,new Color(.15f,.05f,.34f,.20f),32);
            Poly(v,new Color(.15f,.05f,.34f,.18f),361,449,411,402,501,397,592,445,661,541,713,654,687,1000,235,1000,198,687,245,552);

            // Head, neck, torso, arms and legs form one restrained humanoid placeholder.
            Ellipse(v,450,285,92,112,SilhouetteEdge,36);
            Ellipse(v,450,285,83,103,SilhouetteFill,36);
            Poly(v,SilhouetteEdge,411,374,489,374,507,438,393,438);
            Poly(v,SilhouetteFill,414,378,486,378,501,435,399,435);

            float[] torso={300,473,384,423,450,407,516,423,600,473,653,568,673,697,631,1000,269,1000,227,697,247,568};
            Poly(v,SilhouetteEdge,torso);
            Poly(v,SilhouetteFill,310,486,386,439,450,425,514,439,590,486,637,570,653,689,614,984,286,984,247,689,263,570);

            float[] leftArm={235,542,305,481,354,524,329,626,304,788,259,984,183,984,212,785,232,623};
            float[] rightArm={596,481,665,542,668,623,688,785,717,984,641,984,596,788,571,626,546,524};
            Poly(v,SilhouetteEdge,leftArm);
            Poly(v,SilhouetteEdge,rightArm);
            Poly(v,SilhouetteFill,246,548,305,497,338,530,315,628,289,972,206,972,233,782,248,622);
            Poly(v,SilhouetteFill,603,497,662,548,651,622,674,782,701,972,607,972,580,628,557,530);

            // Separate legs remain readable as a full-body slot rather than a floating bust.
            Poly(v,SilhouetteEdge,304,804,440,798,446,1000,256,1000);
            Poly(v,SilhouetteEdge,460,798,596,804,644,1000,454,1000);
            Poly(v,SilhouetteFill,313,821,429,815,429,1000,272,1000);
            Poly(v,SilhouetteFill,471,815,587,821,628,1000,471,1000);

            // Subtle interior light keeps the blank shape from reading as a missing texture.
            Line(v,450,454,450,982,new Color(.42f,.82f,.92f,.16f),2);
            Line(v,376,478,352,698,new Color(.45f,.58f,.90f,.10f),2);
            Line(v,524,478,548,698,new Color(.70f,.40f,.90f,.10f),2);
            Line(v,397,431,503,431,new Color(.55f,.86f,.94f,.14f),2);

            // Corner brackets mark the replaceable portrait area without covering the silhouette.
            Color bracket=CyberTheme.Alpha(CyberTheme.Cyan,.58f);
            Line(v,85,105,85,65,bracket,3);Line(v,85,65,125,65,bracket,3);
            Line(v,815,105,815,65,bracket,3);Line(v,815,65,775,65,bracket,3);
            Line(v,85,895,85,935,bracket,3);Line(v,85,935,125,935,bracket,3);
            Line(v,815,895,815,935,bracket,3);Line(v,815,935,775,935,bracket,3);
            for(int i=0;i<5;i++)
            {
                Line(v,100+i*18,112,108+i*18,112,CyberTheme.Alpha(CyberTheme.Purple,.38f),2);
                Line(v,792+i*18,888,800+i*18,888,CyberTheme.Alpha(CyberTheme.Purple,.38f),2);
            }
        }
    }
}
