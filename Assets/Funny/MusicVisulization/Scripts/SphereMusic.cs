using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;
using UnityEngine.Device;

public class SphereMusic : MonoBehaviour
{
    public int _band;
    public float _startScale, _maxScale;
    public bool _useBuffer;
    // Start is called before the first frame update
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        float freq = Mathf.Max(AudioVis._audioBandBuffer[_band], 0f);
        float amp = Mathf.Max(AudioVis._amplitudeBuffer, 0f);

        if (_useBuffer)
        {
            transform.localScale = new Vector3(
                            (freq * _maxScale) + _startScale,
                            (freq * _maxScale) + _startScale,
                            (freq * _maxScale) + _startScale
                            );


           

        }

        if (!_useBuffer)
        {
            transform.localScale = new Vector3(
                            (freq * _maxScale) + _startScale,
                            (freq * _maxScale) + _startScale,
                            (freq * _maxScale) + _startScale
                            );

           
        }
    }
}
