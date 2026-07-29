using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;



//[System.Serializable]
//public class VerticesData
//{
//    public Vector3 position;
//    public Quaternion rotation;
//    public Vector3 scale;
//    public float opacity;
//    public Vector3 f_dc;
//    public float[] sh_R;  // R 通道的 15 个系数 (f_rest_0, 3, 6, 9, ...)
//    public float[] sh_G;  // G 通道的 15 个系数 (f_rest_1, 4, 7, 10, ...)
//    public float[] sh_B;  // B 通道的 15 个系数 (f_rest_2, 5, 8, 11, ...)
//}


//[System.Serializable]
//public struct SplatData
//{
//    public Vector3 pos;
//    public Vector3 scale;
//    public Vector4 rot;
//    public Vector4 color;
//}

[PreferBinarySerialization]
[CreateAssetMenu(fileName = "GaussianSplatData", menuName = "ScriptableObjects/GaussianSplat", order = 1)]
public class GaussianSplatData : ScriptableObject
{
    [HideInInspector] public Vector3[] positions;
    [HideInInspector] public Quaternion[] rotations;
    [HideInInspector] public Vector3[] scales;
    [HideInInspector] public Color[] colors; 

    [HideInInspector] public float[] shData;

    //[HideInInspector]public List<VerticesData> splatDataList;
    public uint splatCount;

}



