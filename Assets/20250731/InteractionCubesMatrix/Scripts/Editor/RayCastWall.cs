using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Time = UnityEngine.Time;


public class RayCastWall : MonoBehaviour
{
    [SerializeField] private float rayLength = 100f;
    [SerializeField] private LayerMask raycastLayerMask = -1;
    public Transform rightHand;
    private Transform rightHandRayTrans;

    [HideInInspector] public float animationDuration = 0.5f;
    [HideInInspector] public float moveDistance = 0.2f;
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

    private Transform rayLineEff;
    private Animator rayLineAni;
    private int rayLineBool;
    private Transform pointEff;
    private Transform mainCamera;
    public float maxDistance = 2.5f;
    public float minDistance = 0.05f;
    public float maxAngle = 30;

    private bool canCheck = false;
    RaycastHit hit;

    public float maxHoldTime = 2f;

    public float cdTime = 2.5f;

    [HideInInspector]
    public AudioSource audioSource;
    [HideInInspector]
    public AudioClip clip;

    private float leftTime = 0f;

    private bool canInteracble = true;

    private Vector3 handOriginPos = new Vector3(0, 0, 0.0475f);

    private bool isRayAniShow = false;

    private bool isAutoShow = false;
    private List<GameObject> childWords;

    private void Awake()
    {
        attributes = GetComponent<MoveableTypePrintingAttributes>();

    }

    //public void SetInteracble(bool interacble)
    //{
    //    canInteracble = interacble;

    //    if (rightHand.Find("Hand_R/right_wrist/right_palm/rayEff") != null)
    //    {
    //        rightHand.Find("Hand_R/right_wrist/right_palm/rayEff").gameObject.SetActive(interacble);
    //    }
    //    if (rightHand.Find("rayEff") != null)
    //    {
    //        rightHand.Find("rayEff").gameObject.SetActive(interacble);
    //    }
    //}
    // Start is called before the first frame update
    void Start()
    {
        //childWords = new List<GameObject>();
        //if (attributes == null)
        //{
        //    Debug.LogError("未找到 MoveableTypePrintingAttributes!");
        //    return;
        //}
        ////InitData();
        //if (AppConfig.lbeSceneId == 3)
        //{
        //    isAutoShow = true;
        //    for (int i = 0; i < this.transform.childCount; i++)
        //    {
        //        childWords.Add(transform.GetChild(i).gameObject);
        //    }
        //}
        //else
        //{
        //    isAutoShow = false;
        //}
        //StartCoroutine(DelayedInitialization());
    }

    //public void InitData()
    //{
    //    rightHand = GameObject.Find("RightHand Controller").transform;
    //    //GetComponent<TypePrintingSweepEffect>().enabled = false;
    //    if (rightHand != null)
    //    {
    //        // 手上的射线效果开启
    //        if (rightHand.Find("Hand_R/right_wrist/right_palm/rayEff") != null)
    //        {
    //            rightHand.Find("Hand_R/right_wrist/right_palm/rayEff").gameObject.SetActive(true);
    //            rightHandRayTrans = rightHand.Find("Hand_R/right_wrist/right_palm");
    //        }
    //        if (rightHand.Find("rayEff") != null)
    //        {
    //            rightHand.Find("rayEff").gameObject.SetActive(true);
    //            rightHandRayTrans = rightHand;
    //        }
    //        rayLineEff = rightHandRayTrans.Find("rayEff/rayLine");
    //        rayLineEff.gameObject.SetActive(true);
    //        mainCamera = GameObject.FindWithTag("MainCamera").transform;
    //        pointEff = rightHandRayTrans.Find("rayEff/pointEff");
    //        pointEff.gameObject.SetActive(false);
    //        rayLineBool = Animator.StringToHash("showLine");
    //        rayLineAni = rayLineEff.GetComponent<Animator>();
    //        rayLineAni.SetBool(rayLineBool, false);
    //        isRayAniShow = false;
    //        canCheck = true;

    //        leftTime = cdTime;
    //    }
    //}

