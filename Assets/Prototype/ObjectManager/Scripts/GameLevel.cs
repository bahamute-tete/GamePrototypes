using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameLevel : PresistableObject
{
    [SerializeField]
    SpawnZone spawnZone;

    [SerializeField]
    PresistableObject[] presistableObjects;

    public static GameLevel Current { get; private set; }

    public void ConfigureSpawn(Shape shape) 
    {
        spawnZone.ConfigureSpawn(shape);
    }



    private void OnEnable()
    {
        Current = this;

        if (presistableObjects == null)
        { 
            presistableObjects = new PresistableObject[0];
        }
    }

    public override void Save(GameDataWriter writer)
    {
        writer.Write(presistableObjects.Length);
        for (int i = 0; i < presistableObjects.Length; i++)
        {
            presistableObjects[i].Save(writer);
        }
    }

    public override void Load(GameDataReader reader)
    {
        int saveCount = reader.ReadInt();
        for (int i = 0; i < saveCount; i++)
        {
            presistableObjects[i].Load(reader);
        }
    }
}
