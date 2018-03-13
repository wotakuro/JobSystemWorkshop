Shader "App/BoardChara"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_ExpectedRect("Expected",Vector) = (0,0,0.25,0.25)
		_RectValue("Rect",Vector) = (0,0,0,0)
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent"}
		LOD 100
		
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			
			#include "UnityCG.cginc"
			
			sampler2D _MainTex;
			float4 _ExpectedRect;
			float4 _MainTex_ST;
			
			UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_DEFINE_INSTANCED_PROP(float4,_RectValue)
			UNITY_INSTANCING_BUFFER_END(Props)

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			
			v2f vert (appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				float4 rect = UNITY_ACCESS_INSTANCED_PROP(Props,_RectValue);
				v.vertex.x *= rect.z /_ExpectedRect.z;
				v.vertex.y *= rect.w / _ExpectedRect.w;
				o.vertex = UnityObjectToClipPos(v.vertex);

				v.uv.x = (v.uv.x * rect.z) + rect.x;
				v.uv.y = (v.uv.y * rect.w) + rect.y;

				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv);
				return col;
			}
			ENDCG
		}

		// その２のパス
		Pass
		{
            Tags { "LightMode" = "SRPTest"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			
			#include "UnityCG.cginc"
			
			sampler2D _MainTex;
			float4 _ExpectedRect;
			float4 _MainTex_ST;
			
			UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_DEFINE_INSTANCED_PROP(float4,_RectValue)
			UNITY_INSTANCING_BUFFER_END(Props)

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			
			v2f vert (appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				float4 rect = UNITY_ACCESS_INSTANCED_PROP(Props,_RectValue);
				v.vertex.x *= rect.z /_ExpectedRect.z;
				v.vertex.y *= rect.w / _ExpectedRect.w;
				o.vertex = UnityObjectToClipPos(v.vertex);

				v.uv.x = (v.uv.x * rect.z) + rect.x;
				v.uv.y = (v.uv.y * rect.w) + rect.y;

				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv);
				return col;
			}
			ENDCG
		}
	}
}
