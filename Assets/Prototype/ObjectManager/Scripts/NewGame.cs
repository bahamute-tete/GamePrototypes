using System;
using System.Collections;
using System.Collections.Generic;
using TMPro.EditorUtilities;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using Random = UnityEngine.Random;
using UnityEngine.UI;

public class NewGame : PresistableObject
{

    
    const int saveVersion = 4;

    [SerializeField] ShapeFactory shapeFactory;

    public KeyCode createKey = KeyCode.C;
    public KeyCode newGameKey = KeyCode.N;
    public KeyCode saveKey = KeyCode.S;
    public KeyCode loadKey = KeyCode.L;
    public KeyCode deletKey = KeyCode.X;


    List<Shape> shapes;

    public PresistableStorage storage;


    public float CreationSpeed { get; set; }
    float creationProgress;

    public float DestructionSpeed { get; set; }
    float destructionProgress;

    public int levelCount;
    int loadedLevelBuildIndex;


    Random.State mainRandomState;
    [SerializeField] bool reseedOnLoad;

    [SerializeField] Slider creationSpeedSlider;
    [SerializeField] Slider destructionSpeedSlider;
    private void Start()
    {
        Random.state = mainRandomState;
        int seed = Random.Range(0, int.MaxValue)^ (int)Time.unscaledTime;
        mainRandomState = Random.state;
        Random.InitState(seed);

        shapes = new List<Shape>();
        if (Application.isEditor)
        {
            //Scene loadedLevel = SceneManager.GetSceneByName("Level1");
            //if (loadedLevel.isLoaded)
            //{
            //    SceneManager.SetActiveScene(loadedLevel);
            //    return;
            //}


            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedLevel = SceneManager.GetSceneAt(i);
                if (loadedLevel.name.Contains("Level"))
                {
                    SceneManager.SetActiveScene(loadedLevel);
                    loadedLevelBuildIndex = loadedLevel.buildIndex;
                    return;
                }
            }
        }

        BeginNewGame();
        StartCoroutine(LoadLevel(1));
       
       
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(createKey))
        {
            CreateObject();
        }
        else if (Input.GetKey(newGameKey))
        {
            BeginNewGame();
            StartCoroutine(LoadLevel(loadedLevelBuildIndex));
        }
        else if (Input.GetKeyDown(saveKey))
        {
            storage.Save(this, saveVersion);
        }
        else if (Input.GetKeyDown(loadKey))
        {
            BeginNewGame();
            storage.Load(this);
        }
        else if (Input.GetKeyDown(deletKey))
        {
            DestroyShape();
        }
        else
        {
            for (int i = 0; i < levelCount; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    BeginNewGame();
                    StartCoroutine(LoadLevel(i));
                    return;
                }
            }
        }
    }

    private void FixedUpdate()
    {
        for (int i = 0;i<shapes.Count;i++)
        {
            shapes[i].GameUpdate();
        }

       creationProgress += Time.deltaTime * CreationSpeed;

        while (creationProgress >= 1f)
        {
          
            creationProgress -= 1f;
            CreateObject();
        }

        destructionProgress += Time.deltaTime * DestructionSpeed;

        while (destructionProgress >= 1f)
        {
           
            destructionProgress -= 1f;
            DestroyShape();
        }
    }

    IEnumerator LoadLevel(int levelBuildIndex)
    {
        enabled = false;
        if (loadedLevelBuildIndex > 0)
        { 
            yield return SceneManager.UnloadSceneAsync(loadedLevelBuildIndex);
        }
        //SceneManager.LoadScene("Level1",LoadSceneMode.Additive);
        yield return SceneManager.LoadSceneAsync(levelBuildIndex, LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(levelBuildIndex));
        loadedLevelBuildIndex = levelBuildIndex;
        enabled = true;
    }
    public override void Save(GameDataWriter writer)
    {
        
        writer.Write(shapes.Count);
        writer.Write(Random.state);
        writer.Write(CreationSpeed);
        writer.Write(creationProgress);
        writer.Write(DestructionSpeed);
        writer.Write(destructionProgress);
        writer.Write(loadedLevelBuildIndex);
        GameLevel.Current.Save(writer);
        for (int i = 0;i< shapes.Count; i++) 
        {
            writer.Write(shapes[i].ShapeID);
            writer.Write(shapes[i].MaterialID);
            shapes[i].Save(writer);  
        }
    }

    public override void Load(GameDataReader reader)
    {

        int version = reader.Version;
        if (version > saveVersion)
        {
            Debug.LogError("Unsupported future save version" + version);
            return;
        }

        StartCoroutine(LoadGame(reader));
    }

    IEnumerator LoadGame(GameDataReader reader)
    {
        int version = reader.Version;
        int count =version<0? -version: reader.ReadInt();

        if (version >= 3)
        {
            Random.State state = reader.ReadRandomState();
            if (!reseedOnLoad)
            {
                Random.state = state;
            }
            creationSpeedSlider.value= CreationSpeed = reader.ReadFloat();
           
            creationProgress = reader.ReadFloat();
            destructionSpeedSlider.value = DestructionSpeed = reader.ReadFloat();
            destructionProgress = reader.ReadFloat();
        }

        yield return  LoadLevel(version < 2 ? 1 : reader.ReadInt());
        if (version >= 3)
        {
            GameLevel.Current.Load(reader);
        }

        for (int i = 0; i < count; i++)
        {
            int shapeID = version>0? reader.ReadInt():0;
            int materialID = version>0? reader.ReadInt():0;
            Shape instance = shapeFactory.Get(shapeID,materialID);
            instance.Load(reader);
            shapes.Add(instance);
        }
    }

    void DestroyShape()
    {
        if (shapes.Count > 0)
        { 
            int index =  Random.Range(0, shapes.Count);
            shapeFactory.Reclaim(shapes[index]);
            int lastIndex = shapes.Count - 1;
            shapes[index] = shapes[lastIndex];
            shapes.RemoveAt(lastIndex);
        
        }
    }
    private void BeginNewGame()
    {
        for (int i = 0; i < shapes.Count; i++)
        {
            shapeFactory.Reclaim(shapes[i]);
        }

        Random.state = mainRandomState;
        int seed = Random.Range(0, int.MaxValue) ^ (int)Time.unscaledTime;
        mainRandomState = Random.state;
        Random.InitState(seed);

        CreationSpeed = 0;
        creationSpeedSlider.value = 0;
        DestructionSpeed = 0;
        destructionSpeedSlider.value = 0;

        shapes.Clear();
    }

    private void CreateObject()
    {
        Shape instance = shapeFactory.GetRandom();
        GameLevel.Current.ConfigureSpawn(instance);
        shapes.Add(instance);
    }
}
