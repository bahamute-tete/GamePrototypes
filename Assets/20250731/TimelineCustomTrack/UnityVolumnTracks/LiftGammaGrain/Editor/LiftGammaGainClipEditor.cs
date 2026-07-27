#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[CustomEditor(typeof(LiftGammaGainClip))]
public class LiftGammaGainClipEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);

        EditorGUILayout.HelpBox(
            "Capture: 从当前 Timeline 上为本轨道绑定的 Volume 读取 LiftGammaGain 的真实 Vector4,精确填入 template。\n" +
            "用法:Volume 上用色环调出想要的「起点」状态 → 在 Clip 上点 Capture → 该 Clip 起点即与 Volume 一致,过渡无跳变。",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Capture from Bound Volume", GUILayout.Height(28f)))
            {
                CaptureFromVolume();
            }
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play Mode 下修改 template 的数据在退出后会丢失。",
                MessageType.Warning);
        }
    }

    private void CaptureFromVolume()
    {
        var clip = target as LiftGammaGainClip;
        if (clip == null) return;

        if (!TimelineBindingUtility.TryGetBoundVolume(clip, out var volume))
        {
            Debug.LogWarning("[LGG Capture] 找不到绑定的 Volume。\n" +
                             "请确认:\n  1) 当前 Timeline 处于 Timeline 窗口的活动状态;\n  2) 本轨道已绑定一个含 LiftGammaGain 的 Volume。");
            return;
        }

        if (!volume.profile.TryGet<LiftGammaGain>(out var lgg))
        {
            Debug.LogWarning($"[LGG Capture] Volume「{volume.name}」的 Profile 上未包含 LiftGammaGain 组件。");
            return;
        }

        Undo.RecordObject(clip, "Capture LiftGammaGain from Volume");
        clip.template.lift  = lgg.lift.value;
        clip.template.gamma = lgg.gamma.value;
        clip.template.gain  = lgg.gain.value;
        EditorUtility.SetDirty(clip);

        Debug.Log(
            $"[LGG Capture] ← {volume.name}\n" +
            $"  Lift  = {Format(lgg.lift.value)}    override={lgg.lift.overrideState}\n" +
            $"  Gamma = {Format(lgg.gamma.value)}    override={lgg.gamma.overrideState}\n" +
            $"  Gain  = {Format(lgg.gain.value)}    override={lgg.gain.overrideState}");
    }

    private static string Format(Vector4 v)
        => $"({v.x:F4}, {v.y:F4}, {v.z:F4}, {v.w:F4})";
}
#endif
