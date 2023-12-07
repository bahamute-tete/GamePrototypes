using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstanceCubes : MonoBehaviour
{
    public GameObject _sampleCubePrefab;
    GameObject[] _sampleCube = new GameObject[512];

    public float _maxScale;
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < _sampleCube.Length; i++)
        { 
            GameObject _instanceSampleCube = Instantiate(_sampleCubePrefab);
            _instanceSampleCube.transform.position = this.transform.position;
            _instanceSampleCube.transform.parent = this.transform;
            _instanceSampleCube.name = "SampleCube" + i;
            this.transform.eulerAngles = new Vector3(0, -0.703125f * i, 0);
            _instanceSampleCube.transform.position = Vector3.forward * 13f;
           
            _sampleCube[i] = _instanceSampleCube;
        }
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < _sampleCube.Length; i++)
        {
            if (_sampleCube != null)
            {
                _sampleCube[i].transform.localScale = new Vector3(1, AudioVis._samples[i]*_maxScale, 1);
            }
        }
    }
}
