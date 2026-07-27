using UnityEngine;
using UnityEngine.Playables;

// ====================================================================
//   Horizon Glow B Behaviour
//   每个 clip 实例的运行时数据
// ====================================================================

[System.Serializable]
public class HorizonGlowBBehaviour : PlayableBehaviour
{
    public HorizonGlowState startState;
    public HorizonGlowState endState;
    public AnimationCurve   curve;
}
