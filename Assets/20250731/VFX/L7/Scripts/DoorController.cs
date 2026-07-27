using UnityEngine;
using System.Collections;
using System.Collections.Generic;


[ExecuteInEditMode]
public class DoorController : MonoBehaviour
{
    public Transform pivot_L;
    public Transform pivot_R;
    
    [Header("动画设置")]
    public float openAngle = 90f;           
    public float animationDuration_OpenDoor = 1f;   
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private bool isOpen = false;
    private bool isAnimating = false;

    public float animationDuration_Appearance = 1f;
    public AnimationCurve animationCurve_Appearance = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private bool isAppearance = false;
    private bool isAppearanceAnimating = false;

    [HideInInspector] public Material doorMaterial_L;
    [HideInInspector] public Material doorMaterial_R;

    [Header("Shader参数")]
    static private int glowThresholdID = Shader.PropertyToID("_Threshold");
    static private int useFlipBoolID = Shader.PropertyToID("_FlipBook");
    static private int clipThresholdID = Shader.PropertyToID("_ClipThreshold");
    static private int frameClipThresholdID = Shader.PropertyToID("_CutoffHeight1");

    public Transform targetTransform;
    private DoorController doorController;

    [Header("门控制设置")]
    public float maxDistanceThreshold = 10f;    // 最大距离阈值
    public float minDistanceThreshold = 2f;     // 最小距离阈值
    public bool useSmoothDoorControl = true;    // 是否使用平滑控制
    public float smoothSpeed = 5f;              // 平滑速度
    public bool openOutward = false;        // 开门方向：false=向内开，true=向外开
    public AnimationCurve animationCurve_AppearanceDuration = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Range(0f,1f)]public float doorGlow = 0f;//花纹发光控制
    public bool useFlipBook = false;//是否动画

    private void Awake()
    {
        if (pivot_L != null && pivot_R != null)
        { 
           doorMaterial_L = pivot_L.GetComponentInChildren<Renderer>().sharedMaterials[1];
           doorMaterial_R = pivot_R.GetComponentInChildren<Renderer>().sharedMaterials[1];
        }
    }
    private void OnEnable()
    {
        if (pivot_L != null && pivot_R != null)
        {
            doorMaterial_L = pivot_L.GetComponentInChildren<Renderer>().sharedMaterials[1];
            doorMaterial_R = pivot_R.GetComponentInChildren<Renderer>().sharedMaterials[1];
        }
    }

    private void Start()
    {
      
        //SetMaterialForDoor(pivot_L, doorMaterial_L);
        //SetMaterialForDoor(pivot_R, doorMaterial_R);
    }


    private void OnDrawGizmos()
    {
        if ( targetTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetTransform.position, maxDistanceThreshold);
            Gizmos.DrawWireSphere(targetTransform.position, minDistanceThreshold);
            Vector3 bias = new Vector3(0, 2.367996f, 0);
            Gizmos.DrawLine(targetTransform.transform.position, transform.gameObject.transform.position + bias);
        }
    }

    private void Update()
    {


        if (targetTransform != null)
        {
            UpdateDoorsBasedOnDistance();
            SetFlipAnimation(useFlipBook);
            SetDoorGlow();
        }
    }


    void UpdateDoorsBasedOnDistance()
    {

            GameObject wall = transform.gameObject;
            Vector3 bias = new Vector3(0, 2.367996f, 0);

            float distance = Vector3.Distance(targetTransform.transform.position, wall.transform.position+bias);

            bool isPlayerInFront = IsPlayerInFrontOfWall(wall, targetTransform.transform.position);

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
                SetDoorAngleByDistanceSmooth(normalizedDistance, smoothSpeed);
                //SetGlowThreshold(1.0f - normalizedDistance);
            }
            else
            {
                SetDoorAngleByDistance(normalizedDistance);
                //SetGlowThreshold(1.0f - normalizedDistance);
            }
            
        
    }


    #region Methods 

    void SetFlipAnimation(bool useFlipBook)
    {
        if (doorMaterial_L != null && doorMaterial_R != null)
        {
            doorMaterial_L.SetFloat(useFlipBoolID, useFlipBook ? 1f : 0f);
            doorMaterial_R.SetFloat(useFlipBoolID, useFlipBook ? 1f : 0f);
        }
    }
    void SetDoorGlow()
    {
        if (doorMaterial_L != null && doorMaterial_R != null)
        { 
            doorMaterial_L.SetFloat(glowThresholdID, doorGlow);
            doorMaterial_R.SetFloat(glowThresholdID, doorGlow);
        }

    }
    bool IsPlayerInFrontOfWall(GameObject wall, Vector3 playerPosition)
    {
        Vector3 wallForward = wall.transform.right;
        Vector3 wallToPlayer = (playerPosition - wall.transform.position).normalized;
        float dot = Vector3.Dot(wallForward, wallToPlayer);
        return dot > 0f;
    }

    private void SetMaterialForDoor(Transform doorPivot, Material mat)
    {

        for (int i = 0; i < doorPivot.childCount; i++)
        {
            Transform child = doorPivot.GetChild(i);
            Renderer renderer = child.GetComponent<Renderer>();

            if (renderer != null && renderer.materials.Length >= 2)
            {

                Material[] materials = renderer.materials;

                materials[1] = mat;
                renderer.materials = materials;

                //Debug.Log($"已为 {child.name} 设置第二个材质");
            }

        }
    }



    public void OpenDoor()
    {
        if (!isAnimating && !isOpen)
        {
            StartCoroutine(AnimateDoor(true));
        }
    }
    

    public void CloseDoor()
    {
        if (!isAnimating && isOpen)
        {
            StartCoroutine(AnimateDoor(false));
        }
    }
    

    public void ToggleDoor()
    {
        if (isOpen)
            CloseDoor();
        else
            OpenDoor();
    }
    
    private IEnumerator AnimateDoor(bool opening)
    {
        isAnimating = true;
        
        float startAngle = opening ? 0f : openAngle;
        float endAngle = opening ? openAngle : 0f;
        
        float angleMultiplier = openOutward ? -1f : 1f;
        float elapsedTime = 0f;
        
        while (elapsedTime < animationDuration_OpenDoor)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration_OpenDoor;
            t = animationCurve.Evaluate(t);
            
            float currentAngle = Mathf.Lerp(startAngle, endAngle, t);
            
            if (pivot_L != null)
                pivot_L.localRotation = Quaternion.Euler(0, currentAngle * angleMultiplier, 0);
            
            if (pivot_R != null)
                pivot_R.localRotation = Quaternion.Euler(0, -currentAngle * angleMultiplier, 0);
            yield return null;
        }

        float finalAngle = opening ? openAngle : 0f;
        if (pivot_L != null)
            pivot_L.localRotation = Quaternion.Euler(0, finalAngle * angleMultiplier, 0);
        if (pivot_R != null)
            pivot_R.localRotation = Quaternion.Euler(0, -finalAngle * angleMultiplier, 0);
        
        isOpen = opening;
        isAnimating = false;
    }
    
    public bool IsOpen => isOpen;
    public bool IsAnimating => isAnimating;

    public bool IsAppearance => isAppearance;
    public bool IsAppearanceAnimating => isAppearanceAnimating;

    public void SetDoorAngleByDistance(float normalizedDistance)
    {
        if (isAnimating) return;
        
        float targetAngle = (1f - Mathf.Clamp01(normalizedDistance)) * openAngle;
        
        float angleMultiplier = openOutward ? -1f : 1f;
        
        if (pivot_L != null)
            pivot_L.localRotation = Quaternion.Euler(0, targetAngle * angleMultiplier, 0);
        if (pivot_R != null)
            pivot_R.localRotation = Quaternion.Euler(0, -targetAngle * angleMultiplier, 0);
        
        isOpen = targetAngle > 0f;
    }
    
    public void SetDoorAngleByDistanceSmooth(float normalizedDistance, float smoothSpeed = 5f)
    {
        if (isAnimating) return;
        
        float targetAngle = (1f - Mathf.Clamp01(normalizedDistance)) * openAngle;
        
        float angleMultiplier = openOutward ? -1f : 1f;
        float targetAngleL = targetAngle * angleMultiplier;
        float targetAngleR = -targetAngle * angleMultiplier;
        
        float currentAngleL = pivot_L != null ? pivot_L.localRotation.eulerAngles.y : 0f;
        float currentAngleR = pivot_R != null ? pivot_R.localRotation.eulerAngles.y : 0f;
        

        if (currentAngleL > 180f) currentAngleL -= 360f;
        if (currentAngleR > 180f) currentAngleR -= 360f;

        float newAngleL = Mathf.Lerp(currentAngleL, targetAngleL, Time.deltaTime * smoothSpeed);
        float newAngleR = Mathf.Lerp(currentAngleR, targetAngleR, Time.deltaTime * smoothSpeed);
        

        if (pivot_L != null)
            pivot_L.localRotation = Quaternion.Euler(0, newAngleL, 0);
        if (pivot_R != null)
            pivot_R.localRotation = Quaternion.Euler(0, newAngleR, 0);
        
        isOpen = targetAngle > 0f;
    }

    public void SetGlowThreshold(float normalizedDistance)
    {

        Renderer pivot_L_renderer = pivot_L.GetComponentInChildren<Renderer>();
        Renderer pivot_R_renderer = pivot_R.GetComponentInChildren<Renderer>();

        if (pivot_L_renderer != null && pivot_L_renderer.materials.Length >= 2)
        {

            Material material = pivot_L_renderer.sharedMaterials[1];

            material.SetFloat(glowThresholdID, normalizedDistance);
        }


        if (pivot_R_renderer != null && pivot_R_renderer.materials.Length >= 2)
        {

            Material material = pivot_R_renderer.sharedMaterials[1];

            material.SetFloat(glowThresholdID, normalizedDistance);
        }


    }


    private IEnumerator AnimateDoorApperance(bool appearance)
    {
        isAppearanceAnimating = true;
        float elapsedTime = 0f;
        float startVale = appearance ? 0f : 1f;
        float endValue = appearance ? 1f : 0f;
        while (elapsedTime < animationDuration_Appearance)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration_Appearance;
            t = animationCurve_Appearance.Evaluate(t);

            float currentThreshold = Mathf.Lerp(startVale, endValue, t);


            Renderer pivot_L_renderer = pivot_L.GetComponentInChildren<Renderer>();
            Renderer pivot_R_renderer = pivot_R.GetComponentInChildren<Renderer>();

            if (pivot_L_renderer != null && pivot_L_renderer.materials.Length >= 2)
            {
                Material materialFrame = pivot_L_renderer.sharedMaterials[0];
                Material material = pivot_L_renderer.sharedMaterials[1];

                materialFrame.SetFloat(frameClipThresholdID, Mathf.Lerp(0f, -1.1f, currentThreshold));
                material.SetFloat(clipThresholdID, currentThreshold);
            }


            if (pivot_R_renderer != null && pivot_R_renderer.materials.Length >= 2)
            {
                Material materialFrame = pivot_R_renderer.sharedMaterials[0];
                Material material = pivot_R_renderer.sharedMaterials[1];

                materialFrame.SetFloat(frameClipThresholdID, Mathf.Lerp(0f, -1.1f, currentThreshold));
                material.SetFloat(clipThresholdID, currentThreshold);
            }


            yield return null;
        }

        isAppearance = appearance;
        isAppearanceAnimating = false;
    }

    public void ShowDoor()
    {
        if (!IsAppearanceAnimating && !isAppearance)
        {
            StartCoroutine(AnimateDoorApperance(true));
        }
    }


    public void FadeDoor()
    {
        if (!IsAppearanceAnimating && !isAppearance)
        {
            StartCoroutine(AnimateDoorApperance(false));
        }
    }


    public void ToggleShowDoor()
    {
        if (isAppearance)
            ShowDoor();
        else
            FadeDoor();
    }
    #endregion
}
