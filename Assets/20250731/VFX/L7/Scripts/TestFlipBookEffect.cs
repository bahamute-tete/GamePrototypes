using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;


[Serializable]
public class MaterialsSetting
{
    public Material material_L;
    public Material material_R;
}
public class TestFlipBookEffect : MonoBehaviour
{
    public int count = 1;
    public float gap = 0.5f;
    public GameObject screenWallPrefab;
    public List<GameObject> screenWalls = new List<GameObject>();
    public List<MaterialsSetting> settings = new List<MaterialsSetting>();

    
    [Header("门控制设置")]
    [SerializeField] private List<DoorController> doorControllers = new List<DoorController>();
    public float maxDistanceThreshold = 10f;    // 最大距离阈值
    public float minDistanceThreshold = 2f;     // 最小距离阈值
    public bool useSmoothDoorControl = true;    // 是否使用平滑控制
    public float smoothSpeed = 5f;              // 平滑速度
    public AnimationCurve animationCurve_AppearanceDuration = AnimationCurve.EaseInOut(0, 0, 1, 1);


    private Camera playerCamera;
    

    void Start()
    {
        GeneratScreenWall(count, gap);
        

        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = FindObjectOfType<Camera>();
            
        GetDoorController();

        ScreenWallMaterialSet(settings);

        AppearanceDoor();
    }


    void Update()
    {
        if (doorControllers.Count > 0 && playerCamera != null)
        {
            UpdateDoorsBasedOnDistance();
        }
    }

    void GeneratScreenWall(int count ,float gap)
    { 
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(screenWallPrefab);
            go.name = "ScreenWall_" + i;
            go.transform.SetParent(this.transform);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localPosition = new Vector3(0, 0, -i * gap);
            go.transform.localScale = new Vector3(1, 1, 1)*1.7f;
            screenWalls.Add(go);
        }
    }

    void ScreenWallMaterialSet(List<MaterialsSetting> settings)
    {
        if (settings.Count != count) return;

        for (int i = 0; i < count; i++)
        {
            if (doorControllers[i] != null)
            {
                doorControllers[i].doorMaterial_L = settings[i].material_L;
                doorControllers[i].doorMaterial_R = settings[i].material_R;
            }

        }
        
    }
    void AppearanceDoor()
    {
        for (int i = 0; i < doorControllers.Count && i < screenWalls.Count; i++)
        {
            if (doorControllers[i] != null && screenWalls[i] != null)
            {
                GameObject wall = screenWalls[i];
                DoorController door = doorControllers[i];

                float t = animationCurve_AppearanceDuration.Evaluate((float)i / count);
                door.animationDuration_Appearance = Mathf.Lerp(5f, 1f, t);

                door.ToggleShowDoor();
            }


        }
    }

    void UpdateDoorsBasedOnDistance()
    {

        for (int i = 0; i < doorControllers.Count && i < screenWalls.Count; i++)
        {
            if (doorControllers[i] != null && screenWalls[i] != null)
            {
                GameObject wall = screenWalls[i];
                DoorController door = doorControllers[i];
                
                float distance = Vector3.Distance(playerCamera.transform.position, wall.transform.position);
                
                bool isPlayerInFront = IsPlayerInFrontOfWall(wall, playerCamera.transform.position);
                
                float normalizedDistance;
                
                if (isPlayerInFront && distance <= maxDistanceThreshold)
                {
                    // 归一化距离
                    normalizedDistance = Mathf.Clamp01((distance - minDistanceThreshold) / (maxDistanceThreshold - minDistanceThreshold));
                }
                else
                {
                    // 门关闭
                    normalizedDistance = 1f;
                }
                
                if (useSmoothDoorControl)
                {
                    door.SetDoorAngleByDistanceSmooth(normalizedDistance, smoothSpeed);
                    door.SetGlowThreshold(1.0f-normalizedDistance);
                }
                else
                {
                    door.SetDoorAngleByDistance(normalizedDistance);
                    door.SetGlowThreshold(1.0f-normalizedDistance);
                }
            }
        }
    }
    

    bool IsPlayerInFrontOfWall(GameObject wall, Vector3 playerPosition)
    {
        Vector3 wallForward = wall.transform.right;
        Vector3 wallToPlayer = (playerPosition - wall.transform.position).normalized;
        float dot = Vector3.Dot(wallForward, wallToPlayer);
        return dot > 0f;
    }
    
    void GetDoorController()
    {
        doorControllers.Clear();
        
        foreach (GameObject wall in screenWalls)
        {
            if (wall != null)
            {
                DoorController door = wall.GetComponent<DoorController>();

                if (door != null)
                {
                    doorControllers.Add(door);
                    //Debug.Log($"找到DoorController在: {door.gameObject.name}");
                }
                else
                {
                    // 如果没找到，添加null占位，保持索引对应
                    doorControllers.Add(null);
                    //Debug.LogWarning($"在{wall.name}中未找到DoorController组件");
                }
            }
        }

    }
}
