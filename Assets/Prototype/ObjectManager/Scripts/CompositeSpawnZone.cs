using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CompositeSpawnZone : SpawnZone
{
    [SerializeField]
    SpawnZone[] spawnZones;

    [SerializeField]
    bool sequential;
    int nextsequentialIndex;
    public override Vector3 SpawnPoint
    {

        get {
            int index;
            if (sequential)
            {
                index = nextsequentialIndex++;
                if (nextsequentialIndex >= spawnZones.Length)
                {
                    nextsequentialIndex = 0;
                }
            }
            else
            {
                index = Random.Range(0, spawnZones.Length);
            }

          
            return spawnZones[index].SpawnPoint;
        }
    }

    [SerializeField]
    bool overrideConfig;

    public override void Save(GameDataWriter writer)
    {
        writer.Write(nextsequentialIndex);
    }

    public override void Load(GameDataReader reader)
    {
        nextsequentialIndex = reader.ReadInt();
    }

    public override void ConfigureSpawn(Shape shape)
    {
        if (overrideConfig)
        {
            base.ConfigureSpawn(shape);
        }
        else
        {
            int index;
            if (sequential)
            {
                index = nextsequentialIndex++;
                if (nextsequentialIndex >= spawnZones.Length)
                {
                    nextsequentialIndex = 0;
                }
            }
            else
            {
                index = Random.Range(0, spawnZones.Length);
            }


            spawnZones[index].ConfigureSpawn(shape);
        }
    }
}
