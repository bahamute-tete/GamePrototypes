using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class CubeMouseDragBridge : MonoBehaviour
{

    [Header("References")]
    [SerializeField]private Camera mainCamera;
    [Header("Plane")]
    [SerializeField]private float planeDepth = 0f;

    private World world;
    private EntityManager entityManager;
    private Entity dragInputEntity;
    private Plane plane;


    // Start is called before the first frame update
    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        world = World.DefaultGameObjectInjectionWorld;
        if (world == null||!world.IsCreated)
            return;

        entityManager = world.EntityManager;
        dragInputEntity = entityManager.CreateEntity(typeof(CubeDragInput));
        entityManager.SetName(dragInputEntity, "CubeDragInput");

        entityManager.SetComponentData(dragInputEntity, new CubeDragInput
        {
            TrargetPos = float3.zero,
            IsDragging = 0f,
            CurrentOffset = float3.zero
        });

        plane = new Plane(Vector3.back, new Vector3(0f, 0, planeDepth));
    }

    // Update is called once per frame
    void Update()
    {
        if (Mouse.current == null)
            return;


        bool isDragging = Mouse.current.leftButton.isPressed;

        var dragInput =entityManager.GetComponentData<CubeDragInput>(dragInputEntity);

        dragInput.IsDragging = isDragging ? 1f : 0f;


        if (isDragging && TryGetMouseWorldPosition(out float3 mouseWorldPosition))
        {
            dragInput.TrargetPos = mouseWorldPosition;
            //Debug.Log($"Mouse World Position: {mouseWorldPosition}");
        }

        entityManager.SetComponentData(dragInputEntity, dragInput);
    }

    private bool TryGetMouseWorldPosition(out float3 mouseWorldPosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (plane.Raycast(ray, out float enter))
        {
            mouseWorldPosition = ray.GetPoint(enter);
            return true;
        }

        mouseWorldPosition = float3.zero;
        return false;
    }

    private void OnDestroy()
    {
        if (world==null||!world.IsCreated)
            return;

        if (dragInputEntity == Entity.Null)
            return;

        var manager = world.EntityManager;

        if (manager != null && manager.Exists(dragInputEntity))
        {
            manager.DestroyEntity(dragInputEntity);
        }
    }


    private void OnDrawGizmos()
    {
        if (Mouse.current == null || mainCamera == null)
            return;
        if (TryGetMouseWorldPosition(out float3 mouseWorldPosition))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(mouseWorldPosition, 1.0f);
        }
    }


}



