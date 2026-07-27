using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class BatchDisslove : MonoBehaviour
{

    private List<Material> m_MaterialList = new List<Material>();
    [Range(-1.1f, 0f)]
    public float m_DissloveVaule;
    //public bool m_RunInEditor;

    // Start is called before the first frame update
    void Start()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {

            Material mat = renderers[i].sharedMaterial;
            if (m_MaterialList.Contains(mat) == false)
            {
                m_MaterialList.Add(mat);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < m_MaterialList.Count; i++)
        {
            Material mat = m_MaterialList[i];
            mat.SetFloat("_CutoffHeight1", m_DissloveVaule);


        }
    }

}
