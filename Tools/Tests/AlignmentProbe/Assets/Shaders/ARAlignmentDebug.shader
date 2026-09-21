// TEMPORARY, NOT FOR COMMIT. Draws the LiDAR mesh permanently so AR-to-background
// alignment can be judged from a still frame, without a sound-triggered wave.
Shader "Unlit/ARAlignmentDebug"
{
    Properties { _Alpha ("Alpha", Range(0,1)) = 0.5 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            float _Alpha;

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Monotonic height ramp: blue low, red high, over a 3 m span.
                // With the AR layer correctly registered the blue end sits at the
                // bottom of the screen (the desk) and red at the top (upper
                // shelves). A 180-degree error inverts that, which a repeating
                // banded ramp could not distinguish.
                float t = saturate((i.positionWS.y + 1.0) / 3.0);
                float3 col = lerp(float3(0, 0.2, 1), float3(1, 0.1, 0), t);
                // Contour line every 0.25 m for a crisp read of the direction.
                float c = 1.0 - saturate(abs(frac(i.positionWS.y * 4.0) - 0.5) * 16.0);
                col = lerp(col, float3(1, 1, 1), c);
                return half4(col, _Alpha);
            }
            ENDHLSL
        }
    }
}
