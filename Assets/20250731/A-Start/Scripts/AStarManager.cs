using System.Collections.Generic;
using System.IO;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Random = UnityEngine.Random;


public class Node
{
    public bool isWalkable;
    public Vector3 worldPos;
    public int gridX;
    public int gridY;


    public int gCost;
    public int hCost;
    public Node parent;

    public Node(bool isWalkable, Vector3 worldPos, int gridX, int gridY)
    {
        this.isWalkable = isWalkable;
        this.worldPos = worldPos;
        this.gridX = gridX;
        this.gridY = gridY;
        
    }

    public int fcost=>gCost+hCost;
}


public class AStarManager : MonoBehaviour
{

    private Node[,] grid;
    public int sizeX=10, sizeY=10;
    public float nodeSize = 1.0f;


    private List<Node> openList ;
    private List<Node> closeList;


    public Transform startPoint;
    public Transform endPoint;


    public List<GameObject> obstacles= new List<GameObject>();

    List<Node> path = new List<Node>();

    GameObject pathGRP;
    // Start is called before the first frame update
    void Start()
    {

        pathGRP = new GameObject("PathGRP");

        CreateGrid();
    }


    // Update is called once per frame
    void Update()
    {
        UpdateGridWalkability();

        path = FindPath(startPoint.position, endPoint.position);

        // Clear previous path visualization
        foreach (Transform t in pathGRP.transform)
        {
            Destroy(t.gameObject);
        }

        if (path != null)
        {
            DrawPath(path);
        }
    }


