Shader "GeometryRhythm/Note"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; float shade:TEXCOORD0; };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex);
                o.shade=.88+.12*abs(v.normal.z); return o;
            }
            // Preserve the physical rim without fog washing out readable note colors.
            fixed4 frag(v2f i):SV_Target { fixed4 c=_Color; c.rgb*=i.shade; return c; }
            ENDCG
        }
    }
}
