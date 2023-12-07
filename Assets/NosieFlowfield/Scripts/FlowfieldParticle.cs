using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowfieldParticle : MonoBehaviour
{

    public float _moveSpeed;
    public int _audioClip;
    void Start()
    {
        
    }

    
    void Update()
    {
        this.transform.position += transform.forward * _moveSpeed * Time.deltaTime;
    }

    public void ApplyRotation(Vector3 rotation, float rotationSpeed)
    {
        Quaternion targetRotation = Quaternion.LookRotation(rotation.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
}
