using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Presentation tokens only; no note rendering or judgement dependencies.</summary>
    public static class CyberTheme
    {
        public static readonly Color Background=new Color(.023f,.039f,.070f);
        public static readonly Color Panel=new Color(.044f,.080f,.125f);
        public static readonly Color PanelLight=new Color(.070f,.130f,.185f);
        public static readonly Color Cyan=new Color(.29f,.93f,.96f);
        public static readonly Color Purple=new Color(.61f,.39f,1f);
        public static readonly Color Lime=new Color(.86f,.97f,.40f);
        public static readonly Color Pink=new Color(1f,.35f,.57f);
        public static readonly Color Text=new Color(.90f,.95f,1f);
        public static readonly Color Muted=new Color(.44f,.59f,.69f);
        static Font bold,regular,mono;
        public static Font Bold => bold!=null?bold:bold=Load("Rajdhani-Bold");
        public static Font Regular => regular!=null?regular:regular=Load("Rajdhani-Medium");
        public static Font Mono => mono!=null?mono:mono=Load("ShareTechMono-Regular");
        static Font Load(string name) => Resources.Load<Font>("Fonts/"+name)??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Color Alpha(Color c,float alpha) {c.a=alpha;return c;}
        // Local canvas-space mesh helpers retain crisp edges at different resolutions.
        public static void Quad(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int i=v.currentVertCount;v.AddVert(a,color,Vector2.zero);v.AddVert(b,color,Vector2.zero);
            v.AddVert(c,color,Vector2.zero);v.AddVert(d,color,Vector2.zero);v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);
        }
        public static void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 dir=b-a,n=new Vector2(-dir.y,dir.x).normalized*width*.5f;Quad(v,a+n,b+n,b-n,a-n,color);
        }
        public static void Polygon(VertexHelper v,Vector2[] points,Color color)
        {
            int start=v.currentVertCount;foreach(var p in points) v.AddVert(p,color,Vector2.zero);
            for(int i=1;i<points.Length-1;i++) v.AddTriangle(start,start+i,start+i+1);
        }
    }
}
