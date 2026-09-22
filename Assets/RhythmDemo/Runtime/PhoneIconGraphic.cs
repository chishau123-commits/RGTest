using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Small phone mark used by the MainUI friend-system entry.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PhoneIconGraphic : MaskableGraphic
    {
        Vector2 P(float x,float y)
        {
            var r=rectTransform.rect;
            return new Vector2(r.xMin+x*r.width/26f,r.yMax-y*r.height/30f);
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();
            Color ink=color;
            var body=new[]{P(4,1),P(22,1),P(23,3),P(23,27),P(22,29),P(4,29),P(3,27),P(3,3)};
            CyberTheme.Polygon(v,body,new Color(ink.r,ink.g,ink.b,.14f));
            for(int i=0;i<body.Length;i++)CyberTheme.Line(v,body[i],body[(i+1)%body.Length],1.5f,ink);
            CyberTheme.Line(v,P(9,5),P(17,5),1.2f,ink);
            CyberTheme.Line(v,P(9,25),P(17,25),1.2f,ink);
            for(int i=0;i<3;i++)
            {
                float x=8+i*4;
                CyberTheme.Line(v,P(x,14),P(x,20-i*2),i==2?2.2f:1.4f,i==2?CyberTheme.Pink:ink);
            }
        }
    }
}
