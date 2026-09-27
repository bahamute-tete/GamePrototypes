using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Collider))]
public class SphereFogVolume : MonoBehaviour
{
    // Preserve serialized settings and Timeline bindings from the single-volume version.
    [SerializeField, HideInInspector, FormerlySerializedAs("smoothness")] float legacySmoothness = 5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("density")] float legacyDensity = 1f;
    [SerializeField, HideInInspector, FormerlySerializedAs("fogColor")] Color legacyColor = new Color(0.6f, 0.65f, 0.7f, 1f);
    [SerializeField, HideInInspector, FormerlySerializedAs("affectSkybox")] bool legacySky = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("skyDistance")] float legacySkyDistance = 50f;
    [System.NonSerialized] public Collider fogCollider;
    public static SphereFogVolume ActiveVolume => ClearZoneFogController.FirstVolume;

    // Old scripts and Timeline continue to address the shared style through their bound volume.
    public float smoothness { get => ClearZoneFogController.Active != null ? ClearZoneFogController.Active.smoothness : legacySmoothness; set { if (ClearZoneFogController.Active != null) ClearZoneFogController.Active.smoothness = value; else legacySmoothness = value; } }
    public float density { get => ClearZoneFogController.Active != null ? ClearZoneFogController.Active.density : legacyDensity; set { if (ClearZoneFogController.Active != null) ClearZoneFogController.Active.density = value; else legacyDensity = value; } }
    public Color fogColor { get => ClearZoneFogController.Active != null ? ClearZoneFogController.Active.fogColor : legacyColor; set { if (ClearZoneFogController.Active != null) ClearZoneFogController.Active.fogColor = value; else legacyColor = value; } }
    public bool affectSkybox { get => ClearZoneFogController.Active != null ? ClearZoneFogController.Active.affectSkybox : legacySky; set { if (ClearZoneFogController.Active != null) ClearZoneFogController.Active.affectSkybox = value; else legacySky = value; } }
    public float skyDistance { get => ClearZoneFogController.Active != null ? ClearZoneFogController.Active.skyDistance : legacySkyDistance; set { if (ClearZoneFogController.Active != null) ClearZoneFogController.Active.skyDistance = value; else legacySkyDistance = value; } }

    internal bool TryGetShape(out Vector4 center, out Vector4 x, out Vector4 y, out Vector4 z)
    {
        center = x = y = z = Vector4.zero;
        if (!isActiveAndEnabled) return false;
        if (fogCollider == null) fogCollider = GetComponent<Collider>();
        var t = transform;
        var s = t.lossyScale;
        s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        if (fogCollider is SphereCollider sphere)
        {
            var p = t.TransformPoint(sphere.center);
            center = new Vector4(p.x, p.y, p.z, Mathf.Max(0, sphere.radius) * Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
            return true;
        }
        if (fogCollider is BoxCollider box)
        {
            var p = t.TransformPoint(box.center);
            center = new Vector4(p.x, p.y, p.z, -1); // Negative radius marks an oriented box.
            var half = Vector3.Scale(box.size * 0.5f, s);
            var a = t.right; x = new Vector4(a.x, a.y, a.z, Mathf.Abs(half.x));
            a = t.up; y = new Vector4(a.x, a.y, a.z, Mathf.Abs(half.y));
            a = t.forward; z = new Vector4(a.x, a.y, a.z, Mathf.Abs(half.z));
            return true;
        }
        return false;
    }

    void OnEnable() { fogCollider = GetComponent<Collider>(); ClearZoneFogController.Register(this); }
    void OnDisable() { ClearZoneFogController.Unregister(this); }
    void OnValidate() { ClearZoneFogController.Invalidate(); }
    void LateUpdate() { ClearZoneFogController.TickFromVolume(this); }
    public void ForceRefresh() { ClearZoneFogController.Refresh(true); }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(SphereFogVolume))]
public class SphereFogVolumeEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        var volume = (SphereFogVolume)target;
        UnityEditor.EditorGUILayout.HelpBox("Clear Zone Fog volumes use SphereCollider or BoxCollider. Smooth Union K on ClearZoneFogController controls smin blending. The Collider physics toggle does not affect fog.", UnityEditor.MessageType.Info);
        if (!(volume.GetComponent<Collider>() is SphereCollider) && !(volume.GetComponent<Collider>() is BoxCollider))
            UnityEditor.EditorGUILayout.HelpBox("A SphereCollider or BoxCollider is required.", UnityEditor.MessageType.Error);
        if (ClearZoneFogController.Active == null)
        {
            UnityEditor.EditorGUILayout.HelpBox("No ClearZoneFogController is active. Using the legacy settings of the first valid volume.", UnityEditor.MessageType.Warning);
            if (GUILayout.Button("Create Controller & Migrate Fog Settings")) Migrate(volume);
        }
        else if (GUILayout.Button("Select ClearZoneFogController")) UnityEditor.Selection.activeObject = ClearZoneFogController.Active;
        ClearZoneFogControllerEditor.DrawCapacity();
        if (GUILayout.Button("Sync Shared Fog Color to Unity Fog")) SyncFogColor(volume);
    }

    public static ClearZoneFogController Migrate(SphereFogVolume volume)
    {
        if (ClearZoneFogController.Active != null) return ClearZoneFogController.Active;
        // Read before OnEnable establishes the controller as the shared source.
        var smooth = volume.smoothness; var density = volume.density; var color = volume.fogColor;
        var sky = volume.affectSkybox; var distance = volume.skyDistance;
        var go = new GameObject("Clear Zone Fog Controller");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, volume.gameObject.scene);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Clear Zone Fog Controller");
        var controller = UnityEditor.Undo.AddComponent<ClearZoneFogController>(go);
        controller.smoothness = smooth; controller.density = density; controller.fogColor = color;
        controller.affectSkybox = sky; controller.skyDistance = distance;
        UnityEditor.EditorUtility.SetDirty(controller);
        controller.ForceRefresh();
        UnityEditor.Selection.activeObject = controller;
        return controller;
    }

    public static void SyncFogColor(SphereFogVolume volume)
    {
        var source = ClearZoneFogController.FirstVolume;
        ClearZoneFogControllerEditor.SyncColor(ClearZoneFogController.Active != null ? ClearZoneFogController.Active.fogColor : source != null ? source.fogColor : volume.fogColor);
    }
}
#endif
