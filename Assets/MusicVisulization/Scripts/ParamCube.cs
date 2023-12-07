using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParamCube : MonoBehaviour
{
    public int _band;
    public float _startScale, _maxScale;
    public bool _useBuffer;
    Material _material;
    public float _red,_green, _blue;
    // Start is called before the first frame update
    void Start()
    {
       _material = GetComponentInChildren<MeshRenderer>().material;
    }

    // Update is called once per frame
    void Update()
    {
         float freq =Mathf.Max( AudioVis._audioBandBuffer[_band],0f);
        float amp = Mathf.Max(AudioVis._amplitudeBuffer, 0f);
        float r = _red* freq;
        float g= _green * freq;
        float b = _blue * freq;
        if (_useBuffer)
        {
            transform.localScale = new Vector3(
                            transform.localScale.x,
                            (freq * _maxScale) + _startScale,
                            transform.localScale.z
                            );

          
            Color _color = new Color(r,g,b);
            _material.SetColor("_Color", _color);
        
        }

        if (!_useBuffer)
        {
            transform.localScale = new Vector3(
                            transform.localScale.x,
                            (freq * _maxScale) + _startScale,
                            transform.localScale.z
                            );

            Color _color = new Color(freq, freq, freq);
            _material.SetColor("_Color", _color);
        }
    }
}
