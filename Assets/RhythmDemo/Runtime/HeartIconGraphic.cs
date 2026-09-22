using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Small heart mark used by the News like action.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HeartIconGraphic : MaskableGraphic
    {
        Vector2 P(float x,float y)
        {
            var r=rectTransform.rect;
            return new Vector2(r.xMin+x*r.width/28f,r.yMax-y*r.height/24f);
        }

        void Disc(VertexHelper v,Vector2 center,float radius,Color color,int segments=18)
        {
            int start=v.currentVertCount;
            v.AddVert(center,color,Vector2.zero);
            for(int i=0;i<=segments;i++)
            {
                float a=i/(float)segments*Mathf.PI*2;
                v.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);
            }
            for(int i=0;i<segments;i++)v.AddTriangle(start,start+i+1,start+i+2);
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();
            Vector2 left=P(9,8),right=P(19,8),tip=P(14,23),notch=P(14,8);
            Color fill=new Color(color.r,color.g,color.b,.32f);
            Disc(v,left,8f,fill);
            Disc(v,right,8f,fill);
            CyberTheme.Polygon(v,new[]{P(2,9),P(26,9),tip},fill);
            var heart=new[]{
                notch,P(11,4),P(8,2),P(4,5),P(2,10),P(3,15),P(7,19),tip,
                P(21,19),P(25,15),P(26,10),P(24,5),P(20,2),P(17,4)
            };
            for(int i=0;i<heart.Length;i++)CyberTheme.Line(v,heart[i],heart[(i+1)%heart.Length],1.35f,color);
        }
    }
}
