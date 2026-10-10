using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Login-only pixel sky. Shares the desktop palette, with independent artwork.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberTitleGraphic : MaskableGraphic
    {
        Vector2 P(float x,float y)
        {
            var r=rectTransform.rect;
            return new Vector2(r.xMin+x*r.width/1600f,r.yMax-y*r.height/900f);
        }

        void Block(VertexHelper v,float x,float y,float width,float height,Color color)
        {
            CyberTheme.Quad(v,P(x,y),P(x+width,y),P(x+width,y+height),P(x,y+height),color);
        }

        void Spark(VertexHelper v,float x,float y,Color color)
        {
            Block(v,x+6,y,3,15,color);
            Block(v,x,y+6,6,3,color);
            Block(v,x+9,y+6,6,3,color);
        }

        void Bank(VertexHelper v,Color color,bool upper,params float[] edge)
        {
            // Disjoint rectangular strips keep concave stair silhouettes free of fan artifacts.
            for(int i=0;i<edge.Length-2;i+=2)
                Block(v,edge[i],upper?0:edge[i+1],edge[i+2]-edge[i],
                    upper?edge[i+1]:900-edge[i+1],color);
        }

        void Signal(VertexHelper v,Color color,params float[] xy)
        {
            for(int i=2;i<xy.Length;i+=2)
                Block(v,Mathf.Min(xy[i-2],xy[i]),Mathf.Min(xy[i-1],xy[i+1]),
                    Mathf.Max(3,Mathf.Abs(xy[i]-xy[i-2])),Mathf.Max(3,Mathf.Abs(xy[i+1]-xy[i-1])),color);
            Block(v,xy[xy.Length-2]-3,xy[xy.Length-1]-3,9,9,color);
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();
            v.AddVert(P(0,0),new Color(.76f,.73f,.91f),Vector2.zero);
            v.AddVert(P(1600,0),new Color(.69f,.65f,.86f),Vector2.zero);
            v.AddVert(P(1600,900),new Color(.88f,.82f,.95f),Vector2.zero);
            v.AddVert(P(0,900),new Color(.84f,.86f,.97f),Vector2.zero);
            v.AddTriangle(0,1,2);v.AddTriangle(0,2,3);

            // Pixel cloud banks frame the quiet product and account area.
            Bank(v,new Color(.91f,.87f,1f,.55f),true,
                0,156,144,126,336,96,516,66,672,36,816,0);
            Block(v,1512,72,88,24,new Color(.97f,.91f,1f,.33f));
            Block(v,1374,96,226,24,new Color(.97f,.91f,1f,.33f));
            Block(v,1236,120,364,24,new Color(.97f,.91f,1f,.33f));
            Block(v,1128,144,472,24,new Color(.97f,.91f,1f,.33f));
            Block(v,1056,168,544,18,new Color(.97f,.91f,1f,.33f));
            Block(v,1128,168,472,3,new Color(1f,.94f,1f,.23f));

            Color blue=new Color(.74f,.88f,.96f,.46f);
            Block(v,0,330,42,24,blue);Block(v,0,354,90,36,blue);
            Block(v,0,390,132,42,blue);Block(v,0,432,174,42,blue);
            Block(v,0,474,210,54,blue);Block(v,0,528,174,24,blue);
            Color rose=new Color(.96f,.77f,.89f,.37f);
            Block(v,1540,378,60,42,rose);Block(v,1504,420,96,48,rose);
            Block(v,1474,468,126,72,rose);Block(v,1444,540,156,54,rose);
            Block(v,1474,594,126,30,rose);

            // Three stepped horizons replace the desktop's circles and all-over grid.
            Bank(v,new Color(.65f,.65f,.86f,.22f),false,
                0,672,96,696,240,720,444,744,642,768,984,744,1176,714,
                1356,678,1488,642,1600,642);
            Bank(v,new Color(.75f,.71f,.91f,.54f),false,
                0,732,144,750,318,774,522,798,936,780,1128,756,
                1332,726,1488,702,1600,702);
            Bank(v,new Color(.60f,.55f,.79f,.18f),false,
                0,786,180,804,420,822,1140,804,1350,780,1488,762,1600,762);

            // Fixed, sparse dither clusters keep the cloud texture understated.
            Color dither=new Color(.96f,.94f,1f,.27f);
            for(int i=0;i<25;i++)
            {
                float x=24+i*66,y=744+(i%5)*12;
                Block(v,x,y,3,3,dither);Block(v,x+6,y+6,3,3,dither);
                if(i%3==0)Block(v,x+12,y,3,3,dither);
            }

            Signal(v,new Color(.49f,.39f,.73f,.22f),0,210,84,210,84,186,240,186);
            Signal(v,new Color(.97f,.94f,1f,.42f),0,222,96,222,96,198,216,198);
            Signal(v,new Color(.49f,.39f,.73f,.22f),1600,252,1518,252,1518,216,1440,216);
            Signal(v,new Color(.96f,.92f,1f,.48f),1600,738,1542,738,1542,768,1392,768);
            Signal(v,new Color(.44f,.47f,.73f,.22f),0,696,60,696,60,732,162,732);

            Spark(v,276,224,new Color(.99f,.96f,1f,.78f));
            Spark(v,636,342,new Color(.98f,.95f,1f,.66f));
            Spark(v,696,126,new Color(.91f,.96f,1f,.62f));
            Spark(v,986,102,new Color(.98f,.94f,1f,.60f));
            Spark(v,1494,324,new Color(.97f,.94f,1f,.70f));
            Block(v,600,198,6,6,new Color(.48f,.36f,.73f,.26f));
            Block(v,570,222,3,3,new Color(.48f,.36f,.73f,.26f));
            Block(v,1476,156,6,6,new Color(.97f,.91f,1f,.54f));
        }
    }
}
