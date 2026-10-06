using UnityEngine;

namespace SlotSystem
{
    /// <summary>
    /// 一次挂载的句柄。Attach 返回此对象,Detach 时回传以精确解绑,
    /// 避免用 GameObject 引用反查导致歧义。
    /// </summary>
    public class SlotAttachment
    {
        public SlotManager Owner { get; internal set; }
        public string SlotId { get; internal set; }
        public GameObject Attached { get; internal set; }
        public Transform Anchor { get; internal set; }
        public bool DestroyOnDetach { get; internal set; }

        /// <summary>挂载物是否仍然有效(未被销毁、未被解绑)。</summary>
        public bool IsValid => Owner != null && Attached != null && !_detached &&
            Attached.transform.parent == Anchor;

        internal bool _detached;
    }
}
