using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Time = UnityEngine.Time;

public class RayCastWall : MonoBehaviour
{
    [SerializeField] private float rayLength = 100f;
    [SerializeField] private LayerMask raycastLayerMask = -1;
    
    // 鼠标交互相关
    private Camera mainCamera;
    [Header("鼠标交互")]
    [SerializeField] private bool requireMouseHold = true; // 是否需要按住鼠标
    
    [HideInInspector] public float animationDuration = 0.5f;
    [HideInInspector] public float moveDistance = 10.0f;
    [Header("颜色")]
    [SerializeField, ColorUsage(true, true)] private Color exitColor = Color.white;
    [SerializeField, ColorUsage(true, true)] private Color hitColor = Color.green;
    [Header("调试")]
    [SerializeField] private bool drawRay = true;
    [SerializeField] private Color rayColor = Color.red;

    public MoveableTypePrintingAttributes attributes;
    private Ray ray;
    private GameObject lastHitObject;
    private bool isHitting = false;
    private Dictionary<string, Coroutine> colorTransitionCoroutines = new Dictionary<string, Coroutine>();

    // 移除VR相关变量，保留效果相关变量
    private Transform pointEff;
    
    RaycastHit hit;

    public float maxHoldTime = 2f;
    public float cdTime = 2.5f;

    
    public AudioSource audioSource;
    public AudioClip clip;

    private float leftTime = 0f;
    private bool canInteracble = true;
    private bool isAutoShow = false;
    private List<GameObject> childWords;

    // 鼠标交互相关变量
    private bool isMousePressed = false;
    private float mouseHoldTime = 0f;

