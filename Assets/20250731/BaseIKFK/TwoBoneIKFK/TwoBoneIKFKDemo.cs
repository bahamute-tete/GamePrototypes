using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]

public class TwoBoneIKFKDemo : MonoBehaviour
{

    public enum ControlMode{ IK, FK}
    public enum IKSolverType{ Absolute, Incremental, ThreeD }

    [SerializeField] private ControlMode controlMode = ControlMode.IK;
    [SerializeField] private IKSolverType ikSolver = IKSolverType.Absolute;
    [SerializeField] private int elbowDirection = 1; // 1 for right, -1 for left
    [SerializeField] private float fkDegreesPerSecond = 90f; // Degrees per second for FK rotation

    [Header("Joint Transforms")]
    [SerializeField] private Transform poleTarget; // 3D only: defines which way the elbow bends; falls back to current elbow direction if null
    [SerializeField] private Transform root;
    [SerializeField] private Transform elbow;
    [SerializeField] private Transform hand;

    [Header("Target Transform")]
    [SerializeField] private Transform target;

    [Header("IK/FK Visualization")]
    [SerializeField] private LineRenderer chainLine;
    [SerializeField] private float chainLineWidth = 0.1f;


    [ContextMenu("SnapTargetToHand")]
    private void SnapTargetToHand()
    {
        if (target != null && hand != null)
        {
            target.position = hand.position;
            target.rotation = hand.rotation;
        }
    }

   

    private void Awake()
    {
        CreateLineIfNeed();
    }

    private void CreateLineIfNeed()
    {
        if (chainLine != null) return;

        chainLine = gameObject.AddComponent<LineRenderer>();
        chainLine.positionCount = 3;
        chainLine.useWorldSpace = true;
        chainLine.widthMultiplier = chainLineWidth;
        chainLine.numCapVertices = 6;
        chainLine.material = new Material(Shader.Find("Sprites/Default"));
        chainLine.startColor = Color.cyan;
        chainLine.endColor = Color.cyan;
    }





    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (!HasValidChain()) return;

        HandleKeyboardDemoControls();

        if (controlMode == ControlMode.IK)
        {
            switch (ikSolver)
            {
                case IKSolverType.Incremental:
                    SolveIKIncremental();
                    break;
                case IKSolverType.ThreeD:
                    SolveIK3D();
                    break;
                default:
                    SolveIK();
                    break;
            }
        }

        DrawChain();
    }




    private bool HasValidChain()
    {
        return root != null && elbow != null && hand != null && target != null;
    }

    private void HandleKeyboardDemoControls()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Tab))
            controlMode = controlMode == ControlMode.IK ? ControlMode.FK : ControlMode.IK;

     if (Input.GetKeyDown(KeyCode.F))
            elbowDirection *= -1;

    if (controlMode == ControlMode.FK)
        {
            float rootInput = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            float elbowInput = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            root.Rotate(0f, 0f, rootInput * fkDegreesPerSecond * Time.deltaTime, Space.Self);
            elbow.Rotate(0f, 0f, elbowInput * fkDegreesPerSecond * Time.deltaTime, Space.Self);
        }
