using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[DisallowMultipleComponent]
public class PlanarReflectionRenderer : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("水面材质 —— 启用时会自动打 _PLANAR_REFLECTION_ON 关键字")]
    public Material waterMaterial;

    [Header("Reflection Content")]
    public LayerMask reflectionLayers = ~0;
    public bool reflectSkybox = true;
    public Color clearColor = Color.black;
    public bool renderShadows = false;

    [Header("Resolution / Quality")]
    [Range(1, 8)] public int resolutionDivider = 2;   // RT 尺寸 = 主相机 / divider
    public bool useHDR = false;
    public float nearClip = 0.1f;
    public float farClip = 500f;
    [Tooltip("镜像平面向上微偏移，避免水面自身被斜投影裁切")]
    public float clipPlaneOffset = 0.01f;

    Camera _reflectionCam;
    //RenderTexture _reflectionRT;
    //int _rtW, _rtH;

    readonly Dictionary<Camera, RenderTexture> _rtCache = new Dictionary<Camera, RenderTexture>();



    const string KW = "_PLANAR_REFLECTION_ON";
    static readonly int ID_Tex = Shader.PropertyToID("_PlanarReflectionTex");
    static readonly int ID_VP = Shader.PropertyToID("_PlanarReflectionVP");

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        if (waterMaterial != null) waterMaterial.EnableKeyword(KW);
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        if (waterMaterial != null) waterMaterial.DisableKeyword(KW);
        Cleanup();
    }

    void Cleanup()
    {
        //if (_reflectionRT != null)
        //{
        //    if (Application.isPlaying) Destroy(_reflectionRT); else DestroyImmediate(_reflectionRT);
        //    _reflectionRT = null;
        //}
        //if (_reflectionCam != null)
        //{
        //    var go = _reflectionCam.gameObject;
        //    if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        //    _reflectionCam = null;
        //}

        foreach (var kvp in _rtCache)
        {
            if (kvp.Value != null)
            {
                if (Application.isPlaying) Destroy(kvp.Value); else DestroyImmediate(kvp.Value);
            }
        }
        _rtCache.Clear();

        if (_reflectionCam != null)
        {
            var go = _reflectionCam.gameObject;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            _reflectionCam = null;
        }
    }

    void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        // 排除自己 / 反射类相机 / preview，避免递归
        if (cam == _reflectionCam) return;
        if (cam.cameraType == CameraType.Reflection || cam.cameraType == CameraType.Preview) return;
        // 只在 Game 相机上工作 —— 排除 SceneView / Preview / Reflection probe
        if (cam.cameraType != CameraType.Game) return;

        //// 编辑器非运行态不渲染反射，避免 [ExecuteAlways] 在编辑器里造成不稳定
        //if (!Application.isPlaying) return;

        bool isGame = cam.cameraType == CameraType.Game;
        bool isScene = cam.cameraType == CameraType.SceneView;
        if (!isGame && !isScene) return;

        EnsureCamera(cam);

        var rt = GetOrCreateRT(cam);   // ★ 取当前相机对应的 RT
        _reflectionCam.targetTexture = rt;
        //EnsureRT(cam);

        // ===== 平面参数 =====
        Vector3 n = transform.up;
        Vector3 p = transform.position + n * clipPlaneOffset;
        float d = -Vector3.Dot(n, p);
        Vector4 plane = new Vector4(n.x, n.y, n.z, d);

        // ===== 镜像 view 矩阵 =====
        // SPI 下 cam.worldToCameraMatrix 是「中央眼」矩阵 —— mono 反射用它合适
        Matrix4x4 refl = Matrix4x4.identity;
        CalculateReflectionMatrix(ref refl, plane);
        
        //_reflectionCam.transform.position = refl.MultiplyPoint(cam.transform.position);
        //_reflectionCam.worldToCameraMatrix = cam.worldToCameraMatrix * refl;

        //_reflectionCam.CopyFrom(cam);
        //_reflectionCam.enabled = false;
        //_reflectionCam.cameraType = CameraType.Reflection;
        //_reflectionCam.stereoTargetEye = StereoTargetEyeMask.None;
        //_reflectionCam.targetTexture = rt;

        //reflectionLayers = reflectionLayers & ~(1 << LayerMask.NameToLayer("Water"));
        _reflectionCam.cullingMask = reflectionLayers;

        _reflectionCam.clearFlags = reflectSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        _reflectionCam.backgroundColor = clearColor;
        _reflectionCam.nearClipPlane = nearClip;
        _reflectionCam.farClipPlane = farClip;
        _reflectionCam.aspect = (float)rt.width / rt.height;
        _reflectionCam.fieldOfView = cam.fieldOfView;

        _reflectionCam.transform.position = refl.MultiplyPoint(cam.transform.position);
        _reflectionCam.transform.rotation = Quaternion.LookRotation(
            refl.MultiplyVector(cam.transform.forward),
            refl.MultiplyVector(cam.transform.up)
        );

        _reflectionCam.worldToCameraMatrix = cam.worldToCameraMatrix * refl;


        // ===== 斜投影 —— 把水面以下全部裁掉 =====
        Vector4 clipPlane = CameraSpacePlane(_reflectionCam, p, n, 1.0f);
        Matrix4x4 obliqueProjection = _reflectionCam.CalculateObliqueMatrix(clipPlane);


        //   renderIntoTexture: true 告诉 Unity 这个矩阵是用来渲染到 RT 的
        //_reflectionCam.projectionMatrix = GL.GetGPUProjectionMatrix(obliqueProjection, renderIntoTexture: true);
        _reflectionCam.projectionMatrix = obliqueProjection;
        Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(obliqueProjection, true);
        Shader.SetGlobalMatrix(ID_VP, gpuProjection * _reflectionCam.worldToCameraMatrix);

       

        // 镜像后三角形绕序翻转，必须反转面剔除
        bool oldInvertCulling = GL.invertCulling;
        GL.invertCulling = !oldInvertCulling;
        try
        {
#pragma warning disable 0618
            UniversalRenderPipeline.RenderSingleCamera(ctx, _reflectionCam);
#pragma warning restore 0618
        }
        finally
        {
            GL.invertCulling = oldInvertCulling;
        }

        Shader.SetGlobalTexture(ID_Tex, rt);
    }

    void EnsureCamera(Camera mainCam)
    {
        if (_reflectionCam != null) return;

        var go = new GameObject("[PlanarReflectionCam]");
        go.hideFlags = HideFlags.HideAndDontSave;

        _reflectionCam = go.AddComponent<Camera>();
        _reflectionCam.enabled = false;
        _reflectionCam.stereoTargetEye = StereoTargetEyeMask.None;  // ★ 关键：脱离 XR 渲染流程

        var ucd = go.AddComponent<UniversalAdditionalCameraData>();
        ucd.renderPostProcessing = false;
        ucd.renderShadows = renderShadows;
        ucd.requiresColorOption = CameraOverrideOption.Off;
        ucd.requiresDepthOption = CameraOverrideOption.Off;
        ucd.allowXRRendering = false;  // ★ 关键：URP 层级也禁用 XR 路径

        // 从主相机拷贝 FOV / 近远裁 / aspect，避免反射用默认 60° FOV
        _reflectionCam.fieldOfView = mainCam.fieldOfView;
        _reflectionCam.nearClipPlane = nearClip;
        _reflectionCam.farClipPlane = farClip;
    }

    RenderTexture GetOrCreateRT(Camera cam)
    {
        int w = Mathf.Max(8, cam.pixelWidth / resolutionDivider);
        int h = Mathf.Max(8, cam.pixelHeight / resolutionDivider);

        if (_rtCache.TryGetValue(cam, out var rt))
        {
            if (rt != null && rt.width == w && rt.height == h)
                return rt;

            // 尺寸变了（窗口被拖动 / 视图被 resize）→ 销毁重建
            if (Application.isPlaying) Destroy(rt); else DestroyImmediate(rt);
            _rtCache.Remove(cam);
        }

        var fmt = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        rt = new RenderTexture(w, h, 16, fmt)
        {
            name = $"PlanarReflectionRT_{cam.cameraType}",
            useMipMap = true,
            autoGenerateMips = true,
            antiAliasing = 1,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear
        };
        rt.Create();

        _rtCache[cam] = rt;
        return rt;
    }

    //void EnsureRT(Camera mainCam)
    //{
    //    int w = Mathf.Max(8, mainCam.pixelWidth / resolutionDivider);
    //    int h = Mathf.Max(8, mainCam.pixelHeight / resolutionDivider);

    //    if (_reflectionRT != null && _rtW == w && _rtH == h) return;

    //    if (_reflectionRT != null)
    //    {
    //        if (Application.isPlaying) Destroy(_reflectionRT); else DestroyImmediate(_reflectionRT);
    //    }

    //    var fmt = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
    //    _reflectionRT = new RenderTexture(w, h, 16, fmt)
    //    {
    //        name = "PlanarReflectionRT",
    //        useMipMap = true,          // ← 改：开启 mipmap
    //        autoGenerateMips = true,   // ← 改：相机渲染完后自动生成 mip
    //        antiAliasing = 1,
    //        wrapMode = TextureWrapMode.Clamp,  // ← 加：避免边缘 wrap 干扰高 mip 采样
    //        filterMode = FilterMode.Trilinear  // ← 加：保证 mip 之间平滑过渡
    //    };
    //    _reflectionRT.Create();
    //    _rtW = w; _rtH = h;

    //    _reflectionCam.targetTexture = _reflectionRT;
    //    _reflectionCam.aspect = (float)w / h;
    //    _reflectionCam.nearClipPlane = nearClip;
    //    _reflectionCam.farClipPlane = farClip;

    //    if (reflectSkybox)
    //    {
    //        _reflectionCam.clearFlags = CameraClearFlags.Skybox;
    //    }
    //    else
    //    {
    //        _reflectionCam.clearFlags = CameraClearFlags.SolidColor;
    //        _reflectionCam.backgroundColor = clearColor;
    //    }
    //}

    // ---- 反射矩阵：经典公式 ----
    static void CalculateReflectionMatrix(ref Matrix4x4 m, Vector4 plane)
    {
        m.m00 = 1F - 2F * plane[0] * plane[0]; m.m01 = -2F * plane[0] * plane[1]; m.m02 = -2F * plane[0] * plane[2]; m.m03 = -2F * plane[3] * plane[0];
        m.m10 = -2F * plane[1] * plane[0]; m.m11 = 1F - 2F * plane[1] * plane[1]; m.m12 = -2F * plane[1] * plane[2]; m.m13 = -2F * plane[3] * plane[1];
        m.m20 = -2F * plane[2] * plane[0]; m.m21 = -2F * plane[2] * plane[1]; m.m22 = 1F - 2F * plane[2] * plane[2]; m.m23 = -2F * plane[3] * plane[2];
        m.m30 = 0F; m.m31 = 0F; m.m32 = 0F; m.m33 = 1F;
    }

    // 把世界空间裁切平面转到反射相机的相机空间
    static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
    {
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(pos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }
}
