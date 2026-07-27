using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class GameOfLifePatterns : MonoBehaviour 
{
    [Header("RLE Import")]
    [SerializeField] private bool useRLEInput = true;
    [TextArea(3, 10)]
    [SerializeField] private string rleInput = "";
    [SerializeField] private bool useRLEFile = false;
    [SerializeField] private string rleFilePath = "";
    private Vector2Int[] cachedRLEPattern;
    private string lastRLEInput;



    public Vector2Int[] GetCurrentPattern()
    {
        if (useRLEInput)
        {
            if (!useRLEFile)
            {
                if (cachedRLEPattern == null || lastRLEInput != rleInput)
                {
                    cachedRLEPattern = RLEParser.Parse(rleInput);
                    lastRLEInput = rleInput;
                    Debug.Log($"Parsed RLE pattern with {cachedRLEPattern.Length} cells.");
                }

            }
            else
            {
                if (cachedRLEPattern == null || lastRLEInput != rleFilePath)
                {
                    cachedRLEPattern = RLEParser.LoadAndParse(rleFilePath);
                    lastRLEInput = rleFilePath;
                    Debug.Log($"Loaded RLE pattern from file with {cachedRLEPattern.Length} cells.");
                }
            }
        }
       

         return cachedRLEPattern;

       
    }


}
