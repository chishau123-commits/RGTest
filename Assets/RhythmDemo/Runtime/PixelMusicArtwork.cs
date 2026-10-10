using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    // Grid-aligned artwork. Only the two hovered mode cards redraw, at a capped 24 Hz.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PixelMusicArtwork : MaskableGraphic
    {
        public int kind; // 0 wallpaper, 1 story skyline, 2 record, 3 sealed chapter
        public int edition;
        RectTransform hoverRegion;
        RhythmFrontend hoverOwner;
        Canvas hoverCanvas;
        float speed, engagement, travel, rotation, redrawAt;
        public Vector2? PreviewPointer { get; set; }
        public float MotionSpeed => speed;
        public float Travel => travel;
        public float Rotation => rotation;
        public void BindHover(RectTransform region,RhythmFrontend owner)
        {hoverRegion=region;hoverOwner=owner;hoverCanvas=GetComponentInParent<Canvas>();}
        void Update()
        {
            if(hoverRegion==null)return;
            var camera=hoverCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:hoverCanvas.worldCamera;
            bool hover=(PreviewPointer.HasValue||Application.isFocused)&&!hoverOwner.UiTransitionLocked&&
                RectTransformUtility.RectangleContainsScreenPoint(hoverRegion,PreviewPointer??(Vector2)Input.mousePosition,camera);
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f),target=hover?1:0;
            if(speed==0&&engagement==0&&!hover)return;
            speed=Mathf.MoveTowards(speed,target,dt/(hover?.65f:.85f));
            engagement=Mathf.MoveTowards(engagement,target,dt/.45f);
            travel=Mathf.Repeat(travel+speed*44*dt,34560);
            rotation=Mathf.Repeat(rotation+speed*200*dt,360);
            if(Time.unscaledTime>=redrawAt||(speed==0&&engagement==0))
            {SetVerticesDirty();redrawAt=Time.unscaledTime+1f/24;}
        }
        void MovingBlock(VertexHelper v,float x,float y,float w,float h,Color c)
        {
            float left=Mathf.Max(0,x),right=Mathf.Min(320,x+w);
            if(right>left)Block(v,left,y,right-left,h,c);
        }
        static float Pixel(float value)=>Mathf.Round(value/4)*4;
        void PixelLine(VertexHelper v,Vector2 from,Vector2 to,Color c)
        {
            int count=Mathf.CeilToInt(Vector2.Distance(from,to)/3);
            for(int i=0;i<=count;i++)
            {var p=Vector2.Lerp(from,to,i/(float)Mathf.Max(1,count));Block(v,Pixel(p.x),Pixel(p.y),8,8,c);}
        }
        static readonly Color Ink=new Color(.34f,.25f,.57f);
        static readonly Color Lilac=new Color(.70f,.62f,.88f);
        static readonly Color Paper=new Color(.94f,.91f,.99f);
        void Block(VertexHelper v,float x,float y,float w,float h,Color c)
        {
            var r=rectTransform.rect;
            float s=Mathf.Min(r.width/320f,r.height/180f);
            float ox=r.center.x-160*s,oy=r.center.y+90*s;
            CyberTheme.Quad(v,new Vector2(ox+x*s,oy-y*s),new Vector2(ox+(x+w)*s,oy-y*s),
                new Vector2(ox+(x+w)*s,oy-(y+h)*s),new Vector2(ox+x*s,oy-(y+h)*s),c);
        }
        void Disc(VertexHelper v,float cx,float cy,float radius,Color c)
        {
            for(float y=-radius;y<radius;y+=4)
            {
                float x=Mathf.Floor(Mathf.Sqrt(Mathf.Max(0,radius*radius-(y+2)*(y+2)))/4)*4;
                Block(v,cx-x,cy+y,x*2,4,c);
            }
        }
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();var r=rectTransform.rect;
            CyberTheme.Quad(v,new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax),
                new Vector2(r.xMax,r.yMin),new Vector2(r.xMin,r.yMin),kind==0?new Color(.79f,.76f,.92f):new Color(.86f,.82f,.95f));
            if(kind==0)
            {
                for(int i=0;i<12;i++)
                {
                    Block(v,i*32,144-(i%4)*8,32,60,new Color(.69f,.65f,.85f,.5f));
                    Block(v,i*32,0,32,16+(i%4)*6,new Color(.94f,.90f,1f,.4f));
                }
                for(int i=0;i<8;i++)
                {Block(v,12+i*44,35+(i%3)*42,4,1,Paper);Block(v,13+i*44,34+(i%3)*42,1,4,Paper);}
                return;
            }
            for(int i=0;i<5;i++)Block(v,12,40+i*24,296,1,new Color(.73f,.66f,.87f,.4f));
            if(kind==1)
            {
                Disc(v,236,52,28,Paper);
                if(hoverRegion!=null)
                    for(int i=0;i<12;i++)
                    {
                        float x=Pixel(Mathf.Repeat(i*48-travel*.35f+48,576)-48);
                        int h=20+i%3*12;
                        MovingBlock(v,x,140-h,40,h,new Color(.79f,.73f,.91f));
                    }
                for(int i=0;i<(hoverRegion!=null?12:10);i++)
                {
                    int h=28+(i*17+edition*11)%44;
                    float x=hoverRegion!=null?Pixel(Mathf.Repeat(i*36-travel+36,432)-36):i*36;
                    if(hoverRegion!=null)
                    {MovingBlock(v,x,148-h,32,h,Lilac);MovingBlock(v,x+8,156-h,4,4,Paper);}
                    else{Block(v,x,148-h,32,h,Lilac);Block(v,x+8,156-h,4,4,Paper);}
                }
                Block(v,32,136,256,8,Ink);Block(v,44,128,232,8,Ink);
                for(int i=0;i<(hoverRegion!=null?8:7);i++)
                {
                    float x=hoverRegion!=null?32+Mathf.Repeat(20+i*32-Pixel(travel*2),256):52+i*32;
                    Block(v,x,144,hoverRegion!=null?Mathf.Min(8,288-x):8,32,Ink);
                }
                Block(v,92,96,124,32,Paper);Block(v,100,88,108,8,Paper);
                for(int i=0;i<5;i++)Block(v,104+i*20,102,12,12,Lilac);
                Block(v,92,122,124,4,CyberTheme.Purple);
                Block(v,20,160,8,8,CyberTheme.Cyan);Block(v,292,120,8,8,CyberTheme.Pink);
            }
            else if(kind==2)
            {
                Disc(v,150,94,76,new Color(.76f,.70f,.89f));
                Disc(v,146,86,72,Ink);Disc(v,146,86,60,new Color(.43f,.33f,.66f));
                Disc(v,146,86,48,Ink);Disc(v,146,86,36,new Color(.43f,.33f,.66f));
                Disc(v,146,86,24,edition%2==0?CyberTheme.Purple:CyberTheme.Pink);
                Disc(v,146,86,8,Paper);Disc(v,146,86,4,Ink);
                if(hoverRegion!=null)
                {
                    // Asymmetric groove glints and label marks make rotation legible on the pixel grid.
                    for(int arc=0;arc<2;arc++)for(int j=0;j<5;j++)
                    {
                        float angle=(rotation+arc*180+j*7)*Mathf.Deg2Rad;
                        Block(v,Pixel(146+Mathf.Cos(angle)*54),Pixel(86+Mathf.Sin(angle)*54),4,4,Lilac);
                    }
                    float a=rotation*Mathf.Deg2Rad;
                    Block(v,Pixel(146+Mathf.Cos(a)*16)-2,Pixel(86+Mathf.Sin(a)*16)-2,4,4,Paper);
                    float eased=engagement*engagement*(3-2*engagement);
                    var elbow=Vector2.Lerp(new Vector2(240,104),new Vector2(218,76),eased);
                    var tip=Vector2.Lerp(new Vector2(212,116),new Vector2(184,88),eased);
                    PixelLine(v,new Vector2(240,24),elbow,Paper);PixelLine(v,elbow,tip,Paper);
                    Block(v,Pixel(tip.x)-4,Pixel(tip.y),12,16,Lilac);
                    Disc(v,244,28,8,Paper);
                    for(int i=0;i<3;i++)
                    {
                        float h=4+Pixel(speed*(6+6*Mathf.Sin(rotation*.04f+i*1.6f)));
                        Block(v,268+i*12,144-h,4,h,CyberTheme.Cyan);
                    }
                }
                else{Block(v,240,24,12,84,Paper);Block(v,216,104,32,8,Paper);Block(v,208,108,16,20,Lilac);}
                Block(v,42,28,20,4,CyberTheme.Cyan);Block(v,50,20,4,20,CyberTheme.Cyan);
            }
            else
            {
                Block(v,132,50,56,8,Lilac);Block(v,124,58,8,40,Lilac);Block(v,188,58,8,40,Lilac);
                Block(v,112,94,96,60,Paper);Block(v,112,94,96,4,Lilac);
                Block(v,156,110,8,24,Lilac);
            }
        }
    }
}
