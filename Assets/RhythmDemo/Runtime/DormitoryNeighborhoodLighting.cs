using UnityEngine;

namespace GeometryRhythm
{
    public sealed partial class DormitoryDuskScene
    {
        readonly MeshRenderer[] castShadows=new MeshRenderer[4];
        readonly MaterialPropertyBlock[] lightBlocks=new MaterialPropertyBlock[4];
        readonly MaterialPropertyBlock[] shadowBlocks=new MaterialPropertyBlock[4];
        readonly float[] neighborSun=new float[4];
        Color32[] courtyardPixels;
        int courtyardWidth,courtyardHeight;
        Material neighborLight,trainHaze,castShadowMaterial;
        Mesh castShadowMesh;
        static readonly int SunExposureId=Shader.PropertyToID("_SunExposure");
        static readonly int ShadowTextureId=Shader.PropertyToID("_MainTex");
        static readonly int ShadowColorId=Shader.PropertyToID("_Color");

        void BuildNeighborhoodLighting()
        {
            var texture=background.GetComponent<SpriteRenderer>().sprite.texture;
            courtyardPixels=texture.GetPixels32();courtyardWidth=texture.width;courtyardHeight=texture.height;
            var shader=Resources.Load<Shader>("Shaders/DuskSprite");
            if(shader==null)throw new System.InvalidOperationException("Missing dusk sprite lighting shader");
            neighborLight=new Material(shader){name="NPC golden rim / cool shade"};owned.Add(neighborLight);
            trainHaze=new Material(shader){name="Distant railway atmospheric palette"};
            trainHaze.SetFloat("_Lighting",0);trainHaze.SetFloat("_DistanceHaze",.32f);owned.Add(trainHaze);
            castShadowMaterial=new Material(Shader.Find("Sprites/Default")){name="NPC projected ground shadows"};owned.Add(castShadowMaterial);
            castShadowMesh=new Mesh{name="Sunset shadow projected away from upper left"};
            // Start with the same foot-anchored 48px canvas as the character. Project onto
            // the pavement toward the lower right; the source alpha supplies the silhouette.
            var corners=new[]{new Vector2(-1,-2f/24),new Vector2(1,-2f/24),new Vector2(-1,46f/24),new Vector2(1,46f/24)};
            var vertices=new Vector3[4];
            for(int i=0;i<4;i++)vertices[i]=new Vector3(corners[i].x+corners[i].y*.85f,-corners[i].y*.30f,0);
            castShadowMesh.vertices=vertices;castShadowMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
            castShadowMesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};
            castShadowMesh.triangles=new[]{0,2,1,1,2,3};castShadowMesh.RecalculateBounds();owned.Add(castShadowMesh);
            BuildPaintedForegroundOcclusion();
        }

        void BuildNeighborLighting(int index)
        {
            neighbors[index].sharedMaterial=neighborLight;
            lightBlocks[index]=new MaterialPropertyBlock();shadowBlocks[index]=new MaterialPropertyBlock();
            var node=new GameObject(neighbors[index].name+" sunset cast shadow");node.layer=WorldLayer;node.transform.SetParent(transform,false);
            node.AddComponent<MeshFilter>().sharedMesh=castShadowMesh;
            castShadows[index]=node.AddComponent<MeshRenderer>();castShadows[index].sharedMaterial=castShadowMaterial;
            castShadows[index].sortingOrder=index==3?1:4;
            node.SetActive(index==3);
        }

