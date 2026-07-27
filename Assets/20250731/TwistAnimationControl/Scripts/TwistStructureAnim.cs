using System.Collections.Generic;
using UnityEngine;
using Color = UnityEngine.Color;




public class TwistStructureAnim : MonoBehaviour
{
    #region Movement Fields
    public enum EaseType
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }
    public enum MovmentMode
    {
        CW,
        CCW,
        PingPong,
        OddAndEven,            //奇数块顺时针，偶数块逆时针
        Alternate,             // 每次循环改变方向
        Random,                // 每个块随机选择方向
        Median,                // 从中间分段，前段后段方向相反
        Spiral,                // 角度随块的位置递增变化
        Custom                 // +(a)Turn cw ‹a› degrees. -(a)Turn ccw ‹a› degrees(minus sign). Default a=90.
    }

    private List<int> segmentRandomDirections = new List<int>(); // 用于Random模式
    private int alternateCounter = 0; // Alternate模式 最后一个块完成时增加计数器
    private System.Random random = new System.Random(); // 随机数生成器

    // Custom Rule
    private List<float> customRuleAngles = new List<float>(); 
    private List<int> customRuleCurrentIndex = new List<int>(); // 每个块当前执行到规则的哪一步
    private bool isCustomRuleParsed = false; // 标记规则是否已解析

    #endregion

    // [Header("Base")]
    public Transform structurePrefab;
    private List<Transform> segments = new List<Transform>();

    // [Header("TwistShape")]
    public int numSegments = 12;
    public float depthStep = 0.5f;
    public float twistAngle = 10.0f;

    // [Header("Animation")]
    public bool activeAnimation = true;

    public EaseType easeType = EaseType.EaseOut;
    public MovmentMode movmentType = MovmentMode.CW;
    public string rule = ""; 

    public float duration = 5.0f;
    public float addtionAngle = 90.0f;
    public float waitTime = 1.0f;
    public float delayTime = 0.5f;


    #region Animation State Fields

    private List<Vector3> initialAngles = new List<Vector3>();
    private List<Vector3> originalAngles = new List<Vector3>(); 
    private List<Vector3> targetAngles = new List<Vector3>();
    private List<float> segmentAnimationTimes = new List<float>();
    private List<bool> segmentIsWaiting = new List<bool>();
    private List<float> segmentWaitTimers = new List<float>();
    private List<bool> segmentIsReversing = new List<bool>();
    private bool isReturningToOriginal = false;
    private List<bool> segmentHasReturned = new List<bool>();
    private bool wasActiveLastFrame = true; // 追踪上一帧的activeAnimation状态

    #endregion


    #region Appearance Fields
    public enum LightMode
    {
        Solid,
        Gradient,
        Wave,
        Idle
    }

    public enum SolidDistributionMode
    {
        Uniform,
        Random
    }

    public enum IdleColorType
    {
        Solid,
        Gradient
    }

    // [Header("Appearacen")]
    public LightMode lightMode = LightMode.Gradient;

    public string shaderPropertyName= "_BaseColor";

    public SolidDistributionMode solidDistributionMode = SolidDistributionMode.Uniform;
    public Color solidColor = Color.white;
    public List<Color> randomColors = new List<Color>();    // 用于Random模式

    public Gradient gradient = new Gradient();

    public bool idleLightAnimation = true;
    public IdleColorType idleColorType = IdleColorType.Gradient;
    public float idleFrequency = 1.0f;

    public float waveSpeed = 1.0f;

    private Color segmentColor = Color.white;
    private Material[] mats;
    private Color[] gradientColors;
    private float idleLightTimer = 0f;
    private List<Material> cachedMaterials = new List<Material>();
    private bool materialsInitialized = false;
    private Color lastSolidColor;
    private LightMode lastLightMode;
    private SolidDistributionMode lastSolidDistributionMode; 
    private List<Color> lastRandomColors = new List<Color>(); 
    

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        GenerateTwistShape();
        InitializeMaterials();

        gradientColors = new Color[segments.Count];

        initialGradientColor();

        lastSolidColor = solidColor;
        lastLightMode = lightMode;
        lastSolidDistributionMode = solidDistributionMode;

        if (randomColors != null)
        {
            lastRandomColors = new List<Color>(randomColors);
        }

        ApplyInitialColor(shaderPropertyName);
    }

    // Update is called once per frame
    void Update()
    {
         TwistAnimation();
         UpdateColor(shaderPropertyName);
    }


    #region InitalMethods

    private void InitializeMaterials()
    {
        if (segments == null || segments.Count == 0) return;

        cachedMaterials.Clear();

        for (int i = 0; i < segments.Count; i++)
        {
            Renderer renderer = segments[i].GetComponent<Renderer>();
            if (renderer != null)
            {
                cachedMaterials.Add(renderer.material);
            }
            else
            {
                cachedMaterials.Add(null);
                Debug.LogWarning($"Segment {i} has no Renderer component!");
            }
        }

        materialsInitialized = true;
    }
    private void initialGradientColor()
    {
        if (segments.Count == 0) return;

        for (int i = 0; i < segments.Count; i++)
        {
            float t = segments.Count > 1 ? i / (float)(segments.Count - 1) : 0f;
            gradientColors[i] = gradient.Evaluate(t);
        }
    }

    private void ApplyInitialColor(string shaderPropertyName)
    {
        if (!materialsInitialized || segments.Count == 0) return;

        if (lightMode == LightMode.Solid)
        {
            if (solidDistributionMode == SolidDistributionMode.Uniform)
            {
                segmentColor = solidColor;
                for (int i = 0; i < segments.Count; i++)
                {
                    if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                    {
                        cachedMaterials[i].SetColor(shaderPropertyName, segmentColor);
                    }
                }
            }
            else if (solidDistributionMode == SolidDistributionMode.Random)
            {
                if (randomColors == null || randomColors.Count == 0)
                {
                    Debug.LogWarning("Random colors list is empty! Using solid color instead.");
                    segmentColor = solidColor;
                    for (int i = 0; i < segments.Count; i++)
                    {
                        if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                        {
                            cachedMaterials[i].SetColor(shaderPropertyName, segmentColor);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < segments.Count; i++)
                    {
                        Color randomColor = randomColors[random.Next(0, randomColors.Count)];
                        gradientColors[i] = randomColor;
                        
                        if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                        {
                            cachedMaterials[i].SetColor(shaderPropertyName, randomColor);
                        }
                    }
                }
            }
        }
        else if (lightMode == LightMode.Gradient)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                {
                    cachedMaterials[i].SetColor(shaderPropertyName, gradientColors[i]);
                }
            }
        }
    }
    private void GenerateTwistShape()
    {
        if (structurePrefab == null) return;

        if (movmentType == MovmentMode.Custom && !isCustomRuleParsed)
        {
            ParseCustomRule(rule);
        }

        for (int i = 0; i < numSegments; i++)
        {
            Transform segment = Instantiate(structurePrefab, Vector3.zero, Quaternion.identity, transform);
            segment.localPosition = new Vector3(0, 0, i * depthStep);
            float segmentTwistAngle = i * twistAngle;
            segment.localRotation = Quaternion.Euler(0, 0, segmentTwistAngle);
            
            Vector3 initialAngle = new Vector3(0, 0, segmentTwistAngle);
            float angleDirection = GetAngleDirection();
            Vector3 targetAngle = new Vector3(0, 0, segmentTwistAngle + addtionAngle * angleDirection);

            segments.Add(segment);
            originalAngles.Add(initialAngle);
            initialAngles.Add(initialAngle);
            targetAngles.Add(targetAngle);
            segmentAnimationTimes.Add(-i * delayTime);
            segmentIsWaiting.Add(false);
            segmentWaitTimers.Add(0f);
            segmentIsReversing.Add(false);
            segmentHasReturned.Add(false);

            segmentRandomDirections.Add(random.Next(0, 2) == 0 ? -1 : 1);
            customRuleCurrentIndex.Add(0); // 初始化Custom Rule索引
        }
    }

    #endregion

    #region AnimationMethods
    void TwistAnimation()
    {
        if (segments.Count == 0) return;

        // 检测activeAnimation从true变为false
        if (!activeAnimation && wasActiveLastFrame)
        {
            isReturningToOriginal = true;
            for (int i = 0; i < segmentHasReturned.Count; i++)
            {
                segmentHasReturned[i] = false;
                segmentAnimationTimes[i] = 0f;
               
                initialAngles[i] = new Vector3(0, 0, segments[i].localRotation.eulerAngles.z);
            }
        }

        // 检测activeAnimation从false变为true
        if (activeAnimation && !wasActiveLastFrame && !isReturningToOriginal)
        {
            
            for (int i = 0; i < segments.Count; i++)
            {
                segmentAnimationTimes[i] = -i * delayTime;
                segmentIsWaiting[i] = false;
                segmentWaitTimers[i] = 0f;
            }
        }

        wasActiveLastFrame = activeAnimation;

        if (isReturningToOriginal)
        {
            bool allReturned = true;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] == null) continue;

                if (segmentHasReturned[i]) continue;

                segmentAnimationTimes[i] += Time.deltaTime;
                float segmentNormalizedTime = Mathf.Clamp01(segmentAnimationTimes[i] / duration);
                float easedT = ApplyEasing(segmentNormalizedTime, easeType);

                Vector3 currentAngle = Vector3.Lerp(initialAngles[i], originalAngles[i], easedT);
                segments[i].localRotation = Quaternion.Euler(currentAngle);

                if (segmentNormalizedTime >= 1f)
                {
                    segments[i].localRotation = Quaternion.Euler(originalAngles[i]);
                    segmentHasReturned[i] = true;
                }
                else
                {
                    allReturned = false;
                }
            }

            if (allReturned)
            {
                isReturningToOriginal = false;
                for (int i = 0; i < segments.Count; i++)
                {
                    initialAngles[i] = originalAngles[i];
                    float angleDirection = GetAngleDirection();
                    targetAngles[i] = new Vector3(0, 0, originalAngles[i].z + addtionAngle * angleDirection);
                    segmentAnimationTimes[i] = -i * delayTime; // 重置延迟时间
                    segmentIsWaiting[i] = false;
                    segmentWaitTimers[i] = 0f;
                    segmentIsReversing[i] = false;
                }
            }
            return;
        }
