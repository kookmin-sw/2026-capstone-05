// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SyntyStudios/EmissiveScroll"
{
	Properties
	{
		_Emissive("Emissive", 2D) = "white" {}
		_Colour_01("Colour_01", Color) = (0,0.8694534,1,0)
		_Colour_02("Colour_02", Color) = (0,0.8694534,1,0)
		_Speed("Speed", Vector) = (-0.2,0,0,0)
		_LED_Mask_01("LED_Mask_01", 2D) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform float4 _Colour_02;
		uniform float4 _Colour_01;
		uniform sampler2D _Emissive;
		uniform float2 _Speed;
		uniform sampler2D _LED_Mask_01;
		uniform float4 _LED_Mask_01_ST;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 panner6 = ( 1.0 * _Time.y * _Speed + i.uv_texcoord);
			float4 tex2DNode2 = tex2D( _Emissive, panner6 );
			float4 lerpResult21 = lerp( _Colour_02 , _Colour_01 , tex2DNode2);
			o.Albedo = ( lerpResult21 * tex2DNode2 ).rgb;
			float4 color20 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
			float2 uv_LED_Mask_01 = i.uv_texcoord * _LED_Mask_01_ST.xy + _LED_Mask_01_ST.zw;
			float4 lerpResult19 = lerp( lerpResult21 , color20 , tex2D( _LED_Mask_01, uv_LED_Mask_01 ).a);
			o.Emission = lerpResult19.rgb;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18909
-2972.8;328;3058;845;2730.774;-40.14482;1.3;True;True
Node;AmplifyShaderEditor.TextureCoordinatesNode;4;-1505.12,183.2236;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;10;-1376.88,340.509;Inherit;False;Property;_Speed;Speed;3;0;Create;True;0;0;0;False;0;False;-0.2,0;0,0.5;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.PannerNode;6;-1068.619,302.7625;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;2;-823.0745,137.1592;Inherit;True;Property;_Emissive;Emissive;0;0;Create;True;0;0;0;False;0;False;-1;a455f2adbb9c33e4ab5c004c67744396;a04762f510244b444bc0cd178f62bcf3;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;22;-866.5737,691.4447;Inherit;False;Property;_Colour_02;Colour_02;2;0;Create;True;0;0;0;False;0;False;0,0.8694534,1,0;1,0,0.9680333,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;15;-1093.785,504.0089;Inherit;False;Property;_Colour_01;Colour_01;1;0;Create;True;0;0;0;False;0;False;0,0.8694534,1,0;0.3223858,1,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;17;-506.1838,829.1198;Inherit;True;Property;_LED_Mask_01;LED_Mask_01;4;0;Create;True;0;0;0;False;0;False;-1;5669792805fe08b4bb7e31a730309bfc;5669792805fe08b4bb7e31a730309bfc;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;20;-497.1155,585.8195;Inherit;False;Constant;_Color1;Color 1;3;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;21;-576.6735,357.3447;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;13;-371.1881,272.9698;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;19;-111.3132,539.9431;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;194.7777,337.1235;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;SyntyStudios/EmissiveScroll;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;16;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;6;0;4;0
WireConnection;6;2;10;0
WireConnection;2;1;6;0
WireConnection;21;0;22;0
WireConnection;21;1;15;0
WireConnection;21;2;2;0
WireConnection;13;0;21;0
WireConnection;13;1;2;0
WireConnection;19;0;21;0
WireConnection;19;1;20;0
WireConnection;19;2;17;4
WireConnection;0;0;13;0
WireConnection;0;2;19;0
ASEEND*/
//CHKSM=37F389891951AF53AEDBC2C404B4F100FC4DD2DE