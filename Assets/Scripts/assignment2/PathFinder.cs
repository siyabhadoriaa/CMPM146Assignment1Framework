using UnityEngine;
using System.Collections.Generic;

public class PathFinder : MonoBehaviour
{
    public static (List<Vector3>, int) AStar(GraphNode start, GraphNode destination, Vector3 target)
    {
        var openSet = new PriorityQueue<AStarEntry>();
        var entriesById = new Dictionary<int, AStarEntry>();

        var startEntry = new AStarEntry(start, null, null, 0f, Heuristic(start, target));
        entriesById[start.GetID()] = startEntry;
        openSet.Enqueue(startEntry, startEntry.FScore);

        int expanded = 0;

        while (openSet.Count > 0)
        {
            AStarEntry current = openSet.Dequeue();
            expanded++;

            if (current.Node == destination)
            {
                return (ReconstructPath(current, target), expanded);
            }

            foreach (var neighbor in current.Node.GetNeighbors())
            {
                GraphNode neighborNode = neighbor.GetNode();
                int neighborID = neighborNode.GetID();

                float tentativeG = current.GScore + Vector3.Distance(current.Node.GetCenter(), neighborNode.GetCenter());

                if (!entriesById.TryGetValue(neighborID, out AStarEntry neighborEntry) || tentativeG < neighborEntry.GScore)
                {
                    float fScore = tentativeG + Heuristic(neighborNode, target);
                    var newEntry = new AStarEntry(neighborNode, current, neighbor, tentativeG, fScore);
                    entriesById[neighborID] = newEntry;
                    openSet.Enqueue(newEntry, fScore);
                }
            }
        }

        return (new List<Vector3>() { target }, expanded); // fallback
    }

    static float Heuristic(GraphNode node, Vector3 target)
    {
        return Vector3.Distance(node.GetCenter(), target);
    }

    static List<Vector3> ReconstructPath(AStarEntry endEntry, Vector3 target)
    {
        List<Vector3> path = new List<Vector3> { target };
        AStarEntry current = endEntry;

        while (current.CameFrom != null)
        {
            path.Insert(0, current.FromNeighbor.GetWall().midpoint);
            current = current.CameFrom;
        }

        return path;
    }

    private class AStarEntry
    {
        public GraphNode Node;
        public AStarEntry CameFrom;
        public GraphNeighbor FromNeighbor;
        public float GScore;
        public float FScore;

        public AStarEntry(GraphNode node, AStarEntry cameFrom, GraphNeighbor fromNeighbor, float gScore, float fScore)
        {
            Node = node;
            CameFrom = cameFrom;
            FromNeighbor = fromNeighbor;
            GScore = gScore;
            FScore = fScore;
        }
    }

    public Graph graph;

    void Start()
    {
        EventBus.OnTarget += PathFind;
        EventBus.OnSetGraph += SetGraph;
    }

    void Update() { }

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
                start = n;

            if (Util.PointInPolygon(target, n.GetPolygon()))
                destination = n;
        }

        if (destination != null)
        {
            EventBus.ShowTarget(target);
            (List<Vector3> path, int expanded) = PathFinder.AStar(start, destination, target);
            Debug.Log("found path of length " + path.Count + " expanded " + expanded + " nodes, out of: " + graph.all_nodes.Count);
            EventBus.SetPath(path);
        }
    }

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

