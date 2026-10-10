using UnityEngine;

namespace GeometryRhythm
{
    public sealed partial class DormitoryDuskScene
    {
        const float VendingX=6.6f;
        readonly System.Collections.Generic.List<Transform> maples=new System.Collections.Generic.List<Transform>();
        AudioSource city, vendingAudio;
        AudioClip buttonSound,coinSound,motorSound,dropSound,takeSound;
        Transform drink;
        SpriteRenderer coinHint;
        float coinHintAlpha;
        public bool CoinHintVisible=>coinHint!=null&&coinHint.color.a>.5f;
        void UpdateCoinHint(float dt)
        {
            if(coinHint==null)return;
            float target=NearVending&&vendTime<0&&!drinkReady&&elapsed>=receiptUntil?1:0;
            coinHintAlpha=Mathf.MoveTowards(coinHintAlpha,target,dt*4);
            coinHint.color=new Color(1,1,1,coinHintAlpha);
            coinHint.transform.localPosition=actor.localPosition+new Vector3(0,2.15f+Mathf.Sin(elapsed*2)*.06f,-1);
        }
        float vendTime=-1,receiptUntil;
        bool drinkReady;
        public bool NearVending=>State==TravelState.Walking&&Mathf.Abs(position-VendingX)<.9f;
        public bool DrinkReady=>drinkReady;
        public int DrinksCollected {get;private set;}
        public event System.Action DrinkCollected;
        string VendingHint=>vendTime>=0?"自动贩卖机正在出货…":drinkReady?"[ E ] 取走饮料":elapsed<receiptUntil?"拿到了一罐温热的饮料。":"[ E ] 自动贩卖机 · 选择饮料";
        Sprite DetailSprite(string name,string[] rows,Color color)
        {
            int w=rows[0].Length,h=rows.Length;
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Point};
            var pixels=new Color[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                if(rows[y][x]!='0')pixels[(h-1-y)*w+x]=rows[y][x]=='2'?new Color(1,.76f,.35f):color;
            texture.SetPixels(pixels);texture.Apply();owned.Add(texture);
            var sprite=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),24);owned.Add(sprite);return sprite;
        }
        void BuildCourtyardDetails()
        {
            var hint=DetailSprite("Coin into slot hint",new[]{
                "0000222220000","0002111112000","0021121211200","0021121211200",
                "0021121211200","0002111112000","0000222220000","0000002000000",
                "0000002000000","0000202020000","0000022200000","0000002000000",
                "0000000000000","0222222222220","0211111111120","0222222222220"},new Color(.43f,.23f,.09f));
            var hintObject=new GameObject("Turi coin interaction hint");hintObject.layer=WorldLayer;
            hintObject.transform.SetParent(transform,false);
            coinHint=hintObject.AddComponent<SpriteRenderer>();coinHint.sprite=hint;coinHint.sortingOrder=50;coinHint.color=Color.clear;
            var maple=DetailSprite("Foreground maple silhouette",new[]{
                "00000000100000000","00000001110000000","00000011211000000",
                "00010011211001000","00011011211011000","00111111211111100",
                "00011111211111000","10001111211110001","11111111211111111",
                "01111111211111110","00111111211111100","00011111211111000",
                "00111111211111100","00000000200000000","00000000200000000",
                "00000000020000000","00000000020000000"},new Color(.8f,.23f,.07f));
            for(int i=0;i<6;i++)
            {
                var leaf=new GameObject("Near lens maple "+i);leaf.layer=WorldLayer;leaf.transform.SetParent(transform,false);
                var r=leaf.AddComponent<SpriteRenderer>();r.sprite=maple;r.sortingOrder=40;
                r.color=Color.Lerp(new Color(1,.68f,.38f,.9f),Color.white,i/5f);
                maples.Add(leaf.transform);
            }
            var can=DetailSprite("Dispensed warm drink",new[]{"02220","21112","11211","11211","11211","21112","02220"},new Color(.72f,.24f,.17f));
            drink=new GameObject("Vending drink in pickup tray").transform;drink.SetParent(transform,false);drink.gameObject.layer=WorldLayer;
            var renderer=drink.gameObject.AddComponent<SpriteRenderer>();renderer.sprite=can;renderer.sortingOrder=2;
            drink.gameObject.SetActive(false);
            vendingAudio=gameObject.AddComponent<AudioSource>();vendingAudio.playOnAwake=false;
            buttonSound=DepartureFoley.Create("click");motorSound=DepartureFoley.Create("motor");
            coinSound=DepartureFoley.Create("coin");owned.Add(coinSound);
            dropSound=DepartureFoley.Create("door");takeSound=DepartureFoley.Create("ding");
            owned.Add(buttonSound);owned.Add(motorSound);owned.Add(dropSound);owned.Add(takeSound);
            city=gameObject.AddComponent<AudioSource>();city.playOnAwake=false;city.loop=true;city.spatialBlend=0;
            city.clip=CreateCityAmbience();owned.Add(city.clip);city.volume=0;city.Play();
        }
        public bool TryUseVending()
        {
            if(OutdoorInputLocked||!NearVending)return false;
            hasMoveTarget=false;
            if(vendTime>=0)return true;
            if(drinkReady)
            {
                drinkReady=false;drink.gameObject.SetActive(false);DrinksCollected++;
                receiptUntil=elapsed+3;vendingAudio.PlayOneShot(takeSound,.4f);DrinkCollected?.Invoke();return true;
            }
            if(elapsed<receiptUntil)return true;
            vendTime=0;vendingAudio.panStereo=.3f;vendingAudio.PlayOneShot(buttonSound,.55f);return true;
        }
        void UpdateCourtyardDetails(float dt)
        {
            if(city==null)return;
            city.volume=.24f*Mathf.Clamp01(elapsed/3)*musicFade;
            vendingAudio.volume=musicFade;
            for(int i=0;i<maples.Count;i++)
            {
                float phase=Mathf.Repeat(elapsed*(.043f+i*.003f)+i/6f,1);
                var leaf=maples[i];
                float arc=phase*Mathf.PI*6+i*1.7f;
                float x=-14+i*5.5f-phase*3+Mathf.Sin(arc)*1.5f+Mathf.Sin(arc*.47f+i)*.9f;
                float y=10-phase*21+Mathf.Sin(arc+.8f)*1.25f;
                leaf.localPosition=new Vector3(x,y,-6);
                leaf.localRotation=Quaternion.Euler(0,0,i*41+phase*210+Mathf.Sin(phase*12+i)*20);
                float size=1.3f+(i%3)*.45f;
                leaf.localScale=new Vector3(size*(.35f+.65f*Mathf.Abs(Mathf.Cos(phase*10+i))),size,1);
            }
            if(vendTime>=0)
            {
                float before=vendTime;vendTime+=dt;
                if(before<.08f&&vendTime>=.08f)vendingAudio.PlayOneShot(coinSound,.8f);
                if(before<.7f&&vendTime>=.7f)vendingAudio.PlayOneShot(motorSound,.55f);
                if(before<2.1f&&vendTime>=2.1f)
                {vendingAudio.Stop();vendingAudio.PlayOneShot(dropSound,.75f);drink.gameObject.SetActive(true);}
                if(vendTime>=2.1f)
                {
                    float t=Mathf.Clamp01((vendTime-2.1f)/.4f);
                    drink.localPosition=new Vector3(VendingX,-3.65f-.35f*t+Mathf.Sin(t*Mathf.PI)*.08f,-2);
                }
                if(vendTime>=2.5f){vendTime=-1;drinkReady=true;}
            }
        }
        static AudioClip CreateCityAmbience()
        {
            // Synthesized distant traffic, overlapping indistinct crowd voices and occasional horns.
            // A seam crossfade makes the stereo bed loop without clicks.
            const int rate=22050,seconds=24,fade=22050;
            int count=rate*seconds;var data=new float[(count+fade)*2];
            var rng=new System.Random(927);float low=0;
            for(int i=0;i<count+fade;i++)
            {
                float t=(float)i/rate,n=(float)rng.NextDouble()*2-1;
                low=Mathf.Lerp(low,n,.018f);
                float traffic=low*(.65f+.35f*Mathf.Sin(t*.47f))+.025f*Mathf.Sin(t*2*Mathf.PI*67);
                float l=traffic,r=traffic;
                for(int v=0;v<5;v++)
                {
                    float envelope=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*(1.1f+v*.13f)+v*2)),3);
                    float syllable=.5f+.5f*Mathf.Sin(t*(12+v)+v);
                    float voice=Mathf.Sin(2*Mathf.PI*((130+v*31)*t+Mathf.Sin(t*.8f+v)*3));
                    voice+=.35f*Mathf.Sin(2*Mathf.PI*(370+v*49)*t);
                    float murmur=voice*envelope*syllable*.012f;
                    l+=murmur*(v%2==0?1:.4f);r+=murmur*(v%2==0?.4f:1);
                }
                float hornTime=Mathf.Repeat(t+5,17);
                float horn=hornTime<.7f?Mathf.Sin(hornTime/.7f*Mathf.PI)*.014f*(Mathf.Sin(t*2*Mathf.PI*349)+Mathf.Sin(t*2*Mathf.PI*440)):0;
                data[i*2]=l+horn;data[i*2+1]=r+horn*.5f;
            }
            for(int i=0;i<fade;i++)for(int c=0;c<2;c++)
                data[i*2+c]=Mathf.Lerp(data[(count+i)*2+c],data[i*2+c],(float)i/fade);
            var loop=new float[count*2];System.Array.Copy(data,loop,loop.Length);
            var clip=AudioClip.Create("Dusk city / traffic and distant crowd",count,2,rate,false);clip.SetData(loop,0);return clip;
        }
    }
}