/////////////////////////////////////////////////////////////////////////////
        if (!activeAnimation) return;

        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] == null) continue;

            if (segmentIsWaiting[i])
            {
                segmentWaitTimers[i] += Time.deltaTime;
                if (segmentWaitTimers[i] >= waitTime)
                {
                    float overshoot = segmentWaitTimers[i] - waitTime;
                    
                    initialAngles[i] = targetAngles[i];


                    
                    if (movmentType == MovmentMode.Random )
                    {
                        segmentRandomDirections[i] = random.Next(0, 2) == 0 ? -1 : 1;
                    }
                    if (movmentType == MovmentMode.Alternate && i == segments.Count - 1)
                    {
                        alternateCounter++; 
                    }
                    if (movmentType == MovmentMode.PingPong)
                    {
                        segmentIsReversing[i] = !segmentIsReversing[i];
                    }
                    if (movmentType == MovmentMode.Spiral)
                    {
                        // segmentIsReversing[i] = !segmentIsReversing[i];
                    }

                    if (movmentType == MovmentMode.Custom)
                    {
                       customRuleCurrentIndex[i]++;
                    }
                    
                    //float angleDirection = GetAngleDirectionForSegment(i);
                    float angleForSegment = GetAngleForSegment(i);
                    targetAngles[i] = new Vector3(0, 0, targetAngles[i].z + angleForSegment);

                    segmentAnimationTimes[i] = overshoot;
                    segmentIsWaiting[i] = false;
                    segmentWaitTimers[i] = 0f;
                }
                continue;
            }

            segmentAnimationTimes[i] += Time.deltaTime;
            
            if (segmentAnimationTimes[i] < 0)
            {
                segments[i].localRotation = Quaternion.Euler(initialAngles[i]);
                continue;
            }
            
            float segmentNormalizedTime = Mathf.Clamp01(segmentAnimationTimes[i] / duration);
            float easedT = ApplyEasing(segmentNormalizedTime, easeType);

            Vector3 currentAngle = Vector3.Lerp(initialAngles[i], targetAngles[i], easedT);
            segments[i].localRotation = Quaternion.Euler(currentAngle);

            if (segmentAnimationTimes[i] >= duration && !segmentIsWaiting[i])
            {

                float overshoot = segmentAnimationTimes[i] - duration;

                segments[i].localRotation = Quaternion.Euler(targetAngles[i]);

                segmentIsWaiting[i] = true;
                segmentWaitTimers[i] = overshoot;
            }
        }
    }
    private float GetAngleDirection()
    {
        switch (movmentType)
        {
            case MovmentMode.CW:
                return 1f;
            case MovmentMode.CCW:
                return -1f;
            case MovmentMode.PingPong:
                return 1f;
            case MovmentMode.OddAndEven:
                return 1f; 
            case MovmentMode.Alternate:
                return alternateCounter % 2 == 0 ? 1f : -1f;
            case MovmentMode.Random:
                return 1f;
            case MovmentMode.Median:
                return 1f;
            case MovmentMode.Spiral:
                return 1f;
            case MovmentMode.Custom:
                return 1f;
            default:
                return 1f;
        }
    }
    private float GetAngleForSegment(int index)
    {
        if (movmentType == MovmentMode.Custom && customRuleAngles.Count > 0 && isCustomRuleParsed )
        {
           int ruleIndex = customRuleCurrentIndex[index] % customRuleAngles.Count; //loop rules
            return customRuleAngles[ruleIndex];
        }
        else if (movmentType == MovmentMode.Spiral)
        {
            // 螺旋模式：角度随块的位置递增
            float spiralFactor = 1f + (index / (float)numSegments) *1f;
            float angle = addtionAngle * spiralFactor * (segmentIsReversing[index] ? -1f : 1f);
            return angle;
        }
        else
        {
            return addtionAngle * GetAngleDirectionForSegment(index);
            
        }
    }
    private float GetAngleDirectionForSegment(int index)
    {
        switch (movmentType)
        {
            case MovmentMode.CW:
                return 1f;
            case MovmentMode.CCW:
                return -1f;
            case MovmentMode.PingPong:
                return segmentIsReversing[index] ? -1f : 1f;
            case MovmentMode.OddAndEven:
                return (index % 2 == 0) ? 1f : -1f;
            case MovmentMode.Alternate:
                return alternateCounter % 2 == 0 ? 1f : -1f;
            case MovmentMode.Random:
                return segmentRandomDirections[index];
            case MovmentMode.Median:
                int middle = numSegments / 2;
                return (index < middle) ? 1f : -1f;
            case MovmentMode.Spiral:
                return 1f;
            case MovmentMode.Custom:
                return 1f;
            default:
                return 1f;
        }
    }
    private float ApplyEasing(float t, EaseType type)
    {
        switch (type)
        {
            case EaseType.Linear:
                return t;
            case EaseType.EaseIn:
                return t * t;
            case EaseType.EaseOut:
                return t * (2 - t);
            case EaseType.EaseInOut:
                return 3f * t * t - 2f * t * t * t;
            default:
                return t;
        }
    }
    private void ParseCustomRule(string ruleString)
    {
        customRuleAngles.Clear();

        if (string.IsNullOrEmpty(ruleString))
        {
            Debug.LogWarning("Custom rule is empty");
            customRuleAngles.Add(90f);
            isCustomRuleParsed = true;
            return;
        }

        string[] rules = ruleString.Split(new char[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (string r in rules)
        {
            string trimmed = r.Trim();//移除字符串开头和结尾的空白字符

            if (string.IsNullOrEmpty(trimmed)) continue;


            try
            {
                if (trimmed.StartsWith("+"))
                {
                    string numberPart = trimmed.Substring(1).Trim();
                    if (numberPart.StartsWith("(") && numberPart.EndsWith(")"))
                    {
                        numberPart = numberPart.Substring(1, numberPart.Length - 2);
                        float angle = float.Parse(numberPart);
                        customRuleAngles.Add(angle);
                    }

                    else if (string.IsNullOrEmpty(numberPart))
                    {
                        customRuleAngles.Add(90f);
                    }
                    else
                    {

                        float angle = float.Parse(numberPart);
                        customRuleAngles.Add(angle);
                    }
                }

                if (trimmed.StartsWith("-"))
                {
                    string numberPart = trimmed.Substring(1).Trim();
                    if (numberPart.StartsWith("(") && numberPart.EndsWith(")"))
                    {
                        numberPart = numberPart.Substring(1, numberPart.Length - 2);
                        float angle = float.Parse(numberPart);
                        customRuleAngles.Add(-angle);
                    }

                    else if (string.IsNullOrEmpty(numberPart))
                    {
                        customRuleAngles.Add(-90f);
                    }
                    else
                    {

                        float angle = float.Parse(numberPart);
                        customRuleAngles.Add(-angle);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to parse custom rule '{trimmed}': {e.Message}");
            }


            if (customRuleAngles.Count == 0)
            {
                Debug.LogWarning("No valid rules parsed, using default +90");
                customRuleAngles.Add(90f);
            }

            isCustomRuleParsed = true;

            //Debug.Log($"Custom rule parsed: {customRuleAngles.Count} steps");
            //for (int i = 0; i < customRuleAngles.Count; i++)
            //{
            //    Debug.Log($"  Step {i}: {customRuleAngles[i]} degrees");
            //}
        }
    }

    #endregion

    #region ColorMethods
    private void UpdateColor(string shaderPropertyName)
    {
        if (!materialsInitialized || segments.Count == 0) return;

        bool needsRefresh = false;

        // 检测模式变化
        if (lastLightMode != lightMode)
        {
            lastLightMode = lightMode;
            needsRefresh = true;
            idleLightTimer = 0f;
        }

        // 检测Solid颜色变化
        if (lightMode == LightMode.Solid && lastSolidColor != solidColor)
        {
            lastSolidColor = solidColor;
            needsRefresh = true;
        }

        // 检测SolidDistributionMode变化
        if (lightMode == LightMode.Solid && lastSolidDistributionMode != solidDistributionMode)
        {
            lastSolidDistributionMode = solidDistributionMode;
            needsRefresh = true;
        }

           // 检测randomColors列表变化
        if (lightMode == LightMode.Solid && solidDistributionMode == SolidDistributionMode.Random)
        {
            bool colorsChanged = false;
            
            // 检查列表长度是否变化
            if (randomColors == null || lastRandomColors == null || randomColors.Count != lastRandomColors.Count)
            {
                colorsChanged = true;
            }
            else
            {
                // 检查每个颜色是否变化
                for (int i = 0; i < randomColors.Count; i++)
                {
                    if (randomColors[i] != lastRandomColors[i])
                    {
                        colorsChanged = true;
                        break;
                    }
                }
            }
            
            if (colorsChanged)
            {
                needsRefresh = true;
                // 更新lastRandomColors
                lastRandomColors.Clear();
                if (randomColors != null)
                {
                    lastRandomColors = new List<Color>(randomColors);
                }
            }
        }

        //Solid模式
        if (lightMode == LightMode.Solid)
        {
           if (needsRefresh)
            {
                if (solidDistributionMode == SolidDistributionMode.Uniform)
                {
                    segmentColor = solidColor;
                    for (int i = 0; i < segments.Count; i++)
                    {
                        if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                        {
                            cachedMaterials[i].SetColor(shaderPropertyName, segmentColor);
                        }
                    }
                }
                else if (solidDistributionMode == SolidDistributionMode.Random)
                {
                    if (randomColors == null || randomColors.Count == 0)
                    {
                        Debug.LogWarning("Random colors list is empty! Using solid color instead.");
                        segmentColor = solidColor;
                        for (int i = 0; i < segments.Count; i++)
                        {
                            if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                            {
                                cachedMaterials[i].SetColor(shaderPropertyName, segmentColor);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < segments.Count; i++)
                        {
                            Color randomColor = randomColors[random.Next(0, randomColors.Count)];
                            //Debug.Log($"Segment {i} assigned random color {randomColor}");
                            gradientColors[i] = randomColor; // 保存用于可能的Idle模式
                            
                            if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                            {
                                cachedMaterials[i].SetColor(shaderPropertyName, randomColor);
                            }
                        }
                    }
                }
            }
           
        }

        //Gradient模式
        else if (lightMode == LightMode.Gradient)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                float t = segments.Count > 1 ? i / (float)(segments.Count - 1) : 0f;
                gradientColors[i] = gradient.Evaluate(t);

                if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                {
                    cachedMaterials[i].SetColor(shaderPropertyName, gradientColors[i]);
                }
            }
        }

        //Wave模式
        if (lightMode == LightMode.Wave)
        {
            idleLightTimer += Time.deltaTime * waveSpeed;
            
            //float normalizedTime = (idleLightTimer % 1f); 
            float normalizedTime = (idleLightTimer % duration) / duration;
            
            for (int i = 0; i < segments.Count; i++)
            {
                float phaseOffset = i / (float)segments.Count;
                float t = (normalizedTime - phaseOffset) % 1f;
                
                gradientColors[i] = gradient.Evaluate(t);
                
                if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                {
                    cachedMaterials[i].SetColor(shaderPropertyName, gradientColors[i]);
                }
            }
        }

        //Idle模式
        else if (lightMode == LightMode.Idle && idleLightAnimation)
        {
            idleLightTimer += Time.deltaTime * idleFrequency;

            float breathingIntensity = (Mathf.Sin(idleLightTimer * 2f * Mathf.PI) + 1f) / 2f; // 0到1之间波动

            if (idleColorType == IdleColorType.Solid)
            {
                Color baseColor = solidColor;
                Color breathingColor = baseColor * breathingIntensity;
                breathingColor.a = baseColor.a; // 保持alpha不变

                 for (int i = 0; i < segments.Count; i++)
                {
                    if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                    {
                        cachedMaterials[i].SetColor(shaderPropertyName, breathingColor);
                    }
                }

            }

            else if (idleColorType == IdleColorType.Gradient)
            {
                 for (int i = 0; i < segments.Count; i++)
                {

                    // 如果gradientColors未初始化，从gradient采样
                    if (gradientColors[i] == default(Color))
                    {
                        float t = segments.Count > 1 ? i / (float)(segments.Count - 1) : 0f;
                        gradientColors[i] = gradient.Evaluate(t);
                    }

                    Color baseColor = gradientColors[i];
                    Color animatedColor = Color.Lerp(baseColor * 0.5f, baseColor, breathingIntensity);
                    animatedColor.a = baseColor.a; // 保持alpha不变

                    if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty(shaderPropertyName))
                    {
                        cachedMaterials[i].SetColor(shaderPropertyName, animatedColor);
                    }
                }

            }

           
        }

    }
    
    #endregion
}




