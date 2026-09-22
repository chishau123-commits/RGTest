using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Small newspaper mark used by the MainUI entry button.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NewsIconGraphic : MaskableGraphic
    {
        Vector2 P(float x,float y)
        {
            var r=rectTransform.rect;
            return new Vector2(r.xMin+x*r.width/28f,r.yMax-y*r.height/24f);
        }

        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();
            Color ink=color;
            var paper=new[]{
                P(1,3),P(20,3),P(27,8),P(27,22),P(1,22)
            };
            CyberTheme.Polygon(v,paper,new Color(ink.r,ink.g,ink.b,.16f));
            CyberTheme.Line(v,paper[0],paper[1],1.6f,ink);
            CyberTheme.Line(v,paper[1],paper[2],1.6f,ink);
            CyberTheme.Line(v,paper[2],paper[3],1.6f,ink);
            CyberTheme.Line(v,paper[3],paper[4],1.6f,ink);
            CyberTheme.Line(v,paper[4],paper[0],1.6f,ink);
            CyberTheme.Line(v,P(20,3),P(20,8),1.4f,ink);
            CyberTheme.Line(v,P(20,8),P(27,8),1.4f,ink);
            for(int i=0;i<4;i++)CyberTheme.Line(v,P(5,10+i*3),P(13,10+i*3),1.2f,ink);
            CyberTheme.Line(v,P(16,11),P(22,11),1.2f,ink);
            CyberTheme.Line(v,P(16,15),P(22,15),1.2f,ink);
            CyberTheme.Line(v,P(16,19),P(22,19),1.2f,ink);
        }
    }
}
