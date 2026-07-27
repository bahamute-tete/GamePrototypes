using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class UVOffset : MonoBehaviour
{
    public Vector2 offsetSpeed;
    Material mat => GetComponent<Renderer>().sharedMaterial;
    int offsetID = Shader.PropertyToID("_Offset");
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        mat.SetVector(offsetID, Time.time* offsetSpeed);
    }
}
