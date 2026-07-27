using UnityEngine;
using UnityEngine.Playables;

// ====================================================================
//   Horizon Glow Transition Behaviour
//   每个 clip 实例的运行时数据，由 HorizonGlowTransitionClip.CreatePlayable 填充
// ====================================================================

[System.Serializable]
public class HorizonGlowTransitionBehaviour : PlayableBehaviour
{
    public float startBlend;
    public float endBlend;
    public AnimationCurve curve;
}
