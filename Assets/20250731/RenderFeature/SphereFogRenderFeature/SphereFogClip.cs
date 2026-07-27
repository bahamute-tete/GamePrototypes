// =============================================================================
//  SphereFogClip.cs
//  Timeline 上 Sphere Fog Track 的 Clip 定义。
//  每个 Clip = 一份"雾状态快照",多个 Clip 之间用 Timeline 的 blend 平滑过渡。
//
//  文件名必须 == 类名 (SphereFogClip),否则 PlayableAsset 的 MonoScript 引用断开。
//
//  支持的参数:
//    - Fog: smoothness / density / fogColor
//    - Skybox: skyDistance
//    - Noise: noiseScale / noiseStrength / noiseSpeed
//    - Transform (可选,driveTransform 勾上才驱动): position / rotationEuler / scale
//
//  不支持的参数 (需要时用 Signal Track 触发,或直接代码控制):
//    - noiseTexture (Texture2D 引用,不能数值混合)
//    - affectSkybox (bool 开关)
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class SphereFogBehaviour : PlayableBehaviour
{
    [Header("Fog")]
    [Min(0.001f)] public float smoothness = 5f;
    [Range(0, 1)] public float density    = 1f;
    [ColorUsage(true, true)]
    public Color fogColor = new Color(0.6f, 0.65f, 0.7f, 1f);

    [Header("Skybox")]
    [Min(0.1f)] public float skyDistance = 50f;

    [Header("Noise")]
    public float   noiseScale    = 0.1f;
    public float   noiseStrength = 3f;
    public Vector3 noiseSpeed    = new Vector3(0.02f, 0.01f, 0.015f);

    [Header("Transform (勾选才驱动 Volume 的 Transform)")]
    [Tooltip("勾上 → 本 Clip 会写 Volume 的 position/rotation/scale\n" +
             "不勾 → Volume 的 Transform 保持场景里的设置\n" +
             "通常只在需要动雾盒位置/大小的关键 Clip 上勾,其他 Clip 留着不勾")]
    public bool driveTransform = false;

    public Vector3 position;
    public Vector3 rotationEuler;
    public Vector3 scale = Vector3.one;
}

public class SphereFogClip : PlayableAsset, ITimelineClipAsset
{
    public SphereFogBehaviour template = new SphereFogBehaviour();

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.Looping;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<SphereFogBehaviour>.Create(graph, template);
    }
}
