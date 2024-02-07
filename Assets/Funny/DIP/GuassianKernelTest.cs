using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class GuassianKernelTest : MonoBehaviour
{


    // Start is called before the first frame update
    void Start()
    {
        float[] verticla = new float[3] { 0f, 0f, 0f };
        float[] horizontal = new float[3] { 0f, 0f, 0f };
        float sum = 0;
        CreateGaussianKernal(1.0f, out verticla, out horizontal, out sum);

        foreach (var v in verticla)
        {
            Debug.Log("Vertical:" + v);
        }

        foreach (var h in horizontal)
        {
            Debug.Log("horizontal:" + h);
        }


        Debug.Log("sum:" + sum);
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    void CreateGaussianKernal(float sig, out float[] verticla, out float[] horizontal, out float sum)
    {
       
        int m = 3;
        int n = 3;
        verticla = new float[3] { 0f,0f,0f};
        horizontal = new float[3] { 0f, 0f, 0f };

        sum = 0;


        int midr = (m - 1) / 2;
        int midc = (n - 1) / 2;
        //float K = 1f / (2f * PI * Pow(sig, 2));
        float K = 1;

        for (int s = 0; s < m; s++)
        {
            for (int t = 0; t < n; t++)
            {
                float squareR =Mathf.Pow(s - midr, 2) + Mathf.Pow(t - midc, 2);
                float index = -squareR / (2.0f * Mathf.Pow(sig, 2));
                sum += K * Mathf.Exp(index);

                if (t == midc) verticla[s] = K * Mathf.Exp(index);

                if (s == midr) horizontal[t] = K * Mathf.Exp(index);
            }
        }
    }

}
