using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenBlurRenderFeatureControl : MonoBehaviour
{
    public ScreenBlurFeature feature;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetFilterMode_Box()
    {
        feature.settings.filterType = ScreenBlurFeature.Settings.FilterType.Box;
    }

    public void SetFilterMode_Gaussian()
    {
        feature.settings.filterType = ScreenBlurFeature.Settings.FilterType.Gaussian;
    }
}
