using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnowFall : MonoBehaviour
{
    public Shader shader;

    private Material snowFallMat;
    private MeshRenderer meshRenderer => GetComponent<MeshRenderer>();

    [Range(0.001f,0.1f)]
    public float flakeAmount;
    [Range(0f,1f)]
    public float flakeOpacity;

    public float uvScale;
    // Start is called before the first frame update
    void Start()
    {
        snowFallMat = new Material(shader);
    }

    // Update is called once per frame
    void Update()
    {
        snowFallMat.SetFloat("_FlakeAmount", flakeAmount);
        snowFallMat.SetFloat("_FlakeOpacity", flakeOpacity);
        snowFallMat.SetFloat("_UVScale", uvScale);

        RenderTexture snow = (RenderTexture)meshRenderer.material.GetTexture("_DisplacementMap");

        RenderTexture temp = RenderTexture.GetTemporary(snow.width, snow.height, 0, RenderTextureFormat.ARGBFloat);

        Graphics.Blit(snow, temp,snowFallMat);
        Graphics.Blit(temp, snow);
        meshRenderer.material.SetTexture("_DisplacementMap", snow);

        RenderTexture.ReleaseTemporary(temp);
    }
}
