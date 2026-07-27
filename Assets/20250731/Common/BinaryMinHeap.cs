namespace LiangZhu.Geometry
{
    /// <summary>
    /// 二叉最小堆,(node, dist) 项。Unity 的 .NET Standard 2.1 没有 PriorityQueue&lt;,&gt;,
    /// 这里自己实现。不做 decrease-key,改用 lazy deletion:允许重复入堆,
    /// 出堆时由调用方比对 dist[node] 跳过 stale 项。内部数组复用,跨 query 不重分配。
    /// </summary>
    public sealed class BinaryMinHeap
    {
        private int[] _node;
        private float[] _dist;
        private int _count;

        public BinaryMinHeap(int capacity = 256)
        {
            if (capacity < 1) capacity = 1;
            _node = new int[capacity];
            _dist = new float[capacity];
            _count = 0;
        }

        public int Count => _count;
        public void Clear() => _count = 0;

        public void Push(int node, float dist)
        {
            if (_count == _node.Length) Grow();
            int i = _count++;
            _node[i] = node;
            _dist[i] = dist;
            // sift up
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_dist[p] <= _dist[i]) break;
                Swap(i, p);
                i = p;
            }
        }

        public bool TryPop(out int node, out float dist)
        {
            if (_count == 0) { node = -1; dist = 0f; return false; }
            node = _node[0];
            dist = _dist[0];
            _count--;
            if (_count > 0)
            {
                _node[0] = _node[_count];
                _dist[0] = _dist[_count];
                SiftDown(0);
            }
            return true;
        }

        private void SiftDown(int i)
        {
            while (true)
            {
                int l = (i << 1) + 1;
                int r = l + 1;
                int smallest = i;
                if (l < _count && _dist[l] < _dist[smallest]) smallest = l;
                if (r < _count && _dist[r] < _dist[smallest]) smallest = r;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            int tn = _node[a]; _node[a] = _node[b]; _node[b] = tn;
            float td = _dist[a]; _dist[a] = _dist[b]; _dist[b] = td;
        }

        private void Grow()
        {
            int n = _node.Length << 1;
            System.Array.Resize(ref _node, n);
            System.Array.Resize(ref _dist, n);
        }
    }
}
