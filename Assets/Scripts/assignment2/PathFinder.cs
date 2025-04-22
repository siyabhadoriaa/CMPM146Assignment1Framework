using UnityEngine;
using System.Collections.Generic;

public class PathFinder : MonoBehaviour
{
    public static (List<Vector3>, int) AStar(GraphNode start, GraphNode destination, Vector3 target)
    {
        var openSet = new PriorityQueue<GraphNode>();
        var cameFrom = new Dictionary<GraphNode, GraphNode>();
        var gScore = new Dictionary<GraphNode, float>();
        var fScore = new Dictionary<GraphNode, float>();
        var visited = new HashSet<GraphNode>();

        gScore[start] = 0f;
        fScore[start] = Heuristic(start, target);

        openSet.Enqueue(start, fScore[start]);
        int expanded = 0;

        while (openSet.Count > 0)
        {
            GraphNode current = openSet.Dequeue();
            expanded++;

            if (current == destination)
            {
                return (ReconstructPath(cameFrom, current, target), expanded);
            }

            visited.Add(current);

            foreach (var neighbor in current.GetNeighbors())
            {
                GraphNode neighborNode = neighbor.GetNode();
                if (visited.Contains(neighborNode))
                    continue;

                float tentative_gScore = gScore[current] + Vector3.Distance(current.GetCenter(), neighborNode.GetCenter());

                if (!gScore.ContainsKey(neighborNode) || tentative_gScore < gScore[neighborNode])
                {
                    cameFrom[neighborNode] = current;
                    gScore[neighborNode] = tentative_gScore;
                    fScore[neighborNode] = tentative_gScore + Heuristic(neighborNode, target);
                    openSet.Enqueue(neighborNode, fScore[neighborNode]);
                }
            }
        }

        return (new List<Vector3>() { target }, expanded);
    }

    static float Heuristic(GraphNode node, Vector3 target)
    {
        return Vector3.Distance(node.GetCenter(), target);
    }


    static List<Vector3> ReconstructPath(Dictionary<GraphNode, GraphNode> cameFrom, GraphNode current, Vector3 target)
    {
        var path = new List<Vector3> { target };

        while (cameFrom.ContainsKey(current))
        {
            path.Insert(0, current.GetCenter());  // Use GetCenter() to access the 'center' of the node
            current = cameFrom[current];
        }

        path.Insert(0, current.GetCenter()); // include the start node
        return path;
    }



    public Graph graph;

    void Start()
    {
        EventBus.OnTarget += PathFind;
        EventBus.OnSetGraph += SetGraph;
    }

    void Update()
    {
        // no logic needed here
    }

    public void SetGraph(Graph g)
    {
        graph = g;
    }

    public void PathFind(Vector3 target)
    {
        if (graph == null) return;

        GraphNode start = null;
        GraphNode destination = null;

        foreach (var n in graph.all_nodes)
        {
            if (Util.PointInPolygon(transform.position, n.GetPolygon()))
            {
                start = n;
            }
            if (Util.PointInPolygon(target, n.GetPolygon()))
            {
                destination = n;
            }
        }

        if (destination != null)
        {
            EventBus.ShowTarget(target);
            (List<Vector3> path, int expanded) = PathFinder.AStar(start, destination, target);
            Debug.Log("found path of length " + path.Count + " expanded " + expanded + " nodes, out of: " + graph.all_nodes.Count);
            EventBus.SetPath(path);
        }
    }

    // Internal Priority Queue class
    private class PriorityQueue<T>
    {
        private List<(T item, float priority)> elements = new List<(T, float)>();
        public int Count => elements.Count;

        public void Enqueue(T item, float priority)
        {
            elements.Add((item, priority));
        }

        public T Dequeue()
        {
            int bestIndex = 0;
            float bestPriority = elements[0].priority;

            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i].priority < bestPriority)
                {
                    bestPriority = elements[i].priority;
                    bestIndex = i;
                }
            }

            T bestItem = elements[bestIndex].item;
            elements.RemoveAt(bestIndex);
            return bestItem;
        }
    }
}
