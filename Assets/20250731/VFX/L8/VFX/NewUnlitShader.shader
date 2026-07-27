Shader "Unlit/NewUnlitShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }


            float4 texture0( in float2 x )
            {
                //return texture( iChannel0, x );
                float2 res = iChannelResolution[0].xy;
                float2 u = x*res - 0.5;
                float2 p = floor(u);
                float2 f = fract(u);
                f = f*f*(3.0-2.0*f);    
                float4 a = texture( iChannel0, (p+float2(0.5,0.5))/res, -64.0 );
	            float4 b = texture( iChannel0, (p+float2(1.5,0.5))/res, -64.0 );
	            float4 c = texture( iChannel0, (p+float2(0.5,1.5))/res, -64.0 );
	            float4 d = texture( iChannel0, (p+float2(1.5,1.5))/res, -64.0 );
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x),f.y);
            }
    
            float2 flow( float2 uv, in float2x2 m )
            {
                for( int i=0; i<50; i++ )
                    uv += 0.00015 * m * (-1.0+2.0*texture0(0.5*uv).xz);
                return uv;
            }

            void mainImage( out float4 fragColor, in float2 fragCoord )
            {
                float2 p = fragCoord.xy / iResolution.xy;

                // animate
                float an = 0.5*iTime;
                float co = cos(an);
                float si = sin(an);
                float2x2  ma = float2x2( co, -si, si, co );
    

                // orbit, distance and distance gradient
                float2 uva = 0.05*(p + float2(1.0,0.0)/iResolution.xy);
	            float2 uvb = 0.05*(p + float2(0.0,1.0)/iResolution.xy);
	            float2 uvc = 0.05*p;
	            float2 nuva = flow( uva, ma );
	            float2 nuvb = flow( uvb, ma );
	            float2 nuvc = flow( uvc, ma );
                float fa = length(nuva-uva)*95.0;
                float fb = length(nuvb-uvb)*95.0;
                float fc = length(nuvc-uvc)*95.0;
                vec3 nor = normalize( vec3((fa-fc)*iResolution.x,1.0,(fb-fc)*iResolution.y ) );

                // material
  	            vec3 col = 0.2 + 0.8*texture(iChannel1, 50.0*nuvc).xyz;
                col *= 1.0 + 0.15*nor;
                float ss, sw;
                ss = sin(6000.0*nuvc.x); sw = fwidth(ss); col *= 0.5 + 0.5*smoothstep(-sw,sw,ss+0.95);
                ss = sin(6000.0*nuvc.y); sw = fwidth(ss); col *= 0.5 + 0.5*smoothstep(-sw,sw,ss+0.95);
    
                // ilumination
                vec3 lig = normalize( vec3( 1.0,1.0,-0.4 ) );
                col *= vec3(0.7,0.8,0.9) + vec3(0.6,0.5,0.4)*clamp( dot(nor,lig), 0.0, 1.0 );    
                col += 0.40*pow( nor.y, 4.0 );
                col += 0.15*pow( nor.y, 2.0 );
                col *= sqrt( fc*fc*fc );
 
                // postpro
                col = 1.5*pow( col+vec3(0.0,0.0,0.015), vec3(0.6,0.8,1.0) );
                col *= 0.5 + 0.5*sqrt( 16.0*p.x*p.y*(1.0-p.x)*(1.0-p.y) );

                fragColor = float4( col, 1.0 );
            }

            
// 常量定义
const vec2 halfXY = vec2(0.5, 0.5);

// UV初始化（标准化坐标）
vec2 uv = fragCoord.xy / iResolution.xy;

// 基础网格处理
vec2 baseXY(vec2 uv) {
    scaledUv = uv * gridRes;
    gridC = floor(scaledUv);
    return (gridC / gridRes);
}

// 主UV处理流程（包含时间动态扰动）
void processUV(inout vec2 uv, float time) {
    // 中心化处理
    uv = uv - halfXY;
    
    // 基础扰动（低频正弦波）
    uv += vec2(sin(time / 47.), cos(time / 37.));
    
    // 极坐标变换
    float theta = atan2(uv.x, uv.y);
    float r = length(uv);
    
    // 动态波纹效果
    vec2 rg = vec2(
        0.5 + 0.5*cos(theta + time/37. + sin(7.*r + time/5.)),
        0.5 + 0.5*sin(theta + time/13. + cos(11.*r + time/17.))
    );
    
    // 次级扰动计算
    vec2 uvN = sqrt(abs(uv - rg));
    float thetaN = atan2(uvN.y, uvN.x);
    float rN = length(uvN);
    
    // 复合扰动
    rg = rg*halfXY + halfXY * vec2(
        rg.x + pow(cos(thetaN + sin(rN + time/5.) + time/43.), 2.),
        rg.y + pow(sin(thetaN + cos(rN + time/11.) + time/31.), 2.)
    );
    
    // 频率调制
    rg *= vec2(
        abs(sin(rg.x * 17. / 5.)),
        abs(cos(rg.y * 23. / 3.))
    );
    
    // 最终混合
    float thetaR = atan2(rg.x, rg.y);
    float rgM = length(rg);
    rg = halfXY * (rg + (halfXY + halfXY*vec2(
        sin(thetaR * rgM)*cos(thetaR * rgM),
        cos(thetaR + time/47.)*sin(rgM)
    )));
    
    // 非线性混合
    rg = vec2(
        mix(sqrt(rg.x), rg.x*rg.x, clamp(rg.y - rg.x, 0., 1.)),
        mix(sqrt(rg.y), rg.y*rg.y, clamp(rg.x - rg.y, 0., 1.))
    );
}


            ENDCG
        }
    }
}
