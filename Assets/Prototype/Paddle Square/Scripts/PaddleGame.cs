using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PaddleGame : MonoBehaviour
{
    [SerializeField]
    Ball ball;

    [SerializeField]
    Paddle topPaddle, bottomPaddle;

    [SerializeField]
    Vector2 arenaExtnets = new Vector2(10f, 10f);

    [SerializeField,Min(2)]
    int pointsToWin = 3;

    [SerializeField]
    TextMeshPro countDownText;

    [SerializeField, Min(1f)]
    float newGameDelay = 3f;

    float countDownUntilNewGame;

    [SerializeField]
    livingCamera livingCamera;

    private void Awake()
    {
        countDownUntilNewGame = newGameDelay;
    }

    private void StartNewGame()
    {
        ball.StartNewGame();
        bottomPaddle.StartNewGame();
        topPaddle.StartNewGame();
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        bottomPaddle.Move(ball.Position.x, arenaExtnets.x);
        topPaddle.Move(ball.Position.x, arenaExtnets.x);

        if (countDownUntilNewGame <= 0f)
        {
            UpdateGame();
        }
        else
        {
            UpdateCountDown();
        }


    }

    void EndGame()
    {
        countDownUntilNewGame = newGameDelay;
        countDownText.SetText("Game Over");
        countDownText.gameObject.SetActive(true);
        ball.EndGame();
    }

    private void UpdateCountDown()
    {
        countDownUntilNewGame -= Time.deltaTime;
        if (countDownUntilNewGame <= 0f)
        {
            countDownText.gameObject.SetActive(false);
            StartNewGame();
        }
        else
        {
            float displayValue = Mathf.Ceil(countDownUntilNewGame);
            if (displayValue < newGameDelay)
            {
                countDownText.SetText("{0}", displayValue);
            }
        }
        countDownText.SetText("{0}", countDownUntilNewGame);
    }

    private void UpdateGame()
    {
        ball.Move();
        BounceYIfNeeded();
        BounceXIfNeeded(ball.Position.x);
        ball.UpdateVisualization();
    }

    private void BounceXIfNeeded(float x)
    {
        //小于负范围或者大于正的范围
        float xExtents = arenaExtnets.x - ball.Extents;//10f
        if (x < -xExtents)
        {
            livingCamera.PushXZ(ball.Velocity);
            ball.BounceX(-xExtents);
        }
        else if (x > xExtents)
        {
            livingCamera.PushXZ(ball.Velocity);
            ball.BounceX(xExtents);
        }
    }

    private void BounceYIfNeeded()
    {
        //小于负范围或者大于正的范围
        float yExtents = arenaExtnets.y - ball.Extents;//10f
        if (ball.Position.y < -yExtents)
        {
            BounceY(-yExtents,bottomPaddle,topPaddle);
           
        }
        else if (ball.Position.y > yExtents)
        {
            BounceY(yExtents, topPaddle,bottomPaddle);
        }
    }

    private void BounceY(float boundary,Paddle defender,Paddle attacker)
    {   //确定反弹发生了多久（忽略了paddle的厚度）
        float durationAfterBounce = (ball.Position.y - boundary) / ball.Velocity.y;
        //计算发生弹跳时候球的位置
        float bounceX = ball.Position.x - ball.Velocity.x * durationAfterBounce;

        //先计算X反弹来防止穿出边界
        BounceXIfNeeded(bounceX);
        bounceX = ball.Position.x - ball.Velocity.x * durationAfterBounce;
        livingCamera.PushXZ(ball.Velocity);
        ball.BounceY(boundary);

        if (defender.HitBall(bounceX, ball.Extents, out float hitFactor))
        {
            ball.SetXPositionAndSpeed(bounceX, hitFactor, durationAfterBounce);
        }
        else
        {
            livingCamera.JostleY();
            if (attacker.ScorePoint(pointsToWin))
            {
                EndGame();
            }
        }
       
        
    }
}
