using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.ProcMesh
{
    /// <summary>
    /// 任意"曲线来源"的统一出口。GetCurves 返回来源自身空间(通常是某个 mesh 的 local)的曲线;
    /// CurveToWorld 把该空间映射到世界。消费方再用自身的 worldToLocal 转进渲染空间,
    /// 这样不同来源(路点物体、表面最短路…)能在世界系下正确对齐。
    /// </summary>
    public interface ICurveSource
    {
        IReadOnlyList<ResampleResult> GetCurves();
        Matrix4x4 CurveToWorld { get; }
    }

    /// <summary>
    /// 可选:暴露一个版本号,每次曲线真正重算时自增。消费方据此判断要不要重建,
    /// 避免在几何没变(例如只是拖生长滑块)时白白重跑昂贵的下游。
    /// </summary>
    public interface ICurveVersion
    {
        int Version { get; }
    }
}
