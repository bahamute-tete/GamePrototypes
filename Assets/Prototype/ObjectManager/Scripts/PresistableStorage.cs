using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PresistableStorage : MonoBehaviour
{
    string savePath;

    private void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "saveFile");
        Debug.Log(savePath);    
    }
    // Start is called before the first frame update
    public void Save(PresistableObject o,int version)
    {
        using (var writer = new BinaryWriter (File.Open(savePath,FileMode.Create)))
        {
            writer.Write (-version);
            o.Save(new GameDataWriter(writer));
        }
    }

    public void Load(PresistableObject o)
    {
        //using (var reader = new BinaryReader(File.Open(savePath, FileMode.Open)))
        //{
        //    o.Load(new GameDataReader(reader,-reader.ReadInt32()));
        //}

        byte[] data = File.ReadAllBytes(savePath);
        var reader = new BinaryReader(new MemoryStream(data));
        o.Load(new GameDataReader(reader, -reader.ReadInt32()));

    }
}
