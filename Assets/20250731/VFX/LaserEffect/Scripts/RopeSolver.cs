using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public enum IntegrationMethod
{
    StandardVerlet,
    VelocityVerlet
}


[System.Serializable]
public class LineBaseSetting
{
    public Color lineColor = Color.white;
    public List<Vector3> initialPos = new List<Vector3> ();
    public AnimationCurve lineWidth = AnimationCurve.Linear(0.0f,1.0f,1.0f,1.0f);
    public float lineLength = 10.0f;
    public int massPointCount = 10;
    public Material mat;


    private void CreatePos()
    {
        initialPos.Clear();

        for (int i = 0; i < massPointCount; i++)
        {
            float restLength = lineLength / massPointCount;
            Vector3 pos = new Vector3(i * restLength, 0, 0);
            initialPos.Add(pos);
        }
    }
    public void CreateLine(LineRenderer lr)
    {
        CreatePos();

        if (initialPos.Count <= 1) return;

        lr.positionCount = initialPos.Count;

        lr.SetPositions(initialPos.ToArray());
        lr.widthCurve = lineWidth;

        if (mat == null)
        { 
            Material material = new Material(Shader.Find("Unlit/Color"));
            material.SetColor("_Color", lineColor);
            lr.material = material;
        }else
        {
            lr.material = mat;
        }

        

    }
}


[RequireComponent(typeof(LineRenderer))]
public class RopeSolver : MonoBehaviour
{

    [System.Serializable]
    public class Particle
    {
        public Vector3 pos;
        public Vector3 vel;
        public Vector3 force;
        public float mass = 1.0f;
        public bool isFixed = false;
    }
    
    public LineBaseSetting line = new LineBaseSetting();
    private LineRenderer lr => GetComponent<LineRenderer>();



    private Particle[] particles;
    private float[] restLength;
    private Vector3[] previousPos;

    [Header("Integration Settings")]
    public IntegrationMethod integrationMethod = IntegrationMethod.VelocityVerlet;

    [Header("Spring Paramters")]
    public List<int> fixedPointIds = new List<int>();
    public float k = 100.0f;  
    public float massPerPoint = 0.1f;  
    public float damping = 2.0f;  
    public Vector3 gravity = new Vector3(0, -2.0f, 0); 

    [Header("Wind Parameters")]
    public bool enableWind = true; 
    public Vector3 windDirection = new Vector3(1, 0, 0); 
    public float windStrength = 3.0f; 
    public float windVariation = 1.5f; // 风力变化幅度
    public float windFrequency = 0.5f; 
    public AnimationCurve windPattern = AnimationCurve.EaseInOut(0, 1, 1, 0); // 风力模式


