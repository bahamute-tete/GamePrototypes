Shader "Custom/TunnelEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CenterX ("Center X", Range(0, 1)) = 0.5
        _CenterY ("Center Y", Range(0, 1)) = 0.5
        _RadialScale ("Radial Scale", Range(0.1, 10)) = 1
        _AngleOffset ("Angle Offset", Range(0, 360)) = 0
        _DepthScale ("Depth Scale", Range(0.1, 5)) = 1
        _PerspectiveStrength ("Perspective Strength", Range(0, 2)) = 1
        _FadeDistance ("Fade Distance", Range(0, 2)) = 1
        _ViewPointX ("View Point X", Range(0, 1)) = 0.5
        _ViewPointY ("View Point Y", Range(0, 1)) = 0.5
        _AnimationSpeed ("Animation Speed", Range(-5, 5)) = 1
        _RadialAnimationSpeed ("Radial Animation Speed", Range(-5, 5)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha

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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _CenterX;
            float _CenterY;
            float _RadialScale;
            float _AngleOffset;
            float _DepthScale;
            float _PerspectiveStrength;
            float _FadeDistance;
            float _ViewPointX;
            float _ViewPointY;
            float _AnimationSpeed;
            float _RadialAnimationSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv-0.5;
                float2 centered = uv - float2(_CenterX, _CenterY);
                
                float2 viewOffset = uv - float2(_ViewPointX, _ViewPointY);
                float distanceToView = length(viewOffset);
                
                float radius = length(centered);
                float angle = atan2(centered.y, centered.x);
                
                float depth = 1.0 / (1.0 + distanceToView * _PerspectiveStrength);
                float scaledRadius = radius * _RadialScale * depth * _DepthScale;
                
                // 添加时间驱动的动画
                float timeOffset = _Time.y * _AnimationSpeed;
                float radialTimeOffset = _Time.y * _RadialAnimationSpeed;
                
                float normalizedAngle = (angle + 3.14159) / (2 * 3.14159);
                normalizedAngle += (_AngleOffset / 360.0) + timeOffset;
                normalizedAngle = frac(normalizedAngle);
                
                // 在径向添加动画，模拟向前或向后运动
                float animatedRadius = scaledRadius + radialTimeOffset;
                
                float2 polarUV = float2(normalizedAngle, animatedRadius);
                
                fixed4 col = tex2D(_MainTex, polarUV);
                
                float fadeAlpha = saturate(1.0 - (distanceToView / _FadeDistance));
                col.a *= fadeAlpha;
                
                return col;
            }
            ENDCG
        }
    }
}
