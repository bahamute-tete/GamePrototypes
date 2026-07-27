// Assets/Scripts/Timeline/TimelineRebindUtility.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Timeline 重绑定通用工具:定位片段资源上的 ExposedReference 字段并读取其 exposedName。
/// 运行时安全(不依赖 UnityEditor),供 TimelineNameBinder 与拷贝窗口复用。
/// </summary>
public static class TimelineRebindUtility
{
    private static readonly Dictionary<Type, FieldInfo[]> s_Cache = new Dictionary<Type, FieldInfo[]>();
    private static readonly Type s_ExposedRefDef = typeof(ExposedReference<>);

    /// <summary>取得某类型(通常是 clip.asset 的类型)上所有 ExposedReference&lt;T&gt; 字段(含私有 [SerializeField])。</summary>
    public static FieldInfo[] GetExposedReferenceFields(Type type)
    {
        if (type == null) return Array.Empty<FieldInfo>();
        if (s_Cache.TryGetValue(type, out var cached)) return cached;

        var list = new List<FieldInfo>();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var f in type.GetFields(flags))
        {
            var ft = f.FieldType;
            if (ft.IsGenericType && ft.GetGenericTypeDefinition() == s_ExposedRefDef)
                list.Add(f);
        }
        var arr = list.ToArray();
        s_Cache[type] = arr;
        return arr;
    }

    /// <summary>从对象的某个 ExposedReference 字段读取 exposedName。</summary>
    public static PropertyName GetExposedName(object owner, FieldInfo field)
    {
        var boxed = field.GetValue(owner);                 // 装箱的 ExposedReference<T>
        var nameField = field.FieldType.GetField("exposedName");
        return (PropertyName)nameField.GetValue(boxed);
    }

    /// <summary>空/未设置的 exposedName 其 id 为 0,等于 default。</summary>
    public static bool IsValidName(PropertyName name) => name != default(PropertyName);
}