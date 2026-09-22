using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Small contact-add mark used by the Friends window.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FriendRequestIconGraphic : MaskableGraphic
    {
        Vector2 P(float x,float y)
        {
            var r=rectTransform.rect;
            return new Vector2(r.xMin+x*r.width/30f,r.yMax-y*r.height/26f);
        }

        void Ring(VertexHelper v,Vector2 center,float radius,Color color,int segments=16)
        {
            Vector2 previous=center+Vector2.right*radius;
            for(int i=1;i<=segments;i++)
            {
                float a=i/(float)segments*Mathf.PI*2;
                Vector2 current=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                CyberTheme.Line(v,previous,current,1.5f,color);previous=current;
            }
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();
            Color ink=color;
            Ring(v,P(11,9),5.2f,ink);
            CyberTheme.Line(v,P(3,22),P(6,15),1.5f,ink);
            CyberTheme.Line(v,P(6,15),P(11,13),1.5f,ink);
            CyberTheme.Line(v,P(11,13),P(16,15),1.5f,ink);
            CyberTheme.Line(v,P(16,15),P(19,22),1.5f,ink);
            CyberTheme.Line(v,P(22,6),P(28,6),2f,CyberTheme.Pink);
            CyberTheme.Line(v,P(25,3),P(25,9),2f,CyberTheme.Pink);
        }
    }
}
