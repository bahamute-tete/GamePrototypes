Shader "GaussianSplat/Render"
{
    Properties
    {
        _Scale("Global Scale", Float) = 1.0
        _FlipY("Flip Y", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 5.0

            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

 

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            StructuredBuffer<float3> _PositionBuffer;
            StructuredBuffer<float4> _ColorBuffer;
            StructuredBuffer<float4> _OutputTranformMatrixBuffer; // x, y, angle

            StructuredBuffer<uint> _OrderBuffer;

            float4x4 _LocalToWorldMatrix;
            float _Scale;
            float _FlipY;

            Varyings vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings output;

                uint index = _OrderBuffer[instanceID];
                
                float3 posOS = _PositionBuffer[index];
                posOS.y *= _FlipY?-1.0 : 1.0;
                float3 posWS = mul(_LocalToWorldMatrix, float4(posOS, 1.0)).xyz;
                float3 posVS = TransformWorldToView(posWS);

                float4 transformData = _OutputTranformMatrixBuffer[index]; 
                float w = transformData.x * _Scale; 
                float h = transformData.y * _Scale; 
                float angle = transformData.z;      

                //Quad
                float2 vertexOffset = float2(0, 0);
                uint vID = vertexID % 6;
                // 0,1,2 (Tri 1) -> (-1,-1), (1,-1), (1,1)
                if (vID == 0) vertexOffset = float2(-1, -1);
                else if (vID == 1) vertexOffset = float2(1, -1);
                else if (vID == 2) vertexOffset = float2(1, 1);
                 // 3,4,5 (Tri 2) -> (-1,-1), (1,1), (-1,1)   
                else if (vID == 3) vertexOffset = float2(-1, -1);
                else if (vID == 4) vertexOffset = float2(1, 1);
                else if (vID == 5) vertexOffset = float2(-1, 1);

                float c = cos(angle);
                float s = sin(angle);
                float2x2 rotMat = float2x2(c, -s, s, c);

                float2 scaledOffset = vertexOffset * float2(w, h);
                float2 rotatedOffset = mul(rotMat, scaledOffset); 
                posVS.xy += rotatedOffset;
                

                output.positionCS = TransformWViewToHClip(posVS);
                output.uv = vertexOffset; 
                output.color = _ColorBuffer[index];

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv; // -1 to 1
                float distSq = dot(uv, uv);
                if (distSq > 1.0) discard;
                
                // G(x) = exp(-x^2 / 2)。
                // x = 3 * uv
                // Alpha = exp( - (3 * uv)^2 / 2 ) = exp( -4.5 * uv^2 )
                float alpha = exp(-4.5f * distSq);

                float4 color =float4(input.color.rgb, input.color.a * alpha);
                
                color.rgb = pow(color.rgb, 2.2);
                if (color.a < 0.01) discard;
                return color;
            }
            ENDHLSL
        }
    }
}
