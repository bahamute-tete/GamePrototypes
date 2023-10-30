using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public class Game : MonoBehaviour
{
    [SerializeField]
    Runner runner;

    [SerializeField]
    TrackingCamera trackingCamera;

    [SerializeField]
    TextMeshPro displayText;

    //默认设置为匹配每秒 120 帧并使用它来运行
    [SerializeField, Min(0.0001f)]
    float maxDeltaTime = 1f / 120f;


    bool isPlaying;

    [SerializeField]
    SkylineGenerator[] skylineGenerators;

    [SerializeField]
    SkylineGenerator obstacleGenerator;


    [SerializeField]
    float extraGapFactor = 0.5f, extraSequenceFactor = 1f;


    void StartNewGame()
    {
        trackingCamera.StartNewGame();
        runner.StartNewGame(obstacleGenerator.StartNewGame(trackingCamera));
        trackingCamera.Track(runner.Position);


        for (int i = 0; i < skylineGenerators.Length; i++)
        {
            skylineGenerators[i].StartNewGame(trackingCamera);
        }

        isPlaying = true;
       
    }

    void Update()
    {
       
        if (isPlaying)
        {
            UpdateGame();
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            StartNewGame();
        }


    }

    private void UpdateGame()
    {

        if (Input.GetKeyDown(KeyCode.Space))
        {
            runner.StartJumping();
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            runner.EndJumping();
        }


        float accumulateDeltaTime = Time.deltaTime;
        while(accumulateDeltaTime > maxDeltaTime && isPlaying) 
        {
            isPlaying = runner.Run(maxDeltaTime);
            accumulateDeltaTime -= Time.deltaTime;  
        }

        //果跑步者不再活跃，我们也可以立即停止
        isPlaying = isPlaying && runner.Run(accumulateDeltaTime);
        runner.UpdateVisualiztion();
        trackingCamera.Track(runner.Position);
        displayText.SetText("{0}", Mathf.Floor(runner.Position.x));

        obstacleGenerator.FillView(trackingCamera,
                                    runner.SpeedX*extraGapFactor,
                                    runner.SpeedX*extraSequenceFactor
            );

        for (int i = 0; i < skylineGenerators.Length; i++)
        {
            skylineGenerators[i].FillView(trackingCamera);
        }
    }
}
