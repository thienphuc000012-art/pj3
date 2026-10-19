// Made with Amplify Shader Editor v1.9.9.7
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "/Vefects/SH_Vefects_Extra_Billboard_01_Unlit_BIRP"
{
	Properties
	{
		_Texture( "Texture", 2D ) = "white" {}
		_Emission( "Emission", Float ) = 3
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

		SubShader
	{
		Tags{ "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		ZWrite On
		Blend One Zero
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
			float3 worldPos;
			float3 worldNormal;
			float3 worldRefl;
			float4 uv2_texcoord2;
			float4 uv3_texcoord3;
			float4 uv4_texcoord4;
			float4 screenPos;
			float4 vertexColor;
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
		uniform sampler2D _Texture;
		uniform float4 _Texture_ST;
		uniform float _Emission;
		#endif
		void surf( VefectsInput i , inout VefectsSurfaceOutput o )
		{
			float2 uv_Texture = i.uv_texcoord * _Texture_ST.xy + _Texture_ST.zw;
			o.Emission = ( tex2D( _Texture, uv_Texture ).rgb * _Emission );
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
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#pragma target 3.5
		#define ASE_VERSION 19907
		#pragma surface surf Unlit keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform sampler2D _Texture;
		uniform float4 _Texture_ST;
		uniform float _Emission;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 uv_Texture = i.uv_texcoord * _Texture_ST.xy + _Texture_ST.zw;
			o.Emission = ( tex2D( _Texture, uv_Texture ).rgb * _Emission );
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback Off
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
/*ASEBEGIN
Version=19907
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;10;-768,0;Inherit;True;Property;_Texture;Texture;0;0;Create;True;0;0;0;False;0;False;-1;None;5468874f0432fd94a805ff702e5eae90;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;35;-384,128;Inherit;False;Property;_Emission;Emission;1;0;Create;True;0;0;0;False;0;False;3;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;34;-384,0;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;36;0,0;Float;False;True;-1;3;AmplifyShaderEditor.MaterialInspector;0;0;Unlit;/Vefects/SH_Vefects_Extra_Billboard_01_Unlit_BIRP;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;0;False;;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;34;0;10;5
WireConnection;34;1;35;0
WireConnection;36;2;34;0
ASEEND*/
//CHKSM=97ACEEFACA01EB03F17AA906BD150C1A781C7463