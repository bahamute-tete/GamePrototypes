using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.ProcMesh
{
    /// <summary>
    /// 把现有 CurveResamplerAuthoring(路点驱动)适配成 ICurveSource,零侵入,不改原文件。
    /// 用法:new WaypointCurveSource(authoring),或直接喂给 SweepBatch / 消费组件。
    /// 注意这是普通类(非 MonoBehaviour),只能在代码里用,不能拖进 Inspector。
    /// </summary>
    public sealed class WaypointCurveSource : ICurveSource
    {
        readonly CurveResamplerAuthoring _authoring;
        readonly ResampleResult[] _one = new ResampleResult[1];

        public WaypointCurveSource(CurveResamplerAuthoring authoring)
            => _authoring = authoring;

        public IReadOnlyList<ResampleResult> GetCurves()
        {
            if (_authoring == null || !_authoring.Result.IsValid)
                return Array.Empty<ResampleResult>();
            _one[0] = _authoring.Result;          // Result 处于 authoring 自身 local space
            return _one;
        }

        public Matrix4x4 CurveToWorld =>
            _authoring != null ? _authoring.transform.localToWorldMatrix : Matrix4x4.identity;
    }
}
