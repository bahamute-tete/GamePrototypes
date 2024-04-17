using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public class Shape : PresistableObject
{
    static int colorPropertyID = Shader.PropertyToID("_Color");
    static MaterialPropertyBlock sharedPropertyBlock;
    public int MaterialID { get; private set; }

    public int ShapeID
    {
        get { return shapeID; }
        set {
            if (shapeID == int.MinValue && value != int.MinValue)
                shapeID = value;
            else
                Debug.LogError("Not allow to change shapeID");
        }
    }

    int shapeID = int.MinValue;

    MeshRenderer meshRenderer;


    public Vector3 AngularVelocity { get; set; }

    public Vector3 Velocity { get; set; }
    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    public void SetMaterial(Material material, int materialID)
    {
        meshRenderer.material = material;
        MaterialID =materialID;
    }

    Color color;
    public void SetColor(Color color) {
        this.color = color;

        if (sharedPropertyBlock == null)
            sharedPropertyBlock = new MaterialPropertyBlock();

        sharedPropertyBlock.SetColor(colorPropertyID, color);
        meshRenderer.SetPropertyBlock(sharedPropertyBlock);
    }


    public override void Save(GameDataWriter writer)
    {
        base.Save(writer);
        writer.Write(color);
        writer.Write(AngularVelocity);
        writer.Write(Velocity);
    }

    public override void Load(GameDataReader reader)
    {
        base.Load(reader);
        SetColor(reader.Version>0? reader.ReadColor():Color.white);
        AngularVelocity = reader.Version >= 4 ? reader.ReadVector3() : Vector3.zero;
        Velocity = reader.Version >=4? reader.ReadVector3() : Vector3.zero;
    }

    public void GameUpdate()
    {
        transform.Rotate(AngularVelocity * Time.deltaTime);
        transform.localPosition += Velocity * Time.deltaTime;
    }

}
