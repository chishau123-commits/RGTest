using UnityEngine;

namespace GeometryRhythm
{
    public sealed partial class DormitoryDuskScene
    {
        // All actors are pooled, decorative, and driven by the same scene clock as the doors.
        readonly Sprite[,] neighborFrames=new Sprite[4,4];
        readonly SpriteRenderer[] neighbors=new SpriteRenderer[4];
        readonly Transform[] neighborShadows=new Transform[4];
        Transform commuterTrain;
        AudioSource railwayAudio,childrenAudio;
        AudioLowPassFilter railwayLowPass;
        float railwayOcclusion;
        public bool RailwayAudioPlaying=>railwayAudio!=null&&railwayAudio.isPlaying;
        public float RailwayAudioVolume=>railwayAudio==null?0:railwayAudio.volume;
        public float RailwayAudioPan=>railwayAudio==null?0:railwayAudio.panStereo;
        public float RailwayOcclusion=>railwayOcclusion;
        public float RailwayCutoff=>railwayLowPass==null?0:railwayLowPass.cutoffFrequency;
        float chaseAge=-1;
        const float ChaseStartX=-16.8f,ChaseEndX=16.8f,ChaseSpeed=7.6f;
        public bool ChaseFinished {get;private set;}
        public int ChaseStartCount {get;private set;}
        public int ActiveChaseActors
        {
            get {int count=0;for(int i=0;i<3;i++)if(neighbors[i]!=null&&neighbors[i].gameObject.activeSelf)count++;return count;}
        }
        public bool ChildrenAudioPlaying=>childrenAudio!=null&&childrenAudio.isPlaying;
        public float ChildrenAudioVolume=>childrenAudio==null?0:childrenAudio.volume;
        public float ChildrenAudioPan=>childrenAudio==null?0:childrenAudio.panStereo;
        public float ChildrenAudioLength=>childrenAudio==null?0:childrenAudio.clip.length;
        float nextTrainAt=9f,trainAge=-1;
        int trainNumber;
        const float TrainDuration=18f;
        public int TrainPassCount=>trainNumber;
        public bool TrainPassing=>trainAge>=0;
        public Vector2 DogPosition=>neighbors[2].transform.localPosition;
        public int NeighborhoodActorCount=>neighbors[0]==null?0:neighbors.Length;

        void BuildNeighborhoodLife()
        {
            var source=Resources.Load<Texture2D>("Dusk/courtyard-neighbors");
            if(source==null)throw new System.InvalidOperationException("Missing courtyard neighbors atlas");
            SliceNeighbors(source);
            BuildNeighborhoodLighting();
            var shade=DetailSprite("Neighbor contact shadow",new[]{
                "000111111111000","011111111111110","111111111111111","011111111111110","000111111111000"},
                new Color(.16f,.12f,.23f,.30f));
            string[] names={"Child in yellow chasing dog","Child in coral chasing dog","Playful courtyard dog","Elder sitting beside begging bowl"};
            for(int i=0;i<4;i++)
            {
                var node=new GameObject(names[i]);node.layer=WorldLayer;node.transform.SetParent(transform,false);
                neighbors[i]=node.AddComponent<SpriteRenderer>();neighbors[i].sprite=neighborFrames[i,0];
                neighbors[i].sortingOrder=i==3?2:5;
                var contact=new GameObject(names[i]+" shadow");contact.layer=WorldLayer;contact.transform.SetParent(transform,false);
                var renderer=contact.AddComponent<SpriteRenderer>();renderer.sprite=shade;renderer.sortingOrder=i==3?1:4;
                contact.transform.localScale=new Vector3(i==3?1.7f:1.1f,1,1);neighborShadows[i]=contact.transform;
                BuildNeighborLighting(i);
                if(i<3)
                {
                    neighbors[i].transform.localPosition=new Vector3(ChaseStartX,-5.75f,-1.5f);
                    node.SetActive(false);contact.SetActive(false);
                }
            }
            childrenAudio=gameObject.AddComponent<AudioSource>();childrenAudio.playOnAwake=false;
            childrenAudio.loop=false;childrenAudio.spatialBlend=0;childrenAudio.volume=0;
            childrenAudio.clip=Resources.Load<AudioClip>("Audio/courtyard-children-laughter");
            if(childrenAudio.clip==null)throw new System.InvalidOperationException("Missing courtyard children laughter recording");
            BuildCommuterTrain();
        }

