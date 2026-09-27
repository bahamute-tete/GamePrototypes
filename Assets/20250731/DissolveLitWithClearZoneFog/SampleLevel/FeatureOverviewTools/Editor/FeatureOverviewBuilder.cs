using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace LiangZhu.Demo.Editor
{
    public static class FeatureOverviewBuilder
    {
        public const string Root = "Assets/20250731/DissolveLitWithClearZoneFog/SampleLevel";
        public const string AssetsRoot = Root + "/FeatureOverviewAssets";
        public const string ScenePath = Root + "/FeatureOverview.unity";
        public const string TimelinePath = Root + "/FeatureOverviewTimeline.playable";
        public const string MultiTimelinePath = Root + "/FeatureOverviewMultiTimeline.playable";
        static Material body, baseMat, accent;

        [MenuItem("Tools/LiangZhu/Refresh Feature Overview")]
        public static void RefreshInEditor()
        {
            var d = UnityEngine.Object.FindObjectOfType<FeatureOverviewData>();
            if (!d) throw new InvalidOperationException("请打开 FeatureOverview 场景。");
            FeatureOverviewRenderer.Refresh(d, -1);
            EditorSceneManager.SaveScene(d.gameObject.scene, ScenePath);
            FeatureOverviewRenderer.Validate(d);
            Debug.Log("A2C_OVERVIEW_REFRESH_PASSED");
        }

        [MenuItem("Tools/LiangZhu/Rebuild A2C Feature Overview")]
        public static void BuildInEditor()
        {
            // Preserve both on-disk assets and unsaved scene edits before replacing demo content.
            string backup = "Library/FeatureOverviewBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            foreach (string path in new[] { ScenePath, TimelinePath, MultiTimelinePath })
                if (File.Exists(path)) File.Copy(path, backup + "/" + Path.GetFileName(path));
            var current = SceneManager.GetActiveScene();
            if (current.isDirty && current.path != ScenePath)
                throw new InvalidOperationException("其他场景有未保存内容，请先保存。Demo 备份已完成。");
            if (current.path == ScenePath)
                EditorSceneManager.SaveScene(current, backup + "/FeatureOverview_Unsaved.unity", true);
            Build();
            EditorApplication.delayCall += RefreshInEditor;
        }

        static void Build()
        {
            Directory.CreateDirectory(AssetsRoot + "/Materials");
            Directory.CreateDirectory(AssetsRoot + "/Previews");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.42f, .48f, .55f);
            var d = new GameObject("Opaque A2C Dissolve — Overview").AddComponent<FeatureOverviewData>();
            d.sharedNoise = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsRoot + "/SharedNoise.png");
            if (!d.sharedNoise) throw new InvalidOperationException("Missing Demo SharedNoise.png");
            body = Material("Opaque Cyan", new Color(.08f, .58f, .68f), true);
            baseMat = Material("Graphite Plinth", new Color(.095f, .125f, .16f), false);
            accent = Material("Accent", new Color(.9f, .38f, .09f), false);
            var key = new GameObject("Studio Key").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.6f; key.color = new Color(1, .89f, .78f);
            key.transform.rotation = Quaternion.Euler(43, -32, 0); key.shadows = LightShadows.Soft;
            var fill = new GameObject("Studio Fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .65f; fill.color = new Color(.45f, .72f, 1);
            fill.transform.rotation = Quaternion.Euler(25, 135, 0);
            string[] names = { "Noise", "World Direction", "World Radial", "Local Space", "World Space", "Timeline Amount", "Timeline Parameters" };
            d.stages = new GameObject[7]; d.cameras = new Camera[7]; d.controllers = new DissolveController[7];
            for (int i = 0; i < 7; i++)
            {
                var stage = new GameObject(names[i] + " — Source");
                stage.transform.position = new Vector3(i * 30, 0, 0); d.stages[i] = stage;
                var camera = new GameObject(names[i] + " Camera").AddComponent<Camera>();
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .049f, .065f);
                camera.transform.position = stage.transform.position + new Vector3(3.5f, 3.1f, -7);
                camera.transform.LookAt(stage.transform.position + new Vector3(0, 1, 0));
                camera.orthographic = true; camera.orthographicSize = i == 0 ? 2.85f : 2.1f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 25; camera.allowMSAA = true;
                var extra = camera.GetUniversalAdditionalCameraData();
                extra.renderPostProcessing = false; extra.antialiasing = AntialiasingMode.None;
                extra.requiresColorOption = CameraOverrideOption.On;
                d.cameras[i] = camera;
                Primitive(stage, PrimitiveType.Cylinder, "Plinth", new Vector3(0, -.16f, 0), new Vector3(3.8f, .16f, 3.8f), baseMat);
                Primitive(stage, PrimitiveType.Cylinder, "Accent Ring", new Vector3(0, -.32f, 0), new Vector3(3.85f, .025f, 3.85f), accent);
                var c = stage.AddComponent<DissolveController>(); d.controllers[i] = c;
                c.mode = i == 0 ? DissolveController.DissolveMode.Noise : i == 2 ? DissolveController.DissolveMode.Radial : DissolveController.DissolveMode.Direction;
                c.space = i == 0 || i == 3 || i == 5 ? DissolveController.DissolveSpace.Local : DissolveController.DissolveSpace.World;
                c.noiseTexture = d.sharedNoise; c.noiseScale = .8f; c.axisDirection = Vector3.up;
                c.DissolveEdgeWidth = .065f; c.DissolveEdgeIntensity = 2;
                c.DissolveEdgeColor = new Color(1, .27f, .035f);
                var origin = new GameObject("Origin").transform; origin.SetParent(stage.transform, false);
                origin.localPosition = i == 2 ? new Vector3(0, 1, 0) : Vector3.zero;
                c.worldOrigin = origin; c.planeOffset = 1.1f; c.radius = 1.05f;
                if (i == 0 || i == 5)
                    c.controlledRenderers.Add(Primitive(stage, PrimitiveType.Sphere, "Hero", new Vector3(0, 1.1f, 0), Vector3.one * 2.1f, body));
                else if (i == 2)
                {
                    for (int n = 0; n < 8; n++)
                    {
                        float a = n * Mathf.PI / 4;
                        c.controlledRenderers.Add(Primitive(stage, PrimitiveType.Capsule, "Radial Pillar " + n, new Vector3(Mathf.Cos(a) * 1.05f, 1, Mathf.Sin(a) * 1.05f), new Vector3(.48f, .9f, .48f), body));
                    }
                    c.controlledRenderers.Add(Primitive(stage, PrimitiveType.Sphere, "Center", new Vector3(0, 1, 0), Vector3.one * 1.5f, body));
                }
                else
                {
                    for (int n = 0; n < 3; n++)
                    {
                        float height = 1.4f + n * .4f;
                        var r = Primitive(stage, PrimitiveType.Cube, "Column " + n, new Vector3((n - 1) * .95f, height / 2 + (n == 1 ? .22f : 0), 0), new Vector3(.55f, height, .75f), body);
                        if (i == 3 || i == 4) r.transform.localRotation = Quaternion.Euler(0, n * 15, (n - 1) * 22);
                        c.controlledRenderers.Add(r);
                    }
                }
                c.ForceRefresh(); stage.SetActive(false);
            }
            var holdCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.4f, 1), new Keyframe(.6f, 1), new Keyframe(1, 0));
            for (int i = 0; i < holdCurve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(holdCurve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(holdCurve, i, AnimationUtility.TangentMode.Linear);
            }
            d.director = Director("Timeline — Basic Workflow", TimelinePath);
            AddTrack(d.director, d.controllers[5], DissolveParameter.Amount, 0, 1, 6, holdCurve, "Dissolve — Hold — Restore");
            d.multiDirector = Director("Timeline — Independent Parameters", MultiTimelinePath);
            AddTrack(d.multiDirector, d.controllers[6], DissolveParameter.PlaneOffset, -.2f, 2.2f, 6, AnimationCurve.Linear(0, 0, 1, 1));
            AddTrack(d.multiDirector, d.controllers[6], DissolveParameter.EdgeWidth, .02f, .18f, 4, AnimationCurve.EaseInOut(0, 0, 1, 1));
            AddTrack(d.multiDirector, d.controllers[6], DissolveParameter.EdgeIntensity, .5f, 4, 6, new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 2)));
            BuildBoard(d);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        }

        static PlayableDirector Director(string name, string path)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (!timeline) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, path); }
            else foreach (var track in timeline.GetRootTracks().ToArray()) timeline.DeleteTrack(track);
            var director = new GameObject(name).AddComponent<PlayableDirector>();
            director.playOnAwake = false; director.extrapolationMode = DirectorWrapMode.Hold; director.playableAsset = timeline;
            EditorUtility.SetDirty(timeline); return director;
        }
        static void AddTrack(PlayableDirector director, DissolveController controller, DissolveParameter parameter, float start, float end, double duration, AnimationCurve curve, string name = null)
        {
            var timeline = (TimelineAsset)director.playableAsset;
            var track = timeline.CreateTrack<DissolveTrack>(null, parameter.ToString());
            track.parameter = parameter; track.parameterConfigured = true;
            var clip = track.CreateClip<DissolveClip>(); clip.start = 0; clip.duration = duration; clip.displayName = name ?? parameter.ToString();
            var b = ((DissolveClip)clip.asset).template;
            b.startValue = start; b.endValue = end; b.curve = curve; b.endBehaviour = DissolveEndBehaviour.HoldEnd;
            director.SetGenericBinding(track, controller);
            EditorUtility.SetDirty(clip.asset); EditorUtility.SetDirty(track); EditorUtility.SetDirty(timeline);
        }
        static void BuildBoard(FeatureOverviewData d)
        {
            d.boardCamera = new GameObject("Overview Camera").AddComponent<Camera>();
            d.boardCamera.transform.position = new Vector3(0, 0, -1000); d.boardCamera.cullingMask = 1 << 5;
            d.boardCamera.clearFlags = CameraClearFlags.SolidColor; d.boardCamera.backgroundColor = new Color(.025f, .035f, .05f); d.boardCamera.orthographic = true;
            d.boardCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvas = new GameObject("Six Panel Overview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); canvas.layer = 5;
            d.boardCanvas = canvas.GetComponent<Canvas>(); d.boardCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            d.boardCanvas.worldCamera = d.boardCamera; d.boardCanvas.planeDistance = 10;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840, 2160); scaler.matchWidthOrHeight = .5f;
            var image = new GameObject("Cached Overview — no Play required", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            image.layer = 5; image.transform.SetParent(canvas.transform, false); d.boardImage = image.GetComponent<RawImage>();
            var rect = image.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            var fit = image.GetComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 16f / 9;
        }
        static Material Material(string name, Color color, bool dissolve)
        {
            string path = AssetsRoot + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Custom/LiangZhu/Opaque_Dissolve_Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", 0); m.SetFloat("_Smoothness", .45f);
            m.SetFloat("_DissolveEnabled", dissolve ? 1 : 0); m.SetFloat("_DissolveCoverageWidth", .015f); m.SetFloat("_DissolveAAPixels", 1.3f);
            m.SetFloat("_FresnelIntensity", .12f); m.SetFloat("_FogEnable", 0); m.DisableKeyword("_FOG_AFFECTS");
            EditorUtility.SetDirty(m); return m;
        }
        static MeshRenderer Primitive(GameObject parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; return renderer;
        }
    }
}
