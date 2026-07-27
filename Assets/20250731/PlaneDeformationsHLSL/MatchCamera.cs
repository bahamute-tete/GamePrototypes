using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI;
using Button = UnityEngine.UIElements.Button;
using EnumField = UnityEngine.UIElements.EnumField;

public enum DeformationType
{
    DEFORMED_ONE = 1,
    DEFORMED_TWO = 2,
    DEFORMED_THREE = 3,
    DEFORMED_FOUR = 4,
    DEFORMED_FIVE = 5,
    DEFORMED_SIX = 6,
    DEFORMED_SEVEN = 7,
    DEFORMED_EIGHT = 8,
    DEFORMED_NINE = 9,
    DEFORMED_TEN = 10,
    DEFORMED_ELEVEN = 11,

}

[RequireComponent(typeof(MeshFilter))]
public class MatchCamera : MonoBehaviour
{
    Camera camera;
    Mesh mesh;
    Vector3[] nearClipPlanePointsCS = new Vector3[4];

    public Shader shader;
    public Texture2D texture;
    private Material material;

    //private Button button;
    //private TextField textField;
    //private int _clickCount = 0;
    private EnumField enumField;

    private DeformationType currentDeformationType = DeformationType.DEFORMED_ONE;

    private void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();

        enumField = uiDocument.rootVisualElement.Q<EnumField>("enumField") as EnumField;
        if (enumField != null)
        {
            enumField.Init(currentDeformationType);
            enumField.value = currentDeformationType;
            enumField.RegisterValueChangedCallback(OnDeformationTypeChanged);
        }

        //button = uiDocument.rootVisualElement.Q<Button>("testBtn") as Button;
        //button.RegisterCallback<ClickEvent>( OnButtonClick);
        ////button.clicked += OnButtonClick;

        //textField = uiDocument.rootVisualElement.Q<TextField>("textField") as TextField;
        //textField.value = _clickCount.ToString();

       


    }



    private void OnDeformationTypeChanged(ChangeEvent<Enum> evt)
    {
        currentDeformationType = (DeformationType)evt.newValue;
        if (material != null)
        {
            material.SetTexture("_MainTex", texture);
            material.SetInt("_DeformationType", (int)currentDeformationType);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (shader is null) return;

        if (material is null) material = new Material(shader);
        
        material.SetInt("_DeformationType", (int)currentDeformationType);

        if (camera is null)
        {
            camera = Camera.main;
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearClipPlanePointsCS);
        }

        if (nearClipPlanePointsCS.Length != 0)
        {
            mesh = new Mesh();
           
            mesh.vertices = nearClipPlanePointsCS;
            mesh.triangles = new int[] { 0, 1, 2, 2, 3, 0 };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0)
            };
            mesh.RecalculateNormals();
            
            var meshFilter = gameObject.GetComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            transform.position = camera.transform.position;
            transform.rotation = camera.transform.rotation;
        }

        transform.GetComponent<MeshRenderer>().material = material;
        material.SetTexture("_MainTex", texture);
        


    }

    // Update is called once per frame
    void Update()
    {
        
        if (camera != null)
        {
            transform.position = camera.transform.position;
            transform.rotation = camera.transform.rotation;
        }
    }

    private void OnValidate() {
        if (material != null)
        {
            material.SetTexture("_MainTex", texture);
        }
    }
}
