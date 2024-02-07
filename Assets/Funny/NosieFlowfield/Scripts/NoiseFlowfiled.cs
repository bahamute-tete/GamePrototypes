using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class NoiseFlowfiled : MonoBehaviour
{
    FastNoise _FastNoise;

    public Vector3Int _GridSize;
    public float cellSize;
    public Vector3[,,] _flowfieldDirection;
    public float _increment;

    public Vector3 _offset, _offsetSpeed;

    //particles
    public GameObject _particlePrefab;
    public int _amountOfParticles;
     List<FlowfieldParticle> _particles;
    public float _spawnRadius;
    public float _particleScale,_particleMoveSpeed,_particleRotateSpeed;

    bool _particlesSpawnValidation(Vector3 position)
    {
        bool valid = true;
        foreach (var particle in _particles)
        {
            if (Vector3.Distance(position, particle.transform.position) < _spawnRadius)
            {
                valid = false;
            }
        }

        if (valid)
        {
             return true;
        }
        else
        { 
             return false;
            
        }

    }
    // Start is called before the first frame update
    void Start()
    {
        _FastNoise = new FastNoise();
        _flowfieldDirection = new Vector3[_GridSize.x, _GridSize.y, _GridSize.z];
        _particles = new List<FlowfieldParticle>();

        for (int i = 0; i < _amountOfParticles; i++)
        {
            int attempt = 0;

            while (attempt < 100)
            {
                Vector3 randomPos = new Vector3(
                Random.Range(this.transform.position.x, this.transform.position.x + _GridSize.x * cellSize),
                Random.Range(this.transform.position.y, this.transform.position.y + _GridSize.y * cellSize),
                Random.Range(this.transform.position.z, this.transform.position.z + _GridSize.z * cellSize));

                bool isValid = _particlesSpawnValidation(randomPos);

                if (isValid)
                {
                    GameObject particlesInstance = Instantiate(_particlePrefab);
                    particlesInstance.transform.position = randomPos;
                    particlesInstance.transform.parent = this.transform;
                    particlesInstance.transform.localScale = new Vector3(_particleScale, _particleScale, _particleScale);

                    _particles.Add(particlesInstance.GetComponent<FlowfieldParticle>());

                    break;

                }

                if (!isValid)
                {
                    attempt++;
                }

            }
            


        }
    }

    // Update is called once per frame
    void Update()
    {
        CalculateFlowfieldDirections();
        ParticleBehaviour();
    }

    void CalculateFlowfieldDirections()
    {
        _offset = new Vector3(_offset.x + (_offsetSpeed.x * Time.deltaTime), _offset.y + (_offsetSpeed.y * Time.deltaTime), _offset.z + (_offsetSpeed.z * Time.deltaTime));
        float xOff = 0f;
        for (int x = 0; x < _GridSize.x; x++)
        {
            float yOff = 0f;
            for (int y = 0; y < _GridSize.y; y++)
            {
                float zOff = 0f;
                for (int z = 0; z < _GridSize.z; z++)
                {
                    float noise = _FastNoise.GetSimplex(xOff + _offset.x, yOff + _offset.y, zOff + _offset.z) + 1;
                    Vector3 noiseDirection = new Vector3(Mathf.Cos(noise * Mathf.PI), Mathf.Sin(noise * Mathf.PI), Mathf.Cos(noise * Mathf.PI));
                    _flowfieldDirection[x,y,z] = Vector3.Normalize(noiseDirection);

                    zOff += _increment;
                }
                yOff += _increment;
            }
            xOff += _increment;
        }
    }

    void ParticleBehaviour()
    {
        foreach (var p in _particles)
        {

            //XEdge
            if (p.transform.position.x > this.transform.position.x + (_GridSize.x * cellSize))
            {
                p.transform.position = new Vector3(this.transform.position.x, p.transform.position.y, p.transform.position.z);
            }

            if (p.transform.position.x < this.transform.position.x)
            {
                p.transform.position = new Vector3(this.transform.position.x + (_GridSize.x * cellSize), p.transform.position.y, p.transform.position.z);
            }

            //YEdge
            if (p.transform.position.y > this.transform.position.y + (_GridSize.y * cellSize))
            {
                p.transform.position = new Vector3(p.transform.position.x, this.transform.position.y, p.transform.position.z);
            }

            if (p.transform.position.y < this.transform.position.y)
            {
                p.transform.position = new Vector3(p.transform.position.x, this.transform.position.y + (_GridSize.y * cellSize), p.transform.position.z);
            }

            //ZEdge
            if (p.transform.position.z > this.transform.position.z + (_GridSize.z * cellSize))
            {
                p.transform.position = new Vector3(p.transform.position.x, p.transform.position.y, this.transform.position.z);
            }

            if (p.transform.position.z < this.transform.position.z)
            {
                p.transform.position = new Vector3(p.transform.position.x, p.transform.position.y, this.transform.position.z + (_GridSize.z * cellSize));
            }



            Vector3Int particlePos = new Vector3Int(
                Mathf.FloorToInt(Mathf.Clamp((p.transform.position.x - this.transform.position.x) / cellSize, 0, _GridSize.x - 1)),
                Mathf.FloorToInt(Mathf.Clamp((p.transform.position.y - this.transform.position.y) / cellSize, 0, _GridSize.y - 1)),
                Mathf.FloorToInt(Mathf.Clamp((p.transform.position.z - this.transform.position.z) / cellSize, 0, _GridSize.z - 1))
                );

            p.ApplyRotation(_flowfieldDirection[particlePos.x, particlePos.y, particlePos.z], _particleRotateSpeed);
            p._moveSpeed = _particleMoveSpeed;
            p.transform.localScale = new Vector3(_particleScale, _particleScale, _particleScale);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(this.transform.position+new Vector3((_GridSize.x*cellSize)*0.5f, (_GridSize.y * cellSize) * 0.5f, (_GridSize.z * cellSize) * 0.5f),
            new Vector3(_GridSize.x*cellSize,_GridSize.y*cellSize,_GridSize.z*cellSize));
    }
}