#endif
    }

    private void SolveIK()
    {
        Vector2 rootPosition = root.position;
        Vector2 targetOffset =(Vector2)target.position - rootPosition;

       
        
        float upperLength = Vector2.Distance(root.position, elbow.position);
        float lowerLength = Vector2.Distance(elbow.position, hand.position);

        if (upperLength < 0.0001f || lowerLength < 0.0001f) return;

        float distance = targetOffset.magnitude;
        float minimumDistance = Mathf.Abs(upperLength - lowerLength)+0.0001f;
        float maximumDistance = upperLength + lowerLength - 0.0001f;

        distance = Mathf.Clamp(distance, minimumDistance, maximumDistance);

        Vector2 direction = targetOffset.sqrMagnitude > 0.0001f ? targetOffset.normalized : Vector2.right;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
       

        float shoulderCos =Mathf.Clamp((upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2 * upperLength * distance), -1f, 1f);
        float wristCos = Mathf.Clamp((lowerLength * lowerLength + distance * distance - upperLength * upperLength) / (2 * lowerLength * distance), -1f, 1f);

        float shoulderOffset = Mathf.Acos(shoulderCos) * Mathf.Rad2Deg;
        float wristOffset = Mathf.Acos(wristCos) * Mathf.Rad2Deg;
        float bend = Mathf.Sign(elbowDirection==0?1:elbowDirection);

        root.rotation = Quaternion.Euler(0, 0, targetAngle - shoulderOffset * bend);
        elbow.rotation = Quaternion.Euler(0, 0, targetAngle + wristOffset * bend);

    }


    private void SolveIKIncremental()
    {
        Vector2 rootPosition = root.position;
        Vector2 targetOffset = (Vector2)target.position - rootPosition;

        float upperLength = Vector2.Distance(root.position, elbow.position);
        float lowerLength = Vector2.Distance(elbow.position, hand.position);

        if (upperLength < 0.0001f || lowerLength < 0.0001f) return;

        float distance = targetOffset.magnitude;
        float minimumDistance = Mathf.Abs(upperLength - lowerLength) + 0.0001f;
        float maximumDistance = upperLength + lowerLength - 0.0001f;

        distance = Mathf.Clamp(distance, minimumDistance, maximumDistance);

        Vector2 direction = targetOffset.sqrMagnitude > 0.0001f ? targetOffset.normalized : Vector2.right;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        float shoulderCos = Mathf.Clamp((upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2 * upperLength * distance), -1f, 1f);
        float wristCos = Mathf.Clamp((lowerLength * lowerLength + distance * distance - upperLength * upperLength) / (2 * lowerLength * distance), -1f, 1f);

        float shoulderOffset = Mathf.Acos(shoulderCos) * Mathf.Rad2Deg;
        float wristOffset = Mathf.Acos(wristCos) * Mathf.Rad2Deg;
        float bend = Mathf.Sign(elbowDirection == 0 ? 1 : elbowDirection);
        float desiredUpperAngle = targetAngle - shoulderOffset * bend;
        float desiredLowerAngle = targetAngle + wristOffset * bend;


        Vector2 currentUpperDir = ((Vector2)elbow.position - rootPosition).normalized;
        float currentUpperAngle = Mathf.Atan2(currentUpperDir.y, currentUpperDir.x) * Mathf.Rad2Deg;
        float rootDelta = Mathf.DeltaAngle(currentUpperAngle, desiredUpperAngle);
        root.rotation = Quaternion.AngleAxis(rootDelta, Vector3.forward) * root.rotation;

        Vector2 currentLowerDir = ((Vector2)hand.position - (Vector2)elbow.position).normalized;
        float currentLowerAngle = Mathf.Atan2(currentLowerDir.y, currentLowerDir.x) * Mathf.Rad2Deg;
        float elbowDelta = Mathf.DeltaAngle(currentLowerAngle, desiredLowerAngle);
        elbow.rotation = Quaternion.AngleAxis(elbowDelta, Vector3.forward) * elbow.rotation;
    }


    private void SolveIK3D()
    {
        Vector3 rootPosition = root.position;
        Vector3 targetOffset = target.position - rootPosition;

        float upperLength = Vector3.Distance(root.position, elbow.position);
        float lowerLength = Vector3.Distance(elbow.position, hand.position);

        if (upperLength < 0.0001f || lowerLength < 0.0001f) return;

        float distance = targetOffset.magnitude;
        float minimumDistance = Mathf.Abs(upperLength - lowerLength) + 0.0001f;
        float maximumDistance = upperLength + lowerLength - 0.0001f;

        distance = Mathf.Clamp(distance, minimumDistance, maximumDistance);

        Vector3 dirToTarget = targetOffset.sqrMagnitude > 0.0001f ? targetOffset.normalized : root.right;

        // Bend reference: project the pole direction onto the plane perpendicular
        // to dirToTarget. Fall back to the current elbow direction, then world up,
        // so the elbow never flips when the pole is parallel to the target line.
        Vector3 poleDir = poleTarget != null
            ? poleTarget.position - rootPosition
            : elbow.position - rootPosition;

        Vector3 bendRef = Vector3.ProjectOnPlane(poleDir, dirToTarget);
        if (bendRef.sqrMagnitude < 0.0001f)
            bendRef = Vector3.ProjectOnPlane(elbow.position - rootPosition, dirToTarget);
        if (bendRef.sqrMagnitude < 0.0001f)
            bendRef = Vector3.ProjectOnPlane(Vector3.up, dirToTarget);
        if (bendRef.sqrMagnitude < 0.0001f)
            bendRef = Vector3.ProjectOnPlane(Vector3.forward, dirToTarget);
        bendRef.Normalize();

        // Plane normal: rotating dirToTarget around this axis moves it toward bendRef
        Vector3 axis = Vector3.Cross(dirToTarget, bendRef).normalized;


        float shoulderCos = Mathf.Clamp((upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2 * upperLength * distance), -1f, 1f);
        float wristCos = Mathf.Clamp((lowerLength * lowerLength + distance * distance - upperLength * upperLength) / (2 * lowerLength * distance), -1f, 1f);

        float shoulderOffset = Mathf.Acos(shoulderCos) * Mathf.Rad2Deg;
        float wristOffset = Mathf.Acos(wristCos) * Mathf.Rad2Deg;


        Vector3 desiredUpperDir = Quaternion.AngleAxis(shoulderOffset, axis) * dirToTarget;
        Vector3 desiredLowerDir = Quaternion.AngleAxis(-wristOffset, axis) * dirToTarget;


        Vector3 currentUpperDir = (elbow.position - rootPosition).normalized;
        root.rotation = Quaternion.FromToRotation(currentUpperDir, desiredUpperDir) * root.rotation;

     
        Vector3 currentLowerDir = (hand.position - elbow.position).normalized;
        elbow.rotation = Quaternion.FromToRotation(currentLowerDir, desiredLowerDir) * elbow.rotation;
    }

    public void SetIK(bool useIK)
    {
        controlMode = useIK ? ControlMode.IK : ControlMode.FK;
    }


    private void DrawChain()
    {
        if (chainLine == null) return;
        chainLine.widthMultiplier = chainLineWidth;
        chainLine.SetPosition(0, root.position);
        chainLine.SetPosition(1, elbow.position);
        chainLine.SetPosition(2, hand.position);
    }

    private void OnDrawGizmos()
    {
        if (root == null || elbow == null || hand == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(root.position, elbow.position);
        Gizmos.DrawLine(elbow.position, hand.position);

        DrawPoleGizmos();
    }

    private void DrawPoleGizmos()
    {
        if (poleTarget == null || ikSolver != IKSolverType.ThreeD) return;

        Vector3 rootPosition = root.position;
        Vector3 toTarget = target != null ? target.position - rootPosition : Vector3.zero;
        if (toTarget.sqrMagnitude < 0.0001f) return;
        Vector3 dirToTarget = toTarget.normalized;

        // Magenta: root -> pole, plus a marker at the pole position
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(rootPosition, poleTarget.position);
        Gizmos.DrawWireSphere(poleTarget.position, 0.05f);

        // Cyan: the projected bend reference direction actually used by the solver
        Vector3 bendRef = Vector3.ProjectOnPlane(poleTarget.position - rootPosition, dirToTarget);
        if (bendRef.sqrMagnitude > 0.0001f)
        {
            Vector3 mid = (rootPosition + elbow.position + hand.position) / 3f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(mid, mid + bendRef.normalized * 0.5f);
        }
    }
}
