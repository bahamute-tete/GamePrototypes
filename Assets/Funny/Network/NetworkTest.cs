using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkTest : MonoBehaviour
{


    NetworkManager manager;
    // Start is called before the first frame update
    void Start()
    {
        
        manager  = GetComponent<NetworkManager>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