        void UpdateNeighborLighting(int index,Vector2 foot,float dt)
        {
            // Read the existing painted pavement lighting, smoothed over a small footprint.
            // This makes the runners dim under tree/building shade instead of glowing uniformly.
            int cx=Mathf.RoundToInt(foot.x/background.localScale.x*24+courtyardWidth*.5f);
            int cy=Mathf.RoundToInt(foot.y*24+courtyardHeight*.5f);
            float brightness=0,warmth=0;
            for(int dy=-2;dy<=2;dy++)for(int dx=-4;dx<=4;dx++)
            {
                var c=courtyardPixels[Mathf.Clamp(cy+dy,0,courtyardHeight-1)*courtyardWidth+Mathf.Clamp(cx+dx,0,courtyardWidth-1)];
                brightness+=c.r/255f;warmth+=(c.r-c.b)/255f;
            }
            float target=Mathf.Clamp01(Mathf.InverseLerp(.29f,.78f,brightness/45)*.7f+Mathf.InverseLerp(.03f,.36f,warmth/45)*.3f);
            neighborSun[index]=dt<=0?target:Mathf.Lerp(neighborSun[index],target,1-Mathf.Exp(-dt*12));
            var block=lightBlocks[index];block.SetFloat(SunExposureId,neighborSun[index]);
            // A custom property block replaces the renderer's texture binding; retain it
            // explicitly when swapping the runtime-generated animation sprites.
            block.SetTexture(ShadowTextureId,neighbors[index].sprite.texture);
            block.SetVector("_MainTex_TexelSize",new Vector4(1f/48,1f/48,48,48));
            neighbors[index].SetPropertyBlock(block);
            var projected=castShadows[index];projected.gameObject.SetActive(neighbors[index].gameObject.activeSelf);
            projected.transform.localPosition=new Vector3(Pixel(foot.x),Pixel(foot.y),-.7f);
            var shadowBlock=shadowBlocks[index];shadowBlock.SetTexture(ShadowTextureId,neighbors[index].sprite.texture);
            shadowBlock.SetColor(ShadowColorId,new Color(.16f,.105f,.23f,Mathf.Lerp(.075f,.25f,neighborSun[index])));
            projected.SetPropertyBlock(shadowBlock);
        }

        static bool InForegroundPolygon(Vector2 point,Vector2[] polygon)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
            {
                var a=polygon[i];var b=polygon[j];
                if((a.y>point.y)!=(b.y>point.y)&&point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return inside;
        }

        void BuildPaintedForegroundOcclusion()
        {
            // The full background already contains these foreground planters. Reuse its exact
            // baked pixels/UVs above the actors; the separate parallax foliage is farther forward.
            var left=new[]{new Vector2(0,299),new Vector2(6,299),new Vector2(12,305),new Vector2(16,304),
                new Vector2(19,297),new Vector2(23,298),new Vector2(28,305),new Vector2(34,309),
                new Vector2(40,304),new Vector2(44,302),new Vector2(48,307),new Vector2(52,309),
                new Vector2(56,303),new Vector2(60,306),new Vector2(64,311),new Vector2(70,314),
                new Vector2(75,310),new Vector2(80,312),new Vector2(84,310),new Vector2(88,316),
                new Vector2(92,315),new Vector2(96,312),new Vector2(100,317),new Vector2(103,322),
                new Vector2(108,326),new Vector2(112,330),
                new Vector2(112,360),new Vector2(0,360)};
            var right=new[]{new Vector2(640,292),new Vector2(622,301),new Vector2(609,296),new Vector2(594,305),
                new Vector2(580,301),new Vector2(570,308),new Vector2(558,307),new Vector2(550,312),new Vector2(538,313),
                new Vector2(532,320),new Vector2(527,323),new Vector2(526,360),new Vector2(640,360)};
            var pixels=new Color32[courtyardPixels.Length];
            for(int y=0;y<courtyardHeight;y++)for(int x=0;x<courtyardWidth;x++)
            {
                var point=new Vector2((x+.5f)*640/courtyardWidth,360-(y+.5f)*360/courtyardHeight);
                var sample=courtyardPixels[y*courtyardWidth+x];
                if(InForegroundPolygon(point,left)||InForegroundPolygon(point,right))pixels[y*courtyardWidth+x]=sample;
            }
            var texture=new Texture2D(courtyardWidth,courtyardHeight,TextureFormat.RGBA32,false){name="Exact painted planter occlusion",filterMode=FilterMode.Point};
            texture.SetPixels32(pixels);texture.Apply();owned.Add(texture);
            var sprite=Sprite.Create(texture,new Rect(0,0,courtyardWidth,courtyardHeight),new Vector2(.5f,.5f),24,0,SpriteMeshType.FullRect);owned.Add(sprite);
            var node=new GameObject("Painted foreground planters / actor occluder");node.layer=WorldLayer;node.transform.SetParent(background,false);
            var renderer=node.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=20;
        }
    }
}
