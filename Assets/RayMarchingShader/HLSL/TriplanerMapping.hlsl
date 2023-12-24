#ifndef RAYMARCHING_TRIPLANEMAPPING_INCLUDED
#define RAYMARCHING_TRIPLANEMAPPING_INCLUDED


struct TriplanarUV {
	float2 x, y, z;
};


struct TriplanarOutput {
	float3 texColor;
	float3 normal;
    float4 control;// r =metallic, g = occlusion, a = roughness
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
float4 _MainTex_ST;

TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

TEXTURE2D(_MOSMap);
SAMPLER(sampler_MOSMap);

TEXTURE2D(_TopMainTex);
SAMPLER(sampler_TopMainTex);


TEXTURE2D(_TopMOHSMap);
SAMPLER(sampler_TopMOHSMap);

TEXTURE2D(_TopNormalMap);
SAMPLER(sampler_TopNormalMap);

float _MapScale;

TriplanarUV GetTriplanarUV (float3 p,float3 n) {
	TriplanarUV triUV;
    p = p * _MapScale;
	triUV.x = p.zy;
	triUV.y = p.xz;
	triUV.z = p.xy;

    if (n.x <0.) {
		triUV.x.x = -triUV.x.x;
	}

	if (n.y <0.) {
		triUV.y.x = -triUV.y.x;
	}

	if (n.z>=0.) {
		triUV.z.x = -triUV.z.x;
	}

    triUV.x.y += 0.5;
	triUV.z.x += 0.5;

	return triUV;
}

float3 GetTriplanarWeights (float3 n) {
	float3 triW = abs(n);
	return triW / (triW.x + triW.y + triW.z);
}


float3 BlendTriplanarNormal (float3 mappedNormal, float3 surfaceNormal) {
	float3 n;
	n.xy = mappedNormal.xy + surfaceNormal.xy;
	n.z = mappedNormal.z * surfaceNormal.z;
	return n;
}


TriplanarOutput TriplanarMapping (float3 p, float3 n)
{
    TriplanarUV triUV;
    TriplanarOutput output;

    triUV  = GetTriplanarUV(p,n);

    float3 ty = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,triUV.y).rgb;
    float3 tx = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,triUV.x).rgb;
    float3 tz = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,triUV.z).rgb;
    float3 triW = GetTriplanarWeights(n);

    float4 my = SAMPLE_TEXTURE2D(_MOSMap,sampler_MOSMap,triUV.y);
    float4 mx = SAMPLE_TEXTURE2D(_MOSMap,sampler_MOSMap,triUV.x);
    float4 mz = SAMPLE_TEXTURE2D(_MOSMap,sampler_MOSMap,triUV.z);


    float3 tangentNormalX = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap, triUV.x));
    float3 tangentNormalY = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap, triUV.y));
    float3 tangentNormalZ = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap, triUV.z));


    #if defined(_SEPARATE_TOP_MAPS)
        if (n.y > 0) {
            ty = SAMPLE_TEXTURE2D(_TopMainTex,sampler_TopMainTex,triUV.y).rgb;
            my = SAMPLE_TEXTURE2D(_TopMOHSMap,sampler_TopMOHSMap, triUV.y);
            tangentNormalY = UnpackNormal(SAMPLE_TEXTURE2D(_TopNormalMap,sampler_TopNormalMap, triUV.y));
        }
    #endif


    if (n.x < 0) {
    tangentNormalX.x = -tangentNormalX.x;

    }
    if (n.y < 0) {
    tangentNormalY.x = -tangentNormalY.x;

    }
    if (n.z >= 0) {
    tangentNormalZ.x = -tangentNormalZ.x;
    }


    float3 worldNormalX =
    BlendTriplanarNormal(tangentNormalX, n.zyx).zyx;
    float3 worldNormalY =
    BlendTriplanarNormal(tangentNormalY, n.xzy).xzy;
    float3 worldNormalZ =
    BlendTriplanarNormal(tangentNormalZ, n);

    float3 normal = normalize(worldNormalX * triW.x + worldNormalY * triW.y + worldNormalZ * triW.z);


    output.texColor = ty*n.y+tx*n.x+tz*n.z;
    output.normal =normal;
    output.control = mx * triW.x + my * triW.y + mz * triW.z;

    return output;
}



#endif
