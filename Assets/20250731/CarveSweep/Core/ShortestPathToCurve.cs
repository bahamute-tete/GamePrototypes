using LiangZhu.Geometry;

namespace LiangZhu.ProcMesh
{
    /// <summary>
    /// 把最短路 PathResult 转成 ResampleResult。核心思路:PathResult.Points 就是一串 Vector3,
    /// 直接当 waypoints 喂给现有 CurveResampler.Resample —— CurveResampler 一行都不用改。
    /// </summary>
    public static class ShortestPathToCurve
    {
        public static bool TryConvert(
            in PathResult path,
            ResampleMode mode, ResampleSpec spec, float specValue,
            float catmullAlpha, int subdivPerSeg,
            out ResampleResult result,
            float simplifyEpsilon = 0f)
        {
            result = default;
            // 不可达 / 退化路径(起止吸附到同一节点等)跳过
            if (!path.Reachable || path.Points == null || path.Points.Length < 2)
                return false;

            // 平滑前先 RDP 抽稀去边尺度锯齿;epsilon<=0 时原样返回(等于关闭)
            var pts = simplifyEpsilon > 0f
                ? PolylineSimplifier.Decimate(path.Points, simplifyEpsilon)
                : path.Points;
            if (pts.Length < 2) return false; // 抽稀必留首尾,这里只是保险

            // 最短路是开放曲线,closed 恒为 false
            result = CurveResampler.Resample(
                pts, mode, spec, specValue,
                closed: false, catmullAlpha, subdivPerSeg);

            return result.IsValid;
        }
    }
}
