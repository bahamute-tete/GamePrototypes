using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawTrance : MonoBehaviour
{

    public Camera camera;
    public Shader drawShader;

    [Range(1f,500f),Min(1.0f)]
    public float _BrushSize;
    [Range(0.0f, 1.0f), Min(0f)]
    public float _BrushStrenth;

    private RenderTexture _Splatmap;
    private Material _snowMat, _drawMat;

    private RaycastHit _Hit;
    // Start is called before the first frame update
    void Start()
    {
        _drawMat = new Material(drawShader);
        _drawMat.SetVector("_Color", Color.red);

        _snowMat = GetComponent<MeshRenderer>().material;
        _Splatmap = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGBFloat);
        _snowMat.SetTexture("_DisplacementMap", _Splatmap);

    }

    // Update is called once per frame
    void Update()
    {
        _drawMat.SetFloat("_BrushSize", _BrushSize);
        _drawMat.SetFloat("_BrushStrenth", _BrushStrenth);

        if (Input.GetKey(KeyCode.Mouse0))
        {
            if (Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition), out _Hit))
            {
                _drawMat.SetVector("_Texcoord", new Vector4(_Hit.textureCoord.x, _Hit.textureCoord.y, 0, 0));
                RenderTexture temp = RenderTexture.GetTemporary(_Splatmap.width, _Splatmap.height,0, RenderTextureFormat.ARGBFloat);

                Graphics.Blit(_Splatmap, temp);
                Graphics.Blit(temp, _Splatmap, _drawMat);

                RenderTexture.ReleaseTemporary(temp);
            }
        }
    }


    private void OnGUI()
    {
        GUI.DrawTexture(new Rect(0,0,256,256),_Splatmap,ScaleMode.ScaleToFit,false,1);
    }
}
