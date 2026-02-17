Shader "Custom/SimpleReveal"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0, 1, 1, 0.5)
        _RevealRadius ("Reveal Radius", Float) = 2
        _EdgeSoftness ("Edge Softness", Float) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _RevealRadius;
                float _EdgeSoftness;
                float3 _CurrentPos;
                int _PointCount;
            CBUFFER_END
            
            float4 _RevealPoints[64];

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float minDist = 9999;
                
                // Check distance to all revealed points
                for (int i = 0; i < _PointCount; i++)
                {
                    float dist = distance(IN.positionWS, _RevealPoints[i].xyz);
                    minDist = min(minDist, dist);
                }
                
                // Also check current camera position
                float currentDist = distance(IN.positionWS, _CurrentPos);
                minDist = min(minDist, currentDist);
                
                // Calculate alpha based on distance
                float alpha = saturate((_RevealRadius - minDist) / _EdgeSoftness);
                
                // Discard if not revealed
                if (alpha < 0.01)
                    discard;
                
                return half4(_BaseColor.rgb, _BaseColor.a * alpha);
            }
            ENDHLSL
        }
    }
}