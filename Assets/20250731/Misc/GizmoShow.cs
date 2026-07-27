using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class GizmoShow : MonoBehaviour
{

    public float scaleFactor = 1.0f;
    public Color gizmoColor = Color.yellow;
    public float normalLength = 1.0f;

    public enum GizmosShape {Box,Sphere };
    public GizmosShape shape = GizmosShape.Box;

    // Start is called before the first frame update
    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;

        Gizmos.matrix = transform.localToWorldMatrix;

        switch (shape)
        {
            case GizmosShape.Box:
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one * scaleFactor);

                Gizmos.DrawLine(Vector3.zero, Vector3.zero + new Vector3(0, 0, 1) * normalLength);

                Vector3 startPos = Vector3.zero + new Vector3(0, 0, 1) * normalLength;
                Vector3 endPos1 = startPos + new Vector3(0, -0.5f, -1) * normalLength * 0.1f;
                Vector3 endPos2 = startPos + new Vector3(0, 0.5f, -1) * normalLength * 0.1f;
                Vector3 endPos3 = startPos + new Vector3(0.5f, 0, -1) * normalLength * 0.1f;
                Vector3 endPos4 = startPos + new Vector3(-0.5f, 0, -1) * normalLength * 0.1f;
                Gizmos.DrawLine(startPos, endPos1);
                Gizmos.DrawLine(startPos, endPos2);
                Gizmos.DrawLine(startPos, endPos3);
                Gizmos.DrawLine(startPos, endPos4);
                break;

            case GizmosShape.Sphere:
                Gizmos.DrawWireSphere(Vector3.zero, 1.0f*scaleFactor);
                break;



        }
        

    }
}
