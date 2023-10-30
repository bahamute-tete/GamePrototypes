
using UnityEngine;

[System.Serializable]
public struct FloatRange
{
    public float min, max;
    public float RandomValue => Random.Range(min, max);

    public FloatRange(float min, float max)
    { 
        this.min = min;
        this.max = max;
    }

    //建新范围的方法
    public FloatRange GrowExtents(float extents)
    {
        return new FloatRange(min - extents, max + extents);
    }

    //用于移动范围
    public FloatRange Shift(float shift)
    {
        return new FloatRange(min + shift, max + shift);
    }

    //从某个位置向两个方向扩展的范围
    public static FloatRange PositionExtents(float position, float extents)
    {
        return new FloatRange(position - extents, position + extents);
    }
}
