using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ConstraintType
{
    None,
    Distance,
    EndPointPlane,
    EqualSpacing,
    EndAndDistance,
    EqualSpacingAndEndPointPlane,
    DistanceAndEndPointPlaneAndEqualSpacing,
}

public class PointFllowingManager : MonoBehaviour
{

    public Transform[] points = new Transform[4]; // P0, P1, P2, P3

    [Header("Settings")]
    public Vector3 initialDirection = Vector3.forward; // Changed to screen inward
    public float[] followSpeeds = new float[4] { 1f, 0.8f, 0.6f, 0.4f }; 
    public float lineLength = 6f;
    public float restoreSpeed = 1f; 
    private float spacing = 2f;

    [Header("Control Settings")]
    public float moveSpeed = 3f;
    public KeyCode upKey = KeyCode.W;
    public KeyCode downKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;

    [Header("Mouse Control Settings")]
    //public bool useMouseControl = false;
    public float mouseMoveSpeed = 5f;
    public float mouseSensitivity = 1f;

    private Vector3 lastMousePosition;
    private bool isFirstFrame = true;

    [Header("Constraint Settings")]
    public ConstraintType constraintType = ConstraintType.Distance;
    public int constraintIterations = 3;

    [Header("End Point Plane Constraint")]
    public Vector3 planeNormal = Vector3.up;
    public float planeDistance = 0f;
    public Transform planeReference;

    private bool isMoving = false;
    private Vector3[] targetPositions = new Vector3[4]; 
    private Vector3[] velocities = new Vector3[4]; 
    private Vector3 lastP0Position;

    public float test1 = 10.0f;
    public float test2 = 10.0f;

    public Camera mainCamera;
    // Start is called before the first frame update
    void Start()
    {
        if (mainCamera == null)
        { 
            mainCamera = Camera.main;
        }
        AdjustPoints();
    }

    // Update is called once per frame
    void Update()
    {

        //ControlFirstPointWithMouse();
        ControlFirstPoint();
        UpdateFollowingPoints();

        switch (constraintType)
        {
            case ConstraintType.Distance:
                DistanceConstraints();
                break;

            case ConstraintType.EndPointPlane:
                EndPointPlaneConstraint();
                break;
            case ConstraintType.EqualSpacingAndEndPointPlane:
                EqualSpacingConstraint();
                EndPointPlaneConstraint();
                break;
            case ConstraintType.EndAndDistance:
                DistanceConstraints();
                EndPointPlaneConstraint();
                break;

            case ConstraintType.DistanceAndEndPointPlaneAndEqualSpacing:
                DistanceConstraints();
                EndPointPlaneConstraint();
                EqualSpacingConstraint();
                break;

            default:
                // No constraints applied
                break;
        }
    }


    void AdjustPoints()
    {
        Vector3 startPosition = points[0].position;

        if (constraintType == ConstraintType.EndPointPlane || constraintType == ConstraintType.EndAndDistance || constraintType == ConstraintType.EqualSpacingAndEndPointPlane || constraintType ==ConstraintType.DistanceAndEndPointPlaneAndEqualSpacing)
        {
            Vector4 l = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, planeDistance);
            Vector4 s = new Vector4(startPosition.x, startPosition.y, startPosition.z, 1.0f);

            lineLength = Vector4.Dot(l, s) /Vector4.Dot(l,initialDirection);
        }

        spacing = lineLength / 3.0f; // Adjust spacing based on line length and number of segments
        
           

