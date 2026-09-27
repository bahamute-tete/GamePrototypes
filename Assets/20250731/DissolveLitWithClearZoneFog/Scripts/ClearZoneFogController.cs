using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(1000)]
public class ClearZoneFogController : MonoBehaviour
{
    public const int MaxVolumes = 8;
    public static ClearZoneFogController Active { get; private set; }
    public static SphereFogVolume FirstVolume { get; private set; }
    public static int ValidVolumeCount { get; private set; }
    public static int PublishedVolumeCount { get; private set; }
    public static int ControllerCount => Controllers.Count;

    [Header("Clear Zone Fog")]
    [Tooltip("clearZoneFogWeight: 1 = Clear Zone Fog, 0 = Unity distance fog. Animate to 0 before disabling volumes for a smooth handover.")]
    [UnityEngine.Serialization.FormerlySerializedAs("shapeFogWeight")]
    [Range(0f, 1f)] public float clearZoneFogWeight = 1f;
    [Min(0.001f)] public float smoothness = 5f;
    [Tooltip("smoothUnionK: smooth union distance in world units. 0 = min. Higher values round connections and expand clear regions. Independent of smoothness.")]
    [Min(0f)] public float smoothUnionK = 1f;
    [Range(0, 1)] public float density = 1f;
    [ColorUsage(true, true)] public Color fogColor = new Color(0.6f, 0.65f, 0.7f, 1f);
    [Header("Skybox")]
    public bool affectSkybox = true;
    [Min(0.1f),HideInInspector] public float skyDistance = 50f;

    static readonly List<SphereFogVolume> Volumes = new List<SphereFogVolume>();
    static readonly List<ClearZoneFogController> Controllers = new List<ClearZoneFogController>();
    static readonly Vector4[] Centers = new Vector4[MaxVolumes];
    static readonly Vector4[] X = new Vector4[MaxVolumes], Y = new Vector4[MaxVolumes], Z = new Vector4[MaxVolumes];
    static readonly int CountID = Shader.PropertyToID("_SF_VolumeCount"), ActiveID = Shader.PropertyToID("_SF_Active");
    static readonly int CentersID = Shader.PropertyToID("_SF_CenterRadius"), XID = Shader.PropertyToID("_SF_AxisX"), YID = Shader.PropertyToID("_SF_AxisY"), ZID = Shader.PropertyToID("_SF_AxisZ");
    static readonly int SmoothID = Shader.PropertyToID("_SF_Smoothness"), DensityID = Shader.PropertyToID("_SF_Density"), ColorID = Shader.PropertyToID("_SF_FogColor");
    static readonly int SkyID = Shader.PropertyToID("_SF_AffectSky"), SkyDistanceID = Shader.PropertyToID("_SF_SkyDistance");
    static readonly int UnionKID = Shader.PropertyToID("_SF_SmoothUnionK");
    static readonly int WeightID = Shader.PropertyToID("_SF_Weight");
    static float lastWeight;
    static float lastUnionK;
    static Vector4 lastSettings;
    static Color lastColor;
    static bool dirty = true, overCapacity;

    internal static void Register(SphereFogVolume v) { if (!Volumes.Contains(v)) Volumes.Add(v); Refresh(true); }
    internal static void Unregister(SphereFogVolume v) { Volumes.Remove(v); Refresh(true); }
    internal static void Invalidate() { dirty = true; }
    internal static void TickFromVolume(SphereFogVolume v)
    {
        // Compatibility path for existing scenes before a controller is added.
        if (Active == null && Volumes.Count > 0 && Volumes[0] == v) Refresh(false);
    }
    void OnEnable() { if (!Controllers.Contains(this)) Controllers.Add(this); Refresh(true); }
    void OnDisable() { Controllers.Remove(this); Refresh(true); }
    void OnValidate() { dirty = true; }
    void LateUpdate() { if (Active == this) Refresh(false); }
    public void ForceRefresh() { Refresh(true); }

