using System.Collections;
using System.Collections.Generic;
using TreeEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.Experimental.GraphView.Port;


namespace CameraCullingGPU
{
    public class QuadTree
    {
        public class QuadTreeNode
        {
            public Rect bounds;
            public QuadTreeNode[] children;
            public List<Vector3> positions;
            public bool divided;



            public QuadTreeNode(Rect bounds)
            {
                this.bounds = bounds;
                children = new QuadTreeNode[4];
                positions =new List<Vector3>();
                divided = false;
            }
        }

        private QuadTreeNode root;
        private int capacity;
        private List<GameObject> lrContent = new List<GameObject>();
        private GameObject LrContetParent = new GameObject("ContentForDeug");
       

        public QuadTree(Rect rootRect, int capacity)
        {
            this.capacity = capacity;
            root = new QuadTreeNode(rootRect);
        }

        public void Clear()
        {
            Clear(root);        
        }
        private void Clear(QuadTreeNode node)
        {
            node.positions.Clear();

            for (int i = 0; i < node.children.Length; i++)
            {
                if (node.divided)
                {
                    Clear(node.children[i]);
                    node.children[i] = null;
                }
                node.divided = false;
            }


        }

        public void Insert(Vector3 p)
        { 
            Insert(root, p);
        }

        private void Insert(QuadTreeNode node, Vector3 p)
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

                //foreach (var child in node.children)
                //{
                //    Insert(child, p);


                //}

                for (int i = 0; i < node.children.Length; i++)
                {
                    if (node.children[i].bounds.Contains(p))
                    {
                        Insert(node.children[i], p);
                        break;  // Break after inserting into the appropriate child
                    }
                }
            }

        }

        private void SplitNode(QuadTreeNode node)
        {
            float subWidth = node.bounds.width / 2;
            float subHeight = node.bounds.height / 2;
            float xc = node.bounds.x + node.bounds.width / 2;
            float yc = node.bounds.y + node.bounds.height / 2;

            Rect ne = new(xc, yc, subWidth, subHeight);
            node.children[3] = new QuadTreeNode(ne);

            Rect nw = new Rect(xc - subWidth, yc, subWidth, subHeight);
            node.children[2] = new QuadTreeNode(nw);

            Rect se = new Rect(xc, yc - subHeight, subWidth, subHeight);
            node.children[1] = new QuadTreeNode(se);


            Rect sw = new Rect(xc - subWidth, yc - subHeight, subWidth, subHeight);
            node.children[0] = new QuadTreeNode(sw);

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

        public void QueryRange(Rect range, List<Vector3> found)
        {
            QueryRange( root,  range,  found);
        }
         private void QueryRange(QuadTreeNode node, Rect range, List<Vector3> found)
        {
            if (!node.bounds.Overlaps(range))
            {
                return;
            }
            else
            {

                for (int i = 0; i < node.positions.Count; i++)
                {
                    if (range.Contains(node.positions[i]))
                    {
                        found.Add(node.positions[i]);
                    }
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

        public void Show()
        {
            Show(root);
        }
        private void Show(QuadTreeNode node)
        {
            LineRenderer lr = new LineRenderer();
            GameObject go = new GameObject("BoundaryDebug");
            lr = go.AddComponent<LineRenderer>();
            go.transform.SetParent(LrContetParent.transform);
            lrContent.Add(go);



            lr.positionCount = 5;
            lr.startWidth = lr.endWidth = 0.01f;
            lr.startColor = lr.endColor = Color.white;
            lr.material = new Material(Shader.Find("Particles/Standard Unlit"));

            Vector3 v1 = new Vector3(node.bounds.x, node.bounds.y, 0);
            Vector3 v2 = new Vector3(node.bounds.x, node.bounds.y + node.bounds.height, 0);
            Vector3 v3 = new Vector3(node.bounds.x + node.bounds.width, node.bounds.y + node.bounds.height, 0);
            Vector3 v4 = new Vector3(node.bounds.x + node.bounds.width, node.bounds.y, 0);

            lr.SetPosition(0, v1);
            lr.SetPosition(1, v2);
            lr.SetPosition(2, v3);
            lr.SetPosition(3, v4);
            lr.SetPosition(4, v1);


            if (node.divided)
            {
                Show(node.children[0]);
                Show(node.children[1]);
                Show(node.children[2]);
                Show(node.children[3]);
            }
        }

    }



}