    // 延迟初始化协程
    private IEnumerator DelayedInitialization()
    {
        yield return null;

        // 确保有分类数据后再初始化颜色
        int retryCount = 0;
        int maxRetries = 10; // 最大重试次数

        while (attributes.GetAllCategories().Count == 0 && retryCount < maxRetries)
        {
            yield return new WaitForSeconds(0.1f); // 等待0.1秒
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


    // Update is called once per frame
    void Update()
    {
        if (canInteracble)
        {
            if (isAutoShow)
            {
                autoShowTimeTemp += Time.deltaTime;
                autoHideTimeTemp += Time.deltaTime;
                if (!isHitting&& autoShowTimeTemp >= autoShowRate)
                {
                    rayLineEff.gameObject.SetActive(false);
                    rayLineAni.SetBool(rayLineBool, false);
                    autoShowTimeTemp = 0;

                    GameObject curObj = childWords[Random.Range(0, childWords.Count)];

                    if (lastHitObject != curObj)
                    {
                        timeCount += Time.deltaTime;

                        //if (timeCount <= maxHoldTime || leftTime > 0)
                        //{
                        //    return;
                        //}

                        Debug.Log("GetCategoryNamesByObject:" + curObj.name);
                        // 获取当前击中物体的分类
                        string newCategoryName = attributes.GetCategoryNamesByObject(curObj);

                        // 获取上一个击中物体的分类
                        string lastCategoryName = lastHitObject != null ? attributes.GetCategoryNamesByObject(lastHitObject) : null;

                        // 如果分类不同，才执行离开和击中事件
                        if (lastCategoryName != newCategoryName)
                        {
                            // 如果之前有击中物体，触发离开事件
                            if (lastHitObject != null)
                            {
                                attributes.HandleRayExit(lastHitObject, animationDuration);
                                // 启动颜色过渡协程 - 从hitColor到exitColor
                                StartColorTransition(lastCategoryName, hitColor, exitColor, animationDuration);
                            }
                            // audioSource.Stop();
                            // audioSource.PlayOneShot(clip);

                            // GameObject pointEffClone = Instantiate(pointEff.gameObject);
                            // pointEffClone.transform.parent = curObj.transform;
                            // pointEffClone.transform.localPosition = new Vector3(4.5f, 0, 0);
                            // pointEffClone.transform.localEulerAngles = new Vector3(0, -90, 0);
                            // pointEffClone.SetActive(true);
                            // Destroy(pointEffClone, 1.5f);

                            // 触发击中事件
                            attributes.HandleRayHit(curObj, animationDuration, moveDistance);
                            // 启动颜色过渡协程 - 从exitColor到hitColor
                            StartColorTransition(newCategoryName, exitColor, hitColor, animationDuration);
                        }

                        lastHitObject = curObj;
                        isHitting = true;
                        timeCount = 0;
                        leftTime = cdTime;
                        Invoke("AutoHideWords", 5);
                    }

                }

                if (autoHideTimeTemp >= autoHideRate)
                { 
                    autoHideTimeTemp = 0;
                }
                }
            else
            {
                HandleMouseInput();
                CheckHandPosition();
            }
            leftTime -= Time.deltaTime;
            if (leftTime <= 0)
            {
                leftTime = 0;
            }

        }

        //if (Input.GetKeyDown(KeyCode.H))
        //{
        //    AutoHideWords();
        //}
    }

    private int autoShowRate = 3;
    private float autoShowTimeTemp = 0;

    private int autoHideRate = 4;
    private float autoHideTimeTemp = 0;
    private void AutoShowWords()
    {

    }
    private void AutoHideWords()
    {
        if (isHitting && lastHitObject != null)
        {
            autoShowTimeTemp = 0;
            // 射线没有击中任何物体，如果之前有击中，触发离开事件
            string categoryName = attributes.GetCategoryNamesByObject(lastHitObject);
            attributes.HandleRayExit(lastHitObject, animationDuration);
            // 启动颜色过渡协程 - 从hitColor到exitColor
            StartColorTransition(categoryName, hitColor, exitColor, animationDuration);
            lastHitObject = null;
            isHitting = false;
        }
    }
    float curHandDis = 0;
    float curHandAngel = 0;
    float delayJudgeTime = 0.2f;
    float delayTempTime = 0;

    float missIndex = 0;
    float showIndex = 0;
    void CheckHandPosition()
    {
        if (mainCamera != null && rightHandRayTrans != null)
        {
            //视锥判断
            //curHandDis = Vector3.Distance(rightHandRayTrans.position, mainCamera.position);
            //curHandAngel = Vector3.Angle(mainCamera.forward, rightHandRayTrans.position - mainCamera.position);

            //Debug.Log("curHandDis:" + curHandDis);
            //Debug.Log("curHandAngle:" + curHandAngel);
            //if (rightHandRayTrans.localPosition != handOriginPos)
            //{
            //    if (curHandDis < minDistance || curHandDis > maxDistance || curHandAngel > maxAngle)
            //    {
            //        missIndex++;
            //    }
            //    else if(curHandDis>missIndex&&curHandDis<maxDistance&&curHandAngel<maxAngle)
            //    {
            //        showIndex++;
            //    }
            //    delayTempTime += Time.deltaTime;
            //    if (delayTempTime >= delayJudgeTime)
            //    {

            //        if (missIndex > showIndex)
            //        {
            //            rayLineAni.SetBool(rayLineBool, false);
            //        }
            //        else if (missIndex < showIndex)
            //        {
            //            rayLineAni.SetBool(rayLineBool, true);
            //        }
            //        delayTempTime = 0;
            //        missIndex = 0;
            //        showIndex = 0;
            //    }
            //}
            //高度判断
            //Debug.Log(Mathf.Abs(mainCamera.position.y - rightHandRayTrans.position.y));
            //Debug.Log("rightHandRayTrans.position:" + rightHandRayTrans.position);
            if (rightHandRayTrans.position.Equals(handOriginPos))
            {
                rayLineAni.SetBool(rayLineBool, false);
                isRayAniShow = false;
            }
            else
            {
                if (Mathf.Abs(mainCamera.position.y - rightHandRayTrans.position.y) > 0.3f)
                {
                    rayLineAni.SetBool(rayLineBool, false);
                    isRayAniShow = false;
                }
                else
                {
                    rayLineAni.SetBool(rayLineBool, true);
                    isRayAniShow = true;
                }
            }
            //Debug.Log("isRayAniShow:"+isRayAniShow);
        }
    }

    private float timeCount = 0;
    private int effTimeCount = 0;
    //private GameObject pointEffClone;
    private void HandleMouseInput()
    {
        if (rightHandRayTrans == null) return;
        if (!isRayAniShow) return;

        ray = new Ray(rightHandRayTrans.position, rightHandRayTrans.forward);
        string categoryName = null;

        if (Physics.Raycast(ray, out hit, rayLength, raycastLayerMask))
        {
            if (lastHitObject != hit.collider.gameObject)
            {

                rayLineAni.SetBool(rayLineBool, true);
                timeCount += Time.deltaTime;

                if (timeCount <= maxHoldTime || leftTime > 0)
                {
                    return;
                }


                // 获取当前击中物体的分类
                string newCategoryName = attributes.GetCategoryNamesByObject(hit.collider.gameObject);

                // 获取上一个击中物体的分类
                string lastCategoryName = lastHitObject != null ? attributes.GetCategoryNamesByObject(lastHitObject) : null;

                // 如果分类不同，才执行离开和击中事件
                if (lastCategoryName != newCategoryName)
                {
                    // 如果之前有击中物体，触发离开事件
                    if (lastHitObject != null)
                    {
                        attributes.HandleRayExit(lastHitObject, animationDuration);
                        // 启动颜色过渡协程 - 从hitColor到exitColor
                        StartColorTransition(lastCategoryName, hitColor, exitColor, animationDuration);
                    }
                    audioSource.Stop();
                    audioSource.PlayOneShot(clip);

                    GameObject pointEffClone = Instantiate(pointEff.gameObject);
                    pointEffClone.transform.parent = hit.collider.gameObject.transform;
                    pointEffClone.transform.localPosition = new Vector3(4.5f, 0, 0);
                    pointEffClone.transform.localEulerAngles = new Vector3(0, -90, 0);
                    pointEffClone.SetActive(true);
                    Destroy(pointEffClone, 1.5f);

                    // 触发击中事件
                    attributes.HandleRayHit(hit.collider.gameObject, animationDuration, moveDistance);
                    // 启动颜色过渡协程 - 从exitColor到hitColor
                    StartColorTransition(newCategoryName, exitColor, hitColor, animationDuration);
                }

                lastHitObject = hit.collider.gameObject;
                isHitting = true;
                timeCount = 0;
                leftTime = cdTime;
            }
        }
        else if (isHitting && lastHitObject != null)
        {
            // 射线没有击中任何物体，如果之前有击中，触发离开事件
            categoryName = attributes.GetCategoryNamesByObject(lastHitObject);
            attributes.HandleRayExit(lastHitObject, animationDuration);
            // 启动颜色过渡协程 - 从hitColor到exitColor
            StartColorTransition(categoryName, hitColor, exitColor, animationDuration);
            lastHitObject = null;
            isHitting = false;
        }

        if (drawRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * rayLength, rayColor);
        }
    }

    // 启动颜色过渡协程
    private void StartColorTransition(string categoryName, Color fromColor, Color toColor, float duration)
    {
        if (string.IsNullOrEmpty(categoryName)) return;

        // 如果该分类已有颜色过渡协程在运行，先停止它
        if (colorTransitionCoroutines.TryGetValue(categoryName, out Coroutine coroutine))
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
            colorTransitionCoroutines.Remove(categoryName);
        }

        // 启动新的颜色过渡协程
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

            // 应用当前插值后的颜色
            attributes.SetCategoryGlowColor(categoryName, currentColor);

            yield return null;
        }

        // 确保最终颜色精确匹配目标颜色
        attributes.SetCategoryGlowColor(categoryName, toColor);

        // 从字典中移除协程引用
        colorTransitionCoroutines.Remove(categoryName);
    }

    private void OnDrawGizmos()
    {
        if (drawRay && rightHandRayTrans != null)
        {
            Gizmos.color = rayColor;
            Vector3 direction = rightHandRayTrans.transform.forward;
            Gizmos.DrawRay(rightHandRayTrans.transform.position, direction * rayLength);
        }
    }
}
