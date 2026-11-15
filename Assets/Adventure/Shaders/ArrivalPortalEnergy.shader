Shader "Adventure/Arrival Portal Energy"
{
    Properties
    {
        _BaseMap("Portal Colour", 2D) = "white" {}
        _OpacityMap("Portal Mask", 2D) = "white" {}
        [HDR] _Tint("Glow Colour", Color) = (0.15,0.65,1.8,1)
        _Charge("Charge", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_OpacityMap); SAMPLER(sampler_OpacityMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; float4 _Tint; float _Charge;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.uv = TRANSFORM_TEX(input.uv, _BaseMap); return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 centre = input.uv - .5;
                float angle = _Time.y * .23;
                float2 moving = float2(centre.x * cos(angle) - centre.y * sin(angle), centre.x * sin(angle) + centre.y * cos(angle)) + .5;
                half3 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, moving).rgb;
                half mask = SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, input.uv).r;
                half pulse = .88 + .12 * sin(_Time.y * 2.3 + length(centre) * 20);
                return half4((colour + .15) * _Tint.rgb * pulse * _Charge, mask * _Charge);
            }
            ENDHLSL
        }
    }
}
