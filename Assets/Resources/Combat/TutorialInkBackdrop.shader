Shader "UI/Adventure/Tutorial Ink Backdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Ink Brush", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BrushRect ("Brush Sprite UV", Vector) = (0,0,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float4 world : TEXCOORD1; };
            sampler2D _MainTex; float4 _BrushRect; fixed4 _Color; float4 _ClipRect;
            v2f vert(appdata v) { v2f o; o.world=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=(v.uv-_BrushRect.xy)/max(.0001,_BrushRect.zw-_BrushRect.xy); o.color=v.color*_Color; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv=i.uv;
                fixed4 brush=tex2D(_MainTex,lerp(_BrushRect.xy,_BrushRect.zw,float2(uv.y,1-uv.x)));
                float grain=noise(uv*float2(160,490));
                float flow=noise(float2(uv.y*11,4.1))*.11 + noise(uv*float2(19,63))*.065;
                float edge=.77+flow;
                float body=1-smoothstep(edge-.16,edge+.055,uv.x);
                float dry=1-smoothstep(edge-.035,edge+.11,uv.x);
                float alpha=max(body*(.94+.06*grain),dry*brush.a*lerp(.22,.85,grain));
                float flecks=step(.89,noise(uv*float2(83,213)))*step(uv.x,edge+.14)*step(edge-.025,uv.x);
                alpha=max(alpha,flecks*brush.a*.5);
                float detail=dot(brush.rgb,float3(.3,.5,.2));
                fixed3 ink=(.0025+detail*.045+grain*.003)*float3(.96,.98,1);
                fixed4 result=fixed4(ink*i.color.rgb,alpha*i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
