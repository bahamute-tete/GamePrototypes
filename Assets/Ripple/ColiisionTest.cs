using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColiisionTest : MonoBehaviour
{
    private int waveNumber;
    public float distanceX,distanceZ;
    public float[] waveAmplitude;
    public float magnitudeDivider;
    Renderer renderer;

    Mesh mesh;
    // Start is called before the first frame update
    void Start()
    {
        renderer = GetComponent<Renderer>();
        mesh = GetComponent<MeshFilter>().mesh;
    }

    // Update is called once per frame
    void Update()
    {
        //for (int i = 0; i < 8; i++)
        //{
        //    waveAmplitude[i] = renderer.material.GetFloat("_WaveAmp" + (i + 1));
        //    if (waveAmplitude[i] > 0.0f)
        //    {
        //        renderer.material.SetFloat("_WaveAmp" + (i + 1), waveAmplitude[i] * 0.98f);
        //    }

        //    if (waveAmplitude[i] < 0.05f)
        //    {
        //        renderer.material.SetFloat("_WaveAmp" + (i + 1), 0);
        //    }
        //}
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.rigidbody)
        {
            waveNumber++;
            if (waveNumber == 9)
            {
                waveNumber = 1;
            }

            waveAmplitude[waveNumber - 1] = 0;

            distanceX = this.transform.position.x -collision.gameObject.transform.position.x;

            distanceZ = this.transform.position.z - collision.gameObject.transform.position.z;

            renderer.material.SetFloat("_WaveX"+ waveNumber,distanceX/mesh.bounds.size.x*2.5f);
            renderer.material.SetFloat("_WaveZ" + waveNumber, distanceZ / mesh.bounds.size.z*2.5f);

            renderer.material.SetFloat("_WaveAmp" + waveNumber, collision.rigidbody.velocity.magnitude * magnitudeDivider);
        }
    }
}
