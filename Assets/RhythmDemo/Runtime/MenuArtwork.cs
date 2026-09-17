using UnityEngine;
using UnityEngine.UI;
namespace GeometryRhythm
{
    /// <summary>Original vector album illustration: a signal core. Menu decoration, NOT a Note skin.
    /// UGUI caches the mesh; no per-frame mesh generation.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuArtwork : MaskableGraphic
    {
        Vector2 center;float scale;
        Vector2 At(float radius,float degrees)=>center+new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad),Mathf.Sin(degrees*Mathf.Deg2Rad))*radius*scale;
        void Arc(VertexHelper v,float radius,float thickness,float start,float end,Color color)
        {
            int count=Mathf.Max(2,Mathf.CeilToInt((end-start)/3));
            for(int i=0;i<count;i++){float a=Mathf.Lerp(start,end,i/(float)count),b=Mathf.Lerp(start,end,(i+1f)/count);
                CyberTheme.Quad(v,At(radius,a),At(radius,b),At(radius-thickness,b),At(radius-thickness,a),color);}
        }
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();var r=rectTransform.rect;center=r.center;scale=Mathf.Min(r.width,r.height)/500;
            for(int i=0;i<80;i++)
            {
                int n=v.currentVertCount;v.AddVert(center,new Color(.06f,.33f,.39f,.52f),Vector2.zero);
                v.AddVert(At(245,i*4.5f),new Color(.01f,.06f,.11f,0),Vector2.zero);v.AddVert(At(245,(i+1)*4.5f),new Color(.01f,.06f,.11f,0),Vector2.zero);v.AddTriangle(n,n+1,n+2);
            }
            var dim=CyberTheme.Alpha(CyberTheme.Cyan,.24f);
            Arc(v,224,1,0,360,dim);Arc(v,213,1,0,360,CyberTheme.Alpha(CyberTheme.Purple,.3f));
            for(int i=0;i<120;i++) CyberTheme.Line(v,At(221,i*3),At(i%5==0?231:225,i*3),scale*(i%5==0?2:1),i%5==0?CyberTheme.Muted:dim);
            Arc(v,198,13,18,135,CyberTheme.Cyan);Arc(v,198,13,157,241,CyberTheme.Purple);Arc(v,198,13,253,342,CyberTheme.Alpha(CyberTheme.Cyan,.32f));
            Arc(v,180,2,0,360,CyberTheme.Alpha(CyberTheme.Cyan,.65f));
            Arc(v,174,19,12,98,CyberTheme.PanelLight);Arc(v,174,19,106,217,CyberTheme.PanelLight);Arc(v,174,19,227,350,CyberTheme.PanelLight);
            for(int i=0;i<36;i++)Arc(v,148,i%3==0?10:4,i*10,i*10+5,i<22?CyberTheme.Alpha(CyberTheme.Cyan,.6f):CyberTheme.Purple);
            Arc(v,127,1,0,360,dim);Arc(v,109,2,22,164,CyberTheme.Purple);Arc(v,109,2,193,335,CyberTheme.Cyan);
            for(int layer=0;layer<3;layer++)
            {
                Color c=layer==0?CyberTheme.Cyan:CyberTheme.Alpha(layer==1?CyberTheme.Purple:CyberTheme.Cyan,.55f);
                float radius=72-layer*12,rotation=90+layer*28;
                for(int i=0;i<3;i++)CyberTheme.Line(v,At(radius,rotation+i*120),At(radius,rotation+(i+1)*120),scale*2,c);
            }
            CyberTheme.Line(v,center+new Vector2(-248,0)*scale,center+new Vector2(-209,0)*scale,scale,dim);
            CyberTheme.Line(v,center+new Vector2(209,0)*scale,center+new Vector2(248,0)*scale,scale,dim);
            for(int i=0;i<27;i++)
            {
                float x=(i-13)*9,y=-255,h=3+Mathf.Abs(Mathf.Sin(i*1.37f))*14;
                CyberTheme.Line(v,center+new Vector2(x,y)*scale,center+new Vector2(x,y+h)*scale,scale*3,i<18?CyberTheme.Cyan:CyberTheme.Purple);
            }
        }
    }
}
