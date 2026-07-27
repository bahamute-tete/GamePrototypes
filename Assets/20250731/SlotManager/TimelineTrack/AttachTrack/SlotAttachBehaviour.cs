using System;
using UnityEngine;
using UnityEngine.Playables;

namespace SlotSystem.Timeline
{
    /// <summary>
    /// 单个挂载 clip 的运行数据。
    /// resolvedTarget 在 SlotAttachClip.CreatePlayable 中由 ExposedReference 解析后写入。
    /// </summary>
    [Serializable]
    public class SlotAttachBehaviour : PlayableBehaviour
    {
        // 运行时解析的挂载物,不序列化(序列化的是 clip 上的 ExposedReference)
        [NonSerialized] public GameObject resolvedTarget;

        public string slotId;
        public AttachMode attachMode = AttachMode.Snap;
    }
}
