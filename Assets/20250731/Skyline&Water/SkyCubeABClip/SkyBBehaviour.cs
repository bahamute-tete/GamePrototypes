using UnityEngine;
using UnityEngine.Playables;

// ====================================================================
//   Sky B Behaviour
//   每个 clip 实例的运行时数据
// ====================================================================

[System.Serializable]
public class SkyBBehaviour : PlayableBehaviour
{
    public SkyState startState;
    public SkyState endState;
    public AnimationCurve curve;
}
