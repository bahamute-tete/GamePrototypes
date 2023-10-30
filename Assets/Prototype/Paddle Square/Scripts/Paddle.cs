using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Paddle : MonoBehaviour
{
    static readonly int timeOfLastHitID = Shader.PropertyToID("_TimelastHit");
    static readonly int emissionColorId = Shader.PropertyToID("_HDRColor");
    static readonly int faceColorId = Shader.PropertyToID("_FaceColor");
    Material paddleMat, goalMat, scoreMat;

    [SerializeField]
    MeshRenderer goalRenderer;
    [SerializeField, ColorUsage(true, true)]
    Color goalColor = Color.white;

    [SerializeField]
    TextMeshPro scoreText;

    int score;

    [SerializeField, Min(0f)]
    float //extents = 4f,//paddle size
            minExtents = 4f,
            maxExtents = 4f,
            speed = 10f,
            maxTargetBias = 0.75f;//和目标之间的最大偏差

    [SerializeField]
    bool isAI;

    float extents, targetingBias;

    void SetExtents(float newExtents)
    {
        extents = newExtents;
        Vector3 s = transform.localScale;
        s.x = 2f * newExtents;
        transform.localScale = s;
    }

    void ChangeTargetingBias()
    {
        targetingBias = Random.Range(-maxTargetBias, maxTargetBias);
    }



    public void StartNewGame()
    {
        SetScore(0);
        ChangeTargetingBias();
    }

    public bool ScorePoint(int pointsToWin)
    {
        
        SetScore(score + 1,pointsToWin);

      
        return score >= pointsToWin;
    }

    void SetScore(int newScore,float pointsToWin =1000f)
    {
        score = newScore;
        scoreText.SetText("{0}", newScore);

        goalMat.SetFloat(timeOfLastHitID, Time.time);
        scoreMat.SetColor(faceColorId, goalColor * (newScore / pointsToWin));
        SetExtents(Mathf.Lerp(maxExtents, minExtents, newScore / (pointsToWin - 1f)));
    }


    public void Move(float target, float arenaExtents)
    {
        Vector3 p = transform.localPosition;
        p.x = isAI ? AdjustByAI(p.x, target) : AdjustByPlayer(p.x);
        float limit = arenaExtents - extents;
        p.x = Mathf.Clamp(p.x, -limit, limit);
        transform.localPosition = p;
    }


    float AdjustByAI(float x, float target)
    {

        //x  is paddle posX
        //target is ball posX;
        target += targetingBias * extents;

         if (x < target)
        {
            return Mathf.Min(x + speed * Time.deltaTime, target);
        }
        return Mathf.Max(x - speed * Time.deltaTime, target);
    }


    float AdjustByPlayer(float x)
    {
        //左移动 ，右移动 ，不动
        bool goRight = Input.GetKey(KeyCode.RightArrow);
        bool goLeft = Input.GetKey(KeyCode.LeftArrow);

        if (goRight && !goLeft)
        {
            return x + speed * Time.deltaTime;
        }
        else if (goLeft && !goRight)
        {
            return x - speed * Time.deltaTime;
        }

        return x;
    }

    public bool HitBall(float ballX, float ballExtents,out float hitFactor)
    {
        ChangeTargetingBias();
        //归一化处理
        hitFactor = (ballX - transform.localPosition.x) / (extents + ballExtents);
        bool success= -1f <= hitFactor && hitFactor <= 1;
        if (success)
        {
            paddleMat.SetFloat(timeOfLastHitID, Time.time);
        }
        return success;
    }

    private void Awake()
    {
        paddleMat = GetComponent<MeshRenderer>().material;
        goalMat = goalRenderer.material;

        goalMat.SetColor(emissionColorId, goalColor);

        scoreMat = scoreText.fontMaterial;
        SetScore(0);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
