using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(Animator))]
public class CharacterAnimationDemo : MonoBehaviour
{
    public AnimationClip idleClip;
    public AnimationClip walkClip;
    public AnimationClip runClip;
    

    [Range(0f, 1f)]
    public float moveBlend;

    [Min(0f)]
    public float blendDuration = 0.2f;

    // 显示出来，方便观察；运行时由代码控制。
    [SerializeField]
    private float currentBlend;

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;

    private void OnEnable()
    {
        if (idleClip == null || walkClip == null || runClip ==null)
        {
            Debug.LogError("请先指定 Idle , Walk, Run 动画。", this);
            return;
        }

        // 1. 创建图，由游戏时间推动动画。
        graph = PlayableGraph.Create("CharacterAnimationDemo");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        // 2. 创建两个动画节点。
        var idle = AnimationClipPlayable.Create(graph, idleClip);
        var walk = AnimationClipPlayable.Create(graph, walkClip);
        var run = AnimationClipPlayable.Create(graph, runClip);

        // 3. 创建具有两个输入口的混合节点。
        mixer = AnimationMixerPlayable.Create(graph, 3);

        // 将动画节点的输出口 0，接到混合节点的输入口 0 和 1。
        graph.Connect(idle, 0, mixer, 0);
        graph.Connect(walk, 0, mixer, 1);
        graph.Connect(run, 0, mixer, 2);

        // 4. 将混合结果输出给角色的 Animator。
        var output = AnimationPlayableOutput.Create(
            graph, "Animation", GetComponent<Animator>());

        output.SetSourcePlayable(mixer);

        currentBlend = Mathf.Clamp01(moveBlend);

        UpdateWeights();
        graph.Play();
    }

    private void Update()
    {
        if (!graph.IsValid())
            return;

        float targetBlend = Mathf.Clamp01(moveBlend);

        if (blendDuration <= 0f)
        {
            currentBlend = targetBlend;
        }
        else
        {
            currentBlend = Mathf.MoveTowards(
                currentBlend,
                targetBlend,
                Time.deltaTime / blendDuration);
        }

        UpdateWeights();
    }

    private void UpdateWeights()
    {
        float blend = Mathf.Clamp01(currentBlend);

        float idleWeight;
        float walkWeight;
        float runWeight;

        if (blend <= 0.5f)
        {
            float t = blend * 2.0f;
            idleWeight = 1.0f - t;
            walkWeight = t;
            runWeight = 0;
        }
        else
        {
            float t = (blend - 0.5f) * 2.0f;

            idleWeight = 0f;
            walkWeight = 1.0f - t;
            runWeight = t;

        }
        mixer.SetInputWeight(0, idleWeight);
        mixer.SetInputWeight(1, walkWeight);
        mixer.SetInputWeight(2, runWeight);
    }

    private void OnDisable()
    {
        // Graph 需要手动销毁，其中的节点和输出会一起释放。
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }

    public void SetMoveBlend(float value)
    {
        moveBlend = Mathf.Clamp01(value);
    }
}