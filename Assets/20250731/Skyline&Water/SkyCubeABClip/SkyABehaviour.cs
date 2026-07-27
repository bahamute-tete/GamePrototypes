using UnityEngine;
using UnityEngine.Playables;

// ====================================================================
//   Sky A Behaviour
//   每个 clip 实例的运行时数据
// ====================================================================

[System.Serializable]
public class SkyABehaviour : PlayableBehaviour
{
    public SkyState startState;
    public SkyState endState;
    public AnimationCurve curve;
}
