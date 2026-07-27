using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;
using System.Globalization;
using System;

public class ConvertPLYData : MonoBehaviour
{

    const float SH_C0 = 0.28209479177387814f;   // = 1 / (2*sqrt(pi))

    [Header("PLY File Path")]
    public string plyFilePath = "Assets/ExportedMesh.ply";

    [Header("Output Settings")]
    public string outputFolder = "Assets/20250731/GaussianSplat/Resource";
    public string assetName = "GaussianSplatData";

    [System.Serializable]
    public struct PLYDate
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public float opacity;
        public Vector3 f_dc;
        public float[] sh_R;  // R 通道的 15 个系数
        public float[] sh_G;  // G 通道的 15 个系数
        public float[] sh_B;  // B 通道的 15 个系数
    }

    struct PlyPropertyHeader
    {
        public string name;
        public string type;
    }

    // 委托用于快速设置属性，避免在循环中进行字符串比较
    delegate void PropertySetter(ref PLYDate data, float val);

    List<PLYDate> ParsePLY(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("PLY file not found: " + path);
            return null;
        }

        List<PLYDate> dataList = new List<PLYDate>();
        List<PlyPropertyHeader> activeProperties = new List<PlyPropertyHeader>();
        int vertexCount = 0;
        bool isBinary = false;
        long bodyStartPos = 0;

        Debug.Log("Start Parsing PLY Header...");

        // 1. 读取 Header (二进制安全方式)
        using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            bool isVertexElement = false;
            while (true)
            {
                string line = ReadAsciiLine(fs);
                if (line == null) break;
                
                string trimLine = line.Trim();
                if (string.IsNullOrEmpty(trimLine)) continue;

                if (trimLine.StartsWith("format"))
                {
                    if (trimLine.Contains("binary_little_endian")) isBinary = true;
                }
                else if (trimLine.StartsWith("element vertex"))
                {
                    string[] parts = trimLine.Split(' ');
                    if (parts.Length >= 3) vertexCount = int.Parse(parts[2]);
                    isVertexElement = true;
                }
                else if (trimLine.StartsWith("element") && !trimLine.StartsWith("element vertex"))
                {
                    isVertexElement = false;
                }
                else if (trimLine.StartsWith("property") && isVertexElement)
                {
                    // property float x
                    string[] parts = trimLine.Split(' ');
                    if (parts.Length >= 3)
                    {
                        activeProperties.Add(new PlyPropertyHeader { 
                            type = parts[1], 
                            name = parts[parts.Length - 1] 
                        });
                    }
                }
                else if (trimLine.StartsWith("end_header"))
                {
                    bodyStartPos = fs.Position;
                    break;
                }
            }
        }

        Debug.Log($"Header Parsed. Mode: {(isBinary ? "Binary" : "ASCII")}, Vertices: {vertexCount}, Props: {activeProperties.Count}");

        // 2. 预构建 Setters (性能优化)
        List<PropertySetter> setters = new List<PropertySetter>();
        foreach (var prop in activeProperties)
        {
            setters.Add(CreateSetter(prop.name));
        }

        // 3. 读取数据体
        using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            fs.Seek(bodyStartPos, SeekOrigin.Begin);

            if (isBinary)
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    for (int i = 0; i < vertexCount; i++)
                    {
                        PLYDate data = CreateEmptyPLYDate();

                        // 遍历所有定义的属性
                        for(int p = 0; p < activeProperties.Count; p++)
                        {
                            var prop = activeProperties[p];
                            float val = 0;
                            
                            // 基础类型读取
                            switch (prop.type)
                            {
                                case "float": val = br.ReadSingle(); break;
                                case "double": val = (float)br.ReadDouble(); break;
                                case "uchar": val = (float)br.ReadByte(); break; // Color 0-255
                                case "int": val = (float)br.ReadInt32(); break;
                                default: 
                                    // 未知类型跳过字节 (假设4字节float以防万一，或者报错)
                                    // PLY通常类型固定，这里简单处理float
                                    val = br.ReadSingle(); 
                                    break;
                            }
                            
                            setters[p](ref data, val);
                        }

                        NormalizeQuaternion(ref data);
                        dataList.Add(data);
                    }
                }
            }
            else // ASCII 模式
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    for (int i = 0; i < vertexCount; i++)
                    {
                        string line = sr.ReadLine();
                        if (line == null) break;
                        
                        string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        PLYDate data = CreateEmptyPLYDate();
                        
                        for (int p = 0; p < activeProperties.Count && p < parts.Length; p++)
                        {
                            // 使用 InvariantCulture 防止系统语言导致小数点解析错误
                            if(float.TryParse(parts[p], NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                            {
                                setters[p](ref data, val);
                            }
                        }
                        
                        NormalizeQuaternion(ref data);
                        dataList.Add(data);
                    }
                }
            }
        }

        return dataList;
    }

    // 辅助方法：逐字节读取ASCII行，确保不缓冲后续的二进制数据
    string ReadAsciiLine(FileStream fs)
    {
        List<byte> bytes = new List<byte>();
        while (true)
        {
            int b = fs.ReadByte();
            if (b == -1 && bytes.Count == 0) return null; // End of file
            if (b == -1 || b == '\n') break;
            bytes.Add((byte)b);
        }
        string s = System.Text.Encoding.ASCII.GetString(bytes.ToArray());
        return s.TrimEnd('\r'); // 处理Windows换行
    }

    // 辅助方法：创建初始对象
    PLYDate CreateEmptyPLYDate()
    {
        PLYDate d = new PLYDate();
        d.f_dc = Vector3.zero;
        d.sh_R = new float[15];
        d.sh_G = new float[15];
        d.sh_B = new float[15];
        d.rotation = new Quaternion(0,0,0,1); // Identity but x,y,z,w will be set
        return d;
    }

    void NormalizeQuaternion(ref PLYDate data)
    {
        float qMag = Mathf.Sqrt(data.rotation.x * data.rotation.x +
                                data.rotation.y * data.rotation.y +
                                data.rotation.z * data.rotation.z +
                                data.rotation.w * data.rotation.w);
        if (qMag > 0.0001f)
        {
            data.rotation.x /= qMag;
            data.rotation.y /= qMag;
            data.rotation.z /= qMag;
            data.rotation.w /= qMag;
        }
    }

    PropertySetter CreateSetter(string attrName)
    {
        switch (attrName)
        {
            case "x": return (ref PLYDate d, float v) => d.position.x = v;
            case "y": return (ref PLYDate d, float v) => d.position.y = -v;
            case "z": return (ref PLYDate d, float v) => d.position.z = v;

            // sigmoid
            //case "f_dc_0": return (ref PLYDate d, float v) => d.f_dc.x = 1.0f / (1.0f + Mathf.Exp(-v));
            //case "f_dc_1": return (ref PLYDate d, float v) => d.f_dc.y = 1.0f / (1.0f + Mathf.Exp(-v));
            //case "f_dc_2": return (ref PLYDate d, float v) => d.f_dc.z = 1.0f / (1.0f + Mathf.Exp(-v));

            case "f_dc_0": return (ref PLYDate d, float v) => d.f_dc.x = Mathf.Clamp01(0.5f + SH_C0 * v);
            case "f_dc_1": return (ref PLYDate d, float v) => d.f_dc.y = Mathf.Clamp01(0.5f + SH_C0 * v);
            case "f_dc_2": return (ref PLYDate d, float v) => d.f_dc.z = Mathf.Clamp01(0.5f + SH_C0 * v);

            // scale with exponent
            case "scale_0": return (ref PLYDate d, float v) => d.scale.x = Mathf.Exp(v);
            case "scale_1": return (ref PLYDate d, float v) => d.scale.y = Mathf.Exp(v);
            case "scale_2": return (ref PLYDate d, float v) => d.scale.z = Mathf.Exp(v);

            // Unity Quaternion is (x,y,z,w)
            case "rot_0": return (ref PLYDate d, float v) => d.rotation.w = v;
            case "rot_1": return (ref PLYDate d, float v) => d.rotation.x = -v;
            case "rot_2": return (ref PLYDate d, float v) => d.rotation.y = v;
            case "rot_3": return (ref PLYDate d, float v) => d.rotation.z = -v;

            // sigmoid
            case "opacity": return (ref PLYDate d, float v) => d.opacity = 1.0f / (1.0f + Mathf.Exp(-v));
        }

        // Handle f_rest_* dynamically
        if (attrName.StartsWith("f_rest"))
        {
            try
            {
                string[] s = attrName.Split('_');
                if (s.Length >= 3 && int.TryParse(s[2], out int restIdx))
                {
                    int channel = restIdx % 3;  // 0=R, 1=G, 2=B
                    int shIdx = restIdx / 3;    // 球谐系数索引 (0-14)

                    if (shIdx < 15)
                    {
                        if (channel == 0) return (ref PLYDate d, float v) => d.sh_R[shIdx] = v;
                        if (channel == 1) return (ref PLYDate d, float v) => d.sh_G[shIdx] = v;
                        if (channel == 2) return (ref PLYDate d, float v) => d.sh_B[shIdx] = v;
                    }
                }
            }
            catch { }
        }

        // Return empty action for unknown properties
        return (ref PLYDate d, float v) => { };
    }

    [ContextMenu("CreateGSAsset")]
    public void ConvertPLYToScriptableObject()
    {
        if (!File.Exists(plyFilePath))
        {
            Debug.LogError("PLY file not found: " + plyFilePath);
            return;
        }

        List<PLYDate> plyDatas = ParsePLY(plyFilePath);

        if (plyDatas == null || plyDatas.Count == 0)
        {
            Debug.LogError("Failed to parse PLY or no data found.");
            return;
        }
        
        Debug.Log($"Parsed {plyDatas.Count} vertices successfully!");

#if UNITY_EDITOR
        GaussianSplatData splatData = ScriptableObject.CreateInstance<GaussianSplatData>();
        //splatData.splatDataList = new List<VerticesData>();

        int count = plyDatas.Count;
        splatData.positions = new Vector3[count];
        splatData.rotations = new Quaternion[count];
        splatData.scales = new Vector3[count];
        splatData.colors = new Color[count];
        splatData.splatCount = (uint)count;

        for (int i = 0; i < count; i++)
        { 
            var plyData = plyDatas[i];
            splatData.positions[i] = plyData.position;
            splatData.rotations[i] = plyData.rotation;
            splatData.scales[i] = plyData.scale;
            splatData.colors[i] = new Color(plyData.f_dc.x, plyData.f_dc.y, plyData.f_dc.z, plyData.opacity);

            // 填充 SH 数据
            //for (int j = 0; j < 15; j++)
            //{
            //    splatData.shData[i * 45 + j] = plyData.sh_R[j];
            //    splatData.shData[i * 45 + 15 + j] = plyData.sh_G[j];
            //    splatData.shData[i * 45 + 30 + j] = plyData.sh_B[j];
            //}

        }

        if (Directory.Exists(outputFolder))
        { 
            Directory.CreateDirectory(outputFolder);
        }

        string assetPath = Path.Combine(outputFolder, assetName + ".asset");
        AssetDatabase.CreateAsset(splatData, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GaussianSplatData asset created at: " + assetPath);
#endif
    }
}
