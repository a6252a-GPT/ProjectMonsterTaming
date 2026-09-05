Shader "ProjectMT/Feel/MonsterActiveCasterAccent"
{
    Properties
    {
        _Color ("Accent", Color) = (1,0.85,0.35,1)
        _Intensity ("Intensity", Range(0,2)) = 0
        _RimWeight ("Rim Weight", Range(0,1)) = 1
        _BodyFill ("Body Fill", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _RimWeight;
                half _BodyFill;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 view = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0h - saturate(abs(dot(SafeNormalize(input.normalWS), view))), 1.6h);
                half lineMask = pow(saturate(1.0h - abs(input.uv.y * 2.0h - 1.0h)), 1.5h);
                half alpha = lerp(lineMask, lerp(_BodyFill, 1.0h, rim), _RimWeight) * _Intensity * _Color.a;
                half3 glowColor = lerp(_Color.rgb, half3(1, 1, 1), (1.0h - _RimWeight) * lineMask * 0.55h);
                return half4(glowColor, alpha);
            }
            ENDHLSL
        }
    }
}