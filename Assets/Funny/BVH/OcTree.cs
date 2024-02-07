using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CameraCullingGPU
{
    public class Octree
    {
        public class OctreeNode
        {
            public Bounds bounds;
            public OctreeNode[] children;
            public List<Vector3> positions;
            public bool divided;

            public OctreeNode(Bounds bounds)
            {
                this.bounds = bounds;
                children = new OctreeNode[8];
                positions = new List<Vector3>();
                divided = false;
            }


            public bool Overlaps(Bounds bound)
            {
                // 检查一个立方体是否在另一个立方体的右边  
                bool aRightOfB = (bounds.min.x > bound.max.x);
                // 检查一个立方体是否在另一个立方体的左边  
                bool aLeftOfB = (bound.max.x < bound.min.x);
                // 检查一个立方体是否在另一个立方体的下面  
                bool aBelowB = (bound.min.y> bound.max.y);
                // 检查一个立方体是否在另一个立方体的上面  
                bool aAboveB = (bound.max.y < bound.min.y);
                // 检查一个立方体是否在另一个立方体的前面（靠近观察者）  
                bool aFrontOfB = (bound.min.z> bound.max.z);
                // 检查一个立方体是否在另一个立方体的后面（远离观察者）  
                bool aBehindB = (bound.max.z < bound.min.z);

                // 如果以上六个条件中任何一个为真，则两个立方体不重叠  
                return !(aRightOfB || aLeftOfB || aBelowB || aAboveB || aFrontOfB || aBehindB);
            }
            
        }

        private OctreeNode root;
        private int capacity;

        private List<GameObject> lrContent = new List<GameObject>();
        private GameObject LrContetParent = new GameObject("ContentForDebug");

        public Octree(Bounds rootBounds, int capacity)
        {
            this.capacity = capacity;
            root = new OctreeNode(rootBounds);
        }

        public void Clear()
        {
            Clear(root);
        }

        private void Clear(OctreeNode node)
        {
            node.positions.Clear();

            for (int i = 0; i < node.children.Length; i++)
            {
                if (node.divided)
                {
                    Clear(node.children[i]);
                    node.children[i] = null;
                }
            }

            node.divided = false;
        }

        public void Insert(Vector3 p)
        {
            Insert(root, p);
        }

        private void Insert(OctreeNode node, Vector3 p)
        {
            if (!node.bounds.Contains(p)) return;

            if (node.positions.Count < capacity)
            {
                node.positions.Add(p);
            }
            else
            {
                if (!node.divided)
                {
                    SplitNode(node);
                }

                for (int i = 0; i < node.children.Length; i++)
                {
                    if (node.children[i].bounds.Contains(p))
                    {
                        Insert(node.children[i], p);
                        break; // 在插入到适当的子节点后中断  
                    }
                }
            }
        }

        private void SplitNode(OctreeNode node)
        {
            float subWidth = node.bounds.size.x / 2;
            float subHeight = node.bounds.size.y / 2;
            float subDepth = node.bounds.size.z / 2;

            Vector3 center = node.bounds.center;

            Bounds[] childBounds = new Bounds[8];
            childBounds[0] = new Bounds(center + new Vector3(-subWidth, -subHeight, -subDepth), new Vector3(subWidth, subHeight, subDepth)); // 后左下  
            childBounds[1] = new Bounds(center + new Vector3(subWidth, -subHeight, -subDepth), new Vector3(subWidth, subHeight, subDepth)); // 后右下  
            childBounds[2] = new Bounds(center + new Vector3(-subWidth, subHeight, -subDepth), new Vector3(subWidth, subHeight, subDepth)); // 后左上  
            childBounds[3] = new Bounds(center + new Vector3(subWidth, subHeight, -subDepth), new Vector3(subWidth, subHeight, subDepth)); // 后右上  
            childBounds[4] = new Bounds(center + new Vector3(-subWidth, -subHeight, subDepth), new Vector3(subWidth, subHeight, subDepth)); // 前左下  
            childBounds[5] = new Bounds(center + new Vector3(subWidth, -subHeight, subDepth), new Vector3(subWidth, subHeight, subDepth)); // 前右下  
            childBounds[6] = new Bounds(center + new Vector3(-subWidth, subHeight, subDepth), new Vector3(subWidth, subHeight, subDepth)); // 前左上  
            childBounds[7] = new Bounds(center + new Vector3(subWidth, subHeight, subDepth), new Vector3(subWidth, subHeight, subDepth)); // 前右上  

            for (int i = 0; i < 8; i++)
            {
                node.children[i] = new OctreeNode(childBounds[i]);
            }

            foreach (var p in node.positions)
            {
                foreach (var child in node.children)
                {
                    Insert(child, p);
                }
            }

            node.positions.Clear();
            node.divided = true;
        }

        public void QueryRange(Bounds range, List<Vector3> found)
        {
            QueryRange(root, range, found);
        }

        private void QueryRange(OctreeNode node, Bounds range, List<Vector3> found)
        {
            if (!node.Overlaps(range))
            {
                return;
            }

            for (int i = 0; i < node.positions.Count; i++)
            {
                if (range.Contains(node.positions[i]))
                {
                    found.Add(node.positions[i]);
                }
            }

            if (node.divided)
            {
                foreach (var child in node.children)
                {
                    QueryRange(child, range, found);
                }
            }
        }



        //public void Show()
        //{
        //    Show(root);
        //}

        //private void Show(OctreeNode node)
        //{
        //    LineRenderer lr = new LineRenderer();
        //    GameObject go = new GameObject("BoundaryDebug");
        //    lr = go.AddComponent<LineRenderer>();
        //    go.transform.SetParent(LrContetParent.transform);
        //    lrContent.Add(go);



        //    lr.positionCount = 5;
        //    lr.startWidth = lr.endWidth = 0.01f;
        //    lr.startColor = lr.endColor = Color.white;
        //    lr.material = new Material(Shader.Find("Particles/Standard Unlit"));

        //    Vector3 v1 = new Vector3(node.bounds.x, node.bounds.y, 0);
        //    Vector3 v2 = new Vector3(node.bounds.x, node.bounds.y + node.bounds.height, 0);
        //    Vector3 v3 = new Vector3(node.bounds.x + node.bounds.width, node.bounds.y + node.bounds.height, 0);
        //    Vector3 v4 = new Vector3(node.bounds.x + node.bounds.width, node.bounds.y, 0);

        //    lr.SetPosition(0, v1);
        //    lr.SetPosition(1, v2);
        //    lr.SetPosition(2, v3);
        //    lr.SetPosition(3, v4);
        //    lr.SetPosition(4, v1);


        //    if (node.divided)
        //    {
        //        Show(node.children[0]);
        //        Show(node.children[1]);
        //        Show(node.children[2]);
        //        Show(node.children[3]);
        //    }
        //}
    }


}
