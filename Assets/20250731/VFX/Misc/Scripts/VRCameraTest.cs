using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera)),ExecuteInEditMode]
public class VRCameraTest : MonoBehaviour
{
    
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minVerticalAngle = -90f;
    [SerializeField] private float maxVerticalAngle = 90f;
    
    private float rotationX = 0f;
    
    void Start()
    {
        
    }

    void Update()
    {
        if (Input.GetMouseButton(1)) 
        {
          
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            transform.Rotate(Vector3.up * mouseX);
            
            rotationX -= mouseY;
            rotationX = Mathf.Clamp(rotationX, minVerticalAngle, maxVerticalAngle);
            

            Vector3 rotation = transform.eulerAngles;
            rotation.x = rotationX;
            transform.eulerAngles = rotation;
        }
    }
}
