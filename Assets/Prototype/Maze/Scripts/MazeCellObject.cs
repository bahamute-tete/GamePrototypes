using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeCellObject : MonoBehaviour
{

#if UNITY_EDITOR
    static List<Stack<MazeCellObject>> pools;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Clear()
    {
        if (pools == null)
        {
            pools = new();
        }
        else
        {
            for (int i = 0; i < pools.Count; i++)
            {
                pools[i].Clear();
            }
        }
    }
#endif

    //每个实例都有一个对象池
    [NonSerialized]
    Stack<MazeCellObject> pool;

    public MazeCellObject GetInstance()
    {
        if (pool == null)
        {
            pool = new Stack<MazeCellObject>();
#if UNITY_EDITOR
            pools.Add(pool);
#endif
        }

        if (pool.TryPop(out MazeCellObject instance))
        {
            instance.gameObject.SetActive(true);
        }
        else
        {   
            instance = Instantiate(this);
            //将实例化出来的池子设置给新的实例
            instance.pool = pool;
        }

        return instance;
    }

    public void Recycle()
    { 
        pool.Push(this);
        gameObject.SetActive(false);
    }
}