        void SliceNeighbors(Texture2D source)
        {
            // The generated source has four columns but unequal row heights. Trim alpha per
            // cell, then bake onto identical foot-anchored 48px canvases once at scene creation.
            float[] rowEdges={0,.37f,.63f,.80f,1};
            int[] heights={31,31,16,26};
            var input=source.GetPixels32();
            for(int row=0;row<4;row++)
            {
                var bounds=new RectInt[4];int largestHeight=1,largestWidth=1;
                for(int col=0;col<4;col++)
                {
                    int left=source.width*col/4,right=source.width*(col+1)/4;
                    int bottom=Mathf.RoundToInt(source.height*(1-rowEdges[row+1]));
                    int top=Mathf.RoundToInt(source.height*(1-rowEdges[row]));
                    int minX=right,minY=top,maxX=left,maxY=bottom;
                    for(int y=bottom;y<top;y++)for(int x=left;x<right;x++)
                        if(input[y*source.width+x].a>=160)
                        {minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                    bounds[col]=new RectInt(minX,minY,maxX-minX+1,maxY-minY+1);
                    largestHeight=Mathf.Max(largestHeight,bounds[col].height);largestWidth=Mathf.Max(largestWidth,bounds[col].width);
                }
                float scale=Mathf.Min((float)heights[row]/largestHeight,40f/largestWidth);
                for(int col=0;col<4;col++)
                {
                    var rect=bounds[col];int w=Mathf.Max(1,Mathf.RoundToInt(rect.width*scale)),h=Mathf.Max(1,Mathf.RoundToInt(rect.height*scale));
                    var pixels=new Color32[48*48];
                    for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                    {
                        var color=input[(rect.y+Mathf.Min(rect.height-1,(int)((y+.5f)*rect.height/h)))*source.width+
                            rect.x+Mathf.Min(rect.width-1,(int)((x+.5f)*rect.width/w))];
                        color.a=color.a>=160?(byte)255:(byte)0;
                        pixels[(y+2)*48+(48-w)/2+x]=color;
                    }
                    var texture=new Texture2D(48,48,TextureFormat.RGBA32,false){name="Neighbor "+row+" frame "+col,filterMode=FilterMode.Point};
                    texture.SetPixels32(pixels);texture.Apply();owned.Add(texture);
                    var sprite=Sprite.Create(texture,new Rect(0,0,48,48),new Vector2(.5f,2f/48),PixelsPerUnit,0,SpriteMeshType.FullRect);
                    owned.Add(sprite);neighborFrames[row,col]=sprite;
                }
            }
        }

        void StartCourtyardChase()
        {
            // Called only by the actual descent-completed transition, never by a timer or idle state.
            if(ChaseStartCount!=0)return;
            ChaseStartCount++;chaseAge=0;
            childrenAudio.panStereo=-.85f;childrenAudio.volume=0;childrenAudio.Play();
            UpdateCourtyardChase(0);
        }

        void UpdateCourtyardChase(float dt)
        {
            if(chaseAge<0||ChaseFinished)return;
            chaseAge+=dt;
            float lead=ChaseStartX+chaseAge*ChaseSpeed;
            for(int i=0;i<3;i++)
            {
                float x=lead-(i==2?0:i==0?1.7f:3.2f);
                float lane=i==0?.20f:i==1?-.12f:0;
                float y=-5.75f+lane+Mathf.Sin(chaseAge*3+i)*.055f;
                bool visible=x>=ChaseStartX&&x<=ChaseEndX;
                neighbors[i].gameObject.SetActive(visible);neighborShadows[i].gameObject.SetActive(visible);
                neighbors[i].flipX=false;
                neighbors[i].sprite=neighborFrames[i,(int)(chaseAge*(i==2?15:13)+i)%4];
                float hop=Mathf.Abs(Mathf.Sin(chaseAge*13+i))*.045f;
                neighbors[i].transform.localPosition=new Vector3(Pixel(x),Pixel(y+hop),-1.5f);
                neighborShadows[i].localPosition=new Vector3(Pixel(x+.1f),Pixel(y-.02f),-1.4f);
                UpdateNeighborLighting(i,new Vector2(x,y),dt);
            }
            float childrenX=lead-2.45f;
            float pan=Mathf.Clamp((childrenX-cameraX)/13.5f,-1,1);
            float distance=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,17,Mathf.Abs(childrenX-cameraX)));
            childrenAudio.panStereo=pan*.85f;
            childrenAudio.volume=.48f*distance*Mathf.SmoothStep(0,1,chaseAge/.45f)*musicFade;
            if(lead-3.2f>ChaseEndX)
            {ChaseFinished=true;childrenAudio.Stop();childrenAudio.volume=0;}
        }

