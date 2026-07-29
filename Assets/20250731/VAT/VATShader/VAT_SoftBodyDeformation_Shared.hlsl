#ifndef VAT_SOFT_BODY_DEFORMATION_SHARED_INCLUDED
#define VAT_SOFT_BODY_DEFORMATION_SHARED_INCLUDED

// Shared VAT Soft Body decoding code.
//
// Shader Graph Custom Function (File mode):
//   Source  : this file
//   Name    : VATSoftBodyDeformation
//   Precision: Float (Half is also provided)
//
// Pass an explicit Time value (normally Time node -> Time) so the same function
// can be generated for regular Shader Graph and VFX Graph contexts.

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"

struct VATFrameData
{
    float2 currentUV;
    float2 nextUV;
    float interpolation;
    float rawPositionData;
};

struct VATPositionData
{
    float3 offset;
    float packedNormal;
};

struct VATGeometry
{
    float3 positionOS;
    float3 normalOS;
    float3 tangentOS;
};

float VAT_PositiveModulo(float value, float divisor)
{
    return value - divisor * floor(value / divisor);
}

UnityTexture2D VAT_BuildTexture2D(TEXTURE2D_PARAM(textureObject, textureSampler))
{
    return UnityBuildTexture2DStructInternal(
        TEXTURE2D_ARGS(textureObject, textureSampler),
        float4(0.0, 0.0, 0.0, 0.0),
        float4(1.0, 1.0, 0.0, 0.0)
    );
}

VATFrameData VAT_GetFrameData(
    float4 vatUV,
    float currentTime,
    float autoPlayback,
    float gameTimeAtFirstFrame,
    float playbackStartFrame,
    float displayFrame,
    float playbackSpeed,
    float houdiniFPS,
    float frameCountValue,
    float3 boundMaxValue,
    float3 boundMinValue)
{
    VATFrameData output;

    // SideFX encodes texture-layout flags in the decimal portion of the bounds.
    float3 boundMax = boundMaxValue * 10.0;
    float3 boundMin = boundMinValue * 10.0;
    float frameCount = max(1.0, floor(frameCountValue + 0.5));
    float firstFrameIndex = clamp(
        floor(playbackStartFrame) - 1.0,
        0.0,
        frameCount - 1.0
    );
    float playbackFrameCount = max(1.0, frameCount - firstFrameIndex);
    
    // Use the explicit currentTime argument instead of the VFX-only _TimeParameters
    // global so the same function compiles in regular Shader Graph materials and in
    // VFX Graph contexts (wire a Time node into the Time input).
    //
    // The per-instance game-time buffer path is opt-in: it only compiles when the
    // including shader defines VAT_GAME_TIME_PER_INSTANCE before including this
    // file (see VAT_SoftBodyDeformation_Reuse.shader). Shader Graph generated code
    // never defines it, which keeps GPU-instanced material variants compiling.
#if defined(VAT_GAME_TIME_PER_INSTANCE) && (defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED))
    float elapsedTime = currentTime - _gameTimeAtFirstFrameBuffer[unity_InstanceID];
#else
    float elapsedTime = currentTime - gameTimeAtFirstFrame;
#endif
    
    float animationFrame = frac(
        elapsedTime
        * (houdiniFPS / max(playbackFrameCount - 0.01, 0.01))
        * playbackSpeed
    ) * playbackFrameCount;

    float selectedFrame = (autoPlayback > 0.5)
        ? floor(animationFrame) + 1.0
        : floor(displayFrame);

    float currentFrame = firstFrameIndex
        + VAT_PositiveModulo(selectedFrame - 1.0, playbackFrameCount);
    float nextFrame = firstFrameIndex
        + VAT_PositiveModulo(selectedFrame, playbackFrameCount);
    output.interpolation = frac((autoPlayback > 0.5) ? animationFrame : displayFrame);

    float uScale = 1.0 - (ceil(boundMin.z) - boundMin.z);
    float vScale = 1.0 - frac(-boundMax.x);
    float u = vatUV.x * uScale;
    float baseV = (1.0 - vatUV.y) * vScale;

    output.currentUV = float2(
        u,
        1.0 - (baseV + currentFrame / frameCount * vScale)
    );
    output.nextUV = float2(
        u,
        1.0 - (baseV + nextFrame / frameCount * vScale)
    );
    output.rawPositionData = step(0.5, frac(boundMax.z));
    return output;
}

