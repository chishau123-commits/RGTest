Shader "Thart/Unlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Emission ("Emission", Range(0,1)) = 0
        _FadeColor ("Fade Color", Color) = (0.035,0.04,0.058,1)
        _FadeNear ("Fade Near", Float) = 12
        _FadeFar ("Fade Far", Float) = 40
        _FadeStrength ("Fade Strength", Range(0,1)) = 1
        // 沿 Z 轴重复的暗带：隧道壁/地板上形成向消失点收敛的横纹，是最强的纵深线索
        _StripeFreq ("Stripe Freq", Float) = 0
        _StripeDim ("Stripe Dim", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Emission;
            fixed4 _FadeColor;
            float _FadeNear;
            float _FadeFar;
            float _FadeStrength;
            float _StripeFreq;
            float _StripeDim;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float depth : TEXCOORD0;
                float worldZ : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.depth = -UnityObjectToViewPos(v.vertex).z;
                o.worldZ = mul(unity_ObjectToWorld, v.vertex).z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = _Color + _Emission;

                // 横纹：沿隧道方向一亮一暗，读起来就是一段段向远处退去的路面
                if (_StripeFreq > 0.0001)
                {
                    float band = frac(i.worldZ * _StripeFreq);
                    c.rgb *= lerp(1.0, _StripeDim, step(0.5, band));
                }

                // 按视深度溶进背景：远端几何体自然消失，隧道才有纵深
                float f = saturate((i.depth - _FadeNear) / max(0.001, _FadeFar - _FadeNear));
                f = f * f * (3.0 - 2.0 * f) * _FadeStrength;

                c.rgb = lerp(c.rgb, _FadeColor.rgb, f);
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}