    private void Awake()
    {
        attributes = GetComponent<MoveableTypePrintingAttributes>();
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }
    }

    void Start()
    {
        if (attributes == null)
        {
            Debug.LogError("未找到 MoveableTypePrintingAttributes!");
            return;
        }
        
        StartCoroutine(DelayedInitialization());
    }

    // 延迟初始化协程
    private IEnumerator DelayedInitialization()
    {
        yield return null;

        // 确保有分类数据后再初始化颜色
        int retryCount = 0;
        int maxRetries = 10;

        while (attributes.GetAllCategories().Count == 0 && retryCount < maxRetries)
        {
            yield return new WaitForSeconds(0.1f);
            retryCount++;
        }

        if (attributes.GetAllCategories().Count > 0)
        {
            InitializeAllObjectsGlowColor();
            Debug.Log("已初始化所有物体的颜色：" + attributes.GetAllCategories().Count + "个分类");
        }
        else
        {
            Debug.LogWarning("等待分类数据超时，无法初始化颜色");
        }
    }

    // 初始化所有物体的 glowColor
    private void InitializeAllObjectsGlowColor()
    {
        if (attributes != null)
        {
            List<string> allCategories = attributes.GetAllCategories();
            foreach (string category in allCategories)
            {
                attributes.SetCategoryGlowColor(category, exitColor);
            }
        }
    }

    void Update()
    {
        if (canInteracble)
        {
           
            HandleMouseInteraction();
            
            
            leftTime -= Time.deltaTime;
            if (leftTime <= 0)
            {
                leftTime = 0;
            }
        }
    }

    //// 自动展示逻辑（保持原有逻辑不变）
    //private int autoShowRate = 3;
    //private float autoShowTimeTemp = 0;
    //private int autoHideRate = 4;
    //private float autoHideTimeTemp = 0;
    private float timeCount = 0;



    //private void AutoHideWords()
    //{
    //    if (isHitting && lastHitObject != null)
    //    {
    //        autoShowTimeTemp = 0;
    //        string categoryName = attributes.GetCategoryNamesByObject(lastHitObject);
    //        attributes.HandleRayExit(lastHitObject, animationDuration);
    //        StartColorTransition(categoryName, hitColor, exitColor, animationDuration);
    //        lastHitObject = null;
    //        isHitting = false;
    //    }
    //}

    // 鼠标交互处理
    private void HandleMouseInteraction()
    {
        if (mainCamera == null) return;

        // 检测鼠标输入
        bool mouseDown = Input.GetMouseButtonDown(0);
        bool mouseHeld = Input.GetMouseButton(0);
        bool mouseUp = Input.GetMouseButtonUp(0);

        // 更新鼠标按下状态
        if (mouseDown)
        {
            isMousePressed = true;
            mouseHoldTime = 0f;
        }
        else if (mouseUp)
        {
            isMousePressed = false;
            mouseHoldTime = 0f;
            
            // 鼠标松开时处理射线离开
            HandleRayExit();
        }

        // 如果需要按住鼠标且鼠标没按下，则不处理射线
        if (requireMouseHold && !mouseHeld)
        {
            return;
        }

        // 更新鼠标按住时间
        if (isMousePressed)
        {
            mouseHoldTime += Time.deltaTime;
        }

        // 创建从鼠标位置到世界的射线
        Vector3 mousePosition = Input.mousePosition;
        ray = mainCamera.ScreenPointToRay(mousePosition);

        // 射线检测
        if (Physics.Raycast(ray, out hit, rayLength, raycastLayerMask))
        {
            HandleRayHit(hit.collider.gameObject);
        }
        else
        {
            HandleRayExit();
        }

        if (drawRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * rayLength, rayColor);
        }
    }

    // 处理射线击中
    private void HandleRayHit(GameObject hitObject)
    {
        if (lastHitObject != hitObject)
        {
            timeCount += Time.deltaTime;

            // 检查持续时间和冷却时间
            if (timeCount <= maxHoldTime || leftTime > 0)
            {
                return;
            }

            string newCategoryName = attributes.GetCategoryNamesByObject(hitObject);
            string lastCategoryName = lastHitObject != null ? attributes.GetCategoryNamesByObject(lastHitObject) : null;

            if (lastCategoryName != newCategoryName)
            {
                if (lastHitObject != null)
                {
                    attributes.HandleRayExit(lastHitObject, animationDuration);
                    StartColorTransition(lastCategoryName, hitColor, exitColor, animationDuration);
                }

                // 播放音效
                if (audioSource != null && clip != null)
                {
                    audioSource.Stop();
                    audioSource.PlayOneShot(clip);
                }

                // 创建点击效果（如果有pointEff预制体）
                if (pointEff != null)
                {
                    GameObject pointEffClone = Instantiate(pointEff.gameObject);
                    pointEffClone.transform.parent = hitObject.transform;
                    pointEffClone.transform.localPosition = new Vector3(4.5f, 0, 0);
                    pointEffClone.transform.localEulerAngles = new Vector3(0, -90, 0);
                    pointEffClone.SetActive(true);
                    Destroy(pointEffClone, 1.5f);
                }

                attributes.HandleRayHit(hitObject, animationDuration, moveDistance);
                StartColorTransition(newCategoryName, exitColor, hitColor, animationDuration);
            }

            lastHitObject = hitObject;
            //Debug.Log($"lastHitObject={lastHitObject}");
            isHitting = true;
            timeCount = 0;
            leftTime = cdTime;
        }
    }

    // 处理射线离开
    private void HandleRayExit()
    {
        if (isHitting && lastHitObject != null)
        {
            string categoryName = attributes.GetCategoryNamesByObject(lastHitObject);
            attributes.HandleRayExit(lastHitObject, animationDuration);
            StartColorTransition(categoryName, hitColor, exitColor, animationDuration);
            lastHitObject = null;
            isHitting = false;
        }
    }

    // 启动颜色过渡协程
    private void StartColorTransition(string categoryName, Color fromColor, Color toColor, float duration)
    {
        if (string.IsNullOrEmpty(categoryName)) return;

        if (colorTransitionCoroutines.TryGetValue(categoryName, out Coroutine coroutine))
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
            colorTransitionCoroutines.Remove(categoryName);
        }

        Coroutine newCoroutine = StartCoroutine(ColorTransitionCoroutine(categoryName, fromColor, toColor, duration));
        colorTransitionCoroutines[categoryName] = newCoroutine;
    }

    // 颜色过渡协程
    private IEnumerator ColorTransitionCoroutine(string categoryName, Color fromColor, Color toColor, float duration)
    {
        float startTime = Time.time;

        while (Time.time - startTime < duration)
        {
            float t = (Time.time - startTime) / duration;
            Color currentColor = Color.Lerp(fromColor, toColor, t);
            attributes.SetCategoryGlowColor(categoryName, currentColor);
            yield return null;
        }

        attributes.SetCategoryGlowColor(categoryName, toColor);
        colorTransitionCoroutines.Remove(categoryName);
    }

    private void OnDrawGizmos()
    {
        if (drawRay && mainCamera != null)
        {
            Gizmos.color = rayColor;
            Vector3 mousePos = Input.mousePosition;
            Ray gizmoRay = mainCamera.ScreenPointToRay(mousePos);
            Gizmos.DrawRay(gizmoRay.origin, gizmoRay.direction * rayLength);
        }
    }
}
