using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BatchInstance : MonoBehaviour
{

    public GameObject[] prefabs;
    public int instanceCount = 10;
    public int column=2;
    public float spacing = 2.0f;

    // Start is called before the first frame update
    void Start()
    {
        GreateInstancePrefab();
    }

    private void GreateInstancePrefab()
    {
        for (int i = 0; i < instanceCount; i++)
        { 
            var prefab = prefabs[i % prefabs.Length];
            var  instance = Instantiate(prefab);
            instance.transform.parent = transform;
            instance.transform.localScale = Vector3.one;
            int row = i / column;
            int col = i % column;
            instance.transform.localPosition = new Vector3(col * spacing, row * spacing,0f );
            instance.transform.rotation = Quaternion.identity;
            instance.name = prefab.name + "_" + i;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
