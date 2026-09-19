using UnityEngine;
using UnityEngine.UI;
namespace GeometryRhythm
{
    /// <summary>Original circuit frame, generated only on layout rebuild. No fullscreen postprocess.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberFrameGraphic : MaskableGraphic
    {
        Vector2 P(float x,float y) {var r=rectTransform.rect;return new Vector2(r.xMin+x*r.width/1600,r.yMax-y*r.height/900);}
        void L(VertexHelper v,float x,float y,float x2,float y2,Color c,float w=1)=>CyberTheme.Line(v,P(x,y),P(x2,y2),w,c);
        void Plate(VertexHelper v,float x,float y,float width,float height,Color a,Color b,bool flip=false)
        {
            float k=flip?-1:1;int i=v.currentVertCount;
            v.AddVert(P(x+height,y),a,Vector2.zero);v.AddVert(P(x+width,y),b,Vector2.zero);
            v.AddVert(P(x+width-height,y+k*height),b,Vector2.zero);v.AddVert(P(x,y+k*height),a,Vector2.zero);
            v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);
        }
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();var faint=CyberTheme.Alpha(CyberTheme.Secondary,.008f);
            for(int x=32;x<1600;x+=64)L(v,x,130,x,822,faint);
            for(int y=130;y<825;y+=64)L(v,32,y,1568,y,faint);
            Plate(v,0,0,575,15,CyberTheme.Purple,CyberTheme.Cyan);Plate(v,588,0,255,8,CyberTheme.Cyan,CyberTheme.Purple);
            Plate(v,1190,0,410,15,CyberTheme.Cyan,CyberTheme.Purple);
            Plate(v,830,0,264,7,CyberTheme.Purple,CyberTheme.Purple);
            Plate(v,1003,0,66,27,CyberTheme.Purple,CyberTheme.Cyan);
            Plate(v,346,0,124,26,CyberTheme.Cyan,CyberTheme.Cyan);
            Plate(v,494,10,85,20,CyberTheme.Purple,CyberTheme.Purple);
            Plate(v,0,900,360,14,CyberTheme.Purple,CyberTheme.Cyan,true);Plate(v,952,900,648,16,CyberTheme.Cyan,CyberTheme.Purple,true);
            Plate(v,1110,878,187,12,CyberTheme.Purple,CyberTheme.Cyan,true);
            Plate(v,855,900,100,31,CyberTheme.Purple,CyberTheme.Cyan,true);
            Plate(v,274,900,97,25,CyberTheme.Purple,CyberTheme.Purple,true);
            // Only the original edge plates above interpolate colors. All other strokes stay solid blue.
            L(v,32,116,1120,116,CyberTheme.Alpha(CyberTheme.Secondary,.32f));L(v,1120,116,1148,94,CyberTheme.Purple,2);L(v,1148,94,1568,94,CyberTheme.Purple,2);
            L(v,32,840,442,840,CyberTheme.Purple);L(v,442,840,458,856,CyberTheme.Purple);L(v,458,856,967,856,CyberTheme.Alpha(CyberTheme.Purple,.25f));
            L(v,980,840,1568,840,CyberTheme.Alpha(CyberTheme.Secondary,.4f));
            for(int i=0;i<12;i++){float y=290+i*24;L(v,20,y,i%3==0?31:25,y,CyberTheme.Alpha(CyberTheme.Secondary,.28f));L(v,1575,y,1580,y,CyberTheme.Alpha(CyberTheme.Purple,.4f));}
            for(int i=0;i<6;i++)Plate(v,1440+i*18,111,14,5,CyberTheme.Secondary,CyberTheme.Secondary);
        }
    }
}
