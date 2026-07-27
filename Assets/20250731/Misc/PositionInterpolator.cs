using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PositionInterpolator : MonoBehaviour
{


    public Vector3 from = default, to = default;

    public Transform relative = default;

    public void Interpolate(float t)
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

        transform.position =p;
    }
}
