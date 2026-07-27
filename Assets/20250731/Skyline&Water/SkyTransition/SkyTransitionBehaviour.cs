using UnityEngine;
using UnityEngine.Playables;

// ====================================================================
//   Sky Transition Behaviour
//   每个 clip 实例的运行时数据，由 SkyTransitionClip.CreatePlayable 填充
// ====================================================================

[System.Serializable]
public class SkyTransitionBehaviour : PlayableBehaviour
{
    public float startBlend;
    public float endBlend;
    public AnimationCurve curve;
}
