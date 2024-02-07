using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelTracks : MonoBehaviour
{
    public Shader drawShader;

    private RenderTexture _Splatmap;
    private Material _snowMat, _drawMat;

    private RaycastHit _Hit;

    public GameObject terrain;
    public Transform[] wheels;
    int layerMask;

    [Range(0.1f, 2), Min(0.1f)]
    public float _BrushSize;
    [Range(0.0f, 1.0f), Min(0f)]
    public float _BrushStrenth;

    // Start is called before the first frame update
    void Start()
    {
        layerMask = LayerMask.GetMask("Ground");
        _drawMat = new Material(drawShader);

        _snowMat = terrain.GetComponent<MeshRenderer>().material;
        _snowMat.SetTexture("_DisplacementMap", _Splatmap = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGBFloat));
    }

    // Update is called once per frame
    void Update()
    {
        _drawMat.SetFloat("_BrushSize", _BrushSize);
        _drawMat.SetFloat("_BrushStrenth", _BrushStrenth);
        for (int i = 0; i < wheels.Length; i++)
        {
            if (Physics.Raycast(wheels[i].position,-Vector3.up, out _Hit,1f,layerMask))
            {
                _drawMat.SetVector("_Texcoord", new Vector4(_Hit.textureCoord.x, _Hit.textureCoord.y, 0, 0));
                RenderTexture temp = RenderTexture.GetTemporary(_Splatmap.width, _Splatmap.height, 0, RenderTextureFormat.ARGBFloat);

                Graphics.Blit(_Splatmap, temp);
                Graphics.Blit(temp, _Splatmap, _drawMat);

                RenderTexture.ReleaseTemporary(temp);
            }

        }
    }
}
