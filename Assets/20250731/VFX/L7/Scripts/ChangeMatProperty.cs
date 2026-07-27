using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ChangeMatProperty : MonoBehaviour
{

    public List<Transform> piecesL= new List<Transform>();
    public List<Transform> piecesR = new List<Transform>();
    public Material mat_L, mat_R;

    public Color baseColor = Color.white;
    [ColorUsage(true,true) ]public Color lightBandColor = Color.white;
    [Range(0f,1.0f)]public float blend_l = 0.5f;
    [Range(0f, 1.0f)] public float blend_r = 0.5f;
    [Range(0f, 1.0f)] public float dissolve =0f;




    static int _BaseColorPropertyID = Shader.PropertyToID("_BaseColor");
    static int _BlendPropertyID = Shader.PropertyToID("_Blend");
    static int _LightBandColorPropertyID = Shader.PropertyToID("_LightBandColor");
    static int _DissolvePropertyID = Shader.PropertyToID("_Dissoive");

    // Start is called before the first frame update
    void Start()
    {
        SetChildrenMaterials(piecesL, mat_L);
        SetChildrenMaterials(piecesR, mat_R);
    }

    // Update is called once per frame
    void Update()
    {
#if UNITY_EDITOR
        if (piecesL.Count == 0 && piecesR.Count == 0) return;
        if (mat_L == null && mat_R == null) return;

        SetChildrenMaterials(piecesL, mat_L);
        SetChildrenMaterials(piecesR, mat_R);
#endif
        if (mat_L != null)
            MaterialParameterUpdate(mat_L, 1);
        if (mat_R != null)
            MaterialParameterUpdate(mat_R, 0);
    }

    private void SetChildrenMaterials(List<Transform> members, Material mat)
    {
        if (mat == null) return;
        
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] != null)
            {
                Renderer renderer = members[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = mat;
                }
            }
        }
    }

    private void MaterialParameterUpdate(Material mat, int side)
    {
        if (mat == null) return;
        
        if (mat.HasProperty(_BaseColorPropertyID))
            mat.SetColor(_BaseColorPropertyID, baseColor);
            
        if (mat.HasProperty(_LightBandColorPropertyID))
            mat.SetColor(_LightBandColorPropertyID, lightBandColor);
            
        if (mat.HasProperty(_BlendPropertyID))
            mat.SetFloat(_BlendPropertyID, side == 1 ? blend_l : blend_r);

        if (mat.HasProperty(_DissolvePropertyID))
            mat.SetFloat(_DissolvePropertyID, dissolve);
    }
}
