Shader "GeometryRhythm/Flat"
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
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; UNITY_FOG_COORDS(0) float shade:TEXCOORD1; };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex);
                // The face stays flat-colored; only the thin physical rim is slightly darker.
                o.shade=.88+.12*abs(v.normal.z); UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            fixed4 frag(v2f i):SV_Target { fixed4 c=_Color; c.rgb*=i.shade; UNITY_APPLY_FOG(i.fogCoord,c); return c; }
            ENDCG
        }
    }
}
