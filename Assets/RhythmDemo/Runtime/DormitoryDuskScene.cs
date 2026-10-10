using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>A layered pixel stage, rendered at a fixed resolution independently of the desktop UI.</summary>
    public sealed partial class DormitoryDuskScene : MonoBehaviour
    {
        const int WorldLayer=30;
        const float Limit=10.5f;
        const float PixelsPerUnit=24f; // 360 framebuffer rows / 15 world units.
        public const float StairX=0f;
        const float GroundY=-5.1f,LandingY=-3.625f,StairDuration=1.5f;
        const float DescentDuration=StairDuration/.6f,ExitPause=.55f,EntryStart=2.65f;
        public enum TravelState { Descending,Walking,Ascending,Entering,Closing,Finished }
        public TravelState State {get;private set;}
        public bool CanUseStairs=>State==TravelState.Walking&&Mathf.Abs(position-StairX)<=.9f;
        public string InteractionHint=>State==TravelState.Descending?"正在下楼…":
            State==TravelState.Ascending||State==TravelState.Entering||State==TravelState.Closing||State==TravelState.Finished?"正在上楼…":
            NearVending?VendingHint:CanUseStairs?"[ E ] 上楼 · 返回电脑":"走近中央楼梯口或自动贩卖机，按 E 互动";
        readonly List<Object> owned=new List<Object>();
        readonly Sprite[] frames=new Sprite[16];
        readonly Sprite[] stairFrames=new Sprite[8];
        Camera stageCamera;
        Transform background,foreground,actor,shadow,doorLeft,doorRight;
        DormitoryHingedDoor leftHinge,rightHinge;
        AudioSource ambience;
        readonly List<AudioListener> suspendedListeners=new List<AudioListener>();
        float cameraX,cameraVelocity,doorOpen,musicFade=1;
        public float CameraOffset=>cameraX;
        public float DoorOpen=>doorOpen;
        public float EntryPullback=>Mathf.Clamp01((elapsed-EntryStart-ExitPause)/DescentDuration);
        public float LocationTitleAlpha
        {
            get
            {
                float t=elapsed-(EntryStart+ExitPause+DescentDuration+.15f);
                return Mathf.SmoothStep(0,1,Mathf.Clamp01(t/1.1f))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-2.9f)/3f)));
            }
        }
        public bool MusicPlaying=>ambience!=null&&ambience.isPlaying;
        public void SetExitFade(float visibility){musicFade=Mathf.Clamp01(visibility);}
        SpriteRenderer actorRenderer;
        float position=StairX,animationTime,elapsed,stairTime,stairStartX;
        float actorY=LandingY;
        RectTransform inputViewport;
        bool hasMoveTarget;
        float moveTarget,zoom=7.5f,zoomTarget=7.5f,zoomVelocity;
        public float ZoomSize=>zoom;
        public void SetInputViewport(RectTransform viewport){inputViewport=viewport;}
        public void MoveTo(float x)
        {
            if(OutdoorInputLocked||State!=TravelState.Walking)return;
            moveTarget=Mathf.Clamp(x,-Limit,Limit);hasMoveTarget=true;
        }
        public void ScrollZoom(float delta)
        {
            if(!OutdoorInputLocked&&State==TravelState.Walking)zoomTarget=Mathf.Clamp(zoomTarget-delta*.65f,3.8f,7.5f);
        }
        System.Action reachedDoor;
        bool facingLeft;
        public RenderTexture Output {get;private set;}
        public float PlayerX=>position;
        public bool FacingLeft=>facingLeft;
        public int CurrentFrame {get;private set;}
        public void RenderFrame(){if(stageCamera!=null)stageCamera.Render();}

        public void Initialize(System.Action onReachedDoor=null)
        {
            reachedDoor=onReachedDoor;State=TravelState.Descending;stairTime=-2.65f;
            // Isolate the stage from the rhythm gameplay camera and scene geometry.
            // Keep screen-space X/Y near the origin; large X values lose subpixel precision.
            transform.localPosition=new Vector3(0,0,10000);
            // Three render samples per authored pixel: subpixel follow without low-res layer jumps.
            // SpriteMask needs the stencil buffer provided by a 24-bit depth target.
            Output=new RenderTexture(1920,1080,24){name="Dusk supersampled pixel framebuffer",filterMode=FilterMode.Bilinear};
            Output.Create();
            var cam=new GameObject("Dusk camera");cam.transform.SetParent(transform,false);
            stageCamera=cam.AddComponent<Camera>();stageCamera.orthographic=true;
            stageCamera.orthographicSize=7.5f;stageCamera.aspect=16f/9f;
            stageCamera.clearFlags=CameraClearFlags.SolidColor;stageCamera.backgroundColor=new Color(.16f,.13f,.23f);
            stageCamera.cullingMask=1<<WorldLayer;stageCamera.targetTexture=Output;
            stageCamera.nearClipPlane=.1f;stageCamera.farClipPlane=50;
            cam.transform.localPosition=new Vector3(0,0,-20);
            background=Picture("Frontal dormitory / fixed stage","Dusk/dormitory-frontal",0,15f);
            background.localScale=new Vector3(1.06f,1,1); // Overscan for gentle horizontal follow.
            foreground=Picture("Independent near foliage","Dusk/dusk-foreground",21,16.5f);
            foreground.localPosition=new Vector3(0,-.3f,-3);
            BuildDoor();
            BuildLeaves();
            BuildCourtyardDetails();
            BuildNeighborhoodLife();
            foreach(var listener in FindObjectsOfType<AudioListener>())
                if(listener.enabled){suspendedListeners.Add(listener);listener.enabled=false;}
            cam.AddComponent<AudioListener>();
            ambience=gameObject.AddComponent<AudioSource>();
            ambience.clip=Resources.Load<AudioClip>("Music/dormitory-liebestraum");
            if(ambience.clip==null)throw new System.InvalidOperationException("Missing supplied Liebestraum BGM");
            ambience.loop=true;ambience.spatialBlend=0;ambience.volume=0;ambience.Play();
            var sourceSheet=Resources.Load<Texture2D>("Dusk/turi-walk");
            if(sourceSheet==null)throw new System.InvalidOperationException("Missing Turi walking atlas");
            // Resolve the previous 32 PPU -> 24 PPU reduction once, never during movement.
            var sheet=BakePixels(sourceSheet,288,96,false);
            for(int row=0;row<2;row++)for(int col=0;col<8;col++)
            {
                // Atlas is authored top-down: right row, then left row; feet baseline is y=60.
                var sprite=Sprite.Create(sheet,new Rect(col*36,(1-row)*48,36,48),new Vector2(.5f,3f/48f),PixelsPerUnit,0,SpriteMeshType.FullRect);
                frames[row*8+col]=sprite;owned.Add(sprite);
            }
            var stairsSource=Resources.Load<Texture2D>("Dusk/turi-stairs");
            if(stairsSource==null)throw new System.InvalidOperationException("Missing Turi stair atlas");
            var stairs=BakePixels(stairsSource,144,96,false);
            for(int row=0;row<2;row++)for(int col=0;col<4;col++)
            {
                var sprite=Sprite.Create(stairs,new Rect(col*36,(1-row)*48,36,48),new Vector2(.5f,3f/48f),PixelsPerUnit,0,SpriteMeshType.FullRect);
                stairFrames[row*4+col]=sprite;owned.Add(sprite);
            }
            actor=new GameObject("Turi").transform;actor.SetParent(transform,false);actor.gameObject.layer=WorldLayer;
            actorRenderer=actor.gameObject.AddComponent<SpriteRenderer>();actorRenderer.sortingOrder=3;
            actorRenderer.sprite=frames[0];
            var shadowTexture=new Texture2D(48,12,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var pixels=new Color[48*12];
            for(int y=0;y<12;y++)for(int x=0;x<48;x++)
            {float d=Mathf.Pow((x-23.5f)/24,2)+Mathf.Pow((y-5.5f)/6,2);pixels[y*48+x]=new Color(.12f,.10f,.20f,d<1?.32f:0);}
            shadowTexture.SetPixels(pixels);shadowTexture.Apply();owned.Add(shadowTexture);
            var bakedShadow=BakePixels(shadowTexture,36,10,true);
            var shadowSprite=Sprite.Create(bakedShadow,new Rect(0,0,36,10),new Vector2(.5f,.5f),PixelsPerUnit,0,SpriteMeshType.FullRect);owned.Add(shadowSprite);
            shadow=new GameObject("Turi contact shadow").transform;shadow.SetParent(transform,false);shadow.gameObject.layer=WorldLayer;
            var sr=shadow.gameObject.AddComponent<SpriteRenderer>();sr.sprite=shadowSprite;sr.sortingOrder=2;
            Step(0,0);
        }
        readonly List<Transform> leaves=new List<Transform>();
        void BuildLeaves()
        {
            // Pooled decorative pixel silhouettes: no collision or gameplay effects.
            var texture=new Texture2D(7,5,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var pixels=new Color[35];
            string[] mask={"0001000","0011110","0111111","1111100","0010000"};
            for(int y=0;y<5;y++)for(int x=0;x<7;x++)
                if(mask[y][x]=='1')pixels[y*7+x]=y==2?new Color(.95f,.65f,.24f):new Color(.65f,.31f,.12f);
            texture.SetPixels(pixels);texture.Apply();owned.Add(texture);
            var sprite=Sprite.Create(texture,new Rect(0,0,7,5),new Vector2(.5f,.5f),24);owned.Add(sprite);
            for(int i=0;i<32;i++)
            {
                var leaf=new GameObject("Decorative falling leaf "+i);leaf.layer=WorldLayer;
                leaf.transform.SetParent(transform,false);
                var renderer=leaf.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
                renderer.sortingOrder=i%3==0?30:4;
                renderer.color=new Color(1,1,1,i%3==0?.8f:.6f);
                leaves.Add(leaf.transform);
            }
        }
        void UpdateLeaves()
        {
            for(int i=0;i<leaves.Count;i++)
            {
                float seed=i*2.399963f;
                float age=elapsed*(.55f+(i%5)*.12f)+i*.587f;
                float arc=elapsed*(.65f+(i%4)*.12f)+seed;
                float y=8.5f-Mathf.Repeat(age,18f)+Mathf.Sin(arc)*.65f;
                float x=-14f+Mathf.Repeat(i*7.37f,28f)+Mathf.Sin(arc)*.95f+Mathf.Sin(arc*.43f+seed)*.7f;
                var leaf=leaves[i];
                leaf.localPosition=new Vector3(x,y,i%3==0?-4:-.5f);
                leaf.localRotation=Quaternion.Euler(0,0,Mathf.Sin(elapsed*1.1f+seed)*40+i*29);
                float size=i%3==0?1f:.65f;
                leaf.localScale=new Vector3(size*(.45f+.55f*Mathf.Abs(Mathf.Sin(elapsed*1.6f+seed))),size,1);
            }
        }
        void BuildDoor()
        {
            var texture=background.GetComponent<SpriteRenderer>().sprite.texture;
            // Only source x~799..873 is operable. The four sidelights and transom stay in the facade.
            // On the 640px bake this is x=306..334, split at the central meeting stile x=320.
            var opening=new Texture2D(28,28,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var pixels=new Color[28*28];
            for(int y=0;y<28;y++)for(int x=0;x<28;x++)
                pixels[y*28+x]=y<5?new Color(.34f,.22f,.14f):new Color(.10f,.075f,.12f);
            opening.SetPixels(pixels);opening.Apply();owned.Add(opening);
            var aperture=Sprite.Create(opening,new Rect(0,0,28,28),new Vector2(.5f,.5f),24);owned.Add(aperture);
            var hole=new GameObject("Doorway interior");hole.layer=WorldLayer;hole.transform.SetParent(background,false);
            hole.transform.localPosition=new Vector3(0,-71f/24,-.1f);
            var renderer=hole.AddComponent<SpriteRenderer>();renderer.sprite=aperture;renderer.sortingOrder=1;
            for(int side=0;side<2;side++)
            {
                var sprite=Sprite.Create(texture,new Rect(306+14*side,95,14,28),new Vector2(side,0),24,0,SpriteMeshType.FullRect);owned.Add(sprite);
                var leaf=new GameObject(side==0?"Left door hinge":"Right door hinge");leaf.layer=WorldLayer;
                leaf.transform.SetParent(background,false);leaf.transform.localPosition=new Vector3((side==0?-14:14)/24f,-85f/24,-.2f);
                var hinge=leaf.AddComponent<DormitoryHingedDoor>();hinge.Initialize(sprite,side==1);
                if(side==0)leftHinge=hinge;else rightHinge=hinge;
                if(side==0)doorLeft=leaf.transform;else doorRight=leaf.transform;
            }
        }
        Transform Picture(string name,string resource,int order,float height)
        {
            var texture=Resources.Load<Texture2D>(resource);
            if(texture==null)throw new System.InvalidOperationException("Missing dusk layer: "+resource);
            int rows=Mathf.RoundToInt(height*PixelsPerUnit/2)*2;
            int columns=Mathf.RoundToInt(rows*(float)texture.width/texture.height/2)*2;
            var baked=BakePixels(texture,columns,rows,true,resource=="Dusk/dormitory-frontal");
            var sprite=Sprite.Create(baked,new Rect(0,0,columns,rows),new Vector2(.5f,.5f),PixelsPerUnit,0,SpriteMeshType.FullRect);
            owned.Add(sprite);
            var node=new GameObject(name);node.layer=WorldLayer;node.transform.SetParent(transform,false);
            var renderer=node.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=order;
            return node.transform;
        }
        Texture2D BakePixels(Texture2D source,int width,int height,bool areaAverage,bool keepReadable=false)
        {
            var input=source.GetPixels32();var output=new Color32[width*height];
            float sx=(float)source.width/width,sy=(float)source.height/height;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                if(!areaAverage)
                {
                    var pixel=input[Mathf.Min(source.height-1,(int)((y+.5f)*sy))*source.width+
                        Mathf.Min(source.width-1,(int)((x+.5f)*sx))];
                    // The generated atlas has near-opaque body pixels. Solid pixel sprites must
                    // not pick up changing wall colours through that residual transparency.
                    pixel.a=pixel.a>=128?(byte)255:(byte)0;
                    output[y*width+x]=pixel;
                    continue;
                }
                // Area integration with premultiplied alpha keeps fine railings and foliage stable.
                float left=x*sx,right=(x+1)*sx,bottom=y*sy,top=(y+1)*sy;
                float red=0,green=0,blue=0,alpha=0,weight=0;
                for(int iy=(int)bottom;iy<Mathf.CeilToInt(top)&&iy<source.height;iy++)
                for(int ix=(int)left;ix<Mathf.CeilToInt(right)&&ix<source.width;ix++)
                {
                    float w=(Mathf.Min(right,ix+1)-Mathf.Max(left,ix))*(Mathf.Min(top,iy+1)-Mathf.Max(bottom,iy));
                    Color32 c=input[iy*source.width+ix];float a=c.a/255f;
                    red+=c.r*a*w;green+=c.g*a*w;blue+=c.b*a*w;alpha+=a*w;weight+=w;
                }
                output[y*width+x]=alpha>0?new Color32((byte)Mathf.RoundToInt(red/alpha),
                    (byte)Mathf.RoundToInt(green/alpha),(byte)Mathf.RoundToInt(blue/alpha),
                    (byte)Mathf.RoundToInt(255*alpha/weight)):new Color32(0,0,0,0);
            }
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name=source.name+" / pixel-grid bake",
                filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(output);texture.Apply(false,!keepReadable);owned.Add(texture);return texture;
        }
        static float Pixel(float coordinate)=>Mathf.Round(coordinate*PixelsPerUnit)/PixelsPerUnit;
        void Update()
        {
            if(stageCamera==null)return;
            if(Application.isFocused&&inputViewport!=null&&!OutdoorInputLocked&&State==TravelState.Walking)
            {
                Vector2 point;
                if(RectTransformUtility.ScreenPointToLocalPointInRectangle(inputViewport,Input.mousePosition,null,out point)
                    &&inputViewport.rect.Contains(point))
                {
                    if(Input.GetMouseButtonDown(0))
                    {
                        float x=Mathf.InverseLerp(inputViewport.rect.xMin,inputViewport.rect.xMax,point.x);
                        MoveTo(stageCamera.transform.localPosition.x+(x-.5f)*2*stageCamera.orthographicSize*stageCamera.aspect);
                    }
                    ScrollZoom(Input.mouseScrollDelta.y);
                }
            }
            Step(0,Mathf.Min(Time.unscaledDeltaTime,.05f));
        }
        public void Step(float axis,float dt)
        {
            axis=Mathf.Clamp(axis,-1,1);elapsed+=dt;
            if(OutdoorInputLocked){axis=0;hasMoveTarget=false;}
            UpdateLeaves();
            UpdateCourtyardDetails(dt);
            UpdateNeighborhoodLife(dt);
            if(ambience!=null)ambience.volume=.6f*Mathf.Clamp01(elapsed/3)*musicFade;
            if(State==TravelState.Entering||State==TravelState.Closing||State==TravelState.Finished)
            {
                stairTime+=dt;
                if(State==TravelState.Entering)
                {
                    actorRenderer.color=new Color(1,1,1,1-Mathf.Clamp01(stairTime/.4f));
                    if(stairTime>=.4f){State=TravelState.Closing;stairTime=0;}
                }
                else if(State==TravelState.Closing)
                {
                    doorOpen=1-Mathf.SmoothStep(0,1,stairTime/.65f);
                    if(stairTime>=.65f){State=TravelState.Finished;reachedDoor?.Invoke();}
                }
                DrawActor(dt);return;
            }
            if(State!=TravelState.Walking)
            {
                bool up=State==TravelState.Ascending||State==TravelState.Finished;
                stairTime+=dt;
                float travelTime=up?stairTime:stairTime-ExitPause;
                float t=Mathf.Clamp01(travelTime/(up?StairDuration:DescentDuration));
                position=up?Mathf.Lerp(stairStartX,StairX,Mathf.Clamp01(t*4)):StairX;
                actorY=Mathf.Lerp(up?GroundY:LandingY,up?LandingY:GroundY,t);
                actorY+=travelTime>0&&t<1?Mathf.Sin(t*Mathf.PI*12)*.035f:0;
                CurrentFrame=(up?4:0)+(travelTime<=0||t>=1?0:(int)(travelTime*(up?12:7.2f))%4);
                actorRenderer.sprite=stairFrames[CurrentFrame];
                doorOpen=up?Mathf.SmoothStep(0,1,(t-.65f)/.35f):
                    stairTime<0?Mathf.SmoothStep(0,1,(stairTime+.65f)/.65f):1-Mathf.SmoothStep(0,1,(stairTime-ExitPause-.35f)/.65f);
                actorRenderer.color=new Color(1,1,1,up?1:Mathf.Clamp01((stairTime+.25f)/.25f));
                DrawActor(dt);
                if(t>=1&&State!=TravelState.Finished)
                {
                    State=up?TravelState.Entering:TravelState.Walking;
                    stairTime=0;
                    if(!up)StartCourtyardChase();
                }
                return;
            }
            if(hasMoveTarget)
            {
                float distance=moveTarget-position;
                axis=dt>0?Mathf.Clamp(distance/(2.5f*dt),-1,1):0;
                if(Mathf.Abs(distance)<.005f){hasMoveTarget=false;axis=0;}
            }
            float old=position;position=Mathf.Clamp(position+axis*2.5f*dt,-Limit,Limit);
            bool moving=Mathf.Abs(position-old)>.0001f;
            if(axis!=0)facingLeft=axis<0;
            animationTime=moving?animationTime+dt:0;
            CurrentFrame=(facingLeft?8:0)+(moving?2+(int)(animationTime*9)%6:(int)(elapsed*1.5f)%2);
            actorRenderer.sprite=frames[CurrentFrame];
            actorY=GroundY;DrawActor(dt);
        }
        void DrawActor(float dt)
        {
            if(dt>0)zoom=Mathf.SmoothDamp(zoom,zoomTarget,ref zoomVelocity,.65f,4f,dt);
            float close=1-Mathf.InverseLerp(3.8f,7.5f,zoom);
            float horizontalLimit=Mathf.Max(.65f,14.1f-zoom*stageCamera.aspect);
            float target=Mathf.Clamp(Mathf.Sign(position)*Mathf.Max(0,Mathf.Abs(position)-.8f)*Mathf.Lerp(.10f,1,close),-horizontalLimit,horizontalLimit);
            if(dt>0)cameraX=Mathf.SmoothDamp(cameraX,target,ref cameraVelocity,.55f,Mathf.Lerp(.65f,6,close),dt);
            cameraX=Mathf.Clamp(cameraX,-horizontalLimit,horizontalLimit);
            // Quintic ease gives zero velocity and acceleration at both ends.
            float t=EntryPullback;
            float pullback=t*t*t*(t*(t*6-15)+10);
            stageCamera.orthographicSize=Mathf.Lerp(2.8f,zoom,pullback);
            stageCamera.transform.localPosition=new Vector3(Mathf.Round(cameraX*72)/72,Mathf.Lerp(LandingY+.7f,(GroundY+1.6f)*close,pullback),-20);
            UpdateOutdoorCamera(dt);
            foreground.localPosition=new Vector3(-Mathf.Round(cameraX*.35f*72)/72,-.3f,-3);
            leftHinge.SetOpen(doorOpen);rightHinge.SetOpen(doorOpen);
            actor.localPosition=new Vector3(Pixel(position),Pixel(actorY),-1);
            UpdateCoinHint(dt);
            shadow.localPosition=new Vector3(actor.localPosition.x+Pixel(.16f),Pixel(actorY-.03f),0);
            shadow.gameObject.SetActive(actorRenderer.color.a>.5f);
        }
        public bool TryAscend()
        {
            if(OutdoorInputLocked||!CanUseStairs)return false;
            hasMoveTarget=false;
            State=TravelState.Ascending;stairTime=0;stairStartX=position;return true;
        }
        void OnDestroy()
        {
            foreach(var listener in suspendedListeners)if(listener!=null)listener.enabled=true;
            if(stageCamera!=null)stageCamera.targetTexture=null;
            if(Output!=null){Output.Release();Destroy(Output);}
            foreach(var item in owned)if(item!=null)Destroy(item);
        }
    }
}



