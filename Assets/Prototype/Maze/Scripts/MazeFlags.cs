using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Flags]
public enum MazeFlags
{
    //������д��
    Empty = 0,
    PassageN = 0b0001,
    PassageE = 0b0010,
    PassageS = 0b0100,
    PassageW = 0b1000,

    PassageAll = 0b1111

}

public static class MazeFlagsExtentions
{
    //�����this ָ�����ǵ������������ʵ������
    public static bool Has(this MazeFlags flags, MazeFlags mask) =>
        (flags & mask) == mask;

    public static bool HasAny(this MazeFlags flags, MazeFlags mask) =>
        (flags & mask) != 0;

    public static bool HasNot(this MazeFlags flags, MazeFlags mask) =>
     (flags & mask) != mask;

    //����Ƿ�ֻ��һ��ͨ���ķ��� ������2������2������
    public static bool HasExactlyOne(this MazeFlags flags) =>
    flags != 0 && (flags & (flags - 1)) == 0;

    public static MazeFlags With(this MazeFlags flags, MazeFlags mask) =>
    flags | mask;

    public static MazeFlags Without(this MazeFlags flags, MazeFlags mask) =>
    flags & ~mask;

}
