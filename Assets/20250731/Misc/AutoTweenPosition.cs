using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class AutoTweenPosition : MonoBehaviour
{
    [Min(0f)]
    public float duration = 1f;

    float value;

    [SerializeField] bool autoReverse = false, smoothStep = false;

    public bool Reversed { get; set; }
    public bool AutoReversed
    {
        get => autoReverse;
        set => autoReverse = value;
    }

    public Vector3 from = default, to = default;

    public Transform relative = default;



    float SmoothValue => 3f * value * value - 2f * value * value * value;

    public float Duration { get => duration; set => duration = value; }


    void Update()
    {
        float delta = Time.deltaTime / Duration;
        if (Reversed)
        {
            value -= delta;
            if (value <= 0f)
            {
                if (autoReverse)
                {
                    value = Mathf.Min(1f, -value);
                    Reversed = false;
                }
                else
                {
                    value = 0f;
                    enabled = false;
                }
            }
        }
        else
        {
            value += delta;
            if (value >= 1f)
            {
                if (autoReverse)
                {
                    value = Mathf.Max(0f, 2f - value);
                    Reversed = true;
                }
                else
                {
                    value = 1f;
                    enabled = false;
                }
            }
        }

        Interpolate(smoothStep ? SmoothValue : value);
    }


    private void OnEnable()
    {
        transform.localPosition = Vector3.zero;
        value = 0;
    }

    private void OnDisable()
    {
        transform.localPosition = Vector3.zero;
        value = 0;
    }


    void Interpolate(float t)
    {
        Vector3 p;
        if (relative)
        {
            p = Vector3.LerpUnclamped(relative.TransformPoint(from), relative.TransformPoint(to), t);
        }
        else
        {
            p = Vector3.LerpUnclamped(from, to, t);

        }

        transform.position = p;
    }
}
