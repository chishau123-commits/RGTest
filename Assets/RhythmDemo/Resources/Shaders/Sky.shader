Shader "GeometryRhythm/Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos:SV_POSITION; float3 direction:TEXCOORD0; };
            v2f vert(float4 v:POSITION) { v2f o; o.pos=UnityObjectToClipPos(v); o.direction=v.xyz; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float q=saturate(normalize(i.direction).y*.8+.5);
                return fixed4(lerp(float3(.86,.82,.76),float3(1,.976,.925),q),1);
            }
            ENDCG
        }
    }
}
