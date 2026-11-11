Shader "Obsidian Forge/Ambient Particle"
{
    Properties
    {
        [Enum(Mote,0,Grain,1,Spore,2,Halo,3,Ember,4,Haze,5)] _Form("Particle silhouette", Float) = 0
        _Brightness("Brightness", Range(0,64)) = 1
        [HideInInspector] _CameraFade("Camera fade", Vector) = (0,0,0,0)
        [HideInInspector] _WakeMode("Deposited wake mote", Float) = 0
        [HideInInspector] _WakeTime("Wake time", Float) = 0
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
                float _Form; float _Brightness; float4 _CameraFade; float _WakeMode; float _WakeTime;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float4 motion : TEXCOORD1; float2 birth : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float2 wakeAge : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 world = TransformObjectToWorld(input.positionOS.xyz);
    float3 rightWS = normalize(float3(UNITY_MATRIX_I_V._m00,UNITY_MATRIX_I_V._m10,UNITY_MATRIX_I_V._m20));
    float3 upWS = normalize(float3(UNITY_MATRIX_I_V._m01,UNITY_MATRIX_I_V._m11,UNITY_MATRIX_I_V._m21));
    world = AmbientWakeVertex(world,input.uv,input.motion,input.birth,_WakeMode,_WakeTime,rightWS,upWS,output.wakeAge);
    output.world = world;
    output.positionCS = TransformWorldToHClip(world);
    output.color = input.color; output.uv = input.uv*2-1;
    return output;
}
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return AmbientParticleColor(input.uv,input.color,input.world,GetCameraPositionWS(),input.wakeAge,_Form,_Brightness,_CameraFade,_WakeMode);
            }
            ENDHLSL
        }
    }
}
