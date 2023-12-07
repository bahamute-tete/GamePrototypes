using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkylineObject : MonoBehaviour
{
    [SerializeField, Min(1f)]
    float extents;

    public float MaxX => transform.localPosition.x + extents;

    //连接在这个物体后面的那个物体
    public SkylineObject Next { get; set; }


    //池子
    [System.NonSerialized]
    Stack<SkylineObject> pool;

    //����һ���컨��==>ͨ������һ����ֱ��϶�Ŀ����÷�Χ
    [SerializeField]
    FloatRange gapY;

    public FloatRange GapY => gapY.Shift(transform.localPosition.y);
    public Vector3 PlaceAfter(Vector3 position)
    {//先把中心点挪半个范围，然后将物体挪到这个位置，最后挪半个范围，使最后的点在物体的后方
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
        //�����Ҫ������һ���³أ�
        //Ȼ���Դӳ��е���һ��ʵ������������
        //���ʵ�������ã���ᴴ��һ����ʵ����������ء�
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
