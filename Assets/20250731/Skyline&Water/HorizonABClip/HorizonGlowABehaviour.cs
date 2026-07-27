using UnityEngine;
using UnityEngine.Playables;

// ====================================================================
//   Horizon Glow A Behaviour
//   每个 clip 实例的运行时数据
// ====================================================================

[System.Serializable]
public class HorizonGlowABehaviour : PlayableBehaviour
{
    public HorizonGlowState startState;
    public HorizonGlowState endState;
    public AnimationCurve   curve;
}
