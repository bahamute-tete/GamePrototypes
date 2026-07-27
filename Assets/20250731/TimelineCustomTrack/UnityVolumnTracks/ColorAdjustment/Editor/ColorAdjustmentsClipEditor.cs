#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[CustomEditor(typeof(ColorAdjustmentsClip))]
public class ColorAdjustmentsClipEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);

        EditorGUILayout.HelpBox(
            "Capture: 从绑定的 Volume 读取 ColorAdjustments 当前的真实值,精确填入 template。",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Capture from Bound Volume", GUILayout.Height(28f)))
            {
                CaptureFromVolume();
            }
        }
    }

    private void CaptureFromVolume()
    {
        var clip = target as ColorAdjustmentsClip;
        if (clip == null) return;

        if (!TimelineBindingUtility.TryGetBoundVolume(clip, out var volume))
        {
            Debug.LogWarning("[CA Capture] 找不到绑定的 Volume(Timeline 窗口当前活动状态 + Track 已绑定 Volume)");
            return;
        }

        if (!volume.profile.TryGet<ColorAdjustments>(out var ca))
        {
            Debug.LogWarning($"[CA Capture] Volume「{volume.name}」的 Profile 上未包含 ColorAdjustments 组件。");
            return;
        }

        Undo.RecordObject(clip, "Capture ColorAdjustments from Volume");
        clip.template.postExposure = ca.postExposure.value;
        clip.template.contrast     = ca.contrast.value;
        clip.template.colorFilter  = ca.colorFilter.value;
        clip.template.hueShift     = ca.hueShift.value;
        clip.template.saturation   = ca.saturation.value;
        EditorUtility.SetDirty(clip);

        Debug.Log(
            $"[CA Capture] ← {volume.name}\n" +
            $"  PostExposure = {ca.postExposure.value:F4}\n" +
            $"  Contrast     = {ca.contrast.value:F4}\n" +
            $"  ColorFilter  = {ca.colorFilter.value}\n" +
            $"  HueShift     = {ca.hueShift.value:F4}\n" +
            $"  Saturation   = {ca.saturation.value:F4}");
    }
}
#endif
