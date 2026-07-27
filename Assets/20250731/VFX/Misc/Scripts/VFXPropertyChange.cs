using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class VFXPropertyChange : MonoBehaviour
{
    public Texture3D initialTexture3D;
    public VisualEffect visualEffect;
    public VisualEffectAsset effectAsset;

    public List<Texture3D> sdfs = new List<Texture3D>();


    
    void Start()
    {

        List<VFXExposedProperty> properties = new List<VFXExposedProperty>();
        effectAsset.GetExposedProperties(properties);

        SetTexture3DProperty("sdfTexture", initialTexture3D);

    }


    public void  SetTexture3DProperty(string propertyName, Texture3D texture)
    {
        if (visualEffect != null && texture != null)
        {
            visualEffect.SetTexture(propertyName, texture);
           // Debug.Log($"设置Texture3D属性 {propertyName}");
        }
    }
    public void SetTimeShiftProperty(string propertyName, float timeShift)
    {
        if (visualEffect != null)
        {
            visualEffect.SetFloat(propertyName, timeShift);
            // Debug.Log($"设置Texture3D属性 {propertyName}");
        }
    }




    public void SetTexture3DsProperty(string propertyName, List<Texture3D> texture3Ds)
    {
        if (visualEffect != null && texture3Ds.Count != 0)
        {
            int randomIndex = Random.Range(0, texture3Ds.Count);
            visualEffect.SetTexture(propertyName, texture3Ds[randomIndex]);
        }
    }

    float GetFloatProperty(string propertyName)
    {
        if (visualEffect != null)
        {
            return visualEffect.GetFloat(propertyName);
        }
        return 0f;
    }
}

