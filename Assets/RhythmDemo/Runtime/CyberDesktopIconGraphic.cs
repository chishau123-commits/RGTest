using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    public enum DesktopIconKind
    {
        Diary,
        Friends,
        News,
        Volume,
        Background,
        Home,
        Help,
        Music
    }

    /// <summary>Small original icon set for the desktop-style main interface.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberDesktopIconGraphic : MaskableGraphic
    {
        public DesktopIconKind kind;
        public Color iconColor=Color.white;

        Vector2 P(float x,float y)
        {
            var r=rectTransform.rect;
            return new Vector2(r.xMin+x*r.width/100f,r.yMax-y*r.height/100f);
        }

        void Poly(VertexHelper v,Color color,params float[] xy)
        {
            var points=new Vector2[xy.Length/2];
            for(int i=0;i<points.Length;i++)points[i]=P(xy[i*2],xy[i*2+1]);
            CyberTheme.Polygon(v,points,color);
        }

        void Line(VertexHelper v,float x1,float y1,float x2,float y2,float width=4)
        {
            CyberTheme.Line(v,P(x1,y1),P(x2,y2),width,iconColor);
        }

        void Ellipse(VertexHelper v,float centerX,float centerY,float radius,Color color,int segments=24)
        {
            int start=v.currentVertCount;
            v.AddVert(P(centerX,centerY),color,Vector2.zero);
            for(int i=0;i<=segments;i++)
            {
                float angle=i/(float)segments*Mathf.PI*2f;
                v.AddVert(P(centerX+Mathf.Cos(angle)*radius,centerY+Mathf.Sin(angle)*radius),color,Vector2.zero);
            }
            for(int i=0;i<segments;i++)v.AddTriangle(start,start+i+1,start+i+2);
        }

        void Arc(VertexHelper v,float centerX,float centerY,float radius,float start,float end,float width)
        {
            int count=Mathf.Max(2,Mathf.CeilToInt((end-start)/6f));
            for(int i=0;i<count;i++)
            {
                float a=Mathf.Lerp(start,end,i/(float)count);
                float b=Mathf.Lerp(start,end,(i+1f)/count);
                Line(v,centerX+Mathf.Cos(a*Mathf.Deg2Rad)*radius,centerY-Mathf.Sin(a*Mathf.Deg2Rad)*radius,
                    centerX+Mathf.Cos(b*Mathf.Deg2Rad)*radius,centerY-Mathf.Sin(b*Mathf.Deg2Rad)*radius,width);
            }
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();
            Color fill=CyberTheme.Alpha(iconColor,.20f);
            Color light=CyberTheme.Alpha(iconColor,.72f);

            switch(kind)
            {
                case DesktopIconKind.Diary:
                    Poly(v,fill,20,17,49,11,81,21,81,80,50,88,20,76);
                    Poly(v,CyberTheme.Alpha(iconColor,.42f),49,11,81,21,81,80,49,88);
                    Line(v,20,17,49,11,3);Line(v,49,11,49,88,3);Line(v,49,88,81,80,3);
                    Line(v,31,35,42,32,2);Line(v,31,50,60,44,2);Line(v,31,65,60,59,2);
                    break;
                case DesktopIconKind.Friends:
                    Ellipse(v,37,32,13,fill,28);Ellipse(v,67,37,11,fill,28);
                    Line(v,37,45,37,72,4);Line(v,21,67,37,50,4);Line(v,37,50,56,63,4);
                    Line(v,67,49,67,72,4);Line(v,54,68,67,56,4);Line(v,67,56,82,67,4);
                    break;
                case DesktopIconKind.News:
                    Poly(v,fill,15,24,69,16,84,28,84,81,16,88);
                    Line(v,15,24,69,16,3);Line(v,69,16,84,28,3);Line(v,84,28,84,81,3);
                    Poly(v,CyberTheme.Alpha(iconColor,.54f),29,28,67,23,69,69,29,74);
                    Line(v,37,39,60,35,2);Line(v,37,51,60,47,2);Line(v,37,63,53,60,2);
                    Line(v,72,45,78,43,2);Line(v,72,58,78,56,2);
                    break;
                case DesktopIconKind.Volume:
                    Poly(v,fill,17,42,34,42,52,26,52,75,34,59,17,59);
                    Line(v,17,42,34,42,3);Line(v,34,42,52,26,3);Line(v,52,26,52,75,3);
                    Line(v,52,75,34,59,3);Line(v,34,59,17,59,3);Line(v,17,59,17,42,3);
                    Arc(v,51,50,21,308,52,3);Arc(v,51,50,31,314,46,2);
                    break;
                case DesktopIconKind.Background:
                    Poly(v,fill,15,20,85,20,85,80,15,80);
                    Line(v,15,20,85,20,3);Line(v,85,20,85,80,3);Line(v,85,80,15,80,3);Line(v,15,80,15,20,3);
                    Ellipse(v,66,35,8,light,24);
                    Poly(v,CyberTheme.Alpha(iconColor,.48f),20,72,39,45,51,59,65,38,81,72);
                    break;
                case DesktopIconKind.Home:
                    Poly(v,fill,16,47,50,17,84,47,75,59,66,51,66,82,34,82,34,51,25,59);
                    Line(v,16,47,50,17,4);Line(v,50,17,84,47,4);Line(v,84,47,75,59,3);
                    Line(v,75,59,66,51,3);Line(v,66,51,66,82,3);Line(v,66,82,34,82,3);
                    Line(v,34,82,34,51,3);Line(v,34,51,25,59,3);Line(v,25,59,16,47,3);
                    Poly(v,light,45,62,55,62,55,82,45,82);
                    break;
                case DesktopIconKind.Help:
                    Ellipse(v,50,50,38,fill,32);
                    Arc(v,50,50,38,0,360,3);
                    Arc(v,50,42,15,35,245,4);Line(v,50,57,50,66,4);
                    Ellipse(v,50,78,3,light,16);
                    break;
                case DesktopIconKind.Music:
                    Ellipse(v,31,75,12,fill,24);Ellipse(v,69,68,12,fill,24);
                    Line(v,42,75,42,25,4);Line(v,80,68,80,18,4);Line(v,42,25,80,18,4);
                    Line(v,52,31,70,27,3);Line(v,52,41,70,37,3);
                    break;
            }
        }
    }

    /// <summary>Fine 72px pixel art, rendered in horizontal runs without texture filtering.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PixelDesktopIconGraphic : MaskableGraphic
    {
        public DesktopIconKind kind;
        public bool showShortcutArrow=true;
        const int Size=72;
        readonly Color32[] pixels=new Color32[Size*Size];
        static readonly Color32 Ink=new Color32(88,62,129,255),Deep=new Color32(117,84,167,255);
        static readonly Color32 Purple=new Color32(154,112,217,255),Lavender=new Color32(203,178,244,255);
        static readonly Color32 Pale=new Color32(237,226,255,255),Paper=new Color32(255,249,255,255);
        static readonly Color32 Cyan=new Color32(89,193,219,255),Ice=new Color32(189,238,249,255);
        static readonly Color32 Pink=new Color32(234,120,178,255),Blush=new Color32(251,190,216,255);
        static readonly Color32 Skin=new Color32(255,226,215,255),SkinShade=new Color32(233,180,195,255);

        void Dot(int x,int y,Color32 c)
        {
            if(x>=0&&x<Size&&y>=0&&y<Size)pixels[y*Size+x]=c;
        }
        void Fill(int x,int y,int w,int h,Color32 c)
        {
            for(int row=y;row<y+h;row++)for(int col=x;col<x+w;col++)Dot(col,row,c);
        }
        void Oval(int x,int y,int w,int h,Color32 c)
        {
            for(int row=y;row<y+h;row++)for(int col=x;col<x+w;col++)
            {
                float dx=(col+.5f-x)/w*2-1,dy=(row+.5f-y)/h*2-1;
                if(dx*dx+dy*dy<=1)Dot(col,row,c);
            }
        }
        void Poly(Color32 c,params int[] xy)
        {
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                bool inside=false;
                for(int i=0,j=xy.Length-2;i<xy.Length;j=i,i+=2)
                    if((xy[i+1]>y+.5f)!=(xy[j+1]>y+.5f)&&
                        x+.5f<(xy[j]-xy[i])*(y+.5f-xy[i+1])/(xy[j+1]-xy[i+1])+xy[i])inside=!inside;
                if(inside)Dot(x,y,c);
            }
        }
        void Line(int x0,int y0,int x1,int y1,Color32 c)
        {
            int dx=Mathf.Abs(x1-x0),sx=x0<x1?1:-1,dy=-Mathf.Abs(y1-y0),sy=y0<y1?1:-1,err=dx+dy;
            while(true)
            {
                Dot(x0,y0,c);if(x0==x1&&y0==y1)break;
                int e=2*err;if(e>=dy){err+=dy;x0+=sx;}if(e<=dx){err+=dx;y0+=sy;}
            }
        }
        void Spark(int x,int y,Color32 c)
        {
            Line(x-3,y,x+3,y,c);Line(x,y-3,x,y+3,c);Dot(x-1,y-1,Paper);Dot(x+1,y+1,Paper);
        }
        void Portrait(int x,int y,Color32 hair,Color32 highlight,Color32 ribbon)
        {
            // Rounded stepped silhouette, side locks, irises, lashes and a sailor collar.
            Oval(x-4,y+8,12,30,Ink);Oval(x+23,y+8,12,30,Ink);
            Oval(x-3,y+9,10,27,hair);Oval(x+24,y+9,10,27,hair);
            Oval(x-1,y+1,32,36,Ink);Oval(x,y+2,30,32,hair);
            Oval(x+5,y+10,22,25,SkinShade);Oval(x+6,y+10,20,23,Skin);
            Poly(hair,x+1,y+8,x+29,y+7,x+28,y+21,x+22,y+14,x+19,y+19,x+17,y+12,x+11,y+19,x+11,y+14,x+4,y+23);
            Line(x+5,y+8,x+14,y+5,highlight);Line(x+5,y+9,x+13,y+6,highlight);
            Line(x+19,y+6,x+24,y+8,highlight);
            Fill(x+8,y+21,5,1,Ink);Fill(x+19,y+21,5,1,Ink);
            Dot(x+7,y+20,Ink);Dot(x+24,y+20,Ink);
            Oval(x+9,y+22,4,6,Ink);Oval(x+19,y+22,4,6,Ink);
            Fill(x+10,y+24,2,3,Cyan);Fill(x+20,y+24,2,3,Cyan);
            Dot(x+10,y+22,Paper);Dot(x+20,y+22,Paper);
            Fill(x+7,y+28,3,1,Blush);Fill(x+23,y+28,3,1,Blush);
            Fill(x+15,y+30,3,1,Pink);Dot(x+15,y+29,Paper);
            Poly(Ink,x+7,y+34,x+24,y+34,x+31,y+46,x,y+46);
            Poly(hair,x+8,y+35,x+23,y+35,x+28,y+44,x+3,y+44);
            Poly(Paper,x+9,y+35,x+16,y+39,x+23,y+35,x+20,y+41,x+12,y+41);
            Fill(x+15,y+40,3,4,ribbon);
            Poly(Ink,x-3,y+10,x+4,y+6,x+6,y+9,x+10,y+7,x+10,y+14,x+5,y+12,x,y+15);
            Poly(ribbon,x-2,y+10,x+3,y+8,x+5,y+10,x+9,y+9,x+9,y+13,x+5,y+11,x,y+13);
            Dot(x+4,y+10,Paper);
        }
        void Diary()
        {
            Oval(13,62,46,5,new Color32(111,82,157,65));
            Poly(Ink,17,6,53,3,60,9,60,61,20,65,13,60,13,12);
            Poly(Purple,18,7,52,5,58,10,58,60,20,63,15,59,15,12);
            Poly(Paper,22,9,56,7,56,58,22,61);
            Poly(Pale,23,11,54,9,54,56,23,59);
            Fill(18,10,3,49,Lavender);Line(16,13,16,57,Pale);
            for(int y=16;y<53;y+=10){Oval(10,y,12,5,Ink);Oval(11,y,10,3,Ice);Fill(15,y+1,5,1,Paper);}
            Fill(26,15,24,1,Lavender);Fill(28,17,20,1,Paper);
            // Cover cameo and the tiny ribbon bookmark use the same palette as the desktop.
            Oval(28,22,22,27,Lavender);Oval(29,23,20,25,Paper);
            Oval(32,26,14,17,Deep);Oval(34,31,10,12,Skin);
            Poly(Purple,32,29,45,28,45,36,41,32,39,35,36,32,33,37);
            Fill(35,36,2,3,Ink);Fill(41,36,2,3,Ink);Dot(35,36,Paper);Dot(41,36,Paper);
            Fill(37,41,3,1,Pink);Poly(Pink,33,43,43,43,47,48,30,48);
            Fill(28,52,22,1,Lavender);Fill(31,55,15,1,Lavender);
            Poly(Deep,46,5,52,5,52,20,49,17,46,20);
            Poly(Blush,47,5,51,5,51,17,49,15,47,17);
            Line(24,60,55,57,Purple);Spark(58,6,Ice);
        }
        void Friends()
        {
            Oval(7,62,57,6,new Color32(111,82,157,65));
            Portrait(33,9,Cyan,Ice,Lavender);
            Portrait(12,18,Purple,Pale,Pink);
            Spark(58,9,Pink);Spark(8,15,Ice);
            // Speech bubble denotes messaging, without a large square frame around the faces.
            Poly(Ink,44,2,63,2,66,5,66,15,61,19,61,15,44,15,41,12,41,5);
            Poly(Paper,45,3,62,3,65,6,65,13,60,16,60,14,45,14,43,11,43,6);
            for(int x=47;x<61;x+=5)Fill(x,8,2,2,Lavender);
        }
        void News()
        {
            Oval(6,62,59,5,new Color32(111,82,157,65));
            Poly(Ink,8,8,62,8,65,11,65,57,62,60,8,60,5,57,5,11);
            Fill(7,10,56,48,Purple);Fill(8,11,54,45,Pale);
            Fill(8,11,54,8,Lavender);Fill(9,12,52,1,Paper);
            Fill(10,20,50,33,Paper);Fill(10,19,50,1,Deep);
            for(int x=49;x<59;x+=4){Fill(x,14,3,3,Deep);Dot(x,14,Paper);}
            Fill(12,14,20,2,Purple);Fill(12,17,12,1,Paper);
            Fill(13,24,18,23,Lavender);Fill(14,25,16,21,Pale);
            Oval(16,27,12,15,Pink);Oval(18,32,8,10,Skin);
            Poly(Blush,16,30,27,29,27,35,24,32,21,35,19,32,17,36);
            Dot(19,36,Ink);Dot(24,36,Ink);Fill(21,40,2,1,Pink);
            Poly(Purple,17,42,26,42,29,46,14,46);
            Fill(35,24,22,3,Purple);Fill(35,29,17,2,Cyan);
            for(int y=34;y<46;y+=4){Fill(35,y,22,1,Lavender);Fill(35,y+1,18,1,Pale);}
            Fill(13,50,44,1,Ice);Fill(10,55,50,1,Paper);
            Fill(28,59,14,3,Deep);Fill(30,59,10,2,Lavender);
            Poly(Ink,25,62,46,62,50,65,21,65);Fill(25,63,21,1,Lavender);
            Spark(61,7,Pink);
        }
        void Music()
        {
            Oval(7,62,58,5,new Color32(111,82,157,65));
            Poly(Ink,9,8,60,8,65,13,65,58,60,63,9,63,5,58,5,13);
            Fill(7,11,56,49,Purple);Fill(9,13,52,45,Pale);
            Fill(11,15,48,7,Lavender);Fill(13,17,21,2,Paper);
            for(int x=39;x<57;x+=5)Fill(x,17,3,3,Cyan);
            Oval(15,28,30,30,Ink);Oval(17,30,26,26,Deep);
            Oval(20,33,20,20,Lavender);Oval(24,37,12,12,Paper);
            Oval(28,41,4,4,Pink);
            Fill(47,30,3,21,Deep);Fill(53,26,3,25,Deep);
            Fill(47,30,9,3,Deep);Fill(47,30,9,2,Cyan);
            Oval(42,48,8,5,Purple);Oval(48,48,8,5,Purple);
            Fill(12,55,45,2,Ice);Fill(15,59,39,1,Paper);
            Spark(61,6,Cyan);
        }
        void ShortcutArrow()
        {
            Fill(3,54,15,14,Ink);Fill(4,55,13,12,Paper);Fill(5,56,11,10,Pale);
            Poly(Deep,6,64,6,61,10,57,14,57,14,61,12,59,8,63,8,65);
            Dot(10,58,Paper);Line(4,55,16,55,Paper);
        }
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();System.Array.Clear(pixels,0,pixels.Length);
            switch(kind){case DesktopIconKind.Diary:Diary();break;case DesktopIconKind.Friends:Friends();break;case DesktopIconKind.News:News();break;case DesktopIconKind.Music:Music();break;}
            if(showShortcutArrow)ShortcutArrow();
            Rect r=rectTransform.rect;float sx=r.width/Size,sy=r.height/Size;
            for(int y=0;y<Size;y++)for(int x=0;x<Size;)
            {
                Color32 c=pixels[y*Size+x];int end=x+1;
                while(end<Size&&pixels[y*Size+end].Equals(c))end++;
                if(c.a>0)CyberTheme.Quad(v,new Vector2(r.xMin+x*sx,r.yMax-y*sy),new Vector2(r.xMin+end*sx,r.yMax-y*sy),
                    new Vector2(r.xMin+end*sx,r.yMax-(y+1)*sy),new Vector2(r.xMin+x*sx,r.yMax-(y+1)*sy),c);
                x=end;
            }
        }
    }
}
