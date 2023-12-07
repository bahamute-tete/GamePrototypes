Shader "Unlit/Shadow_Reciver"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color("Color",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma shader_feature _ALPHATEST_ON




            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "HLSLSupport.cginc"

            
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 shadowCoord : TEXCOORD1;
                float3 positionWS:TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

           v2f vert (appdata v)
            {
                v2f o;

                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.vertex = TransformWorldToHClip(worldPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.shadowCoord = TransformWorldToShadowCoord(worldPos); // jave.lin : shadow recieve 将 世界坐标 转到 灯光坐标（阴影坐标）
                o.positionWS = worldPos;
                return o;
            }


            float GetDistanceFade(float3 positionWS)
            {
                float4 posVS = mul(GetWorldToViewMatrix(), float4(positionWS, 1));
                //return posVS.z;
                #if UNITY_REVERSED_Z
                float vz = -posVS.z;
                #else
                float vz = posVS.z;
                #endif
                // jave.lin : 30.0 : start fade out distance, 40.0 : end fade out distance
                float fade = 1 - smoothstep(30.0, 40.0, vz);
                return fade;
            }


            half4 frag(v2f i) : SV_Target
            {
                half3 ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
                //Light mainLight = GetMainLight(i.shadowCoord); // jave.lin : shadow recieve 获取 shadowAttenuation 衰减值
                //half shadow = mainLight.shadowAttenuation;
                //return shadow;
                //return unity_IndirectSpecColor;
                half shadow = MainLightRealtimeShadow(i.shadowCoord); // jave.lin : shadow recieve 如果不需要用到 Light 结构的数据，可以直接使用该接口来获取
                //real4 ambient = UNITY_LIGHTMODEL_AMBIENT;
                //real4 ambient = glstate_lightmodel_ambient;

                half shadowFadeOut = GetDistanceFade(i.positionWS); // jave.lin : 计算 shadow fade out
                shadow = lerp(1, shadow, shadowFadeOut); // jave.lin : 阴影 shadow fade out


                half4 col = tex2D(_MainTex, i.uv);
                half4 finalCol = col * _Color ;
                // 直接用 ambient 作为阴影色效果不太好
                //finalCol.rgb = lerp(ambient.rgb, finalCol.rgb, shadow);
                // 混合后的效果好很多
                finalCol.rgb = lerp(finalCol.rgb * ambient.rgb, finalCol.rgb, shadow); // jave.lin : shadow recieve 我们可以将 ambient 作为阴影色
                // jave.lin : shadow recieve 部分写法可以是：finalCol.rgb *= shadow; 也是看个人的项目需求来定
                return finalCol;
            }
            ENDHLSL
        }


    }
}
