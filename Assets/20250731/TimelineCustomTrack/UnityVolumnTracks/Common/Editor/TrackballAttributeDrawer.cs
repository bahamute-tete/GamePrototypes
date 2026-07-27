#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TrackballAttribute))]
public class TrackballAttributeDrawer : PropertyDrawer
{
    // 右侧 offset 数值条占用宽度
    private const float k_OffsetFieldWidth = 110f;
    // ColorField 与 Slider 之间的间隙
    private const float k_Spacing          = 4f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Vector4)
        {
            EditorGUI.LabelField(position, label.text, "[Trackball] 仅支持 Vector4");
            return;
        }

        var attr = (TrackballAttribute)attribute;

        EditorGUI.BeginProperty(position, label, property);

        // 切出 label 区域,剩余给控件
        var contentRect = EditorGUI.PrefixLabel(position, label);

        // 区域划分: [ColorField .......... ] [OffsetSlider]
        float colorWidth = contentRect.width - k_OffsetFieldWidth - k_Spacing;
        var colorRect = new Rect(contentRect.x, contentRect.y, colorWidth, contentRect.height);
        var offsetRect = new Rect(colorRect.xMax + k_Spacing, contentRect.y,
                                  k_OffsetFieldWidth, contentRect.height);

        var v = property.vector4Value;
        var rgb = new Color(v.x, v.y, v.z, 1f);

        EditorGUI.BeginChangeCheck();

        // 参数顺序: position, label, value, showEyedropper, showAlpha, hdr
        var newRgb = EditorGUI.ColorField(colorRect, GUIContent.none, rgb,
                                         showEyedropper: true,
                                         showAlpha: false,
                                         hdr: attr.hdr);

        // Slider:左侧滑条 + 右侧数值
        var newOffset = EditorGUI.Slider(offsetRect, GUIContent.none, v.w,
                                         attr.minOffset, attr.maxOffset);

        if (EditorGUI.EndChangeCheck())
        {
            property.vector4Value = new Vector4(newRgb.r, newRgb.g, newRgb.b, newOffset);
        }

        EditorGUI.EndProperty();
    }
}
#endif
