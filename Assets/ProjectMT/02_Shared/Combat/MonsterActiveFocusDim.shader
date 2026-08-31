Shader "ProjectMT/UI/MonsterActiveFocusDim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _DimColor ("Dim Color", Color) = (0.012, 0.02, 0.045, 0.38)
        _CasterCenter ("Caster Center", Vector) = (0.5, 0.5, 0, 0)
        _CasterRadius ("Caster Radius", Vector) = (0.1, 0.2, 0, 0)
        _TargetCenter ("Target Center", Vector) = (0.7, 0.5, 0, 0)
        _TargetRadius ("Target Radius", Vector) = (0.08, 0.16, 0, 0)
        _UseTarget ("Use Target", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _DimColor;
            float4 _CasterCenter;
            float4 _CasterRadius;
            float4 _TargetCenter;
            float4 _TargetRadius;
            float _UseTarget;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color;
                return output;
            }

            float SoftHole(float2 uv, float2 center, float2 radius)
            {
                float2 safeRadius = max(radius, float2(0.0001, 0.0001));
                float distanceFromCenter = length((uv - center) / safeRadius);
                return 1.0 - smoothstep(0.72, 1.0, distanceFromCenter);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float casterHole = SoftHole(
                    input.texcoord,
                    _CasterCenter.xy,
                    _CasterRadius.xy);
                float targetHole = SoftHole(
                    input.texcoord,
                    _TargetCenter.xy,
                    _TargetRadius.xy) * saturate(_UseTarget);
                float visibility = 1.0 - max(casterHole, targetHole);
                fixed4 color = _DimColor * input.color;
                color.a *= visibility;
                return color;
            }
            ENDCG
        }
    }
}
