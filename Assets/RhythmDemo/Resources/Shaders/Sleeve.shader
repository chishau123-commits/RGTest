Shader "GeometryRhythm/Sleeve"
{
    Properties { _Color ("Color",Color)=(.4,.7,1,.15) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos:SV_POSITION; };
            fixed4 _Color;
            v2f vert(float4 v:POSITION) { v2f o; o.pos=UnityObjectToClipPos(v); return o; }
            fixed4 frag(v2f i):SV_Target { return _Color; }
            ENDCG
        }
    }
}