        void UpdateNeighborhoodLife(float dt)
        {
            if(neighbors[0]==null)return;
            UpdateCourtyardChase(dt);
            float idle=Mathf.Repeat(elapsed+2,12f);
            neighbors[3].sprite=neighborFrames[3,idle<7?0:idle<8.5f?1:idle<10?2:3];
            neighbors[3].transform.localPosition=new Vector3(-8.1f,-4.7f,-.5f);
            neighborShadows[3].localPosition=new Vector3(-8,-4.72f,-.4f);
            UpdateNeighborLighting(3,new Vector2(-8.1f,-4.7f),dt);

            if(trainAge<0&&elapsed>=nextTrainAt)
            {
                trainAge=0;trainNumber++;commuterTrain.gameObject.SetActive(true);
                railwayOcclusion=0;railwayLowPass.cutoffFrequency=6800;
                railwayAudio.volume=0;railwayAudio.panStereo=trainNumber%2==1?-.78f:.78f;railwayAudio.Play();
            }
            if(trainAge<0)return;
            trainAge+=dt;
            float progress=Mathf.Clamp01(trainAge/TrainDuration),direction=trainNumber%2==1?1:-1;
            float trainX=Mathf.Lerp(-23,23,progress)*direction;
            float backdropX=trainX*24+320;
            // The painted viaduct reappears lower on the right. Change track planes only
            // while all 224 pixels of the train are hidden behind the dormitory.
            float rightTrack=Mathf.SmoothStep(0,1,Mathf.InverseLerp(204,436,backdropX));
            float rightY=(RightRailSurface(backdropX)-180)/24;
            commuterTrain.localPosition=new Vector3(trainX,Mathf.Lerp(.20f,rightY,rightTrack),-.15f);
            commuterTrain.localRotation=Quaternion.Euler(0,0,rightTrack*Mathf.Atan(1f/70)*Mathf.Rad2Deg);
            commuterTrain.localScale=new Vector3(direction,1,1);
            UpdateRailwayAudio(trainX,backdropX,dt);
            if(trainAge>=TrainDuration)
            {
                commuterTrain.gameObject.SetActive(false);railwayAudio.Stop();railwayAudio.volume=0;trainAge=-1;
                nextTrainAt=elapsed+28+(trainNumber*7%17);
            }
        }

        void UpdateRailwayAudio(float trainX,float backdropX,float dt)
        {
            // Integrate coverage over the whole train, not just its centre or renderer visibility.
            // The building occupies x=92..548 in the same baked art used by the sprite mask.
            float covered=Mathf.Max(0,Mathf.Min(backdropX+112,548)-Mathf.Max(backdropX-112,92))/224;
            float blend=1-Mathf.Exp(-dt/.28f);
            railwayOcclusion=Mathf.Lerp(railwayOcclusion,Mathf.Clamp01(covered),blend);
            railwayLowPass.cutoffFrequency=Mathf.Exp(Mathf.Lerp(Mathf.Log(6800),Mathf.Log(650),railwayOcclusion));
            float distance=1/Mathf.Sqrt(1+Mathf.Pow(trainX/12,2));
            float arrival=Mathf.SmoothStep(0,1,trainAge/1.4f);
            float departure=Mathf.SmoothStep(0,1,(TrainDuration-trainAge)/2f);
            railwayAudio.volume=.42f*distance*arrival*departure*Mathf.Lerp(1,.68f,railwayOcclusion)*musicFade;
            railwayAudio.panStereo=Mathf.Lerp(railwayAudio.panStereo,Mathf.Clamp(trainX/18,-.78f,.78f),blend);
        }

        // Wheel-contact line in the baked 640x360 background (origin at lower left).
        static float RightRailSurface(float x)=>182f+(x-548f)/70f;

