using UnityEngine;
using UnityEngine.UI;
namespace GeometryRhythm
{
    /// <summary>Cut-corner panel. Button tint changes its fill, not its border.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberPanelGraphic : MaskableGraphic
    {
        public Color accent=CyberTheme.Cyan;
        public float cut=16;
        public bool outline=true;
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();var r=rectTransform.rect;float c=Mathf.Min(cut,Mathf.Min(r.width,r.height)*.3f);
            var p=new[]{new Vector2(r.xMin+c,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin+c),
                new Vector2(r.xMax-c,r.yMin),new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax-c)};
            CyberTheme.Polygon(v,p,color);if(!outline)return;
            for(int i=0;i<p.Length;i++) CyberTheme.Line(v,p[i],p[(i+1)%p.Length],1,CyberTheme.Alpha(accent,.32f));
            CyberTheme.Line(v,p[5],p[0],2.5f,accent);CyberTheme.Line(v,p[2],p[3],2.5f,accent);
            CyberTheme.Line(v,p[0],p[0]+Vector2.right*Mathf.Min(48,r.width*.2f),2,accent);
        }
    }
}
