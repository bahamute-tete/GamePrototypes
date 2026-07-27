using System;
using UnityEngine;

// 标记一个 Vector4 字段:xyz = RGB 颜色,w = offset
// 在 Inspector 中渲染为「ColorField + Offset Slider」一行
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class TrackballAttribute : PropertyAttribute
{
    public readonly float minOffset;
    public readonly float maxOffset;
    public readonly bool  hdr;

    public TrackballAttribute(float minOffset = -1f, float maxOffset = 1f, bool hdr = true)
    {
        this.minOffset = minOffset;
        this.maxOffset = maxOffset;
        this.hdr       = hdr;
    }
}
