using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CACameraControll : MonoBehaviour
{
    [Header("CameraControl")]
    [SerializeField]private Camera mainCamera;
    [SerializeField]private KeyCode resetKey = KeyCode.Mouse2;
    [SerializeField]private KeyCode moveKey = KeyCode.Mouse1;

    [Header("Settings")]
    [SerializeField] private float dragSpeed = 20f;
    [SerializeField] private float zoomSpeed = 20f;
    [SerializeField] private float minHeight = 2f;
    [SerializeField] private float maxHeight = 200f;

    private Vector3 initialPosition;
   

    // Start is called before the first frame update
    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("No camera found! Please assign a camera.");
                return;
            }
        }

        initialPosition = mainCamera.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        
        CameraMovement();
        CameraPositionRest();
        CameraZoom();
    }

    private void CameraPositionRest()
    {
        if (Input.GetKeyDown(resetKey))
        {
            mainCamera.transform.position = initialPosition;
        }
    }

    private void CameraMovement()
    {
        if (Input.GetKey(moveKey))
        {
            float h = Input.GetAxis("Mouse X");
            float v = Input.GetAxis("Mouse Y");

            
            Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.ProjectOnPlane(mainCamera.transform.up, Vector3.up);
            }
            Vector3 right = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;


            Vector3 moveDir = -(right * h + forward * v);

           
            float heightFactor = Mathf.Max(1f, mainCamera.transform.position.y / 10f);
            
            mainCamera.transform.position += moveDir * dragSpeed * heightFactor * Time.deltaTime;
        }
    }

    private void CameraZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            Vector3 currentPos = mainCamera.transform.position;

            Vector3 targetPos = currentPos + mainCamera.transform.forward * scroll * zoomSpeed;

            mainCamera.transform.position = targetPos;
        }
    }
}