VATPositionData VAT_SamplePosition(
    UnityTexture2D positionTexture,
    UnityTexture2D positionTexture2,
    float2 uv,
    float rawPositionData,
    float loadPositionTexture2,
    float3 boundMaxValue,
    float3 boundMinValue)
{
    float4 positionSample = SAMPLE_TEXTURE2D_LOD(
        positionTexture.tex,
        positionTexture.samplerstate,
        uv,
        0.0
    );
    float3 encodedPosition = positionSample.rgb;

    if (loadPositionTexture2 > 0.5)
    {
        encodedPosition += SAMPLE_TEXTURE2D_LOD(
            positionTexture2.tex,
            positionTexture2.samplerstate,
            uv,
            0.0
        ).rgb * 0.01;
    }

    float3 boundMax = boundMaxValue * 10.0;
    float3 boundMin = boundMinValue * 10.0;

    VATPositionData output;
    output.offset = (rawPositionData > 0.5)
        ? encodedPosition
        : encodedPosition * (boundMax - boundMin) + boundMin;
    output.packedNormal = positionSample.a;
    return output;
}

float3 VAT_DecodeCompressedNormal(float packedValue)
{
    float packed = packedValue * 1024.0;
    float highBits = floor(packed / 32.0);
    float2 encoded = float2(highBits, packed - highBits * 32.0) / 31.5;
    encoded = encoded * 4.0 - 2.0;

    float squaredLength = dot(encoded, encoded);
    float reconstruction = sqrt(saturate(1.0 - squaredLength * 0.25));
    float3 normal = float3(
        -encoded.x * reconstruction,
        1.0 - squaredLength * 0.5,
        encoded.y * reconstruction
    );
    return normalize(clamp(normal, -1.0, 1.0));
}

float4 VAT_DecodeRotation(float4 encodedRotation, float rawPositionData)
{
    return (rawPositionData > 0.5)
        ? encodedRotation
        : encodedRotation * 2.0 - 1.0;
}

float3 VAT_RotateByQuaternion(float3 inputVector, float4 quaternion)
{
    return inputVector + 2.0 * cross(
        quaternion.xyz,
        quaternion.w * inputVector + cross(quaternion.xyz, inputVector)
    );
}

