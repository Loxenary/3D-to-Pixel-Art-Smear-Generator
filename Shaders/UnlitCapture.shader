// JUSTIFICATION: flat albedo capture so lighting is never baked into pixel art output; see 2026-06-23-unlit-capture-default.md
// Supports both URP (_BaseMap/_BaseColor) and legacy Standard (_MainTex/_Color) source materials.
Shader "SmearFramework/UnlitCapture"
{
    Properties
    {
        _MainTex  ("Albedo", 2D)     = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Color    ("Color (legacy)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        Pass
        {
            Lighting Off
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _BaseColor;
            float4    _Color;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 albedo = tex2D(_MainTex, i.uv);
                return albedo * _BaseColor * _Color;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Texture"
}
