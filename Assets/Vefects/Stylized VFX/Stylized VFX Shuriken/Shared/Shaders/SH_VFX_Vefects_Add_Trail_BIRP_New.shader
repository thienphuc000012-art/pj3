// Made with Amplify Shader Editor v1.9.9.7
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_VFX_Vefects_Add_Trail_BIRP_New"
{
	Properties
	{
		_EmissiveIntensity( "Emissive Intensity", Float ) = 1
		_Texture( "Texture", 2D ) = "white" {}
		_TrailScale( "Trail Scale", Float ) = 1
		_TrailSpeed( "Trail Speed", Float ) = 1
		[Space(33)][Header(AR)][Space(13)] _Cull( "Cull", Float ) = 2
		_Src( "Src", Float ) = 1
		_Dst( "Dst", Float ) = 1
		_ZWrite( "ZWrite", Float ) = 0
		_ZTest( "ZTest", Float ) = 2
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

		SubShader
	{
		Tags{ "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IsEmissive" = "true"  }
		Cull [_Cull]
		ZWrite [_ZWrite]
		ZTest [_ZTest]
		Blend [_Src] [_Dst]
		Pass
		{
			Name "UniversalForward"
			Tags { "LightMode" = "UniversalForward" }
			CGPROGRAM
			#pragma target 3.5
			#pragma vertex VefectsVert
			#pragma fragment VefectsFrag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"

#ifndef INTERNAL_DATA
#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
#endif

		#include "UnityShaderVariables.cginc"

		#define ASE_VERSION 19907

		struct VefectsSurfaceOutput
		{
			half3 Albedo; half3 Normal; half3 Emission; half3 Specular;
			half Metallic; half Smoothness; half Occlusion; half Alpha;
		};
		#ifndef INTERNAL_DATA
		#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
		#endif
		#undef WorldNormalVector
		#undef WorldReflectionVector
		#define WorldNormalVector(data, normal) half3(dot(data.internalSurfaceTtoW0, normal), dot(data.internalSurfaceTtoW1, normal), dot(data.internalSurfaceTtoW2, normal))
		#define WorldReflectionVector(data, normal) reflect(data.worldRefl, WorldNormalVector(data, normal))
		struct VefectsInput
		{
			float2 uv_texcoord;
			float4 vertexColor;
			float3 worldPos;
			float3 worldNormal;
			float3 worldRefl;
			float4 uv2_texcoord2;
			float4 uv3_texcoord3;
			float4 uv4_texcoord4;
			float4 screenPos;
			half ASEIsFrontFacing;
			half filler;
			half3 internalSurfaceTtoW0;
			half3 internalSurfaceTtoW1;
			half3 internalSurfaceTtoW2;};
inline half4 LightingUnlit( VefectsSurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		
		#ifndef VEFECTS_SHARED_MATERIAL_DECLS
		#define VEFECTS_SHARED_MATERIAL_DECLS
		uniform float _Dst;
		uniform float _Src;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
		uniform sampler2D _Texture;
		uniform float _TrailScale;
		uniform float _TrailSpeed;
		uniform float _EmissiveIntensity;
		#endif
		void surf( VefectsInput i , inout VefectsSurfaceOutput o )
		{
			float2 appendResult110 = (float2(_TrailScale , 1.0));
			float2 appendResult107 = (float2(_TrailSpeed , 0.0));
			float2 uv_TexCoord111 = i.uv_texcoord * appendResult110 + ( _Time.y * appendResult107 );
			float4 tex2DNode112 = tex2D( _Texture, uv_TexCoord111 );
			o.Emission = ( ( ( tex2DNode112.r + tex2DNode112.g ) * _EmissiveIntensity ) * (i.vertexColor).rgb * i.vertexColor.a );
			o.Alpha = 1;
		}

		

			struct VefectsURPAppData
			{
				float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; fixed4 color : COLOR;
				float4 texcoord0 : TEXCOORD0; float4 texcoord1 : TEXCOORD1; float4 texcoord2 : TEXCOORD2; float4 texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			struct VefectsURPVaryings
			{
				float4 positionCS : SV_POSITION; float4 texcoord0 : TEXCOORD0; float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2; float4 texcoord3 : TEXCOORD3; float4 screenPos : TEXCOORD4;
				float3 worldPos : TEXCOORD5; float3 worldNormal : TEXCOORD6; fixed4 color : COLOR;
				float4 tSpace0 : TEXCOORD7; float4 tSpace1 : TEXCOORD8; float4 tSpace2 : TEXCOORD9;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			VefectsURPVaryings VefectsVert(VefectsURPAppData v)
			{
				UNITY_SETUP_INSTANCE_ID(v);
				VefectsURPVaryings o; UNITY_INITIALIZE_OUTPUT(VefectsURPVaryings, o); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				float4 clipPosition = UnityObjectToClipPos(v.vertex);
				o.positionCS=clipPosition; o.texcoord0=v.texcoord0; o.texcoord1=v.texcoord1; o.texcoord2=v.texcoord2; o.texcoord3=v.texcoord3;
				float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				float3 worldNormal = UnityObjectToWorldNormal(v.normal);
				float3 worldTangent = normalize(UnityObjectToWorldDir(v.tangent.xyz));
				float3 worldBinormal = normalize(cross(worldNormal, worldTangent) * v.tangent.w * unity_WorldTransformParams.w);
				o.worldPos = worldPos; o.worldNormal = worldNormal;
				o.tSpace0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x);
				o.tSpace1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y);
				o.tSpace2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z);
				o.screenPos=ComputeScreenPos(clipPosition); o.color=v.color; return o;
			}
			fixed4 VefectsFrag(VefectsURPVaryings IN, fixed facing : VFACE) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
				VefectsInput i; UNITY_INITIALIZE_OUTPUT(VefectsInput, i);
				i.uv_texcoord = IN.texcoord0; i.uv2_texcoord2 = IN.texcoord1;
				i.uv3_texcoord3 = IN.texcoord2; i.uv4_texcoord4 = IN.texcoord3;
				i.vertexColor = IN.color; i.worldPos = IN.worldPos; i.worldNormal = IN.worldNormal;
				i.screenPos = IN.screenPos; i.ASEIsFrontFacing = facing >= 0 ? 1 : 0; i.filler = 0;
				i.internalSurfaceTtoW0 = IN.tSpace0.xyz; i.internalSurfaceTtoW1 = IN.tSpace1.xyz; i.internalSurfaceTtoW2 = IN.tSpace2.xyz;
				i.worldRefl = reflect(-normalize(_WorldSpaceCameraPos.xyz - IN.worldPos), IN.worldNormal);
				VefectsSurfaceOutput surfaceOutput; UNITY_INITIALIZE_OUTPUT(VefectsSurfaceOutput, surfaceOutput);
				surf(i, surfaceOutput);
				return fixed4(surfaceOutput.Emission + surfaceOutput.Albedo, surfaceOutput.Alpha);
			}
			ENDCG
		}
	}
SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IsEmissive" = "true"  }
		Cull [_Cull]
		ZWrite [_ZWrite]
		ZTest [_ZTest]
		Blend [_Src] [_Dst]
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.5
		#define ASE_VERSION 19907
		#pragma surface surf Unlit keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
			float4 vertexColor : COLOR;
		};

		uniform float _Dst;
		uniform float _Src;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
		uniform sampler2D _Texture;
		uniform float _TrailScale;
		uniform float _TrailSpeed;
		uniform float _EmissiveIntensity;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 appendResult110 = (float2(_TrailScale , 1.0));
			float2 appendResult107 = (float2(_TrailSpeed , 0.0));
			float2 uv_TexCoord111 = i.uv_texcoord * appendResult110 + ( _Time.y * appendResult107 );
			float4 tex2DNode112 = tex2D( _Texture, uv_TexCoord111 );
			o.Emission = ( ( ( tex2DNode112.r + tex2DNode112.g ) * _EmissiveIntensity ) * (i.vertexColor).rgb * i.vertexColor.a );
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback Off
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
/*ASEBEGIN
Version=19907
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;105;-2688,128;Inherit;False;Property;_TrailSpeed;Trail Speed;4;0;Create;False;0;0;0;False;0;False;1;0.4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;107;-2432,128;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;106;-2688,0;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;108;-2688,-384;Inherit;False;Property;_TrailScale;Trail Scale;3;0;Create;False;0;0;0;False;0;False;1;0.05;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;109;-2304,0;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;110;-2432,-384;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;111;-2048,-128;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;112;-1664,-128;Inherit;True;Property;_Texture;Texture;2;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;114;-1280,-112;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;115;-1024,0;Inherit;False;Property;_EmissiveIntensity;Emissive Intensity;1;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;113;-1280,384;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;104;974,-50;Inherit;False;1238;166;AR;5;99;100;101;102;103;;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;117;-1024,-128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;116;-768,128;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;101;1536,0;Inherit;False;Property;_Dst;Dst;7;0;Create;True;0;0;0;True;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;118;-384,0;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;100;1280,0;Inherit;False;Property;_Src;Src;6;0;Create;True;0;0;0;True;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;102;1792,0;Inherit;False;Property;_ZWrite;ZWrite;8;0;Create;True;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;103;2048,0;Inherit;False;Property;_ZTest;ZTest;9;0;Create;True;0;0;0;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;99;1024,0;Inherit;False;Property;_Cull;Cull;5;0;Create;True;0;0;0;True;3;Space(33);Header(AR);Space(13);False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;129;0,0;Float;False;True;-1;3;AmplifyShaderEditor.MaterialInspector;0;0;Unlit;Vefects/SH_VFX_Vefects_Add_Trail_BIRP_New;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;True;_ZWrite;0;True;_ZTest;False;0;False;;0;False;;False;0;0;False;;0;Custom;0.5;True;True;0;False;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;1;5;True;_Src;10;True;_Dst;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;True;_Cull;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;107;0;105;0
WireConnection;109;0;106;0
WireConnection;109;1;107;0
WireConnection;110;0;108;0
WireConnection;111;0;110;0
WireConnection;111;1;109;0
WireConnection;112;1;111;0
WireConnection;114;0;112;1
WireConnection;114;1;112;2
WireConnection;117;0;114;0
WireConnection;117;1;115;0
WireConnection;116;0;113;0
WireConnection;118;0;117;0
WireConnection;118;1;116;0
WireConnection;118;2;113;4
WireConnection;129;2;118;0
ASEEND*/
//CHKSM=BCBAB6B23C6DC6B18136BE099D64048EF37A74C1