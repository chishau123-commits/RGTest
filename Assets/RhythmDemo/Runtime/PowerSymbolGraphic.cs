using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    // Vector power symbol, independent of font glyph coverage.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PowerSymbolGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r=rectTransform.rect;Vector2 center=r.center;
            float radius=Mathf.Min(r.width,r.height)*.36f,stroke=radius*.22f;
            for(int i=0;i<40;i++)
            {
                float a=(45+270f*i/40)*Mathf.Deg2Rad,b=(45+270f*(i+1)/40)*Mathf.Deg2Rad;
                Line(vh,center+new Vector2(Mathf.Sin(a),Mathf.Cos(a))*radius,
                    center+new Vector2(Mathf.Sin(b),Mathf.Cos(b))*radius,stroke);
            }
            Line(vh,center+Vector2.up*radius*1.2f,center+Vector2.up*radius*.1f,stroke);
        }
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width)
        {
            Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width*.5f;
            int start=vh.currentVertCount;
            vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);
            vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
    }
}
