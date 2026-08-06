using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using EasyButtons;
using UnityEngine;

//[ExecuteInEditMode]
public class Height : MonoBehaviour
{
    private Mesh _mesh;
    private Material _shareMaterial;
    private float _YRange;

    private Vector3 boundsMin;
    private Vector3 boundsSize;
    private Renderer rend;
    private MaterialPropertyBlock props;

    public int MaterialIndex;

    private MeshFilter _meshFilter;

    // public bool isWorld;
    // Start is called before the first frame update
    void Start()
    {
        props = new MaterialPropertyBlock();
        rend = GetComponent<Renderer>();
        _meshFilter = GetComponent<MeshFilter>();
        _shareMaterial = rend.sharedMaterial;
    }

    // Update is called once per frame
    void Update()
    {
        //if (_mesh == null)
        //{

        // _shareMaterial.SetVector("_BoundsMin", boundsMin);
        // _shareMaterial.SetVector("_BoundsSize", boundsSize);

        // Pass to shader (use MaterialPropertyBlock for efficiency)

        // if (rend != null)
        //     rend = GetComponent<Renderer>();
        //
        // if (_meshFilter != null)
        //     _meshFilter = GetComponent<MeshFilter>();

        // if (isWorld)
        // {
        //     boundsMin = rend.bounds.min; // World-space min
        //     boundsSize = rend.bounds.size;
        //     props.SetFloat("_WorldSwitch",1);
        // }
        // else
        // {
            // boundsMin = _meshFilter.mesh.bounds.min; // World-space min
            // boundsSize = _meshFilter.mesh.bounds.size;
            // props.SetFloat("_WorldSwitch", 0);
        // }

        // props.SetVector("_BoundsMin", boundsMin);
        // props.SetVector("_BoundsSize", boundsSize);
        // rend.SetPropertyBlock(props);

        //_shareMaterial.SetFloat("_Height", _mesh.bounds.size.y);
    }


#if UNITY_EDITOR
   public void SetAABB()
    {
        Material material = this.GetComponent<MeshRenderer>().sharedMaterials[MaterialIndex];
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        material.SetVector("_BoundsMin", meshFilter.mesh.bounds.min);
        material.SetVector("_BoundsSize", meshFilter.mesh.bounds.size);
    }
#endif




}
