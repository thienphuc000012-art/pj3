Shader "FootTrails/ParticleBuiltIn"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _TintColor("Tint", Color) = (1,1,1,1)
        _Intensity("Brightness", Range(0,8)) = 1
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha [_DstBlend]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _BaseMap;
            fixed4 _TintColor;
            float _Intensity;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            v2f vert(appdata input)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(input.vertex);
                o.uv = input.uv;
                o.color = input.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_BaseMap, i.uv) * i.color * _TintColor;
                color.rgb *= _Intensity;
                return color;
            }
            ENDCG
        }
    }
}
