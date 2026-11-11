Shader "Obsidian Forge/Ambient Trail"
{
    Properties
    {
        [HideInInspector] _Style("Wind style", Float) = 3
        _Brightness("Brightness", Range(0,64)) = 1
        [HideInInspector] _TrailScale("World scale", Float) = 1
        [HideInInspector] _CameraFade("Camera fade", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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
            #include "Shared/AmbientVisualCore.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Style; float _Brightness; float _TrailScale; float4 _CameraFade;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0;  UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float3 world : TEXCOORD1;  UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 world = TransformObjectToWorld(input.positionOS.xyz);
    
    output.world = world;
    output.positionCS = TransformWorldToHClip(world);
    output.color = input.color; output.uv = input.uv;
    return output;
}
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return AmbientWindColor(input.uv,input.color,input.world,GetCameraPositionWS(),_Brightness,_CameraFade);
            }
            ENDHLSL
        }
    }
}
