Shader "Adventure/Day Night Sky"
{
    Properties
    {
        _TopColor("Zenith", Color) = (.15,.36,.62,1)
        _HorizonColor("Horizon", Color) = (.52,.63,.68,1)
        _GroundColor("Ground", Color) = (.18,.2,.18,1)
        _SunColor("Sun", Color) = (1,.9,.7,1)
        _SunDirection("Sun Direction", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _TopColor, _HorizonColor, _GroundColor, _SunColor;
            float4 _SunDirection;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.direction=v.positionOS.xyz; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float3 d=normalize(i.direction);
                half3 sky=lerp(_HorizonColor.rgb,_TopColor.rgb,pow(saturate(d.y),.55));
                sky=lerp(sky,_GroundColor.rgb,smoothstep(0,.3,-d.y));
                float alignment=dot(d,normalize(_SunDirection.xyz));
                sky+=_SunColor.rgb*(smoothstep(.99965,.99985,alignment)*2+pow(saturate(alignment),180)*.12)*step(0,d.y);
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
