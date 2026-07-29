using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PLYImporter : MonoBehaviour
{
    [Header("PLY File Path")]
    public string plyFilePath = "Assets/ExportedMesh.ply"; // Houdini exported PLY path


    // PLY vertex data structure (matches Houdini export properties)
    public struct PLYVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Color color;
        public float pscale;
        public Vector3 uv;
    }

    // 仅在编辑器下显示的按钮功能
#if UNITY_EDITOR
    [ContextMenu("Bake PLY to VFX Textures")]
    void BakeToVFXTextures()
    {
        if (!File.Exists(plyFilePath))
        {
            Debug.LogError("PLY file not found: " + plyFilePath);
            return;
        }

        Debug.Log("Start Parsing PLY...");
        List<PLYVertex> vertices = ParsePLY(plyFilePath);
        
        if (vertices == null || vertices.Count == 0)
        {
            Debug.LogError("No vertices parsed.");
            return;
        }

        Debug.Log($"Parsed {vertices.Count} vertices. Baking to Textures...");
        BakeTextures(vertices);
    }
#endif

    void Start()
    {

    }

    /// <summary>
    /// 解析 PLY 并返回数据列表
    /// </summary>
    List<PLYVertex> ParsePLY(string path)
    {
        string[] lines = File.ReadAllLines(path);
        List<PLYVertex> vertices = new List<PLYVertex>();

        int vertexCount = 0;
        int vertexLineStart = 0;
        List<string> vertexProperties = new List<string>();
        bool isVertexElement = false;
        bool isHeaderEnd = false;

        // 1. Parse Header
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("element vertex"))
            {
                vertexCount = int.Parse(line.Split(' ')[2]);
                isVertexElement = true;
            }
            else if (line.StartsWith("element ") && !line.StartsWith("element vertex"))
            {
                isVertexElement = false;
            }
            else if (line.StartsWith("property") && isVertexElement)
            {
                string[] parts = line.Split(' ');
                vertexProperties.Add(parts[parts.Length - 1]);
            }

            else if (line == "end_header")
            {
                isHeaderEnd = true;
                vertexLineStart = i + 1;
                break;
            }
        }

        if (!isHeaderEnd) return null;

        // 2. Parse Body
        for (int i = 0; i < vertexCount; i++)
        {
            if (vertexLineStart + i >= lines.Length) break;

            string vertexLine = lines[vertexLineStart + i].Trim();
            // 优化：对于百万级数据，Split 可能会慢，但为了兼容性暂时保留
            string[] vertexData = vertexLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            PLYVertex vertex = new PLYVertex();
            vertex.color = Color.white;

            int idx = 0;
            foreach (string prop in vertexProperties)
            {
                if (idx >= vertexData.Length) break;
                float val = float.Parse(vertexData[idx]);

                switch (prop)
                {
                    case "x": vertex.position.x = val; break;
                    case "y": vertex.position.y = val; break;
                    case "z": vertex.position.z = val; break;
                    case "uv1": vertex.uv.x = val; break;
                    case "uv2": vertex.uv.y = val; break;
                    case "uv3": vertex.uv.z = val; break;
                    case "nx": vertex.normal.x = val; break;
                    case "ny": vertex.normal.y = val; break;
                    case "nz": vertex.normal.z = val; break;
                    // 注意：如果 PLY 里的颜色是 0-255 的 uchar，除以 255 是对的。如果是 0-1 float，则不需要除。
                    // 这里假设是 uchar (Houdini 默认)
                    case "red": vertex.color.r = val / 255f; break;
                    case "green": vertex.color.g = val / 255f; break;
                    case "blue": vertex.color.b = val / 255f; break;
                    case "alpha": vertex.color.a = val / 255f; break;
                    case "pscale":vertex.pscale = val;break;
                }
                idx++;
            }
            vertices.Add(vertex);
        }

        return vertices;
    }

#if UNITY_EDITOR
    void BakeTextures(List<PLYVertex> vertices)
    {
        int count = vertices.Count;
        // 计算纹理尺寸，使其尽量为正方形 (例如 100万点 -> 1000x1000)
        int width = Mathf.CeilToInt(Mathf.Sqrt(count));
        int height = Mathf.CeilToInt((float)count / width);

        // 1. 创建位置纹理 (使用 RGBAFloat 保证精度)
        Texture2D posTex = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
        posTex.filterMode = FilterMode.Point; // 必须是 Point，避免插值
        posTex.wrapMode = TextureWrapMode.Clamp;

        // 2. 创建颜色纹理 (RGBA32 足够)
        Texture2D colTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        colTex.filterMode = FilterMode.Point;
        colTex.wrapMode = TextureWrapMode.Clamp;

        Texture2D uvTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        uvTex.filterMode = FilterMode.Point;
        uvTex.wrapMode = TextureWrapMode.Clamp;

        Color[] posColors = new Color[width * height];
        Color[] colColors = new Color[width * height];
        Color[] uvColors = new Color[width * height];

        for (int i = 0; i < count; i++)
        {
            PLYVertex v = vertices[i];
            // 位置存入 RGB，A 通道可以存其他数据(如大小)
            posColors[i] = new Color(v.position.x, v.position.y, v.position.z, v.pscale);
            colColors[i] = v.color;
        }

        posTex.SetPixels(posColors);
        colTex.SetPixels(colColors);
        uvTex.SetPixels(uvColors);
        posTex.Apply();
        colTex.Apply();
        uvTex.Apply();

        // 3. 保存文件
        string dir = Path.GetDirectoryName(plyFilePath);
        string fileName = Path.GetFileNameWithoutExtension(plyFilePath);

        // 保存位置图为 EXR (高动态范围，保留浮点精度)
        byte[] posBytes = posTex.EncodeToEXR(Texture2D.EXRFlags.None);
        string posPath = Path.Combine(dir, fileName + "_PosMap.exr");
        File.WriteAllBytes(posPath, posBytes);

        // 保存颜色图为 PNG
        byte[] colBytes = colTex.EncodeToPNG();
        string colPath = Path.Combine(dir, fileName + "_ColorMap.png");
        File.WriteAllBytes(colPath, colBytes);

        byte[] uvBytes = uvTex.EncodeToPNG();
        string uvPath = Path.Combine(dir, fileName + "_UVMap.png");
        File.WriteAllBytes(uvPath, uvBytes);

        Debug.Log($"Bake Complete! \nPosMap: {posPath} \nColorMap: {colPath} \nParticle Count: {count}");
        
        AssetDatabase.Refresh();
        
        // 自动设置纹理导入设置 (可选，确保不压缩)
        SetTextureImporter(posPath, true);
        SetTextureImporter(colPath, false);
        SetTextureImporter(uvPath, false);
    }

    void SetTextureImporter(string path, bool isHdr)
    {
        // 转换绝对路径为相对路径
        string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
        TextureImporter importer = AssetImporter.GetAtPath(relativePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed; // 关键：不要压缩
            importer.mipmapEnabled = false; // 关键：不需要 Mipmap
            importer.filterMode = FilterMode.Point;
            importer.npotScale = TextureImporterNPOTScale.None;
            if(isHdr) importer.textureType = TextureImporterType.Default; // EXR 默认即可
            importer.SaveAndReimport();
        }
    }
#endif
}
