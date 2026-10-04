Shader "Hidden/EchoVision/HumanStencilDebug"
{
    Properties { _MainTex ("Stencil", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float4x4 _CurrentMatrix, _CameraMatrix;
            float _Mode;
            Varyings vert(Attributes v)
            {
                Varyings o; o.position = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 oldUV = mul(float3(i.uv, 1), (float3x3)_CurrentMatrix).xy;
                float2 cameraUV = mul(float3(i.uv, 1), (float3x3)_CameraMatrix).xy;
                half oldMask = step(.5, tex2D(_MainTex, oldUV).r);
                half cameraMask = step(.5, tex2D(_MainTex, cameraUV).r);
                if (_Mode < .5) return half4(1, .1, .1, oldMask * .55);
                if (_Mode < 1.5) return half4(0, 1, 1, cameraMask * .55);
                if (_Mode < 2.5) return half4(oldMask, cameraMask, cameraMask, max(oldMask,cameraMask)*.55);
                return 0;
            }
            ENDHLSL
        }
    }
}
