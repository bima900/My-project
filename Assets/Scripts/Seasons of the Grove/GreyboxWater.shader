// Shader air sederhana (URP, unlit, transparan). Pengganti sementara sebelum Shader Graph + Scene Depth.
Shader "Greybox/Water"
{
    Properties
    {
        _ShallowColor("Shallow", Color) = (0.30, 0.78, 0.85, 1)
        _DeepColor("Deep", Color) = (0.05, 0.30, 0.50, 1)
        _Alpha("Alpha", Range(0,1)) = 0.78
        _Scale("Wave Scale", Float) = 0.35
        _Speed("Wave Speed", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _Alpha;
                float _Scale;
                float _Speed;
            CBUFFER_END

            struct Attributes { float4 posOS : POSITION; };
            struct Varyings { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.posWS = TransformObjectToWorld(i.posOS.xyz);
                o.posCS = TransformWorldToHClip(o.posWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.posWS.xz * _Scale;
                float t = _Time.y * _Speed;
                float w = sin(uv.x * 2.0 + t) + sin(uv.y * 2.3 - t * 1.3) + sin((uv.x + uv.y) * 1.7 + t * 0.7);
                w = w / 6.0 + 0.5;
                half3 c = lerp(_DeepColor.rgb, _ShallowColor.rgb, w);
                float sparkle = pow(saturate(sin(uv.x * 7.0 + t * 2.0) * sin(uv.y * 6.0 - t * 1.6)), 12.0);
                c += sparkle * 0.25;
                return half4(c, _Alpha);
            }
            ENDHLSL
        }
    }
}
