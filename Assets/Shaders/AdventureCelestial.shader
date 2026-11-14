Shader "Adventure/Celestial Unlit"
{
    Properties { _MainTex("Texture",2D)="white"{} _Tint("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float3 positionWS:TEXCOORD1; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionWS=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.uv=v.uv; o.color=v.color; return o; }
            half4 frag(Varyings i):SV_Target
            {
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*_Tint*i.color;
                // The moon sets behind the horizon instead of fading out at a fixed time/position.
                color.a*=smoothstep(-.006,.006,normalize(i.positionWS-_WorldSpaceCameraPos).y);
                return color;
            }
            ENDHLSL
        }
    }
}