    // Start is called before the first frame update
    void Start()
    {
        InitialLineRender();
        line.CreateLine(lr);

        InitialParticles(particles, restLength);

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        ApplyGravity();
        ApplySpringForce();
        ApplyDamping();
        
        if (enableWind)
        {
            ApplyWindForce();
        }
        
        if (integrationMethod == IntegrationMethod.StandardVerlet)
        {
            VerletIntegrate();
        }
        else
        {
            VelocityVerletIntegrate();
        }
        
        UpdateLineRenderer();
        
        // 重置力
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].force = Vector3.zero;
        }
    }

    void InitialLineRender()
    {
        if (lr != null)
            lr.positionCount = 0;
    }

    void InitialParticles(Particle[] particles, float[] restLength)
    {
        this.particles = new Particle[line.initialPos.Count];
        this.restLength = new float[line.initialPos.Count - 1];
        this.previousPos = new Vector3[line.initialPos.Count];

        for (int i = 0; i < this.particles.Length; i++)
        {
            this.particles[i] = new Particle();
            this.particles[i].pos = line.initialPos[i];
            this.particles[i].vel = Vector3.zero;
            this.particles[i].force = Vector3.zero;
            this.particles[i].mass = massPerPoint;

            if (fixedPointIds.Count > 0)
            {             
                this.particles[i].isFixed = fixedPointIds.Contains(i);
            }
            
            this.previousPos[i] = line.initialPos[i];
        }

        for (int i = 0; i < this.restLength.Length; i++)
        {
            this.restLength[i] = Vector3.Distance(this.particles[i].pos, this.particles[i + 1].pos);
        }
    }


    void ApplySpringForce()
    {
        for (int i = 0; i < particles.Length - 1; i++)
        {
            Vector3 currentDir = particles[i + 1].pos - particles[i].pos;
            float currentDistance = currentDir.magnitude;

            if (currentDistance > 0.001f)
            {
                Vector3 spingforce = k * (currentDistance - restLength[i]) * currentDir.normalized;
                particles[i].force += spingforce;
                particles[i + 1].force -= spingforce;
            }
        }
    }


    void ApplyGravity()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].isFixed)
            {
                Vector3 g = gravity * particles[i].mass;
                particles[i].force += g;
            }

        }
    }


    void ApplyDamping()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].isFixed)
            {
                Vector3 dampingForce = -damping * particles[i].vel;
                particles[i].force += dampingForce;
            }
        }
    }

    void ApplyWindForce()
    {
        float time = Time.time;
        
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].isFixed)
            {
                Vector3 baseWind = windDirection.normalized * windStrength;
                
                float windNoise = Mathf.PerlinNoise(time * windFrequency, i);
                float windMultiplier = 1.0f + (windNoise - 0.5f) * windVariation;
                windMultiplier *= windPattern.Evaluate((time * windFrequency) % 1.0f);
                
                float distanceFactor = (float)i / (particles.Length - 1);
                windMultiplier *= (0.2f + 0.8f * distanceFactor);
                
                Vector3 finalWind = baseWind * windMultiplier * particles[i].mass;
                particles[i].force += finalWind;
            }
        }
    }

    void VerletIntegrate()
    {
        float dt = Time.deltaTime;
        
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].isFixed)
            {
                Vector3 acceleration = particles[i].force / particles[i].mass;
                
                //x(t+dt) = 2*x(t) - x(t-dt) + a(t)*dt^2
              
                Vector3 newPos = 2.0f * particles[i].pos - previousPos[i] + acceleration * dt * dt;
                
                previousPos[i] = particles[i].pos;
                
                particles[i].pos = newPos;

                //v(t+dt) =x(t)-x(t-dt))/2*dt
                particles[i].vel = (particles[i].pos - previousPos[i]) / (2*dt);

              
            }
        }
    }

    void VelocityVerletIntegrate()
    {
        float dt = Time.fixedDeltaTime;
        
        // 存储当前加速度
        Vector3[] currentAccelerations = new Vector3[particles.Length];
        for (int i = 0; i < particles.Length; i++)
        {
            currentAccelerations[i] = particles[i].isFixed ? Vector3.zero : particles[i].force / particles[i].mass;
        }
        
        // 更新位置: x(t+dt) = x(t) + v(t)*dt + 0.5*a(t)*dt^2
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].isFixed)
            {
                particles[i].pos = particles[i].pos + particles[i].vel * dt + 0.5f * currentAccelerations[i] * dt * dt;
            }
        }
        
        // 重新计算力和新加速度
        // 先重置力
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].force = Vector3.zero;
        }
        
        // 重新应用所有力
        ApplyGravity();
        ApplySpringForce();
        if (enableWind)
        { 
            ApplyWindForce();
        }
        ApplyDamping();
        
        // 计算新加速度并更新速度: v(t+dt) = v(t) + 0.5*(a(t) + a(t+dt))*dt
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].isFixed)
            {
                Vector3 newAcceleration = particles[i].force / particles[i].mass;
                particles[i].vel = particles[i].vel + 0.5f * (currentAccelerations[i] + newAcceleration) * dt;
            }
        }
    }

    void UpdateLineRenderer()
    {
        if (lr != null && particles != null)
        {
            Vector3[] positions = new Vector3[particles.Length];
            for (int i = 0; i < particles.Length; i++)
            {
                positions[i] = particles[i].pos;
            }
            lr.SetPositions(positions);
        }
    }
}
