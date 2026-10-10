Shader "GeometryRhythm/DuskSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer tint", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _SunExposure ("Ground sunlight", Range(0,1)) = 1
        _Lighting ("Receive sunset light", Range(0,1)) = 1
        _DistanceHaze ("Distant sunset haze", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float4 _MainTex_TexelSize;
            float _SunExposure, _Lighting, _DistanceHaze;
            struct dusk_v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float worldX:TEXCOORD1; };
            dusk_v2f Vert(appdata_t v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                dusk_v2f o;
                float4 local=UnityFlipSprite(v.vertex,_Flip);
                o.vertex=UnityObjectToClipPos(local);
                o.worldX=mul(unity_ObjectToWorld,local).x;
                o.uv=v.texcoord; o.color=v.color*_Color*_RendererColor;
                return o;
            }
            fixed4 Frag(dusk_v2f i):SV_Target
            {
                fixed4 c=SampleSpriteTexture(i.uv)*i.color;
                // One authored pixel, never a screen-resolution glossy outline.
                float2 pixel=_MainTex_TexelSize.xy;
                float left=SampleSpriteTexture(i.uv-float2(pixel.x,0)).a;
                float above=SampleSpriteTexture(i.uv+float2(0,pixel.y)).a;
                float right=SampleSpriteTexture(i.uv+float2(pixel.x,0)).a;
                float sunEdge=max(1-left,(1-above)*.65);
                float backEdge=(1-right)*(1-sunEdge);
                float sun=saturate(_SunExposure);
                float3 shade=c.rgb*float3(.72,.73,.94);
                float3 lit=c.rgb*float3(1.13,.99,.80);
                float3 body=lerp(shade,lit,sun*.72);
                body+=float3(.24,.125,.035)*sunEdge*sun;
                body*=1-backEdge*.14;
                c.rgb=lerp(c.rgb,body,_Lighting);
                float3 haze=lerp(float3(.83,.49,.32),float3(.59,.43,.49),saturate((i.worldX+12)/24));
                c.rgb=lerp(c.rgb,haze,_DistanceHaze);
                c.rgb*=c.a;
                return c;
            }
            ENDCG
        }
    }
}
