using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Flags]
public enum MazeFlags
{
    //二进制写法
    Empty = 0,
    PassageN = 0b0001,
    PassageE = 0b0010,
    PassageS = 0b0100,
    PassageW = 0b1000,

    PassageAll = 0b1111

}

public static class MazeFlagsExtentions
{
    //这里的this 指代的是调用这个方法的实例本身
    public static bool Has(this MazeFlags flags, MazeFlags mask) =>
        (flags & mask) == mask;

    public static bool HasAny(this MazeFlags flags, MazeFlags mask) =>
        (flags & mask) != 0;

    public static bool HasNot(this MazeFlags flags, MazeFlags mask) =>
     (flags & mask) != mask;

    //检查是否只有一个通道的方向 而不是2个或者2个以上
    public static bool HasExactlyOne(this MazeFlags flags) =>
    flags != 0 && (flags & (flags - 1)) == 0;

    public static MazeFlags With(this MazeFlags flags, MazeFlags mask) =>
    flags | mask;

    public static MazeFlags Without(this MazeFlags flags, MazeFlags mask) =>
    flags & ~mask;

}
