Shader "GeometryRhythm/Backdrop"
{
    // Black, not white: a video only gets its texture once VideoPlayer delivers a frame, and a
    // white default would flash the whole screen white until then.
    Properties { _MainTex ("Texture", 2D) = "black" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        // A picture of a place, not a surface in one: the background queue draws it before any
        // geometry, ZWrite off keeps it out of the depth buffer so every note still draws over
        // it, and no fog pragma means the stage fog never reaches it however far away it looks.
        Tags { "Queue"="Background" "RenderType"="Background" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o; }
            // Opaque by construction: the backdrop replaces the sky, so a source with a stray
            // alpha channel must not blend through to the camera's clear colour.
            fixed4 frag(v2f i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv)*_Color; c.a=1; return c; }
            ENDCG
        }
    }
}