        void BuildCommuterTrain()
        {
            // Only the two exposed sections above the distant viaduct are visible. The mask
            // follows the authored background's overscan and excludes the building and trees.
            var backdrop=background.GetComponent<SpriteRenderer>().sprite.texture;
            int w=backdrop.width,h=backdrop.height;
            var maskPixels=new Color32[w*h];
            for(int y=180;y<200;y++)for(int x=0;x<w;x++)
            {
                // Contiguous windows follow the clear rail corridor. Colour-keying the
                // sunset skyline would incorrectly punch holes through the moving carriages.
                int treeInset=Mathf.Max(0,190-y);
                bool leftCorridor=x>=34+treeInset&&x<92&&y>=184&&y<198;
                float rail=RightRailSurface(x);
                bool rightCorridor=x>=548&&x<618-treeInset&&y>=Mathf.FloorToInt(rail)&&y<rail+14;
                bool corridor=leftCorridor||rightCorridor;
                if(corridor)maskPixels[y*w+x]=Color.white;
            }
            var maskTexture=new Texture2D(w,h,TextureFormat.RGBA32,false){name="Exposed distant railway mask",filterMode=FilterMode.Point};
            maskTexture.SetPixels32(maskPixels);maskTexture.Apply();owned.Add(maskTexture);
            var maskSprite=Sprite.Create(maskTexture,new Rect(0,0,w,h),new Vector2(.5f,.5f),24,0,SpriteMeshType.FullRect);owned.Add(maskSprite);
            var maskObject=new GameObject("Railway behind dormitory and foliage");maskObject.layer=WorldLayer;maskObject.transform.SetParent(background,false);
            var mask=maskObject.AddComponent<SpriteMask>();mask.sprite=maskSprite;mask.alphaCutoff=.5f;
            mask.isCustomRangeActive=true;mask.frontSortingOrder=2;mask.backSortingOrder=0;
            commuterTrain=new GameObject("Occasional distant commuter train").transform;
            commuterTrain.gameObject.layer=WorldLayer;commuterTrain.SetParent(background,false);
            var car=new GameObject("Four carriage sunset commuter train");car.layer=WorldLayer;car.transform.SetParent(commuterTrain,false);
            var renderer=car.AddComponent<SpriteRenderer>();renderer.sprite=CreateTrainSprite();renderer.sortingOrder=1;
            trainHaze.mainTexture=renderer.sprite.texture;
            renderer.sharedMaterial=trainHaze;renderer.maskInteraction=SpriteMaskInteraction.VisibleInsideMask;
            commuterTrain.gameObject.SetActive(false);
            // Keep the filter on a dedicated source object: it must not muffle the children or BGM.
            var sound=new GameObject("Railway rolling sound / building occlusion");sound.transform.SetParent(transform,false);
            railwayAudio=sound.AddComponent<AudioSource>();railwayAudio.playOnAwake=false;railwayAudio.loop=false;
            railwayAudio.spatialBlend=0;railwayAudio.dopplerLevel=0;railwayAudio.priority=100;
            railwayLowPass=sound.AddComponent<AudioLowPassFilter>();
            railwayLowPass.cutoffFrequency=6800;railwayLowPass.lowpassResonanceQ=1;
            railwayAudio.clip=CreateRailwayRumble();owned.Add(railwayAudio.clip);railwayAudio.volume=0;
        }

        Sprite CreateTrainSprite()
        {
            var source=Resources.Load<Texture2D>("Dusk/distant-commuter-train");
            if(source==null)throw new System.InvalidOperationException("Missing sunset commuter train art");
            var pixels=source.GetPixels32();int left=source.width,right=0,bottom=source.height,top=0;
            for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)
                if(pixels[y*source.width+x].a>=160){left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
            var crop=new Texture2D(right-left+1,top-bottom+1,TextureFormat.RGBA32,false);
            crop.SetPixels(source.GetPixels(left,bottom,crop.width,crop.height));crop.Apply();
            // Match the background's area-sampled pixel grain. Body is about 8px tall;
            // the remaining pixels describe roof equipment and the faint undercarriage.
            var baked=BakePixels(crop,224,12,true);Destroy(crop);
            var sprite=Sprite.Create(baked,new Rect(0,0,224,12),new Vector2(.5f,0),24,0,SpriteMeshType.FullRect);
            owned.Add(sprite);return sprite;
        }

        static AudioClip CreateRailwayRumble()
        {
            // One continuous pass avoids a conspicuous short rumble loop. Low rolling energy
            // survives wall filtering; paired bogie impacts and wheel hiss carry the exposed detail.
            const int rate=44100;var samples=new float[rate*19];var random=new System.Random(73);
            float low=0,mid=0,peak=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,noise=(float)random.NextDouble()*2-1;
                low=Mathf.Lerp(low,noise,.025f);mid=Mathf.Lerp(mid,noise,.24f);
                float bogie=Mathf.Repeat(t,1.08f),impact=0;
                for(int axle=0;axle<4;axle++)
                {
                    float phase=bogie-(axle/2)*.46f-(axle%2)*.115f;
                    if(phase>=0)impact+=Mathf.Exp(-phase*55);
                }
                float roll=low*1.8f+Mathf.Sin(t*Mathf.PI*2*71)*.055f+
                    Mathf.Sin(t*Mathf.PI*2*113)*.025f;
                float clack=impact*(mid*.72f+Mathf.Sin(t*Mathf.PI*2*790)*.10f);
                float hiss=(noise-mid)*.11f*(.8f+.2f*Mathf.Sin(t*1.7f));
                float seam=Mathf.Min(1,Mathf.Min(t,19-t)*30);
                samples[i]=seam*(roll+clack+hiss);peak=Mathf.Max(peak,Mathf.Abs(samples[i]));
            }
            for(int i=0;i<samples.Length;i++)samples[i]*=.78f/Mathf.Max(.001f,peak);
            var clip=AudioClip.Create("Continuous rail roll / bogies / wheel hiss",samples.Length,1,rate,false);
            clip.SetData(samples,0);return clip;
        }
    }
}