VATGeometry VAT_EvaluateGeometry(
    float3 sourcePositionOS,
    float4 vatUV,
    VATFrameData frameData,
    UnityTexture2D positionTexture,
    UnityTexture2D positionTexture2,
    UnityTexture2D rotationTexture,
    float interpolateFrames,
    float supportSurfaceNormals,
    float useCompressedNormals,
    float loadPositionTexture2,
    float3 boundMaxValue,
    float3 boundMinValue)
{
    VATPositionData current = VAT_SamplePosition(
        positionTexture,
        positionTexture2,
        frameData.currentUV,
        frameData.rawPositionData,
        loadPositionTexture2,
        boundMaxValue,
        boundMinValue
    );
    VATPositionData next = current;

    if (interpolateFrames > 0.5)
    {
        next = VAT_SamplePosition(
            positionTexture,
            positionTexture2,
            frameData.nextUV,
            frameData.rawPositionData,
            loadPositionTexture2,
            boundMaxValue,
            boundMinValue
        );
    }

    VATGeometry output;
    float3 positionOffset = (interpolateFrames > 0.5)
        ? lerp(current.offset, next.offset, frameData.interpolation)
        : current.offset;
    output.positionOS = sourcePositionOS + positionOffset;

    // Vertices with a near-zero VAT V coordinate are padding vertices.
    if (vatUV.y <= 0.1)
    {
        output.positionOS = 0.0;
    }

    if (useCompressedNormals > 0.5)
    {
        float3 currentNormal = VAT_DecodeCompressedNormal(current.packedNormal);
        float3 nextNormal = VAT_DecodeCompressedNormal(next.packedNormal);
        output.normalOS = normalize(
            (interpolateFrames > 0.5)
                ? lerp(currentNormal, nextNormal, frameData.interpolation)
                : currentNormal
        );
        output.tangentOS = 0.0;
    }
    else
    {
        float4 currentRotation = VAT_DecodeRotation(
            SAMPLE_TEXTURE2D_LOD(
                rotationTexture.tex,
                rotationTexture.samplerstate,
                frameData.currentUV,
                0.0
            ),
            frameData.rawPositionData
        );
        float4 nextRotation = currentRotation;

        if (interpolateFrames > 0.5)
        {
            nextRotation = VAT_DecodeRotation(
                SAMPLE_TEXTURE2D_LOD(
                    rotationTexture.tex,
                    rotationTexture.samplerstate,
                    frameData.nextUV,
                    0.0
                ),
                frameData.rawPositionData
            );
        }

        float3 currentNormal = VAT_RotateByQuaternion(
            float3(0.0, 1.0, 0.0),
            currentRotation
        );
        float3 nextNormal = VAT_RotateByQuaternion(
            float3(0.0, 1.0, 0.0),
            nextRotation
        );
        output.normalOS = normalize(
            (interpolateFrames > 0.5)
                ? lerp(currentNormal, nextNormal, frameData.interpolation)
                : currentNormal
        );

        if (supportSurfaceNormals > 0.5)
        {
            float3 currentTangent = VAT_RotateByQuaternion(
                float3(-1.0, 0.0, 0.0),
                currentRotation
            );
            float3 nextTangent = VAT_RotateByQuaternion(
                float3(-1.0, 0.0, 0.0),
                nextRotation
            );
            output.tangentOS = normalize(
                (interpolateFrames > 0.5)
                    ? lerp(currentTangent, nextTangent, frameData.interpolation)
                    : currentTangent
            );
        }
        else
        {
            output.tangentOS = 0.0;
        }
    }

    return output;
}

float3 VAT_EvaluatePosition(
    float3 sourcePositionOS,
    float4 vatUV,
    VATFrameData frameData,
    UnityTexture2D positionTexture,
    UnityTexture2D positionTexture2,
    float interpolateFrames,
    float loadPositionTexture2,
    float3 boundMaxValue,
    float3 boundMinValue)
{
    VATPositionData current = VAT_SamplePosition(
        positionTexture,
        positionTexture2,
        frameData.currentUV,
        frameData.rawPositionData,
        loadPositionTexture2,
        boundMaxValue,
        boundMinValue
    );
    float3 offset = current.offset;

    if (interpolateFrames > 0.5)
    {
        VATPositionData next = VAT_SamplePosition(
            positionTexture,
            positionTexture2,
            frameData.nextUV,
            frameData.rawPositionData,
            loadPositionTexture2,
            boundMaxValue,
            boundMinValue
        );
        offset = lerp(current.offset, next.offset, frameData.interpolation);
    }

    return (vatUV.y <= 0.1) ? 0.0 : sourcePositionOS + offset;
}

float3 VAT_SampleColor(
    VATFrameData frameData,
    UnityTexture2D colorTexture,
    float loadColorTexture,
    float interpolateFrames,
    float interpolateColor)
{
    if (loadColorTexture <= 0.5)
    {
        return 0.0;
    }

    float3 color = SAMPLE_TEXTURE2D_LOD(
        colorTexture.tex,
        colorTexture.samplerstate,
        frameData.currentUV,
        0.0
    ).rgb;

    if (interpolateFrames > 0.5 && interpolateColor > 0.5)
    {
        float3 nextColor = SAMPLE_TEXTURE2D_LOD(
            colorTexture.tex,
            colorTexture.samplerstate,
            frameData.nextUV,
            0.0
        ).rgb;
        color = lerp(color, nextColor, frameData.interpolation);
    }
    return color;
}

