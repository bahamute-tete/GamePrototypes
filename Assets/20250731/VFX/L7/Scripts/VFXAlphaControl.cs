using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
[ExecuteInEditMode]
public class VFXAlphaControl : MonoBehaviour
{
    public List<VisualEffect> effects = new List<VisualEffect>();
    public float alpha=1f;
    // Start is called before the first frame update

    private void OnEnable()
    {
        effects.Clear();
        GetComponentsInChildren<VisualEffect>(effects);
       
    }
    void Start()
    {
       
       
    }

    // Update is called once per frame
    void Update()
    {
        if (effects.Count != 0)
        {
            SetAlpha(alpha);
        }
    }

    void SetAlpha(float alpha)
    {
        foreach (var effect in effects)
        {
            effect.SetFloat("Alpha", alpha);
        }
    }

    
}
