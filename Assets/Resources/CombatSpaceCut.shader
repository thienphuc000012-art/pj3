Shader "Hidden/Combat/SpaceCut"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _Cuts[24];
            int _CutCount;
            float4 _CutViewport;
            float4 _EdgeTint;
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 p = (uv - .5) * _CutViewport.xy;
                float2 shift = 0;
                float rim = 0, seam = 0;
                for (int i = 0; i < _CutCount; i++)
                {
                    float4 cut = _Cuts[i];
                    float d = dot(p, cut.xy) - cut.z;
                    float2 tangent = float2(cut.y, -cut.x);
                    // Opposite sides shear in opposing directions, then settle back.
                    shift += sign(d) * (tangent * .8 + cut.xy * .25) * cut.w;
                    float energy = saturate(cut.w / 8);
                    rim = max(rim, exp(-abs(d) * .7) * energy);
                    seam = max(seam, (1 - smoothstep(.4, 1.7, abs(d))) * energy);
                }
                shift = clamp(shift, -40, 40);
                float2 sampleUV = clamp(uv + shift * _CutViewport.zw, _CutViewport.zw, 1 - _CutViewport.zw);
                half3 colour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV).rgb;
                // Tiny optical fringe confined to the seam, not a full-screen colour wash.
                float2 fringe = float2(.7, 0) * _CutViewport.zw * rim;
                colour.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV + fringe).r;
                colour.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV - fringe).b;
                colour *= 1 - seam * .3;
                colour += lerp(_EdgeTint.rgb, float3(1, .98, .94), .75) * rim * .55;
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
