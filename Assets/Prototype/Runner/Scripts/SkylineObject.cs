using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkylineObject : MonoBehaviour
{
    [SerializeField, Min(1f)]
    float extents;

    public float MaxX => transform.localPosition.x + extents;

    public SkylineObject Next { get; set; }


    [System.NonSerialized]
    Stack<SkylineObject> pool;

    //添加一个天花板==>通过添加一个垂直间隙的可配置范围
    [SerializeField]
    FloatRange gapY;

    public FloatRange GapY => gapY.Shift(transform.localPosition.y);
    public Vector3 PlaceAfter(Vector3 position)
    {
        position.x += extents;
        transform.localPosition = position;
        position.x += extents;
        return position;
    }

#if UNITY_EDITOR
    static List<Stack<SkylineObject>> pools;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ClearPools()
    {
        if (pools == null)
        {
            pools = new List<Stack<SkylineObject>>();
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
    public SkylineObject GetInstance()
    {
        //如果需要，创建一个新池，
        //然后尝试从池中弹出一个实例并激活它。
        //如果实例不可用，则会创建一个新实例并设置其池。
        if (pool == null)
        { 
            pool = new ();
#if UNITY_EDITOR
            pools.Add(pool);
#endif
        }

        if (pool.TryPop(out SkylineObject instance))
        {
            instance.gameObject.SetActive(true);
        }
        else
        {
            instance = Instantiate(this);
            instance.pool = pool;
        }

        return instance;
    }

    public SkylineObject Recycle()
    {
        pool.Push(this);
        gameObject.SetActive(false);
        SkylineObject n = Next;
        Next = null;
        return n;
    }

    public void FillGap(Vector3 position, float gap)
    {
        extents = gap * 0.5f;
        position.x += extents;
        transform.localPosition = position;
    }

    public virtual void Check(Runner runner) { }

}
