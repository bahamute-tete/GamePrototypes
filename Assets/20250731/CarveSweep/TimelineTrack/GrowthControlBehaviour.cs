using UnityEngine;
using UnityEngine.Playables;

namespace LiangZhu.ProcMesh.Timeline
{
    /// <summary>单个生长 clip 的数据：从 from 生长到 to，沿 ease 曲线按 clip 进度推进。</summary>
    [System.Serializable]
    public class GrowthControlBehaviour : PlayableBehaviour
    {
        [Tooltip("clip 开始时的 _GrowT")]
        public float from = 0f;

        [Tooltip("clip 结束时的 _GrowT。归一化模式 0..1；距离模式填世界距离")]
        public float to = 1f;

        [Tooltip("clip 进度(0..1) -> 生长插值的缓动曲线")]
        public AnimationCurve ease = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
}
