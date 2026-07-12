// note: basset-2024-smear-stylized-motion.md (vertex alpha mask for multiples)
//
// Unlit transparent shader for multiple ghosts. The mesh still uses per-vertex
// alpha from Eq 12, while the visible color follows the source material/texture
// so the copy keeps the character's original look.
Shader "SmearFramework/Ghost"
{
    Properties
    {
        _MainTex   ("Texture", 2D)    = "white" {}
        _BaseColor ("Tint",    Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _BaseColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                // texture + material tint provide color; vertex alpha controls visibility
                return half4(tex.rgb * _BaseColor.rgb, tex.a * i.color.a * _BaseColor.a);
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent"
}
