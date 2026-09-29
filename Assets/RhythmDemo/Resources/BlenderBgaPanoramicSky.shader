Shader "GeometryRhythm/Blender BGA Panoramic Sky"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "white" {}
        _Rotation ("Rotation", Range(0, 360)) = 0
        _Exposure ("Exposure", Range(0, 8)) = 1
    }
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

            sampler2D _MainTex;
            float _Rotation;
            float _Exposure;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float angle = radians(_Rotation);
                direction.xz = mul(float2x2(cos(angle), -sin(angle), sin(angle), cos(angle)), direction.xz);
                float2 uv = float2(atan2(direction.x, direction.z) / (2.0 * UNITY_PI) + 0.5,
                    asin(clamp(direction.y, -1.0, 1.0)) / UNITY_PI + 0.5);
                return tex2D(_MainTex, uv) * _Exposure;
            }
            ENDCG
        }
    }
}
