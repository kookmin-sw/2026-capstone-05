// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SyntyStudios/Building"
{
	Properties
	{
		_Street_Colour("Street_Colour", Color) = (1,0.7209212,0.3349057,0)
		_Window_Colour("Window_Colour", Color) = (1,0.7802997,0.4764151,1)
		_Buildings_Spec_01("Buildings_Spec_01", 2D) = "white" {}
		_Buildings_Diffuse_01("Buildings_Diffuse_01", 2D) = "white" {}
		_Buildings_Normal_Map("Buildings_Normal_Map", 2D) = "bump" {}
		_Buildings_Emissive_01("Buildings_Emissive_01", 2D) = "white" {}
		_Buildings_Emissive_Mask_01("Buildings_Emissive_Mask_01", 2D) = "white" {}
		_Gradient("Gradient", 2D) = "white" {}
		_Brightness("Brightness", Range( 0 , 5)) = 2
		_Scale("Scale", Range( 0 , 0.05)) = 0.005
		_Offset("Offset", Range( 0 , 1)) = 0.4
		_Spec("Spec", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Back
		ZTest LEqual
		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf Standard keepalpha noshadow exclude_path:deferred noambient 
		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
		};

		uniform sampler2D _Buildings_Normal_Map;
		uniform float4 _Buildings_Normal_Map_ST;
		uniform sampler2D _Buildings_Diffuse_01;
		uniform float4 _Buildings_Diffuse_01_ST;
		uniform sampler2D _Buildings_Emissive_01;
		uniform float4 _Buildings_Emissive_01_ST;
		uniform float4 _Window_Colour;
		uniform float _Brightness;
		uniform sampler2D _Gradient;
		uniform float _Scale;
		uniform float _Offset;
		uniform float4 _Street_Colour;
		uniform sampler2D _Buildings_Emissive_Mask_01;
		uniform float4 _Buildings_Emissive_Mask_01_ST;
		uniform sampler2D _Buildings_Spec_01;
		uniform float4 _Buildings_Spec_01_ST;
		uniform float _Spec;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Buildings_Normal_Map = i.uv_texcoord * _Buildings_Normal_Map_ST.xy + _Buildings_Normal_Map_ST.zw;
			o.Normal = UnpackNormal( tex2D( _Buildings_Normal_Map, uv_Buildings_Normal_Map ) );
			float2 uv_Buildings_Diffuse_01 = i.uv_texcoord * _Buildings_Diffuse_01_ST.xy + _Buildings_Diffuse_01_ST.zw;
			o.Albedo = tex2D( _Buildings_Diffuse_01, uv_Buildings_Diffuse_01 ).rgb;
			float2 uv_Buildings_Emissive_01 = i.uv_texcoord * _Buildings_Emissive_01_ST.xy + _Buildings_Emissive_01_ST.zw;
			float3 ase_worldPos = i.worldPos;
			float2 uv_Buildings_Emissive_Mask_01 = i.uv_texcoord * _Buildings_Emissive_Mask_01_ST.xy + _Buildings_Emissive_Mask_01_ST.zw;
			float4 lerpResult62 = lerp( ( ( tex2D( _Buildings_Emissive_01, uv_Buildings_Emissive_01 ) * _Window_Colour ) * _Brightness ) , ( tex2D( _Gradient, (ase_worldPos*_Scale + _Offset).xy ) * _Street_Colour ) , tex2D( _Buildings_Emissive_Mask_01, uv_Buildings_Emissive_Mask_01 ));
			o.Emission = lerpResult62.rgb;
			o.Metallic = (float)0;
			float2 uv_Buildings_Spec_01 = i.uv_texcoord * _Buildings_Spec_01_ST.xy + _Buildings_Spec_01_ST.zw;
			o.Smoothness = ( tex2D( _Buildings_Spec_01, uv_Buildings_Spec_01 ) * _Spec ).r;
			o.Alpha = 1;
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18935
-2696.8;255.2;2478;1241;2510.811;98.8512;1;True;True
Node;AmplifyShaderEditor.WorldPosInputsNode;44;-2122.898,355.3514;Inherit;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;76;-1945.099,697.2133;Inherit;False;Property;_Scale;Scale;9;0;Create;True;0;0;0;False;0;False;0.005;1;0;0.05;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;77;-1867.599,804.9131;Inherit;False;Property;_Offset;Offset;10;0;Create;True;0;0;0;False;0;False;0.4;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;91;-1434.224,40.89491;Inherit;False;Property;_Window_Colour;Window_Colour;1;0;Create;True;0;0;0;False;0;False;1,0.7802997,0.4764151,1;0.3223858,1,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;57;-1010.462,-43.24786;Inherit;True;Property;_Buildings_Emissive_01;Buildings_Emissive_01;5;0;Create;True;0;0;0;False;0;False;-1;6f440ff87739f9142b8ba7ca4e302e31;6f440ff87739f9142b8ba7ca4e302e31;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ScaleAndOffsetNode;75;-1567.698,531.6128;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;98;-532.8782,247.1586;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;70;-1364.562,483.2388;Inherit;True;Property;_Gradient;Gradient;7;0;Create;True;0;0;0;False;0;False;-1;1aa1a60b399b5474f980a840ef7b2cec;1aa1a60b399b5474f980a840ef7b2cec;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;74;-879.3889,542.619;Inherit;False;Property;_Brightness;Brightness;8;0;Create;True;0;0;0;False;0;False;2;0;0;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;65;-867.136,766.5364;Inherit;False;Property;_Street_Colour;Street_Colour;0;0;Create;True;0;0;0;False;0;False;1,0.7209212,0.3349057,0;0.3223858,1,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;60;-490.9861,976.7663;Inherit;True;Property;_Buildings_Emissive_Mask_01;Buildings_Emissive_Mask_01;6;0;Create;True;0;0;0;False;0;False;-1;7c238865282a2d14fadd596afbe715f4;7c238865282a2d14fadd596afbe715f4;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;52;-499.9061,-308.1424;Inherit;True;Property;_Buildings_Spec_01;Buildings_Spec_01;2;0;Create;True;0;0;0;False;0;False;-1;1eb07476c28635546917d90068c7baed;1eb07476c28635546917d90068c7baed;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;63;-541.4145,767.4172;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;73;-383.6349,419.7691;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;101;-504.469,-12.95493;Inherit;False;Property;_Spec;Spec;11;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;100;-247.469,-87.95493;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;51;-512.6949,-691.4392;Inherit;True;Property;_Buildings_Diffuse_01;Buildings_Diffuse_01;3;0;Create;True;0;0;0;False;0;False;-1;8d62ef60a478479458b717cd16718c5a;8d62ef60a478479458b717cd16718c5a;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;53;-500.5598,-498.6946;Inherit;True;Property;_Buildings_Normal_Map;Buildings_Normal_Map;4;0;Create;True;0;0;0;False;0;False;-1;ee65395a565e9074f804642904a21ef2;ee65395a565e9074f804642904a21ef2;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.IntNode;99;-232.7231,300.4716;Inherit;False;Constant;_Int1;Int 1;13;0;Create;True;0;0;0;False;0;False;0;0;False;0;1;INT;0
Node;AmplifyShaderEditor.LerpOp;62;-139.9515,501.2314;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;6.6504,-42.40187;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;SyntyStudios/Building;False;False;False;False;True;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Back;0;False;-1;3;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;False;0;False;Opaque;;Geometry;ForwardOnly;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;0;4;10;25;False;0.5;False;0;5;False;-1;10;False;-1;0;5;False;-1;10;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;75;0;44;0
WireConnection;75;1;76;0
WireConnection;75;2;77;0
WireConnection;98;0;57;0
WireConnection;98;1;91;0
WireConnection;70;1;75;0
WireConnection;63;0;70;0
WireConnection;63;1;65;0
WireConnection;73;0;98;0
WireConnection;73;1;74;0
WireConnection;100;0;52;0
WireConnection;100;1;101;0
WireConnection;62;0;73;0
WireConnection;62;1;63;0
WireConnection;62;2;60;0
WireConnection;0;0;51;0
WireConnection;0;1;53;0
WireConnection;0;2;62;0
WireConnection;0;3;99;0
WireConnection;0;4;100;0
ASEEND*/
//CHKSM=6A7AA18A007D6CD87E43D701CAB2633143FE22E5