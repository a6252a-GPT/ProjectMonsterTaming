Shader "ProjectMT/TreasureSpirit/DistanceFog"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.04, 0.035, 0.03, 0.58)
        _ClearRadius ("Clear Radius", Float) = 7.25
        _FadeDistance ("Fade Distance", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry+400"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            float _ClearRadius;
            float _FadeDistance;
            float4 _PlayerPos;
            int _LightCount;
            float4 _Lights[16];

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = world.xyz;
                o.vertex = mul(UNITY_MATRIX_VP, world);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float dist = distance(i.worldPos.xz, _PlayerPos.xz);
                float fog = smoothstep(_ClearRadius, _ClearRadius + max(0.01, _FadeDistance), dist);

                float torchClear = 0;
                for (int n = 0; n < 16; n++)
                {
                    float hole = _Lights[n].w;
                    if (hole < 0.01)
                    {
                        continue;
                    }

                    float torchDist = distance(i.worldPos.xz, _Lights[n].xz);
                    torchClear = max(torchClear, 1.0 - smoothstep(hole * 0.45, hole, torchDist));
                }

                fog *= 1.0 - torchClear;
                return fixed4(_BaseColor.rgb, _BaseColor.a * fog);
            }
            ENDCG
        }
    }

    FallBack Off
}
