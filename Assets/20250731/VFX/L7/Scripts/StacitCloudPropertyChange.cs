using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class StacitCloudPropertyChange : MonoBehaviour
{
    public Material cloudMaterial;
    public Color Color = Color.white;

    static int _CloudColorID = Shader.PropertyToID("_BaseColor");
    private void OnEnable()
    {
        SetMaterials(cloudMaterial);
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (cloudMaterial == null) return;
        UpdateMaterial(cloudMaterial);


    }


    

    void SetMaterials(Material mat)
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            if (renderer.sharedMaterial != null)
            {
                renderer.sharedMaterial = mat;
            }
        }
    }

    void UpdateMaterial(Material mat)
    {
        mat.SetColor(_CloudColorID, Color);
    }
}
