Shader "Custom/Wireframe"
{
    Properties
    {
        _WireColor ("Wire Color", Color) = (0, 1, 1, 1)
        _WireThickness ("Wire Thickness", Range(0, 0.01)) = 0.002
        _FillColor ("Fill Color", Color) = (0, 0, 0, 0)
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom
            #include "UnityCG.cginc"

            float4 _WireColor;
            float4 _FillColor;
            float _WireThickness;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2g
            {
                float4 pos : SV_POSITION;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float3 bary : TEXCOORD0;
            };

            v2g vert(appdata v)
            {
                v2g o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
            {
                g2f o;
                
                o.pos = IN[0].pos;
                o.bary = float3(1, 0, 0);
                triStream.Append(o);
                
                o.pos = IN[1].pos;
                o.bary = float3(0, 1, 0);
                triStream.Append(o);
                
                o.pos = IN[2].pos;
                o.bary = float3(0, 0, 1);
                triStream.Append(o);
            }

            float4 frag(g2f i) : SV_Target
            {
                float minBary = min(i.bary.x, min(i.bary.y, i.bary.z));
                float delta = fwidth(minBary);
                float wire = smoothstep(0, delta + _WireThickness, minBary);
                
                return lerp(_WireColor, _FillColor, wire);
            }
            ENDCG
        }
    }
}
