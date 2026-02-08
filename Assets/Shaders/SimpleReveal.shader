Shader "Custom/SimpleReveal"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0, 1, 1, 0.5)
        _RevealCenter ("Reveal Center", Vector) = (0, 0, 0, 0)
        _RevealRadius ("Reveal Radius", Float) = 0
        _EdgeSoftness ("Edge Softness", Float) = 1
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
                float3 _RevealCenter;
                float _RevealRadius;
                float _EdgeSoftness;
            CBUFFER_END

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
                // Calculate distance from reveal center
                float dist = distance(IN.positionWS, _RevealCenter);
                
                // Calculate alpha based on distance
                float alpha = saturate((_RevealRadius - dist) / _EdgeSoftness);
                
                // If outside reveal radius, fully transparent
                if (dist > _RevealRadius)
                    discard;
                
                return half4(_BaseColor.rgb, _BaseColor.a * alpha);
            }
            ENDHLSL
        }
    }
}