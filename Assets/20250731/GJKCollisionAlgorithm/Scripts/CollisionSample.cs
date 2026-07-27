using MirzaBeig.Shaders.ImageEffects;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class CollisionSample : MonoBehaviour
{
   
    public Color shapeColor = new Color(0, 1, 0, 0.5f);
    public Color boundColor = new Color(0, 1, 0, 0.5f);
    private List<ShapeForGJK> shapes = new List<ShapeForGJK>();

    private ShapeForGJK BoundTop, BoundDown, Boundleft, BoundRight;



    public int shapeCount = 30;
    public int verticesCount = 10;
    public float minRange = 1.0f;
    public float maxRange = 2.0f;

    public Bounds bounds = new Bounds(Vector3.zero, new Vector3(20, 20, 0));

    public Vector3 gravity = new Vector3(0, -9.81f, 0f);
    public float friction = 0.5f;
    private Dictionary<ShapeForGJK, Vector3> shapeForces = new Dictionary<ShapeForGJK, Vector3>();
    private Dictionary<ShapeForGJK, Vector3> shapeVelocities = new Dictionary<ShapeForGJK, Vector3>();

    private Camera camera;


    public float explosionRadius = 10.0f;
    public float explosionForce = 50.0f;


    public TextMeshProUGUI infoText;
    public TextMeshProUGUI fps;

    int removeCount = 0;

    public float maxSpeed = 20.0f;
    [Min(1)]
    public int solverIterations = 5;

    // Start is called before the first frame update
    void Start()
    {
        camera = Camera.main;

        CreateShapes();
        InitializeBounds();
        InitializeForces();
    }

    // Update is called once per frame
    void Update()
    {
        if (fps != null)
            fps.text = $"FPS: {(int)(1.0f / Time.deltaTime)}";

        UpdatePosition();
        UpdateColor();

    }

    #region Initlizaion
    void CreateShapes()
    {
        int maxIterations = 30;

        for (int i = 0; i < shapeCount; i++)
        {
            Vector3 position = new Vector3(
                Random.Range(bounds.min.x + maxRange, bounds.max.x - maxRange),
                Random.Range(bounds.min.y + maxRange, bounds.max.y - maxRange),
                0
            );
            GameObject shapeObj = new GameObject("ShapeForGJK_" + i);
            shapeObj.transform.SetParent(this.transform);
            shapeObj.transform.position = position;

            ShapeForGJK shape = shapeObj.AddComponent<ShapeForGJK>();
            shape.expectVertexCount = verticesCount;
            shape.minRange = minRange;
            shape.maxRange = maxRange;
            shape.lineColor = shapeColor;

            //AddComponent<ShapeForGJK>() 后，Start() 不会立即执行,手动初始化
            shape.CreateConvexShape(verticesCount, minRange, maxRange);
            shapes.Add(shape);

        }

        for (int i = shapes.Count - 1; i >= 0; i--)
        {
            if (!ResovleOverlaps(shapes[i], maxIterations))
            {
                Debug.Log("Failed to resolve overlaps for shape " + shapes[i].name);
                Destroy(shapes[i].gameObject);
                shapes.RemoveAt(i);
                removeCount++;
            }
        }

        if (infoText != null)
        {
            infoText.text = $"Shape Count: {shapes.Count}\n" +
                $"Remove Count: {removeCount}\n" +
                $"Gravity: {gravity.y}\n" +
                $"Friction: {friction}\n" +
                $"Explosion Radius: {explosionRadius}\n" +
                $"Explosion Force: {explosionForce}\n";
            infoText.wordSpacing = 30;
            infoText.lineSpacing = 50;
        }

    }
    ShapeForGJK CreateBound(string name, List<Vector3> points)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(this.transform);
        obj.transform.position = Vector3.zero;
        ShapeForGJK shape = obj.AddComponent<ShapeForGJK>();
        shape.lineColor = boundColor;
        shape.SetManualPoints(points);
        return shape;

    }
    private bool ResovleOverlaps(ShapeForGJK shape, int maxIterations)
    {
        int iteration = 0;

        while (iteration < maxIterations)
        {
            bool hasOverlap = false;

            foreach (var otherShape in shapes)
            {
                if (shape == otherShape) continue;

                GJKAlgorithm.PolygonShape polyShape = new GJKAlgorithm.PolygonShape(shape.GetVerticesWorldPosition());
                GJKAlgorithm.PolygonShape otherPolyShape = new GJKAlgorithm.PolygonShape(otherShape.GetVerticesWorldPosition());

                if (GJKAlgorithm.GJK(polyShape, otherPolyShape, true))
                {
                    hasOverlap = true;
                    //Move shape to a new random position
                    Vector3 dir;
                    float depth;


                    if (GJKAlgorithm.EPA(polyShape, otherPolyShape, out dir, out depth, true))
                    {
                        Vector3 moveVector = dir.normalized * (depth + 0.05f);
                        shape.transform.position -= moveVector;
                    }
                    else
                    {
                        //If EPA fails, just move randomly
                        shape.transform.position = new Vector3(
                            Random.Range(bounds.min.x + maxRange, bounds.max.x - maxRange),
                            Random.Range(bounds.min.y + maxRange, bounds.max.y - maxRange),
                            0
                        );
                    }
                    break;
                }
            }

            if (!hasOverlap)
            {
                return true;
            }

            iteration++;
        }

        return false;
    }
    void InitializeBounds()
    {
        float thickness = 2.0f;
        float extraWidth = 5.0f;

        List<Vector3> topPoints = new List<Vector3>
        {
            new Vector3(bounds.min.x - extraWidth, bounds.max.y, 0),
            new Vector3(bounds.max.x + extraWidth, bounds.max.y, 0),
            new Vector3(bounds.max.x + extraWidth, bounds.max.y + thickness, 0),
            new Vector3(bounds.min.x - extraWidth, bounds.max.y + thickness, 0)
        };

        List<Vector3> downPoints = new List<Vector3>
        {
            new Vector3(bounds.min.x - extraWidth, bounds.min.y, 0),
            new Vector3(bounds.max.x + extraWidth, bounds.min.y, 0),
            new Vector3(bounds.max.x + extraWidth, bounds.min.y - thickness, 0),
            new Vector3(bounds.min.x - extraWidth, bounds.min.y - thickness, 0)
        };

        List<Vector3> leftPoints = new List<Vector3>
        {
            new Vector3(bounds.min.x, bounds.min.y - extraWidth, 0),
            new Vector3(bounds.min.x, bounds.max.y + extraWidth, 0),
            new Vector3(bounds.min.x - thickness, bounds.max.y + extraWidth, 0),
            new Vector3(bounds.min.x - thickness, bounds.min.y - extraWidth, 0)
        };

        List<Vector3> rightPoints = new List<Vector3>
        {
            new Vector3(bounds.max.x, bounds.min.y - extraWidth, 0),
            new Vector3(bounds.max.x, bounds.max.y + extraWidth, 0),
            new Vector3(bounds.max.x + thickness, bounds.max.y + extraWidth, 0),
            new Vector3(bounds.max.x + thickness, bounds.min.y - extraWidth, 0)
        };

        BoundTop = CreateBound("BoundTop", topPoints);
        BoundDown = CreateBound("BoundDown", downPoints);
        Boundleft = CreateBound("BoundLeft", leftPoints);
        BoundRight = CreateBound("BoundRight", rightPoints);
    }
    void InitializeForces()
    {
        foreach (var shape in shapes)
        {
            if (!shapeForces.ContainsKey(shape))
            {
                shapeForces[shape] = Random.insideUnitCircle.normalized * Random.Range(1.0f, 4.0f);
            }
        }
    }

    #endregion

    #region CollisionCheck
    void CheckCollisionPair(ShapeForGJK shapeA, ShapeForGJK shapeB)
    {
        // 简单的半径检测
        float distSq = (shapeA.transform.position - shapeB.transform.position).sqrMagnitude;
        float radiusSum = shapeA.maxRange + shapeB.maxRange;
        if (distSq > radiusSum * radiusSum) return;

        GJKAlgorithm.PolygonShape polyShapeA = new GJKAlgorithm.PolygonShape(shapeA.GetVerticesWorldPosition());
        GJKAlgorithm.PolygonShape polyShapeB = new GJKAlgorithm.PolygonShape(shapeB.GetVerticesWorldPosition());


        if (GJKAlgorithm.GJK(polyShapeA, polyShapeB, true))
        {
            Vector3 dir;
            float depth;


            if (GJKAlgorithm.EPA(polyShapeA, polyShapeB, out dir, out depth, true))
            {
               
                Vector3 normal = dir.normalized;

                // 双向分离
                float separationAmount = depth + 0.05f;
                Vector3 moveVector = normal * separationAmount * 0.5f;

                // A 向 -normal 移，B 向 +normal 移
                shapeA.transform.position -= moveVector;
                shapeB.transform.position += moveVector;

                if (shapeVelocities.ContainsKey(shapeA) && shapeVelocities.ContainsKey(shapeB))
                {
                    //冲量计算 I= m * Δv`

                    Vector3 v1 = shapeVelocities[shapeA];
                    Vector3 v2 = shapeVelocities[shapeB];

                    Vector3 relativeVelocity = v1 - v2;
                    float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

                    if (velocityAlongNormal < 0) return;

                    float e = 1.0f;
                    float jn = -(1 + e) * velocityAlongNormal * 0.5f;


                    float fric = friction;
                    Vector3 tangent = relativeVelocity - velocityAlongNormal * normal;
                    Vector3 jt = Vector3.zero;
                    if (tangent.sqrMagnitude > 0.00001f)
                    {
                        jt = -friction * tangent * 0.5f;
                    }

                    Vector3 impulse = (jn * normal + jt);

                    shapeVelocities[shapeA] += impulse;
                    shapeVelocities[shapeB] -= impulse;
                }
            }

        }
    }
    void CheckBoundCollision(ShapeForGJK shape, ShapeForGJK bound)
    {
        if (bound == null) return;

        GJKAlgorithm.PolygonShape polyShape = new GJKAlgorithm.PolygonShape(shape.GetVerticesWorldPosition());
        GJKAlgorithm.PolygonShape polyBound = new GJKAlgorithm.PolygonShape(bound.GetVerticesWorldPosition());


        if (GJKAlgorithm.GJK(polyShape, polyBound, true))
        {
            Vector3 dir;
            float depth;


            if (GJKAlgorithm.EPA(polyShape, polyBound, out dir, out depth, true))
            {
                Vector3 normal = dir.normalized;

                shape.transform.position -= normal * (depth + 0.01f);

                if (shapeVelocities.ContainsKey(shape))
                {
                    // 仅当物体朝向墙壁运动时才反弹 (normal 指向墙内)
                    if (Vector3.Dot(shapeVelocities[shape], normal) > 0)
                    {
                        shapeVelocities[shape] = Vector3.Reflect(shapeVelocities[shape], normal);
                        shapeVelocities[shape] *= 0.9f;
                    }
                }
            }
        }
    }

    #endregion

    #region Update
    Vector3 GetExplosionForce(ShapeForGJK shape, Vector3 position, float explosionRadius, float explosionForce)
    {

        Vector3 toShape = (shape.transform.position - position);
        toShape.z = 0;

        float distance = toShape.magnitude;

        if (distance > explosionRadius) return Vector3.zero;

        if (distance < 0.5f) distance = 0.5f;

        if (distance < 0.1f) distance = 0.1f;

        float strength = (1.0f - (distance / explosionRadius)) * explosionForce;

        return toShape.normalized * strength;


    }
    void UpdatePosition()
    {
        Vector3? explosionCenter = null;

        if (Input.GetMouseButton(0))
        {
            explosionCenter = camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -camera.transform.position.z));
        }


        foreach (var shape in shapes)
        {

            if (!shapeVelocities.ContainsKey(shape))
            {
                shapeVelocities[shape] = Vector3.zero;
            }

            Vector3 totalForce = shapeForces[shape] + gravity;

            if (explosionCenter.HasValue)
            {
                totalForce += GetExplosionForce(shape, explosionCenter.Value, explosionRadius, explosionForce);
            }

            Vector3 acc = totalForce;

            shapeVelocities[shape] += acc * Time.deltaTime;
            shapeVelocities[shape] = Vector3.ClampMagnitude(shapeVelocities[shape], maxSpeed);

            shape.transform.position += shapeVelocities[shape] * Time.deltaTime;
        }

        //use BVH to optimize collision detection
        BVHTreeGJK bvh = new BVHTreeGJK(shapes);

        List<(ShapeForGJK, ShapeForGJK)> potentialPairs = new List<(ShapeForGJK, ShapeForGJK)>();

        // 获取潜在碰撞对 使用 HashSet 避免重复对 (A,B) 和 (B,A)
        for (int i = 0; i < shapes.Count; i++)
        {
            ShapeForGJK shapeA = shapes[i];

            Vector3 pos = shapeA.transform.position;
            float r = shapeA.maxRange;
            AABBRegion queryRegion = new AABBRegion(pos.x - r, pos.y - r, pos.x + r, pos.y + r);

            List<ShapeForGJK> candinates = new List<ShapeForGJK>();
            bvh.Query(bvh.rootNode, queryRegion, candinates);
            foreach (var shapeB in candinates)
            {
                if (shapeA == shapeB) continue;

                if (shapeA.GetInstanceID() < shapeB.GetInstanceID())
                    potentialPairs.Add((shapeA, shapeB));
            }
        }

        // 2. Solver Iterations
        for (int k = 0; k < solverIterations; k++)
        {
           
            foreach (var pair in potentialPairs)
            {
                CheckCollisionPair(pair.Item1, pair.Item2);
            }

            foreach (var shape in shapes)
            {
                CheckBoundCollision(shape, BoundTop);
                CheckBoundCollision(shape, BoundDown);
                CheckBoundCollision(shape, Boundleft);
                CheckBoundCollision(shape, BoundRight);
            }
        }

        // 3. Final Clamp: 
        foreach (var shape in shapes)
        {
           
            Vector3 pos = shape.transform.position;
            float r = shape.maxRange;


            float minX = bounds.min.x + r;
            float maxX = bounds.max.x - r;
            float minY = bounds.min.y + r;
            float maxY = bounds.max.y - r;

            if (pos.x < minX) { pos.x = minX; var v = shapeVelocities[shape]; v.x *= -1.0f; shapeVelocities[shape] = v; }
            if (pos.x > maxX) { pos.x = maxX; var v = shapeVelocities[shape]; v.x *= -1.0f; shapeVelocities[shape] = v; }
            if (pos.y < minY) { pos.y = minY; var v = shapeVelocities[shape]; v.y *= -1.0f; shapeVelocities[shape] = v; }
            if (pos.y > maxY) { pos.y = maxY; var v = shapeVelocities[shape]; v.y *= -1.0f; shapeVelocities[shape] = v; }

            shape.transform.position = pos;
        }


    }

    void UpdateColor()
    {

        foreach (var shape in shapes)
        {
            if (shapeVelocities.ContainsKey(shape))
            {
                float speed = shapeVelocities[shape].magnitude;
                float t = Mathf.Clamp01(speed / maxSpeed);
                Color color = Color.Lerp(shapeColor, Color.red, t);
                shape.lineColor = color;
            }
        }
    }
    #endregion
    private void OnDrawGizmos()
    {
        //Draw bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

    }

}
