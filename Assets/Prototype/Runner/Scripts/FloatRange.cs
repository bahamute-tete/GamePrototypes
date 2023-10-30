
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

    //���·�Χ�ķ���
    public FloatRange GrowExtents(float extents)
    {
        return new FloatRange(min - extents, max + extents);
    }

    //�����ƶ���Χ
    public FloatRange Shift(float shift)
    {
        return new FloatRange(min + shift, max + shift);
    }

    //��ĳ��λ��������������չ�ķ�Χ
    public static FloatRange PositionExtents(float position, float extents)
    {
        return new FloatRange(position - extents, position + extents);
    }
}