        if (points.Length == 4)
        {
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null)
                {

                    points[i].name = $"Point_P{i}";
                    points[i].position = startPosition + initialDirection * i * spacing;
                    points[i].localScale = Vector3.one;
                }
            }
        }
        else
        {
            Debug.Log("Need 4 Points");
        }
        
    }


    void ControlFirstPoint()
    {
        if (points[0] == null) return;

        Vector3 movement = Vector3.zero;

        if (Input.GetKey(upKey)) movement += Vector3.forward;
        if (Input.GetKey(downKey)) movement += Vector3.back;
        if (Input.GetKey(leftKey)) movement += Vector3.left;
        if (Input.GetKey(rightKey)) movement += Vector3.right;



        if (movement != Vector3.zero)
        {
            movement = movement.normalized * moveSpeed * Time.deltaTime;
            points[0].position += movement;
            isMoving = true;
        }
        else
        {
            isMoving = false;
        }

        Vector3 p0Movement = points[0].position - lastP0Position;
        lastP0Position = points[0].position;
        velocities[0] = p0Movement / Time.deltaTime;
        targetPositions[0] = points[0].position;

    }

    void ControlFirstPointWithMouse()
    {
        if (points[0] == null || mainCamera == null) return;

        // Create a ray from camera through mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Debug.Log($"{ray.origin}");
        // Project onto a plane at the desired distance
        float targetDistance = Vector3.Distance(mainCamera.transform.position, points[0].position);
        Vector3 mouseWorldPos = ray.origin +ray.direction * targetDistance;
        
        if (isFirstFrame)
        {
            lastMousePosition = mouseWorldPos;
            isFirstFrame = false;
            isMoving = false;
        }
        else
        {
            Vector3 mouseDelta = mouseWorldPos - lastMousePosition;
            
            if (mouseDelta.magnitude > 0.01f)
            {
                Vector3 targetPos = points[0].position + mouseDelta * mouseMoveSpeed * mouseSensitivity;
                points[0].position = Vector3.Lerp(points[0].position, targetPos, mouseMoveSpeed * Time.deltaTime);
                isMoving = true;
            }
            else
            {
                isMoving = false;
            }
            
            lastMousePosition = mouseWorldPos;
        }
        
        Vector3 p0Movement = points[0].position - lastP0Position;
        lastP0Position = points[0].position;
        velocities[0] = p0Movement / Time.deltaTime;
        targetPositions[0] = points[0].position;
    }


    void UpdateFollowingPoints()
    {
        for (int i = 1; i < 4; i++)
        {
            if (points[i] == null) continue;

            if (isMoving)
            {
                Vector3 prePos = points[i-1].position;
                Vector3 curPos = points[i].position;

                Vector3 vectorToPrePoint = prePos - curPos;
                float currentDistance = vectorToPrePoint.magnitude;

                if (currentDistance > spacing*0.1f)
                {
                    Vector3 dir = vectorToPrePoint.normalized;
                    Vector3 desierPos = prePos - dir * spacing;
                    float lerpSpeed = followSpeeds[i] * test1;
                    targetPositions[i] = Vector3.Lerp(curPos, desierPos, lerpSpeed * Time.deltaTime);
                }
                else
                {
                    targetPositions[i] = curPos;
                }
            }
            else
            {
                Vector3 dir = initialDirection;
                Vector3 rootPosition = points[0].position;
                Vector3 desierPos = rootPosition + dir * i * spacing;
                targetPositions[i]= Vector3.Lerp(targetPositions[i], desierPos, restoreSpeed * Time.deltaTime);
            }

            Vector3 oldPos = points[i].position;
            points[i].position = targetPositions[i];

            velocities[i] = (points[i].position -oldPos) / Time.deltaTime;
        }
    }

    void DistanceConstraints()
    {
        for (int j = 0; j < constraintIterations; j++)
        {
            for (int i = 0; i < 3; i++)
            {
                if (points[i] == null || points[i + 1] == null) continue;

                Vector3 curPos = points[i].position;
                Vector3 nextPos = points[i + 1].position;

                Vector3 vectorToNextPos = nextPos - curPos;
                float vectorBias = vectorToNextPos.magnitude;

                if (vectorBias > 0.001f)
                {
                    Vector3 dir = vectorToNextPos.normalized;

                    Vector3 finalPos;

                    if (isMoving)
                    {
                        finalPos = curPos + dir * spacing;
                    }
                    else
                    { 
                        Vector3 idealPos = points[0].position + initialDirection * (i+1) * spacing;

                        Vector3 idealDir = (idealPos - curPos).normalized;

                        Vector3 blendDir = Vector3.Lerp(dir, idealDir, Mathf.Clamp01( restoreSpeed*Time.deltaTime/(i+1)));

                        finalPos = curPos + blendDir * spacing;
                    }

                    points[i + 1].position = finalPos;
                }


            }
        }

        for (int i = 1; i < 4; i++)
        {
            targetPositions[i] = points[i].position;
        }

    }


    void EqualSpacingConstraint()
    {
        // 计算从第一个点到最后一个点的总距离
        Vector3 startPos = points[0].position;
        Vector3 endPos = points[3].position;
        Vector3 totalVector = endPos - startPos;
        float totalDistance = totalVector.magnitude;
        
        if (totalDistance > 0.001f)
        {
            Vector3 direction = totalVector.normalized;
            float adjustedSpacing = totalDistance / 3.0f;
            
           
            for (int i = 1; i < 3; i++) // 只调整P1和P2
            {
                if (points[i] != null)
                {
                    Vector3 newPos = startPos + direction * (adjustedSpacing * i);
                    
                    // 根据移动状态调整lerp速度
                    float lerpSpeed = isMoving ? followSpeeds[i] * test2 : restoreSpeed * 0.5f;
                    
                    points[i].position = Vector3.Lerp(points[i].position, newPos, lerpSpeed * Time.deltaTime);
                    targetPositions[i] = points[i].position;
                }
            }
        }
    }


    void EndPointPlaneConstraint()
    {
        if (points[3] == null) return;

        Vector3 normal = planeNormal.normalized;
        Vector3 planePoint;

        if (planeReference != null)
        {
            planePoint = planeReference.position;
        }
        else
        {
            planePoint = normal * planeDistance;
        }

        Vector3 endPointPos = points[3].position;
        Vector3 vectorToPlane = endPointPos - planePoint;
        float distanceToPlane = Vector3.Dot(vectorToPlane, normal);

        if (Mathf.Abs(distanceToPlane) > 0.001f)
        {
            Vector3 projectedPos = endPointPos - normal * distanceToPlane;
            points[3].position = projectedPos;
            targetPositions[3] = projectedPos;
        }
    }
}


