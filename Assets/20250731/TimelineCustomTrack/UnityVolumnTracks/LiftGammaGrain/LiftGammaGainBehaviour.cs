using UnityEngine;
using UnityEngine.Playables;

// xyz = RGB 通道乘数,w = offset
// 重要:URP 内部 LGG 存的 xyz 直接就是 RGB(色环只是 UI 工具);
//      Volume Inspector 上 offset 滑条显示的 [-0.16, 2] 是 URP 对内部 [-1, 1]
//      做了 power 映射后的显示值,真正写入 Vector4Parameter 的 w 仍在 [-1, 1] 内。
//      想精确对齐 Volume 当前状态,用 Inspector 下方的「Capture from Bound Volume」按钮。
[System.Serializable]
public class LiftGammaGainBehaviour : PlayableBehaviour
{
    [Trackball(-2f, 2f, hdr: true)]
    [Tooltip("暗部(Shadows)。xyz=RGB,w=offset。\n用 Capture 按钮可从绑定的 Volume 同步当前精确数值。")]
    public Vector4 lift  = new Vector4(1f, 1f, 1f, 0f);

    [Trackball(-2f, 2f, hdr: true)]
    [Tooltip("中调(Midtones)。xyz=RGB,w=offset。\n用 Capture 按钮可从绑定的 Volume 同步当前精确数值。")]
    public Vector4 gamma = new Vector4(1f, 1f, 1f, 0f);

    [Trackball(-2f, 2f, hdr: true)]
    [Tooltip("亮部(Highlights)。xyz=RGB,w=offset。\n用 Capture 按钮可从绑定的 Volume 同步当前精确数值。")]
    public Vector4 gain  = new Vector4(1f, 1f, 1f, 0f);
}
