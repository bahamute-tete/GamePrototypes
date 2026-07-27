using UnityEngine;

namespace LiangZhu.Geometry.Curves
{
    /// <summary>
    /// 曲线求值统一接口。位置 / 导数以全局归一化参数 t∈[0,1] 表达，段解析为实现内部细节。
    /// 实现：CatmullRomEvaluator；以后可加 BSplineEvaluator / NurbsEvaluator，
    /// 经本接口被 ArcLengthTable / ParallelTransportFrames / CurveAnalysis 等通用工具直接复用。
    /// （与 IRayAccelerator/BVH 同一套"接口 + 可替换实现"模式。）
    /// </summary>
    public interface ICurveEvaluator
    {
        bool IsLoop { get; }
        int SegmentCount { get; }

        /// <summary>全局 t∈[0,1] 处的位置。</summary>
        Vector3 Evaluate(float t);

        /// <summary>全局 t∈[0,1] 处的未归一化一阶导数（切线方向）。</summary>
        Vector3 EvaluateDerivative(float t);
    }
}
