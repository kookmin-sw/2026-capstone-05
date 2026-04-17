// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SyntyStudios/EmissiveColourChange"
{
	Properties
	{
		_Emissive("Emissive", 2D) = "white" {}
		_BaseTexture("BaseTexture", 2D) = "white" {}
		_Flipbook("Flipbook", 2D) = "white" {}
		_EmissiveMask("EmissiveMask", 2D) = "white" {}
		_Columns("Columns", Float) = 3
		_RowsColumns("RowsColumns", Float) = 3
		_Speed("Speed", Vector) = (-0.2,0,0,0)
		_speed("speed", Float) = 10
		_EmissivePower("EmissivePower", Range( 0 , 1)) = 1
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

		uniform sampler2D _BaseTexture;
		uniform float4 _BaseTexture_ST;
		uniform sampler2D _Emissive;
		uniform float2 _Speed;
		uniform sampler2D _Flipbook;
		uniform float _Columns;
		uniform float _RowsColumns;
		uniform float _speed;
		uniform sampler2D _EmissiveMask;
		uniform float4 _EmissiveMask_ST;
		uniform float _EmissivePower;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_BaseTexture = i.uv_texcoord * _BaseTexture_ST.xy + _BaseTexture_ST.zw;
			o.Albedo = tex2D( _BaseTexture, uv_BaseTexture ).rgb;
			float4 color20 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
			float2 uv_TexCoord4 = i.uv_texcoord * float2( 0.01,0.01 );
			float2 panner6 = ( 1.0 * _Time.y * _Speed + uv_TexCoord4);
			// *** BEGIN Flipbook UV Animation vars ***
			// Total tiles of Flipbook Texture
			float fbtotaltiles37 = _Columns * _RowsColumns;
			// Offsets for cols and rows of Flipbook Texture
			float fbcolsoffset37 = 1.0f / _Columns;
			float fbrowsoffset37 = 1.0f / _RowsColumns;
			// Speed of animation
			float fbspeed37 = _Time[ 1 ] * _speed;
			// UV Tiling (col and row offset)
			float2 fbtiling37 = float2(fbcolsoffset37, fbrowsoffset37);
			// UV Offset - calculate current tile linear index, and convert it to (X * coloffset, Y * rowoffset)
			// Calculate current tile linear index
			float fbcurrenttileindex37 = round( fmod( fbspeed37 + 0.0, fbtotaltiles37) );
			fbcurrenttileindex37 += ( fbcurrenttileindex37 < 0) ? fbtotaltiles37 : 0;
			// Obtain Offset X coordinate from current tile linear index
			float fblinearindextox37 = round ( fmod ( fbcurrenttileindex37, _Columns ) );
			// Multiply Offset X by coloffset
			float fboffsetx37 = fblinearindextox37 * fbcolsoffset37;
			// Obtain Offset Y coordinate from current tile linear index
			float fblinearindextoy37 = round( fmod( ( fbcurrenttileindex37 - fblinearindextox37 ) / _Columns, _RowsColumns ) );
			// Reverse Y to get tiles from Top to Bottom
			fblinearindextoy37 = (int)(_RowsColumns-1) - fblinearindextoy37;
			// Multiply Offset Y by rowoffset
			float fboffsety37 = fblinearindextoy37 * fbrowsoffset37;
			// UV Offset
			float2 fboffset37 = float2(fboffsetx37, fboffsety37);
			// Flipbook UV
			half2 fbuv37 = i.uv_texcoord * fbtiling37 + fboffset37;
			// *** END Flipbook UV Animation vars ***
			float2 uv_EmissiveMask = i.uv_texcoord * _EmissiveMask_ST.xy + _EmissiveMask_ST.zw;
			float4 lerpResult40 = lerp( tex2D( _Flipbook, fbuv37 ) , tex2D( _EmissiveMask, uv_EmissiveMask ) , 0.5);
			float4 lerpResult19 = lerp( color20 , tex2D( _Emissive, panner6 ) , lerpResult40);
			float4 lerpResult31 = lerp( color20 , lerpResult19 , _EmissivePower);
			o.Emission = lerpResult31.rgb;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18909
-2657.6;235.2;2782;995;2604.775;-76.54481;1.3;True;True
Node;AmplifyShaderEditor.TextureCoordinatesNode;33;-1678.846,696.6552;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;36;-1783.241,1059.577;Inherit;False;Property;_speed;speed;7;0;Create;True;0;0;0;False;0;False;10;-1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;35;-1580.018,869.4981;Inherit;False;Property;_Columns;Columns;4;0;Create;True;0;0;0;False;0;False;3;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;39;-1789.675,951.4448;Inherit;False;Property;_RowsColumns;RowsColumns;5;0;Create;True;0;0;0;False;0;False;3;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;34;-1563.002,1079.843;Inherit;False;Constant;_Float3;Float 3;0;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCFlipBookUVAnimation;37;-1251.219,799.798;Inherit;False;0;0;6;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;10;-1658.98,275.509;Inherit;True;Property;_Speed;Speed;6;0;Create;True;0;0;0;False;0;False;-0.2,0;0.1,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;4;-1652.02,57.1236;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;0.01,0.01;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;42;-228.375,1098.345;Inherit;False;Constant;_Float4;Float 3;0;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;38;-965.5188,771.6981;Inherit;True;Property;_Flipbook;Flipbook;2;0;Create;True;0;0;0;False;0;False;-1;2ad1574d9c09f7740ae70259745db9e4;fcf8488f61b58ce44bd47d330dfacdb5;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PannerNode;6;-1276.619,90.86255;Inherit;True;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;30;-753.574,1050.245;Inherit;True;Property;_EmissiveMask;EmissiveMask;3;0;Create;True;0;0;0;False;0;False;-1;36150e972e30a6a4eba79d1c523bddfd;36150e972e30a6a4eba79d1c523bddfd;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;20;-497.1155,585.8195;Inherit;False;Constant;_Color1;Color 1;3;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;2;-940.0744,17.55921;Inherit;True;Property;_Emissive;Emissive;0;0;Create;True;0;0;0;False;0;False;-1;0d509c6f81c59b542926956d8d70473e;0d509c6f81c59b542926956d8d70473e;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;40;-289.4749,821.4448;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;32;138.2262,972.2448;Inherit;False;Property;_EmissivePower;EmissivePower;8;0;Create;True;0;0;0;False;0;False;1;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;19;-78.81318,446.3431;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;31;94.02623,718.7448;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;24;-306.3743,-71.65517;Inherit;True;Property;_BaseTexture;BaseTexture;1;0;Create;True;0;0;0;False;0;False;-1;1f0583b064d0cd142a54d977ac5b0481;1f0583b064d0cd142a54d977ac5b0481;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;401.4777,343.6235;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;SyntyStudios/EmissiveColourChange;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;16;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;37;0;33;0
WireConnection;37;1;35;0
WireConnection;37;2;39;0
WireConnection;37;3;36;0
WireConnection;37;4;34;0
WireConnection;38;1;37;0
WireConnection;6;0;4;0
WireConnection;6;2;10;0
WireConnection;2;1;6;0
WireConnection;40;0;38;0
WireConnection;40;1;30;0
WireConnection;40;2;42;0
WireConnection;19;0;20;0
WireConnection;19;1;2;0
WireConnection;19;2;40;0
WireConnection;31;0;20;0
WireConnection;31;1;19;0
WireConnection;31;2;32;0
WireConnection;0;0;24;0
WireConnection;0;2;31;0
ASEEND*/
//CHKSM=54767B25420FAFDAE2770A2F5BE15D8C7FC27265