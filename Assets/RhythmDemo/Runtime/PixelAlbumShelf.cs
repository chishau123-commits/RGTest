using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GeometryRhythm
{
    // A small perspective scene projected directly into the UI mesh. Box faces, covers and
    // the floor share the same camera, so picking follows the displayed 3D geometry.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PixelAlbumShelf : MaskableGraphic, IPointerClickHandler, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<int> Selected;
        public Action<int> Clicked;
        public Action<bool> ExtractionSettled;
        float extractionRaw;
        bool extractionTarget;
        public bool IsExtracting => extractionTarget||extractionRaw>0;
        public bool DetailReady => extractionTarget&&extractionRaw>=1;
        public void SetExtracted(bool value){extractionTarget=value;if(!value&&extractionRaw==0)ExtractionSettled?.Invoke(false);}
        float Extraction => DesktopMotion.Smooth(extractionRaw);
        public Func<bool> CanInteract;
        public bool Story;
        public int Count { get; private set; }
        public int Index { get; private set; }
        public float Depth { get; private set; }
        public bool Settled => Depth>=.999f&&Mathf.Abs(cursor-Index)<.002f;
        float started,cursor,velocity,nextWheel,dragDistance;
        bool dragging,ignoreClick;
        Vector2 dragStart;
        readonly Vector2[][] faces=new Vector2[64][];
        readonly int[] faceIndices=new int[64];
        int faceCount;
        struct ShelfSlot { public int index; public Vector3 position; public float depth; }
        readonly List<ShelfSlot> slots=new List<ShelfSlot>(64);
        Quaternion cameraRotation,boxRotation;
        Vector3 boxPosition;
        float light;
        float faceOpacity=1;
        bool entryHeld;
        public void HoldFlat(){entryHeld=true;Depth=0;SetVerticesDirty();}
        public void ReleaseEntry(){entryHeld=false;started=Time.unscaledTime;SetVerticesDirty();}
        float boxScale=1;
        bool coverSpace;
        static readonly Color Paper=new Color(.94f,.91f,.99f);
        static readonly Color Violet=new Color(.55f,.43f,.83f);
        static readonly Color Ice=new Color(.45f,.89f,.94f);
        public void Configure(int count,int index,bool story)
        {
            Count=count;Index=Mathf.Clamp(index,0,Mathf.Max(0,count-1));cursor=Index;
            Story=story;started=Time.unscaledTime;raycastTarget=true;
            for(int i=0;i<faces.Length;i++)faces[i]=new Vector2[4];
            SetVerticesDirty();
        }
        public void Select(int index)
        {Index=Mathf.Clamp(index,0,Mathf.Max(0,Count-1));SetVerticesDirty();}
        public void Step(int direction)
        {
            if(Count<2||!Allowed)return;
            int next=Mathf.Clamp(Index+direction,0,Count-1);
            if(next!=Index)Selected?.Invoke(next);
        }
        bool Allowed => !IsExtracting&&Depth>=.999f&&(CanInteract==null||CanInteract());
        void Update()
        {
            float t=entryHeld?0:Mathf.Clamp01((Time.unscaledTime-started)/1.6f);
            float d=t*t*t*(t*(6*t-15)+10);
            float previous=cursor;
            cursor=Mathf.SmoothDamp(cursor,Index,ref velocity,.18f,30,Time.unscaledDeltaTime);
            if(Mathf.Abs(cursor-Index)<.001f){cursor=Index;velocity=0;}
            if(d!=Depth||cursor!=previous){Depth=d;SetVerticesDirty();}
            float previousExtraction=extractionRaw;
            extractionRaw=Mathf.MoveTowards(extractionRaw,extractionTarget?1:0,Time.unscaledDeltaTime/.65f);
            if(previousExtraction!=extractionRaw)
            {
                SetVerticesDirty();
                if(extractionRaw==0||extractionRaw==1)ExtractionSettled?.Invoke(extractionRaw==1);
            }
            if(!Allowed||!Application.isFocused)return;
            if(Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.UpArrow))Step(-1);
            if(Input.GetKeyDown(KeyCode.RightArrow)||Input.GetKeyDown(KeyCode.DownArrow))Step(1);
        }
        public void OnScroll(PointerEventData e)
        {
            if(Time.unscaledTime<nextWheel||Mathf.Abs(e.scrollDelta.y)<.01f)return;
            nextWheel=Time.unscaledTime+.14f;Step(e.scrollDelta.y>0?-1:1);
        }
        public void OnBeginDrag(PointerEventData e){dragStart=e.position;dragDistance=0;dragging=true;ignoreClick=false;}
        public void OnDrag(PointerEventData e)
        {
            if(!dragging)return;
            dragDistance=e.position.x-dragStart.x;
            float threshold=70*(canvas!=null?canvas.scaleFactor:1);
            if(Mathf.Abs(dragDistance)>threshold){Step(dragDistance>0?-1:1);dragStart=e.position;ignoreClick=true;}
        }
        public void OnEndDrag(PointerEventData e){dragging=false;ignoreClick|=Mathf.Abs(dragDistance)>8;}
        public void OnPointerClick(PointerEventData e)
        {
            if(ignoreClick){ignoreClick=false;return;}
            if(!Allowed||e.button!=PointerEventData.InputButton.Left)return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out var p))return;
            for(int i=faceCount-1;i>=0;i--)if(Contains(faces[i],p)){if(faceIndices[i]>=0)(Clicked??Selected)?.Invoke(faceIndices[i]);return;}
        }
        static bool Contains(Vector2[] corners,Vector2 p)
        {
            float sign=0;
            for(int j=0;j<4;j++)
            {
                Vector2 a=corners[(j+1)%4]-corners[j],b=p-corners[j];float cross=a.x*b.y-a.y*b.x;
                if(Mathf.Abs(cross)<.01f)continue;
                if(sign==0)sign=Mathf.Sign(cross);else if(sign*cross<0)return false;
            }
            return true;
        }
        public Vector2 CardScreenPoint(int index)
        {
            for(int i=0;i<faceCount;i++)if(faceIndices[i]==index)
            {
                for(int y=1;y<20;y++)for(int x=1;x<20;x++)
                {
                    Vector2 p=Vector2.Lerp(Vector2.Lerp(faces[i][0],faces[i][1],x*.05f),Vector2.Lerp(faces[i][3],faces[i][2],x*.05f),y*.05f);
                    bool visible=rectTransform.rect.Contains(p);
                    for(int j=i+1;j<faceCount&&visible;j++)if(Contains(faces[j],p))visible=false;
                    if(visible)return RectTransformUtility.WorldToScreenPoint(null,rectTransform.TransformPoint(p));
                }
            }
            return Vector2.negativeInfinity;
        }
        Vector2 Project(Vector3 p)
        {
            p.x+=180*(1-Depth);p=cameraRotation*p;float perspective=1100/(1100+p.z*Depth);
            var r=rectTransform.rect;float scale=Mathf.Lerp(Mathf.Min(r.width/430,r.height/330),Mathf.Min(r.width/1080,r.height/680),Depth);
            return r.center+new Vector2(p.x-20,p.y-10)*perspective*scale;
        }
        Vector3 Face(float x,float y,float z=0)=>boxPosition+boxRotation*(new Vector3(x-180,140-y,z)*boxScale);
        Color Tone(Color c){c.a*=faceOpacity;return c;}
        void Plane(VertexHelper v,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {CyberTheme.Quad(v,Project(a),Project(b),Project(c),Project(d),Tone(color));}
        void Tile(VertexHelper v,float x,float y,float w,float h,Color c)
        {
            if(coverSpace){x=92+(x-24)*1.16f;y=22+(y-16)*1.08f;w*=1.16f;h*=1.08f;}
            Plane(v,Face(x,y),Face(x+w,y),Face(x+w,y+h),Face(x,y+h),Color.Lerp(c*.65f,c,light));
        }
        void Disc(VertexHelper v,float cx,float cy,float radius,Color c)
        {
            for(float y=-radius;y<radius;y+=4)
            {
                float x=Mathf.Floor(Mathf.Sqrt(Mathf.Max(0,radius*radius-(y+2)*(y+2)))/4)*4;
                Tile(v,cx-x,cy+y,x*2,4,c);
            }
        }
        void Card(VertexHelper v,int index)
        {
            float relative=index-cursor,focus=Mathf.Exp(-relative*relative*2.8f);
            light=.92f+.08f*focus;
            // Pose is supplied by the same physical slot used by the empty sleeves.
            var shell=new Color(.69f,.61f,.85f);
            Plane(v,Face(0,0,12),Face(360,0,12),Face(360,0),Face(0,0),new Color(.98f,.96f,1));
            Plane(v,Face(0,0,12),Face(0,0),Face(0,280),Face(0,280,12),new Color(.58f,.49f,.77f));
            Tile(v,0,0,360,280,shell);Tile(v,3,3,354,274,new Color(.84f,.79f,.94f));
            Tile(v,7,7,346,266,new Color(.93f,.90f,.99f));
            Tile(v,12,12,60,256,new Color(.80f,.75f,.91f));
            for(int i=0;i<12;i++)Tile(v,18,20+i*20,46,2,new Color(.91f,.87f,.98f));
            Tile(v,74,8,3,264,new Color(.64f,.56f,.80f));
            Tile(v,84,14,258,246,new Color(.73f,.65f,.87f));
            coverSpace=true;Tile(v,24,16,212,220,new Color(.84f,.79f,.94f));
            Color accent=Story?(index==0?Ice:Violet):Color.Lerp(Ice,Violet,index%3*.45f);
            if(Story)
            {
                Disc(v,168,66,24,new Color(.98f,.96f,1));
                for(int i=0;i<7;i++)
                {
                    int h=24+(i*19+index*11)%60;
                    Tile(v,28+i*28,180-h,24,h,new Color(.65f,.56f,.81f));
                    Tile(v,32+i*28,188-h,4,4,accent);
                }
                Tile(v,28,180,192,8,new Color(.56f,.53f,.75f));
                for(int i=0;i<6;i++)Tile(v,36+i*32,188,4,28,Violet);
                Tile(v,68,146,116,30,new Color(.79f,.78f,.92f));
                for(int i=0;i<5;i++)Tile(v,76+i*20,154,12,12,new Color(.28f,.36f,.52f));
                Tile(v,68,172,116,4,accent);
                if(index>0){Tile(v,104,94,40,28,Violet);Tile(v,112,82,24,4,accent);Tile(v,108,86,4,12,accent);Tile(v,132,86,4,12,accent);}
            }
            else
            {
                for(int i=0;i<6;i++)Tile(v,28,32+i*28,192,1,new Color(.23f,.27f,.4f));
                Disc(v,124,120,84,new Color(.30f,.21f,.50f));
                Disc(v,124,120,72,new Color(.46f,.35f,.67f));
                Disc(v,124,120,64,new Color(.34f,.25f,.55f));
                Disc(v,124,120,48,new Color(.46f,.35f,.67f));
                Disc(v,124,120,40,new Color(.34f,.25f,.55f));
                Disc(v,124,120,24,accent);Disc(v,124,120,8,new Color(.9f,.9f,1));
                Tile(v,152,52,4,12,new Color(.55f,.54f,.7f));Tile(v,160,60,4,12,new Color(.55f,.54f,.7f));
                Tile(v,76,172,4,12,Violet);Tile(v,84,180,4,8,Violet);
            }
            coverSpace=false;
            Tile(v,92,264,120,3,accent);
            for(int i=0;i<8;i++)Tile(v,266+i*8,265,3,7-(i%3),Violet);
            Tile(v,4,4,350,2,Color.white);Tile(v,354,8,2,260,new Color(.97f,.95f,1));
            Tile(v,16,12,40,3,new Color(.97f,.95f,1));Tile(v,16,15,3,32,new Color(.97f,.95f,1));
            if(focus>.1f)Tile(v,0,16,3,248,Color.Lerp(shell,Ice,focus));
        }
        void EmptyCase(VertexHelper v)
        {
            light=1;
            float fade=Mathf.Lerp(.32f,.14f,Mathf.InverseLerp(-360,1250,boxPosition.z));
            var edge=Color.Lerp(Paper,Violet,fade);
            Tile(v,0,0,360,280,edge);Tile(v,4,4,352,272,Color.Lerp(Paper,Color.white,.16f));
            Tile(v,18,12,46,256,Color.Lerp(Paper,Violet,fade*.45f));
            Tile(v,80,14,264,244,Color.Lerp(Paper,Violet,fade*.24f));
            Tile(v,80,14,264,10,Color.Lerp(Paper,Violet,fade*.65f));
            Tile(v,4,4,350,2,new Color(.98f,.96f,1));
            Tile(v,348,16,2,246,Color.Lerp(Paper,Violet,fade));
            Tile(v,18,20,34,2,edge);Tile(v,18,244,34,2,edge);
        }
        void RegisterFace(int index)
        {
            if(faceCount>=faces.Length)return;
            var f=faces[faceCount];f[0]=Project(Face(0,0));f[1]=Project(Face(360,0));
            f[2]=Project(Face(360,280));f[3]=Project(Face(0,280));
            faceIndices[faceCount++]=index;
        }
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();faceCount=0;faceOpacity=1;cameraRotation=Quaternion.Euler(26*Depth,0,0)*Quaternion.Euler(0,52*Depth,0);
            var r=rectTransform.rect;
            CyberTheme.Quad(v,new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin),new Vector2(r.xMin,r.yMin),Paper);
            // Real albums replace sleeves in the central column, sharing size, rotation and spacing.
            // One depth sort also makes decorative sleeves participate in occlusion and picking.
            slots.Clear();
            int first=Mathf.FloorToInt(cursor)-4;
            for(int row=first;row<first+18;row++)for(int column=-1;column<=1;column++)
            {
                if(row<0||(column!=0&&row<Mathf.FloorToInt(cursor)-1))continue;
                int index=column==0&&row<Count?row:-1;
                float focus=index>=0?Mathf.Exp(-(index-cursor)*(index-cursor)*2.8f):0;
                var position=new Vector3(column*430-180,focus*24*Depth,((row-cursor)*112+column*340)*Depth+40);
                slots.Add(new ShelfSlot{index=index,position=position,depth=(cameraRotation*position).z});
            }
            slots.Sort((a,b)=>b.depth.CompareTo(a.depth));
            boxRotation=Quaternion.identity;boxScale=1;
            Vector3 selectedPosition=Vector3.zero;
            foreach(var slot in slots)
            {
                if(slot.index==Index&&Extraction>0){selectedPosition=slot.position;continue;}
                boxPosition=slot.position;
                faceOpacity=(slot.index==Index?1:DesktopMotion.Smooth(Mathf.InverseLerp(.12f,.58f,Depth)))*Mathf.Lerp(1,.22f,Extraction);
                if(slot.index>=0)Card(v,slot.index);else EmptyCase(v);
                RegisterFace(slot.index);
            }
            if(Extraction>0&&Count>0)
            {
                float e=Extraction;
                boxPosition=Vector3.Lerp(selectedPosition,Quaternion.Inverse(cameraRotation)*new Vector3(0,60,-100),e);
                boxRotation=Quaternion.Slerp(Quaternion.identity,Quaternion.Inverse(cameraRotation),e);
                boxScale=Mathf.Lerp(1,1.5f,e);faceOpacity=1;
                Card(v,Index);RegisterFace(Index);
            }
        }
    }
}