    private void UpdateGridWalkability()
    {
        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                Node node = grid[x, y];
                bool isWalkable = true;
                if (obstacles.Count != 0)
                {
                    foreach (GameObject obstacle in obstacles)
                    {
                        BoxCollider bbox;
                        if (obstacle.TryGetComponent<BoxCollider>(out var component))
                        {
                            bbox = component;
                        }
                        else
                        {
                            bbox = obstacle.AddComponent<BoxCollider>();
                        }

                        if (bbox.bounds.Contains(node.worldPos))
                        {
                            isWalkable = false;
                            break;
                        }
                    }
                }
                node.isWalkable = isWalkable;
            }
        }
    }



    private void CreateGrid()
    {
        grid = new Node[sizeX, sizeY];

        Vector3 gridBottomLeft = transform.position - Vector3.right * sizeX * nodeSize / 2 - Vector3.forward * sizeY * nodeSize / 2;


     
        

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeY; z++)
            {

                Vector3 nodeWorldPos = gridBottomLeft + Vector3.right * (x * nodeSize + nodeSize / 2) + Vector3.forward * (z * nodeSize + nodeSize / 2);

              

                bool isWalkable = true;

                if (obstacles.Count != 0)
                {
                    if (obstacles.Count != 0)
                    {
                        foreach (GameObject obstacle in obstacles)
                        {
                            BoxCollider bbox;
                            if (obstacle.TryGetComponent<BoxCollider>(out var component))
                            {
                                bbox = component;
                            }
                            else
                            {
                                bbox = obstacle.AddComponent<BoxCollider>();
                            }

                            if (bbox.bounds.Contains(nodeWorldPos))
                            {
                                isWalkable = false;
                                break;
                            }

                        }
                    }
                }

                grid[x, z] = new Node(isWalkable, nodeWorldPos, x, z);

            }
        }

    }


    public List<Node> FindPath(Vector3 startPos, Vector3 endPos)
    {
        Node startNode = GetNodeFromWorldPos(startPos);
       
        Node endNode = GetNodeFromWorldPos(endPos);

        if (startNode == null || endNode == null || !startNode.isWalkable || !endNode.isWalkable)
        {
            return null;
        }


        openList = new List<Node>() { startNode };
        closeList = new List<Node>();


        while (openList.Count > 0)
        {
            Node currentNode = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].fcost < currentNode.fcost || (openList[i].fcost == currentNode.fcost && openList[i].hCost < currentNode.hCost))
                {
                    currentNode = openList[i];
                }
            }

            openList.Remove(currentNode);
            closeList.Add(currentNode);

            if (currentNode == endNode)
            {
                return RetracePath(startNode, endNode);
            }

            foreach (Node neighbor in GetNeightbors(currentNode))
            {
                if (!neighbor.isWalkable || closeList.Contains(neighbor))
                { 
                    continue;
                }

                int newGCost = currentNode.gCost + 1;

                if (!openList.Contains(neighbor))
                {
                    neighbor.hCost = CalculateHCost(neighbor, endNode);
                    neighbor.gCost = newGCost;
                    neighbor.parent = currentNode;
                    openList.Add(neighbor);
                }
               
            }
        }

        return null;
    }

    private int CalculateHCost(Node current, Node target)
    {
        int xDistance = Mathf.Abs(current.gridX - target.gridX);
        int zDistance = Mathf.Abs(current.gridY - target.gridY);
        return xDistance + zDistance;
    }

    private List<Node> GetNeightbors(Node currentNode)
    {
        List<Node> neighbors = new List<Node>();
        //int[] dx = { 1, -1, 0, 0,1,-1,-1,1 };
        //int[] dy = { 0, 0, 1, -1,1,-1,1,-1 };

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int i = 0; i < 4; i++)
        {

            int newX = currentNode.gridX + dx[i];
            int newZ = currentNode.gridY + dy[i];

            if (newX >= 0 && newX < sizeX && newZ >= 0 && newZ < sizeY)
            {
                neighbors.Add(grid[newX, newZ]);
            }
        }

        return neighbors;
    }

    private List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }

        path.Add(startNode);
        path.Reverse();
        return path;
       
    }


    private Node GetNodeFromWorldPos(Vector3 worldPos)
    {

        float percentX = (worldPos.x - transform.position.x + sizeX * nodeSize / 2) / (sizeX * nodeSize);
        float percentZ = (worldPos.z - transform.position.z + sizeY * nodeSize / 2) / (sizeY * nodeSize);

        percentX = Mathf.Clamp01(percentX);
        percentZ = Mathf.Clamp01(percentZ);


        int x = Mathf.RoundToInt((sizeX - 1) * percentX);
        int z = Mathf.RoundToInt((sizeY - 1) * percentZ);
        return grid[x, z];
    }


    private void DrawPath(List<Node> path)
    {
        
        foreach (Node node in path)
        {
            GameObject pathcube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pathcube.transform.position = node.worldPos;
            pathcube.transform.localScale = Vector3.one * (nodeSize - 0.1f);
            pathcube.GetComponent<Renderer>().material.color = Color.cyan;
            Destroy(pathcube.GetComponent<Collider>());
            pathcube.transform.SetParent(pathGRP.transform);
        }
    }







    private void OnDrawGizmos()
    {
        Node[,] DebugGrid = new Node[sizeX, sizeY];
        Vector3 gridBottomLeft = transform.position - Vector3.right * sizeX * nodeSize / 2 - Vector3.forward * sizeY * nodeSize / 2;
        for (int i = 0; i < sizeX; i++)
        {
            for (int j = 0; j < sizeY; j++)
            {
                Vector3 nodeWorldPos = gridBottomLeft + Vector3.right * (i * nodeSize + nodeSize / 2) + Vector3.forward * (j * nodeSize + nodeSize / 2);
                bool isWalkable = true;


                if (obstacles.Count != 0)
                {
                    foreach (GameObject obstacle in obstacles)
                    {
                        BoxCollider bbox;
                        if (obstacle.TryGetComponent<BoxCollider>(out var component))
                        {
                             bbox = component;
                        }
                        else
                        {
                             bbox = obstacle.AddComponent<BoxCollider>();
                        }

                        if (bbox.bounds.Contains(nodeWorldPos))
                        {
                            isWalkable = false;
                        }

                    }
                }
                


                DebugGrid[i, j] = new Node(isWalkable, nodeWorldPos, sizeX, sizeY);

                if (DebugGrid[i, j].isWalkable)
                {
                    Gizmos.color = Color.gray;
                    Gizmos.DrawWireCube(nodeWorldPos, Vector3.one * nodeSize);
                } 
            }
        }
    }
}
