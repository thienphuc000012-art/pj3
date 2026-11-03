Shader "Adventure/Exploration Scan"
{
    Properties
    {
        [HDR] _ScanColor ("Scan Color", Color) = (0.06, 0.72, 1, 1)
        [HideInInspector] _ScanOriginRadius ("Origin / Radius", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScanWaveParams ("Width / Intensity / Trail / Grid Scale", Vector) = (1, 2, 8, 1)
        [HideInInspector] _ScanDetailParams ("Grid / Edges / Tint / Scanline Density", Vector) = (0.2, 0.5, 0.03, 8)
        [HideInInspector] _ScanLineParams ("Scanline Strength / Overall Strength", Vector) = (0.2, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Exploration Scan Overlay"
            Cull Off ZWrite Off ZTest Always
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ScanColor;
                float4 _ScanOriginRadius;
                float4 _ScanWaveParams;
                float4 _ScanDetailParams;
                float4 _ScanLineParams;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            float WorldGrid(float3 positionWS)
            {
                float3 coordinates = positionWS * _ScanWaveParams.w;
                float3 gridDistance = abs(frac(coordinates + 0.5) - 0.5);
                float3 footprint = max(fwidth(coordinates), 0.0001);
                float3 gridLines = 1.0 - smoothstep(0.35, 1.35, gridDistance / footprint);
                float3 normal = abs(normalize(cross(ddx(positionWS), ddy(positionWS)) + 0.000001));
                normal /= max(normal.x + normal.y + normal.z, 0.0001);
                return dot(float3(max(gridLines.y, gridLines.z), max(gridLines.x, gridLines.z), max(gridLines.x, gridLines.y)), normal);
            }

            float DepthEdges(float2 uv, float rawDepth)
            {
                float2 pixel = rcp(_ScaledScreenParams.xy) * 1.5;
                float centerDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float right = LinearEyeDepth(SampleSceneDepth(saturate(uv + float2(pixel.x, 0))), _ZBufferParams);
                float up = LinearEyeDepth(SampleSceneDepth(saturate(uv + float2(0, pixel.y))), _ZBufferParams);
                float difference = max(abs(centerDepth - right), abs(centerDepth - up));
                return smoothstep(0.003, 0.025, difference / max(centerDepth, 1.0));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.000001) return 0;
                    float reconstructionDepth = rawDepth;
                #else
                    if (rawDepth >= 0.999999) return 0;
                    float reconstructionDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv, reconstructionDepth, UNITY_MATRIX_I_VP);
                float distanceFromOrigin = distance(positionWS, _ScanOriginRadius.xyz);
                float behindWave = _ScanOriginRadius.w - distanceFromOrigin;
                float band = 1.0 - smoothstep(0.0, _ScanWaveParams.x, abs(behindWave));
                float trail = step(0.0, behindWave) * saturate(1.0 - behindWave / _ScanWaveParams.z);
                trail *= trail;

                float grid = WorldGrid(positionWS);
                float edges = DepthEdges(uv, rawDepth);
                float scanline = 0.65 + 0.35 * sin(positionWS.y * _ScanDetailParams.w * TWO_PI - _Time.y * 4.0);
                scanline = lerp(1.0, scanline, _ScanLineParams.x);
                float reveal = _ScanDetailParams.z + grid * _ScanDetailParams.x + edges * _ScanDetailParams.y;
                float emission = band * _ScanWaveParams.y + trail * reveal * scanline;
                return half4(_ScanColor.rgb * emission * _ScanLineParams.y, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
