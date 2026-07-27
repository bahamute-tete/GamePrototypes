using System;
using UnityEngine;

namespace LiangZhu.Geometry.Curves
{
    /// <summary>
    /// 曲线地面投影：沿曲线参数 t 均匀采样，每个采样点交给 groundSampler 求地面高度，
    /// 产出按 t 索引的高度 LUT（float[]）。纯几何、无场景依赖——地面查询通过回调注入
    /// （上层通常用 LiangZhu.Geometry.RayMesh 向下射线实现）。
    /// 约定：第 i 项对应 t=i/(count-1)，t∈[0,1] 含端点（闭环时末项与首项重合，无害）；
    /// 命中项写 hitY+yOffset；未命中项回退为解析点自身的 Y（该处保持原高度，平滑过渡到命中邻居），不叠加偏移。
    /// </summary>
    public static class GroundProjection
    {
        public static float[] BakeHeightLut(
            CatmullRomEvaluator eval, int count, float yOffset,
            Func<Vector3, (bool hit, float y)> groundSampler)
        {
            if (eval == null || groundSampler == null) return null;
            count = Mathf.Max(2, count);

            var lut = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                Vector3 p = eval.Evaluate(t);          // 解析点：取其 XZ 投影，Y 作未命中回退
                var r = groundSampler(p);
                lut[i] = r.hit ? r.y + yOffset : p.y;  // 未命中 → 保持解析原 Y
            }
            return lut;
        }
    }
}