Shader "Abyssal/HUD_RadialRing"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0.9, 1, 1)
        _Fill ("Fill", Range(0, 1)) = 1
        _InnerRadius ("Inner Radius", Range(0.1, 0.5)) = 0.37
        _OuterRadius ("Outer Radius", Range(0.1, 0.5)) = 0.47
        _BgColor ("Background Track Color", Color) = (0.1, 0.2, 0.3, 0.25)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _BgColor;
            float _Fill;
            float _InnerRadius;
            float _OuterRadius;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Centered coordinates from -0.5 to +0.5
                float2 c = i.uv - float2(0.5, 0.5);
                float dist = length(c);

                // Anti-aliased ring boundary
                float edge = fwidth(dist);
                if (edge < 0.001) edge = 0.003;

                float ringMask = smoothstep(_InnerRadius - edge, _InnerRadius, dist) *
                                 (1.0 - smoothstep(_OuterRadius, _OuterRadius + edge, dist));

                // Clockwise angle starting from top (0 at top, 1.0 at full turn)
                float angle = atan2(c.x, c.y); // radians: 0 at top (+y), PI/2 at right (+x), -PI/2 at left (-x)
                if (angle < 0.0) angle += 6.2831853;
                float normAngle = angle / 6.2831853;

                // Active fill vs background track
                float fillMask = step(normAngle, _Fill);

                fixed4 finalCol = lerp(_BgColor, _Color, fillMask);
                finalCol.a *= ringMask;

                return finalCol;
            }
            ENDCG
        }
    }
}
