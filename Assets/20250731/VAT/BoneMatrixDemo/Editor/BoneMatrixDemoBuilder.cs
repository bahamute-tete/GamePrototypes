using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VATLearning.Editor
{
    public static class BoneMatrixDemoBuilder
    {
        private const string Root = "Assets/20250731/VAT/BoneMatrixDemo";
        private const int FrameCount = 120;
        private const int Segments = 20;
        private static readonly Vector3 Joint = new Vector3(0, 1, 0);

        [MenuItem("Tools/VAT Learning/1 - Create Two Bone Demo")]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating the demo.");
            Shader shader = Shader.Find("VATLearning/BoneMatrixUnlit");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("BoneMatrixUnlit shader is missing or has compilation errors. Check Console.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 每次生成独立目录，保留之前的实验结果。
            string output = AssetDatabase.GenerateUniqueAssetPath(Root + "/Generated");
            AssetDatabase.CreateFolder(Root, output.Substring(Root.Length + 1));
            Mesh mesh = CreateRibbon();
            Texture2D texture = BakeBoneTexture();
            ValidateData(mesh, texture);
            AssetDatabase.CreateAsset(mesh, output + "/Ribbon.asset");
            AssetDatabase.CreateAsset(texture, output + "/BoneMatrices.asset");

            Material animated = new Material(shader) { name = "Animated" };
            animated.SetTexture("_BoneTexture", texture);
            animated.SetFloat("_FrameCount", FrameCount);
            Material rest = new Material(animated) { name = "BindPose" };
            rest.SetFloat("_ApplySkinning", 0);
            AssetDatabase.CreateAsset(animated, output + "/Animated.mat");
            AssetDatabase.CreateAsset(rest, output + "/BindPose.mat");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateRibbonObject("LEFT - Original mesh", -1.05f, mesh, rest);
            MeshRenderer right = CreateRibbonObject("RIGHT - Texture driven mesh", 1.05f, mesh, animated);
            var player = new GameObject("Playback - select to inspect").AddComponent<BoneMatrixDemoPlayer>();
            player.animatedRenderer = right;
            player.frameCount = FrameCount;
            player.framesPerSecond = 60;

            Camera camera = new GameObject("Demo Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 1.2f, -10);
            camera.orthographic = true;
            camera.orthographicSize = 2.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30;

            EditorSceneManager.SaveScene(scene, output + "/TwoBoneDemo.unity");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1, 0), Quaternion.identity, 3.5f, true);
            Debug.Log("Bone Matrix Demo created: " + output + "/TwoBoneDemo.unity\nPress Play and open Game view. Read BoneMatrixDemo/README.md for the lesson.");
        }

        private static MeshRenderer CreateRibbonObject(string name, float x, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = new Vector3(x, 0, 0);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        // 第一步：一条 2 米高、0.4 米宽的带子，共 42 个顶点。
        private static Mesh CreateRibbon()
        {
            var positions = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<Vector2>();
            var weights = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row <= Segments; row++)
            {
                float y = 2f * row / Segments;
                float w1 = Mathf.InverseLerp(0.7f, 1.3f, y);
                for (int side = 0; side < 2; side++)
                {
                    positions.Add(new Vector3(side == 0 ? -0.2f : 0.2f, y, 0));
                    uv.Add(new Vector2(side, (float)row / Segments));
                    indices.Add(new Vector2(0, 1));
                    weights.Add(new Vector2(1 - w1, w1));
                }
                if (row == Segments) continue;
                int a = row * 2;
                triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            Mesh mesh = new Mesh { name = "Ribbon - 42 vertices, 2 influences" };
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, indices);
            mesh.SetUVs(2, weights);
            mesh.SetTriangles(triangles, 0);
            // Shader 变形不会自动改变 CPU bounds；覆盖整个摆动范围。
            mesh.bounds = new Bounds(new Vector3(0, 1, 0), new Vector3(3, 3, 0.2f));
            return mesh;
        }

        // 第二步：在编辑器中烘焙。这里用数学定义骨骼运动，避免依赖 FBX/Animator。
        // Bone 0 位于原点且保持不动；Bone 1 是其子骨骼，关节位于 (0,1,0)。
        private static Matrix4x4 BoneMatrix(int bone, int frame)
        {
            if (bone == 0) return Matrix4x4.identity;
            float angle = 60f * Mathf.Sin(2f * Mathf.PI * frame / FrameCount);
            Matrix4x4 currentBone = Matrix4x4.TRS(Joint, Quaternion.Euler(0, 0, angle), Vector3.one);
            Matrix4x4 bindpose = Matrix4x4.Translate(-Joint);
            // 本例 Renderer 为单位矩阵：inverse(Renderer) * Bone * Bindpose。
            return currentBone * bindpose;
        }

        private static Texture2D BakeBoneTexture()
        {
            // 宽 6 = 2 根骨骼 * 3 行；高 120 = 120 帧。
            // 使用 Float 便于核对原始数值；无 sRGB、无压缩、无 mipmap。
            var texture = new Texture2D(6, FrameCount, TextureFormat.RGBAFloat, false, true)
            {
                name = "Bone matrices - x bone rows, y animation frames",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[6 * FrameCount];
            for (int frame = 0; frame < FrameCount; frame++)
                for (int bone = 0; bone < 2; bone++)
                {
                    Matrix4x4 matrix = BoneMatrix(bone, frame);
                    for (int row = 0; row < 3; row++)
                    {
                        Vector4 r = matrix.GetRow(row);
                        pixels[frame * 6 + bone * 3 + row] = new Color(r.x, r.y, r.z, r.w);
                    }
                }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        [MenuItem("Tools/VAT Learning/2 - Validate Bake Math")]
        public static void ValidateBakeMath()
        {
            Mesh mesh = CreateRibbon();
            Texture2D texture = BakeBoneTexture();
            try { ValidateData(mesh, texture); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(texture); }
        }

        // 独立用关节绕点旋转公式对照纹理解码结果，捕捉行列、寻址、Bindpose 错误。
        private static void ValidateData(Mesh mesh, Texture2D texture)
        {
            Vector3[] vertices = mesh.vertices;
            var weights = new List<Vector2>();
            mesh.GetUVs(2, weights);
            float maxError = 0;
            for (int frame = 0; frame < FrameCount; frame++)
            {
                float radians = 60f * Mathf.Deg2Rad * Mathf.Sin(2f * Mathf.PI * frame / FrameCount);
                for (int v = 0; v < vertices.Length; v++)
                {
                    Vector3 p = vertices[v];
                    Vector3 d = p - Joint;
                    Vector3 rotated = Joint + new Vector3(
                        Mathf.Cos(radians) * d.x - Mathf.Sin(radians) * d.y,
                        Mathf.Sin(radians) * d.x + Mathf.Cos(radians) * d.y, d.z);
                    Vector3 expected = p * weights[v].x + rotated * weights[v].y;
                    Vector3 decoded = Decode(texture, 0, frame, p) * weights[v].x
                                      + Decode(texture, 1, frame, p) * weights[v].y;
                    maxError = Mathf.Max(maxError, Vector3.Distance(expected, decoded));
                    if (!mesh.bounds.Contains(decoded))
                        throw new InvalidOperationException("Animated vertex escaped the mesh bounds.");
                }
                if (Vector3.Distance(Decode(texture, 1, frame, Joint), Joint) > 0.00001f)
                    throw new InvalidOperationException("Joint should remain fixed. Check Bindpose.");
            }
            if (maxError > 0.00001f)
                throw new InvalidOperationException("Texture skinning differs from analytic rotation: " + maxError);
            Debug.Log($"Bone Matrix math PASS: {FrameCount * vertices.Length} vertex samples; joint and bounds checked; max error {maxError:G4}.");
        }

        private static Vector3 Decode(Texture2D texture, int bone, int frame, Vector3 point)
        {
            Vector4 p = new Vector4(point.x, point.y, point.z, 1);
            Vector4 r0 = texture.GetPixel(bone * 3, frame);
            Vector4 r1 = texture.GetPixel(bone * 3 + 1, frame);
            Vector4 r2 = texture.GetPixel(bone * 3 + 2, frame);
            return new Vector3(Vector4.Dot(r0, p), Vector4.Dot(r1, p), Vector4.Dot(r2, p));
        }
    }
}
