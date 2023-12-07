using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MicropTest : MonoBehaviour
{
    AudioSource audioSource => GetComponent<AudioSource>();
    AudioClip clip;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            clip = Microphone.Start(Microphone.devices[0], true, 10, AudioSettings.outputSampleRate);
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}
