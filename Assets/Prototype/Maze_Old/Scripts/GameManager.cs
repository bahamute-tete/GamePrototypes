using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public Maze_Old mazePrefab;

    private Maze_Old mazeInstance;
    // Start is called before the first frame update
    void Start()
    {
        BeginGame();
    }

    private void BeginGame()
    {
        mazeInstance = Instantiate(mazePrefab);
        StartCoroutine( mazeInstance.Generate());
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            RestartGame();
        }
    }

    private void RestartGame()
    {
        StopAllCoroutines();
        Destroy(mazeInstance.gameObject);
        BeginGame();
    }
}
