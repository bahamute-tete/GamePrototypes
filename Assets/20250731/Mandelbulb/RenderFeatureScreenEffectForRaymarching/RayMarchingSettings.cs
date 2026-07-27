using UnityEngine;

[System.Serializable]
public struct FogSettings
{
    public Vector3 boxCenter;
    public Vector3 boxSize;
    public Color baseColor;
    public Color targetColor;
    public float henyeyGreenstein_G;
    public float absorption;
    public float scatteringCoeff;
    public float ambientLightIntensity;
    public float directLightIntensity;
    public float density;
    public float stepSize;
}

[System.Serializable]
public struct RayMarchingRenderSettings
{
    public float aoIntensity;
    public Cubemap environment;
    public Texture3D sdfTexture;
}