    internal static void Refresh(bool force)
    {
        for (int i = Controllers.Count - 1; i >= 0; i--)
            if (Controllers[i] == null || !Controllers[i].isActiveAndEnabled) Controllers.RemoveAt(i);
        Active = Controllers.Count > 0 ? Controllers[0] : null;
        bool changed = force || dirty;
        int count = 0, valid = 0;
        FirstVolume = null;
        for (int i = 0; i < Volumes.Count; i++)
        {
            var v = Volumes[i];
            if (v == null) { Volumes.RemoveAt(i--); continue; }
            if (!v.TryGetShape(out var center, out var x, out var y, out var z)) continue;
            valid++;
            if (FirstVolume == null) FirstVolume = v;
            if (count == MaxVolumes) continue;
            if (!Centers[count].Equals(center) || !X[count].Equals(x) || !Y[count].Equals(y) || !Z[count].Equals(z)) changed = true;
            Centers[count] = center; X[count] = x; Y[count] = y; Z[count] = z;
            count++;
        }
        ValidVolumeCount = valid;
        if (valid > MaxVolumes && !overCapacity)
            Debug.LogWarning("Clear Zone Fog supports 8 valid volumes. The first 8 enabled volumes participate; additional volumes are on standby.", Active != null ? (Object)Active : FirstVolume);
        overCapacity = valid > MaxVolumes;
        Vector4 settings = Vector4.zero;
        Color color = Color.clear;
        if (count > 0)
        {
            settings = Active != null
                ? new Vector4(Active.smoothness, Active.density, Active.affectSkybox ? 1 : 0, Active.skyDistance)
                : new Vector4(FirstVolume.smoothness, FirstVolume.density, FirstVolume.affectSkybox ? 1 : 0, FirstVolume.skyDistance);
            settings.x = Mathf.Max(0.001f, settings.x); settings.y = Mathf.Clamp01(settings.y); settings.w = Mathf.Max(0.1f, settings.w);
            color = Active != null ? Active.fogColor : FirstVolume.fogColor;
        }
        float unionK = count > 0 && Active != null ? Mathf.Max(0f, Active.smoothUnionK) : 0f;
        float weight = count > 0 ? (Active != null ? Mathf.Clamp01(Active.clearZoneFogWeight) : 1f) : 0f;
        if (lastWeight != weight) changed = true;
        if (lastUnionK != unionK) changed = true;
        if (PublishedVolumeCount != count || !lastSettings.Equals(settings) || !lastColor.Equals(color)) changed = true;
        if (!changed) return;
        // Always upload fixed-sized arrays: Unity retains the first uploaded array length.
        if (count > 0)
        {
            Shader.SetGlobalVectorArray(CentersID, Centers);
            Shader.SetGlobalVectorArray(XID, X); Shader.SetGlobalVectorArray(YID, Y); Shader.SetGlobalVectorArray(ZID, Z);
        }
        Shader.SetGlobalFloat(WeightID, weight);
        Shader.SetGlobalFloat(UnionKID, unionK);
        Shader.SetGlobalInt(CountID, count);
        Shader.SetGlobalFloat(ActiveID, count > 0 ? 1 : 0);
        Shader.SetGlobalFloat(SmoothID, settings.x); Shader.SetGlobalFloat(DensityID, settings.y);
        Shader.SetGlobalColor(ColorID, color);
        Shader.SetGlobalFloat(SkyID, settings.z); Shader.SetGlobalFloat(SkyDistanceID, settings.w);
        lastWeight = weight; lastUnionK = unionK; PublishedVolumeCount = count; lastSettings = settings; lastColor = color; dirty = false;
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(ClearZoneFogController))]
public class ClearZoneFogControllerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DrawCapacity();
        if (ClearZoneFogController.ControllerCount > 1)
            UnityEditor.EditorGUILayout.HelpBox("Multiple ClearZoneFogControllers are enabled. Only the first enabled controller supplies the shared settings.", UnityEditor.MessageType.Warning);
        if (GUILayout.Button("Sync Fog Color to Unity Fog")) SyncColor(((ClearZoneFogController)target).fogColor);
    }
    public static void DrawCapacity()
    {
        UnityEditor.EditorGUILayout.LabelField("Valid Clear Volumes", ClearZoneFogController.ValidVolumeCount + " / " + ClearZoneFogController.MaxVolumes);
        if (ClearZoneFogController.ValidVolumeCount > ClearZoneFogController.MaxVolumes)
            UnityEditor.EditorGUILayout.HelpBox("Capacity exceeded: the first 8 enabled volumes participate. Standby volumes replace disabled participants.", UnityEditor.MessageType.Warning);
    }
    public static void SyncColor(Color color)
    {
        UnityEditor.Undo.RecordObjects(Resources.FindObjectsOfTypeAll<RenderSettings>(), "Sync Unity Fog Color");
        RenderSettings.fogColor = color;
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
#endif