void VATSoftBodyDeformation_float(
    float3 SourcePositionOS,
    float4 VATUV,
    float Time,
    UnityTexture2D PositionTexture,
    UnityTexture2D PositionTexture2,
    UnityTexture2D RotationTexture,
    UnityTexture2D ColorTexture,
    float AutoPlayback,
    float GameTimeAtFirstFrame,
    float PlaybackStartFrame,
    float DisplayFrame,
    float PlaybackSpeed,
    float HoudiniFPS,
    float InterpolateFrames,
    float InterpolateColor,
    float SupportSurfaceNormals,
    float UseCompressedNormals,
    float LoadPositionTexture2,
    float LoadColorTexture,
    float FrameCount,
    float3 BoundMax,
    float3 BoundMin,
    out float3 PositionOS,
    out float3 NormalOS,
    out float3 TangentOS,
    out float3 Color,
    out float2 CurrentVATUV,
    out float2 NextVATUV,
    out float FrameInterpolation)
{
    VATFrameData frameData = VAT_GetFrameData(
        VATUV,
        Time,
        AutoPlayback,
        GameTimeAtFirstFrame,
        PlaybackStartFrame,
        DisplayFrame,
        PlaybackSpeed,
        HoudiniFPS,
        FrameCount,
        BoundMax,
        BoundMin
    );
    VATGeometry geometry = VAT_EvaluateGeometry(
        SourcePositionOS,
        VATUV,
        frameData,
        PositionTexture,
        PositionTexture2,
        RotationTexture,
        InterpolateFrames,
        SupportSurfaceNormals,
        UseCompressedNormals,
        LoadPositionTexture2,
        BoundMax,
        BoundMin
    );

    PositionOS = geometry.positionOS;
    NormalOS = geometry.normalOS;
    TangentOS = geometry.tangentOS;
    Color = VAT_SampleColor(
        frameData,
        ColorTexture,
        LoadColorTexture,
        InterpolateFrames,
        InterpolateColor
    );
    CurrentVATUV = frameData.currentUV;
    NextVATUV = frameData.nextUV;
    FrameInterpolation = frameData.interpolation;
}

void VATSoftBodyDeformation_half(
    half3 SourcePositionOS,
    half4 VATUV,
    half Time,
    UnityTexture2D PositionTexture,
    UnityTexture2D PositionTexture2,
    UnityTexture2D RotationTexture,
    UnityTexture2D ColorTexture,
    half AutoPlayback,
    half GameTimeAtFirstFrame,
    half PlaybackStartFrame,
    half DisplayFrame,
    half PlaybackSpeed,
    half HoudiniFPS,
    half InterpolateFrames,
    half InterpolateColor,
    half SupportSurfaceNormals,
    half UseCompressedNormals,
    half LoadPositionTexture2,
    half LoadColorTexture,
    half FrameCount,
    half3 BoundMax,
    half3 BoundMin,
    out half3 PositionOS,
    out half3 NormalOS,
    out half3 TangentOS,
    out half3 Color,
    out half2 CurrentVATUV,
    out half2 NextVATUV,
    out half FrameInterpolation)
{
    float3 positionOSFloat;
    float3 normalOSFloat;
    float3 tangentOSFloat;
    float3 colorFloat;
    float2 currentVATUVFloat;
    float2 nextVATUVFloat;
    float frameInterpolationFloat;

    VATSoftBodyDeformation_float(
        SourcePositionOS,
        VATUV,
        Time,
        PositionTexture,
        PositionTexture2,
        RotationTexture,
        ColorTexture,
        AutoPlayback,
        GameTimeAtFirstFrame,
        PlaybackStartFrame,
        DisplayFrame,
        PlaybackSpeed,
        HoudiniFPS,
        InterpolateFrames,
        InterpolateColor,
        SupportSurfaceNormals,
        UseCompressedNormals,
        LoadPositionTexture2,
        LoadColorTexture,
        FrameCount,
        BoundMax,
        BoundMin,
        positionOSFloat,
        normalOSFloat,
        tangentOSFloat,
        colorFloat,
        currentVATUVFloat,
        nextVATUVFloat,
        frameInterpolationFloat
    );

    PositionOS = positionOSFloat;
    NormalOS = normalOSFloat;
    TangentOS = tangentOSFloat;
    Color = colorFloat;
    CurrentVATUV = currentVATUVFloat;
    NextVATUV = nextVATUVFloat;
    FrameInterpolation = frameInterpolationFloat;
}

#endif
