// Harita çizgileri: yakınlaştırınca kalınlığı değişmez, kenarları yumuşaktır.
// Her kenar dört köşeli bir şerittir; şerit köşe gölgelendiricisinde ekrandaki kalınlığa açılır.
// TEXCOORD0: xy kenarın normali, z tasarım pikseli cinsinden kalınlık, w şeridin hangi yanı (+1 veya -1).
Shader "GeoWar/ScreenLine"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Bir tasarım pikselinin kaç ekran pikseli ettiği; MapView ayarlar.
            float _GeoWarLineScale;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 side : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float across : TEXCOORD0;
                float halfPixels : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                float worldPerPixel = 2.0 * unity_OrthoParams.y / _ScreenParams.y;

                // Yarım piksel fazladan açılır, kenar yumuşatma çizgiyi inceltmesin.
                float halfPixels = max(v.side.z * 0.5 * _GeoWarLineScale, 0.5) + 0.5;
                v.vertex.xy += v.side.xy * halfPixels * worldPerPixel;

                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.across = v.side.w;
                o.halfPixels = halfPixels;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = i.color;
                color.a *= saturate((1.0 - abs(i.across)) * i.halfPixels);
                return color;
            }
            ENDCG
        }
    }
}
