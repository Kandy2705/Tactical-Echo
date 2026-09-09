Shader "TacticalEcho/SimpleWater"
{
    Properties
    {
        _Color("Water Color", Color) = (0.12, 0.42, 0.58, 0.72)
        _DeepColor("Deep Color", Color) = (0.03, 0.12, 0.2, 0.88)
        _Smoothness("Smoothness", Range(0,1)) = 0.9
        _WaveScale("Wave Scale", Float) = 0.08
        _WaveSpeed("Wave Speed", Float) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _DeepColor;
                float _Smoothness;
                float _WaveScale;
                float _WaveSpeed;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float wave = sin((positionWS.x + positionWS.z) * 0.18 + _Time.y * _WaveSpeed) * _WaveScale;
                positionWS.y += wave;
                output.positionWS = positionWS;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), viewDir)), 3.0);
                float3 color = lerp(_Color.rgb, _DeepColor.rgb, 0.35) + fresnel * 0.22;
                float alpha = saturate(lerp(_Color.a, _DeepColor.a, fresnel));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
