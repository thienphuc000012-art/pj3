Shader "UI/Adventure/Battle Defeat Backdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Combat Snapshot", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurSize ("Blur", Range(0,8)) = 3
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _Color; float _BlurSize;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv=i.uv, d=_MainTex_TexelSize.xy*_BlurSize;
                fixed3 scene=tex2D(_MainTex,uv).rgb*4;
                scene+=tex2D(_MainTex,uv+float2(d.x,0)).rgb*2+tex2D(_MainTex,uv-float2(d.x,0)).rgb*2;
                scene+=tex2D(_MainTex,uv+float2(0,d.y)).rgb*2+tex2D(_MainTex,uv-float2(0,d.y)).rgb*2;
                scene+=tex2D(_MainTex,uv+d*2).rgb+tex2D(_MainTex,uv-d*2).rgb;
                scene+=tex2D(_MainTex,uv+float2(d.x,-d.y)*2).rgb+tex2D(_MainTex,uv+float2(-d.x,d.y)*2).rgb;
                scene=scene/16*float3(.40,.35,.35);
                float2 p=(uv-.5)*float2(1.3,1.65);
                float rough=noise(uv*29)*.13+noise(uv*113)*.055+noise(uv*390)*.025;
                float ink=smoothstep(.40,.68,length(p)+rough);
                float grain=hash(floor(uv*float2(1920,1080)))*.075;
                return fixed4(lerp(scene*(.94+grain),float3(.002,.002,.003),ink)*i.color.rgb,i.color.a);
            }
            ENDCG
        }
    }
}
