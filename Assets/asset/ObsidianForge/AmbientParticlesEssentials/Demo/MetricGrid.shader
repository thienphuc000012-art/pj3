Shader "Obsidian Forge/Demo Metric Grid"
{
    Properties
    {
        _BaseColor("Line color",Color) = (.42,.5,.6,1)
        _FloorColor("Floor color",Color) = (.035,.048,.067,1)
        _HorizonColor("Horizon color",Color) = (.012,.02,.035,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        ZWrite On
        Cull Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/Shared/AmbientVisualCore.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half4 _FloorColor; half4 _HorizonColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 world = TransformObjectToWorld(input.positionOS.xyz);
    
    output.world = world;
    output.positionCS = TransformWorldToHClip(world);
    
    return output;
}
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return AmbientGridColor(input.world,GetCameraPositionWS(),_FloorColor,_BaseColor,_HorizonColor);
            }
            ENDHLSL
        }
    }
}
